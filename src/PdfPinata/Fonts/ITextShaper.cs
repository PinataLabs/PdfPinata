using System;

namespace PdfPinata.Fonts;

/// <summary>
/// Turns a run of characters into the glyphs a font actually draws for it, applying the face's own
/// substitution and positioning rules.
/// </summary>
/// <remarks>
/// <para>
/// This is the fourth seam of the backend split, beside <see cref="GlobalFontSettings.FontResolver"/>,
/// <see cref="PinataLayout.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl"/>
/// and <see cref="GlobalFontSettings.GlyphOutlineProvider"/>, and it exists for the same reason:
/// running the OpenType <c>GSUB</c> and <c>GPOS</c> tables is a shaping engine's worth of work per
/// script, and the core package carries no font dependency to do it with.
/// </para>
/// <para>
/// It is the one seam with a working default. Left unset, this library keeps doing what it has
/// always done - one character, one <c>cmap</c> lookup, one glyph, no reordering - so no existing
/// document changes by omission. What that default cannot do is Arabic, whose letters have initial,
/// medial, final and isolated forms that Unicode does not store, or Devanagari, Thai and Khmer,
/// which reorder. Registering a shaper is what makes those correct.
/// </para>
/// <para>
/// An implementation <b>must</b> shape against the <see cref="ShapingFont"/> it is handed rather
/// than resolving the family for itself. That is the face this library has already picked and will
/// really embed, and the glyph identifiers returned here are indices into it - nothing downstream
/// checks which file they were indices into.
/// </para>
/// </remarks>
public interface ITextShaper
{
    /// <summary>
    /// Shapes one run of text against one face.
    /// </summary>
    /// <param name="text">
    /// The characters to shape. Already itemised: one script, one direction, no line breaks and no
    /// control characters. Shaping an empty span yields an empty run.
    /// </param>
    /// <param name="font">
    /// The face to shape against, already resolved. <see cref="ShapingFont.Bytes"/> holds the
    /// file, and the glyph identifiers returned are indices into it.
    /// </param>
    /// <param name="direction">The direction the run is written in.</param>
    /// <param name="script">
    /// The ISO 15924 code of the run's script, lowercase - <c>"latn"</c>, <c>"arab"</c>,
    /// <c>"deva"</c>. Null leaves the choice to the implementation.
    /// </param>
    /// <param name="language">
    /// The BCP 47 tag of the run's language, or null for none. This library never guesses it: the
    /// caller says or the shaper uses the script default, because guessing is how Turkish dotless
    /// <c>i</c> comes out wrong.
    /// </param>
    /// <returns>
    /// The glyphs in visual order, with advances and offsets in the face's design units, or an
    /// empty run for empty input.
    /// <para>
    /// <b>Null declines the run.</b> The caller then draws it the way it would have been drawn had
    /// no shaper been registered at all - one character, one <c>cmap</c> lookup, one glyph - so a
    /// shaper meeting a face it cannot parse, a script it does not implement, or anything else it
    /// would rather not answer for says so by returning null rather than by throwing. Declining is
    /// not failing: the run is still drawn, just not shaped.
    /// </para>
    /// </returns>
    ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
        string script, string language);
}
