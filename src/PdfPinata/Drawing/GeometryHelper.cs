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
using System.Diagnostics;
using System.Collections.Generic;
using PdfPinata.Internal;

// ReSharper disable RedundantNameQualifier
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace PdfPinata.Drawing;

/// <summary>
/// Helper class for Geometry paths.
/// </summary>
internal static class GeometryHelper
{
    /// <summary>
    /// Creates between 1 and 5 Béziers curves from parameters specified like in GDI+.
    /// </summary>
    public static List<XPoint> BezierCurveFromArc(double x, double y, double width, double height, double startAngle, double sweepAngle,
        PathStart pathStart, ref XMatrix matrix)
    {
        var points = new List<XPoint>();
        foreach (var segment in new ArcSegments(startAngle, sweepAngle, pathStart))
            AppendPartialArcQuadrant(points, x, y, width, height, segment.From, segment.To, segment.PathStart, matrix);
        return points;
    }

    /// <summary>
    /// One piece of an arc, lying within a single quadrant, with its angles in degrees.
    /// </summary>
    internal readonly struct ArcSegment
    {
        public ArcSegment(double from, double to, PathStart pathStart)
        {
            From = from;
            To = to;
            PathStart = pathStart;
        }

        /// <summary>The angle the piece starts at.</summary>
        public double From { get; }

        /// <summary>The angle the piece ends at.</summary>
        public double To { get; }

        /// <summary>
        /// How the piece joins what came before it: the first piece as the caller asked, every later
        /// one continuing from where the previous one ended.
        /// </summary>
        public PathStart PathStart { get; }
    }

    /// <summary>
    /// Cuts an arc specified like in GDI+ into the between 1 and 5 pieces a Bézier curve is drawn
    /// for, one per quadrant the arc passes through. The same walk serves both the path geometry and
    /// the renderer's content stream, so the two cannot cut an arc differently.
    /// </summary>
    /// <remarks>
    /// A struct enumerator, so a <c>foreach</c> over it allocates nothing. It hands out one
    /// <see cref="ArcSegment"/> per step, in drawing order.
    /// </remarks>
    internal struct ArcSegments
    {
        public ArcSegments(double startAngle, double sweepAngle, PathStart pathStart)
        {
            var sweep = ClampSweep(sweepAngle);
            _start = NormalizeStartAngle(startAngle, sweep);

            // Is it possible that the arc is small starts and ends in same quadrant?
            _smallAngle = Math.Abs(sweep) <= 90;

            _end = _start + sweep;
            if (_end < 0)
                _end += (1 + Math.Floor(Math.Abs(_end) / 360)) * 360;

            _clockwise = sweepAngle > 0;
            var startQuadrant = Quadrant(_start, true, _clockwise);
            _endQuadrant = Quadrant(_end, false, _clockwise);
            _withinOneQuadrant = startQuadrant == _endQuadrant && _smallAngle;
            _pathStart = pathStart;

            _quadrant = startQuadrant;
            _first = true;
            _finished = false;
            Current = default;
        }

        private readonly double _start;
        private readonly double _end;
        private readonly bool _clockwise;
        private readonly int _endQuadrant;
        private readonly bool _withinOneQuadrant;
        private readonly PathStart _pathStart;
        private int _quadrant;
        private bool _first;
        private bool _smallAngle;
        private bool _finished;

        public readonly ArcSegments GetEnumerator() => this;

        public ArcSegment Current { get; private set; }

        public bool MoveNext()
        {
            if (_finished)
                return false;

            if (_withinOneQuadrant)
            {
                Current = new ArcSegment(_start, _end, _pathStart);
                _finished = true;
                return true;
            }

            Current = SegmentIn(_quadrant);

            // Don't stop immediately if arc is greater than 270 degrees.
            if (_quadrant == _endQuadrant && _smallAngle)
            {
                _finished = true;
                return true;
            }

            _smallAngle = true;
            _quadrant = NextQuadrant(_quadrant);
            _first = false;
            return true;
        }

        /// <summary>
        /// The piece of the arc lying in the quadrant: from where the arc starts to the quadrant's
        /// far edge in the first, from its near edge to where the arc ends in the last, and the
        /// whole quadrant in between.
        /// </summary>
        private readonly ArcSegment SegmentIn(int quadrant)
        {
            if (_first)
                return new ArcSegment(_start, FarEdge(quadrant), _pathStart);

            if (quadrant == _endQuadrant)
                return new ArcSegment(NearEdge(quadrant), _end, PathStart.Ignore1st);

            return new ArcSegment(NearEdge(quadrant), FarEdge(quadrant), PathStart.Ignore1st);
        }

