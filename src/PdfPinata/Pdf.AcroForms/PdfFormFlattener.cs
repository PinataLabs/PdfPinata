using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.Structure;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Turns a form into page content: draws each widget's normal appearance into the page it is on,
/// takes the widgets off the pages, and takes the form out of the document. What
/// <see cref="PdfAcroForm.Flatten()"/> does; <c>docs/specs/form-flattening.md</c> has the decisions.
/// </summary>
/// <remarks>
/// <para>
/// Everything that can be refused is refused before anything is changed, so a refusal leaves the
/// document as it was: a document that cannot be modified, one marked as tagged with no structure
/// tree to tag into, a form holding a signed signature field, and - when the appearances are to be
/// redrawn - a field that has nothing to draw them in.
/// </para>
/// <para>
/// A tagged document is not refused. Each appearance is drawn inside a marked-content sequence and
/// the structure element that held the widget's <c>/OBJR</c> is re-pointed at it, as a <c>/Span</c>;
/// see <see cref="PdfStructureFlattenTagger"/>.
/// </para>
/// <para>
/// The appearance is drawn as a form XObject, by the same <c>q … cm … Do Q</c> the page resizer
/// writes for a form of the same document, and not through <see cref="XGraphics"/>: the
/// graphics surface draws a template it built or a page imported from another document, and a
/// widget's appearance is neither. The XObject is the widget's own, referenced rather than copied.
/// </para>
/// </remarks>
internal static class PdfFormFlattener
{
    private const string AcroFormKey = "/AcroForm";

    internal static void Flatten(PdfAcroForm form, bool redrawAppearances)
    {
        var document = form.Owner;
        document.EnsureCanModify("flattening a form");

        var catalog = document.Internals.Catalog.Elements;
        var attached = catalog.GetDictionary(AcroFormKey);
        if (!ReferenceEquals(attached, form) && !ReferenceEquals(attached, form.Reference?.Value))
        {
            throw new InvalidOperationException(
                "This form is not the interactive form of its document, so flattening it would leave "
                + "the document's own form and its widgets in place. Flatten document.AcroForm.");
        }

        var tagged = RefuseUntaggable(document);

        var fields = FieldsOf(form);
        RefuseSigned(fields);
        if (redrawAppearances)
            RefuseUnredrawable(fields);

        if (redrawAppearances)
        {
            foreach (var field in fields)
                field.RedrawForFlattening();
        }

        var widgets = new List<PdfWidgetAnnotation>();
        foreach (var field in fields)
            widgets.AddRange(field.Widgets);

        var targets = new HashSet<PdfDictionary>();
        foreach (var widget in widgets)
            targets.Add(widget.Reference?.Value as PdfDictionary ?? widget);

        // Before anything is drawn, because the tree is what hands out the identifiers the drawing is
        // marked with. Null for a document with no structure tree, which draws as it always did.
        var tagger = tagged ? PdfStructureFlattenTagger.Open(document, targets, AlternateTextOf(fields)) : null;

        foreach (var page in document.Pages)
            DrawWidgetsOnto(page, targets, form, tagger);

        // Before the widgets leave the structure tree, so that the remover finds the object
        // references of everything drawn already replaced and prunes only what drew nothing.
        tagger?.Commit();

        PdfWidgetAnnotation.RemoveFromPagesAndStructure(document, widgets);
        catalog.Remove(AcroFormKey);
    }

    /// <summary>
    /// Every field in the form, however deep, each once.
    /// </summary>
    private static List<PdfAcroField> FieldsOf(PdfAcroForm form)
    {
        var fields = new List<PdfAcroField>();
        var seen = new HashSet<PdfAcroField>();
        foreach (var root in form.Fields)
        {
            foreach (var field in root.SelfAndDescendants())
            {
                if (seen.Add(field))
                    fields.Add(field);
            }
        }
        return fields;
    }

    /// <summary>
    /// Whether the document has a structure tree for the drawn appearances to be tagged into,
    /// refusing a document that says it is tagged and has none.
    /// </summary>
    /// <remarks>
    /// <c>/MarkInfo /Marked true</c> with no <c>/StructTreeRoot</c> is a document that claims its
    /// content is tagged and holds no element to tag anything as. Drawing the values there would be
    /// content that is neither tagged nor an artifact, and marking it an artifact would take the
    /// values out of the reading order of a document that is meant to be read aloud; neither is a
    /// decision for this to make, so it is refused.
    /// </remarks>
    private static bool RefuseUntaggable(PdfDocument document)
    {
        var catalog = document.Internals.Catalog.Elements;
        if (document.IsTagged || catalog.ContainsKey(PdfCatalog.Keys.StructTreeRoot))
        {
            // A /StructTreeRoot that is not a dictionary is a tree there is nothing to read; drawing
            // the values untagged would contradict the claim just as no tree would.
            if (PdfStructureAnnotationRemover.RootOf(document) == null)
            {
                throw new InvalidOperationException(
                    "This document's /StructTreeRoot is not a dictionary, so there is no structure tree "
                    + "for the flattened form to be tagged into. Repair or remove it before flattening.");
            }
            return true;
        }

        var marked = catalog.GetDictionary(PdfCatalog.Keys.MarkInfo)?.Elements.GetBoolean("/Marked") ?? false;
        if (!marked)
            return false;

        throw new InvalidOperationException(
            "This document is marked as tagged but has no structure tree, so there is no structure "
            + "element for the flattened form to be tagged as. Flattening a form in it would draw "
            + "content that is neither tagged nor an artifact. Remove /MarkInfo, or give the "
            + "document a structure tree, before flattening.");
    }

