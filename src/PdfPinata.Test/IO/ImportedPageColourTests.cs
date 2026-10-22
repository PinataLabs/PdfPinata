using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.IO;

/// <summary>
///   A form XObject inherits the graphics state in force at its <c>Do</c>, colours included. An
///   imported page was written as page content, where a viewer starts in black, so a page that
///   never names a colour relies on that default - and drawn through <see cref="XPdfForm"/> after
///   the caller had filled in red, it came out red. An imported page is therefore drawn with black
///   fill and stroke set inside its own <c>q</c>, which the <c>Q</c> after it takes away again.
/// </summary>
/// <remarks>
///   A template the caller drew themselves (<see cref="XForm"/>) is a different case (#333), and
///   writes what it always wrote.
/// </remarks>
[Rasterizing]
public sealed class ImportedPageColourTests : IDisposable
{
    private readonly Rasterizations _rasterized = new("Out/ImportedPageColour");

    public void Dispose() => _rasterized.Dispose();

    /// <summary>Where the imported page fills a square, in world space.</summary>
    private static readonly XRect ImportedSquare = new(100, 142, 100, 100);

    /// <summary>Where the imported page strokes a horizontal line 8 points wide, in world space.</summary>
    private static readonly XRect ImportedLine = new(300, 188, 150, 8);

    /// <summary>Where the caller fills red after the imported page, in world space.</summary>
    private static readonly XRect CallerAfter = new(100, 400, 100, 100);

    /// <summary>
    ///   An A4 page that fills <see cref="ImportedSquare"/> and strokes <see cref="ImportedLine"/>
    ///   without a single colour operator, written by hand because <see cref="XGraphics"/> always
    ///   names the colour it draws with.
    /// </summary>
    private static MemoryStream AnUncolouredPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Contents.AppendContent().CreateStream(
            Encoding.ASCII.GetBytes("100 600 100 100 re f\n8 w 300 650 m 450 650 l S\n"));

        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return stream;
    }

    /// <summary>The caller paints in red, draws the imported page, then paints in red again.</summary>
    private static PdfDocument DrawnAfterRed()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var source = AnUncolouredPage();
        using var form = XPdfForm.FromStream(source);
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawRectangle(XPens.Red, XBrushes.Red, 300, 400, 100, 100);
            gfx.DrawImage(form, new XRect(0, 0, page.Width.Point, page.Height.Point));
            gfx.DrawRectangle(XBrushes.Red, CallerAfter);
        }

        return document;
    }

    private static bool IsBlack(IMagickColor<byte> c) => c is { R: < 60, G: < 60, B: < 60 };

    private static bool IsRed(IMagickColor<byte> c) => c is { R: > 200, G: < 60, B: < 60 };

    [Test, GoldenImage]
    public void AnImportedPageThatNamesNoColourIsDrawnInBlackWhateverTheCallerLastPaintedIn()
    {
        var page = _rasterized.FirstPageOf(DrawnAfterRed(), "after_red");

        IsBlack(Within(page, ImportedSquare)).Should().BeTrue("the square was filled in the default colour");
        IsBlack(Within(page, ImportedLine)).Should().BeTrue("the line was stroked in the default colour");
    }

    [Test, GoldenImage]
    public void TheCallersColourIsBackInForceAfterTheImportedPage()
    {
        var page = _rasterized.FirstPageOf(DrawnAfterRed(), "red_after");

        IsRed(Within(page, CallerAfter)).Should().BeTrue();
    }

    [Test]
    public void AnImportedPageIsGivenBlackFillAndStrokeInsideItsOwnSaveAndRestore()
    {
        var content = ContentOf(DrawnAfterRed().Pages[0]);

        var doAt = content.IndexOf(" Do Q", StringComparison.Ordinal);
        doAt.Should().BePositive();
        var qAt = content.LastIndexOf("q ", doAt, StringComparison.Ordinal);
        var drawing = content.Substring(qAt, doAt - qAt);

        drawing.Should().Contain(" 0 g ").And.Contain(" 0 G ");
    }

    [Test]
    public void AnImportedPageDrawnOnAnUpwardsPageIsGivenBlackFillAndStrokeToo()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var source = AnUncolouredPage();
        using var form = XPdfForm.FromStream(source);
        // Obsolete, but FromPdfPage still reaches it, and the colours have to be reset there too.
#pragma warning disable CS0618
        using (var gfx = XGraphics.FromPdfPage(page, XPageDirection.Upwards))
#pragma warning restore CS0618
            gfx.DrawImage(form, new XRect(0, 0, page.Width.Point, page.Height.Point));

        ContentOf(page).Should().MatchRegex(@"q [^Q]* cm 0 g 0 G /[A-Za-z0-9]+ Do Q\n");
    }

    [Test]
    public void ATemplateIsDrawnWithTheBytesItAlwaysWas()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var template = new XForm(document, new XRect(0, 0, 100, 100));
        using (var formGfx = XGraphics.FromForm(template))
            formGfx.DrawRectangle(XBrushes.Black, 0, 0, 10, 10);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(template, 0, 0);

        var content = ContentOf(page);

        content.Should().MatchRegex(@"q [^Q]* cm 100 Tz /[A-Za-z0-9]+ Do Q\n");
        content.Should().NotContain(" g ").And.NotContain(" G ");
    }

    private static string ContentOf(PdfPage page) =>
        Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);
}
