using System;
using System.IO;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// One subtable of a TrueType <c>cmap</c>: from a character code, or a character, to a glyph.
/// Formats 0, 4, 6 and 12 are read, which are what a font embedded in a PDF has.
/// </summary>
/// <remarks>
/// <para>
/// Each lookup reads the subtable where it lies, checking every read against the end of the
/// <c>cmap</c> table, so a malformed subtable throws on the lookup that meets the fault.
/// </para>
/// <para>
/// <b>Nothing is built from the subtable, and no lookup takes longer than a search by halves.</b>
/// Formats 4 and 12 are searched that way, which needs their segments in order, as both formats
/// require. Whether they are is found once, in one pass. A format 12 subtable out of order is
/// refused, as FreeType refuses it. A format 4 subtable out of order is searched in turn, as
/// FreeType searches it, but only where it has no more than <see cref="MaximumUnorderedSegments"/>
/// segments: searching a large one in turn for each code took minutes for a file of 95 KB.
/// </para>
/// </remarks>
internal sealed class TrueTypeCharacterMap
{
    private readonly byte[] _data;
    private readonly int _offset;
    private readonly int _end;
    private readonly int _format;

    /// <summary>
    /// The most segments a format 4 subtable may have and still be searched in turn, where they
    /// are out of order. A subset that a broken producer wrote out of order has tens of them.
    /// </summary>
    internal const int MaximumUnorderedSegments = 256;

    /// <summary>
    /// Whether the segments of format 4 or the groups of format 12 are in order: 0 until it is
    /// known. One number, so that two threads finding it out at once both read a whole answer.
    /// </summary>
    private int _order;

    private const int InOrder = 1;
    private const int OutOfOrder = 2;

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
        var stated = reader.ReadUInt16() / 2;
        var endCodes = _offset + 14;
        var startCodes = endCodes + 2 * stated + 2;
        var deltas = startCodes + 2 * stated;
        var rangeOffsets = deltas + 2 * stated;

        // Only the end codes the subtable has room for are searched, whatever count it states.
        // A code past them all then reads a start code that is not there, and throws.
        var segments = Math.Min(stated, Math.Max(0, (_end - endCodes) / 2));
        if (_order == 0)
            _order = EndCodesInOrder(endCodes, segments) ? InOrder : OutOfOrder;

        // The first segment that ends at or after the code is the only one that can hold it.
        var segment = 0;
        if (_order == InOrder)
        {
            int low = 0, high = segments;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (UInt16At(endCodes + 2 * middle) < code)
                    low = middle + 1;
                else
                    high = middle;
            }

            segment = low;
        }
        else
        {
            if (segments > MaximumUnorderedSegments)
                throw new InvalidDataException("The font's format 4 'cmap' subtable has too many segments to search out of order.");
            while (segment < segments && UInt16At(endCodes + 2 * segment) < code)
                segment++;
        }

        if (segment >= stated)
            return 0;

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

    /// <summary>Whether no segment of format 4 ends before the one in front of it does.</summary>
    private bool EndCodesInOrder(int endCodes, int segments)
    {
        var last = -1;
        for (var segment = 0; segment < segments; segment++)
        {
            var end = UInt16At(endCodes + 2 * segment);
            if (end < last)
                return false;
            last = end;
        }

        return true;
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

    /// <summary>
    /// Format 12's groups, found by a search by halves: a font may have tens of thousands of
    /// them. They have to be sorted by their first character and not overlap, as the format
    /// requires, and a subtable where they are not is refused. Only as many groups are read as
    /// the subtable's length has room for, whatever count it states.
    /// </summary>
    private int SegmentedCoverage(ref BigEndianReader reader, int code)
    {
        reader.Skip(12);
        var stated = reader.ReadUInt32();
        var first = _offset + 16L;
        var groups = Math.Min(stated, Math.Max(0, (_end - first) / GroupLength));
        if (_order == 0)
            _order = GroupsInOrder(first, groups) ? InOrder : OutOfOrder;
        if (_order == OutOfOrder)
            throw new InvalidDataException("The groups of the font's format 12 'cmap' subtable are out of order.");

        long low = 0, high = groups - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var (start, end, glyph) = GroupAt(first, middle);
            if (code < start)
                high = middle - 1;
            else if (code > end)
                low = middle + 1;
            else
                return (int)(glyph + ((uint)code - start));
        }

        return 0;
    }

    /// <summary>Whether each group starts after the one before it ends.</summary>
    private bool GroupsInOrder(long first, long groups)
    {
        long lastEnd = -1;
        for (long index = 0; index < groups; index++)
        {
            var (start, end, _) = GroupAt(first, index);
            if (start <= lastEnd || end < start)
                return false;
            lastEnd = end;
        }

        return true;
    }

    private (uint Start, uint End, uint Glyph) GroupAt(long first, long index)
    {
        var group = new BigEndianReader(_data, (int)(first + GroupLength * index), _end);
        return (group.ReadUInt32(), group.ReadUInt32(), group.ReadUInt32());
    }

    private const int GroupLength = 12;

    private int UInt16At(int position)
    {
        var reader = new BigEndianReader(_data, position, _end);
        return reader.ReadUInt16();
    }
}
