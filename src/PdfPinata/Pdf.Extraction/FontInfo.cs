using System.Collections.Generic;

namespace PdfPinata.Pdf.Extraction;

/// <summary>
/// What extraction needs to know about a font: how wide a code is, what it stands for, and how far
/// it advances the pen.
/// </summary>
internal sealed class FontInfo
{
    private readonly ToUnicodeCMap _toUnicode;
    private readonly Dictionary<int, double> _widths = new();
    private readonly double _defaultWidth;

    private FontInfo(ToUnicodeCMap toUnicode, int codeLength, double defaultWidth)
    {
        _toUnicode = toUnicode;
        CodeLength = codeLength;
        _defaultWidth = defaultWidth;
    }

    /// <summary>
    /// How many bytes of a shown string make one code.
    /// </summary>
    public int CodeLength { get; }

    public static FontInfo From(PdfDictionary font)
    {
        var composite = font.Elements.GetName("/Subtype") == "/Type0";
        var toUnicode = ReadToUnicode(font);

        // A composite font's codes are two bytes for the Identity encodings this library writes and
        // for nearly everything else in practice. The CMap is asked first because it is the only
        // thing that actually says so.
        var codeLength = toUnicode?.CodeLength ?? (composite ? 2 : 1);

        var info = new FontInfo(toUnicode, codeLength, composite ? 1.0 : 0.5);
        if (composite)
            info.ReadCompositeWidths(font);
        else
            info.ReadSimpleWidths(font);

        return info;
    }

    /// <summary>
    /// What a code stands for. A code the map does not cover comes back as the code's own low byte
    /// if that is printable, because a simple font with no map is usually one of the standard
    /// encodings and Latin-1 is right far more often than a replacement character would be.
    /// </summary>
    public string TextFor(int code)
    {
        var mapped = _toUnicode?[code];
        if (mapped != null)
            return mapped;

        // A single byte with no map: read it as Latin-1, which agrees with WinAnsi over the range
        // that matters and is right far more often than a replacement character would be. A
        // two-byte code with no map is a glyph index and stands for nothing at all, so it is
        // dropped rather than turned into a character it never meant.
        return CodeLength == 1 ? ((char)code).ToString() : "";
    }

    /// <summary>
    /// The advance of a code, as a fraction of the type size — a glyph one em wide answers 1.
    /// </summary>
    public double WidthOf(int code) =>
        _widths.TryGetValue(code, out var width) ? width : _defaultWidth;

    private static ToUnicodeCMap ReadToUnicode(PdfDictionary font)
    {
        var map = font.Elements.GetDictionary("/ToUnicode");
        if (map?.Stream == null)
            return null;

        map.Stream.TryUnfilter();
        return ToUnicodeCMap.Parse(map.Stream.Value);
    }

    /// <summary>
    /// A simple font lists one width per code from <c>/FirstChar</c> onwards.
    /// </summary>
    private void ReadSimpleWidths(PdfDictionary font)
    {
        var widths = font.Elements.GetArray("/Widths");
        if (widths == null)
            return;

        var first = font.Elements.GetInteger("/FirstChar");
        for (var index = 0; index < widths.Elements.Count; index++)
            _widths[first + index] = widths.Elements.GetReal(index) / 1000.0;
    }

    /// <summary>
    /// A composite font's widths live on its descendant, in a <c>/W</c> array written two ways at
    /// once: <c>c [w1 w2 …]</c> for a run of codes with individual widths, and
    /// <c>cFirst cLast w</c> for a run that shares one.
    /// </summary>
    private void ReadCompositeWidths(PdfDictionary font)
    {
        var descendants = font.Elements.GetArray("/DescendantFonts");
        var descendant = descendants?.Elements.GetDictionary(0);
        var w = descendant?.Elements.GetArray("/W");
        if (w == null)
            return;

        for (var at = 0; at < w.Elements.Count;)
        {
            var first = (int)w.Elements.GetReal(at++);
            if (at >= w.Elements.Count)
                break;

            if (w.Elements[at] is PdfArray individually)
            {
                for (var index = 0; index < individually.Elements.Count; index++)
                    _widths[first + index] = individually.Elements.GetReal(index) / 1000.0;
                at++;
            }
            else
            {
                var last = (int)w.Elements.GetReal(at++);
                if (at >= w.Elements.Count)
                    break;

                var width = w.Elements.GetReal(at++) / 1000.0;

                // A run may legitimately be long, but a malformed one may claim millions of codes;
                // filling that in would be a denial of service by arithmetic.
                if (last - first > 0xFFFF)
                    continue;

                for (var code = first; code <= last; code++)
                    _widths[code] = width;
            }
        }
    }
}
