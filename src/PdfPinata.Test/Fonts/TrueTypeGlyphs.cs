using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;

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
    private readonly Dictionary<string, int> _tables = new(StringComparer.Ordinal);
    private readonly List<(string Tag, uint Checksum, int Offset, int Length)> _records = [];
    private readonly int[] _loca;
    private readonly int _glyf;

    public TrueTypeGlyphs(byte[] bytes)
    {
        _bytes = bytes;

        var numTables = U16(4);
        for (var idx = 0; idx < numTables; idx++)
        {
            var record = RecordAt(idx);
            _records.Add((Ascii(record, 4), U32(record + 4), (int)U32(record + 8), (int)U32(record + 12)));
            _tables[_records[idx].Tag] = _records[idx].Offset;
        }

        NumGlyphs = U16(_tables["maxp"] + 4);
        _glyf = _tables["glyf"];

        var longOffsets = S16(_tables["head"] + 50) != 0;
        var loca = _tables["loca"];
        _loca = new int[NumGlyphs + 1];
        for (var idx = 0; idx <= NumGlyphs; idx++)
            _loca[idx] = longOffsets ? (int)U32(loca + idx * 4) : U16(loca + idx * 2) * 2;
    }

    public int NumGlyphs { get; }

    /// <summary>
    ///   The TrueType program a document embeds, as <c>/FontFile2</c>, for the one face whose
    ///   <c>/FontName</c> <paramref name="isFace"/> accepts.
    /// </summary>
    public static byte[] EmbeddedIn(PdfDocument document, Func<string, bool> isFace)
    {
        var descriptor = document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/FontDescriptor")
            .Single(d => isFace(d.Elements.GetName("/FontName")));

        var program = descriptor.Elements["/FontFile2"];
        return ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
            .Stream.UnfilteredValue;
    }

    /// <summary>
    ///   The table directory as the file states it, in the order it lists the tables.
    /// </summary>
    public IReadOnlyList<(string Tag, uint Checksum, int Offset, int Length)> TableRecords => _records;

    /// <summary>
    ///   Every way in which the table directory fails to describe the file, in words; empty when
    ///   it describes it. The search fields have to follow from the table count, the records have
    ///   to be in tag order, every table has to have bytes, begin on a four-byte boundary, lie
    ///   inside the file, overlap no other table and carry its own checksum, and <c>loca</c> may
    ///   not reach past the end of <c>glyf</c>.
    /// </summary>
    public List<string> DirectoryProblems()
    {
        var problems = new List<string>();

        var count = _records.Count;
        var selector = 0;
        while (2 << selector <= count)
            selector++;
        if (U16(6) != (1 << selector) * 16 || U16(8) != selector || U16(10) != count * 16 - (1 << selector) * 16)
            problems.Add($"searchRange, entrySelector and rangeShift do not follow from {count} tables");

        foreach (var (tag, checksum, offset, length) in _records)
        {
            if (length == 0)
                problems.Add($"'{tag}' has no bytes");
            if (offset % 4 != 0)
                problems.Add($"'{tag}' begins at {offset}, not on a four-byte boundary");
            if (offset + length > _bytes.Length)
                problems.Add($"'{tag}' runs past the end of the file");
            // 'head' is the exception the specification makes: its checksum is taken with the
            // checksumAdjustment field zeroed, so it cannot be recomputed from the bytes as written.
            else if (tag != "head" && checksum != TableChecksum(_bytes, offset, length))
                problems.Add($"'{tag}' does not carry its own checksum");
        }

        var byOffset = _records.OrderBy(record => record.Offset).ToList();
        for (var idx = 1; idx < byOffset.Count; idx++)
        {
            var (previous, current) = (byOffset[idx - 1], byOffset[idx]);
            if (current.Offset < previous.Offset + previous.Length)
                problems.Add($"'{current.Tag}' at {current.Offset} overlaps '{previous.Tag}' at {previous.Offset}");
        }

        for (var idx = 1; idx < _records.Count; idx++)
        {
            if (string.CompareOrdinal(_records[idx - 1].Tag, _records[idx].Tag) >= 0)
                problems.Add($"'{_records[idx].Tag}' is listed after '{_records[idx - 1].Tag}', out of tag order");
        }

        var glyfLength = _records.Single(record => record.Tag == "glyf").Length;
        if (_loca[NumGlyphs] > glyfLength)
            problems.Add($"loca reaches byte {_loca[NumGlyphs]} of a glyf table {glyfLength} bytes long");

        return problems;
    }

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
            return [];

        var components = new List<int>();
        var position = _glyf + _loca[glyph] + 10;
        while (true)
        {
            var flags = U16(position);
            components.Add(U16(position + 2));
            position += 4 + ArgumentsAndTransformLength(flags);

            if ((flags & MoreComponents) == 0)
                return [..components];
        }
    }

    /// <summary>
    ///   How many bytes follow a component's flags and glyph index: its two arguments, then
    ///   whichever transform its flags say it carries.
    /// </summary>
    private static int ArgumentsAndTransformLength(int flags)
    {
        var length = (flags & Arg1And2AreWords) == 0 ? 2 : 4;
        if ((flags & WeHaveAScale) != 0)
            length += 2;
        else if ((flags & WeHaveAnXAndYScale) != 0)
            length += 4;
        if ((flags & WeHaveATwoByTwo) != 0)
            length += 8;

        return length;
    }

    /// <summary>
    ///   The glyph's advance width in thousandths of an em, as <c>/W</c> states it: the
    ///   <c>hmtx</c> advance scaled from the font's own units and truncated. A glyph past the last
    ///   long metric takes that last metric's advance, as the OpenType <c>hmtx</c> table says.
    /// </summary>
    public int AdvanceInThousandths(int glyph)
    {
        var unitsPerEm = U16(_tables["head"] + 18);
        var numberOfHMetrics = U16(_tables["hhea"] + 34);
        var metric = Math.Min(glyph, numberOfHMetrics - 1);
        var advance = U16(_tables["hmtx"] + metric * 4);
        return unitsPerEm == 1000 ? advance : advance * 1000 / unitsPerEm;
    }

    /// <summary>
    ///   The glyph a character is drawn with, through the Windows Unicode cmap subtable.
    /// </summary>
    public int GlyphIndexOf(char character)
    {
        var subtable = WindowsUnicodeSubtable();

        var segCountX2 = U16(subtable + 6);
        var endCodes = subtable + 14;
        var startCodes = endCodes + segCountX2 + 2;
        var idDeltas = startCodes + segCountX2;
        var idRangeOffsets = idDeltas + segCountX2;

        for (var seg = 0; seg < segCountX2 / 2; seg++)
        {
            if (U16(endCodes + seg * 2) < character || U16(startCodes + seg * 2) > character)
                continue;

            var delta = S16(idDeltas + seg * 2);
            var rangeOffset = U16(idRangeOffsets + seg * 2);
            if (rangeOffset == 0)
                return (character + delta) & 0xFFFF;

            var at = idRangeOffsets + seg * 2 + rangeOffset
                     + (character - U16(startCodes + seg * 2)) * 2;
            var glyph = U16(at);
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }
        return 0;
    }

    /// <summary>
    ///   Where the format 4 Windows Unicode (3, 1) cmap subtable begins; the last one listed, if
    ///   the font lists several.
    /// </summary>
    private int WindowsUnicodeSubtable()
    {
        var cmap = _tables["cmap"];
        var subtable = 0;
        var count = U16(cmap + 2);
        for (var idx = 0; idx < count; idx++)
        {
            var record = cmap + 4 + idx * 8;
            if (U16(record) == 3 && U16(record + 2) == 1)
                subtable = cmap + (int)U32(record + 4);
        }
        if (subtable == 0 || U16(subtable) != 4)
            throw new InvalidOperationException("no format 4 Windows Unicode cmap subtable");

        return subtable;
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

        var patched = (byte[])_bytes.Clone();
        // The header is 10 bytes and the first component's flags another 2, so the glyph it
        // names begins at 12.
        var at = _glyf + _loca[glyph] + 12;
        patched[at] = (byte)(component >> 8);
        patched[at + 1] = (byte)component;
        return patched;
    }

    /// <summary>
    ///   A copy that calls itself something else, so that a document drawn with it embeds it under
    ///   a <c>/FontName</c> of its own and a test can tell it from the font it was altered from.
    ///   The library no longer needs it: a font source is cached by its bytes and by the face
    ///   name the resolver serves it under, not by the name inside it (#329). Only the first letter of each
    ///   name is overwritten, so every string keeps its length and its offset. Fonts altered in
    ///   different ways need different <paramref name="letter"/>s, or they share a name again.
    /// </summary>
    public byte[] WithADistinctFontName(char letter = 'X')
    {
        var renamed = (byte[])_bytes.Clone();
        var name = _tables["name"];
        var count = U16(name + 2);
        var storage = name + U16(name + 4);

        for (var idx = 0; idx < count; idx++)
        {
            var record = name + 6 + idx * 12;
            var nameId = U16(record + 6);
            // Family, unique identifier, full name, PostScript name: the four that identify it.
            if (nameId != 1 && nameId != 3 && nameId != 4 && nameId != 6)
                continue;

            var length = U16(record + 8);
            if (length == 0)
                continue;

            var at = storage + U16(record + 10);
            // Windows names are UTF-16BE, so the letter is the second byte of the pair.
            renamed[U16(record) == 3 ? at + 1 : at] = (byte)letter;
        }
        return renamed;
    }

    /// <summary>
    ///   A copy in which <paramref name="character"/>'s glyph advances <paramref name="advance"/>
    ///   font units, and which still calls itself by every name the original does. Only the one
    ///   <c>hmtx</c> entry changes, so the file keeps its length and every offset in it.
    /// </summary>
    public byte[] WithAdvanceOf(char character, ushort advance)
    {
        var glyph = GlyphIndexOf(character);
        if (glyph >= U16(_tables["hhea"] + 34))
            throw new InvalidOperationException("glyph " + glyph + " has no long metric of its own");

        var patched = (byte[])_bytes.Clone();
        var at = _tables["hmtx"] + glyph * 4;
        patched[at] = (byte)(advance >> 8);
        patched[at + 1] = (byte)advance;
        return patched;
    }

    /// <summary>
    ///   A copy whose OS/2 table declares the embedding permissions <paramref name="fsType"/>,
    ///   with the table's checksum in the directory recomputed so that the file still describes
    ///   itself. The field is two bytes at offset 8 of the table.
    /// </summary>
    public byte[] WithFsType(ushort fsType)
    {
        var patched = (byte[])_bytes.Clone();
        var os2 = _tables["OS/2"];
        patched[os2 + 8] = (byte)(fsType >> 8);
        patched[os2 + 9] = (byte)fsType;

        var record = RecordOf("OS/2");
        var sum = TableChecksum(patched, os2, (int)U32(record + 12));
        patched[record + 4] = (byte)(sum >> 24);
        patched[record + 5] = (byte)(sum >> 16);
        patched[record + 6] = (byte)(sum >> 8);
        patched[record + 7] = (byte)sum;
        return patched;
    }

    /// <summary>
    ///   A copy whose directory says the table <paramref name="tag"/> has no bytes, as some fonts
    ///   with their hinting stripped do. Its old data stays where it was, reached by nothing, and
    ///   its checksum is that of no bytes, which is zero.
    /// </summary>
    public byte[] WithTableEmptied(string tag)
    {
        var patched = (byte[])_bytes.Clone();
        var record = RecordOf(tag);

        // The checksum is bytes 4 to 7 of the record and the length bytes 12 to 15.
        Array.Clear(patched, record + 4, 4);
        Array.Clear(patched, record + 12, 4);
        return patched;
    }

    /// <summary>Where the <paramref name="index"/>th record of the table directory begins.</summary>
    private static int RecordAt(int index) => 12 + index * 16;

    /// <summary>Where the directory record of the table <paramref name="tag"/> begins.</summary>
    private int RecordOf(string tag)
    {
        var index = _records.FindIndex(record => record.Tag == tag);
        if (index < 0)
            throw new InvalidOperationException("the font has no '" + tag + "' table");

        return RecordAt(index);
    }

    /// <summary>
    ///   The OpenType checksum of a table: its big-endian 32-bit words summed, the last one padded
    ///   with zeros.
    /// </summary>
    private static uint TableChecksum(byte[] bytes, int offset, int length)
    {
        uint sum = 0;
        for (var at = 0; at < length; at += 4)
        {
            uint word = 0;
            for (var b = 0; b < 4; b++)
                word = (word << 8) | (at + b < length ? bytes[offset + at + b] : (byte)0);
            sum = unchecked(sum + word);
        }

        return sum;
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
        var value = U16(offset);
        return value >= 0x8000 ? value - 0x10000 : value;
    }

    private uint U32(int offset)
    {
        return ((uint)_bytes[offset] << 24) | ((uint)_bytes[offset + 1] << 16)
                                            | ((uint)_bytes[offset + 2] << 8) | _bytes[offset + 3];
    }
}
