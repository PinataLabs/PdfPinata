using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   Text drawn inside a form XObject is text on the page. <see cref="PdfTextExtractor"/> did not
///   follow a <c>Do</c> operator, so the text of a flattened form field - and of any page imported
///   with <see cref="XPdfForm"/> - was invisible to it. Issue #355.
/// </summary>
public class FormXObjectTextExtractionTests
{
    private static XFont Font => new("Arial", 12);

    [Test]
    public void TextInAFormIsReadWhereTheFormIsDrawn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = new XForm(document, new XSize(200, 50));
        using (var inside = XGraphics.FromForm(form))
            inside.DrawString("Inside", Font, XBrushes.Black, 10, 30);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, 100, 200, 200, 50);

        var run = PdfTextExtractor.ExtractRuns(document.Reopened().Pages[0]).Single();

        run.Text.Should().Be("Inside");
        run.Origin.X.Should().BeApproximately(110, 0.5);
        run.FontSize.Should().BeApproximately(12, 0.1);
    }

    [Test]
    public void AFormsMatrixMovesItsText()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = new XForm(document, new XSize(200, 50));
        using (var inside = XGraphics.FromForm(form))
            inside.DrawString("Moved", Font, XBrushes.Black, 10, 30);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, 0, 0, 200, 50);

        var plain = PdfTextExtractor.ExtractRuns(document.Reopened().Pages[0]).Single();

        var reopened = document.Reopened();
        var drawn = (PdfDictionary)reopened.Pages[0].Elements.GetDictionary("/Resources")
            .Elements.GetDictionary("/XObject").Elements.GetObject("/Fm0");
        drawn.Elements.SetMatrix("/Matrix", new XMatrix(2, 0, 0, 2, 7, 0));
        var scaled = PdfTextExtractor.ExtractRuns(reopened.Pages[0]).Single();

        scaled.Origin.X.Should().BeApproximately(plain.Origin.X * 2 + 7, 0.5);
        scaled.FontSize.Should().BeApproximately(plain.FontSize * 2, 0.1);
    }

    [Test]
    public void AFormThatDrawsItselfEndsInsteadOfRunningForEver()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var form = new PdfDictionary(document);
        document.Internals.AddObject(form);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = new PdfRectangle(new XRect(0, 0, 100, 100));

        var forms = new PdfDictionary(document);
        forms.Elements["/Fm0"] = form.Reference;
        var resources = new PdfDictionary(document);
        resources.Elements["/XObject"] = forms;
        form.Elements["/Resources"] = resources;
        form.CreateStream(Encoding.ASCII.GetBytes("q /Fm0 Do Q"));

        var pageResources = new PdfDictionary(document);
        pageResources.Elements["/XObject"] = forms;
        page.Elements["/Resources"] = pageResources;
        page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("/Fm0 Do"));

        var runs = PdfTextExtractor.ExtractRuns(document.Reopened().Pages[0]);

        runs.Should().BeEmpty();
    }
}
