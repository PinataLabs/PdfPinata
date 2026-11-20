using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// How far each code of a font advances, from the font dictionary alone and never from the font
/// program: the PDF's numbers are the ones its producer laid the text out with.
/// </summary>
/// <remarks>
/// A simple font states <c>/Widths</c> from <c>/FirstChar</c> on, and its descriptor's
/// <c>/MissingWidth</c> for a code outside them. A composite font states <c>/W</c> and
/// <c>/DW</c> on its descendant, by CID. Both are in thousandths of text space, which is divided
/// out here. <b>A Type 3 font is the exception</b>: its widths are in glyph space, and its
/// <c>/FontMatrix</c> takes them to text space (ISO 32000-1 9.6.5). A font made by TeX commonly
/// has a matrix of a thousandth, and a bitmap font as commonly has not.
/// </remarks>
internal sealed class FontWidths
{
    private readonly Dictionary<int, double> _widths = new();
    private readonly double _defaultWidth;
    private readonly bool _stated;

    private FontWidths(bool stated, double defaultWidth)
    {
        _stated = stated;
        _defaultWidth = defaultWidth;
    }

    /// <summary>The widths a font dictionary states.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    internal static FontWidths From(PdfDictionary font, PdfDictionary descendant) =>
        descendant == null ? Simple(font) : Composite(descendant);

    /// <summary>
    /// The advance of a code in text space, where 1 is one em, or null when the font dictionary
    /// states no widths at all, as a standard 14 font need not.
    /// </summary>
    internal double? Of(int code)
    {
        if (!_stated)
            return null;

        return _widths.TryGetValue(code, out var width) ? width : _defaultWidth;
    }

    private static FontWidths Simple(PdfDictionary font)
    {
        var stated = font.Elements.GetArray("/Widths");
        var missing = 0.0;
        var descriptor = font.Elements.GetDictionary("/FontDescriptor");
        if (descriptor != null && PdfItemValues.TryGetNumber(descriptor.Elements["/MissingWidth"], out var width))
            missing = width;

        var toTextSpace = TextSpaceUnitOf(font);
        var widths = new FontWidths(stated != null, missing * toTextSpace);
        if (stated == null)
            return widths;

        TryGetCid(font.Elements["/FirstChar"], out var first);
        for (var index = 0; index < stated.Elements.Count && index < 256; index++)
        {
            if (PdfItemValues.TryGetNumber(stated.Elements[index], out var value))
                widths._widths[first + index] = value * toTextSpace;
        }

        return widths;
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

    private static FontWidths Composite(PdfDictionary descendant)
    {
        var defaultWidth = PdfItemValues.TryGetNumber(descendant.Elements["/DW"], out var stated) ? stated : 1000;
        var widths = new FontWidths(true, defaultWidth / 1000);

        var w = descendant.Elements.GetArray("/W");
        if (w == null)
            return widths;

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
                        widths.Set((long)first + index, value / 1000);
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
                widths.Set(cid, shared / 1000);

            at += 3;
        }

        return widths;
    }

    /// <summary>The largest CID a two-byte code can name.</summary>
    private const int MaximumCid = 0xFFFF;

    /// <summary>
    /// Reads a CID, which a file may write as a real such as <c>1.0</c>. One out of range is
    /// held to the range, so that a run that begins or ends outside it still covers what of it
    /// lies inside.
    /// </summary>
    private static bool TryGetCid(PdfItem item, out int cid)
    {
        var isNumber = PdfItemValues.TryGetNumber(item, out var number) && !double.IsNaN(number);
        cid = isNumber ? (int)Math.Max(-1, Math.Min(number, MaximumCid + 1)) : 0;
        return isNumber;
    }

    private void Set(long cid, double width)
    {
        if (cid is >= 0 and <= MaximumCid)
            _widths[(int)cid] = width;
    }
}
