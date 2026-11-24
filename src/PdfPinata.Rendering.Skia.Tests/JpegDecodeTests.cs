using System;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Rendering.Skia.Tests.Helpers;
using PdfPinata.Test.Helpers;
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

    // Every one of these ended the process inside SkiaSharp 4.156.0's codec on Windows x64, with an
    // access violation no handler catches, so a device that lets one through fails this run by
    // ending it and not by an assertion.
    [Test]
    [Arguments(CorruptJpeg.QuantizationTableNumberedFour)]
    [Arguments(CorruptJpeg.QuantizationTableOfWideValuesInTheRoomOfNarrow)]
    [Arguments(CorruptJpeg.QuantizationTableNeverDefined)]
    [Arguments(CorruptJpeg.HuffmanTableNumberedFour)]
    [Arguments(CorruptJpeg.HuffmanTableWhoseCodesDoNotFit)]
    [Arguments(CorruptJpeg.HuffmanTableNeverDefined)]
    [Arguments(CorruptJpeg.FrameWithNoWidth)]
    [Arguments(CorruptJpeg.ComponentSampledZeroTimes)]
    [Arguments(CorruptJpeg.SecondFrame)]
    [Arguments(CorruptJpeg.FrameOfAnUnsupportedProcess)]
    [Arguments(CorruptJpeg.ScanOfAComponentTheFrameLacks)]
    [Arguments(CorruptJpeg.ScanOutOfTheFramesOrder)]
    [Arguments(CorruptJpeg.SegmentRunningPastTheEnd)]
    [Arguments(CorruptJpeg.UnknownMarker)]
    public void AJpegWhoseHeaderIsCorruptIsNotDecoded(string defect)
    {
        var bytes = CorruptJpeg.With(Jpeg(new SKColor(200, 30, 60)), defect);

        Decode(bytes).Should().BeNull();
    }

    // Skia's codec gives up at the hundredth scan, and giving up is what ends the process.
    [Test]
    public void AProgressiveJpegOfAHundredScansIsNotDecoded()
    {
        using var image = new MagickImage(new MagickColor(200, 30, 60), 16, 8);
        image.Settings.Interlace = Interlace.Jpeg;
        var bytes = image.ToByteArray(MagickFormat.Jpeg);
        ReadOnlySpan<byte> refinement = [0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x00, 0x10];
        var many = new System.IO.MemoryStream();
        many.Write(bytes, 0, bytes.Length - 2);
        for (var scan = 0; scan < 100; scan++)
            many.Write(refinement);
        many.Write(bytes, bytes.Length - 2, 2);

        Decode(many.ToArray()).Should().BeNull();
    }

    // Every block adds the largest difference a 15-bit symbol can code, 32767, to a running int,
    // which 65,792 blocks overflow. libjpeg-turbo calls that fatal where all other bad data is
    // only warned about.
    [Test]
    public void AProgressiveJpegWhoseDcValuesCanOverflowIsNotDecoded()
    {
        const int Width = 2056, Height = 2048;
        var file = new System.IO.MemoryStream();
        file.Write([0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x43, 0x00]);
        for (var value = 0; value < 64; value++)
            file.WriteByte(1);
        file.Write([0xFF, 0xC2, 0x00, 0x0B, 0x08, Height >> 8, Height & 0xFF, Width >> 8, Width & 0xFF, 0x01, 0x01, 0x11, 0x00]);
        file.Write([0xFF, 0xC4, 0x00, 0x14, 0x00, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15]);
        file.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00]);
        for (var block = 0; block < Width / 8 * (Height / 8); block++)
            file.Write([0x7F, 0xFF, 0x00]);
        file.Write([0xFF, 0xD9]);

        Decode(file.ToArray()).Should().BeNull();
    }

    // libjpeg-turbo's arithmetic decoder calls running out of data fatal, so a file cut short
    // would end the process, and a whole one cannot be told from it without decoding.
    [Test]
    public void AnArithmeticCodedJpegIsNotDecoded()
    {
        using var image = new MagickImage(new MagickColor(200, 30, 60), 16, 8);
        image.Settings.SetDefine(MagickFormat.Jpeg, "arithmetic-coding", "true");
        var bytes = image.ToByteArray(MagickFormat.Jpeg);

        (bytes.AsSpan().IndexOf([(byte)0xFF, (byte)0xC9]) > 0).Should().BeTrue("the test is of an arithmetic-coded frame");
        Decode(bytes).Should().BeNull();
    }

    [Test]
    [Arguments(CorruptJpeg.QuantizationTableNumberedFour)]
    [Arguments(CorruptJpeg.HuffmanTableNeverDefined)]
    [Arguments(CorruptJpeg.ScanOutOfTheFramesOrder)]
    public void APageWithACorruptJpegIsDrawnWithoutItAndSaysSo(string defect)
    {
        var bytes = CorruptJpeg.With(Jpeg(new SKColor(200, 30, 60)), defect);
        var corrupt = HandWritten.Page("0 0 1 rg 0 0 20 20 re f q 16 0 0 8 30 30 cm /Im1 Do Q", page: (document, page) =>
            HandWritten.Image(document, page, "/Im1", bytes, image =>
            {
                image.Elements.SetInteger("/Width", 16);
                image.Elements.SetInteger("/Height", 8);
                image.Elements.SetName("/ColorSpace", "/DeviceRGB");
                image.Elements.SetInteger("/BitsPerComponent", 8);
                image.Elements.SetName("/Filter", "/DCTDecode");
            })).Reopened().Pages[0];

        using var bitmap = new SKBitmap(200, 150);
        using var canvas = new SKCanvas(bitmap);
        var result = corrupt.Render(canvas);

        result.Diagnostics.Should().ContainSingle().Which.Problem.Should().Be(PageRenderProblem.UnsupportedImage);
        bitmap.GetPixel(10, 140).Should().Be(new SKColor(0, 0, 255), "the rest of the page is still drawn");
    }

    // A frame decoded from one scan is never read past that scan, so what follows it cannot stop
    // the decode and is no reason to refuse the file.
    [Test]
    public void ABaselineJpegWithAnythingAfterItsScanIsStillDecoded()
    {
        var bytes = Jpeg(new SKColor(200, 30, 60));
        byte[] rubbish = [0xFF, 0x02, 0xFF, 0xDB, 0x00, 0x03, 0x09, 0xFF, 0xD9];
        var followed = new byte[bytes.Length - 2 + rubbish.Length];
        bytes.AsSpan(0, bytes.Length - 2).CopyTo(followed);
        rubbish.CopyTo(followed, bytes.Length - 2);

        ShouldBeNear(Decode(followed), 200, 30, 60);
    }

    // The decoder reads every scan of these before it answers a row, so every marker of them is
    // checked, where a baseline file is checked up to its one scan.
    [Test]
    [Arguments(true, "4:2:0")]
    [Arguments(true, "4:4:4")]
    [Arguments(false, "4:2:0")]
    [Arguments(false, "4:1:1")]
    public void AJpegOfAnotherLayoutIsDecoded(bool progressive, string sampling)
    {
        using var image = new MagickImage(new MagickColor(200, 30, 60), 40, 24);
        image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", sampling);
        if (progressive)
            image.Settings.Interlace = Interlace.Jpeg;
        var bytes = image.ToByteArray(MagickFormat.Jpeg);

        (bytes.AsSpan().IndexOf([(byte)0xFF, (byte)0xC2]) > 0).Should().Be(progressive, "the test is of that kind of frame");
        ShouldBeNear(Decode(bytes), 200, 30, 60);
    }

    // The table is read from the tables a decoder knows when a file defines none, as the frames of
    // a Motion JPEG do.
    [Test]
    public void ABaselineJpegWithoutItsHuffmanTablesIsDecodedWithTheStandardOnes()
    {
        using var image = new MagickImage(new MagickColor(200, 30, 60), 16, 8);
        image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", "false");
        var bytes = WithoutSegments(image.ToByteArray(MagickFormat.Jpeg), 0xC4);

        bytes.AsSpan().IndexOf([(byte)0xFF, (byte)0xC4]).Should().BeLessThan(0, "the test is of a file with no table");
        ShouldBeNear(Decode(bytes), 200, 30, 60);
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

    // The file without any segment of this marker before its first scan.
    private static byte[] WithoutSegments(byte[] jpeg, byte marker)
    {
        var kept = new System.IO.MemoryStream();
        kept.Write(jpeg, 0, 2);
        var at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF && jpeg[at + 1] != 0xDA)
        {
            var length = 2 + ((jpeg[at + 2] << 8) | jpeg[at + 3]);
            if (jpeg[at + 1] != marker)
                kept.Write(jpeg, at, length);
            at += length;
        }

        kept.Write(jpeg, at, jpeg.Length - at);
        return kept.ToArray();
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