    /// <summary>
    /// What to say for each widget in place of its control: its field's <c>/TU</c>, or the nearest
    /// ancestor's, since a file may write it on a parent.
    /// </summary>
    private static Func<PdfDictionary, string> AlternateTextOf(List<PdfAcroField> fields)
    {
        var texts = new Dictionary<PdfDictionary, string>();
        foreach (var field in fields)
        {
            var text = ToolTipOf(field);
            if (text.Length == 0)
                continue;

            foreach (var widget in field.Widgets)
                texts[widget.Reference?.Value as PdfDictionary ?? widget] = text;
        }
        return widget => texts.GetValueOrDefault(widget);
    }

    private static string ToolTipOf(PdfDictionary field)
    {
        // A malformed file may make a field its own ancestor.
        for (var depth = 0; field != null && depth < 32; depth++)
        {
            var text = field.Elements.GetString(PdfAcroField.Keys.TU);
            if (text.Length > 0)
                return text;

            field = field.Elements.GetDictionary(PdfAcroField.Keys.Parent);
        }
        return "";
    }

    private static void RefuseSigned(List<PdfAcroField> fields)
    {
        foreach (var field in fields)
        {
            if (!field.IsSigned)
                continue;

            throw new InvalidOperationException(
                "The signature field '" + field.Elements.GetString(PdfAcroField.Keys.T) + "' has been "
                + "signed, so the form cannot be flattened: Flatten changes the page content and "
                + "annotations the signature covers, and saving the document rewrites the file, so "
                + "every signature in it would be invalid.");
        }
    }

    private static void RefuseUnredrawable(List<PdfAcroField> fields)
    {
        foreach (var field in fields)
        {
            if (field.WhyCannotBeRedrawnForFlattening() is { } reason)
            {
                throw new InvalidOperationException(
                    "The field '" + field.Elements.GetString(PdfAcroField.Keys.T) + "' cannot be redrawn, "
                    + "so the form cannot be flattened with redrawAppearances: " + reason);
            }
        }
    }

    /// <summary>
    /// Draws the normal appearance of every widget that <paramref name="targets"/> holds and the
    /// page lists, in one content stream added at the end of the page.
    /// </summary>
    /// <remarks>
    /// The page is asked what it lists rather than each widget asked what page it is on: a
    /// widget's <c>/P</c> is optional, files from other software get it wrong, and a widget listed
    /// on two pages is drawn on both.
    /// </remarks>
    private static void DrawWidgetsOnto(PdfPage page, HashSet<PdfDictionary> targets, PdfAcroForm form,
        PdfStructureFlattenTagger tagger)
    {
        var annotations = page.Elements.GetArray(PdfPage.Keys.Annots);
        if (annotations == null)
            return;

        var operators = new StringBuilder();
        PdfResources resources = null;
        for (var idx = 0; idx < annotations.Elements.Count; idx++)
        {
            if (annotations.Elements.GetDictionary(idx) is not { } widget || !targets.Contains(widget))
                continue;

            if (!TryPlace(widget, form, out var appearance, out var matrix))
                continue;

            resources ??= page.Resources;
            var name = resources.AddExistingForm(appearance);
            BeginMarkedContent(operators, page, widget, tagger);
            // The page's own path for drawing a form notes this; without it a translucent
            // appearance is drawn on a page that is saved with no transparency group.
            page.NoteTransparencyOf(appearance);
            operators.Append("q ");
            foreach (var value in new[] { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.OffsetX, matrix.OffsetY })
                operators.Append(value.ToString(Config.SignificantFigures10, CultureInfo.InvariantCulture)).Append(' ');
            operators.Append("cm ").Append(name).Append(" Do Q\n");
            if (tagger != null)
                operators.Append("EMC\n");
        }

        if (operators.Length == 0)
            return;

        var content = page.Contents.AppendContent();
        content.CreateStream(PdfEncoders.RawEncoding.GetBytes(operators.ToString()));
    }

