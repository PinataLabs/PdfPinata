#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents a graphics path that uses the same notation as GDI+.
/// </summary>
internal class CoreGraphicsPath
{
    // Same values as GDI+ uses.
    const byte PathPointTypeStart = 0;  // move
    const byte PathPointTypeLine = 1;  // line
    const byte PathPointTypeBezier = 3;  // default Bezier (= cubic Bezier)
    const byte PathPointTypePathTypeMask = 0x07;  // type mask (lowest 3 bits).
    const byte PathPointTypeCloseSubpath = 0x80;  // closed flag

    public CoreGraphicsPath()
    { }

    public CoreGraphicsPath(CoreGraphicsPath path)
    {
        _points = new List<XPoint>(path._points);
        _types = new List<byte>(path._types);
    }

    public void MoveOrLineTo(double x, double y)
    {
        // Make a MoveTo if there is no previous subpath or the previous subpath was closed.
        // Otherwise make a LineTo.
        if (_types.Count == 0 || (_types[^1] & PathPointTypeCloseSubpath) == PathPointTypeCloseSubpath)
            MoveTo(x, y);
        else
            LineTo(x, y, false);
    }

    public void MoveTo(double x, double y)
    {
        _points.Add(new XPoint(x, y));
        _types.Add(PathPointTypeStart);
    }

    public void LineTo(double x, double y, bool closeSubpath)
    {
        if (_points.Count > 0 && _points[^1].Equals(new XPoint(x, y)))
            return;

        _points.Add(new XPoint(x, y));
        _types.Add((byte)(PathPointTypeLine | (closeSubpath ? PathPointTypeCloseSubpath : 0)));
    }

    public void BezierTo(double x1, double y1, double x2, double y2, double x3, double y3, bool closeSubpath)
    {
        _points.Add(new XPoint(x1, y1));
        _types.Add(PathPointTypeBezier);
        _points.Add(new XPoint(x2, y2));
        _types.Add(PathPointTypeBezier);
        _points.Add(new XPoint(x3, y3));
        _types.Add((byte)(PathPointTypeBezier | (closeSubpath ? PathPointTypeCloseSubpath : 0)));
    }

    /// <summary>
    /// Adds an arc that fills exactly one quadrant (quarter) of an ellipse.
    /// Just a quick hack to draw rounded rectangles before AddArc is fully implemented.
    /// </summary>
    public void QuadrantArcTo(double x, double y, double width, double height, int quadrant, bool clockwise)
    {
        #pragma warning disable CA1512 // ThrowIfNegative also refuses -0 and NaN, which this comparison lets through to the drawing code.
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        #pragma warning restore CA1512

        var w = Const.κ * width;
        var h = Const.κ * height;
        double x1, y1, x2, y2, x3, y3;
        switch (quadrant)
        {
            case 1:
                if (clockwise)
                {
                    x1 = x + w;
                    y1 = y - height;
                    x2 = x + width;
                    y2 = y - h;
                    x3 = x + width;
                    y3 = y;
                }
                else
                {
                    x1 = x + width;
                    y1 = y - h;
                    x2 = x + w;
                    y2 = y - height;
                    x3 = x;
                    y3 = y - height;
                }
                break;

            case 2:
                if (clockwise)
                {
                    x1 = x - width;
                    y1 = y - h;
                    x2 = x - w;
                    y2 = y - height;
                    x3 = x;
                    y3 = y - height;
                }
                else
                {
                    x1 = x - w;
                    y1 = y - height;
                    x2 = x - width;
                    y2 = y - h;
                    x3 = x - width;
                    y3 = y;
                }
                break;

            case 3:
                if (clockwise)
                {
                    x1 = x - w;
                    y1 = y + height;
                    x2 = x - width;
                    y2 = y + h;
                    x3 = x - width;
                    y3 = y;
                }
                else
                {
                    x1 = x - width;
                    y1 = y + h;
                    x2 = x - w;
                    y2 = y + height;
                    x3 = x;
                    y3 = y + height;
                }
                break;

            case 4:
                if (clockwise)
                {
                    x1 = x + width;
                    y1 = y + h;
                    x2 = x + w;
                    y2 = y + height;
                    x3 = x;
                    y3 = y + height;
                }
                else
                {
                    x1 = x + w;
                    y1 = y + height;
                    x2 = x + width;
                    y2 = y + h;
                    x3 = x + width;
                    y3 = y;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(quadrant));
        }
        BezierTo(x1, y1, x2, y2, x3, y3, false);
    }

