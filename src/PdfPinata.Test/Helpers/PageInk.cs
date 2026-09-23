using System.Collections.Generic;
using System.Linq;
using ImageMagick;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Where a rasterized page has ink on it, for the tests that check what was drawn rather than
///   what was written into the content stream.
/// </summary>
internal static class PageInk
{
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
}
