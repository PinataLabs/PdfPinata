using System;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// How far each code of a font advances, from the font dictionary alone and never from the font
/// program: the PDF's numbers are the ones its producer laid the text out with.
/// </summary>
/// <remarks>
/// <para>
/// A simple font states <c>/Widths</c> from <c>/FirstChar</c> on, and its descriptor's
/// <c>/MissingWidth</c> for a code outside them. A composite font states <c>/W</c> and
/// <c>/DW</c> on its descendant, by CID. Both are in thousandths of text space, which is divided
/// out here. <b>A Type 3 font is the exception</b>: its widths are in glyph space, and its
/// <c>/FontMatrix</c> takes them to text space (ISO 32000-1 9.6.5). A font made by TeX commonly
/// has a matrix of a thousandth, and a bitmap font as commonly has not.
/// </para>
/// <para>
/// <b>This holds the default width and where the others are stated, and not the widths.</b> A
/// <c>/W</c> array of three numbers gives 65,536 CIDs a width, and read into each font dictionary
/// that names it, 1024 dictionaries in 159 KB of file held 2 GiB. The widths are read into a
/// <see cref="WidthTable"/> that the <see cref="RenderFontCache"/> keeps under the array, or
/// under the simple font's dictionary, so they are read once for all the fonts that share it
/// and are charged to the document's budget.
/// </para>
/// </remarks>
internal sealed class FontWidths
{
    private readonly PdfDictionary _simpleFont;
    private readonly PdfArray _w;
    private readonly double _defaultWidth;
    private readonly bool _stated;

    private FontWidths(bool stated, double defaultWidth, PdfDictionary simpleFont, PdfArray w)
    {
        _stated = stated;
        _defaultWidth = defaultWidth;
        _simpleFont = simpleFont;
        _w = w;
    }

    /// <summary>Where a font dictionary states its widths. Reads none of them.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    internal static FontWidths Of(PdfDictionary font, PdfDictionary descendant)
    {
        if (descendant != null)
        {
            var defaultWidth = PdfItemValues.TryGetNumber(descendant.Elements["/DW"], out var stated) ? stated : 1000;
            return new FontWidths(true, defaultWidth / 1000, null, descendant.Elements.GetArray("/W"));
        }

        var missing = 0.0;
        var descriptor = font.Elements.GetDictionary("/FontDescriptor");
        if (descriptor != null && PdfItemValues.TryGetNumber(descriptor.Elements["/MissingWidth"], out var width))
            missing = width;

        var hasWidths = font.Elements.GetArray("/Widths") != null;
        return new FontWidths(hasWidths, missing * TextSpaceUnitOf(font), hasWidths ? font : null, null);
    }

    /// <summary>
    /// The advance of a code in text space, where 1 is one em, or null when the font dictionary
    /// states no widths at all, as a standard 14 font need not.
    /// </summary>
    internal double? Of(int code, RenderFontCache cache)
    {
        if (!_stated)
            return null;

        var table = _simpleFont != null ? cache.Data(FontDataKind.SimpleWidths, _simpleFont, static (font, _) => ReadSimple(font))
            : _w != null ? cache.Data(FontDataKind.CidWidths, _w, static (w, _) => ReadComposite(w))
            : null;
        return table != null && table.TryGet(code, 0, out var width) ? width : _defaultWidth;
    }

    private static WidthTable ReadSimple(PdfDictionary font)
    {
        var stated = font.Elements.GetArray("/Widths");
        var toTextSpace = TextSpaceUnitOf(font);
        var widths = new WidthTable.Builder(1, 256);
        TryGetCid(font.Elements["/FirstChar"], out var first);
        for (var index = 0; index < stated.Elements.Count && index < 256; index++)
        {
            if (PdfItemValues.TryGetNumber(stated.Elements[index], out var value))
                widths.Set((long)first + index, 0, value * toTextSpace);
        }

        return widths.ToTable();
    }

    /// <summary>
    /// What one unit of a simple font's widths comes to in text space: a thousandth, or for a
    /// Type 3 font the horizontal scale of its <c>/FontMatrix</c>.
    /// </summary>
    /// <remarks>
    /// Only the matrix's first number is read. Text is set along x, and a Type 3 font whose
    /// matrix turns an advance along x into one along y is not one any producer writes.
    /// </remarks>
    private static double TextSpaceUnitOf(PdfDictionary font)
    {
        const double thousandth = 0.001;
        if (font.Elements.GetNameOrEmpty("/Subtype") != "/Type3")
            return thousandth;

        var matrix = font.Elements.GetArray("/FontMatrix");
        return matrix != null && matrix.Elements.Count == 6
                              && PdfItemValues.TryGetNumber(matrix.Elements[0], out var scale) && double.IsFinite(scale)
            ? scale
            : thousandth;
    }

