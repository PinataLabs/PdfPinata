using System;

namespace PdfPinata.Rendering.Skia;

/// <summary>
/// Reads the headers of a JPEG of four components that has no Adobe marker, and makes copies of it
/// in which one component's samples are a constant.
/// </summary>
/// <remarks>
/// SkiaSharp's codec reads every JPEG of four components as inverted inks and multiplies each of
/// cyan, magenta and yellow by black (<c>R = c * k / 255</c>, and so on), which is right for what
/// Photoshop writes and wrong for a file with no Adobe marker, whose samples are the inks as they
/// are. The inks cannot be read back from that product, but a copy whose black samples are a
/// constant, and another whose cyan samples are, can: a quantisation table of zeros makes a
/// component's samples 128 whatever they were, and the header is all that changes.
/// </remarks>
internal static class MarkerlessCmykJpeg
{
    /// <summary>
    /// Whether <paramref name="jpeg"/> has four components and no Adobe <c>APP14</c> marker before
    /// its first scan, and the offset of the entry in its frame header for each component.
    /// </summary>
    internal static bool IsMarkerless(ReadOnlySpan<byte> jpeg) => Read(jpeg, out _, out _);

    /// <summary>
    /// Copies <paramref name="jpeg"/> with one component's quantisation table replaced by zeros.
    /// </summary>
    /// <param name="jpeg">A JPEG for which <see cref="IsMarkerless"/> answered true.</param>
    /// <param name="component">The zero-based index of the component, in the frame header's order.</param>
    /// <returns>The copy, or null when all four tables a JPEG may define are in use by its components.</returns>
    internal static byte[] WithComponentFlattened(ReadOnlySpan<byte> jpeg, int component)
    {
        if (!Read(jpeg, out var frame, out var tables))
            return null;

        var free = -1;
        for (var id = 0; id < 4; id++)
        {
            if ((tables & (1 << id)) == 0)
            {
                free = id;
                break;
            }
        }

        if (free < 0)
            return null;

        // The segment: marker, length of 67, the table's precision and identifier, 64 zero bytes.
        const int tableSegment = 69;
        var copy = new byte[jpeg.Length + tableSegment];
        jpeg[..frame].CopyTo(copy);
        copy[frame] = 0xFF;
        copy[frame + 1] = 0xDB;
        copy[frame + 2] = 0;
        copy[frame + 3] = 67;
        copy[frame + 4] = (byte)free;
        jpeg[frame..].CopyTo(copy.AsSpan(frame + tableSegment));
        copy[frame + tableSegment + 10 + 3 * component + 2] = (byte)free;
        return copy;
    }

    // The frame header's offset, and which of the four quantisation table identifiers its
    // components use, as bits.
    private static bool Read(ReadOnlySpan<byte> jpeg, out int frame, out int tables)
    {
        frame = -1;
        tables = 0;
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return false;

        var at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
        {
            var marker = jpeg[at + 1];
            if (marker == 0xFF)
            {
                at++;
                continue;
            }

            if (marker is 0xDA or 0xD9)
                break;

            var length = (jpeg[at + 2] << 8) | jpeg[at + 3];
            if (length < 2 || at + 2 + length > jpeg.Length)
                return false;

            if (marker == 0xEE && length >= 7 && jpeg.Slice(at + 4, 5).SequenceEqual("Adobe"u8))
                return false;

            var isFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isFrame && frame < 0)
            {
                if (length < 8 || jpeg[at + 9] != 4 || length < 8 + 3 * 4)
                    return false;

                frame = at;
                for (var component = 0; component < 4; component++)
                    tables |= 1 << (jpeg[at + 10 + 3 * component + 2] & 3);
            }

            at += 2 + length;
        }

        return frame >= 0;
    }
}
