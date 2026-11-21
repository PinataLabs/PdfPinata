using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Pdf.Rendering;

/// <summary>Turns a TrueType outline into the path a device is handed.</summary>
internal static class TrueTypeOutlinePaths
{
    /// <summary>
    /// The outline in ems, y upwards, origin on the baseline: what <see cref="PageGlyphSource.GetOutline"/>
    /// answers. Each contour is closed, and each quadratic curve becomes the cubic curve that traces it.
    /// </summary>
    /// <param name="outline">The outline in font units.</param>
    /// <param name="unitsPerEm">The font units in one em.</param>
    internal static PagePath ToPagePath(TrueTypeOutline outline, int unitsPerEm)
    {
        if (outline.IsEmpty)
            return PagePath.Empty;

        var scale = 1.0 / unitsPerEm;
        var builder = new PagePathBuilder();
        var first = 0;
        foreach (var last in outline.ContourEnds)
        {
            AppendContour(builder, outline, first, last, scale);
            first = last + 1;
        }

        return builder.ToPath();
    }

    /// <summary>
    /// One contour. Two control points in a row have an on-curve point implied halfway between
    /// them, and a contour may begin with a control point, in which case it is begun at its last
    /// point if that is on the curve, or else halfway between the two.
    /// </summary>
    private static void AppendContour(PagePathBuilder builder, TrueTypeOutline outline, int first, int last, double scale)
    {
        // A contour of one point or none encloses nothing.
        if (last - first < 1)
            return;

        var points = outline.Points;
        var start = points[first];
        var from = first + 1;
        var to = last;
        double startX, startY;
        if (start.OnCurve)
        {
            (startX, startY) = (start.X, start.Y);
        }
        else if (points[last].OnCurve)
        {
            (startX, startY) = (points[last].X, points[last].Y);
            from = first;
            to = last - 1;
        }
        else
        {
            (startX, startY) = ((start.X + points[last].X) / 2, (start.Y + points[last].Y) / 2);
            from = first;
        }

        builder.MoveTo(startX * scale, startY * scale);
        TrueTypePoint? control = null;
        for (var i = from; i <= to; i++)
        {
            var point = points[i];
            if (point.OnCurve)
            {
                if (control is { } c)
                    builder.QuadraticTo(c.X * scale, c.Y * scale, point.X * scale, point.Y * scale);
                else
                    builder.LineTo(point.X * scale, point.Y * scale);
                control = null;
            }
            else
            {
                if (control is { } c)
                {
                    builder.QuadraticTo(c.X * scale, c.Y * scale, (c.X + point.X) / 2 * scale, (c.Y + point.Y) / 2 * scale);
                }

                control = point;
            }
        }

        if (control is { } closing)
            builder.QuadraticTo(closing.X * scale, closing.Y * scale, startX * scale, startY * scale);
        builder.Close();
    }
}
