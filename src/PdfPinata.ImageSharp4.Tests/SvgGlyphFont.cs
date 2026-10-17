using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPinata.ImageSharp4.Tests;

/// <summary>
///   Gives an existing font an OpenType <c>SVG </c> table, so that one of its glyphs is drawn from
///   an SVG document instead of from its outline.
/// </summary>
/// <remarks>
///   SixLabors.Fonts 3 draws a <c>&lt;circle&gt;</c> or an <c>&lt;ellipse&gt;</c> in such a document
///   with <c>IGlyphRenderer.ArcTo</c>, and nothing else in an ordinary font does: TrueType and CFF
///   outlines are lines and Béziers. So this is how a test reaches an arc. The font is built in
///   memory from one the tests already ship, and never written anywhere.
/// </remarks>
internal static class SvgGlyphFont
{
    private const int OffsetTableLength = 12;
    private const int TableRecordLength = 16;

    /// <summary>
    ///   <paramref name="font"/> with an <c>SVG </c> table holding <paramref name="document"/> for
    ///   <paramref name="glyphId"/>. The document's element for the glyph must carry the id
    ///   <c>glyph{glyphId}</c>, as the OpenType specification has it.
    /// </summary>
    public static byte[] With(byte[] font, ushort glyphId, string document)
    {
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var count = U16(font, 4);
        for (var t = 0; t < count; t++)
        {
            var record = OffsetTableLength + t * TableRecordLength;
            var tag = Encoding.ASCII.GetString(font, record, 4);
            var offset = (int)U32(font, record + 8);
            var length = (int)U32(font, record + 12);
            tables[tag] = font.AsSpan(offset, length).ToArray();
        }

        tables["SVG "] = SvgTable(glyphId, Encoding.UTF8.GetBytes(document));

        var position = OffsetTableLength + tables.Count * TableRecordLength;
        var size = position;
        foreach (var table in tables.Values)
            size += Align4(table.Length);

        var result = new byte[size];
        Buffer.BlockCopy(font, 0, result, 0, 4); // the sfnt version
        W16(result, 4, tables.Count);
        // searchRange, entrySelector and rangeShift: a binary search aid nothing here relies on.
        var power = 1;
        var log = 0;
        while (power * 2 <= tables.Count)
        {
            power *= 2;
            log++;
        }

        W16(result, 6, power * 16);
        W16(result, 8, log);
        W16(result, 10, tables.Count * 16 - power * 16);

        var index = 0;
        foreach (var (tag, data) in tables)
        {
            var record = OffsetTableLength + index++ * TableRecordLength;
            Encoding.ASCII.GetBytes(tag, 0, 4, result, record);
            W32(result, record + 4, Checksum(data));
            W32(result, record + 8, (uint)position);
            W32(result, record + 12, (uint)data.Length);
            Buffer.BlockCopy(data, 0, result, position, data.Length);
            position += Align4(data.Length);
        }

        return result;
    }

    /// <summary>One document, for one glyph.</summary>
    private static byte[] SvgTable(ushort glyphId, byte[] document)
    {
        const int headerLength = 10;
        const int listLength = 2 + 12;

        var table = new byte[headerLength + listLength + document.Length];
        // version 0, then where the document list starts; the four reserved bytes stay zero.
        W32(table, 2, headerLength);
        W16(table, headerLength, 1);
        W16(table, headerLength + 2, glyphId);
        W16(table, headerLength + 4, glyphId);
        // The document's offset is from the start of the list, not of the table.
        W32(table, headerLength + 6, listLength);
        W32(table, headerLength + 10, (uint)document.Length);
        Buffer.BlockCopy(document, 0, table, headerLength + listLength, document.Length);
        return table;
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var b = 0; b < 4; b++)
                word = (word << 8) | (i + b < data.Length ? data[i + b] : 0u);
            sum += word;
        }

        return sum;
    }

    private static int Align4(int length) => (length + 3) & ~3;

    private static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

    private static uint U32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];

    private static void W16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }

    private static void W32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
