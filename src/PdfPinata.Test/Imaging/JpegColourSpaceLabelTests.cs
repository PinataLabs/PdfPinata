using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PinataLayout.DocumentObjectModel.Shapes;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   The colour space a JPEG image XObject is labelled with is read from the start-of-frame
///   segment of the JPEG it embeds: one component is <c>/DeviceGray</c>, anything else, or no
///   frame found, <c>/DeviceRGB</c> as every JPEG was before #327.
/// </summary>
/// <remarks>
///   The JPEGs here are hand-built segment sequences rather than images: an image source hands its
///   bytes to the document as they are, so what is tested is how the segments are walked, which no
///   real encoder varies. None of them is ever decoded.
/// </remarks>
public class JpegColourSpaceLabelTests
{
    private static readonly byte[] StartOfImage = [0xFF, 0xD8];

    /// <summary>A baseline frame of a 16-pixel square with <paramref name="components"/> components.</summary>
    private static byte[] Frame(byte components) =>
        [0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x10, 0x00, 0x10, components, 0x01, 0x11, 0x00];

    private static readonly byte[] EndOfImage = [0xFF, 0xD9];

    [Test]
    public void AOneComponentFrameIsLabelledGray()
    {
        ColourSpaceOf([..StartOfImage, ..Frame(1), ..EndOfImage]).Should().Be("/DeviceGray");
    }

    [Test]
    public void FillBytesBeforeTheFrameAreSkipped()
    {
        // Any number of 0xFF may precede a marker (ITU-T T.81 B.1.1.2).
        ColourSpaceOf([..StartOfImage, 0xFF, 0xFF, ..Frame(1), ..EndOfImage]).Should().Be("/DeviceGray");
    }

    [Test]
    public void ASegmentBeforeTheFrameIsSkippedByItsLength()
    {
        byte[] app0 = [0xFF, 0xE0, 0x00, 0x06, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];

        ColourSpaceOf([..StartOfImage, ..app0, ..Frame(1), ..EndOfImage]).Should().Be("/DeviceGray");
    }

    [Test]
    public void AThreeComponentFrameIsLabelledRgb()
    {
        ColourSpaceOf([..StartOfImage, ..Frame(3), ..EndOfImage]).Should().Be("/DeviceRGB");
    }

    [Test]
    public void BytesThatAreNotASegmentEndTheSearchAndTheImageIsLabelledRgb()
    {
        // A one-component frame follows, but nothing after bytes that are not a marker is read.
        ColourSpaceOf([..StartOfImage, 0x00, 0x00, ..Frame(1), ..EndOfImage]).Should().Be("/DeviceRGB");
    }

    [Test]
    [Arguments((byte)0xDA)] // the scan begins
    [Arguments((byte)0xD9)] // the image ends
    public void NoFrameBeforeTheScanOrTheEndLeavesTheImageLabelledRgb(byte marker)
    {
        byte[] segment = [0xFF, marker, 0x00, 0x02];

        ColourSpaceOf([..StartOfImage, ..segment, ..Frame(1), ..EndOfImage]).Should().Be("/DeviceRGB");
    }

    [Test]
    public void AJpegThatEndsBeforeAnyFrameIsLabelledRgb()
    {
        byte[] app0 = [0xFF, 0xE0, 0x00, 0x06, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];

        ColourSpaceOf([..StartOfImage, ..app0]).Should().Be("/DeviceRGB");
    }

    private static string ColourSpaceOf(byte[] jpeg)
    {
        var document = new PdfDocument();
        // Left as it is given, so that the stream is the JPEG and nothing else.
        document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(XImage.FromImageSource(new FixedJpeg(jpeg)), 0, 0, 16, 16);

        var image = document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Single(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image");
        image.Elements.GetName("/Filter").Should().Be("/DCTDecode", "the JPEG is embedded as it was given");
        return image.Elements.GetName("/ColorSpace");
    }

    /// <summary>An opaque image source whose JPEG is the bytes it was made with.</summary>
    private sealed class FixedJpeg(byte[] jpeg) : ImageSource.IImageSource
    {
        public int Width => 16;
        public int Height => 16;
        public string Name => "fixed-" + Convert.ToHexString(jpeg);
        public bool Transparent => false;

        public void SaveAsJpeg(MemoryStream ms) => ms.Write(jpeg, 0, jpeg.Length);

        public PixelBuffer GetPixels() =>
            throw new NotSupportedException("A JPEG image is embedded as its bytes, never as its pixels.");
    }
}
