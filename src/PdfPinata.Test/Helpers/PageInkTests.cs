using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   That the pixel helpers on <see cref="PageInk"/> answer exactly what the private copies they
///   replaced answered, so that a test moved onto them still asserts what it did.
/// </summary>
/// <remarks>
///   Each copy is kept below as it stood, named for the file it came from. The two scales are both
///   here on purpose - half the copies divided by <c>PageSizeConverter.ToSize(PageSize.A4).Width</c>
///   and half by <c>595.0</c> - and they are the same number, which is one of the things this pins.
///
///   The image is noise rather than a drawing, and a width that is not a whole number of pixels per
///   point, so that every threshold is met on both sides and every rounding is exercised. Nothing is
///   rasterized, so none of this needs Ghostscript.
/// </remarks>
public class PageInkTests
{
    /// <summary>An A4 page at 150 dpi, the way <see cref="PdfHelper.Rasterize"/> truncates it.</summary>
    private const int Width = 1239, Height = 1754;

    private static readonly XRect Box = new(60.3, 60.7, 300.45, 30.2);

    private static MagickImage Noise()
    {
        var random = new Random(157);
        var bytes = new byte[Width * Height * 3];
        random.NextBytes(bytes);
        return new MagickImage(bytes, new PixelReadSettings(Width, Height, StorageType.Char, PixelMapping.RGB));
    }

    private static readonly Func<IMagickColor<byte>, bool>[] Tests =
    [
        PageInk.IsBlue,
        c => c.R < 110 && c.G < 110 && c.B < 110,
        c => c.R > 240 || c.G > 240,
    ];

    [Fact]
    public void AWholeImageIsCountedAsTheNineCopiesCountedIt()
    {
        using var image = Noise();

        foreach (var test in Tests)
            PageInk.Count(image, test).Should().Be(CountAsCircleAnnotationTestsDid(image, test));
    }

    [Fact]
    public void ABoxIsCountedAsBothFieldAppearanceCopiesCountedIt()
    {
        using var image = Noise();

        foreach (var test in Tests)
        {
            var count = PageInk.Count(image, Box, test);
            count.Should().Be(CountAsChoiceFieldAppearanceTestsDid(image, Box, test));
            count.Should().Be(CountAsTextFieldAppearanceTestsDid(image, Box, test));
        }
    }

    [Fact]
    public void IsBlueIsTheTestSixFilesWroteAlike()
    {
        using var image = Noise();
        using var pixels = image.GetPixels();

        foreach (var colour in pixels.Take(20000).Select(p => p.ToColor()))
            PageInk.IsBlue(colour).Should().Be(IsBlueAsPageEventsTestsHadIt(colour));
    }

    [Fact]
    public void APointIsReadAsEveryCopyReadIt()
    {
        using var image = Noise();

        foreach (var (x, y) in Points())
        {
            var at = Rgb(PageInk.At(image, x, y));
            at.Should().Be(Rgb(AtAsCircleAnnotationTestsHadIt(image, x, y)));
            at.Should().Be(Rgb(AtAsRadialGradientRenderingTestsHadIt(image, x, y)));
        }
    }

    [Fact]
    public void APlaceInARectangleIsReadAsEveryCopyReadIt()
    {
        using var image = Noise();

        foreach (var area in Areas())
        {
            Rgb(PageInk.Within(image, area)).Should().Be(Rgb(CentreAsSquareAnnotationTestsHadIt(image, area)));
            Rgb(PageInk.Within(image, area)).Should().Be(Rgb(SampleAsTranslucentImageRenderingTestsHadIt(image, area)));

            foreach (var (across, down) in new[] { (0.0, 0.5), (0.25, 0.5), (0.9, 0.1), (1.0 / 3, 2.0 / 3) })
            {
                Rgb(PageInk.Within(image, area, across, down))
                    .Should().Be(Rgb(SampleAtAsGradientTransparencyRenderingTestsHadIt(image, across, area, down)));
            }
        }
    }

    private static IEnumerable<(double X, double Y)> Points()
    {
        yield return (0, 0);
        yield return (100, 500);
        yield return (297.5, 421);
        yield return (123.456, 789.012);
        yield return (594.4, 841.5);
    }

    private static IEnumerable<XRect> Areas()
    {
        yield return Box;
        yield return new XRect(100, 500, 200, 100);
        yield return new XRect(72.5, 144.25, 13.3, 7.7);
    }

    private static (byte R, byte G, byte B) Rgb(IMagickColor<byte> c) => (c.R, c.G, c.B);

    // ----- the copies, as they stood -------------------------------------------------------------------

    private static int CountAsCircleAnnotationTestsDid(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }

    private static int CountAsChoiceFieldAppearanceTestsDid(IMagickImage<byte> image, XRect box, Func<IMagickColor<byte>, bool> match)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;
        var left = (int)(box.X * scale);
        var top = (int)(box.Y * scale);
        var right = (int)Math.Ceiling(box.Right * scale);
        var bottom = (int)Math.Ceiling(box.Bottom * scale);

        using var pixels = image.GetPixels();
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var c = pixels.GetPixel(x, y).ToColor();
                if (c != null && match(c))
                    count++;
            }
        }
        return count;
    }

    private static int CountAsTextFieldAppearanceTestsDid(IMagickImage<byte> image, XRect box, Func<IMagickColor<byte>, bool> match)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;
        using var pixels = image.GetPixels();
        var count = 0;
        for (var y = (int)(box.Y * scale); y < (int)Math.Ceiling(box.Bottom * scale); y++)
        {
            for (var x = (int)(box.X * scale); x < (int)Math.Ceiling(box.Right * scale); x++)
            {
                var c = pixels.GetPixel(x, y).ToColor();
                if (c != null && match(c))
                    count++;
            }
        }
        return count;
    }

    private static bool IsBlueAsPageEventsTestsHadIt(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    private static IMagickColor<byte> AtAsCircleAnnotationTestsHadIt(IMagickImage<byte> image, double x, double y)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;

        using var pixels = image.GetPixels();
        return pixels.GetPixel((int)(x * scale), (int)(y * scale)).ToColor();
    }

    private static IMagickColor<byte> AtAsRadialGradientRenderingTestsHadIt(IMagickImage<byte> page, double x, double y)
    {
        var scale = page.Width / 595.0;
        using var pixels = page.GetPixels();
        return pixels.GetPixel((int)(x * scale), (int)(y * scale)).ToColor();
    }

    private static IMagickColor<byte> CentreAsSquareAnnotationTestsHadIt(IMagickImage<byte> image, XRect box)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;
        var x = (int)((box.X + box.Width / 2) * scale);
        var y = (int)((box.Y + box.Height / 2) * scale);

        using var pixels = image.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }

    private static IMagickColor<byte> SampleAsTranslucentImageRenderingTestsHadIt(IMagickImage<byte> page, XRect area)
    {
        var scale = page.Width / 595.0;
        var x = (int)((area.X + area.Width / 2) * scale);
        var y = (int)((area.Y + area.Height / 2) * scale);

        using var pixels = page.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }

    private static IMagickColor<byte> SampleAtAsGradientTransparencyRenderingTestsHadIt(IMagickImage<byte> page, double across, XRect area, double down = 0.5)
    {
        var scale = page.Width / 595.0;
        var x = (int)((area.X + area.Width * across) * scale);
        var y = (int)((area.Y + area.Height * down) * scale);

        using var pixels = page.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }
}
