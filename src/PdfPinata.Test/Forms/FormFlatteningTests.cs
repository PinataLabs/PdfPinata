using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Structure;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   Flattening a filled form into page content: <see cref="PdfAcroForm.Flatten()"/>. Issue #355.
/// </summary>
/// <remarks>
///   Every test calls the public method, saves, reopens the file and reads what a reader would meet:
///   the catalogue, each page's <c>/Annots</c> as raw dictionaries, and the text of the page.
///   The tests that rasterize are in <see cref="FormFlatteningRasterTests"/>.
/// </remarks>
public class FormFlatteningTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    private static PdfTextField AddText(PdfDocument document, PdfPage page, string name, string value, PdfRectangle box = null)
    {
        var field = new PdfTextField(document) { Name = name };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, box ?? Box);
        field.Text = value;
        return field;
    }

    /// <summary>The subtype of every annotation on every page, from the raw dictionaries.</summary>
    private static List<string> AnnotationSubtypes(PdfDocument document)
    {
        var found = new List<string>();
        foreach (var page in document.Pages)
        {
            var annots = page.Elements.GetArray("/Annots");
            if (annots == null)
                continue;

            for (var idx = 0; idx < annots.Elements.Count; idx++)
                found.Add(annots.Elements.GetDictionary(idx).Elements.GetName("/Subtype"));
        }
        return found;
    }

    // ----- the form and the pages --------------------------------------------------------------------

    [Test]
    public void AFlattenedFormLeavesNoAcroFormAndNoWidget()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "name", "Ada Lovelace");

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        reopened.Internals.Catalog.Elements.ContainsKey("/AcroForm").Should().BeFalse();
        reopened.AcroForm.Should().BeNull();
        AnnotationSubtypes(reopened).Should().NotContain("/Widget");
    }

    // ----- the text -----------------------------------------------------------------------------------

    private static PdfComboBoxField AddCombo(PdfDocument document, PdfPage page, string name, string choice, PdfRectangle box)
    {
        var field = new PdfComboBoxField(document) { Name = name, Options = ["Australia", "Canada", "Ireland"] };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.DefaultAppearance = "/Helv 12 Tf 0 g";
        field.AddWidget(page, box);
        field.SelectedIndex = Array.IndexOf(field.Options, choice);
        return field;
    }

    private static PdfListBoxField AddList(PdfDocument document, PdfPage page, string name, string choice, PdfRectangle box)
    {
        var field = new PdfListBoxField(document) { Name = name, Options = ["Cat", "Dog", "Emu"] };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.DefaultAppearance = "/Helv 12 Tf 0 g";
        field.AddWidget(page, box);
        field.SelectedIndex = Array.IndexOf(field.Options, choice);
        return field;
    }

    [Test]
    public void TheValueOfEachTextAndChoiceFieldIsPageTextAfterwards()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "name", "Ada Lovelace", new PdfRectangle(new XRect(60, 700, 200, 20)));
        AddCombo(document, page, "country", "Canada", new PdfRectangle(new XRect(60, 660, 200, 20)));
        AddList(document, page, "pet", "Dog", new PdfRectangle(new XRect(60, 560, 200, 60)));

        document.AcroForm.Flatten();

        var text = PdfTextExtractor.ExtractText(document.Reopened().Pages[0]);
        text.Should().Contain("Ada Lovelace").And.Contain("Canada").And.Contain("Dog");
    }

    // ----- every page, every widget ---------------------------------------------------------------------

    [Test]
    public void WidgetsOnSeveralPagesAreAllFlattened()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var third = document.AddPage();
        AddText(document, first, "a", "First page value");
        var shared = AddText(document, second, "b", "Shared value");
        shared.AddWidget(third, Box);
        // The third widget claims the first page, as the optional /P of a file from other software
        // sometimes does: the widget is found by what the pages list, not by what it says.
        shared.Widgets[1].Elements.SetReference("/P", first);

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        AnnotationSubtypes(reopened).Should().BeEmpty();
        PdfTextExtractor.ExtractText(reopened.Pages[0]).Should().Be("First page value");
        PdfTextExtractor.ExtractText(reopened.Pages[1]).Should().Be("Shared value");
    }

    [Test]
    public void AnnotationsThatAreNotWidgetsAreLeftWhereTheyWere()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Annotations.Add(new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) });
        AddText(document, page, "name", "Ada");
        page.Annotations.Add(new PdfLinkAnnotation { Rectangle = new PdfRectangle(new XRect(10, 40, 20, 20)) });

        document.AcroForm.Flatten();

        AnnotationSubtypes(document.Reopened()).Should().Equal("/Text", "/Link");
    }

    [Test]
    [Arguments(PdfAnnotationFlags.Hidden)]
    [Arguments(PdfAnnotationFlags.NoView)]
    public void AHiddenWidgetIsNotDrawnButIsStillRemoved(PdfAnnotationFlags hidden)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "shown", "Shown value", new PdfRectangle(new XRect(60, 700, 200, 20)));
        var secret = AddText(document, page, "secret", "Secret value", new PdfRectangle(new XRect(60, 660, 200, 20)));
        secret.Widgets[0].Flags |= hidden;

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        PdfTextExtractor.ExtractText(reopened.Pages[0]).Should().Be("Shown value");
        AnnotationSubtypes(reopened).Should().BeEmpty();
    }

    [Test]
    public void AWidgetWithNoAppearanceIsRemovedAndDrawsNothing()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var empty = new PdfTextField(document) { Name = "empty" };
        document.GetOrCreateAcroForm().Fields.Add(empty);
        empty.AddWidget(page, Box);

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        AnnotationSubtypes(reopened).Should().BeEmpty();
        ContentOf(reopened.Pages[0]).Should().NotContain(" Do");
    }

    // ----- the states of a button ------------------------------------------------------------------------

    /// <summary>A form XObject that paints a rectangle, which is all a button state needs to be.</summary>
    private static PdfDictionary APaintedForm(PdfDocument document, XRect bbox, string matrix = null)
    {
        var form = new PdfDictionary(document);
        document.Internals.AddObject(form);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements.SetRectangle("/BBox", new PdfRectangle(bbox));
        if (matrix != null)
            form.Elements["/Matrix"] = new PdfLiteral(matrix);
        form.CreateStream(Encoding.ASCII.GetBytes("0 g 0 0 1000 1000 re f"));
        return form;
    }

    private static void GiveAppearance(PdfDictionary widget, PdfDictionary normal)
    {
        var ap = new PdfDictionary(widget.Owner);
        ap.Elements["/N"] = normal.Reference;
        widget.Elements["/AP"] = ap;
    }

    /// <summary>The text of the page's content, its streams joined.</summary>
    private static string ContentOf(PdfPage page) =>
        Encoding.ASCII.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);

    [Test]
    public void ACheckBoxDrawsTheStateItsAppearanceStateNames()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var box = new PdfCheckBoxField(document) { Name = "tick" };
        document.GetOrCreateAcroForm().Fields.Add(box);
        var widget = box.AddWidget(page, new PdfRectangle(new XRect(60, 700, 16, 16)));
        var yes = new XForm(document, new XSize(16, 16));
        using (var gfx = XGraphics.FromForm(yes))
            gfx.DrawLine(new XPen(XColors.Black, 2), 3, 8, 13, 8);
        widget.SetAppearance("/Yes", yes);
        widget.SetAppearance("/Off", new XForm(document, new XSize(16, 16)));
        box.Checked = true;

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        AnnotationSubtypes(reopened).Should().BeEmpty();
        var drawn = FormsDrawnOn(reopened.Pages[0]);
        drawn.Should().HaveCount(1);
        Encoding.ASCII.GetString(drawn[0].Stream.UnfilteredValue).Should().Contain(" l");
    }

    /// <summary>The form XObjects in the page's resources.</summary>
    private static List<PdfDictionary> FormsDrawnOn(PdfPage page)
    {
        var xobjects = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/XObject");
        return xobjects == null
            ? []
            : [.. xobjects.Elements.KeyNames.Select(key => xobjects.Elements.GetDictionary(key.Value))];
    }

    [Test]
    public void ARadioGroupDrawsOnlyTheButtonThatIsOn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var group = new PdfRadioButtonField(document) { Name = "delivery", Options = ["Post", "Courier"] };
        document.GetOrCreateAcroForm().Fields.Add(group);
        var post = group.AddWidget(page, new PdfRectangle(new XRect(60, 700, 14, 14)));
        var courier = group.AddWidget(page, new PdfRectangle(new XRect(60, 680, 14, 14)));
        foreach (var (widget, state) in new[] { (post, "/Post"), (courier, "/Courier") })
        {
            var mark = new XForm(document, new XSize(14, 14));
            using (var gfx = XGraphics.FromForm(mark))
                gfx.DrawEllipse(XBrushes.Black, 3, 3, 8, 8);
            widget.SetAppearance(state, mark);
            widget.SetAppearance("/Off", new XForm(document, new XSize(14, 14)));
        }
        group.SelectedIndex = 1;
        // Which button shows its mark is each widget's own /AS; the group's value does not set it.
        post.AppearanceState = "Off";
        courier.AppearanceState = "Courier";

        document.AcroForm.Flatten();

        // Courier is on and Post is off, so the ellipse is painted once, where Courier was.
        var painted = FormsDrawnOn(document.Reopened().Pages[0])
            .Count(form => Encoding.ASCII.GetString(form.Stream.UnfilteredValue).Contains(" c"));
        painted.Should().Be(1);
        FormsDrawnOn(document.Reopened().Pages[0]).Should().HaveCount(1);
    }

    // ----- ISO 32000-1 12.5.5 -------------------------------------------------------------------------------

    /// <summary>The <c>cm</c> operands the first widget was drawn with.</summary>
    private static string PlacementOf(PdfDocument flattened)
    {
        var content = ContentOf(flattened.Reopened().Pages[0]);
        var start = content.IndexOf("q ", StringComparison.Ordinal);
        return content[start..content.IndexOf(" Do", StringComparison.Ordinal)].Trim();
    }

    private static PdfDocument AWidgetShowing(XRect bbox, string matrix, XRect rect)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = new PdfTextField(document) { Name = "t" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        var widget = field.AddWidget(page, new PdfRectangle(rect));
        GiveAppearance(widget, APaintedForm(document, bbox, matrix));
        return document;
    }

    [Test]
    public void TheBoundingBoxIsScaledAndMovedOntoTheRectangle()
    {
        var document = AWidgetShowing(new XRect(0, 0, 100, 50), null, new XRect(200, 300, 200, 100));

        document.AcroForm.Flatten();

        PlacementOf(document).Should().Be("q 2 0 0 2 200 300 cm /Fm0");
    }

    [Test]
    public void TheFormsMatrixIsPartOfTheBoxThatIsMapped()
    {
        // A quarter turn takes [0 0 100 50] to x in [-50, 0] and y in [0, 100]: that box is half as
        // wide as the rectangle (100) and twice as tall as it (50), hence 2 and 0.5. Its left edge
        // is at -50, which the translation has to bring to the rectangle's 200.
        var document = AWidgetShowing(new XRect(0, 0, 100, 50), "[0 1 -1 0 0 0]", new XRect(200, 300, 100, 50));

        document.AcroForm.Flatten();

        PlacementOf(document).Should().Be("q 2 0 0 0.5 300 300 cm /Fm0");
    }

    [Test]
    public void AnAppearanceWithAnEmptyBoundingBoxIsNotDrawn()
    {
        var document = AWidgetShowing(new XRect(10, 10, 0, 30), null, new XRect(200, 300, 100, 50));

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        AnnotationSubtypes(reopened).Should().BeEmpty();
        ContentOf(reopened.Pages[0]).Should().NotContain(" Do");
    }

    // ----- redraw -----------------------------------------------------------------------------------------

    /// <summary>A text field whose value was changed behind its back, so its drawing is stale.</summary>
    private static PdfDocument AStaleField(bool readOnly = false)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = AddText(document, page, "name", "Ada Lovelace");
        field.Elements.SetString("/V", "Grace Hopper");
        field.ReadOnly = readOnly;
        return document;
    }

    [Test]
    public void ByDefaultTheAppearanceIsDrawnAsItIs()
    {
        var document = AStaleField();

        document.AcroForm.Flatten();

        PdfTextExtractor.ExtractText(document.Reopened().Pages[0]).Should().Be("Ada Lovelace");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ARedrawFirstDrawsWhatTheFieldWouldShowNow(bool readOnly)
    {
        var document = AStaleField(readOnly);

        document.AcroForm.Flatten(redrawAppearances: true);

        PdfTextExtractor.ExtractText(document.Reopened().Pages[0]).Should().Be("Grace Hopper");
    }

    [Test]
    public void AnAppearanceWithNoResourcesIsGivenTheFormsDefaultResources()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = AddText(document, page, "name", "Ada Lovelace");
        var appearance = field.Widgets[0].Elements.GetDictionary("/AP").Elements.GetDictionary("/N");
        document.AcroForm.Elements["/DR"] = appearance.Elements["/Resources"];
        appearance.Elements.Remove("/Resources");

        document.AcroForm.Flatten();

        PdfTextExtractor.ExtractText(document.Reopened().Pages[0]).Should().Be("Ada Lovelace");
    }

    // ----- refusals ------------------------------------------------------------------------------------------

    [Test]
    public void ATaggedDocumentIsRefusedAndNamesTheIssue()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        document.Structure.RegisterPage(page);
        var field = AddText(document, page, "name", "Ada");
        document.Structure.AddAnnotation(document.Structure.CreateElement(new PdfTag("/Form")), page, field.Widgets[0]);

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*tagged*#357*");
        var reopened = document.Reopened();
        reopened.AcroForm.Should().NotBeNull();
        AnnotationSubtypes(reopened).Should().Equal("/Widget");
        ContentOf(reopened.Pages[0]).Should().NotContain(" Do");
    }

    [Test]
    public void ATaggedDocumentReadFromAFileIsRefusedToo()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        document.Structure.RegisterPage(page);
        var field = AddText(document, page, "name", "Ada");
        document.Structure.AddAnnotation(document.Structure.CreateElement(new PdfTag("/Form")), page, field.Widgets[0]);
        var reopened = document.Reopened();

        var flatten = () => reopened.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*#357*");
    }

    [Test]
    public void ADocumentMarkedAsTaggedIsRefusedWithNoTreeToShowIt()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "name", "Ada");
        var markInfo = new PdfDictionary(document);
        markInfo.Elements.SetBoolean("/Marked", true);
        document.Internals.Catalog.Elements["/MarkInfo"] = markInfo;

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*#357*");
    }

    [Test]
    public void AFormHoldingASignedSignatureFieldIsRefusedWhole()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "name", "Ada", new PdfRectangle(new XRect(60, 700, 200, 20)));
        var signature = new PdfSignatureField(document) { Name = "approval" };
        document.AcroForm.Fields.Add(signature);
        signature.AddWidget(page, new PdfRectangle(new XRect(60, 600, 200, 40)));
        var signed = new PdfDictionary(document);
        signed.Elements.SetName("/Type", "/Sig");
        signature.Elements["/V"] = signed;

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*approval*signed*");
        var reopened = document.Reopened();
        reopened.AcroForm.Should().NotBeNull();
        AnnotationSubtypes(reopened).Should().Equal("/Widget", "/Widget");
        ContentOf(reopened.Pages[0]).Should().NotContain(" Do");
    }

    [Test]
    public void AnUnsignedSignatureFieldIsFlattenedLikeAnyOther()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var signature = new PdfSignatureField(document) { Name = "approval" };
        document.GetOrCreateAcroForm().Fields.Add(signature);
        var widget = signature.AddWidget(page, new PdfRectangle(new XRect(60, 600, 200, 40)));
        GiveAppearance(widget, APaintedForm(document, new XRect(0, 0, 200, 40)));

        document.AcroForm.Flatten();

        var reopened = document.Reopened();
        reopened.AcroForm.Should().BeNull();
        ContentOf(reopened.Pages[0]).Should().Contain(" Do");
    }

    [Test]
    [Arguments(PdfDocumentOpenMode.ReadOnly)]
    [Arguments(PdfDocumentOpenMode.Import)]
    public void ADocumentThatCannotBeModifiedIsRefused(PdfDocumentOpenMode mode)
    {
        var document = new PdfDocument();
        AddText(document, document.AddPage(), "name", "Ada");
        var reopened = document.Reopened(mode);

        var flatten = () => reopened.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*" + mode + "*");
    }

    [Test]
    public void AFormThatIsNotTheDocumentsOwnIsRefused()
    {
        var document = new PdfDocument();
        AddText(document, document.AddPage(), "name", "Ada");

        var flatten = () => new PdfAcroForm(document).Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*not the interactive form*");
        document.AcroForm.Should().NotBeNull();
    }

    [Test]
    public void AFlattenedFormIsGoneFromTheDocumentAndItsNeedAppearancesWithIt()
    {
        var document = new PdfDocument();
        AddText(document, document.AddPage(), "name", "Ada");
        document.AcroForm.NeedAppearances = true;

        document.AcroForm.Flatten();

        document.AcroForm.Should().BeNull();
        document.Reopened().Internals.Catalog.Elements.ContainsKey("/AcroForm").Should().BeFalse();
    }

    // ----- conformance ----------------------------------------------------------------------------------------

    [Test]
    public void AFlattenedPdfAFormSavesAndKeepsNoFormMachineryAndItsFontsEmbedded()
    {
        var document = new PdfDocument();
        document.Info.Title = "Flattened";
        document.Options.Conformance = PdfAConformance.PdfA2B;
        var page = document.AddPage();
        var font = new XFont("Arial", 12);
        var field = new PdfTextField(document) { Name = "name", Font = font };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, Box);
        field.Text = "Ada Lovelace";
        document.AcroForm.NeedAppearances = false;

        document.AcroForm.Flatten();

        // The save is the check that the writer's PDF/A rules accept what is left; veraPDF is the
        // outside opinion, on the corpus document of the same shape.
        var reopened = document.Reopened();
        reopened.AcroForm.Should().BeNull();
        AnnotationSubtypes(reopened).Should().BeEmpty();
        PdfTextExtractor.ExtractText(reopened.Pages[0]).Should().Be("Ada Lovelace");

        var drawn = FormsDrawnOn(reopened.Pages[0]).Should().ContainSingle().Subject;
        var fonts = drawn.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Font");
        fonts.Elements.KeyNames.Should().NotBeEmpty();
        foreach (var key in fonts.Elements.KeyNames)
        {
            var descendant = fonts.Elements.GetDictionary(key.Value).Elements.GetArray("/DescendantFonts").Elements.GetDictionary(0);
            descendant.Elements.GetDictionary("/FontDescriptor").Elements.ContainsKey("/FontFile2").Should().BeTrue();
        }
    }
}
