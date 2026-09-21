using System;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Reading and writing a <c>/QuadPoints</c> array, which the text markup annotations and
/// <see cref="PdfRedactAnnotation"/> keep the regions they cover in.
/// </summary>
internal static class QuadPoints
{
    /// <summary>
    /// The quadrilaterals an array holds, each as the rectangle that encloses it.
    /// </summary>
    /// <remarks>
    /// Whole quads only: a trailing fragment is not a quadrilateral and is ignored rather than read
    /// as one with coordinates that are not there.
    /// </remarks>
    public static List<PdfRectangle> Read(PdfArray array)
    {
        var quads = new List<PdfRectangle>();
        if (array == null)
            return quads;

        for (var idx = 0; idx + 7 < array.Elements.Count; idx += 8)
        {
            // Written in the order every producer uses; see Append.
            var left = array.Elements.GetReal(idx);
            var top = array.Elements.GetReal(idx + 1);
            var right = array.Elements.GetReal(idx + 2);
            var bottom = array.Elements.GetReal(idx + 5);
            quads.Add(new PdfRectangle(Math.Min(left, right), Math.Min(top, bottom),
                Math.Max(left, right), Math.Max(top, bottom)));
        }

        return quads;
    }

    /// <summary>
    /// Adds a rectangle to an array as the quadrilateral with the same corners.
    /// </summary>
    public static void Append(PdfArray array, PdfRectangle rect)
    {
        double left = Math.Min(rect.X1, rect.X2), right = Math.Max(rect.X1, rect.X2);
        double bottom = Math.Min(rect.Y1, rect.Y2), top = Math.Max(rect.Y1, rect.Y2);

        // Upper-left, upper-right, lower-left, lower-right. The specification's prose calls for
        // the four vertices "in counterclockwise order", which would put the lower two the other
        // way round, but no producer writes them that way and viewers read this order instead.
        foreach (var value in new[] { left, top, right, top, left, bottom, right, bottom })
            array.Elements.Add(new PdfReal(value));
    }

    /// <summary>
    /// The box enclosing every quadrilateral, or null when there are none.
    /// </summary>
    public static PdfRectangle Enclosing(IReadOnlyList<PdfRectangle> quads)
    {
        if (quads.Count == 0)
            return null;

        double x1 = double.MaxValue, y1 = double.MaxValue;
        double x2 = double.MinValue, y2 = double.MinValue;
        foreach (var quad in quads)
        {
            x1 = Math.Min(x1, quad.X1);
            y1 = Math.Min(y1, quad.Y1);
            x2 = Math.Max(x2, quad.X2);
            y2 = Math.Max(y2, quad.Y2);
        }

        return new PdfRectangle(x1, y1, x2, y2);
    }
}
