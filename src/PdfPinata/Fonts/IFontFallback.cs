using System.Collections.Generic;

namespace PdfPinata.Fonts;

/// <summary>
/// Says which font families to try for a character the chosen face has no glyph for.
/// </summary>
/// <remarks>
/// <para>
/// Registered on <see cref="GlobalFontSettings.FontFallback"/>, and optional: with none registered
/// a character the face cannot draw is <c>.notdef</c> - an empty box, or nothing - exactly as it
/// has always been.
/// </para>
/// <para>
/// It is a seam of its own rather than a member of <see cref="IFontResolver"/> for two reasons. A
/// new member on that interface would break every consumer who has written a resolver, and there
/// is no way to add one that netstandard2.1 and Unity's runtime would both accept. And the two
/// answer different questions: a resolver is asked "which file is this family", which every
/// resolver must know, where this is asked "who else could draw this character", which is a
/// judgement about the machine's whole font collection. A resolver may implement both, and if it
/// does it is used automatically - see <see cref="GlobalFontSettings.FontFallback"/>.
/// </para>
/// <para>
/// The library decides which characters need asking about and whether a candidate really covers
/// one; an implementation only proposes. Proposing a family that does not exist, or does not
/// contain the character, costs a resolution and is otherwise harmless - the next candidate is
/// tried and the original face is kept if none of them works.
/// </para>
/// </remarks>
public interface IFontFallback
{
    /// <summary>
    /// The families to try, in order of preference, for a character the requested face cannot
    /// draw. An empty sequence means there is nothing to try.
    /// </summary>
    /// <param name="codePoint">
    /// The Unicode code point with no glyph - a code point rather than a <see cref="char"/>, so
    /// that a character above the basic multilingual plane can be asked about as the one character
    /// it is rather than as the two surrogates it is spelled with. Neither surrogate is a character
    /// and no <c>cmap</c> maps one, so asking about them separately could only ever be answered
    /// "nobody".
    /// </param>
    /// <param name="isBold">Whether a bold face was asked for.</param>
    /// <param name="isItalic">Whether an italic face was asked for.</param>
    IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic);
}
