using System.Collections.Generic;

namespace PdfPinata.Fonts;

/// <summary>
/// Turns text into glyph geometry, so that <see cref="PdfPinata.Drawing.XGraphicsPath.AddString(string,PdfPinata.Drawing.XFontFamily,PdfPinata.Drawing.XFontStyle,double,PdfPinata.Drawing.XPoint,PdfPinata.Drawing.XStringFormat)"/>
/// can produce a path that is fillable, clippable and widenable.
/// </summary>
/// <remarks>
/// This is the third seam of the backend split, beside <see cref="GlobalFontSettings.FontResolver"/>
/// and <see cref="PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl"/>,
/// and it exists for the same reason: reading the contours out of a font means a <c>glyf</c>
/// decoder for TrueType and a Type 2 charstring interpreter for PostScript outlines, and the core
/// package carries no font dependency to do either with. Both shipped backends already reference a
/// library that has solved it.
/// <para>
/// An implementation <b>must</b> take its font bytes from <see cref="GlobalFontSettings.FontResolver"/>
/// rather than resolving a family for itself. Two seams that resolve independently will one day
/// disagree about which face a family means, and the path will then not match the text.
/// </para>
/// </remarks>
public interface IGlyphOutlineProvider
{
    /// <summary>
    /// The outlines of the glyphs of <paramref name="text"/>, one per glyph, in the order they are
    /// drawn and each already advanced along the run.
    /// </summary>
    /// <param name="text">The text to outline. An empty string yields no outlines.</param>
    /// <param name="familyName">The font family, resolved through the registered font resolver.</param>
    /// <param name="isBold">Whether a bold face is wanted.</param>
    /// <param name="isItalic">Whether an italic face is wanted.</param>
    /// <param name="emSize">The size in points the outlines are scaled to.</param>
    /// <returns>
    /// One <see cref="XGlyphOutline"/> per glyph, with the origin on the baseline at the start of
    /// the run and y increasing upwards. See <see cref="XGlyphOutline"/> for the coordinate space.
    /// </returns>
    IEnumerable<XGlyphOutline> GetOutlines(string text, string familyName, bool isBold, bool isItalic, double emSize);
}
