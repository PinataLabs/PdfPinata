using System.IO;
using AwesomeAssertions;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   What a JPEG's start-of-frame segment declares, read straight from the bytes, for the tests that
///   check a JPEG against the colour space it is labelled with.
/// </summary>
internal static class JpegFrame
{
    /// <summary>
    ///   The number of components the JPEG's start-of-frame segment declares: the byte after its
    ///   precision, height and width.
    /// </summary>
    internal static int ComponentCount(byte[] jpeg)
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
}
