using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   Tokens the writer used to produce that were legal but noisy, or not legal at all: a negative
///   zero, a real a PDF cannot hold, a NUL in a name, a /ProcSet that was a literal in memory and an
///   empty content stream left on a page that already had content. See #336.
/// </summary>
public class WriterTokenTests
{
    [Test]
    [Arguments(-0.0001)]
    [Arguments(-0.0)]
    [Arguments(0.0)]
    public void ARealThatRoundsToZeroIsWrittenAsAPlainZero(double value)
    {
        new PdfReal(value).ToString().Should().Be("0");
    }

    [Test]
    public void AHalfIsStillWrittenWithItsLeadingZero()
    {
        new PdfReal(0.5).ToString().Should().Be("0.5");
        new PdfReal(-0.5).ToString().Should().Be("-0.5");
    }

    [Test]
    public void ARealThatRoundsToZeroIsWrittenToTheFileAsAPlainZero()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Elements["/NegativeZero"] = new PdfReal(-0.0);
        page.Elements["/AlmostZero"] = new PdfReal(-0.00000001);

        var file = Latin1(Saved.Bytes(document));

        file.Should().Contain("/NegativeZero 0").And.Contain("/AlmostZero 0").And.NotContain("-0");
    }

    [Test]
    public void APlainTranslationIsWrittenWithoutNegativeZeros()
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = false;
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.TranslateTransform(50, 90);
            gfx.DrawRectangle(XPens.Black, 0, 0, 10, 10);
        }

        var content = Latin1(page.Contents.Elements.GetDictionary(0).Stream.Value);

        content.Should().Contain("1 0 0 1 50 90 cm").And.NotContain("-0 ");
    }

    [Test]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    [Arguments(double.NaN)]
    [Arguments(1e39)]
    [Arguments(-1e39)]
    public void ARealAPdfCannotHoldIsRefused(double value)
    {
        var construct = () => new PdfReal(value);

        construct.Should().Throw<ArgumentException>();
    }

    [Test]
    public void TheLargestSinglePrecisionRealIsStillAccepted()
    {
        new PdfReal(float.MaxValue).Value.Should().Be(float.MaxValue);
    }

    [Test]
    public void ARealTooLargeForAPdfIsStillReadFromAFile()
    {
        var digits = "1" + new string('0', 39) + ".5";
        var bytes = RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            $"<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Huge {digits}>>"
        ]);

        var document = Saved.Open(bytes);

        document.Pages[0].Elements.GetReal("/Huge").Should().BeGreaterThanOrEqualTo(1e39);
    }

    [Test]
    public void ANulInANameIsLeftOut()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Elements["/Nul"] = new PdfName("/A\0B");

        var file = Latin1(Saved.Bytes(document));

        file.Should().Contain("/Nul /AB").And.NotContain("#00");
    }

    [Test]
    public void ANewPagesProcSetIsAnArrayOfNames()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var procSet = page.Resources.Elements.GetArray(PdfResources.Keys.ProcSet);

        procSet.Should().NotBeNull();
        procSet!.Elements.Select(item => ((PdfName)item).Value).Should()
            .Equal("/PDF", "/Text", "/ImageB", "/ImageC", "/ImageI");
    }

    [Test]
    public void ANewPagesProcSetIsWrittenAsItAlwaysWas()
    {
        var document = new PdfDocument();
        _ = document.AddPage().Resources;

        Latin1(Saved.Bytes(document)).Should().Contain("/ProcSet [/PDF/Text/ImageB/ImageC/ImageI]");
    }

    [Test]
    public void AnXGraphicsThatDrawsNothingAddsNoContentStreamToAPageThatHasOne()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XPens.Black, 10, 10, 10, 10);
        var drawn = page.Contents.Elements.GetDictionary(0).Stream.Value.ToArray();

        using (XGraphics.FromPdfPage(page)) { }

        page.Contents.Elements.Should().HaveCount(1);
        page.Contents.Elements.GetDictionary(0).Stream.Value.Should().Equal(drawn,
            "with nothing drawn after it there is no graphics state to protect, so the content is not wrapped in q and Q");
    }

    [Test]
    public void AnXGraphicsThatDrawsNothingLeavesTheNextOneToWrapTheContentBeforeIt()
    {
        var document = Saved.Open(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            RawPdf.Stream("", "0 0 1 RG 10 10 100 100 re S")
        ]));
        var page = document.Pages[0];

        using (XGraphics.FromPdfPage(page)) { }
        Latin1(page.Contents.Elements.GetDictionary(0).Stream.Value).Should().Be("0 0 1 RG 10 10 100 100 re S");

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XPens.Red, 30, 30, 10, 10);

        page.Contents.Elements.Should().HaveCount(2);
        Latin1(page.Contents.Elements.GetDictionary(0).Stream.Value).Should().Be("q\n0 0 1 RG 10 10 100 100 re S Q\n");
    }

    [Test]
    public void AnXGraphicsPrependedThatDrawsNothingAddsNoContentStreamEither()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XPens.Black, 10, 10, 10, 10);

        using (XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Prepend)) { }

        page.Contents.Elements.Should().HaveCount(1);
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
