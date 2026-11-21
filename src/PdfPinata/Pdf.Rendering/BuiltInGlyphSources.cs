namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Chooses where a font dictionary's glyph outlines come from.
/// </summary>
/// <remarks>
/// <para>
/// The order of the branches is the rule: a font program embedded in the file is used before a
/// standard 14 font's name is looked at, because a file that embeds one means it.
/// </para>
/// <para>
/// <b>A font whose glyphs are outlines, set along x in codes of a fixed length, is a new
/// <see cref="PageGlyphSource"/> and a branch here, and nothing in the interpreter.</b> That is
/// TrueType, CFF, Type 1 and the standard 14. Three kinds of font are not that, and each needs
/// more than a source:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Type 3.</b> A glyph is a content stream of the font's <c>/CharProcs</c>, with its own
/// resources, drawn under <c>/FontMatrix</c>, and it may set its own colour (<c>d0</c>) or
/// take the text's (<c>d1</c>). It is not an outline, so <c>DrawGlyph</c> has to run it as it
/// runs a form. Only its advance is read today, by <see cref="FontWidths"/>.
/// </description></item>
/// <item><description>
/// <b>A CMap other than <c>Identity-H</c>.</b> Its codespace ranges give codes of one to four
/// bytes in one string, so <c>ShowString</c> has to split a string by the CMap, where it now
/// takes <see cref="RenderFont.BytesPerCode"/> at a time, and map each code to a CID. Word
/// spacing follows the split: it is added after a code of the single byte 32 and after no
/// longer code, which is a question about each code and not about the font.
/// </description></item>
/// <item><description>
/// <b>Vertical writing.</b> A glyph advances down the page by <c>/W2</c> and <c>/DW2</c> and
/// is placed by a position vector, so <see cref="FontWidths"/>, <c>ShowString</c>'s advance and
/// the direction a <c>TJ</c> adjustment moves in all change.
/// </description></item>
/// </list>
/// </remarks>
internal static class BuiltInGlyphSources
{
    /// <summary>The source for a font, or null where this version has none.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names: for a composite font, the Type 0 dictionary.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    /// <param name="cache">What a source keeps everything it reads from the font in.</param>
    /// <param name="why">Why there is no source, where there is none. It is what the font is reported with.</param>
    /// <remarks>
    /// This must not read a seam, and nor may the constructor of what it answers. It is called
    /// when a font is selected, and a page that selects Helvetica only to show invisible text
    /// has to render with no font resolver set.
    /// </remarks>
    internal static PageGlyphSource Select(PdfDictionary font, PdfDictionary descendant, RenderFontCache cache, out string why)
    {
        if (font.Elements.GetNameOrEmpty("/Subtype") == "/Type3")
        {
            why = "Type 3 fonts are not drawn yet.";
            return null;
        }

        var descriptor = (descendant ?? font).Elements.GetDictionary("/FontDescriptor");
        var fontFile2 = descriptor?.Elements.GetDictionary("/FontFile2");
        if (fontFile2 != null)
        {
            why = null;
            return new EmbeddedTrueTypeGlyphSource(font, descendant, fontFile2, cache);
        }

        if (descriptor?.Elements.GetDictionary("/FontFile3") != null
            || descriptor?.Elements.GetDictionary("/FontFile") != null)
        {
            why = "CFF and Type 1 font programs are not read yet.";
            return null;
        }

        // A font with no font program is a standard 14 font, or is treated as the nearest one,
        // and is served here through RequiredSeams.FontResolver.
        why = "Fonts with no embedded font program are not drawn yet.";
        return null;
    }
}
