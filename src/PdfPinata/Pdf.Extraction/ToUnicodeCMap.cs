using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Extraction;

/// <summary>
/// Reads a font's <c>/ToUnicode</c> CMap: the table that says which characters the glyph codes in a
/// content stream stand for.
/// </summary>
/// <remarks>
/// <para>
/// Without it, extracted text is glyph numbers. A font embedded as Identity-H writes the index of
/// the glyph in the font file, which has nothing to do with the character — the same word in two
/// documents using two subsets of the same face is two different runs of numbers.
/// </para>
/// <para>
/// This is the other half of <see cref="Advanced.PdfToUnicodeMap"/>, which writes these. That one
/// emits <c>bfrange</c> entries, so anything this library produced round-trips; <c>bfchar</c> is
/// read too, because other producers use it.
/// </para>
/// </remarks>
internal sealed class ToUnicodeCMap
{
    private readonly Dictionary<int, string> _map = new();

    /// <summary>
    /// How many bytes make one code. Two for Identity-H and anything else with a two-byte
    /// codespace, one for a simple font.
    /// </summary>
    public int CodeLength { get; private set; } = 1;

    /// <summary>
    /// Parses a CMap, or answers null when there is nothing usable in it.
    /// </summary>
    public static ToUnicodeCMap Parse(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        var text = PdfEncoders.RawEncoding.GetString(bytes, 0, bytes.Length);
        var cmap = new ToUnicodeCMap();

        cmap.ReadCodespace(text);
        cmap.ReadCharMappings(text);
        cmap.ReadRangeMappings(text);

        return cmap._map.Count > 0 ? cmap : null;
    }

    /// <summary>
    /// What a code stands for, or null when the map does not say.
    /// </summary>
    public string this[int code] => _map.TryGetValue(code, out var value) ? value : null;

    /// <summary>
    /// The codespace range says how wide a code is. A two-byte codespace is written
    /// <c>&lt;0000&gt;&lt;FFFF&gt;</c>, so the count of hexadecimal digits gives the width.
    /// </summary>
    private void ReadCodespace(string text)
    {
        var at = text.IndexOf("begincodespacerange", StringComparison.Ordinal);
        if (at < 0)
            return;

        var tokens = HexTokens(text, at, text.IndexOf("endcodespacerange", at, StringComparison.Ordinal));
        if (tokens.Count > 0)
            CodeLength = Math.Max(1, tokens[0].Length / 2);
    }

