using System;
using System.Collections.Generic;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// A rectangle standing in a block of text, with room kept clear around it.
/// </summary>
/// <remarks>
/// <para>
/// The obstacle nearly every caller wants: an image, a pull quote, a table, a signature block —
/// anything already drawn whose box the text should keep out of.
/// </para>
/// <para>
/// The rectangle is in the same coordinates the block is laid out in, which for
/// <c>XTextFormatter</c> means relative to the layout rectangle and unrotated.
/// </para>
/// </remarks>
public sealed class RectangleObstacle : IFlowObstacle
{
    static readonly IReadOnlyList<XInterval> None = new XInterval[0];

    readonly XInterval[] _taken;
    readonly double _top;
    readonly double _bottom;

    /// <summary>
    /// Initializes an obstacle standing in the given rectangle, with text allowed right up to it.
    /// </summary>
    public RectangleObstacle(XRect bounds)
        : this(bounds, 0)
    {
    }

    /// <summary>
    /// Initializes an obstacle standing in the given rectangle, holding text
    /// <paramref name="padding"/> clear of it on every side.
    /// </summary>
    /// <param name="bounds">The rectangle the obstacle occupies.</param>
    /// <param name="padding">
    /// How far text is held off it. <b>A property of the obstacle and not of the formatter</b>,
    /// because how much air a thing needs around it is a fact about that thing: two obstacles in one
    /// block can want different amounts, and a single setting on the formatter could not say so.
    /// MigraDoc carries four such distances on <c>WrapFormat</c> and they earn their keep; one
    /// distance is enough here, and growing it to four sides later changes this class and nothing
    /// else.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Where the padding is negative. That would shrink the obstacle and let text run over the thing
    /// it was given to avoid, which is the opposite of what padding is for. A caller wanting text
    /// closer should pass a smaller rectangle.
    /// </exception>
    public RectangleObstacle(XRect bounds, double padding)
    {
        if (padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(padding), padding,
                "Negative padding would let text run over the obstacle instead of clear of it.");
        }

        if (double.IsNaN(padding) || double.IsInfinity(padding))
        {
            throw new ArgumentOutOfRangeException(nameof(padding), padding,
                "The padding has to be a real distance.");
        }

        Bounds = bounds;
        Padding = padding;

        // Worked out once. An obstacle is asked about on every line of the block it stands in, and
        // the answer never changes.
        Reserved = new XRect(bounds.X - padding, bounds.Y - padding,
            bounds.Width + 2 * padding, bounds.Height + 2 * padding);

        _top = Reserved.Y;
        _bottom = Reserved.Y + Reserved.Height;
        _taken = Reserved.Width <= 0
            ? new XInterval[0]
            : new[] { new XInterval(Reserved.X, Reserved.X + Reserved.Width) };
    }

    /// <summary>The rectangle the obstacle itself occupies.</summary>
    public XRect Bounds { get; }

    /// <summary>How far text is held clear of it.</summary>
    public double Padding { get; }

    /// <summary>
    /// The rectangle text is actually kept out of: <see cref="Bounds"/> grown by
    /// <see cref="Padding"/> on every side.
    /// </summary>
    public XRect Reserved { get; }

    /// <inheritdoc />
    /// <remarks>The foot of <see cref="Reserved"/>, so a line moved here is clear of the padding too.</remarks>
    public double Bottom => _bottom;

    /// <inheritdoc />
    public IReadOnlyList<XInterval> GetExcludedIntervals(FlowBand band)
    {
        // Judged against the reserved rectangle, so the padding holds a line off vertically as well
        // as horizontally: a line whose box would otherwise clear the obstacle by a hair is pushed
        // past it instead. MigraDoc's DistanceTop and DistanceBottom do the same, and for the same
        // reason - a line that just barely clears an image looks like a mistake.
        return band.Overlaps(_top, _bottom) ? _taken : None;
    }
}
