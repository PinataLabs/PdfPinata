using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagick;
using PdfPinata.Drawing;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Where a rasterized page has ink on it, for the tests that check what was drawn rather than
///   what was written into the content stream.
/// </summary>
/// <remarks>
///   The helpers taking a place on the page take it in world coordinates - points from the top left,
///   the space <c>XGraphics</c> draws in - and assume the page is A4, 595 points wide, which is what
///   every test using them draws on. The raster is in the same orientation, so the only conversion
///   is the resolution the page was drawn at, read off the image's width.
///
///   What counts as a given colour stays with each test, because the thresholds are tuned to what
///   that test draws and how the rasterizer blends its edges. Only <see cref="IsBlue"/> is here, the
///   one test six files had written alike.
/// </remarks>
internal static class PageInk
{
    /// <summary>The width of an A4 page in points, which is what the raster is scaled against.</summary>
    private const double A4Width = 595.0;

    /// <summary>
    ///   Every dark pixel of the image, as (x, y) in pixels from the top left.
    /// </summary>
    internal static List<(int X, int Y)> DarkPixelsOf(IMagickImage<byte> image)
    {
        using var pixels = image.GetPixels();
        return [..pixels
            .Where(pixel =>
            {
                var colour = pixel.ToColor();
                return colour is { R: < 128, G: < 128, B: < 128 };
            })
            .Select(pixel => (pixel.X, pixel.Y))];
    }

    /// <summary>How many pixels of the whole image <paramref name="match"/> accepts.</summary>
    internal static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }

    /// <summary>
    ///   How many pixels inside <paramref name="box"/>, given in world space on an A4 page,
    ///   <paramref name="match"/> accepts. A pixel the box only partly covers is counted in.
    /// </summary>
    internal static int Count(IMagickImage<byte> image, XRect box, Func<IMagickColor<byte>, bool> match)
    {
        var scale = ScaleOf(image);
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

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>), in world space on an A4 page.</summary>
    internal static IMagickColor<byte> At(IMagickImage<byte> image, double x, double y)
    {
        var scale = ScaleOf(image);
        using var pixels = image.GetPixels();
        return pixels.GetPixel((int)(x * scale), (int)(y * scale)).ToColor();
    }

    /// <summary>
    ///   The pixel a fraction <paramref name="across"/> and <paramref name="down"/> a rectangle given
    ///   in world space on an A4 page - its middle, by default.
    /// </summary>
    internal static IMagickColor<byte> Within(IMagickImage<byte> image, XRect area, double across = 0.5, double down = 0.5) =>
        At(image, area.X + area.Width * across, area.Y + area.Height * down);

    /// <summary>Pixels per point: the resolution an A4 page was rasterized at, over 72.</summary>
    internal static double ScaleOf(IMagickImage<byte> image) => image.Width / A4Width;

    /// <summary>A saturated blue, as the tests that draw one in pure blue look for it.</summary>
    internal static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;
}
