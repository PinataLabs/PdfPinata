using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   An opaque image reaches the PDF as the JPEG the backend writes, and <c>PdfImage</c> labels every
///   such JPEG <c>/DeviceRGB</c>. So the backend has to write three components whatever the source
///   held. ImageSharp 2.x always did, because its encoder cannot write CMYK or YCCK and falls back to
///   YCbCr; ImageSharp 4's can, and keeps the colour type it decoded, so a CMYK photograph came out
///   as a four-component JPEG and was drawn in the wrong colours.
///   <para>
///     <c>Imaging/cmyk-quadrants.jpg</c> was made with ImageMagick 7.1.2:
///     <c>magick -size 16x16 xc:red ( -size 16x16 xc:lime ) +append ( -size 16x16 xc:blue -size 16x16 xc:white +append ) -append -colorspace CMYK -quality 95 cmyk-quadrants.jpg</c>,
///     a 32-pixel square of four pure colours, written as a four-component CMYK JPEG.
///   </para>
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it runs against both ImageSharp
///     backends. See #305.
///   </para>
/// </summary>
public class ImageSharpCmykJpegTests
{
    private const string CmykAsset = "Imaging/cmyk-quadrants.jpg";

    [Test]
    public void TheSourceReallyIsAFourComponentJpeg()
    {
        ComponentCount(Asset(CmykAsset)).Should().Be(4, "otherwise the test below proves nothing");
    }

    [Test]
    public void ACmykJpegIsWrittenAsAThreeComponentJpeg()
    {
        var jpeg = new Probe().Jpeg(Asset(CmykAsset));

        ComponentCount(jpeg).Should().Be(3, "PdfImage labels the JPEG /DeviceRGB");
    }

    [Test]
    // Each quadrant's centre, and the colour ImageMagick filled it with.
    [Arguments(8, 8, 255, 0, 0)]
    [Arguments(24, 8, 0, 255, 0)]
    [Arguments(8, 24, 0, 0, 255)]
    [Arguments(24, 24, 255, 255, 255)]
    public void ACmykJpegKeepsItsColours(int x, int y, int red, int green, int blue)
    {
        using var written = SKBitmap.Decode(new Probe().Jpeg(Asset(CmykAsset)));
        var pixel = written.GetPixel(x, y);

        // Two lossy encodes, and a CMYK to RGB conversion, lie between the fill and this pixel.
        const int tolerance = 24;
        ((int)pixel.Red).Should().BeCloseTo(red, tolerance);
        ((int)pixel.Green).Should().BeCloseTo(green, tolerance);
        ((int)pixel.Blue).Should().BeCloseTo(blue, tolerance);
    }

    // ----- arrangements -----

    private static byte[] Asset(string name) =>
        File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(name.Split('/')));

    /// <summary>
    ///   The number of components the JPEG's start-of-frame segment declares: the byte after its
    ///   precision, height and width.
    /// </summary>
    private static int ComponentCount(byte[] jpeg)
    {
        var at = 2;
        while (at + 4 <= jpeg.Length)
        {
            jpeg[at].Should().Be(0xFF, "a JPEG's segments each open with a marker");
            var marker = jpeg[at + 1];
            var length = (jpeg[at + 2] << 8) | jpeg[at + 3];

            // SOF0 to SOF15, less DHT (C4), JPG (C8) and DAC (CC), which share the range.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                return jpeg[at + 9];

            at += 2 + length;
        }

        throw new InvalidDataException("The JPEG has no start-of-frame segment.");
    }

    /// <summary>
    ///   The test module in PdfPinata.Test registers Skia, so the ImageSharp backend is called through
    ///   a subclass, as the public <see cref="ImageSource"/> methods call it.
    /// </summary>
    private sealed class Probe : ImageSharpImageSource<Rgba32>
    {
        public byte[] Jpeg(byte[] file)
        {
            var source = FromBinaryImpl("probe", () => file);
            using var _ = source as IDisposable;
            source.Transparent.Should().BeFalse("an opaque image is written as a JPEG");

            using var ms = new MemoryStream();
            source.SaveAsJpeg(ms);
            return ms.ToArray();
        }
    }
}
