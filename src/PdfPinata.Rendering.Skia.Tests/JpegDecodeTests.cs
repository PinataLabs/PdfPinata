using System;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// The JPEG decoding the device does for the core: what a PDF's <c>/DCTDecode</c> holds, turned
/// into finished RGB samples whatever its colour model.
/// </summary>
public class JpegDecodeTests
{
    [Test]
    public void AnRgbJpegIsDecodedToItsColours()
    {
        var image = Decode(Jpeg(new SKColor(200, 30, 60)));

        image.Format.Should().Be(PageImageFormat.Rgba32);
        image.Width.Should().Be(16);
        image.Height.Should().Be(8);
        ShouldBeNear(image, 200, 30, 60);
    }

    [Test]
    public void AGreyJpegIsDecodedToRgb()
    {
        using var grey = new MagickImage(new MagickColor("#808080"), 16, 8);
        grey.ColorSpace = ColorSpace.Gray;
        grey.ColorType = ColorType.Grayscale;

        ShouldBeNear(Decode(grey.ToByteArray(MagickFormat.Jpeg)), 128, 128, 128);
    }

    // ImageMagick writes CMYK as Photoshop does: an Adobe marker, and every ink stored inverted.
    // Pure cyan is no red, full green and full blue.
    [Test]
    public void ACmykJpegIsDecodedToRgbAndItsInvertedInksAreReadAsInks()
    {
        using var cyan = new MagickImage(new MagickColor(0, 255, 255), 16, 8);
        cyan.ColorSpace = ColorSpace.CMYK;
        var bytes = cyan.ToByteArray(MagickFormat.Jpeg);

        ComponentsOf(bytes).Should().Be(4, "the test is of a CMYK JPEG");
        bytes.AsSpan().IndexOf("Adobe"u8).Should().BeGreaterThan(0, "and of one whose inks are inverted");
        ShouldBeNear(Decode(bytes), 0, 255, 255);
    }

    [Test]
    public void DataThatIsNotAJpegIsNotDecoded()
    {
        using var bitmap = new SKBitmap(4, 4);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        Decode(png.ToArray()).Should().BeNull();
        Decode([1, 2, 3, 4]).Should().BeNull();
        Decode([]).Should().BeNull();
    }

    // The header says 20000 by 20000, which is 400 million samples, over the 2^28 a page image may
    // have; the data after it is a real 16 by 8 JPEG, so it is the header alone that is refused.
    [Test]
    public void AJpegWhoseHeaderClaimsTooManySamplesIsNotDecoded()
    {
        var bytes = Jpeg(new SKColor(200, 30, 60));
        var frame = FrameOf(bytes);
        bytes[frame + 5] = 20000 >> 8;
        bytes[frame + 6] = 20000 & 0xFF;
        bytes[frame + 7] = 20000 >> 8;
        bytes[frame + 8] = 20000 & 0xFF;

        Decode(bytes).Should().BeNull();
    }

    private static int FrameOf(byte[] jpeg)
    {
        for (var i = 0; i + 9 < jpeg.Length; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] is >= 0xC0 and <= 0xC2)
                return i;
        }

        throw new InvalidOperationException("The JPEG has no frame header.");
    }

    private static PageImage Decode(byte[] data)
    {
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var device = new SkiaPageDevice(canvas);
        return device.DecodeImage(PageImageCodec.Jpeg, data);
    }

    private static byte[] Jpeg(SKColor colour)
    {
        using var bitmap = new SKBitmap(16, 8);
        bitmap.Erase(colour);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    // The number of components the first start-of-frame marker gives: FF C0 to FF C2, then the
    // length, the precision, the height and the width.
    private static int ComponentsOf(byte[] jpeg)
    {
        for (var i = 0; i + 9 < jpeg.Length; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] is >= 0xC0 and <= 0xC2)
                return jpeg[i + 9];
        }

        return 0;
    }

    // JPEG loses a little of every colour, so each channel is within a few levels of the one it encoded.
    private static void ShouldBeNear(PageImage image, int red, int green, int blue)
    {
        var data = image.Data.Span;
        for (var sample = 0; sample < image.Width * image.Height; sample++)
        {
            data[sample * 4].Should().BeCloseTo((byte)red, 4);
            data[sample * 4 + 1].Should().BeCloseTo((byte)green, 4);
            data[sample * 4 + 2].Should().BeCloseTo((byte)blue, 4);
            data[sample * 4 + 3].Should().Be(255);
        }
    }
}
