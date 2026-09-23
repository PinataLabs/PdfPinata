using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Text;

namespace PdfPinata.Fonts;

/// <summary>
/// The one place characters become glyphs. Every path in this library that needs to know which
/// glyphs a string draws as - measuring, drawing, outlining - asks here, so that registering an
/// <see cref="ITextShaper"/> changes all of them at once and none of them separately.
/// </summary>
internal static class TextShaping
{
    /// <summary>
    /// Shapes a whole string: cuts it into runs that are each one direction, one script and one
    /// face, shapes each of them on its own terms, and hands them back in the order they are drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what measuring and drawing ask, rather than <see cref="Shape(System.ReadOnlySpan{char},PdfPinata.Drawing.XFont,PdfPinata.Fonts.OpenType.OpenTypeDescriptor,PdfPinata.Fonts.XTextDirection,string,string)"/> directly, because a
    /// string is not a run. Shaping "Hello <c>&#x0645;&#x0631;&#x062D;&#x0628;&#x0627;</c>" as one
    /// piece asks the face to apply one script's rules to two scripts' characters and draws the
    /// second of them backwards; cutting it first is the whole of what the Unicode Bidirectional
    /// Algorithm and script itemisation are for. A face with no Arabic in it then draws four empty
    /// boxes, which is what <see cref="GlobalFontSettings.FontFallback"/> is for, and which cuts
    /// the runs again in places the first two cuts know nothing about.
    /// </para>
    /// <para>
    /// Text made entirely of characters below <c>U+02B0</c> skips itemisation. Every one of them is
    /// Latin or Common and none of them is right to left, so itemisation can only ever answer the
    /// single run the fast path shapes - and taking it means the commonest string in the library
    /// costs no bidirectional resolution, no string copy and no list. Coverage is not skippable in
    /// the same way, because a Latin face may lack a Latin character; it is skipped only when no
    /// fallback is registered, which is then the whole of what registering none buys.
    /// </para>
    /// </remarks>
    internal static ShapedText ShapeText(ReadOnlySpan<char> text, XFont font, OpenTypeDescriptor descriptor,
        BidiParagraphDirection direction = BidiParagraphDirection.Automatic, string language = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(descriptor);

        var itemize = NeedsItemizing(text);
        var fallback = FontFallbackResolution.Enabled;

        if (!itemize && !fallback)
            return ShapedText.Of(Shape(text, font, descriptor, XTextDirection.LeftToRight, null, language),
                font, text.Length);

        // Itemisation reads a string and the bidirectional algorithm allocates several arrays the
        // length of it, so one more copy here is not what this path costs.
        var whole = text.ToString();
        var segments = new List<ShapedSegment>();

        if (itemize)
        {
            foreach (var run in TextItemizer.Itemize(whole, direction))
                AddRun(segments, whole, run.Start, run.Length, run.Direction, run.ScriptCode,
                    font, descriptor, language);
        }
        else
        {
            // One run by construction - see NeedsItemizing - and told nothing about its script,
            // exactly as the fast path above tells the shaper nothing.
            AddRun(segments, whole, 0, whole.Length, XTextDirection.LeftToRight, null,
                font, descriptor, language);
        }

        return new ShapedText(segments);
    }

