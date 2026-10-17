using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Fonts;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Measures an <see cref="XGlyphOutline"/> by turning it into polygons, for the tests that compare
///   one glyph outline with another or with the shape it should be.
/// </summary>
/// <remarks>
///   Shared: PdfPinata.ImageSharp4.Tests links this file, for its arc tests and for the linked
///   <c>ImageSharpGlyphOutlineParityTests</c>.
/// </remarks>
internal static class GlyphOutlineGeometry
{
    /// <summary>How many straight pieces each curve is cut into.</summary>
    private const int StepsPerCurve = 32;

    /// <summary>
    ///   Each figure of the outline as a polygon, its curves cut into short straight pieces. Two
    ///   outlines may start a figure at different points, or one may draw the closing line that the
    ///   other leaves to Close; neither changes the polygon's extent or area.
    /// </summary>
    internal static List<List<XPoint>> Flattened(XGlyphOutline outline)
    {
        var figures = new List<List<XPoint>>();
        List<XPoint> figure = null;
        var current = new XPoint();

        foreach (var segment in outline.Segments)
        {
            switch (segment.Kind)
            {
                case XGlyphSegmentKind.Start:
                    figure = [segment.End];
                    figures.Add(figure);
                    current = segment.End;
                    break;
                case XGlyphSegmentKind.Line:
                    figure!.Add(segment.End);
                    current = segment.End;
                    break;
                case XGlyphSegmentKind.Curve:
                    for (var step = 1; step <= StepsPerCurve; step++)
                    {
                        figure!.Add(OnCubic(current, segment.Control1, segment.Control2, segment.End,
                            step / (double)StepsPerCurve));
                    }

                    current = segment.End;
                    break;
            }
        }

        return figures;
    }

    /// <summary>The point at <paramref name="t"/>, from 0 to 1, along a cubic Bézier.</summary>
    internal static XPoint OnCubic(XPoint p0, XPoint p1, XPoint p2, XPoint p3, double t)
    {
        var u = 1 - t;
        return new XPoint(
            u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
    }

    /// <summary>The shoelace formula, over a figure taken as closed.</summary>
    internal static double SignedArea(List<XPoint> figure)
    {
        double sum = 0;
        for (var i = 0; i < figure.Count; i++)
        {
            var a = figure[i];
            var b = figure[(i + 1) % figure.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }

        return sum / 2;
    }
}