    /// <summary>
    /// Closes the current subpath.
    /// </summary>
    public void CloseSubpath()
    {
        var count = _types.Count;
        if (count > 0)
            _types[count - 1] |= PathPointTypeCloseSubpath;
    }

    public void AddArc(double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        var matrix = XMatrix.Identity;
        var points = GeometryHelper.BezierCurveFromArc(x, y, width, height, startAngle, sweepAngle, PathStart.MoveTo1st, ref matrix);
        var count = points.Count;
        Debug.Assert((count + 2) % 3 == 0);

        MoveOrLineTo(points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx += 3)
            BezierTo(points[idx].X, points[idx].Y, points[idx + 1].X, points[idx + 1].Y, points[idx + 2].X, points[idx + 2].Y, false);
    }

    public void AddArc(XPoint point1, XPoint point2, XSize size, double rotationAngle, bool isLargeArg, XSweepDirection sweepDirection)
    {
        var points = GeometryHelper.BezierCurveFromArc(point1, point2, size, rotationAngle, isLargeArg,
            sweepDirection == XSweepDirection.Clockwise, PathStart.MoveTo1st);
        var count = points.Count;
        Debug.Assert((count + 2) % 3 == 0);

        MoveOrLineTo(points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx += 3)
            BezierTo(points[idx].X, points[idx].Y, points[idx + 1].X, points[idx + 1].Y, points[idx + 2].X, points[idx + 2].Y, false);
    }

    /// <summary>
    /// Adds the outline of a pie: the centre, out to where the arc begins, round it, and back.
    /// </summary>
    /// <remarks>
    /// Built the same way <c>XGraphicsPdfRenderer.DrawPie</c> builds one - centre, then the arc
    /// entered with <see cref="PathStart.LineTo1st"/>, then closed - so that a pie collected in a
    /// path and a pie drawn straight to the page are the same shape rather than two shapes that
    /// happen to look alike.
    /// </remarks>
    public void AddPie(double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        var matrix = XMatrix.Identity;
        var points = GeometryHelper.BezierCurveFromArc(x, y, width, height, startAngle,
            sweepAngle, PathStart.MoveTo1st, ref matrix);
        var count = points.Count;
        Debug.Assert((count + 2) % 3 == 0);

        // MoveTo rather than MoveOrLineTo: a pie is a closed shape of its own, like a rectangle or
        // an ellipse, so it starts a figure rather than continuing one. Joining an open figure
        // would run a line from wherever that figure had reached into the pie's centre, and the
        // CloseSubpath below would then close the pair of them as one shape.
        MoveTo(x + width / 2, y + height / 2);
        LineTo(points[0].X, points[0].Y, false);
        for (var idx = 1; idx < count; idx += 3)
            BezierTo(points[idx].X, points[idx].Y, points[idx + 1].X, points[idx + 1].Y, points[idx + 2].X, points[idx + 2].Y, false);

        CloseSubpath();
    }

    /// <summary>
    /// Adds a closed cardinal spline through the points, wrapping from the last back to the first.
    /// </summary>
    /// <remarks>
    /// The difference from <see cref="AddCurve"/> is entirely at the ends: an open curve leaves the
    /// first and last points with no neighbour on one side and clamps them, a closed one takes the
    /// neighbour from the other end of the array, which is what makes the join at the seam as smooth
    /// as every other join. Mirrors <c>XGraphicsPdfRenderer.DrawClosedCurve</c> segment for segment.
    /// </remarks>
    public void AddClosedCurve(XPoint[] points, double tension)
    {
        var count = points.Length;
        if (count < 2)
            throw new ArgumentException("AddClosedCurve requires two or more points.", nameof(points));

        tension /= 3;

        // MoveTo, for the reason given in AddPie: a closed curve is its own figure. AddCurve, just
        // below, keeps MoveOrLineTo because an open curve does continue the figure it is added to.
        MoveTo(points[0].X, points[0].Y);
        if (count == 2)
        {
            ToCurveSegment(points[0], points[0], points[1], points[1], tension);
        }
        else
        {
            ToCurveSegment(points[count - 1], points[0], points[1], points[2], tension);
            for (var idx = 1; idx < count - 2; idx++)
                ToCurveSegment(points[idx - 1], points[idx], points[idx + 1], points[idx + 2], tension);
            ToCurveSegment(points[count - 3], points[count - 2], points[count - 1], points[0], tension);
            ToCurveSegment(points[count - 2], points[count - 1], points[0], points[1], tension);
        }

        CloseSubpath();
    }