    /// <summary>
    /// Shapes one itemised run, cutting it again wherever the face has to change, and adds the
    /// pieces to <paramref name="into"/> in the order they are drawn.
    /// </summary>
    private static void AddRun(List<ShapedSegment> into, string whole, int start, int length,
        XTextDirection direction, string script, XFont font, OpenTypeDescriptor descriptor,
        string language)
    {
        if (!FontFallbackResolution.Enabled)
        {
            into.Add(Segment(whole, start, length, direction, script, font, descriptor, language));
            return;
        }

        var end = start + length;
        var first = into.Count;
        var pieceStart = start;
        XFont piece = null;

        for (var idx = start; idx < end; idx++)
        {
            // The trailing half of a surrogate pair is not a character and has no face of its own.
            // Asked separately it would be a lone surrogate, which no cmap covers, and a cut here
            // would put the two halves of one character in two fonts. It goes wherever its leading
            // half went - so the pair is asked about once, at its leading half, as the one code
            // point it spells.
            if (idx > start && char.IsLowSurrogate(whole[idx]) && char.IsHighSurrogate(whole[idx - 1]))
                continue;

            var codePoint = char.IsHighSurrogate(whole[idx]) && idx + 1 < end
                                                             && char.IsLowSurrogate(whole[idx + 1])
                ? char.ConvertToUtf32(whole[idx], whole[idx + 1])
                : whole[idx];

            var wanted = FontFallbackResolution.FontFor(codePoint, font, descriptor);

            // No opinion, or the same opinion as the piece being built: nothing to cut.
            if (wanted == null || ReferenceEquals(wanted, piece))
                continue;

            if (piece == null)
            {
                // Nothing before this had an opinion, so what has gone before belongs to the first
                // face that does have one - a run opening with a space is not a run of spaces.
                piece = wanted;
                continue;
            }

            into.Add(Segment(whole, pieceStart, idx - pieceStart, direction, script,
                piece, DescriptorFor(piece), language));
            pieceStart = idx;
            piece = wanted;
        }

        var last = piece ?? font;
        into.Add(Segment(whole, pieceStart, end - pieceStart, direction, script,
            last, DescriptorFor(last), language));

        // The pieces of a right-to-left run are drawn right to left: the one written first is the
        // rightmost. Reversing them here is the same thing the bidirectional algorithm does to the
        // runs of a paragraph, one level down and for a reason it knows nothing about.
        if (direction == XTextDirection.RightToLeft)
            into.Reverse(first, into.Count - first);
    }

    private static ShapedSegment Segment(string whole, int start, int length, XTextDirection direction,
        string script, XFont font, OpenTypeDescriptor descriptor, string language)
        => new(
            Shape(whole.AsSpan(start, length), font, descriptor, direction, script, language),
            font, start, length);

    /// <summary>
    /// Whether the text could possibly be more than one run.
    /// </summary>
    /// <remarks>
    /// Below <c>U+02B0</c> - Basic Latin through IPA Extensions - every character is script Latin or
    /// script Common, and no character has a right-to-left or Arabic bidirectional class. So the
    /// paragraph level is 0, no rule can raise anything to an odd level, nothing is reordered, and
    /// there is exactly one script. The first character at or above that bound is where the cheap
    /// answer stops being the true one; combining marks and every non-Latin script are above it.
    /// </remarks>
    private static bool NeedsItemizing(ReadOnlySpan<char> text)
    {
        foreach (var ch in text)
        {
            if (ch >= '\u02B0')
                return true;
        }

        return false;
    }

    /// <summary>
    /// Shapes a run against a font, through the registered <see cref="ITextShaper"/> if there is
    /// one and through the unshaped path if there is not.
    /// </summary>
    internal static ShapedRun Shape(ReadOnlySpan<char> text, XFont font,
        XTextDirection direction = XTextDirection.LeftToRight,
        string script = null, string language = null)
        => Shape(text, font, DescriptorFor(font), direction, script, language);

    /// <inheritdoc cref="Shape(System.ReadOnlySpan{char},PdfPinata.Drawing.XFont,PdfPinata.Fonts.XTextDirection,string,string)"/>
    /// <remarks>
    /// The overload for callers that already hold the descriptor - the measuring path measures a
    /// line at a time and would otherwise look it up once per line.
    /// </remarks>
    internal static ShapedRun Shape(ReadOnlySpan<char> text, XFont font, OpenTypeDescriptor descriptor,
        XTextDirection direction = XTextDirection.LeftToRight,
        string script = null, string language = null)
    {
        ArgumentNullException.ThrowIfNull(font);

        var shaper = GlobalFontSettings.TextShaper;
        if (shaper == null)
            return Unshaped(text, descriptor, direction);

        // A shaper that answers nothing is treated as a shaper that declined the run rather than
        // as an error: the unshaped result is always available and always better than throwing
        // from the middle of a page being drawn.
        return shaper.Shape(text, font.ShapingFont, direction, script, language)
            ?? Unshaped(text, descriptor, direction);
    }

    private static OpenTypeDescriptor DescriptorFor(XFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        return FontDescriptorCache.GetOrCreateDescriptorFor(font) as OpenTypeDescriptor
            ?? throw new InvalidOperationException("No OpenTypeDescriptor for the font to shape against.");
    }

