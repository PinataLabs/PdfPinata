using System;
using System.Collections.Generic;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   As much of a TrueType file as a test needs to say which glyphs are in it and what each one
///   is built from. The library's own reader is internal, and asking it would let the same
///   mistake answer the question and mark the answer.
/// </summary>
internal sealed class TrueTypeGlyphs
{
    private const int MoreComponents = 0x0020;
    private const int Arg1And2AreWords = 0x0001;
    private const int WeHaveAScale = 0x0008;
    private const int WeHaveAnXAndYScale = 0x0040;
    private const int WeHaveATwoByTwo = 0x0080;

    private readonly byte[] _bytes;
    private readonly Dictionary<string, int> _tables = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly int[] _loca;
    private readonly int _glyf;

    public TrueTypeGlyphs(byte[] bytes)
    {
        _bytes = bytes;

        int numTables = U16(4);
        for (int idx = 0; idx < numTables; idx++)
        {
            int record = 12 + idx * 16;
            _tables[Ascii(record, 4)] = (int)U32(record + 8);
        }

        NumGlyphs = U16(_tables["maxp"] + 4);
        _glyf = _tables["glyf"];

        bool longOffsets = S16(_tables["head"] + 50) != 0;
        int loca = _tables["loca"];
        _loca = new int[NumGlyphs + 1];
        for (int idx = 0; idx <= NumGlyphs; idx++)
            _loca[idx] = longOffsets ? (int)U32(loca + idx * 4) : U16(loca + idx * 2) * 2;
    }

    public int NumGlyphs { get; }

    /// <summary>
    ///   The bytes the glyph occupies. A glyph with no outline of its own occupies none, which
    ///   is also how a subset says it left the glyph out.
    /// </summary>
    public int LengthOf(int glyph)
    {
        return _loca[glyph + 1] - _loca[glyph];
    }

    public bool IsComposite(int glyph)
    {
        return LengthOf(glyph) > 0 && S16(_glyf + _loca[glyph]) < 0;
    }

    /// <summary>
    ///   The glyphs a composite glyph is drawn from, in the order it names them.
    /// </summary>
    public int[] ComponentsOf(int glyph)
    {
        if (!IsComposite(glyph))
            return Array.Empty<int>();

        var components = new List<int>();
        int position = _glyf + _loca[glyph] + 10;
        while (true)
        {
            int flags = U16(position);
            components.Add(U16(position + 2));
            position += 4;

            position += (flags & Arg1And2AreWords) == 0 ? 2 : 4;
            if ((flags & WeHaveAScale) != 0)
                position += 2;
            else if ((flags & WeHaveAnXAndYScale) != 0)
                position += 4;
            if ((flags & WeHaveATwoByTwo) != 0)
                position += 8;

            if ((flags & MoreComponents) == 0)
                return components.ToArray();
        }
    }

    /// <summary>
    ///   The glyph a character is drawn with, through the Windows Unicode cmap subtable.
    /// </summary>
    public int GlyphIndexOf(char character)
    {
        int cmap = _tables["cmap"];
        int subtable = 0;
        int count = U16(cmap + 2);
        for (int idx = 0; idx < count; idx++)
        {
            int record = cmap + 4 + idx * 8;
            if (U16(record) == 3 && U16(record + 2) == 1)
                subtable = cmap + (int)U32(record + 4);
        }
        if (subtable == 0 || U16(subtable) != 4)
            throw new InvalidOperationException("no format 4 Windows Unicode cmap subtable");

        int segCountX2 = U16(subtable + 6);
        int endCodes = subtable + 14;
        int startCodes = endCodes + segCountX2 + 2;
        int idDeltas = startCodes + segCountX2;
        int idRangeOffsets = idDeltas + segCountX2;

        for (int seg = 0; seg < segCountX2 / 2; seg++)
        {
            if (U16(endCodes + seg * 2) < character || U16(startCodes + seg * 2) > character)
                continue;

            int delta = S16(idDeltas + seg * 2);
            int rangeOffset = U16(idRangeOffsets + seg * 2);
            if (rangeOffset == 0)
                return (character + delta) & 0xFFFF;

            int at = idRangeOffsets + seg * 2 + rangeOffset
                     + (character - U16(startCodes + seg * 2)) * 2;
            int glyph = U16(at);
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }
        return 0;
    }

    /// <summary>
    ///   A copy of the font in which the given composite glyph is drawn from
    ///   <paramref name="component"/> where it used to name its first component. The glyph data
    ///   keeps its length, so every other table still describes the file.
    /// </summary>
    public byte[] WithFirstComponentRepointed(int glyph, int component)
    {
        if (!IsComposite(glyph))
            throw new InvalidOperationException("glyph " + glyph + " is not composite");

        byte[] patched = (byte[])_bytes.Clone();
        // The header is 10 bytes and the first component's flags another 2, so the glyph it
        // names begins at 12.
        int at = _glyf + _loca[glyph] + 12;
        patched[at] = (byte)(component >> 8);
        patched[at + 1] = (byte)component;
        return patched;
    }

    /// <summary>
    ///   A copy that calls itself something else. The library caches a font source under the
    ///   name in the font's own name table, and refuses a second one by that name, so a font
    ///   built by altering another has to stop claiming to be it. Only the first letter of each
    ///   name is overwritten, so every string keeps its length and its offset.
    /// </summary>
    public byte[] WithADistinctFontName()
    {
        byte[] renamed = (byte[])_bytes.Clone();
        int name = _tables["name"];
        int count = U16(name + 2);
        int storage = name + U16(name + 4);

        for (int idx = 0; idx < count; idx++)
        {
            int record = name + 6 + idx * 12;
            int nameId = U16(record + 6);
            // Family, unique identifier, full name, PostScript name: the four that identify it.
            if (nameId != 1 && nameId != 3 && nameId != 4 && nameId != 6)
                continue;

            int length = U16(record + 8);
            if (length == 0)
                continue;

            int at = storage + U16(record + 10);
            // Windows names are UTF-16BE, so the letter is the second byte of the pair.
            renamed[U16(record) == 3 ? at + 1 : at] = (byte)'X';
        }
        return renamed;
    }

    private string Ascii(int offset, int length)
    {
        return System.Text.Encoding.ASCII.GetString(_bytes, offset, length);
    }

    private int U16(int offset)
    {
        return (_bytes[offset] << 8) | _bytes[offset + 1];
    }

    private int S16(int offset)
    {
        int value = U16(offset);
        return value >= 0x8000 ? value - 0x10000 : value;
    }

    private uint U32(int offset)
    {
        return ((uint)_bytes[offset] << 24) | ((uint)_bytes[offset + 1] << 16)
                                            | ((uint)_bytes[offset + 2] << 8) | _bytes[offset + 3];
    }
}