    /// <summary>
    /// Appends another path to this one.
    /// </summary>
    /// <param name="path">The path to append. Left untouched.</param>
    /// <param name="connect">
    /// Whether the appended path's first figure continues this path's current figure rather than
    /// starting one of its own. Ignored when there is nothing to continue, or when the current
    /// figure has been closed - a closed figure cannot be reopened.
    /// </param>
    public void AddPath(CoreGraphicsPath path, bool connect)
    {
        ArgumentNullException.ThrowIfNull(path);

        var count = path._points.Count;
        if (count == 0)
            return;

        var canConnect = connect && _types.Count > 0
                                 && (_types[^1] & PathPointTypeCloseSubpath) != PathPointTypeCloseSubpath;

        for (var idx = 0; idx < count; idx++)
        {
            var type = path._types[idx];

            // The only byte that has to change is the first, and only when the figures are being
            // joined: a start point turns into a line to the same place. Every other point keeps
            // its type and its close flag exactly as the source path recorded them.
            if (idx == 0 && canConnect && (type & PathPointTypePathTypeMask) == PathPointTypeStart)
                type = (byte)(PathPointTypeLine | (type & PathPointTypeCloseSubpath));

            _points.Add(path._points[idx]);
            _types.Add(type);
        }
    }

    public void AddCurve(XPoint[] points, double tension)
    {
        var count = points.Length;
        if (count < 2)
            throw new ArgumentException("AddCurve requires two or more points.", nameof(points));

        tension /= 3;
        MoveOrLineTo(points[0].X, points[0].Y);
        if (count == 2)
        {
            //figure.Segments.Add(GeometryHelper.CreateCurveSegment(points[0], points[0], points[1], points[1], tension));
            ToCurveSegment(points[0], points[0], points[1], points[1], tension);
        }
        else
        {
            //figure.Segments.Add(GeometryHelper.CreateCurveSegment(points[0], points[0], points[1], points[2], tension));
            ToCurveSegment(points[0], points[0], points[1], points[2], tension);
            for (var idx = 1; idx < count - 2; idx++)
            {
                //figure.Segments.Add(GeometryHelper.CreateCurveSegment(points[idx - 1], points[idx], points[idx + 1], points[idx + 2], tension));
                ToCurveSegment(points[idx - 1], points[idx], points[idx + 1], points[idx + 2], tension);
            }
            //figure.Segments.Add(GeometryHelper.CreateCurveSegment(points[count - 3], points[count - 2], points[count - 1], points[count - 1], tension));
            ToCurveSegment(points[count - 3], points[count - 2], points[count - 1], points[count - 1], tension);
        }
    }

    ///// <summary>
    ///// Appends a Bézier curve for a cardinal spline through pt1 and pt2.
    ///// </summary>
    //void ToCurveSegment(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3, double tension3, bool closeSubpath)
    //{
    //    BezierTo(
    //        x1 + tension3 * (x2 - x0), y1 + tension3 * (y2 - y0),
    //        x2 - tension3 * (x3 - x1), y2 - tension3 * (y3 - y1),
    //        x2, y2, closeSubpath);
    //}

    void ToCurveSegment(XPoint pt0, XPoint pt1, XPoint pt2, XPoint pt3, double tension3)
    {
        BezierTo(
            pt1.X + tension3 * (pt2.X - pt0.X), pt1.Y + tension3 * (pt2.Y - pt0.Y),
            pt2.X - tension3 * (pt3.X - pt1.X), pt2.Y - tension3 * (pt3.Y - pt1.Y),
            pt2.X, pt2.Y,
            false);
    }

    /// <summary>
    /// Gets the path points in GDI+ style.
    /// </summary>
    public XPoint[] PathPoints => _points.ToArray();

    /// <summary>
    /// Gets the path types in GDI+ style.
    /// </summary>
    public byte[] PathTypes => _types.ToArray();

    readonly List<XPoint> _points = new();
    readonly List<byte> _types = new();
}