        /// <summary>The edge of the quadrant the arc enters it by.</summary>
        private readonly double NearEdge(int quadrant) => quadrant * 90 + (_clockwise ? 0 : 90);

        /// <summary>The edge of the quadrant the arc leaves it by.</summary>
        private readonly double FarEdge(int quadrant) => quadrant * 90 + (_clockwise ? 90 : 0);

        private readonly int NextQuadrant(int quadrant)
        {
            if (_clockwise)
                return quadrant == 3 ? 0 : quadrant + 1;
            return quadrant == 0 ? 3 : quadrant - 1;
        }

        /// <summary>
        /// The start angle brought into the range 0 through 360. Of the two ends of that range,
        /// an arc turning backwards starts from 360 and one turning forwards from 0.
        /// </summary>
        private static double NormalizeStartAngle(double startAngle, double sweep)
        {
            var α = startAngle;
            if (α < 0)
                α += (1 + Math.Floor(Math.Abs(α) / 360)) * 360;
            else if (α > 360)
                α -= Math.Floor(α / 360) * 360;
            Debug.Assert(α is >= 0 and <= 360);

            if (α == 0 && sweep < 0)
                return 360;
            #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
            if (α == 360 && sweep > 0)
                return 0;
            #pragma warning restore S1244
            return α;
        }

        /// <summary>The sweep angle, no further than one whole turn either way.</summary>
        private static double ClampSweep(double sweepAngle)
        {
            if (sweepAngle < -360)
                return -360;
            if (sweepAngle > 360)
                return 360;
            return sweepAngle;
        }

        /// <summary>
        /// Calculates the quadrant (0 through 3) of the specified angle. If the angle lies on an edge
        /// (0, 90, 180, etc.) the result depends on the details how the angle is used.
        /// </summary>
        private static int Quadrant(double φ, bool start, bool clockwise)
        {
            Debug.Assert(φ >= 0);
            if (φ > 360)
                φ -= Math.Floor(φ / 360) * 360;

            var quadrant = (int)(φ / 90);
            #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
            if (quadrant * 90 == φ)
            #pragma warning restore S1244
            {
                if ((start && !clockwise) || (!start && clockwise))
                    quadrant = quadrant == 0 ? 3 : quadrant - 1;
            }
            else
            {
                quadrant = clockwise ? (int)Math.Floor(φ / 90) % 4 : (int)Math.Floor(φ / 90);
            }
            return quadrant;
        }
    }

    /// <summary>
    /// Appends a Bézier curve for an arc within a full quadrant.
    /// </summary>
    private static void AppendPartialArcQuadrant(List<XPoint> points, double x, double y, double width, double height, double α, double β, PathStart pathStart, XMatrix matrix)
    {
        Debug.Assert(α is >= 0 and <= 360);
        Debug.Assert(β >= 0);
        if (β > 360)
            β -= Math.Floor(β / 360) * 360;
        Debug.Assert(Math.Abs(α - β) <= 90);

        // Scanling factor.
        var δx = width / 2;
        var δy = height / 2;

        // Center of ellipse.
        var x0 = x + δx;
        var y0 = y + δy;

        // We have the following quarters:
        //     |
        //   2 | 3
        // ----+-----
        //   1 | 0
        //     |
        // If the angles lie in quarter 2 or 3, their values are subtracted by 180 and the
        // resulting curve is reflected at the center. This algorithm works as expected (simply tried out).
        // There may be a mathematically more elegant solution...
        var reflect = false;
        if (α >= 180 && β >= 180)
        {
            α -= 180;
            β -= 180;
            reflect = true;
        }

        double cosα, cosβ, sinα, sinβ;
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        if (width == height)
        #pragma warning restore S1244
        {
            // Circular arc needs no correction.
            α *= Calc.Deg2Rad;
            β *= Calc.Deg2Rad;
        }
        else
        {
            // Elliptic arc needs the angles to be adjusted such that the scaling transformation is compensated.
            α *= Calc.Deg2Rad;
            sinα = Math.Sin(α);
            if (Math.Abs(sinα) > 1E-10)
                α = Math.PI / 2 - Math.Atan(δy * Math.Cos(α) / (δx * sinα));
            β *= Calc.Deg2Rad;
            sinβ = Math.Sin(β);
            if (Math.Abs(sinβ) > 1E-10)
                β = Math.PI / 2 - Math.Atan(δy * Math.Cos(β) / (δx * sinβ));
        }

        var κ = 4 * (1 - Math.Cos((α - β) / 2)) / (3 * Math.Sin((β - α) / 2));
        sinα = Math.Sin(α);
        cosα = Math.Cos(α);
        sinβ = Math.Sin(β);
        cosβ = Math.Cos(β);

        if (!reflect)
        {
            // Calculation for quarter 0 and 1.
            switch (pathStart)
            {
                case PathStart.MoveTo1st:
                    points.Add(matrix.Transform(new XPoint(x0 + δx * cosα, y0 + δy * sinα)));
                    break;

                case PathStart.LineTo1st:
                    points.Add(matrix.Transform(new XPoint(x0 + δx * cosα, y0 + δy * sinα)));
                    break;

                case PathStart.Ignore1st:
                    break;
            }
            points.Add(matrix.Transform(new XPoint(x0 + δx * (cosα - κ * sinα), y0 + δy * (sinα + κ * cosα))));
            points.Add(matrix.Transform(new XPoint(x0 + δx * (cosβ + κ * sinβ), y0 + δy * (sinβ - κ * cosβ))));
            points.Add(matrix.Transform(new XPoint(x0 + δx * cosβ, y0 + δy * sinβ)));
        }
        else
        {
            // Calculation for quarter 2 and 3.
            switch (pathStart)
            {
                case PathStart.MoveTo1st:
                    points.Add(matrix.Transform(new XPoint(x0 - δx * cosα, y0 - δy * sinα)));
                    break;

                case PathStart.LineTo1st:
                    points.Add(matrix.Transform(new XPoint(x0 - δx * cosα, y0 - δy * sinα)));
                    break;

                case PathStart.Ignore1st:
                    break;
            }
            points.Add(matrix.Transform(new XPoint(x0 - δx * (cosα - κ * sinα), y0 - δy * (sinα + κ * cosα))));
            points.Add(matrix.Transform(new XPoint(x0 - δx * (cosβ + κ * sinβ), y0 - δy * (sinβ - κ * cosβ))));
            points.Add(matrix.Transform(new XPoint(x0 - δx * cosβ, y0 - δy * sinβ)));
        }
    }

