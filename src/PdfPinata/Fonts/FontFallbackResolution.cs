using System;
using System.Collections.Concurrent;
using PdfPinata.Drawing;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Pdf;
using PdfPinata.Text;

namespace PdfPinata.Fonts;

/// <summary>
/// Works out which face draws a character that the one asked for cannot.
/// </summary>
/// <remarks>
/// <para>
/// The decision, not the policy: <see cref="IFontFallback"/> proposes families and this tries them
/// against the <c>cmap</c> of each, keeping the first that really has the character. A family that
/// does not resolve, or resolves to a face without the glyph, is passed over; if none of them
/// works the original face is kept, so the worst outcome is the <c>.notdef</c> that would have
/// been drawn anyway.
/// </para>
/// <para>
/// <b>Nothing here runs unless a fallback is registered.</b> <see cref="Enabled"/> is read first on
/// every path that could ask, so a consumer who registers none pays neither the coverage lookups
/// nor the dictionaries. A consumer who does registers one pays a dictionary hit per character,
/// because a coverage answer is a walk along the face's <c>cmap</c> segments and is far too dear
/// to repeat for every letter of every word of every page.
/// </para>
/// </remarks>
static class FontFallbackResolution
{
    // What draws a character: null for "nobody has an opinion", the empty string for "the face
    // that was asked for", and otherwise the family to use instead. Keyed on the face rather than
    // on the family name asked for, because two families resolving to one file have one answer
    // between them. A miss is worth remembering as firmly as a hit - it is the answer that costs a
    // walk down the whole candidate list.
    private static ConcurrentDictionary<(string Face, int CodePoint, XFontStyle Style), string> _decided
        = new ConcurrentDictionary<(string, int, XFontStyle), string>();

    // Resolved fonts, so that the same fallback is the same object every time and a caller can
    // tell two adjacent characters want the same face by reference.
    private static ConcurrentDictionary<(string Family, double Size, XFontStyle Style, PdfFontEncoding Encoding), XFont> _fonts
        = new ConcurrentDictionary<(string, double, XFontStyle, PdfFontEncoding), XFont>();

    /// <summary>Whether anything is registered to fall back to.</summary>
    internal static bool Enabled => GlobalFontSettings.FontFallback != null;

    /// <summary>
    /// Throws away what has been worked out so far, because the policy that produced it has been
    /// replaced.
    /// </summary>
    internal static void Forget()
    {
        _decided = new ConcurrentDictionary<(string, int, XFontStyle), string>();
        _fonts = new ConcurrentDictionary<(string, double, XFontStyle, PdfFontEncoding), XFont>();
    }

    /// <summary>
    /// Which font should draw <paramref name="codePoint"/>: the one that was asked for, another
    /// one, or null for "no opinion - put it with its neighbours".
    /// </summary>
    /// <remarks>
    /// Three characters get no opinion, and in all three cases an opinion would do harm rather
    /// than good:
    /// <list type="bullet">
    /// <item><b>White space.</b> A space carries no shape worth choosing a face for, and giving it
    /// one splits the run it sits in - so a sentence of Devanagari with spaces in it would be
    /// drawn as several runs instead of one, losing the shaping across each boundary, for no
    /// visible gain.</item>
    /// <item><b>A non-spacing mark</b>, which belongs to the letter in front of it. Sending a mark
    /// to a different face from its base breaks the attachment the shaper was about to make, and a
    /// mark placed by one font against a letter drawn by another is a worse defect than a missing
    /// mark.</item>
    /// <item><b>A joining control</b>, U+200C and U+200D, which say how the letters on either side
    /// of them join and are read by the face those letters are drawn from. Moving one to a face of
    /// its own would separate the instruction from the letters it is about and split the run
    /// between them, which is the one thing that certainly stops it working.</item>
    /// <item><b>A character nothing offered can draw.</b> Splitting the run there would cost the
    /// shaping across the boundary and buy an identical <c>.notdef</c>.</item>
    /// </list>
    /// </remarks>
    internal static XFont FontFor(int codePoint, XFont requested, OpenTypeDescriptor descriptor)
    {
        var fallback = GlobalFontSettings.FontFallback;
        if (fallback == null || requested == null || descriptor == null)
            return null;

        var key = (descriptor.FontName, codePoint, requested.Style);
        if (!_decided.TryGetValue(key, out var family))
        {
            family = Decide(fallback, codePoint, requested, descriptor);
            _decided[key] = family;
        }

        if (family == null)
            return null;

        if (family.Length == 0)
            return requested;

        // A family that resolved once and has gone since is not worth failing a page over.
        return Resolved(family, requested) ?? requested;
    }

    private static string Decide(IFontFallback fallback, int codePoint, XFont requested,
        OpenTypeDescriptor descriptor)
    {
        // Whitespace and joining controls are asked about as characters because that is what they
        // are: every one of them is inside the basic multilingual plane, and there is no astral
        // code point either test would answer true for.
        if (codePoint <= 0xFFFF
            && (char.IsWhiteSpace((char)codePoint) || UnicodeProperties.IsJoiningControl((char)codePoint)))
        {
            return null;
        }

        if (UnicodeProperties.BidiClassOf(codePoint) == BidiClass.NSM)
            return null;

        if (Covers(descriptor, codePoint))
            return string.Empty;

        var candidates = fallback.FamiliesFor(codePoint,
            (requested.Style & XFontStyle.Bold) != 0,
            (requested.Style & XFontStyle.Italic) != 0);

        if (candidates == null)
            return null;

        foreach (var family in candidates)
        {
            if (string.IsNullOrWhiteSpace(family))
                continue;

            var candidate = Resolved(family, requested);
            if (candidate == null)
                continue;

            if (FontDescriptorCache.GetOrCreateDescriptorFor(candidate) is OpenTypeDescriptor other
                && Covers(other, codePoint))
            {
                return family;
            }
        }

        return null;
    }

    /// <summary>
    /// The same font at the same size, style and encoding, in another family - or null if that
    /// family cannot be resolved at all.
    /// </summary>
    /// <remarks>
    /// A family nobody can resolve throws from the <see cref="XFont"/> constructor, which is the
    /// right answer to a caller who named a font and meant it and the wrong answer to a list of
    /// things to try. Caught here and read as "not this one".
    /// </remarks>
    private static XFont Resolved(string family, XFont requested)
    {
        var key = (family, requested.Size, requested.Style, requested.PdfOptions.FontEncoding);
        if (_fonts.TryGetValue(key, out var known))
            return known;

        XFont made;
        try
        {
            made = new XFont(family, requested.Size, requested.Style, requested.PdfOptions);
        }
        catch (InvalidOperationException)
        {
            made = null;
        }

        _fonts[key] = made;
        return made;
    }

    /// <summary>Whether a face has a glyph of its own for a code point.</summary>
    /// <remarks>
    /// An astral code point is answered out of the face's <c>cmap</c> format 12 subtable, and a
    /// face without one answers no - which is the honest answer and the one that sends the
    /// character on to the next candidate, where before there was no question that could be asked.
    /// </remarks>
    private static bool Covers(OpenTypeDescriptor descriptor, int codePoint)
    {
        try
        {
            // Glyph 0 is .notdef, which is what a face answers for a character it does not have.
            return descriptor.CharCodeToGlyphIndex(codePoint) != 0;
        }
        catch (Exception)
        {
            // A cmap this reader cannot walk leaves coverage unknown, and unknown has to read as
            // "yes": falling back off a face that can in fact draw the character would replace
            // correct output with different output.
            return true;
        }
    }
}
