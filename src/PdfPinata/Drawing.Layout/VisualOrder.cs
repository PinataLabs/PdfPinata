using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Text;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// The order a line's units draw in, once bidirectional resolution has turned some of it round.
/// </summary>
/// <remarks>
/// <para>
/// This is the second piece of arithmetic the two layout engines in this solution genuinely share -
/// see <see cref="LineSpans"/> for the first. <see cref="XTextFormatter"/> justifies a line by
/// placing whole blocks of it; <c>PinataLayout.Rendering.ParagraphRenderer</c> draws one show-text
/// operator per leaf. Both have to decide where each of their units belongs on a reordered line, and
/// both learned the same rule separately before this existed: order a unit by the leftmost position
/// any of its characters ends up at, not by its first character, because a right-to-left word's
/// first character is its rightmost. That is also what keeps an English phrase inside a Hebrew
/// sentence in its own order, where reversing the whole line would turn it round.
/// </para>
/// <para>
/// It takes character spans and the bidi result already resolved for them, and answers a
/// permutation - which is exactly what <see cref="LineSpans"/> takes and answers in doubles instead.
/// Neither engine's notion of a unit - a <c>Block</c> in one, a leaf in the other - is known here,
/// and nothing about a font or a page is either.
/// </para>
/// <para>
/// Public because <c>PinataLayout.Rendering</c> is a different assembly and this repository
/// deliberately carries no <c>InternalsVisibleTo</c>, the same reason <see cref="LineSpans"/> is
/// public. Pure and stateless, so being public costs little.
/// </para>
/// </remarks>
public static class VisualOrder
{
    /// <summary>
    /// Orders a line's units by where they end up once <paramref name="resolved"/> has run.
    /// </summary>
    /// <param name="resolved">
    /// The line's text, already resolved by <see cref="BidiAlgorithm"/>. Not resolved again here -
    /// both callers already have to resolve for their own reasons, and resolving twice would be both
    /// slower and a second opinion.
    /// </param>
    /// <param name="spans">
    /// Each unit's characters, as a start and a length into the same text <paramref name="resolved"/>
    /// was resolved from. A unit that contributed no characters - a bookmark, a line break, a run of
    /// nothing but bidirectional controls - is given a span of zero length rather than left out, so
    /// that its position in <paramref name="spans"/> still names which unit it is.
    /// </param>
    /// <returns>
    /// The index into <paramref name="spans"/> of each unit, leftmost first. A unit with no
    /// characters of its own takes the key of the unit before it, so it stays beside whatever it
    /// followed rather than drifting to either end of the line.
    /// </returns>
    /// <remarks>
    /// The early-out belongs to the caller: a line with nothing right-to-left in it does not need
    /// reordering at all, and both engines check that before spans are even built, exactly as
    /// <c>CLAUDE.md</c> asks - nothing here may put work back on that path.
    /// </remarks>
    public static int[] Of(BidiResult resolved, IReadOnlyList<(int Start, int Length)> spans)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(spans);

        // Where each character ended up, which is the inverse of the order the algorithm answers.
        var placed = new int[resolved.Levels.Count];
        for (var idx = 0; idx < placed.Length; idx++)
            placed[idx] = int.MaxValue;
        for (var at = 0; at < resolved.VisualOrder.Count; at++)
            placed[resolved.VisualOrder[at]] = at;

        var keys = new int[spans.Count];
        for (var idx = 0; idx < spans.Count; idx++)
        {
            var leftmost = int.MaxValue;
            for (var ch = spans[idx].Start; ch < spans[idx].Start + spans[idx].Length; ch++)
                leftmost = Math.Min(leftmost, placed[ch]);

            keys[idx] = leftmost == int.MaxValue && idx > 0 ? keys[idx - 1] : leftmost;
        }

        return [..Enumerable.Range(0, spans.Count)
            .OrderBy(idx => keys[idx])];
    }
}
