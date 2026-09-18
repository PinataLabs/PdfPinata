using System;
using System.Globalization;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// A run along one axis: where it starts and where it ends.
/// </summary>
/// <remarks>
/// The unit of everything text flow is expressed in. A line's room is a set of these, an obstacle
/// takes some of these away, and the formatter never learns what shape took them - which is what
/// lets a circle be added later without the layout loop knowing a circle exists.
/// <para>
/// A plain <c>readonly struct</c> rather than a record struct, written when the
/// <c>netstandard2.1</c> leg had no <c>IsExternalInit</c> and so could not compile <c>init</c> or
/// positional records. PolySharp now generates it there, so either would compile today.
/// </para>
/// </remarks>
public readonly struct XInterval : IEquatable<XInterval>
{
    /// <summary>
    /// Initializes a run from where it starts to where it ends.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Where it ends before it starts. A run of no width is allowed and means exactly that; one of
    /// negative width is a mistake rather than a measurement, and silently ordering the two would
    /// hide it.
    /// </exception>
    public XInterval(double start, double end)
    {
        // Before the ordering test, because NaN walks straight through it: every comparison
        // against NaN is false, so `end < start` says nothing at all about a NaN end. It would
        // then spread - a NaN width beats no comparison, so such a run is never picked as widest
        // and never rejected either, and the line quietly goes somewhere else.
        if (!double.IsFinite(start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start,
                "An interval has to start at a real coordinate.");
        }

        if (!double.IsFinite(end))
        {
            throw new ArgumentOutOfRangeException(nameof(end), end,
                "An interval has to end at a real coordinate.");
        }

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end,
                "An interval cannot end before it starts.");
        }

        Start = start;
        End = end;
    }

    /// <summary>Where the run begins.</summary>
    public double Start { get; }

    /// <summary>Where the run ends.</summary>
    public double End { get; }

    /// <summary>How far it runs.</summary>
    public double Width => End - Start;

    /// <summary>Whether it covers nothing at all.</summary>
    public bool IsEmpty => End <= Start;

    /// <summary>
    /// The part of this run that also lies in the other, or an empty run where they do not meet.
    /// </summary>
    public XInterval Intersect(XInterval other)
    {
        double start = Math.Max(Start, other.Start);
        double end = Math.Min(End, other.End);
        return end <= start ? new XInterval(start, start) : new XInterval(start, end);
    }

    /// <summary>Whether the two runs share any width at all - touching end to end does not count.</summary>
    public bool Overlaps(XInterval other)
    {
        return Start < other.End && other.Start < End;
    }

    /// <summary>Determines whether the given interval has the same start and end as this one.</summary>
    public bool Equals(XInterval other)
    {
        #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
        return Start.Equals(other.Start) && End.Equals(other.End);
        #pragma warning restore S1244
    }

    /// <summary>Determines whether the given object is an <see cref="XInterval"/> equal to this one.</summary>
    public override bool Equals(object obj)
    {
        return obj is XInterval other && Equals(other);
    }

    /// <summary>Returns a hash code for this interval.</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return (Start.GetHashCode() * 397) ^ End.GetHashCode();
        }
    }

    /// <summary>Determines whether two intervals are equal.</summary>
    public static bool operator ==(XInterval left, XInterval right) => left.Equals(right);

    /// <summary>Determines whether two intervals differ.</summary>
    public static bool operator !=(XInterval left, XInterval right) => !left.Equals(right);

    /// <summary>Returns the interval as <c>[start, end]</c>, in the invariant culture.</summary>
    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", Start, End);
    }
}
