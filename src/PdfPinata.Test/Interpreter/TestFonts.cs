using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;

namespace PdfPinata.Test.Interpreter;

/// <summary>Font dictionaries written by hand, with the widths a test wants and no font program.</summary>
internal static class TestFonts
{
    /// <summary>A simple font whose <c>/Widths</c> start at <paramref name="firstChar"/>.</summary>
    public static PdfDictionary Simple(PdfDocument document, int firstChar, params double[] widths)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/TrueType");
        font.Elements.SetInteger("/FirstChar", firstChar);
        font.Elements.SetInteger("/LastChar", firstChar + widths.Length - 1);
        font.Elements["/Widths"] = new PdfArray(document, widths.Select(width => (PdfItem)new PdfReal(width)).ToArray());
        return font;
    }

    /// <summary>A simple font that states no widths, as a standard 14 font need not.</summary>
    public static PdfDictionary WithoutWidths(PdfDocument document)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", "/Helvetica");
        return font;
    }

    /// <summary>
    /// A composite font whose descendant states <c>/DW</c> and <c>/W</c>, the latter as the items
    /// of the array in order, a nested array given as a <c>double[]</c>.
    /// </summary>
    public static PdfDictionary Composite(PdfDocument document, string encoding, double defaultWidth, params object[] w)
    {
        var descendant = new PdfDictionary(document);
        descendant.Elements.SetName("/Type", "/Font");
        descendant.Elements.SetName("/Subtype", "/CIDFontType2");
        descendant.Elements.SetReal("/DW", defaultWidth);
        descendant.Elements["/W"] = new PdfArray(document, w.Select(item => item is double[] run
            ? new PdfArray(document, run.Select(width => (PdfItem)new PdfReal(width)).ToArray())
            : (PdfItem)new PdfReal(Convert.ToDouble(item))).ToArray());
        document.Internals.AddObject(descendant);

        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type0");
        font.Elements.SetName("/Encoding", encoding);
        font.Elements["/DescendantFonts"] = new PdfArray(document, descendant.Reference);
        return font;
    }
}

/// <summary>
/// A glyph source whose every glyph is the unit square, so that where a glyph lands and how large
/// says everything about how the text was set.
/// </summary>
/// <remarks>Code 0 has no glyph, and code 32 has a glyph that draws nothing.</remarks>
internal sealed class SquareGlyphs : PageGlyphSource
{
    private static readonly PagePath Square = MakeSquare();

    /// <summary>Every code an outline was asked for, in the order they were asked.</summary>
    public List<int> Asked { get; } = [];

    /// <summary>What <see cref="TryGetAdvance"/> answers, or null for it to answer that it knows none.</summary>
    public double? Advance { get; init; }

    public override PagePath GetOutline(int code)
    {
        Asked.Add(code);
        return code switch
        {
            0 => null,
            32 => PagePath.Empty,
            _ => Square
        };
    }

    public override bool TryGetAdvance(int code, out double advance)
    {
        advance = Advance ?? 0;
        return Advance != null;
    }

    private static PagePath MakeSquare()
    {
        var builder = new PagePathBuilder();
        builder.AddRectangle(0, 0, 1, 1);
        return builder.ToPath();
    }
}
