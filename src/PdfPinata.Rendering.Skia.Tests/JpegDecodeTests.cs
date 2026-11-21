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

    // The inks of the file as they are are what a PDF without a /Decode means, so these are drawn
    // as the inks say, where a codec that leaves the inversion in draws them the other way round.
    [Test]
    public void AMarkerlessCmykJpegIsDecodedAsItsInksAreStored()
    {
        var black = Decode(Asset("cmyk-black-nomarker.jpg"));
        var red = Decode(Asset("cmyk-red-nomarker.jpg"));

        ComponentsOf(Asset("cmyk-black-nomarker.jpg")).Should().Be(4);
        Asset("cmyk-black-nomarker.jpg").AsSpan().IndexOf("Adobe"u8).Should().BeLessThan(0, "it has no marker");
        // SkiaSharp alone draws it as (0, 0, 0) and red as (179, 171, 0).
        ShouldBeNear(black, 255, 127, 127);
        ShouldBeNear(red, 0, 0, 0);
    }

    // The red file's components use tables 0 and 1. A table 2 the file defines after the frame
    // header would overwrite a zero table of that number put before it, so the copy takes another.
    [Test]
    public void ASpareTableDefinedAfterTheFrameHeaderDoesNotDefeatTheFlattening()
    {
        var red = Asset("cmyk-red-nomarker.jpg");
        var withSpare = DefineTableAfterFrame(red, 2);

        ShouldBeNear(Decode(withSpare), 0, 0, 0);
    }

    [Test]
    public void AFileThatDefinesEveryTableIdentifierIsDeclined()
    {
        var red = Asset("cmyk-red-nomarker.jpg");
        var all = DefineTableAfterFrame(DefineTableAfterFrame(red, 2), 3);

        Decode(all).Should().BeNull();
    }

    // A table of ones, as a segment of its own, put straight after the frame header.
    private static byte[] DefineTableAfterFrame(byte[] jpeg, int id)
    {
        var at = FrameOf(jpeg);
        var length = (jpeg[at + 2] << 8) | jpeg[at + 3];
        var end = at + 2 + length;
        var table = new byte[69];
        table[0] = 0xFF;
        table[1] = 0xDB;
        table[3] = 67;
        table[4] = (byte)id;
        Array.Fill(table, (byte)1, 5, 64);

        var copy = new byte[jpeg.Length + table.Length];
        jpeg.AsSpan(0, end).CopyTo(copy);
        table.CopyTo(copy, end);
        jpeg.AsSpan(end).CopyTo(copy.AsSpan(end + table.Length));
        return copy;
    }

    private static byte[] Asset(string name)
    {
        using var stream = typeof(JpegDecodeTests).Assembly.GetManifestResourceStream("Assets/" + name);
        using var copy = new System.IO.MemoryStream();
        stream!.CopyTo(copy);
        return copy.ToArray();
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