    /// <summary>
    /// What this library did before there was a seam, and what it still does when none is
    /// registered: one character, one <c>cmap</c> lookup, one glyph, in the order they were
    /// written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept faithful to the old behaviour down to its limits, because every existing document
    /// depends on them: a symbol face has its code points shifted into the range the face actually
    /// encodes, which is wrong and is not this method's to fix - the fix is a real shaper.
    /// </para>
    /// <para>
    /// One limit is gone. A surrogate pair used to be two lookups on its two halves, neither of
    /// which any <c>cmap</c> maps, so an astral character drew <c>.notdef</c> twice. It is now one
    /// character, one lookup and one glyph, answered out of the face's <c>cmap</c> format 12
    /// subtable where it has one and <c>.notdef</c> - once - where it has not. The cluster stays the
    /// index of the high surrogate, so the pair is recovered whole by <c>/ToUnicode</c>.
    /// </para>
    /// <para>
    /// One thing it does do that no <c>cmap</c> lookup can: a right-to-left run comes back
    /// reversed. <see cref="ShapedRun"/> promises visual order and a renderer relies on it, so this
    /// has to keep the promise however little else it can do. It means a consumer who takes no
    /// shaping dependency at all still gets Arabic and Hebrew in the right order - unjoined, which
    /// is wrong, but no longer also backwards, which was the older and louder complaint.
    /// </para>
    /// </remarks>
    internal static ShapedRun Unshaped(ReadOnlySpan<char> text, XFont font,
        XTextDirection direction = XTextDirection.LeftToRight)
        => Unshaped(text, DescriptorFor(font), direction);

    /// <inheritdoc cref="Unshaped(System.ReadOnlySpan{char},PdfPinata.Drawing.XFont,PdfPinata.Fonts.XTextDirection)"/>
    internal static ShapedRun Unshaped(ReadOnlySpan<char> text, OpenTypeDescriptor descriptor,
        XTextDirection direction = XTextDirection.LeftToRight)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (text.Length == 0)
            return ShapedRun.Empty(descriptor.UnitsPerEm, direction);

        var symbol = descriptor.FontFace.cmap.symbol;
        var symbolBase = descriptor.FontFace.os2.usFirstCharIndex & 0xFF00;

        // A joining control is inside the run because a real shaper has to read it - see
        // BidiResult.Runs - and this is not a real shaper. It draws nothing for one: the control is
        // zero width by definition, so whatever glyph the face happens to map it to is not a glyph
        // to put on the page, and .notdef least of all.
        var drawn = 0;
        for (var idx = 0; idx < text.Length; )
        {
            if (UnicodeProperties.IsJoiningControl(text[idx]))
            {
                idx++;
                continue;
            }

            drawn++;
            idx += CharacterLengthAt(text, idx);
        }

        if (drawn == 0)
            return ShapedRun.Empty(descriptor.UnitsPerEm, direction);

        var rightToLeft = direction == XTextDirection.RightToLeft;
        var glyphs = new ShapedGlyph[drawn];
        for (int idx = 0, position = 0; idx < text.Length; )
        {
            var ch = text[idx];
            if (UnicodeProperties.IsJoiningControl(ch))
            {
                idx++;
                continue;
            }

            var length = CharacterLengthAt(text, idx);
            int codePoint;
            if (length == 2)
            {
                // An astral character. A symbol face never encodes one, so the shift below does not
                // apply and would corrupt the code point if it did.
                codePoint = char.ConvertToUtf32(ch, text[idx + 1]);
            }
            else if (symbol)
            {
                // Used | rather than + because of http://PdfPinata.codeplex.com/workitem/15954.
                codePoint = ch | symbolBase;
            }
            else
            {
                codePoint = ch;
            }

            var glyphIndex = descriptor.CharCodeToGlyphIndex(codePoint);

            // The cluster stays the index of the character it came from; only the position in the
            // list changes, so a right-to-left run's clusters descend exactly as a shaper's do. For
            // a surrogate pair it is the index of the high surrogate, and the next glyph's cluster
            // is two further on, which is how the reader recovers both code units.
            var at = rightToLeft ? drawn - 1 - position : position;
            glyphs[at] = new ShapedGlyph((ushort)glyphIndex, idx, descriptor.GlyphIndexToWidth(glyphIndex));
            position++;
            idx += length;
        }

