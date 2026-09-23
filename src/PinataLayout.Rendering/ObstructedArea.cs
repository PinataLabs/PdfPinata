using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;

namespace PinataLayout.Rendering;

/// <summary>
/// A rectangle with things standing in it, which text has to be laid out around.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what makes text flow beside a shape. <see cref="Area.GetFittingRect"/> is
/// already asked once per line by <c>ParagraphRenderer</c>, and every other thing the paragraph
/// loop does - breaking lines, justifying them, aligning them, truncating them - is already
/// expressed in terms of what it answers. An area that answers with a narrower rect where an
/// obstacle overlaps therefore makes text flow around that obstacle <b>without the line-breaking
/// loop changing at all</b>.
/// </para>
/// <para>
/// The obstacles are in the same coordinates as the bounds, which are the page's. They are the
/// shape's rectangle grown by the wrap distances, so the text is held off rather than touching.
/// </para>
/// </remarks>
internal class ObstructedArea : Area
{
    /// <summary>
    /// Initializes an area with obstacles standing in it.
    /// </summary>
    /// <param name="bounds">The area as it would be with nothing in it.</param>
    /// <param name="obstacles">
    /// What stands in it. Copied, so that a later change to the caller's list cannot move an
    /// obstacle out from under text that has been laid out around it.
    /// </param>
    internal ObstructedArea(Rectangle bounds, IEnumerable<Rectangle> obstacles)
    {
        _bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        _obstacles = new List<Rectangle>();

        if (obstacles != null)
        {
            foreach (var obstacle in obstacles)
            {
                if (obstacle != null)
                    _obstacles.Add(new Rectangle(obstacle));
            }
        }
    }

    private readonly Rectangle _bounds;
    private readonly List<Rectangle> _obstacles;

    /// <summary>What stands in this area.</summary>
    internal IReadOnlyList<Rectangle> Obstacles => _obstacles;

    /// <summary>
    /// Gets the widest clear rectangle of the given height whose top is at the given position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The widest clear span, not every clear span.</b> A band with an obstacle in its middle
    /// has two clear spans, and this answers with the wider of them, leaving the other empty. That
    /// is a real limitation and the right first answer: the method returns one
    /// <see cref="Rectangle"/>, and changing it to return a set would touch every implementation
    /// and every caller. It is also what a reader expects - text that hops across a pull quote and
    /// back in the middle of a line is unreadable whatever the geometry says.
    /// </para>
    /// <para>
    /// Null where the band runs off the bottom, as for any area, and also where the band is there
    /// but every part of it is taken. That second case is why the null contract had to be settled
    /// before this class existed: for a plain rectangle it never arises.
    /// </para>
    /// </remarks>
    internal override Rectangle GetFittingRect(XUnit yPosition, XUnit height)
    {
        // Off the bottom: the same answer a plain rectangle gives, for the same reason.
        if (yPosition + height > _bounds.Y + _bounds.Height + Renderer.Tolerance)
            return null;

        var blocked = BlockedSpansIn(yPosition, height);

        // Nothing standing in this band, so the whole of it is free. Kept ahead of the scan rather
        // than folded into it: the scan judges a run against the tolerance and would answer null
        // for an area no wider than that, where this answers with the area. The two differ only for
        // an area of no width, which is exactly the sort of case a pinned layout is made of.
        if (blocked.Count == 0)
            return new Rectangle(_bounds.X, yPosition, _bounds.Width, height);

        // Every part of the band is taken. Not an error: the paragraph moves down past whatever is
        // standing here and carries on below it.
        if (!LineSpans.TryWidestFree(_bounds.X, _bounds.X + _bounds.Width, blocked, Renderer.Tolerance,
                out var widestStart, out var widestWidth))
            return null;

        return new Rectangle(widestStart, yPosition, widestWidth, height);
    }

    /// <summary>
    /// The horizontal spans taken up by obstacles that stand in the given band.
    /// </summary>
    /// <remarks>
    /// Overlap is by the band's box rather than by a line within it: a line whose top is inside an
    /// obstacle's depth but whose baseline falls below it still has ascenders that would collide.
    /// </remarks>
    private List<(double Start, double End)> BlockedSpansIn(XUnit yPosition, XUnit height)
    {
        var blocked = new List<(double Start, double End)>();
        double top = yPosition;
        var bottom = yPosition + height;

        foreach (var obstacle in _obstacles)
        {
            double obstacleTop = obstacle.Y;
            var obstacleBottom = obstacle.Y + obstacle.Height;

            if (obstacleBottom <= top + Renderer.Tolerance || obstacleTop >= bottom - Renderer.Tolerance)
                continue;

            blocked.Add((obstacle.X, obstacle.X + obstacle.Width));
        }

        return blocked;
    }

    public override XUnit X
    {
        get => _bounds.X;
        set => _bounds.X = value;
    }

    public override XUnit Y
    {
        get => _bounds.Y;
        set => _bounds.Y = value;
    }

    public override XUnit Width
    {
        get => _bounds.Width;
        set => _bounds.Width = value;
    }

    public override XUnit Height
    {
        get => _bounds.Height;
        set => _bounds.Height = value;
    }

    /// <summary>
    /// Returns the smallest rectangle containing this area and the given one - <b>a rectangle</b>,
    /// with the obstacles dropped.
    /// </summary>
    /// <remarks>
    /// Deliberate, and worth saying so plainly because it looks like an oversight.
    /// <para>
    /// <c>Unite</c> takes bounding boxes already: <c>Rectangle.Unite</c> carries the comment "This
    /// implementation is of course not correct, but it works for our purposes", and every caller
    /// uses it for render-info geometry where a bounding box is exactly what is wanted. An area
    /// with holes in it has no union that is also an area with holes in it, short of keeping both
    /// sets of obstacles and intersecting the results - which no caller wants and none would read
    /// correctly.
    /// </para>
    /// <para>
    /// So uniting with an obstructed area gives a plain rectangle covering both, which is what its
    /// callers already assume they are getting.
    /// </para>
    /// </remarks>
    internal override Area Unite(Area area)
    {
        if (area == null)
            return new Rectangle(_bounds);

        XUnit minTop = Math.Min(_bounds.Y, area.Y);
        XUnit minLeft = Math.Min(_bounds.X, area.X);
        XUnit maxRight = Math.Max(_bounds.X + _bounds.Width, area.X + area.Width);
        XUnit maxBottom = Math.Max(_bounds.Y + _bounds.Height, area.Y + area.Height);
        return new Rectangle(minLeft, minTop, maxRight - minLeft, maxBottom - minTop);
    }

    /// <summary>
    /// Lowers the area and makes it smaller, keeping whatever stands in it where it stands.
    /// </summary>
    /// <remarks>
    /// The obstacles are in page coordinates and do not move when the area's top does. An obstacle
    /// the area has been lowered past simply stops overlapping any band it is asked about.
    /// </remarks>
    internal override Area Lower(XUnit verticalOffset)
    {
        return new ObstructedArea(
            new Rectangle(_bounds.X, _bounds.Y + verticalOffset, _bounds.Width, _bounds.Height - verticalOffset),
            _obstacles);
    }

    /// <summary>
    /// Takes the offset off the bottom, keeping whatever stands in the area where it stands.
    /// </summary>
    /// <remarks>
    /// The obstacles are in page coordinates and are left alone, for the same reason
    /// <see cref="Lower"/> leaves them alone: one that now falls below the area simply stops
    /// overlapping any band it is asked about.
    /// </remarks>
    internal override Area Shorten(XUnit verticalOffset)
    {
        return new ObstructedArea(
            new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height - verticalOffset),
            _obstacles);
    }
}
