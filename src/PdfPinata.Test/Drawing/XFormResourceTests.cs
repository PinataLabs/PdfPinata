using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   An <see cref="XForm"/> is a content stream with resources of its own, and it carries the same
///   four resource tables a page does — fonts, images, nested forms and the rest. A form is drawn
///   on exactly like a page and then frozen, and the freezing is what most of its rules are about:
///   it can be drawn on once, it is finished the first time it is drawn from, and after that
///   nothing about it may change.
/// </summary>
public class XFormResourceTests
{
    private static XFont AFont() => new("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    private static XImage AnImage() => XImage.FromFile(PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg"));

    /// <summary>
    ///   The resource dictionary of the one form drawn on a page, reached the way a reader reaches
    ///   it: down the page's own XObject table to the form, and then into the form's resources.
    /// </summary>
    private static PdfDictionary ResourcesOfTheOnlyFormOn(PdfPage page)
    {
        var xObjects = page.Resources.Elements.GetDictionary("/XObject");
        foreach (var key in xObjects!.Elements.KeyNames)
        {
            var xObject = xObjects.Elements.GetDictionary(key.Value);
            if (xObject!.Elements.GetName("/Subtype") == "/Form")
                return xObject.Elements.GetDictionary("/Resources");
        }

        return null;
    }

    // ----- what a form will not be built from -------------------------------------------------------

    [Fact]
    public void AFormMustBeBigEnoughToDrawOn()
    {
        var document = new PdfDocument();

        ((Action)(() => _ = new XForm(document, new XRect(0, 0, 0.5, 10)))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new XForm(document, new XRect(0, 0, 10, 0.5)))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AFormMustBelongToADocumentFromTheStart()
    {
        var building = () => new XForm(null!, new XSize(100, 100));

        building.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AFormCanBeSizedByARectangleBySizeOrByTwoLengths()
    {
        var document = new PdfDocument();

        new XForm(document, new XRect(0, 0, 100, 50)).ViewBox.Width.Should().Be(100);
        new XForm(document, new XSize(100, 50)).ViewBox.Height.Should().Be(50);
        new XForm(document, XUnit.FromPoint(100), XUnit.FromPoint(50)).ViewBox.Width.Should().Be(100);
    }

    // ----- the resources it gathers while being drawn on --------------------------------------------

    /// <summary>
    ///   A form has resource tables of its own rather than borrowing the page's, so everything
    ///   drawn into it has to be registered there. This draws one of each kind that has its own
    ///   table — text, an image and another form — and then puts the form on a page, which is what
    ///   finishes it and writes the resources out.
    /// </summary>
    [Fact]
    public void AFormGathersAFontAnImageAndANestedFormIntoItsOwnResources()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var inner = new XForm(document, new XSize(50, 50));
        using (var innerGfx = XGraphics.FromForm(inner))
            innerGfx.DrawRectangle(XBrushes.Red, 0, 0, 50, 50);

        var outer = new XForm(document, new XSize(200, 200));
        using (var outerGfx = XGraphics.FromForm(outer))
        {
            outerGfx.DrawString("Hello", AFont(), XBrushes.Black, 10, 20);
            outerGfx.DrawImage(AnImage(), 10, 30, 80, 60);
            outerGfx.DrawImage(inner, 10, 100);
        }

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(outer, 20, 20);

        var resources = ResourcesOfTheOnlyFormOn(page);

        resources.Should().NotBeNull();
        resources.Elements.GetDictionary("/Font").Should().NotBeNull("the string put a font there");
        resources.Elements.GetDictionary("/XObject").Should()
            .NotBeNull("the image and the nested form both go in the XObject table");
        resources.Elements.GetDictionary("/XObject")!.Elements.Count.Should().Be(2);
    }

    [Fact]
    public void AFontDrawnTwiceIntoAFormIsRegisteredOnce()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(200, 200));

        using (var gfx = XGraphics.FromForm(form))
        {
            gfx.DrawString("one", AFont(), XBrushes.Black, 10, 20);
            gfx.DrawString("two", AFont(), XBrushes.Black, 10, 40);
        }

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, 20, 20);

        ResourcesOfTheOnlyFormOn(page)!
            .Elements.GetDictionary("/Font")!.Elements.Count.Should().Be(1);
    }

    // ----- being frozen -----------------------------------------------------------------------------

    /// <summary>
    ///   Asking a form under construction for a graphics object again hands back the one it already
    ///   has rather than building a second. The form itself refuses a second association; the
    ///   factory never lets it get that far.
    /// </summary>
    [Fact]
    public void AFormUnderConstructionHandsBackTheGraphicsObjectItAlreadyHas()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(100, 100));
        using var first = XGraphics.FromForm(form);

        XGraphics.FromForm(form).Should().BeSameAs(first);
    }

    [Fact]
    public void AFormThatHasFinishedDrawingCannotBeDrawnOnAgain()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(100, 100));
        using (var gfx = XGraphics.FromForm(form))
            gfx.DrawLine(XPens.Black, 0, 0, 10, 10);

        form.DrawingFinished();

        var again = () => XGraphics.FromForm(form);
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FinishingAFormTwiceIsHarmless()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(100, 100));
        using (var gfx = XGraphics.FromForm(form))
            gfx.DrawLine(XPens.Black, 0, 0, 10, 10);

        form.DrawingFinished();

        var again = () => form.DrawingFinished();
        again.Should().NotThrow();
    }

    [Fact]
    public void AFormThatWasNeverDrawnOnCanStillBeFinished()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(100, 100));

        var finishing = () => form.DrawingFinished();

        finishing.Should().NotThrow("an empty form is an empty content stream, which is a thing to be");
    }

    [Fact]
    public void TheTransformOfAFinishedFormMayNotBeChanged()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(100, 100));
        form.Transform = new XMatrix(1, 0, 0, 1, 5, 5);
        using (var gfx = XGraphics.FromForm(form))
            gfx.DrawLine(XPens.Black, 0, 0, 10, 10);
        form.DrawingFinished();

        var setting = () => form.Transform = new XMatrix();

        setting.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AFormReportsTheSizeAndBoundingBoxItWasGiven()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XRect(0, 0, 120, 80));

        form.PointWidth.Should().Be(120);
        form.PointHeight.Should().Be(80);
        form.PixelWidth.Should().BeGreaterThan(0);
        form.PixelHeight.Should().BeGreaterThan(0);
        form.Size.Width.Should().Be(120);
        form.BoundingBox = new XRect(0, 0, 60, 40);
        form.BoundingBox.Width.Should().Be(60);
    }
}
