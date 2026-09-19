using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// A set of runs along one axis, kept in order, never overlapping and never touching.
/// </summary>
/// <remarks>
/// <para>
/// What a line's room is made of. The bounds of a block start as one run; every obstacle takes some
/// of it away; what is left is what text can be put in.
/// </para>
/// <para>
/// <b>A set rather than one run, and that is a deliberate cost.</b> Neither library read before
/// building this does it: iText7 coalesces every left float into one boundary and every right one
/// into another and clips each line to the space between, so it can never describe two runs;
/// QuestPDF hands a single width to Skia's shaper and has no per-line seam to describe anything
/// with. Both would have been cheaper. The set is chosen because it makes taking the widest run a
/// decision the <em>layout loop</em> makes rather than a property of the type - so an obstacle with
/// a gap in it, a circle, a path, is a new implementation of
/// <see cref="IFlowObstacle"/> and not a redesign of everything that touches geometry.
/// </para>
/// <para>
/// Immutable: <see cref="Subtract"/> answers with a new set. A line's room is worked out once and
/// then read by several things, and a set that could be edited underneath them is a bug waiting for
/// a reason.
/// </para>
/// </remarks>
public sealed class IntervalSet : IReadOnlyList<XInterval>
{
    static readonly XInterval[] Nothing = Array.Empty<XInterval>();

    readonly XInterval[] _intervals;

    IntervalSet(XInterval[] normalised)
    {
        _intervals = normalised;
    }

    /// <summary>A set covering nothing.</summary>
    public static IntervalSet Empty { get; } = new IntervalSet(Nothing);

    /// <summary>A set covering one run, from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static IntervalSet Of(double start, double end)
    {
        return Of(new XInterval(start, end));
    }

    /// <summary>
    /// A set covering the given runs, put in order and merged where they meet or overlap.
    /// </summary>
    public static IntervalSet Of(params XInterval[] intervals)
    {
        return Of((IEnumerable<XInterval>)intervals);
    }

    /// <summary>
    /// A set covering the given runs, put in order and merged where they meet or overlap.
    /// </summary>
    public static IntervalSet Of(IEnumerable<XInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        XInterval[] normalised = Normalise(intervals);
        return normalised.Length == 0 ? Empty : new IntervalSet(normalised);
    }

    /// <summary>How many runs the set is made of.</summary>
    public int Count => _intervals.Length;

    /// <summary>The runs, left to right.</summary>
    public XInterval this[int index] => _intervals[index];

    /// <summary>Whether there is no room in the set at all.</summary>
    public bool IsEmpty => _intervals.Length == 0;

    /// <summary>
    /// Takes the given runs out of this set and answers with what is left.
    /// </summary>
    /// <remarks>
    /// The runs taken out may overlap each other, may be given in any order, and may lie wholly or
    /// partly outside this set - all three happen with real obstacles and none of them is an error.
    /// </remarks>
    public IntervalSet Subtract(IEnumerable<XInterval> excluded)
    {
        ArgumentNullException.ThrowIfNull(excluded);

        XInterval[] cuts = Normalise(excluded);
        if (cuts.Length == 0 || _intervals.Length == 0)
            return this;

        var kept = new List<XInterval>();

        foreach (XInterval free in _intervals)
        {
            double cursor = free.Start;

            foreach (XInterval cut in cuts)
            {
                // Wholly to the left of where we have got to, or wholly to the right of this run.
                // The cuts are in order, so once one starts past the run's end so does every one
                // after it.
                if (cut.End <= cursor)
                    continue;
                if (cut.Start >= free.End)
                    break;

                if (cut.Start > cursor)
                    kept.Add(new XInterval(cursor, Math.Min(cut.Start, free.End)));

                cursor = Math.Max(cursor, cut.End);
                if (cursor >= free.End)
                    break;
            }

            if (cursor < free.End)
                kept.Add(new XInterval(cursor, free.End));
        }

        // Already in order and already disjoint: the pieces of one run are separated by the cuts
        // that made them, and the runs themselves were separated to begin with. Nothing to merge.
        return kept.Count == 0 ? Empty : new IntervalSet(kept.ToArray());
    }

