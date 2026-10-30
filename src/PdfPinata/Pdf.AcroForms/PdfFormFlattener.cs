using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Turns a form into page content: draws each widget's normal appearance into the page it is on,
/// takes the widgets off the pages, and takes the form out of the document. What
/// <see cref="PdfAcroForm.Flatten()"/> does; <c>docs/specs/form-flattening.md</c> has the decisions.
/// </summary>
/// <remarks>
/// <para>
/// Everything that can be refused is refused before anything is changed, so a refusal leaves the
/// document as it was: a document that cannot be modified, a tagged one, a form holding a signed
/// signature field, and - when the appearances are to be redrawn - a field that has nothing to
/// draw them in.
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

        RefuseTagged(document);

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

        foreach (var page in document.Pages)
            DrawWidgetsOnto(page, targets, form);

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

    private static void RefuseTagged(PdfDocument document)
    {
        var catalog = document.Internals.Catalog.Elements;
        var marked = catalog.GetDictionary(PdfCatalog.Keys.MarkInfo)?.Elements.GetBoolean("/Marked") ?? false;
        if (!document.IsTagged && !marked && !catalog.ContainsKey(PdfCatalog.Keys.StructTreeRoot))
            return;

        throw new InvalidOperationException(
            "This document is tagged, and flattening a form in a tagged document is not supported: "
            + "each widget's /Form structure element points at the annotation, and the appearance "
            + "drawn into the page in its place would be content that is neither tagged nor an "
            + "artifact. Flattening a tagged form, with re-tagging, is tracked as issue #357. For a "
            + "document rendered by PinataLayout, set PdfDocumentRenderer.TagContent to false before "
            + "rendering.");
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
    private static void DrawWidgetsOnto(PdfPage page, HashSet<PdfDictionary> targets, PdfAcroForm form)
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
            // The page's own path for drawing a form notes this; without it a translucent
            // appearance is drawn on a page that is saved with no transparency group.
            page.NoteTransparencyOf(appearance);
            operators.Append("q ");
            foreach (var value in new[] { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.OffsetX, matrix.OffsetY })
                operators.Append(value.ToString(Config.SignificantFigures10, CultureInfo.InvariantCulture)).Append(' ');
            operators.Append("cm ").Append(name).Append(" Do Q\n");
        }

        if (operators.Length == 0)
            return;

        var content = page.Contents.AppendContent();
        content.CreateStream(PdfEncoders.RawEncoding.GetBytes(operators.ToString()));
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
