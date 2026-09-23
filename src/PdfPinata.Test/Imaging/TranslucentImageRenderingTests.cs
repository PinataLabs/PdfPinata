using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Skia;
using PdfPinata.Test.Helpers;
using SkiaSharp;
using Xunit;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   That a partly transparent image is still there once a reader has drawn it.
///   <see cref="ImagePixelRoundTripTests"/> says the masks are well formed, and said so while
///   every pixel below alpha 128 was being thrown away: a dictionary is not a picture. These
///   tests rasterize, which is the only thing that would have caught it.
/// </summary>
/// <remarks>
///   The defect was that the 1-bit stencil <c>/Mask</c> was written beside the 8-bit
///   <c>/SMask</c>. ISO 32000-1 Table 89 has the soft mask override the stencil, and pdf.js obeys
///   that, but Ghostscript applies both - so an image whose alpha was wholly below the stencil's
///   threshold of 128 was embedded, referenced, and invisible. Reported upstream against a
///   palette PNG as empira/PDFsharp#392; the palette had nothing to do with it, and this fork
///   never had a palette path to begin with, both backends handing back BGRA whatever went in.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class TranslucentImageRenderingTests : IDisposable
{
    private const string OutDir = "Out/TranslucentImage";

    /// <summary>
    ///   Everything rasterized by one test, kept until the test is over - a page is tens of
    ///   megabytes of unmanaged bitmap the collector cannot see the size of.
    /// </summary>
    private readonly List<MagickImageCollection> _rasterized = [];

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static TranslucentImageRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    /// <summary>
    ///   39% of black over white is a light grey, and it used to be nothing at all: every pixel
    ///   sat below the stencil's threshold, so the stencil declared the whole image transparent.
    /// </summary>
    [GoldenImageFact]
    public void AUniformlyTranslucentImageIsDrawnRatherThanErased()
    {
        var page = Rasterize("uniform_alpha_100", Translucent(100));

        // Black at alpha 100 over white is 255 - 100, give or take how a rasterizer rounds.
        Luminance(Sample(page)).Should().BeInRange(140, 170);
    }

    /// <summary>
    ///   The stencil rounded alpha to one of two values at 128, so the two sides of it came out
    ///   as opposites - one invisible and one nearly solid - where they should be a shade apart.
    /// </summary>
    [GoldenImageFact]
    public void TheTwoSidesOfTheOldStencilThresholdAreOnlyOneShadeApart()
    {
        var below = Luminance(Sample(Rasterize("alpha_127", Translucent(127))));
        var above = Luminance(Sample(Rasterize("alpha_128", Translucent(128))));

        below.Should().BeInRange(115, 145);
        above.Should().BeInRange(115, 145);
        Math.Abs(below - above).Should().BeLessThan(8, "one step of alpha is one step of grey");
    }

    /// <summary>
    ///   The document from the upstream report: a 4-bit palette PNG whose <c>tRNS</c> gives its
    ///   five entries the alphas 0, 255, 55, 199 and 121. Two of those are below 128, and those
    ///   are the antialiasing - so the stencil left the shape with hard edges and fewer shades
    ///   than it was drawn with.
    /// </summary>
    [GoldenImageFact]
    public void EveryShadeOfAPartlyTransparentImageSurvives()
    {
        var image = XImage.FromFile(Path.Combine("Assets", "Drawing", "indexed-trns.png"));
        var page = Rasterize("indexed_trns", gfx => gfx.DrawImage(image, Area.X, Area.Y, Area.Width, Area.Height));

        // All five alphas reach the page, so the greys they make are all distinct. Counting them
        // rather than naming them keeps this independent of where in the shape each one falls.
        var greys = new HashSet<byte>();
        using (var pixels = page.GetPixels())
        {
            foreach (var pixel in pixels)
            {
                var colour = pixel.ToColor();
                if (colour != null && colour.R == colour.G && colour.G == colour.B)
                    greys.Add(colour.R);
            }
        }

        // White, plus one grey per alpha other than the fully transparent one. The stencil left
        // three; a rasterizer's own antialiasing may add more, so this is a floor.
        greys.Count.Should().BeGreaterThanOrEqualTo(5);
    }

    // ----- arrangements ---------------------------------------------------------------------------

    /// <summary>Where the image is drawn, in the space the drawing uses.</summary>
    private static readonly XRect Area = new(50, 50, 240, 120);

    /// <summary>Draws a black square at one alpha, filling <see cref="Area"/>.</summary>
    private static Action<XGraphics> Translucent(byte alpha)
    {
        return gfx =>
        {
            var bitmap = new SKBitmap(new SKImageInfo(16, 16, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, new SKColor(0, 0, 0, alpha));

            var image = XImage.FromImageSource(SkiaImageSource.FromSkiaBitmap(bitmap, transparent: true));
            gfx.DrawImage(image, Area.X, Area.Y, Area.Width, Area.Height);
        };
    }

    // ----- rasterizing and reading pixels ---------------------------------------------------------

    private IMagickImage<byte> Rasterize(string name, Action<XGraphics> draw)
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

    /// <summary>The colour at the middle of <see cref="Area"/>.</summary>
    private static IMagickColor<byte> Sample(IMagickImage<byte> page)
    {
        // The drawing is in points from the top left, and so is the raster, so the only
        // conversion is the resolution the page was drawn at.
        var scale = page.Width / 595.0;
        var x = (int)((Area.X + Area.Width / 2) * scale);
        var y = (int)((Area.Y + Area.Height / 2) * scale);

        using var pixels = page.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }

    private static double Luminance(IMagickColor<byte> colour) => 0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B;
}
