using System;
using System.Collections.Generic;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// A block of text and the things standing in it.
/// </summary>
/// <remarks>
/// <para>
/// Container and exclusions kept apart, because they answer different questions and change at
/// different times: the bounds are where the text may go, the obstacles are what has been put there.
/// </para>
/// <para>
/// <b>This is not a shape model.</b> It takes obstacles, not shapes - no wrap style, no floating,
/// no element. Those describe a shape in a document tree, and there is no document tree here;
/// PinataLayout has one and keeps its own.
/// </para>
/// </remarks>
public sealed class TextFlowRegion
{
    private readonly List<IFlowObstacle> _obstacles = [];

    /// <summary>
    /// Initializes a region covering the given bounds with nothing standing in it.
    /// </summary>
    public TextFlowRegion(XRect bounds)
    {
        Bounds = bounds;
    }

    /// <summary>Where text may go, before anything is taken out of it.</summary>
    public XRect Bounds { get; }

    /// <summary>What stands in it.</summary>
    public IList<IFlowObstacle> Obstacles => _obstacles;

    /// <summary>
    /// Adds something for the text to flow around, and answers this region so that several can be
    /// added in one expression.
    /// </summary>
    public TextFlowRegion With(IFlowObstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);

        _obstacles.Add(obstacle);
        return this;
    }

    /// <summary>
    /// The horizontal runs a line occupying the given band may be laid out in.
    /// </summary>
    /// <remarks>
    /// <b>Horizontally only.</b> A band below the bottom of <see cref="Bounds"/> is answered as
    /// though it were inside them, and that is deliberate: an empty answer has to mean <i>something
    /// is standing here, move down past it</i>, because that is what the caller does about it. If it
    /// could also mean <i>there is no more block</i> the caller would have to tell the two apart to
    /// know whether to move down or to stop, and moving down when it should stop is a loop that does
    /// not end. Running out of block is the caller's own business and it already knows how to tell.
    /// </remarks>
    public IntervalSet GetAvailableIntervals(FlowBand band)
    {
        var free = IntervalSet.Of(Bounds.X, Bounds.X + Bounds.Width);
        if (_obstacles.Count == 0)
            return free;

        foreach (var obstacle in _obstacles)
        {
            var taken = obstacle?.GetExcludedIntervals(band);
            if (taken == null || taken.Count == 0)
                continue;

            free = free.Subtract(taken);
            if (free.IsEmpty)
                break;
        }

        return free;
    }

    /// <summary>
    /// How far down a line has to move to get out from under something standing in the given band.
    /// </summary>
    /// <returns>
    /// The nearest depth below the band's top at which the set of things blocking it changes, or
    /// null where nothing stands in the band at all.
    /// </returns>
    /// <remarks>
    /// <b>The nearest foot, not the furthest.</b> Two obstacles can cover a line between them while
    /// neither covers it alone, and the deeper one may go on for a long way after the shallower one
    /// has ended - so moving to the deepest foot would step over bands that do have room in them.
    /// Moving to the nearest foot cannot overshoot: below it the obstacles in the way are a
    /// different set, and the caller asks again.
    /// </remarks>
    public double? NextClearanceBelow(FlowBand band)
    {
        double? nearest = null;

        foreach (var obstacle in _obstacles)
        {
            var taken = obstacle?.GetExcludedIntervals(band);
            if (taken == null || taken.Count == 0)
                continue;

            var bottom = obstacle.Bottom;
            if (bottom <= band.Top)
                continue;

            if (nearest == null || bottom < nearest.Value)
                nearest = bottom;
        }

        return nearest;
    }
}
