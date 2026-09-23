using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Charting.Tests.Helpers;

/// <summary>
///   What a page reads, as text.
/// </summary>
/// <remarks>
///   A show-text operator does not carry text. PdfPinata embeds its fonts as Identity-H, so
///   what it carries is two bytes of glyph identifier per character - <c>&lt;0014&gt; Tj</c> for a
///   "1" - and those numbers are the face's own. Asserting against them would pin these tests to
///   the glyph order of Liberation Sans, which is a fact about a font file rather than about the
///   charting code.
///
///   The document says what they mean. Every Type0 font PdfPinata writes carries a /ToUnicode
///   CMap giving one <c>&lt;gid&gt;&lt;gid&gt;&lt;character&gt;</c> line per glyph used, which is
///   how a reader copies text out of the page - so reading it back is reading the same answer any
///   other consumer of the file would get, and a test may say <c>"0.0"</c> and mean it.
///
///   Only the forms PdfPinata actually emits are handled: composite fonts keyed two bytes at a
///   time, simple fonts one. Anything with no map is returned as its raw bytes, so a font the
///   library starts writing differently shows up as unreadable output rather than as silence.
///
///   One limit, and it is on the positions rather than on the text. What is followed here is the
///   text matrix, not the transformation matrix - so a run drawn under a transform is reported in
///   the space that transform set up rather than on the page. A chart applies one translate to
///   everything it draws, which cancels out of any comparison between two runs on the same page,
///   but a rotated axis title is drawn under a rotate of its own and is not comparable with
///   anything. See <c>AxisTitleTests.RotatedCaption</c>, which compares content streams instead.
/// </remarks>
internal static class ShownText
{
    /// <summary>A string the page shows, and where it starts.</summary>
    internal readonly struct Run
    {
        internal Run(string text, double x, double y, string colour = null, double size = 0, string face = null)
        {
            Text = text;
            X = x;
            Y = y;
            Colour = colour;
            Size = size;
            Face = face;
        }

        internal string Text { get; }

        /// <summary>The left edge of the run, in the space the chart was drawn in.</summary>
        internal double X { get; }

        /// <summary>The baseline of the run - y increases up the page.</summary>
        internal double Y { get; }

        /// <summary>
        ///   The fill colour the run is painted in, written as <see cref="PaintedRectangles.ColourOf"/>
        ///   writes one; black until the page sets another.
        /// </summary>
        internal string Colour { get; }

        /// <summary>The size its <c>Tf</c> set.</summary>
        internal double Size { get; }

        /// <summary>
        ///   The /BaseFont of the font it is shown in, subset tag and all - so whether it is the
        ///   bold or the italic face is in the name.
        /// </summary>
        internal string Face { get; }

        public override string ToString() => $"\"{Text}\" at ({X:F2},{Y:F2}) {Face} {Size:0.##}pt rgb={Colour}";
    }

    /// <summary>Every string the page shows, in the order it draws them.</summary>
    internal static IReadOnlyList<string> On(PdfPage page)
    {
        var texts = new List<string>();
        foreach (var run in RunsOn(page))
            texts.Add(run.Text);
        return texts;
    }

    /// <summary>Every string the page shows with the position it was drawn at, in order.</summary>
    /// <remarks>
    ///   The charting renderers position text with Td alone, which moves the start of the next
    ///   line by an offset from the start of the current one, so following them means adding them
    ///   up. BT resets that origin, and Tm replaces it outright - both are honoured here, though
    ///   only the first is reached by a chart today. Nothing sets a leading or uses T*, so a
    ///   renderer that starts to would report its text at the wrong place rather than silently
    ///   at the right one; there is no leading to follow yet.
    /// </remarks>
    internal static IReadOnlyList<Run> RunsOn(PdfPage page)
    {
        var reader = new RunReader(FontsOf(page));
        foreach (var item in ContentReader.ReadContent(PageContent.Of(page)))
        {
            if (item is COperator op)
                reader.Read(op);
        }

        return reader.Shown;
    }

