using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="StrokedTextTests"/> checks which rendering mode the pen and brush ask for. These
///   check that the reader does something different with each of them: outlined text is hollow,
///   filled text is solid, and a thick pen is visibly thicker than a thin one.
/// </summary>
[Collection(RasterizingCollection.Name)]
public class StrokedTextRenderingTests
{
    private const double FontSize = 60;
    private const double PageWidth = 300;
    private const double PageHeight = 100;

    private static XFont Font => new XFont("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    private static PdfDocument PageShowing(XPen pen, XBrush brush)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = PageWidth;
        page.Height = PageHeight;

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("OO", Font, pen, brush, 20, 75);

        return document;
    }

    private static List<(int X, int Y)> InkOf(XPen pen, XBrush brush)
    {
        // Freed as soon as the pixels have been read out of it. A rasterized page holds unmanaged
        // memory, and these tests make more of them than anything else in the suite.
        using var output = PdfHelper.Rasterize(PageShowing(pen, brush));

        var inked = PageInk.DarkPixelsOf(output.ImageCollection[0]);
        inked.Should().NotBeEmpty("the page should have text drawn on it");
        return inked;
    }

    private static (int Width, int Height) ExtentOf(List<(int X, int Y)> ink)
    {
        return (ink.Max(p => p.X) - ink.Min(p => p.X), ink.Max(p => p.Y) - ink.Min(p => p.Y));
    }

    [GoldenImageFact]
    public void OutlinedTextIsHollowWhereFilledTextIsSolid()
    {
        var filled = InkOf(null, XBrushes.Black);
        var outlined = InkOf(new XPen(XColors.Black, 1), null);

        // The same glyphs in the same place - a capital O twice, chosen because the hole in the
        // middle is most of it.
        var filledExtent = ExtentOf(filled);
        var outlinedExtent = ExtentOf(outlined);
        outlinedExtent.Width.Should().BeCloseTo(filledExtent.Width, 12);
        outlinedExtent.Height.Should().BeCloseTo(filledExtent.Height, 12);

        // But far less ink, because only the edge is drawn.
        outlined.Count.Should().BeLessThan(filled.Count / 2);
    }

    [GoldenImageFact]
    public void AThickerPenLaysDownMoreInkThanAThinOne()
    {
        var thin = InkOf(new XPen(XColors.Black, 1), null);
        var thick = InkOf(new XPen(XColors.Black, 4), null);

        thick.Count.Should().BeGreaterThan(thin.Count);
    }

    [GoldenImageFact]
    public void FillingAndStrokingTogetherCoversAtLeastAsMuchAsFillingAlone()
    {
        var filled = InkOf(null, XBrushes.Black);
        var both = InkOf(new XPen(XColors.Black, 3), XBrushes.Black);

        // The stroke is centred on the outline, so half of it falls outside the filled shape.
        both.Count.Should().BeGreaterThan(filled.Count);
    }

    [GoldenImageFact]
    public void AStrokeIsDrawnInThePensOwnColour()
    {
        using var output = PdfHelper.Rasterize(PageShowing(new XPen(XColors.Red, 3), null));

        using var pixels = output.ImageCollection[0].GetPixels();
        var red = pixels.Count(pixel =>
        {
            var colour = pixel.ToColor();
            return colour is { R: > 150, G: < 100, B: < 100 };
        });

        // The pen carries its own colour rather than borrowing the brush's - which is just as
        // well, because outlined text has no brush to borrow from.
        red.Should().BeGreaterThan(500);
    }
}
