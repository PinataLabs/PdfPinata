using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   The image XObject a colour JPEG has always become, whichever backend encoded it: a
///   <c>/DCTDecode</c> stream holding the backend's JPEG unchanged, labelled <c>/DeviceRGB</c>, with
///   <c>/Interpolate true</c> as every XImage has by default, and no entry beyond those. Pinned when
///   <c>PdfImage</c> began reading the JPEG's frame header to label a grayscale one
///   <c>/DeviceGray</c> (#327), so that the colour case is seen not to move. Shared:
///   PdfPinata.ImageSharp4.Tests links this file.
/// </summary>
internal static class ColourJpegXObject
{
    internal static void ShouldBeAsBefore(PdfImage image, int width, int height, byte[] encodedJpeg)
    {
        image.Elements.Keys.OrderBy(key => key, System.StringComparer.Ordinal).Should().Equal(
            "/BitsPerComponent", "/ColorSpace", "/Filter", "/Height", "/Interpolate", "/Length", "/Subtype", "/Type", "/Width");
        image.Elements.GetBoolean(PdfImage.Keys.Interpolate).Should().BeTrue("an XImage interpolates unless told not to");
        image.Elements.GetName("/Type").Should().Be("/XObject");
        image.Elements.GetName("/Subtype").Should().Be("/Image");
        image.Elements.GetName(PdfImage.Keys.Filter).Should().Be("/DCTDecode");
        image.Elements.GetName(PdfImage.Keys.ColorSpace).Should().Be("/DeviceRGB");
        image.Elements.GetInteger(PdfImage.Keys.BitsPerComponent).Should().Be(8);
        image.Elements.GetInteger(PdfImage.Keys.Width).Should().Be(width);
        image.Elements.GetInteger(PdfImage.Keys.Height).Should().Be(height);
        image.Elements.GetInteger("/Length").Should().Be(encodedJpeg.Length);
        image.Stream.Value.Should().Equal(encodedJpeg);
    }
}
