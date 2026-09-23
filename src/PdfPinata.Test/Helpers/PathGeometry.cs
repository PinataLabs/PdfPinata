using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Reads the path geometry back out of a page's content stream, for the tests that care what
///   shape was drawn rather than what colour it came out.
/// </summary>
/// <remarks>
///   A path is not reachable from outside the library once it has been built - CoreGraphicsPath
///   is internal - so what a path holds is observed by drawing it and reading the operators. That
///   is also the only thing about a path that matters to anyone.
/// </remarks>
internal static class PathGeometry
{
    /// <summary>Every point the page's path operators name, in the order they were written.</summary>
    internal static IReadOnlyList<XPoint> PointsOf(PdfPage page)
    {
        var points = new List<XPoint>();

        foreach (var op in Operators(page))
        {
            var operands = ItemsOf(op.Operands);
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.m:
                case OpCodeName.l:
                    if (operands.Count >= 2)
                        points.Add(new XPoint(Number(operands[0]), Number(operands[1])));
                    break;

                case OpCodeName.c:
                    for (var idx = 0; idx + 1 < operands.Count; idx += 2)
                        points.Add(new XPoint(Number(operands[idx]), Number(operands[idx + 1])));
                    break;
            }
        }

        return points;
    }

    /// <summary>
    ///   The box every path point on the page fits inside, in PDF coordinates - measured up the
    ///   page from its foot. Empty when the page draws no path at all.
    /// </summary>
    internal static XRect BoundsOf(PdfPage page)
    {
        var points = PointsOf(page);
        if (points.Count == 0)
            return XRect.Empty;

        var left = points.Min(point => point.X);
        var right = points.Max(point => point.X);
        var bottom = points.Min(point => point.Y);
        var top = points.Max(point => point.Y);

        return new XRect(left, bottom, right - left, top - bottom);
    }

    /// <summary>How many figures the page's paths begin - one move per contour.</summary>
    internal static int FigureCountOf(PdfPage page)
    {
        return Operators(page).Count(op => op.OpCode.OpCodeName == OpCodeName.m);
    }

    private static IEnumerable<COperator> Operators(PdfPage page)
    {
        return ItemsOf(ContentReader.ReadContent(PageContent.Of(page))).OfType<COperator>();
    }

    private static IReadOnlyList<CObject> ItemsOf(CSequence sequence)
    {
        var items = new List<CObject>();
        foreach (var item in sequence)
            items.Add(item);
        return items;
    }

    private static double Number(CObject operand)
    {
        return operand switch
        {
            CInteger integer => integer.Value,
            CReal real => real.Value,
            _ => 0.0
        };
    }
}