    private static WidthTable ReadComposite(PdfArray w)
    {
        var widths = new WidthTable.Builder(1, MaximumCid + 1);

        // Every CID can be given a width a few times over and no more. A /W that goes on giving
        // the whole range one width, run after run, would otherwise take minutes to read, and
        // what it says after this many is not what any producer wrote.
        var budget = 4 * (MaximumCid + 1);

        // Two forms at once: c [w1 w2 …] gives each of a run of CIDs its own width, and
        // cFirst cLast w gives a run of them one width.
        for (var at = 0; at + 1 < w.Elements.Count && budget > 0;)
        {
            if (!TryGetCid(w.Elements[at], out var first))
                break;

            if (PdfReference.Dereference(w.Elements[at + 1]) is PdfArray individually)
            {
                for (var index = 0; index < individually.Elements.Count && index <= MaximumCid && budget > 0; index++, budget--)
                {
                    if (PdfItemValues.TryGetNumber(individually.Elements[index], out var value))
                        widths.Set((long)first + index, 0, value / 1000);
                }

                at += 2;
                continue;
            }

            if (at + 2 >= w.Elements.Count || !TryGetCid(w.Elements[at + 1], out var last)
                                           || !PdfItemValues.TryGetNumber(w.Elements[at + 2], out var shared))
            {
                break;
            }

            // The counter is a long, because a run that ends at the largest integer would wrap
            // an int round and never finish, and the run is held to the CIDs there can be, so a
            // malformed one that claims millions fills in none past them.
            for (long cid = first; cid <= last && cid <= MaximumCid && budget > 0; cid++, budget--)
                widths.Set(cid, 0, shared / 1000);

            at += 3;
        }

        return widths.ToTable();
    }

    /// <summary>The largest CID a two-byte code can name.</summary>
    internal const int MaximumCid = 0xFFFF;

    /// <summary>
    /// Reads a CID, which a file may write as a real such as <c>1.0</c>. One out of range is
    /// held to the range, so that a run that begins or ends outside it still covers what of it
    /// lies inside.
    /// </summary>
    internal static bool TryGetCid(PdfItem item, out int cid)
    {
        var isNumber = PdfItemValues.TryGetNumber(item, out var number) && !double.IsNaN(number);
        cid = isNumber ? (int)Math.Max(-1, Math.Min(number, MaximumCid + 1)) : 0;
        return isNumber;
    }
}

/// <summary>
/// The numbers a <c>/Widths</c>, <c>/W</c> or <c>/W2</c> array gives each code or CID: one array
/// of doubles, as long as the highest code given any, and a NaN where one is given none.
/// </summary>
/// <remarks>
/// At most 65,536 codes of one or three numbers, so half a megabyte for a <c>/W</c> and a megabyte
/// and a half for a <c>/W2</c> however the array is written, where a dictionary of them was 2 MiB
/// and 3 MiB.
/// </remarks>
internal sealed class WidthTable : IFontData
{
    private readonly double[] _values;
    private readonly int _each;

    private WidthTable(double[] values, int each)
    {
        _values = values;
        _each = each;
    }

    public long HeldBytes => 64 + 8L * _values.Length;

    /// <summary>One of the numbers stated for a code, where the code is given any.</summary>
    /// <param name="code">The code or CID.</param>
    /// <param name="which">Which of its numbers.</param>
    /// <param name="value">The number.</param>
    internal bool TryGet(int code, int which, out double value)
    {
        var at = (long)code * _each + which;
        value = code >= 0 && at < _values.Length ? _values[at] : double.NaN;
        return !double.IsNaN(value);
    }

    /// <summary>Gathers a table. A number that is not one, a NaN, is as good as not stated.</summary>
    /// <param name="each">How many numbers each code has.</param>
    /// <param name="codes">How many codes there can be.</param>
    internal sealed class Builder(int each, int codes)
    {
        private readonly double[] _values = Filled(each * codes);
        private int _used;

        internal void Set(long code, int which, double value)
        {
            if (code < 0 || code >= codes)
                return;

            _values[code * each + which] = value;
            _used = Math.Max(_used, ((int)code + 1) * each);
        }

        internal WidthTable ToTable() => new(_values.AsSpan(0, _used).ToArray(), each);

        private static double[] Filled(int length)
        {
            var values = new double[length];
            Array.Fill(values, double.NaN);
            return values;
        }
    }
}

