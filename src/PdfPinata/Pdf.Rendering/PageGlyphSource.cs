using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Where the glyph outlines of one font dictionary come from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A source answers outlines and nothing about layout.</b> How a shown string splits into
/// codes, how far each code advances by the PDF's own <c>/Widths</c> or <c>/W</c>, character and
/// word spacing, horizontal scaling, rise and the adjustments in a <c>TJ</c> array are all worked
/// out by page rendering, the same way for every kind of font.
/// </para>
/// <para>
/// Each glyph is asked for once and the answer kept, so a source need not cache, and the path a
/// device is handed for a glyph is the same instance every time. That holds for the first
/// 1,048,576 points of outline a font draws. Past that, a glyph not yet kept is asked for each time
/// it is drawn and handed over with <see cref="PagePath.IsShared"/> false, so a font program of a
/// few hundred bytes cannot hold gigabytes of outline.
/// </para>
/// <para>
/// A source is made when a font is selected and may be made for text that is never drawn, such as
/// the invisible text under a scanned page. So making one reads no seam, and nor does
/// <see cref="TryGetAdvance"/>, which is asked for invisible text too: a source that needs
/// <c>GlobalFontSettings.FontResolver</c> reads it in <see cref="GetOutline"/> and nowhere else.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public abstract class PageGlyphSource
{
    /// <summary>The outline of a glyph.</summary>
    /// <param name="code">
    /// For a simple font, the character code, from 0 to 255: choosing the glyph through
    /// <c>/Encoding</c>, <c>/Differences</c> and the font program's <c>cmap</c> is the source's
    /// work. For a composite font, the CID: the font's CMap has been applied, and
    /// <c>/CIDToGIDMap</c> is the source's work.
    /// </param>
    /// <returns>
    /// The outline in text space before the type size is applied, y upwards, with its origin on
    /// the baseline where the glyph is placed: a glyph one em wide spans 0 to 1. So the units per
    /// em of a font program are already divided out. <see cref="PagePath.Empty"/> for a glyph
    /// that draws nothing, such as a space. Null where the font has no such glyph, which is
    /// reported as <see cref="PageRenderProblem.MissingGlyph"/> and drawn as nothing, though the
    /// text still advances past it.
    /// </returns>
    public abstract PagePath GetOutline(int code);

    /// <summary>
    /// How far a glyph advances, for a font whose dictionary does not say: a standard 14 font
    /// written without <c>/Widths</c>. Never asked where the PDF gives a width, because the PDF's
    /// numbers are the ones its producer laid the text out with.
    /// </summary>
    /// <param name="code">As for <see cref="GetOutline"/>.</param>
    /// <param name="advance">The advance in the units of <see cref="GetOutline"/>: 1 is one em.</param>
    /// <returns>
    /// False, which is what this base implementation answers, where the source knows no advance
    /// either. The glyph then advances by nothing.
    /// </returns>
    public virtual bool TryGetAdvance(int code, out double advance)
    {
        advance = 0;
        return false;
    }

    /// <summary>
    /// The glyph a code draws, as a number that every code drawing that glyph shares, or -1 where
    /// the font has no glyph for it. Page rendering keeps one outline for each such number, so
    /// codes that draw one glyph hold one path. This base numbers each code as its own glyph.
    /// </summary>
    internal virtual int GlyphOf(int code) => code;

    /// <summary>The outline of a glyph that <see cref="GlyphOf"/> numbered, as <see cref="GetOutline"/> answers one.</summary>
    internal virtual PagePath OutlineOfGlyph(int glyph) => GetOutline(glyph);
}
