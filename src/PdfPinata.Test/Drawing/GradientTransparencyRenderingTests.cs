using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   That a gradient with alpha actually blends. The structure tests can say the soft mask is
///   well formed; only rasterizing says a reader makes a blend of it rather than the flat opaque
///   band this library used to paint.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class GradientTransparencyRenderingTests : IDisposable
{
    const string OutDir = "Out/GradientTransparency";

    /// <summary>
    ///   Everything rasterized by one test, kept until the test is over. A page at 300 dpi is
    ///   tens of megabytes of unmanaged bitmap that the collector cannot see the size of.
    /// </summary>
    readonly List<MagickImageCollection> _rasterized = new List<MagickImageCollection>();

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static GradientTransparencyRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    /// <summary>The band the gradient is drawn across, in the space the drawing uses.</summary>
    static readonly XRect Band = new XRect(50, 50, 400, 200);

    [GoldenImageFact]
    public void ATransparentToOpaqueGradientLetsTheFillBeneathItThrough()
    {
        var page = Rasterize("fade_over_red", gfx =>
        {
            gfx.DrawRectangle(XBrushes.Red, Band);
            gfx.DrawRectangle(new XLinearGradientBrush(Band, XColor.FromArgb(0, 0, 0, 0), XColors.Black,
                XLinearGradientMode.Horizontal), Band);
        });

        var left = Sample(page, 0.10, 0.5);
        var middle = Sample(page, 0.50, 0.5);
        var right = Sample(page, 0.90, 0.5);

        // Red where the gradient is transparent, black where it is opaque, and darkening
        // between - which is exactly what the flat band this used to paint did not do.
        left.R.Should().BeGreaterThan(180);
        left.G.Should().BeLessThan(80);

        right.R.Should().BeLessThan(60);

        Luminance(middle).Should().BeLessThan(Luminance(left));
        Luminance(middle).Should().BeGreaterThan(Luminance(right));
    }

    [GoldenImageFact]
    public void AHalfTransparentGradientOverWhiteIsMidGreyRatherThanBlack()
    {
        var page = Rasterize("half_over_white", gfx =>
        {
            var half = XColor.FromArgb(128, 0, 0, 0);
            gfx.DrawRectangle(new XLinearGradientBrush(Band, half, half, XLinearGradientMode.Horizontal), Band);
        });

        foreach (var across in new[] { 0.2, 0.5, 0.8 })
        {
            var grey = Sample(page, across, 0.5);
            Luminance(grey).Should().BeInRange(90, 165, "half of black over white is mid grey");
        }
    }

    [GoldenImageFact]
    public void ARadialGradientWithATransparentEdgeVeilsWhatIsUnderIt()
    {
        var centre = new XPoint(Band.X + Band.Width / 2, Band.Y + Band.Height / 2);
        var page = Rasterize("radial_over_stripes", gfx =>
        {
            // Something with structure underneath, so that "obscured" means something.
            for (var y = Band.Y; y < Band.Y + Band.Height; y += 10)
                gfx.DrawRectangle(XBrushes.Red, new XRect(Band.X, y, Band.Width, 5));

            gfx.DrawRectangle(new XRadialGradientBrush(centre, centre, 0, 100,
                XColors.Black, XColor.FromArgb(0, 0, 0, 0)), Band);
        });

        var atCentre = Sample(page, 0.5, 0.5);
        var atEdge = Sample(page, 0.03, 0.5);

        // Opaque black at the middle of the circle, and the stripes still showing at the rim.
        Luminance(atCentre).Should().BeLessThan(60);
        atEdge.R.Should().BeGreaterThan(150);
    }

    [GoldenImageFact]
    public void AnOpaqueShapeDrawnAfterAMaskedGradientIsFullyOpaque()
    {
        var after = new XRect(50, 300, 400, 100);
        var page = Rasterize("mask_left_behind", gfx =>
        {
            gfx.DrawRectangle(new XLinearGradientBrush(Band, XColor.FromArgb(0, 0, 0, 0), XColors.Black,
                XLinearGradientMode.Horizontal), Band);
            gfx.DrawRectangle(XBrushes.Black, after);
        });

        // The rectangle below the gradient is under no mask, so it is black all the way across -
        // including at the end where the gradient's own mask would have made it invisible.
        foreach (var across in new[] { 0.1, 0.5, 0.9 })
            Luminance(SampleAt(page, across, after)).Should().BeLessThan(40);
    }

    [GoldenImageFact]
    public void TwoGradientsOnOnePageDoNotMaskEachOther()
    {
        var lower = new XRect(50, 300, 400, 200);
        var page = Rasterize("two_ramps", gfx =>
        {
            // Fades in to the right...
            gfx.DrawRectangle(new XLinearGradientBrush(Band, XColor.FromArgb(0, 0, 0, 0), XColors.Black,
                XLinearGradientMode.Horizontal), Band);
            // ...and the other fades out to the right.
            gfx.DrawRectangle(new XLinearGradientBrush(lower, XColors.Black, XColor.FromArgb(0, 0, 0, 0),
                XLinearGradientMode.Horizontal), lower);
        });

        Luminance(SampleAt(page, 0.1, Band)).Should().BeGreaterThan(200);
        Luminance(SampleAt(page, 0.9, Band)).Should().BeLessThan(60);

        // The second ramp runs the other way, which it cannot do if the first one's mask is
        // still in force over it.
        Luminance(SampleAt(page, 0.1, lower)).Should().BeLessThan(60);
        Luminance(SampleAt(page, 0.9, lower)).Should().BeGreaterThan(200);
    }

    // ----- rasterizing and reading pixels ---------------------------------------------------------

    IMagickImage<byte> Rasterize(string name, Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    /// <summary>The colour a fraction of the way across and down the band under test.</summary>
    static IMagickColor<byte> Sample(IMagickImage<byte> page, double across, double down)
    {
        return SampleAt(page, across, Band, down);
    }

    /// <summary>The colour a fraction of the way across and down a rectangle of the drawing.</summary>
    static IMagickColor<byte> SampleAt(IMagickImage<byte> page, double across, XRect area, double down = 0.5)
    {
        // The drawing is in points from the top left, and so is the raster, so the only
        // conversion is the resolution the page was drawn at.
        var scale = page.Width / 595.0;
        var x = (int)((area.X + area.Width * across) * scale);
        var y = (int)((area.Y + area.Height * down) * scale);

        using var pixels = page.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }

    static double Luminance(IMagickColor<byte> colour) => 0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B;
}