/// <summary>
/// How a composite font in vertical writing places and advances each CID: <c>/W2</c> and
/// <c>/DW2</c> on its descendant, by ISO 32000-1 9.7.4.3. Like the widths, never from the font program.
/// </summary>
/// <remarks>
/// <para>
/// A glyph is placed with its position vector, (vx, vy), on the pen, and the pen then moves down by
/// the vertical advance. Where <c>/W2</c> gives a CID nothing, vx is half its horizontal width and
/// vy and the advance are <c>/DW2</c>'s, which is [880 -1000] where it is not stated.
/// </para>
/// <para>
/// Like <see cref="FontWidths"/>, this holds the defaults and the <c>/W2</c> array, and what the
/// array says is read into a <see cref="WidthTable"/> the <see cref="RenderFontCache"/> keeps
/// under it.
/// </para>
/// </remarks>
internal sealed class VerticalMetrics
{
    private readonly PdfArray _w2;
    private readonly double _defaultY;
    private readonly double _defaultAdvance;

    private VerticalMetrics(double defaultY, double defaultAdvance, PdfArray w2)
    {
        _defaultY = defaultY;
        _defaultAdvance = defaultAdvance;
        _w2 = w2;
    }

    /// <summary>Where a descendant font states its vertical metrics. Reads none but the defaults.</summary>
    internal static VerticalMetrics Of(PdfDictionary descendant)
    {
        double defaultY = 880, defaultAdvance = -1000;
        var dw2 = descendant.Elements.GetArray("/DW2");
        if (dw2 != null && dw2.Elements.Count >= 2
                        && PdfItemValues.TryGetNumber(dw2.Elements[0], out var y)
                        && PdfItemValues.TryGetNumber(dw2.Elements[1], out var advance))
        {
            (defaultY, defaultAdvance) = (y, advance);
        }

        return new VerticalMetrics(defaultY / 1000, defaultAdvance / 1000, descendant.Elements.GetArray("/W2"));
    }

    /// <summary>
    /// The vertical advance, negative for down the page, and the position vector of a CID, all in
    /// text space before the type size is applied.
    /// </summary>
    /// <param name="cid">The CID.</param>
    /// <param name="horizontalWidth">The CID's horizontal width, half of which is vx where nothing else says.</param>
    /// <param name="cache">What keeps what <c>/W2</c> says.</param>
    internal (double Advance, double X, double Y) Of(int cid, double horizontalWidth, RenderFontCache cache)
    {
        if (_w2 != null)
        {
            var table = cache.Data(FontDataKind.VerticalMetrics, _w2, static (w2, _) => Read(w2));
            if (table.TryGet(cid, 0, out var advance) && table.TryGet(cid, 1, out var x) && table.TryGet(cid, 2, out var y))
                return (advance, x, y);
        }

        return (_defaultAdvance, horizontalWidth / 2, _defaultY);
    }

    private static WidthTable Read(PdfArray w2)
    {
        var metrics = new WidthTable.Builder(3, FontWidths.MaximumCid + 1);

        // The same two forms as /W, each width a triple: c [w1y vx vy …] gives each of a run of
        // CIDs its own, and cFirst cLast w1y vx vy gives a run of them one. Bounded as /W is.
        var budget = 4 * (FontWidths.MaximumCid + 1);
        for (var at = 0; at + 1 < w2.Elements.Count && budget > 0;)
        {
            if (!FontWidths.TryGetCid(w2.Elements[at], out var first))
                break;

            if (PdfReference.Dereference(w2.Elements[at + 1]) is PdfArray individually)
            {
                for (var index = 0; index + 2 < individually.Elements.Count && budget > 0; index += 3, budget--)
                {
                    if (TryGetTriple(individually, index, out var own))
                        Set(metrics, (long)first + index / 3, own);
                }

                at += 2;
                continue;
            }

            if (at + 4 >= w2.Elements.Count || !FontWidths.TryGetCid(w2.Elements[at + 1], out var last))
                break;

            if (TryGetTriple(w2, at + 2, out var shared))
            {
                for (long cid = first; cid <= last && cid <= FontWidths.MaximumCid && budget > 0; cid++, budget--)
                    Set(metrics, cid, shared);
            }

            at += 5;
        }

        return metrics.ToTable();
    }

    /// <summary>The vertical advance and the position vector written at three places of an array, in text space.</summary>
    private static bool TryGetTriple(PdfArray numbers, int from, out (double Advance, double X, double Y) triple)
    {
        triple = default;
        if (!PdfItemValues.TryGetNumber(numbers.Elements[from], out var advance)
            || !PdfItemValues.TryGetNumber(numbers.Elements[from + 1], out var x)
            || !PdfItemValues.TryGetNumber(numbers.Elements[from + 2], out var y))
        {
            return false;
        }

        triple = (advance / 1000, x / 1000, y / 1000);
        return true;
    }

    private static void Set(WidthTable.Builder metrics, long cid, (double Advance, double X, double Y) triple)
    {
        metrics.Set(cid, 0, triple.Advance);
        metrics.Set(cid, 1, triple.X);
        metrics.Set(cid, 2, triple.Y);
    }
}