        return new ShapedRun(glyphs, descriptor.UnitsPerEm, direction);
    }

    /// <summary>
    /// How many <see cref="char"/> the character at <paramref name="index"/> occupies: two for a
    /// well-formed surrogate pair and one for everything else, an unpaired surrogate included.
    /// </summary>
    /// <remarks>
    /// Both loops in <c>Unshaped</c> step through the text with this, and they have to agree - one
    /// counts the glyphs and the other fills them in, so a disagreement is an array of the wrong
    /// size or a hole left in the middle of it.
    /// </remarks>
    private static int CharacterLengthAt(ReadOnlySpan<char> text, int index)
        => char.IsHighSurrogate(text[index])
           && index + 1 < text.Length
           && char.IsLowSurrogate(text[index + 1])
            ? 2
            : 1;

    /// <summary>
    /// The glyph identifiers of a run, one <see cref="char"/> per glyph, in drawing order - the
    /// form <see cref="PdfPinata.Pdf.Internal.PdfEncoders"/> and the content-stream writer want
    /// a Type 0 glyph run in.
    /// </summary>
    /// <remarks>
    /// A glyph identifier is a <see cref="ushort"/> and a <see cref="char"/> is a UTF-16 code unit,
    /// so this string is glyph indices wearing a string's clothes. It is never text and must never
    /// be decoded as any.
    /// </remarks>
    internal static string GlyphIds(ShapedRun run)
    {
        var glyphs = run.Glyphs;
        var ids = new char[glyphs.Count];
        for (var idx = 0; idx < glyphs.Count; idx++)
            ids[idx] = (char)glyphs[idx].GlyphId;

        return new string(ids);
    }

    /// <summary>
    /// The characters of <paramref name="text"/> that the glyph at <paramref name="index"/> stands
    /// for: from its own cluster up to the next one along the text.
    /// </summary>
    /// <remarks>
    /// "The next one along the text" is not "the next one in the list". A right-to-left run is handed
    /// over in visual order, so its clusters descend, and the cluster that follows this one in the
    /// source is found by looking back down the glyphs rather than forward. Several glyphs sharing a
    /// cluster all answer the same characters, which between them is what they drew.
    /// <para>
    /// Two callers, and they must agree about which characters those are.
    /// <see cref="CMapInfo.AddShapedRun"/> spends this on <c>/ToUnicode</c>, so that a ligature
    /// extracts as the characters it swallowed, and <c>XGraphicsPdfRenderer</c> spends it on
    /// <c>/ActualText</c>, so that a reader walking the marked content is told the same thing. Two
    /// implementations would eventually disagree, and the document would then say one thing to a text
    /// extractor and another to a screen reader.
    /// </para>
    /// <para>
    /// They part company over the joining controls U+200C and U+200D, and deliberately.
    /// <c>/ToUnicode</c> keeps them, because extracting the text should hand back what was written and
    /// a joiner that was in the source belongs in the copy. <c>/ActualText</c> drops them, because it
    /// describes what is on the page and nothing on the page stands for a character that is zero width
    /// by definition. So a glyph may extract as two characters and be spoken as one — which is the two
    /// answers being right about different questions, not the two callers drifting apart.
    /// </para>
    /// </remarks>
    internal static string CharactersOf(ShapedRun run, int index, string text)
    {
        var glyphs = run.Glyphs;
        var cluster = glyphs[index].Cluster;
        if (cluster < 0 || cluster >= text.Length)
            return string.Empty;

        var end = text.Length;
        if (run.Direction == XTextDirection.RightToLeft)
        {
            for (var idx = index - 1; idx >= 0; idx--)
            {
                if (glyphs[idx].Cluster > cluster)
                {
                    end = glyphs[idx].Cluster;
                    break;
                }
            }
        }
        else
        {
            for (var idx = index + 1; idx < glyphs.Count; idx++)
            {
                if (glyphs[idx].Cluster > cluster)
                {
                    end = glyphs[idx].Cluster;
                    break;
                }
            }
        }

        end = Math.Min(end, text.Length);
        return end <= cluster ? string.Empty : text.Substring(cluster, end - cluster);
    }
}