    /// <summary>
    /// Keeps only the parts of this set that lie inside the given run.
    /// </summary>
    /// <remarks>
    /// What clips a block's room to the column a line actually sits in. An obstacle straddling a
    /// gutter then becomes an ordinary reduction of each column it reaches into, with nothing to
    /// say about the gutter itself, because no text was ever going there.
    /// </remarks>
    public IntervalSet Intersect(XInterval bounds)
    {
        if (_intervals.Length == 0 || bounds.IsEmpty)
            return Empty;

        var kept = new List<XInterval>(_intervals.Length);
        foreach (XInterval interval in _intervals)
        {
            XInterval part = interval.Intersect(bounds);
            if (!part.IsEmpty)
                kept.Add(part);
        }

        // Already in order and already disjoint: clipping cannot reorder runs or make two meet.
        return kept.Count == 0 ? Empty : new IntervalSet(kept.ToArray());
    }

    /// <summary>
    /// Finds the widest run in the set, where any run is wide enough to be worth having.
    /// </summary>
    /// <param name="tolerance">
    /// How wide a run has to be to count as room. A run no wider than this is treated as no room at
    /// all, which is what keeps a sliver left by two obstacles that all but meet from being offered
    /// to a line.
    /// </param>
    /// <param name="widest">The widest run. Meaningless unless this returns true.</param>
    /// <returns>False where nothing in the set is wide enough, including where the set is empty.</returns>
    /// <remarks>
    /// <b>This is where taking one run rather than filling several is decided</b>, and it is a
    /// method on the set rather than a shape of it. The set still holds every run, so a caller that
    /// learns to fill more than one has them.
    /// <para>
    /// Ties go to the leftmost, because the comparison is strictly greater than and the runs are in
    /// order.
    /// </para>
    /// </remarks>
    public bool TryWidest(double tolerance, out XInterval widest)
    {
        // NaN first, because it passes the negative test below and then does real damage: it seeds
        // the running widest, and nothing compares less than or equal to NaN, so the first run
        // examined is taken however narrow it is. The tolerance stops applying altogether.
        if (!double.IsFinite(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                "The tolerance has to be a real width.");
        }

        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                "A tolerance narrower than nothing would make a run of no width count as room.");
        }

        widest = default;
        double widestSoFar = tolerance;
        bool found = false;

        foreach (XInterval interval in _intervals)
        {
            if (interval.Width <= widestSoFar)
                continue;

            widestSoFar = interval.Width;
            widest = interval;
            found = true;
        }

        return found;
    }

    /// <summary>Returns an enumerator over the intervals, in ascending order.</summary>
    public IEnumerator<XInterval> GetEnumerator()
    {
        return ((IEnumerable<XInterval>)_intervals).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the intervals in braces, or <c>{}</c> when there are none.</summary>
    public override string ToString()
    {
        return _intervals.Length == 0 ? "{}" : "{" + string.Join(", ", _intervals) + "}";
    }

    /// <summary>
    /// Puts runs in order, drops the ones covering nothing, and merges any that overlap or meet.
    /// </summary>
    static XInterval[] Normalise(IEnumerable<XInterval> intervals)
    {
        var ordered = new List<XInterval>();
        foreach (XInterval interval in intervals)
        {
            if (!interval.IsEmpty)
                ordered.Add(interval);
        }

        if (ordered.Count == 0)
            return Nothing;

        ordered.Sort((first, second) => first.Start.CompareTo(second.Start));

        var merged = new List<XInterval>(ordered.Count);
        double start = ordered[0].Start;
        double end = ordered[0].End;

        for (int idx = 1; idx < ordered.Count; idx++)
        {
            XInterval next = ordered[idx];

            // Touching counts as merging: two runs meeting end to end are one run, and leaving them
            // apart would offer a caller two narrow spans where there is one wide one.
            if (next.Start <= end)
            {
                // Max, not assignment: sorted by where they start, so a wide run can still end
                // further right than the one after it.
                end = Math.Max(end, next.End);
                continue;
            }

            merged.Add(new XInterval(start, end));
            start = next.Start;
            end = next.End;
        }

        merged.Add(new XInterval(start, end));
        return merged.ToArray();
    }
}