    /// <summary>Follows one page's text, one operator at a time.</summary>
    private sealed class RunReader
    {
        private readonly Dictionary<string, Decoding> _fonts;

        // The font the text is shown in, which is also where its face is named, and its size.
        private Decoding _current;
        private double _size;
        private string _fill = PaintedRectangles.Grey(0);

        // The text state is part of the graphics state (ISO 32000-1 Table 52), so q and Q save and
        // restore the font and its size together with the fill.
        private readonly Stack<(string Fill, Decoding Current, double Size)> _saved = new();

        // Where the current line starts, and where the next glyph goes. A chart draws one run per
        // line, so the two only differ if one ever writes two runs without moving between them.
        private double _lineX, _lineY;

        internal RunReader(Dictionary<string, Decoding> fonts) => _fonts = fonts;

        internal List<Run> Shown { get; } = [];

        internal void Read(COperator op)
        {
            var name = op.OpCode.OpCodeName;
            if (FollowGraphicsState(name, op.Operands) || FollowTextPosition(name, op.Operands))
                return;

            switch (name)
            {
                case OpCodeName.Tf:
                    SelectFont(op.Operands);
                    break;

                case OpCodeName.Tj:
                case OpCodeName.TJ:
                    Show(op.Operands);
                    break;
            }
        }

        private bool FollowGraphicsState(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.q:
                    _saved.Push((_fill, _current, _size));
                    return true;

                case OpCodeName.Q:
                    if (_saved.Count > 0)
                        (_fill, _current, _size) = _saved.Pop();
                    return true;

                // The fill colour, in whichever of the three device spaces it is named.
                case OpCodeName.rg:
                    if (operands.Count >= 3)
                        _fill = PaintedRectangles.Rgb(Number(operands[0]), Number(operands[1]), Number(operands[2]));
                    return true;

                case OpCodeName.g:
                    if (operands.Count >= 1)
                        _fill = PaintedRectangles.Grey(Number(operands[0]));
                    return true;

                case OpCodeName.k:
                    if (operands.Count >= 4)
                        _fill = PaintedRectangles.Cmyk(Number(operands[0]), Number(operands[1]),
                            Number(operands[2]), Number(operands[3]));
                    return true;

                default:
                    return false;
            }
        }

