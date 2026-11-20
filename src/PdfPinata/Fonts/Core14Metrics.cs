using System;

namespace PdfPinata.Fonts;

/// <summary>
/// Adobe's metrics for the standard 14 fonts: the advance of each glyph by name, the encoding each
/// font carries built in, and which of the 14 a <c>/BaseFont</c> name means.
/// </summary>
/// <remarks>
/// <para>
/// Page rendering reads this for a standard 14 font whose dictionary has no <c>/Widths</c>, which
/// ISO 32000-1 9.6.2.2 allows. The tables are generated from the Core14 AFM files by
/// <c>tools/Core14MetricsGenerator</c>; this class is the only reader of them.
/// </para>
/// <para>
/// A glyph is named, never numbered: the width of a code is the width of the glyph its encoding
/// names, so a caller walks code to glyph name through <c>/Differences</c> or
/// <see cref="GlyphNameOf"/>, then glyph name to width through <see cref="TryGetWidth"/>.
/// </para>
/// </remarks>
internal static class Core14Metrics
{
    /// <summary>
    /// Says which of the 14 fonts a <c>/BaseFont</c> name means, accepting the names other programs
    /// write for them. A subset prefix (<c>ABCDEF+</c>) and spaces are ignored, case is ignored, and
    /// <c>Arial</c> is Helvetica, <c>TimesNewRoman</c> is Times and <c>CourierNew</c> is Courier,
    /// each with <c>,Bold</c>, <c>,Italic</c>, <c>,BoldItalic</c>, <c>-Bold</c>, <c>MT</c> and
    /// <c>PS</c> forms. A name that carries something this does not recognise, such as
    /// <c>Arial-Black</c> or <c>ArialNarrow</c>, is not one of the 14: its widths are not theirs.
    /// </summary>
    public static bool TryResolve(string baseFont, out Core14Font font)
    {
        font = default;
        if (string.IsNullOrEmpty(baseFont))
            return false;

        var name = baseFont.Replace(" ", "");
        if (name.Length > 7 && name[6] == '+' && IsSubsetTag(name))
            name = name.Substring(7);

        for (var i = 0; i < Core14Tables.PostScriptNames.Length; i++)
        {
            if (string.Equals(Core14Tables.PostScriptNames[i], name, StringComparison.OrdinalIgnoreCase))
            {
                font = (Core14Font)i;
                return true;
            }
        }

        var lower = name.ToLowerInvariant();
        string family;
        foreach (var candidate in Families)
        {
            if (lower.StartsWith(candidate.Prefix, StringComparison.Ordinal))
            {
                family = candidate.Family;
                if (!TryReadStyle(lower.Substring(candidate.Prefix.Length), out var bold, out var italic))
                    return false;
                return TryPick(family, bold, italic, out font);
            }
        }

        return false;
    }

    /// <summary>
    /// The advance of a glyph in thousandths of an em, or false when the font has no glyph of that
    /// name. A name that is in some other of the 14 and not this one is false too: Symbol has no
    /// <c>A</c>.
    /// </summary>
    public static bool TryGetWidth(Core14Font font, ReadOnlySpan<char> glyphName, out int width)
    {
        width = 0;
        var pool = Core14Tables.PoolOfFont[(int)font];
        var index = IndexOf(pool, glyphName);
        if (index < 0)
            return false;

        var w = Core14Tables.Widths[(int)font][index];
        if (w == NoGlyph)
            return false;

        width = w;
        return true;
    }

    /// <summary>
    /// The name of the glyph a code draws through the font's built-in encoding: StandardEncoding for
    /// the twelve text fonts, and each font's own for Symbol and ZapfDingbats. Null where the code
    /// names nothing.
    /// </summary>
    public static string GlyphNameOf(Core14Font font, int code)
    {
        if ((uint)code > 255)
            return null;

        var index = Core14Tables.Encodings[(int)font][code];
        if (index == NoGlyph)
            return null;

        var (names, starts) = Pool(Core14Tables.PoolOfFont[(int)font]);
        return names.Substring(starts[index], starts[index + 1] - starts[index]);
    }

