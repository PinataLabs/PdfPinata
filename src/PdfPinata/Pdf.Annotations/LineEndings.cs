using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// The line endings of ISO 32000-1 Table 176 - reading them out of an <c>/LE</c> array, writing
/// them into one, and drawing them - shared by the two annotations that end in them,
/// <see cref="PdfLineAnnotation"/> and <see cref="PdfPolyLineAnnotation"/>.
/// </summary>
internal static class LineEndings
{
    /// <summary>
    /// The length an arrowhead runs back along the line, and the width of every other ending.
    /// </summary>
    /// <remarks>
    /// Scaled from the line's own width, floored at one point so that a hairline still gets a
    /// visible head rather than one four hundredths of a point across.
    /// </remarks>
    public static double Size(double lineWidth) => Math.Max(lineWidth, 1) * 4;

    /// <summary>
    /// The ending named at one position of an <c>/LE</c> array, or <see cref="PdfLineEnding.None"/>
    /// when the array says nothing there or names something Table 176 does not have.
    /// </summary>
    public static PdfLineEnding Read(PdfArray endings, int index)
    {
        if (endings == null || endings.Elements.Count <= index)
            return PdfLineEnding.None;

        var name = endings.Elements.GetName(index);
        if (name.Length > 0 && name[0] == '/')
            name = name[1..];

        return Enum.IsDefined(typeof(PdfLineEnding), name)
            ? Enum.Parse<PdfLineEnding>(name, false)
            : PdfLineEnding.None;
    }

    /// <summary>
    /// An <c>/LE</c> array naming both ends, <see cref="PdfLineEnding.None"/> included - a line
    /// saying it ends in nothing is a line, where one saying nothing may be finished however a
    /// reader likes.
    /// </summary>
    public static PdfArray Write(PdfDocument owner, PdfLineEnding start, PdfLineEnding end) =>
        new PdfArray(owner, new PdfName("/" + start), new PdfName("/" + end));

    /// <summary>
    /// The unit vector from one point towards another, or the x axis when the two coincide.
    /// </summary>
    public static XVector Direction(XPoint from, XPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        return length == 0 ? new XVector(1, 0) : new XVector(dx / length, dy / length);
    }

    /// <summary>
    /// Draws one ending at <paramref name="at"/>, pointing along <paramref name="outward"/> - away
    /// from the rest of the line, which is what makes an arrow at the far end point forwards and
    /// one at the near end point back.
    /// </summary>
    public static void Draw(XGraphics gfx, PdfLineEnding ending, XPoint at, XVector outward,
        XPen pen, XBrush brush, double size)
    {
        if (ending == PdfLineEnding.None)
            return;

        var half = size / 2;

        // Reversed arrowheads are the same triangle turned round, which is the only thing the
        // R-prefixed members of Table 176 change.
        if (ending == PdfLineEnding.ROpenArrow || ending == PdfLineEnding.RClosedArrow)
            outward = new XVector(-outward.X, -outward.Y);

        var across = new XVector(-outward.Y, outward.X);

        switch (ending)
        {
            case PdfLineEnding.Square:
                DrawClosed(gfx, pen, brush, new[]
                {
                    new XPoint(at.X - half, at.Y - half), new XPoint(at.X + half, at.Y - half),
                    new XPoint(at.X + half, at.Y + half), new XPoint(at.X - half, at.Y + half)
                });
                break;

            case PdfLineEnding.Circle:
                var circle = new XRect(at.X - half, at.Y - half, size, size);
                if (brush == null)
                    gfx.DrawEllipse(pen, circle);
                else
                    gfx.DrawEllipse(pen, brush, circle);
                break;

            case PdfLineEnding.Diamond:
                DrawClosed(gfx, pen, brush, new[]
                {
                    new XPoint(at.X, at.Y - half), new XPoint(at.X + half, at.Y),
                    new XPoint(at.X, at.Y + half), new XPoint(at.X - half, at.Y)
                });
                break;

            case PdfLineEnding.OpenArrow:
            case PdfLineEnding.ROpenArrow:
                // Two segments meeting at the tip, drawn as one polyline so that the join is
                // mitred rather than two strokes crossing at a point.
                gfx.DrawLines(pen, new[] { Barb(at, outward, across, size, half, 1), at, Barb(at, outward, across, size, half, -1) });
                break;

            case PdfLineEnding.ClosedArrow:
            case PdfLineEnding.RClosedArrow:
                DrawClosed(gfx, pen, brush, new[]
                {
                    at, Barb(at, outward, across, size, half, 1), Barb(at, outward, across, size, half, -1)
                });
                break;

            case PdfLineEnding.Butt:
                gfx.DrawLine(pen,
                    new XPoint(at.X - across.X * half, at.Y - across.Y * half),
                    new XPoint(at.X + across.X * half, at.Y + across.Y * half));
                break;

            case PdfLineEnding.Slash:
                // "Approximately thirty degrees clockwise from perpendicular", which is what the
                // specification asks for and how precisely it asks for it.
                var cos = Math.Cos(Math.PI / 6);
                var sin = Math.Sin(Math.PI / 6);
                var slash = new XVector(
                    across.X * cos - across.Y * sin,
                    across.X * sin + across.Y * cos);
                gfx.DrawLine(pen,
                    new XPoint(at.X - slash.X * half, at.Y - slash.Y * half),
                    new XPoint(at.X + slash.X * half, at.Y + slash.Y * half));
                break;
        }
    }

    /// <summary>
    /// One of the two back corners of an arrowhead whose tip is at <paramref name="at"/>.
    /// </summary>
    static XPoint Barb(XPoint at, XVector outward, XVector across, double size, double half, int side)
    {
        return new XPoint(
            at.X - outward.X * size + across.X * half * side,
            at.Y - outward.Y * size + across.Y * half * side);
    }

    /// <summary>
    /// Fills a closed shape when there is an interior colour and outlines it either way, which is
    /// what an absent <c>/IC</c> means: the ending is drawn, and is not filled in.
    /// </summary>
    static void DrawClosed(XGraphics gfx, XPen pen, XBrush brush, XPoint[] points)
    {
        if (brush == null)
            gfx.DrawPolygon(pen, points);
        else
            gfx.DrawPolygon(pen, brush, points, XFillMode.Winding);
    }
}
