using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Skia;
using SkiaSharp;
using Xunit;

namespace PdfPinata.Test.Imaging;

/// <summary>
/// PdfPinata.Skia hands PdfImage the decoded pixels themselves - tightly packed, top-down,
/// B, G, R, A - and these tests pin that layout. They deliberately use an image with a different
/// value in every corner and a different value in every channel: a solid fill, which is what the
/// tests they replaced used, passes just as happily against a vertical flip or an R/B swap.
/// </summary>
public class SkiaImageSourceTest
{
    private static PixelBuffer GetPixels(SKBitmap bitmap)
        => SkiaImageSource.FromSkiaBitmap(bitmap, transparent: true).GetPixels();

    private static SKBitmap CreateBitmap(int width, int height)
        => new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));

    [Fact]
    public void GetPixelsReportsTheSizeOfTheBitmap()
    {
        var pixels = GetPixels(CreateBitmap(3, 2));

        pixels.Width.Should().Be(3);
        pixels.Height.Should().Be(2);
        pixels.Pixels.Length.Should().Be(3 * 2 * 4, "a row is exactly Width * 4 bytes, never padded");
        pixels.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void GetPixelsKeepsRowsTopDown()
    {
        var bitmap = CreateBitmap(1, 2);
        bitmap.SetPixel(0, 0, new SKColor(10, 20, 30, 255));    // top row
        bitmap.SetPixel(0, 1, new SKColor(40, 50, 60, 255));    // bottom row

        var pixels = GetPixels(bitmap).Pixels.Span;

        // Row 0 of the buffer is row 0 of the image. The BMP detour this replaced turned the rows
        // over on the way out and back again on the way in, which cancelled and read as correct.
        pixels[0].Should().Be(30, "the top row comes first, blue channel leading");
        pixels[1].Should().Be(20);
        pixels[2].Should().Be(10);

        pixels[4].Should().Be(60, "the bottom row comes last");
        pixels[5].Should().Be(50);
        pixels[6].Should().Be(40);
    }

    [Fact]
    public void GetPixelsPutsEveryCornerWhereItBelongs()
    {
        // Four distinct corners, so a vertical flip, a horizontal flip and a half turn are each
        // told apart from the right answer and from each other.
        var bitmap = CreateBitmap(2, 2);
        bitmap.SetPixel(0, 0, new SKColor(11, 12, 13, 255));
        bitmap.SetPixel(1, 0, new SKColor(21, 22, 23, 255));
        bitmap.SetPixel(0, 1, new SKColor(31, 32, 33, 255));
        bitmap.SetPixel(1, 1, new SKColor(41, 42, 43, 255));

        var pixels = GetPixels(bitmap).Pixels.ToArray();

        pixels.Should().Equal(
            13, 12, 11, 255,
            23, 22, 21, 255,
            33, 32, 31, 255,
            43, 42, 41, 255);
    }

    [Fact]
    public void GetPixelsKeepsStraightAlpha()
    {
        var bitmap = CreateBitmap(1, 1);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0, 128));

        var pixels = GetPixels(bitmap).Pixels.Span;

        // Skia premultiplies by default; that would store red as ~128 and darken the image.
        pixels[2].Should().Be(255, "colour channels must stay unpremultiplied");
        pixels[3].Should().Be(128, "alpha is carried in the fourth byte");
    }

    [Fact]
    public void GetPixelsRefusesAPremultipliedBitmap()
    {
        // FromSkiaBitmap takes whatever bitmap a caller has already decoded, and premultiplied
        // alpha is the one wrong answer that looks right: PdfImage writes the colour channels into
        // the image stream and the alpha into the /SMask separately, so a premultiplied pixel comes
        // out darkened in proportion to its own transparency and nothing says so.
        var bitmap = new SKBitmap(
            new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul));

        var refusing = () => GetPixels(bitmap);

        refusing.Should().Throw<InvalidOperationException>()
            .WithMessage("*premultiplied*");
    }

    [Fact]
    public void GetPixelsAcceptsAnOpaqueBitmap()
    {
        // Opaque is not premultiplied-by-another-name: alpha is 255 throughout, so the colour
        // channels are the same bytes either way and refusing it would turn away a valid image.
        var bitmap = new SKBitmap(
            new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Opaque));
        bitmap.SetPixel(0, 0, new SKColor(10, 20, 30, 255));

        GetPixels(bitmap).Pixels.ToArray().Should().Equal(30, 20, 10, 255);
    }

    [Fact]
    public void TransparentPngRoundTripsThroughPdfImage()
    {
        var bitmap = CreateBitmap(8, 8);
        bitmap.Erase(new SKColor(0, 128, 255, 100));

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();

        var document = new PdfDocument();
        var renderer = XGraphics.FromPdfPage(document.AddPage());
        renderer.DrawImage(XImage.FromStream(() => new MemoryStream(png)), new XPoint(0, 0));

        using var ms = new MemoryStream();
        document.Save(ms);

        ms.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OpaqueJpegTakesTheJpegPath()
    {
        var bitmap = CreateBitmap(8, 8);
        bitmap.Erase(SKColors.Green);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var jpeg = encoded.ToArray();

        var xImage = XImage.FromStream(() => new MemoryStream(jpeg));

        // Preserves the previous ImageSharp behaviour: only PNG sources are treated as transparent.
        xImage.Format.Should().Be(XImageFormat.Jpeg);
    }
}
