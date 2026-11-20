using System;
using System.IO;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// One subtable of a TrueType <c>cmap</c>: from a character code, or a character, to a glyph.
/// Formats 0, 4, 6 and 12 are read, which are what a font embedded in a PDF has.
/// </summary>
/// <remarks>
/// Each lookup reads the subtable where it lies, checking every read against the end of the
/// <c>cmap</c> table, so a malformed subtable throws on the lookup that meets the fault.
/// </remarks>
internal sealed class TrueTypeCharacterMap
{
    private readonly byte[] _data;
    private readonly int _offset;
    private readonly int _end;
    private readonly int _format;

    private TrueTypeCharacterMap(byte[] data, int offset, int end, int format)
    {
        _data = data;
        _offset = offset;
        _end = end;
        _format = format;
    }

    /// <summary>
    /// The subtable at an offset, or null where its format is not one of those read. A lookup
    /// reads no further than the length the subtable states, and never past <paramref name="end"/>,
    /// the end of the <c>cmap</c> table, so a broken subtable cannot read its neighbour's bytes.
    /// </summary>
    internal static TrueTypeCharacterMap At(byte[] data, int offset, int end)
    {
        var reader = new BigEndianReader(data, offset, end);
        if (!reader.TryReadUInt16(out var format) || format is not (0 or 4 or 6 or 12))
            return null;

        // Formats 0, 4 and 6 state their length in 16 bits after the format, and 12 in 32 bits
        // after a reserved field.
        uint length;
        if (format == 12)
        {
            if (!reader.TryReadUInt16(out _) || !reader.TryReadUInt32(out length))
                return null;
        }
        else
        {
            if (!reader.TryReadUInt16(out var shortLength))
                return null;
            length = shortLength;
        }

        var subtableEnd = (int)Math.Min((long)end, (long)offset + length);
        return new TrueTypeCharacterMap(data, offset, subtableEnd, format);
    }

    /// <summary>The glyph a code maps to, or 0, the missing glyph, where it maps to none.</summary>
    /// <exception cref="InvalidDataException">The subtable is malformed.</exception>
    public int GlyphOf(int code)
    {
        if (code < 0)
            return 0;

        var reader = new BigEndianReader(_data, _offset, _end);
        return _format switch
        {
            0 => ByteEncoding(ref reader, code),
            4 => SegmentMapping(ref reader, code),
            6 => TrimmedTable(ref reader, code),
            _ => SegmentedCoverage(ref reader, code)
        };
    }

    private static int ByteEncoding(ref BigEndianReader reader, int code)
    {
        if (code > 255)
            return 0;
        reader.Skip(6 + code);
        return reader.ReadByte();
    }

    private int SegmentMapping(ref BigEndianReader reader, int code)
    {
        if (code > 0xFFFF)
            return 0;

        reader.Skip(6);
        var segments = reader.ReadUInt16() / 2;
        reader.Skip(6);
        var endCodes = _offset + 14;
        var startCodes = endCodes + 2 * segments + 2;
        var deltas = startCodes + 2 * segments;
        var rangeOffsets = deltas + 2 * segments;

        for (var segment = 0; segment < segments; segment++)
        {
            if (UInt16At(endCodes + 2 * segment) < code)
                continue;

            var start = UInt16At(startCodes + 2 * segment);
            if (start > code)
                return 0;

            var delta = UInt16At(deltas + 2 * segment);
            var rangeOffsetAt = rangeOffsets + 2 * segment;
            var rangeOffset = UInt16At(rangeOffsetAt);
            if (rangeOffset == 0)
                return (code + delta) & 0xFFFF;

            var glyph = UInt16At(rangeOffsetAt + rangeOffset + 2 * (code - start));
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }

        return 0;
    }

    private static int TrimmedTable(ref BigEndianReader reader, int code)
    {
        reader.Skip(6);
        var first = reader.ReadUInt16();
        var count = reader.ReadUInt16();
        if (code < first || code >= first + count)
            return 0;
        reader.Skip(2 * (code - first));
        return reader.ReadUInt16();
    }

    private static int SegmentedCoverage(ref BigEndianReader reader, int code)
    {
        reader.Skip(12);
        var groups = reader.ReadUInt32();
        for (var group = 0u; group < groups; group++)
        {
            var start = reader.ReadUInt32();
            var end = reader.ReadUInt32();
            var glyph = reader.ReadUInt32();
            if (code >= start && code <= end)
                return (int)(glyph + ((uint)code - start));
        }

        return 0;
    }

    private int UInt16At(int position)
    {
        var reader = new BigEndianReader(_data, position, _end);
        return reader.ReadUInt16();
    }
}