    /// <summary>
    /// <c>&lt;src&gt; &lt;dst&gt;</c> pairs between <c>beginbfchar</c> and <c>endbfchar</c>.
    /// </summary>
    private void ReadCharMappings(string text)
    {
        var at = 0;
        while ((at = text.IndexOf("beginbfchar", at, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf("endbfchar", at, StringComparison.Ordinal);
            if (end < 0)
                return;

            var tokens = HexTokens(text, at, end);
            for (var index = 0; index + 1 < tokens.Count; index += 2)
                _map[ToCode(tokens[index])] = ToText(tokens[index + 1]);

            at = end;
        }
    }

    /// <summary>
    /// <c>&lt;lo&gt; &lt;hi&gt; &lt;dst&gt;</c> triples between <c>beginbfrange</c> and
    /// <c>endbfrange</c>, where every code from lo to hi maps to consecutive characters from dst.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The array form — <c>&lt;lo&gt; &lt;hi&gt; [&lt;d1&gt; &lt;d2&gt; …]</c>, one destination per
    /// code rather than a run — is read too, and it has to be. Collecting every hexadecimal string
    /// in the block and stepping through them three at a time, as this once did, does not skip an
    /// array: it swallows the array's elements into the same stream and shifts the stride by as many
    /// as it holds, so every later entry in the block maps the wrong codes to the wrong text. Wrong
    /// text is worse than absent text, which is exactly the argument for not guessing at it.
    /// </para>
    /// <para>
    /// A destination is a <em>string</em> of UTF-16BE code units, not a scalar. Reading it as one
    /// number and calling <c>char.ConvertFromUtf32</c> on the result throws for anything but a
    /// single code unit — a ligature such as <c>&lt;00660069&gt;</c> for "fi" reads as 6684777, and
    /// a surrogate pair for an emoji is no better — and the exception came out of extraction rather
    /// than out of the map, taking the whole page with it. The increment for successive codes
    /// applies to the last code unit, as the specification says.
    /// </para>
    /// </remarks>
    private void ReadRangeMappings(string text)
    {
        var at = 0;
        while ((at = text.IndexOf("beginbfrange", at, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf("endbfrange", at, StringComparison.Ordinal);
            if (end < 0)
                return;

            ReadRangeBlock(Tokens(text, at, end));
            at = end;
        }
    }

    private void ReadRangeBlock(List<string> tokens)
    {
        var index = 0;
        while (index + 2 < tokens.Count)
        {
            // Resynchronise rather than give up: a bracket where a source code should be means the
            // block is not shaped as expected, and the entries after it may still be.
            if (IsBracket(tokens[index]) || IsBracket(tokens[index + 1]))
            {
                index++;
                continue;
            }

            var low = ToCode(tokens[index]);
            var high = ToCode(tokens[index + 1]);

            if (tokens[index + 2] == "[")
            {
                index = ReadDestinationArray(tokens, index + 3, low, high);
                continue;
            }

            MapRange(low, high, ToText(tokens[index + 2]));
            index += 3;
        }
    }

    /// <summary>
    /// One destination per code, up to the closing bracket. Answers where to carry on reading.
    /// </summary>
    private int ReadDestinationArray(List<string> tokens, int index, int low, int high)
    {
        for (var code = low; index < tokens.Count && tokens[index] != "]"; index++, code++)
        {
            if (code <= high)
                _map[code] = ToText(tokens[index]);
        }

        return index < tokens.Count ? index + 1 : index;
    }

    /// <summary>
    /// Every code from low to high, the destination advancing by its last code unit each time.
    /// </summary>
    private void MapRange(int low, int high, string destination)
    {
        // A range covering the whole codespace is a producer saying "identity", and expanding it
        // would be 65536 entries of nothing.
        if (high < low || high - low > 0xFFFF || destination.Length == 0)
            return;

        var units = destination.ToCharArray();
        var last = units.Length - 1;
        var start = units[last];

        for (var code = low; code <= high; code++)
        {
            var advanced = start + (code - low);

            // The specification bounds a range so that incrementing cannot carry out of the last
            // code unit. One that does is malformed, and the rest of the range means nothing.
            if (advanced > 0xFFFF)
                return;

            units[last] = (char)advanced;
            _map[code] = new string(units);
        }
    }

    /// <summary>
    /// The hexadecimal strings between two positions, each without its angle brackets, and the
    /// square brackets that group them.
    /// </summary>
    private static List<string> Tokens(string text, int from, int to)
    {
        var tokens = new List<string>();
        if (to < 0 || to > text.Length)
            to = text.Length;

        for (var at = from; at < to; at++)
        {
            var ch = text[at];
            if (ch is '[' or ']')
            {
                tokens.Add(ch == '[' ? "[" : "]");
                continue;
            }

            if (ch != '<')
                continue;

            var close = text.IndexOf('>', at);
            if (close < 0 || close >= to)
                break;

            tokens.Add(text.Substring(at + 1, close - at - 1).Trim());
            at = close;
        }

        return tokens;
    }

    /// <summary>
    /// The hexadecimal strings between two positions, for the blocks that have no arrays in them.
    /// </summary>
    private static List<string> HexTokens(string text, int from, int to)
    {
        var tokens = Tokens(text, from, to);
        tokens.RemoveAll(IsBracket);
        return tokens;
    }

    private static bool IsBracket(string token) => token is "[" or "]";

    /// <summary>
    /// A source code, which is a scalar however many bytes it was written in.
    /// </summary>
    /// <remarks>
    /// Parsed rather than assumed: these come out of a file this library did not write, so a token
    /// that is not hexadecimal at all answers zero instead of throwing out of the enclosing page.
    /// </remarks>
    private static int ToCode(string hex)
    {
        if (hex.Length == 0)
            return 0;

        return int.TryParse(hex.Length > 8 ? hex[..8] : hex,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) ? code : 0;
    }

    /// <summary>
    /// A destination is a run of UTF-16BE code units, so a single mapping may be more than one
    /// character — which is how a ligature says it stands for the letters it joined.
    /// </summary>
    private static string ToText(string hex)
    {
        var text = new StringBuilder();
        for (var at = 0; at < hex.Length; at += 4)
        {
            // A trailing group of fewer than four digits is malformed; read what is there rather
            // than dropping a mapping over it.
            var take = Math.Min(4, hex.Length - at);
            if (!int.TryParse(hex.AsSpan(at, take), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var unit))
                break;

            text.Append((char)unit);
        }

        return text.ToString();
    }
}