    /// <summary>
    /// Creates between 1 and 5 Béziers curves from parameters specified like in WPF.
    /// </summary>
    public static List<XPoint> BezierCurveFromArc(XPoint point1, XPoint point2, XSize size,
        double rotationAngle, bool isLargeArc, bool clockwise, PathStart pathStart)
    {
        // See also http://www.charlespetzold.com/blog/blog.xml from January 2, 2008:
        // http://www.charlespetzold.com/blog/2008/01/Mathematics-of-ArcSegment.html
        var δx = size.Width;
        var δy = size.Height;
        Debug.Assert(δx * δy > 0);
        var factor = δy / δx;
        var isCounterclockwise = !clockwise;

        // Adjust for different radii and rotation angle.
        var matrix = new XMatrix();
        matrix.RotateAppend(-rotationAngle);
        matrix.ScaleAppend(δy / δx, 1);
        var pt1 = matrix.Transform(point1);
        var pt2 = matrix.Transform(point2);

        // Get info about chord that connects both points.
        var midPoint = new XPoint((pt1.X + pt2.X) / 2, (pt1.Y + pt2.Y) / 2);
        var vect = pt2 - pt1;
        var halfChord = vect.Length / 2;

        // Get vector from chord to center.
        XVector vectRotated;

        // (comparing two Booleans here!)
        if (isLargeArc == isCounterclockwise)
            vectRotated = new XVector(-vect.Y, vect.X);
        else
            vectRotated = new XVector(vect.Y, -vect.X);

        vectRotated.Normalize();

        // Distance from chord to center.
        var centerDistance = Math.Sqrt(δy * δy - halfChord * halfChord);
        if (double.IsNaN(centerDistance))
            centerDistance = 0;

        // Calculate center point.
        var center = midPoint + centerDistance * vectRotated;

        // Get angles from center to the two points.
        var α = Math.Atan2(pt1.Y - center.Y, pt1.X - center.X);
        var β = Math.Atan2(pt2.Y - center.Y, pt2.X - center.X);

        // (another comparison of two Booleans!)
        if (isLargeArc == (Math.Abs(β - α) < Math.PI))
        {
            if (α < β)
                α += 2 * Math.PI;
            else
                β += 2 * Math.PI;
        }

        // Invert matrix for final point calculation.
        matrix.Invert();
        var sweepAngle = β - α;

        // Let the algorithm of GDI+ DrawArc to Bézier curves do the rest of the job
        return BezierCurveFromArc(center.X - δx * factor, center.Y - δy, 2 * δx * factor, 2 * δy,
            α / Calc.Deg2Rad, sweepAngle / Calc.Deg2Rad, pathStart, ref matrix);
    }
}
