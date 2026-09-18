using System;
using System.Collections.Generic;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// The room left on a line by the things standing in it.
/// </summary>
/// <remarks>
/// <para>
/// This is the one piece of arithmetic the two layout engines in this solution genuinely share.
/// MigraDoc's <c>ObstructedArea</c> asks it once per line to place a paragraph beside a floating
/// shape; <see cref="XTextFormatter"/> asks it once per line to place text beside a reserved
/// region. Everything else about the two is different - MigraDoc's obstacles are page-absolute and
/// the formatter's are relative to the block, and that difference is real rather than incidental,
/// so gathering the obstacles and deciding which of them stand in a band stays with each engine.
/// </para>
/// <para>
/// It takes and answers in plain doubles for that reason. A signature mentioning
/// <c>Rectangle</c>, <see cref="XRect"/> or <see cref="XUnit"/> would belong to one engine and have
/// to be converted by the other; a pair of coordinates belongs to neither.
/// </para>
/// <para>
/// Public because <c>PinataLayout.Rendering</c> is a different assembly and this repository
/// deliberately carries no <c>InternalsVisibleTo</c>. Pure and stateless, so being public costs
/// little.
/// </para>
/// <para>
/// <b>The sweep itself lives in <see cref="IntervalSet"/> and this is the shorthand for it.</b> It
/// began as its own hand-rolled scan, written before there was an interval type; keeping both would
/// have been two implementations of one idea sitting in one folder, which is the thing this class
/// was extracted to stop. What is left here is the signature MigraDoc calls - doubles in, doubles
/// out - over geometry that is now written once.
/// </para>
/// <para>
/// It allocates where the hand-rolled scan did not: a list of intervals and a set or two, once per
/// line. Deliberate, and cheap against what a page of glyphs costs. If it ever stops being cheap,
/// the specialisation can come back - with a test that it and <see cref="IntervalSet"/> agree,
/// which is what would have made keeping both defensible in the first place.
/// </para>
/// </remarks>
public static class LineSpans
{
    /// <summary>
    /// Finds the widest run of a line that no obstacle stands in.
    /// </summary>
    /// <param name="left">The left edge of the line.</param>
    /// <param name="right">The right edge of the line.</param>
    /// <param name="blocked">
    /// The horizontal spans obstacles take up. May overlap each other, may be given in any order,
    /// and may run outside <paramref name="left"/> and <paramref name="right"/>; all three are
    /// handled. Left as it was found - a span given end-first is read the way round it was plainly
    /// meant rather than refused.
    /// </param>
    /// <param name="tolerance">
    /// How wide a run has to be to count as room. A run no wider than this is treated as no room at
    /// all, which is what keeps a sliver left by two obstacles that all but meet from being offered
    /// to a line.
    /// </param>
    /// <param name="start">Where the widest free run begins. Meaningless unless this returns true.</param>
    /// <param name="width">How wide the widest free run is. Meaningless unless this returns true.</param>
    /// <returns>
    /// False where every part of the line is taken. That is not an error: the caller moves down past
    /// whatever is standing there and asks again lower down.
    /// </returns>
    /// <remarks>
    /// <b>The widest run, not every run.</b> A band with an obstacle in its middle has two free runs
    /// and this answers with the wider, leaving the other empty. Both engines want that today, and
    /// both record it as a decision rather than a limitation: text that hops across a pull quote and
    /// back in the middle of a line is unreadable whatever the geometry says.
    /// <para>
    /// Ties go to the run found first, which is the leftmost, because the comparison is strictly
    /// greater than.
    /// </para>
    /// </remarks>
    public static bool TryWidestFree(double left, double right, List<(double Start, double End)> blocked,
        double tolerance, out double start, out double width)
    {
        start = 0;
        width = 0;

        ArgumentNullException.ThrowIfNull(blocked);

        // A line may have no width - that is an ordinary answer of "no room", and there is a test
        // for it - but it cannot end to the left of where it began. Left unchecked the scan walks
        // a negative line and reports no room, which is the right answer arrived at by accident
        // and hides the caller's mistake.
        // Ahead of the ordering and sign tests below, all of which NaN passes: every comparison
        // against it is false. Named here rather than left to the interval the line is turned
        // into, so the message points at the argument the caller actually passed.
        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            throw new ArgumentOutOfRangeException(!double.IsFinite(left) ? nameof(left) : nameof(right),
                !double.IsFinite(left) ? left : right,
                "A line has to run between real coordinates.");
        }

        if (!double.IsFinite(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                "The tolerance has to be a real width.");
        }

        if (right < left)
        {
            throw new ArgumentOutOfRangeException(nameof(right), right,
                "A line cannot end to the left of where it starts.");
        }

        // A negative tolerance is worse than useless: the test at the end is "wider than the
        // tolerance", so a run of no width at all would pass it and be offered to the caller as
        // room. Zero is allowed and means exactly that - any width at all will do.
        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                "A tolerance narrower than nothing would make a run of no width count as room.");
        }

        var taken = new List<XInterval>(blocked.Count);
        foreach ((double Start, double End) span in blocked)
        {
            // Read the way round it was plainly meant. This never refuses, because it did not
            // refuse before there was an interval type to have an opinion about it.
            taken.Add(span.End < span.Start
                ? new XInterval(span.End, span.Start)
                : new XInterval(span.Start, span.End));
        }

        if (!IntervalSet.Of(left, right).Subtract(taken).TryWidest(tolerance, out XInterval widest))
            return false;

        start = widest.Start;
        width = widest.Width;
        return true;
    }
}
