using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPinata.Fonts;

/// <summary>
/// What a glyph name stands for, and what the codes of the simple encodings a PDF names stand for.
/// </summary>
/// <remarks>
/// A simple font's <c>/Encoding</c> and <c>/Differences</c> say which glyph a code draws by name,
/// and a TrueType font program finds a glyph by character. This is the bridge: the Adobe Glyph List
/// and the <c>uniXXXX</c> and <c>uXXXX</c> forms its specification adds. The tables are generated
/// by <c>tools/GlyphNameTableGenerator</c>; this class is the only reader of them.
/// </remarks>
internal static class GlyphNames
{
    /// <summary>
    /// The character a glyph name stands for, or -1. A suffix after a period is ignored, as the
    /// glyph list's specification says, so <c>A.swash</c> is <c>A</c>. A name joining several
    /// with underscores stands for several characters and so for no one character: -1.
    /// </summary>
    public static int CharacterOf(string glyphName)
    {
        if (string.IsNullOrEmpty(glyphName))
            return -1;

        var name = glyphName.AsSpan();
        var period = name.IndexOf('.');
        if (period == 0)
            return -1;
        if (period > 0)
            name = name.Slice(0, period);
        if (name.IndexOf('_') >= 0)
            return -1;

        var index = IndexOf(name);
        if (index >= 0)
            return GlyphNameTables.GlyphListCharacters[index];

        if (name.Length == 7 && name.StartsWith("uni".AsSpan(), StringComparison.Ordinal))
            return ScalarOf(name.Slice(3));
        if (name.Length is >= 5 and <= 7 && name[0] == 'u')
            return ScalarOf(name.Slice(1));
        return -1;
    }

    /// <summary>
    /// Every name a character goes by: each glyph list name that stands for it, then its
    /// <c>uniXXXX</c> or <c>uXXXXX</c> form. A font program's <c>post</c> table may use any of them.
    /// </summary>
    public static IEnumerable<string> NamesOf(int character)
    {
        if (character <= 0)
            yield break;

        var characters = GlyphNameTables.GlyphListCharacters;
        var starts = GlyphNameTables.GlyphListNameStarts;
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] == character)
                yield return GlyphNameTables.GlyphListNames.Substring(starts[index], starts[index + 1] - starts[index]);
        }

        yield return character <= 0xFFFF
            ? "uni" + character.ToString("X4", CultureInfo.InvariantCulture)
            : "u" + character.ToString("X", CultureInfo.InvariantCulture);
    }

    /// <summary>The name of one of Apple's 258 standard glyphs, which a <c>post</c> table numbers, or null.</summary>
    public static string MacStandardName(int index) =>
        (uint)index < (uint)GlyphNameTables.MacStandardNames.Length ? GlyphNameTables.MacStandardNames[index] : null;

    /// <summary>
    /// The character a code of a simple encoding stands for, or -1 where it stands for none or the
    /// encoding is not one of these.
    /// </summary>
    /// <param name="encoding">
    /// <c>/WinAnsiEncoding</c>, <c>/MacRomanEncoding</c> or <c>/StandardEncoding</c>, with its slash.
    /// </param>
    /// <param name="code">The code, from 0 to 255.</param>
    public static int CharacterOf(string encoding, int code)
    {
        if ((uint)code > 255)
            return -1;

        int character = encoding switch
        {
            "/WinAnsiEncoding" => GlyphNameTables.WinAnsiCharacters[code],
            "/MacRomanEncoding" => GlyphNameTables.MacRomanCharacters[code],
            "/StandardEncoding" => CharacterOf(Core14Metrics.GlyphNameOf(Core14Font.TimesRoman, code)),
            _ => -1
        };
        return character > 0 ? character : -1;
    }

    /// <summary>The MacRomanEncoding code of a character, or -1 where the encoding has none for it.</summary>
    public static int MacRomanCodeOf(int character)
    {
        if (character <= 0)
            return -1;

        var codes = GlyphNameTables.MacRomanCharacters;
        for (var code = 0; code < codes.Length; code++)
        {
            if (codes[code] == character)
                return code;
        }

        return -1;
    }

    /// <summary>Four to six hex digits that are a Unicode scalar value, or -1.</summary>
    private static int ScalarOf(ReadOnlySpan<char> digits)
    {
        foreach (var digit in digits)
        {
            // Upper case only, as the specification says, and no sign or space int.Parse would take.
            if (digit is not (>= '0' and <= '9' or >= 'A' and <= 'F'))
                return -1;
        }

        var value = int.Parse(digits.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return value is > 0x10FFFF or (>= 0xD800 and <= 0xDFFF) ? -1 : value;
    }

    private static int IndexOf(ReadOnlySpan<char> name)
    {
        var names = GlyphNameTables.GlyphListNames;
        var starts = GlyphNameTables.GlyphListNameStarts;
        var low = 0;
        var high = starts.Length - 2;
        while (low <= high)
        {
            var mid = (low + high) >>> 1;
            var order = names.AsSpan(starts[mid], starts[mid + 1] - starts[mid]).CompareTo(name, StringComparison.Ordinal);
            if (order == 0)
                return mid;
            if (order < 0)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return -1;
    }
}
