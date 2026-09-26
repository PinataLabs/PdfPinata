using System;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   That a device without the ink paints a spot colour's alternate, at the tint asked for.
///   <see cref="SpotColorTests"/> reads the Separation back out of the file; a well-formed
///   Separation whose tint transform a reader rejected would pass every one of those and paint
///   nothing, which only rasterizing would show.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class SpotColorRenderingTests : IDisposable
{
    private const string OutDir = "Out/SpotColor";

    private static readonly XRect Solid = new(50, 50, 200, 100);
    private static readonly XRect Half = new(50, 200, 200, 100);
    private static readonly XRect Stroked = new(300, 50, 200, 100);

    private readonly Rasterizations _rasterized = new(OutDir);

    static SpotColorRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose() => _rasterized.Dispose();

    [GoldenImageFact]
    public void ACmykAlternateIsPaintedAtFullAndHalfTint()
    {
        var magenta = new XSpotColor("Rhodamine", XColor.FromCmyk(0, 1, 0, 0));

        var page = Rasterize("cmyk_alternate", gfx =>
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(magenta)), Solid);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(magenta, 0.5)), Half);
            gfx.DrawRectangle(new XPen(XColor.FromSpot(magenta), 20), Stroked);
        });

        var solid = At(page, Solid.X + Solid.Width / 2, Solid.Y + Solid.Height / 2);
        var half = At(page, Half.X + Half.Width / 2, Half.Y + Half.Height / 2);
        var stroke = At(page, Stroked.X, Stroked.Y + Stroked.Height / 2);

        // Magenta ink: green is what it takes away, red and blue it leaves. Half the tint takes
        // away about half as much.
        solid.G.Should().BeLessThan(80);
        solid.R.Should().BeGreaterThan(180);
        half.G.Should().BeInRange(90, 200);
        half.G.Should().BeGreaterThan(solid.G);
        stroke.G.Should().BeLessThan(80, "the pen strokes in the ink too");
        stroke.R.Should().BeGreaterThan(180);
    }

    [GoldenImageFact]
    public void AnRgbAlternateIsPaintedAsItsOwnColour()
    {
        var blue = new XSpotColor("Reflex Blue", XColor.FromArgb(0, 20, 137));

        var page = Rasterize("rgb_alternate", gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(blue)), Solid));

        var solid = At(page, Solid.X + Solid.Width / 2, Solid.Y + Solid.Height / 2);
        solid.R.Should().BeLessThan(30);
        solid.B.Should().BeInRange(110, 165);

        // Every pixel of the rectangle, not just one: counting them is what says the whole shape
        // was painted rather than a corner of it.
        var painted = 0;
        using (var pixels = page.GetPixels())
        {
            foreach (var pixel in pixels)
            {
                var colour = pixel.ToColor();
                if (colour != null && colour.B > colour.R + 80)
                    painted++;
            }
        }

        var scale = page.Width / 595.0;
        var expected = Solid.Width * Solid.Height * scale * scale;
        ((double)painted).Should().BeInRange(expected * 0.9, expected * 1.1);
    }

    private IMagickImage<byte> Rasterize(string name, Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        var images = _rasterized.Of(document, name);

        // A page painting a CMYK alternate is handed back as a CMYK raster, whose channels would
        // read here as cyan, magenta and yellow under the names R, G and B.
        var raster = images[0];
        if (raster.ColorSpace != ColorSpace.sRGB)
            raster.ColorSpace = ColorSpace.sRGB;
        return raster;
    }
}