    /// <summary>
    /// Opens the marked-content sequence a drawn appearance goes in: <c>/Span</c> with the
    /// identifier the structure tree handed out, or an artifact for a widget the tree does not hold.
    /// </summary>
    /// <remarks>
    /// The tag is the type the element has afterwards - <c>/Span</c> for a form control, which is
    /// retyped - because a reader expects the two to agree. A widget no element holds is not content of anything - a tagged document
    /// that never put it in the tree - and an artifact is what is left for a drawing with nowhere to
    /// belong.
    /// </remarks>
    private static void BeginMarkedContent(StringBuilder operators, PdfPage page, PdfDictionary widget, PdfStructureFlattenTagger tagger)
    {
        if (tagger == null)
            return;

        if (tagger.TryBegin(page, widget, out var mcid, out var tag))
            operators.Append(tag).Append(" <</MCID ").Append(mcid.ToString(CultureInfo.InvariantCulture)).Append(">> BDC\n");
        else
            operators.Append("/Artifact BMC\n");
    }

    /// <summary>
    /// The form XObject a widget shows and the matrix that places it on the page, or false when the
    /// widget is not shown or has nothing to show.
    /// </summary>
    private static bool TryPlace(PdfDictionary widget, PdfAcroForm form, out PdfDictionary appearance, out XMatrix matrix)
    {
        matrix = default;
        appearance = NormalAppearanceOf(widget);
        if (appearance == null)
            return false;

        var flags = (PdfAnnotationFlags)widget.Elements.GetInteger(PdfAnnotation.Keys.F);
        if ((flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
            return false;

        if (!TryReadBox(widget, PdfAnnotation.Keys.Rect, out var rect)
            || !TryReadBox(appearance, "/BBox", out var box)
            || !TryReadMatrix(appearance, out var formMatrix))
        {
            return false;
        }

        // ISO 32000-1 12.5.5: the corners of the box through the form's own matrix, the smallest
        // rectangle enclosing them, and the scale and translation that put that rectangle on /Rect.
        // Do applies the form's /Matrix itself, so what is returned is the part after it.
        var corners = new[]
        {
            formMatrix.Transform(new XPoint(box.X1, box.Y1)),
            formMatrix.Transform(new XPoint(box.X2, box.Y1)),
            formMatrix.Transform(new XPoint(box.X2, box.Y2)),
            formMatrix.Transform(new XPoint(box.X1, box.Y2))
        };
        var left = Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
        var right = Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
        var bottom = Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
        var top = Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));

        var width = right - left;
        var height = top - bottom;
        if (!(width > 0) || !(height > 0) || !(rect.Width > 0) || !(rect.Height > 0))
            return false;

        var scaleX = rect.Width / width;
        var scaleY = rect.Height / height;
        matrix = new XMatrix(scaleX, 0, 0, scaleY, rect.X1 - left * scaleX, rect.Y1 - bottom * scaleY);

        // An appearance with no resources of its own is read by Acrobat against the form's
        // default resources, and the form is about to go. The font it names lives there.
        if (appearance.Elements["/Resources"] == null
            && form.Elements.GetDictionary(PdfAcroForm.Keys.DR) is { } defaults)
        {
            appearance.Elements["/Resources"] = (PdfItem)defaults.Reference ?? defaults;
        }

        return true;
    }

    /// <summary>
    /// The form XObject a widget's normal appearance is: <c>/AP /N</c> when that is a stream, or the
    /// stream under the state <c>/AS</c> names when it is a dictionary of them.
    /// </summary>
    private static PdfDictionary NormalAppearanceOf(PdfDictionary widget)
    {
        var normal = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP)?.Elements.GetDictionary("/N");
        if (normal == null)
            return null;

        if (normal.Stream == null)
        {
            var state = widget.Elements.GetNameOrEmpty(PdfAnnotation.Keys.AS);
            normal = state.Length == 0 ? null : normal.Elements.GetDictionary(state);
        }

        if (normal?.Stream == null)
            return null;

        var subtype = normal.Elements.GetNameOrEmpty("/Subtype");
        return subtype.Length == 0 || subtype == "/Form" ? normal : null;
    }

    private static bool TryReadBox(PdfDictionary owner, string key, out PdfRectangle box)
    {
        box = null;
        if (!owner.Elements.ContainsKey(key))
            return false;

        try
        {
            var read = owner.Elements.GetRectangle(key);
            // Either corner may be given first.
            box = new PdfRectangle(Math.Min(read.X1, read.X2), Math.Min(read.Y1, read.Y2),
                Math.Max(read.X1, read.X2), Math.Max(read.Y1, read.Y2));
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static bool TryReadMatrix(PdfDictionary form, out XMatrix matrix)
    {
        matrix = XMatrix.Identity;
        if (!form.Elements.ContainsKey("/Matrix"))
            return true;

        try
        {
            matrix = form.Elements.GetMatrix("/Matrix", false);
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }
}
