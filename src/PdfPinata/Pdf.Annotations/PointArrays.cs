using System;
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Flat arrays of x y pairs - <c>/Vertices</c>, and each stroke of an <c>/InkList</c> - and the box
/// that encloses what is drawn along them.
/// </summary>
internal static class PointArrays
{
    /// <summary>
    /// The points an array holds. A trailing odd number has no partner and is not a point, so it is
    /// ignored rather than read as one with a coordinate that is not there.
    /// </summary>
    public static XPoint[] Read(PdfArray array)
    {
        if (array == null)
            return Array.Empty<XPoint>();

        var points = new XPoint[array.Elements.Count / 2];
        for (var index = 0; index < points.Length; index++)
            points[index] = new XPoint(array.Elements.GetReal(index * 2), array.Elements.GetReal(index * 2 + 1));

        return points;
    }

    /// <summary>
    /// A direct array of the points, x then y for each.
    /// </summary>
    public static PdfArray Write(IEnumerable<XPoint> points)
    {
        var array = new PdfArray();
        foreach (var point in points)
        {
            array.Elements.Add(new PdfReal(point.X));
            array.Elements.Add(new PdfReal(point.Y));
        }

        return array;
    }

    /// <summary>
    /// The box enclosing every point, grown by <paramref name="reach"/> on every side - how far
    /// what is drawn along them spreads beyond them. False when there are no points at all.
    /// </summary>
    public static bool TryEnclose(IEnumerable<XPoint> points, double reach, out PdfRectangle box)
    {
        double x1 = double.MaxValue, y1 = double.MaxValue;
        double x2 = double.MinValue, y2 = double.MinValue;
        var any = false;

        foreach (var point in points)
        {
            any = true;
            x1 = Math.Min(x1, point.X);
            y1 = Math.Min(y1, point.Y);
            x2 = Math.Max(x2, point.X);
            y2 = Math.Max(y2, point.Y);
        }

        box = any ? new PdfRectangle(x1 - reach, y1 - reach, x2 + reach, y2 + reach) : null;
        return any;
    }

    /// <summary>
    /// Maps a point in default user space - y running up from the bottom of the page - into an
    /// appearance drawn over <paramref name="box"/>, whose own origin is its top left with y running
    /// down, as every <see cref="XGraphics"/> surface is. That flip is the whole of the conversion.
    /// </summary>
    public static XPoint IntoForm(XPoint point, PdfRectangle box) =>
        new(point.X - box.X1, box.Y2 - point.Y);
}