        private bool FollowTextPosition(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.BT:
                    _lineX = _lineY = 0;
                    return true;

                case OpCodeName.Td:
                case OpCodeName.TD:
                    if (operands.Count >= 2)
                    {
                        _lineX += Number(operands[0]);
                        _lineY += Number(operands[1]);
                    }
                    return true;

                case OpCodeName.Tm:
                    // The last two operands of a text matrix are its translation.
                    if (operands.Count >= 6)
                    {
                        _lineX = Number(operands[4]);
                        _lineY = Number(operands[5]);
                    }
                    return true;

                default:
                    return false;
            }
        }

        private void SelectFont(CSequence operands)
        {
            // "/F0 10 Tf" - the resource name is the first operand. A name the page does not
            // define leaves no decoding at all, so its bytes are reported as they stand.
            if (operands.Count >= 1 && operands[0] is CName name)
                _fonts.TryGetValue(name.Name, out _current);
            if (operands.Count >= 2)
                _size = Number(operands[1]);
        }

        private void Show(CSequence operands)
        {
            foreach (var text in StringsIn(operands))
                Shown.Add(new Run(Decode(text, _current), _lineX, _lineY, _fill, _size, _current?.Face));
        }
    }

    private static double Number(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => 0
    };

    /// <summary>
    ///   The strings the page shows that read as a number, as they were written.
    /// </summary>
    /// <remarks>
    ///   On a chart whose categories are named with letters and whose data labels are off, these
    ///   are the tick labels of the value axis and nothing else - which is how a test says what
    ///   scale the axis came out at. They are compared as text rather than as numbers because the
    ///   format is part of the answer: the default of "0.0" is what turns a step of 0.02 into
    ///   three tick labels all reading 0.0.
    /// </remarks>
    internal static IReadOnlyList<string> NumericOn(PdfPage page)
    {
        var numbers = new List<string>();
        foreach (var text in On(page))
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                numbers.Add(text);
        }
        return numbers;
    }

    /// <summary>How the bytes of a show-text operator turn back into characters.</summary>
    private sealed class Decoding
    {
        internal Decoding(bool twoByteCodes, IReadOnlyDictionary<int, char> characters, string face)
        {
            TwoByteCodes = twoByteCodes;
            Characters = characters;
            Face = face;
        }

        internal string Face { get; }

        /// <summary>Composite fonts are keyed two bytes at a time, simple fonts one.</summary>
        internal bool TwoByteCodes { get; }

        internal IReadOnlyDictionary<int, char> Characters { get; }
    }

    private static string Decode(string raw, Decoding decoding)
    {
        if (decoding == null)
            return raw;

        var text = new StringBuilder();
        var step = decoding.TwoByteCodes ? 2 : 1;

        for (var idx = 0; idx + step <= raw.Length; idx += step)
        {
            // The lexer reads a string literal one char per byte, so a char here is a byte.
            var code = decoding.TwoByteCodes ? (raw[idx] << 8) | raw[idx + 1] : raw[idx];
            text.Append(decoding.Characters.TryGetValue(code, out var character) ? character : (char)code);
        }

        return text.ToString();
    }

    /// <summary>The decoding for each font the page names, keyed by its resource name.</summary>
    private static Dictionary<string, Decoding> FontsOf(PdfPage page)
    {
        var decodings = new Dictionary<string, Decoding>(StringComparer.Ordinal);

        var fonts = Resolve(page.Elements["/Resources"]) as PdfDictionary;
        if (Resolve(fonts?.Elements["/Font"]) is not PdfDictionary dictionary)
            return decodings;

        foreach (var key in dictionary.Elements.KeyNames)
        {
            if (Resolve(dictionary.Elements[key.Value]) is not PdfDictionary font)
                continue;

            var composite = font.Elements.GetName("/Subtype") == "/Type0";
            decodings[key.Value] = new Decoding(composite, CharactersOf(font), font.Elements.GetName("/BaseFont"));
        }

        return decodings;
    }

    /// <summary>
    ///   The glyph-to-character map of a font, read out of its /ToUnicode CMap. Empty for a font
    ///   that carries none, which leaves its bytes to be reported as they stand.
    /// </summary>
    private static IReadOnlyDictionary<int, char> CharactersOf(PdfDictionary font)
    {
        var characters = new Dictionary<int, char>();

        if (Resolve(font.Elements["/ToUnicode"]) is not PdfDictionary map || map.Stream == null)
            return characters;

        var cmap = Encoding.UTF8.GetString(map.Stream.UnfilteredValue);

        // One line per glyph, "<gid><gid><character>", as PdfToUnicodeMap writes it. Ranges wider
        // than a single code are not emitted, so nothing here has to expand one.
        foreach (Match entry in BfRangeEntry.Matches(cmap))
        {
            var from = int.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var to = int.Parse(entry.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var character = int.Parse(entry.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            for (var code = from; code <= to; code++)
                characters[code] = (char)(character + code - from);
        }

        return characters;
    }

    private static readonly Regex BfRangeEntry =
        new("<([0-9A-Fa-f]{4})><([0-9A-Fa-f]{4})><([0-9A-Fa-f]{4})>", RegexOptions.Compiled);

    /// <summary>The strings inside an operand list, whether given bare or inside a TJ array.</summary>
    private static IEnumerable<string> StringsIn(CSequence operands)
    {
        foreach (var operand in operands)
        {
            switch (operand)
            {
                case CString text:
                    yield return text.Value;
                    break;

                case CArray array:
                    foreach (var item in array)
                    {
                        if (item is CString part)
                            yield return part.Value;
                    }
                    break;
            }
        }
    }

    private static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;
}
