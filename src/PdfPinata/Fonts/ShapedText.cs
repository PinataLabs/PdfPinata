using System;
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Fonts;

/// <summary>
/// What a whole string draws as: one <see cref="ShapedRun"/> per stretch of a single direction, a
/// single script and a single face, in the order they are drawn.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ShapedRun"/> is what a shaper answers for one run, and a run is by definition one
/// direction and one script against one face. A string handed to <c>DrawString</c> is none of
/// those: it may change script half way through, it may change direction, and the face may run out
/// of glyphs part way along - and the three changes do not fall in the same places. This is the
/// join. <see cref="PdfPinata.Text.TextItemizer"/> cuts by direction and script,
/// <see cref="FontFallbackResolution"/> cuts by coverage, and each piece is shaped on its own
/// terms.
/// </para>
/// <para>
/// The segments are in <b>visual order</b>, leftmost first, so a renderer draws them one after
/// another at the advancing pen position and gets bidirectional text right without knowing anything
/// about bidirectional text. Their <see cref="ShapedSegment.Start"/> indices therefore need not
/// ascend.
/// </para>
/// <para>
/// <see cref="Width"/> is in <b>points</b> rather than in design units, unlike
/// <see cref="ShapedRun.Width"/>. Two segments may have been shaped against faces with different
/// units per em, so there is no one em for the whole to be measured in.
/// </para>
/// </remarks>
sealed class ShapedText
{
    /// <summary>
    /// Initializes a new <see cref="ShapedText"/> from segments already in visual order.
    /// </summary>
    internal ShapedText(IReadOnlyList<ShapedSegment> segments)
    {
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));

        double width = 0;
        var glyphs = 0;
        for (var idx = 0; idx < segments.Count; idx++)
        {
            width += segments[idx].Run.WidthAt(segments[idx].Font.Size);
            glyphs += segments[idx].Run.Glyphs.Count;
        }

        Width = width;
        GlyphCount = glyphs;
    }

    /// <summary>
    /// The one-segment case: a string that needed no cutting up, covering <paramref name="length"/>
    /// characters from the start. Much the commonest, and the one worth not building a list for.
    /// </summary>
    internal static ShapedText Of(ShapedRun run, XFont font, int length)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new ShapedText([new ShapedSegment(run, font, 0, length)]);
    }

    /// <summary>The segments, in the order they are drawn.</summary>
    internal IReadOnlyList<ShapedSegment> Segments { get; }

    /// <summary>
    /// How wide the string is, in points: every advance of every segment, each read against the
    /// em of the face its own segment was shaped with. Reordering does not change it - the same
    /// glyphs are drawn whichever order they are drawn in.
    /// </summary>
    internal double Width { get; }

    /// <summary>How many glyphs the string draws as, which is not how many characters it has.</summary>
    internal int GlyphCount { get; }

    /// <summary>
    /// Whether every segment is drawn with the font the caller asked for, which is the case for
    /// every string that needed no fallback - and so for nearly every string.
    /// </summary>
    internal bool IsAllOneFont(XFont font)
    {
        for (var idx = 0; idx < Segments.Count; idx++)
        {
            if (!ReferenceEquals(Segments[idx].Font, font))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"{Segments.Count} segment(s), {GlyphCount} glyph(s), width {Width}pt";
}

/// <summary>
/// One shaped run of a string: its glyphs, the face they are drawn with, and where in the string
/// the characters came from.
/// </summary>
readonly struct ShapedSegment
{
    internal ShapedSegment(ShapedRun run, XFont font, int start, int length)
    {
        Run = run ?? throw new ArgumentNullException(nameof(run));
        Font = font ?? throw new ArgumentNullException(nameof(font));
        Start = start;
        Length = length;
    }

    /// <summary>The glyphs this stretch of the string draws as.</summary>
    internal ShapedRun Run { get; }

    /// <summary>
    /// The face they are drawn with, which is the one the caller asked for unless it had no glyph
    /// for these characters and something registered on
    /// <see cref="GlobalFontSettings.FontFallback"/> did.
    /// </summary>
    internal XFont Font { get; }

    /// <summary>Where the stretch starts, in the order the string was written.</summary>
    internal int Start { get; }

    /// <summary>How many characters of the string it covers.</summary>
    internal int Length { get; }

    /// <summary>
    /// The characters this segment was shaped from, which is what its
    /// <see cref="ShapedGlyph.Cluster"/> indices are indices into.
    /// </summary>
    /// <remarks>
    /// The whole string is returned unsliced when the segment is the whole string, because that is
    /// the common case and a substring of the whole is a copy of it for nothing.
    /// </remarks>
    internal string TextIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Start == 0 && Length == text.Length ? text : text.Substring(Start, Length);
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Start}..{Start + Length - 1}] {Font.FontFamily.Name} {Run}";
}
