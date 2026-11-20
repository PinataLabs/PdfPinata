namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Chooses where a font dictionary's glyph outlines come from. A new kind of font is a new
/// <see cref="PageGlyphSource"/> and a branch here, and nothing in the interpreter.
/// </summary>
/// <remarks>
/// The order of the branches is the rule: a font program embedded in the file is used before a
/// standard 14 font's name is looked at, because a file that embeds one means it.
/// </remarks>
internal static class BuiltInGlyphSources
{
    /// <summary>The source for a font, or null where this version has none.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names: for a composite font, the Type 0 dictionary.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    /// <param name="why">Why there is no source, where there is none. It is what the font is reported with.</param>
    /// <remarks>
    /// This must not read a seam, and nor may the constructor of what it answers. It is called
    /// when a font is selected, and a page that selects Helvetica only to show invisible text
    /// has to render with no font resolver set.
    /// </remarks>
    internal static PageGlyphSource Select(PdfDictionary font, PdfDictionary descendant, out string why)
    {
        if (font.Elements.GetNameOrEmpty("/Subtype") == "/Type3")
        {
            why = "Type 3 fonts are not drawn yet.";
            return null;
        }

        var descriptor = (descendant ?? font).Elements.GetDictionary("/FontDescriptor");
        if (descriptor?.Elements.GetDictionary("/FontFile2") != null)
        {
            // An embedded TrueType font program is read here.
            why = "Embedded TrueType font programs are not read yet.";
            return null;
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