    /// <summary>The PostScript name of the font, as ISO 32000-1 Table 110 lists it.</summary>
    public static string NameOf(Core14Font font) => Core14Tables.PostScriptNames[(int)font];

    /// <summary>Whether the font is Symbol or ZapfDingbats, whose encodings are their own and not StandardEncoding.</summary>
    public static bool IsSymbolic(Core14Font font) => Core14Tables.PoolOfFont[(int)font] != 0;

    const ushort NoGlyph = ushort.MaxValue;

    static (string Names, ushort[] Starts) Pool(int pool) => pool switch
    {
        0 => (Core14Tables.LatinNames, Core14Tables.LatinNameStarts),
        1 => (Core14Tables.SymbolNames, Core14Tables.SymbolNameStarts),
        _ => (Core14Tables.ZapfDingbatsNames, Core14Tables.ZapfDingbatsNameStarts),
    };

    static int IndexOf(int pool, ReadOnlySpan<char> glyphName)
    {
        var (names, starts) = Pool(pool);
        var low = 0;
        var high = starts.Length - 2;
        while (low <= high)
        {
            var mid = (low + high) >>> 1;
            var candidate = names.AsSpan(starts[mid], starts[mid + 1] - starts[mid]);
            var order = candidate.CompareTo(glyphName, StringComparison.Ordinal);
            if (order == 0)
                return mid;
            if (order < 0)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return -1;
    }

    // Longest prefix first, so that couriernew is not read as courier followed by "new".
    static readonly (string Prefix, string Family)[] Families =
    {
        ("timesnewroman", "Times"),
        ("couriernew", "Courier"),
        ("helvetica", "Helvetica"),
        ("courier", "Courier"),
        ("arial", "Helvetica"),
        ("times", "Times"),
    };

    static bool IsSubsetTag(string name)
    {
        for (var i = 0; i < 6; i++)
        {
            if (name[i] < 'A' || name[i] > 'Z')
                return false;
        }

        return true;
    }

    // What follows the family: separators, the MT and PS foundry marks, and the style words.
    // Anything else means the name is some other font that merely starts like one of the 14.
    static bool TryReadStyle(string rest, out bool bold, out bool italic)
    {
        bold = italic = false;
        var i = 0;
        while (i < rest.Length)
        {
            var tail = rest.AsSpan(i);
            if (tail[0] is ',' or '-' or '_')
                i++;
            else if (tail.StartsWith("bold", StringComparison.Ordinal))
            {
                bold = true;
                i += 4;
            }
            else if (tail.StartsWith("italic", StringComparison.Ordinal))
            {
                italic = true;
                i += 6;
            }
            else if (tail.StartsWith("oblique", StringComparison.Ordinal))
            {
                italic = true;
                i += 7;
            }
            else if (tail.StartsWith("regular", StringComparison.Ordinal))
                i += 7;
            else if (tail.StartsWith("roman", StringComparison.Ordinal))
                i += 5;
            else if (tail.StartsWith("ps", StringComparison.Ordinal) || tail.StartsWith("mt", StringComparison.Ordinal))
                i += 2;
            else
                return false;
        }

        return true;
    }

    static bool TryPick(string family, bool bold, bool italic, out Core14Font font)
    {
        font = family switch
        {
            "Helvetica" => bold ? (italic ? Core14Font.HelveticaBoldOblique : Core14Font.HelveticaBold) : (italic ? Core14Font.HelveticaOblique : Core14Font.Helvetica),
            "Courier" => bold ? (italic ? Core14Font.CourierBoldOblique : Core14Font.CourierBold) : (italic ? Core14Font.CourierOblique : Core14Font.Courier),
            _ => bold ? (italic ? Core14Font.TimesBoldItalic : Core14Font.TimesBold) : (italic ? Core14Font.TimesItalic : Core14Font.TimesRoman),
        };
        return true;
    }
}
