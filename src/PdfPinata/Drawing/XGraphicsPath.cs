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
using PdfPinata.Fonts;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents a series of connected lines and curves.
/// </summary>
public sealed class XGraphicsPath
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XGraphicsPath"/> class.
    /// </summary>
    public XGraphicsPath()
    {
        CorePath = new CoreGraphicsPath();
    }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    public XGraphicsPath Clone()
    {
        var path = (XGraphicsPath)MemberwiseClone();
        CorePath = new CoreGraphicsPath(CorePath);
        return path;
    }

    // ----- AddLine ------------------------------------------------------------------------------

    /// <summary>
    /// Adds  a line segment to current figure.
    /// </summary>
    public void AddLine(XPoint pt1, XPoint pt2)
    {
        AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);
    }

    /// <summary>Starts a new figure at the given point without drawing anything.</summary>
    public void AddMove(double x1, double y1)
        => CorePath.MoveTo(x1, y1);

    /// <summary>
    /// Adds  a line segment to current figure.
    /// </summary>
    public void AddLine(double x1, double y1, double x2, double y2)
    {
        CorePath.MoveOrLineTo(x1, y1);
        CorePath.LineTo(x2, y2, false);
    }

    // ----- AddLines -----------------------------------------------------------------------------

    /// <summary>
    /// Adds a series of connected line segments to current figure.
    /// </summary>
    public void AddLines(XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count == 0)
            return;
        CorePath.MoveOrLineTo(points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx++)
            CorePath.LineTo(points[idx].X, points[idx].Y, false);
    }

    // ----- AddBezier ----------------------------------------------------------------------------

    /// <summary>
    /// Adds a cubic Bézier curve to the current figure.
    /// </summary>
    public void AddBezier(XPoint pt1, XPoint pt2, XPoint pt3, XPoint pt4)
    {
        AddBezier(pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
    }

    /// <summary>
    /// Adds a cubic Bézier curve to the current figure.
    /// </summary>
    public void AddBezier(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        CorePath.MoveOrLineTo(x1, y1);
        CorePath.BezierTo(x2, y2, x3, y3, x4, y4, false);
    }

    // ----- AddBeziers ---------------------------------------------------------------------------

    /// <summary>
    /// Adds a sequence of connected cubic Bézier curves to the current figure.
    /// </summary>
    public void AddBeziers(XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count < 4)
            throw new ArgumentException("At least four points required for bezier curve.", nameof(points));

        if ((count - 1) % 3 != 0)
            throw new ArgumentException("Invalid number of points for bezier curve. Number must fulfil 4+3n.",
                nameof(points));

        CorePath.MoveOrLineTo(points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx += 3)
        {
            CorePath.BezierTo(points[idx].X, points[idx].Y, points[idx + 1].X, points[idx + 1].Y,
                points[idx + 2].X, points[idx + 2].Y, false);
        }
    }

    // ----- AddCurve -----------------------------------------------------------------------

    /// <summary>
    /// Adds a spline curve to the current figure.
    /// </summary>
    public void AddCurve(XPoint[] points)
    {
        AddCurve(points, 0.5);
    }

    /// <summary>
    /// Adds a spline curve to the current figure.
    /// </summary>
    public void AddCurve(XPoint[] points, double tension)
    {
        var count = points.Length;
        if (count < 2)
            throw new ArgumentException("AddCurve requires two or more points.", nameof(points));
        CorePath.AddCurve(points, tension);
    }

    /// <summary>
    /// Adds a spline curve to the current figure.
    /// </summary>
    public void AddCurve(XPoint[] points, int offset, int numberOfSegments, double tension)
    {
        throw new NotImplementedException("AddCurve not yet implemented.");
    }

    // ----- AddArc -------------------------------------------------------------------------------

    /// <summary>
    /// Adds an elliptical arc to the current figure.
    /// </summary>
    public void AddArc(XRect rect, double startAngle, double sweepAngle)
    {
        AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Adds an elliptical arc to the current figure.
    /// </summary>
    public void AddArc(double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        CorePath.AddArc(x, y, width, height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Adds an elliptical arc to the current figure. The arc is specified WPF like.
    /// </summary>
    public void AddArc(XPoint point1, XPoint point2, XSize size, double rotationAngle, bool isLargeArg, XSweepDirection sweepDirection)
    {
        CorePath.AddArc(point1, point2, size, rotationAngle, isLargeArg, sweepDirection);
    }

    // ----- AddRectangle -------------------------------------------------------------------------

    /// <summary>
    /// Adds a rectangle to this path.
    /// </summary>
    public void AddRectangle(XRect rect)
    {
        CorePath.MoveTo(rect.X, rect.Y);
        CorePath.LineTo(rect.X + rect.Width, rect.Y, false);
        CorePath.LineTo(rect.X + rect.Width, rect.Y + rect.Height, false);
        CorePath.LineTo(rect.X, rect.Y + rect.Height, true);
        CorePath.CloseSubpath();
    }

    /// <summary>
    /// Adds a rectangle to this path.
    /// </summary>
    public void AddRectangle(double x, double y, double width, double height)
    {
        AddRectangle(new XRect(x, y, width, height));
    }

    // ----- AddRectangles ------------------------------------------------------------------------

    /// <summary>
    /// Adds a series of rectangles to this path.
    /// </summary>
    public void AddRectangles(XRect[] rects)
    {
        var count = rects.Length;
        for (var idx = 0; idx < count; idx++)
        {
            AddRectangle(rects[idx]);
        }
    }

    // ----- AddRoundedRectangle ------------------------------------------------------------------

    /// <summary>
    /// Adds a rectangle with rounded corners to this path.
    /// </summary>
    public void AddRoundedRectangle(double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
    {
        var arcWidth = ellipseWidth / 2;
        var arcHeight = ellipseHeight / 2;
        CorePath.MoveTo(x + width - arcWidth, y);
        CorePath.QuadrantArcTo(x + width - arcWidth, y + arcHeight, arcWidth, arcHeight, 1, true);

        CorePath.LineTo(x + width, y + height - arcHeight, false);
        CorePath.QuadrantArcTo(x + width - arcWidth, y + height - arcHeight, arcWidth, arcHeight, 4, true);

        CorePath.LineTo(x + arcWidth, y + height, false);
        CorePath.QuadrantArcTo(x + arcWidth, y + height - arcHeight, arcWidth, arcHeight, 3, true);

        CorePath.LineTo(x, y + arcHeight, false);
        CorePath.QuadrantArcTo(x + arcWidth, y + arcHeight, arcWidth, arcHeight, 2, true);

        CorePath.CloseSubpath();

    }

    // ----- AddEllipse ---------------------------------------------------------------------------

    /// <summary>
    /// Adds an ellipse to the current path.
    /// </summary>
    public void AddEllipse(XRect rect)
    {
        AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Adds an ellipse to the current path.
    /// </summary>
    public void AddEllipse(double x, double y, double width, double height)
    {
        var w = width / 2;
        var h = height / 2;
        var xc = x + w;
        var yc = y + h;
        CorePath.MoveTo(x + w, y);
        CorePath.QuadrantArcTo(xc, yc, w, h, 1, true);
        CorePath.QuadrantArcTo(xc, yc, w, h, 4, true);
        CorePath.QuadrantArcTo(xc, yc, w, h, 3, true);
        CorePath.QuadrantArcTo(xc, yc, w, h, 2, true);
        CorePath.CloseSubpath();
    }

    // ----- AddPolygon ---------------------------------------------------------------------------

    /// <summary>
    /// Adds a polygon to this path.
    /// </summary>
    public void AddPolygon(XPoint[] points)
    {
        var count = points.Length;
        if (count == 0)
            return;

        CorePath.MoveTo(points[0].X, points[0].Y);
        for (var idx = 0; idx < count - 1; idx++)
            CorePath.LineTo(points[idx].X, points[idx].Y, false);
        CorePath.LineTo(points[count - 1].X, points[count - 1].Y, true);
        CorePath.CloseSubpath();

    }

    // ----- AddPie -------------------------------------------------------------------------------

    /// <summary>
    /// Adds the outline of a pie shape to this path.
    /// </summary>
    public void AddPie(XRect rect, double startAngle, double sweepAngle)
    {
        AddPie(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Adds the outline of a pie shape to this path.
    /// </summary>
    public void AddPie(double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        CorePath.AddPie(x, y, width, height, startAngle, sweepAngle);
    }

    // ----- AddClosedCurve ------------------------------------------------------------------------

    /// <summary>
    /// Adds a closed curve to this path.
    /// </summary>
    public void AddClosedCurve(XPoint[] points)
    {
        AddClosedCurve(points, 0.5);
    }

    /// <summary>
    /// Adds a closed curve to this path.
    /// </summary>
    public void AddClosedCurve(XPoint[] points, double tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        var count = points.Length;
        if (count == 0)
            return;
        if (count < 2)
            throw new ArgumentException("Not enough points.", nameof(points));
        CorePath.AddClosedCurve(points, tension);
    }

    // ----- AddPath ------------------------------------------------------------------------------

    /// <summary>
    /// Adds the specified path to this path.
    /// </summary>
    public void AddPath(XGraphicsPath path, bool connect)
    {
        ArgumentNullException.ThrowIfNull(path);

        // The appended path's own FillMode is not carried over. A path has one fill rule and it
        // belongs to the path being drawn, so taking the other one's would silently change how the
        // figures already here are filled.
        CorePath.AddPath(path.CorePath, connect);
    }

    // ----- AddString ----------------------------------------------------------------------------

    /// <summary>
    /// Adds the outlines of a text string to this path, with the string laid out at a point.
    /// </summary>
    /// <remarks>
    /// <b>To stroke text you do not need a path.</b>
    /// <see cref="XGraphics.DrawString(string,XFont,XPen,XBrush,XRect,XStringFormat)"/> takes a pen
    /// as well as a brush and selects the PDF text rendering mode for you, which is cheaper and
    /// keeps the text searchable. Come here for what that cannot do: fill glyphs with a gradient,
    /// clip a photograph to their shapes, or widen them as geometry.
    /// <para>
    /// Needs <see cref="GlobalFontSettings.GlyphOutlineProvider"/> to be set, and throws naming it
    /// if it is not. Text added to a path is no longer text, so nothing in the file says what it
    /// said and no reader can search or copy it.
    /// </para>
    /// </remarks>
    public void AddString(string s, XFontFamily family, XFontStyle style, double emSize, XPoint origin,
        XStringFormat format)
    {
        // The same reduction DrawString's point overloads make: a rectangle with no extent, which
        // every alignment but Near and BaseLine then measures against and finds nothing in.
        AddString(s, family, style, emSize, new XRect(origin.X, origin.Y, 0, 0), format);
    }

    /// <summary>
    /// Adds the outlines of a text string to this path, laid out within a rectangle.
    /// </summary>
    /// <remarks>
    /// <b>To stroke text you do not need a path.</b>
    /// <see cref="XGraphics.DrawString(string,XFont,XPen,XBrush,XRect,XStringFormat)"/> takes a pen
    /// as well as a brush and selects the PDF text rendering mode for you, which is cheaper and
    /// keeps the text searchable. Come here for what that cannot do: fill glyphs with a gradient,
    /// clip a photograph to their shapes, or widen them as geometry.
    /// <para>
    /// Needs <see cref="GlobalFontSettings.GlyphOutlineProvider"/> to be set, and throws naming it
    /// if it is not. Text added to a path is no longer text, so nothing in the file says what it
    /// said and no reader can search or copy it.
    /// </para>
    /// </remarks>
    public void AddString(string s, XFontFamily family, XFontStyle style, double emSize, XRect layoutRect,
        XStringFormat format)
    {
        ArgumentNullException.ThrowIfNull(s);

        ArgumentNullException.ThrowIfNull(family);

        format ??= XStringFormats.Default;

        // A BaseLine line alignment puts the baseline on the rectangle's top edge and ignores the
        // height, exactly as XGraphics.DrawString does. The two guards were separate code saying
        // the same thing, and both are gone.

        if (s.Length == 0)
            return;

        var font = new XFont(family.Name, emSize, style);

        // Measured and placed by the code the renderer draws text with, not by a copy of it.
        // A path is built in the caller's world, which measures y downwards.
        var width = FontHelper.MeasureString(s, font, format).Width;
        var origin = TextOrigin.For(layoutRect, width, font, format, true);

        var isBold = (style & XFontStyle.Bold) != 0;
        var isItalic = (style & XFontStyle.Italic) != 0;

        // Read before anything is added, so that an unregistered provider leaves the path as it
        // found it rather than half filled.
        var provider = GlobalFontSettings.GlyphOutlineProvider;

        foreach (var outline in provider.GetOutlines(s, family.Name, isBold, isItalic, emSize))
            AddGlyphOutline(outline, origin);
    }

    /// <summary>
    /// Copies one glyph's outline into the path, turned the right way up and moved to where the
    /// text starts.
    /// </summary>
    /// <remarks>
    /// A font measures y upwards from the baseline and this path measures it downwards, so the
    /// sign of y is flipped here - once, for every backend, rather than once per backend.
    /// </remarks>
    private void AddGlyphOutline(XGlyphOutline outline, XPoint origin)
    {
        foreach (var segment in outline.Segments)
        {
            switch (segment.Kind)
            {
                case XGlyphSegmentKind.Start:
                    CorePath.MoveTo(origin.X + segment.End.X, origin.Y - segment.End.Y);
                    break;

                case XGlyphSegmentKind.Line:
                    CorePath.LineTo(origin.X + segment.End.X, origin.Y - segment.End.Y, false);
                    break;

                case XGlyphSegmentKind.Curve:
                    CorePath.BezierTo(
                        origin.X + segment.Control1.X, origin.Y - segment.Control1.Y,
                        origin.X + segment.Control2.X, origin.Y - segment.Control2.Y,
                        origin.X + segment.End.X, origin.Y - segment.End.Y, false);
                    break;

                case XGlyphSegmentKind.Close:
                    CorePath.CloseSubpath();
                    break;
            }
        }
    }

    // --------------------------------------------------------------------------------------------

    /// <summary>
    /// Closes the current figure and starts a new figure.
    /// </summary>
    public void CloseFigure()
    {
        CorePath.CloseSubpath();
    }

    /// <summary>
    /// Starts a new figure without closing the current figure. The next segment added begins a
    /// figure of its own rather than being joined to the end of the current one.
    /// </summary>
    public void StartFigure()
    {
        CorePath.StartFigure();
    }

    // --------------------------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets an XFillMode that determines how the interiors of shapes are filled.
    /// </summary>
    public XFillMode FillMode
    {
        get => _fillMode;
        set => _fillMode = value;
        // Nothing to do.
    }

    private XFillMode _fillMode;

    // --------------------------------------------------------------------------------------------

    /// <summary>
    /// Converts each curve in this XGraphicsPath into a sequence of connected line segments.
    /// </summary>
    #pragma warning disable CA1822 // Public API: making these no-op overloads static would break every caller, and the operation acts on this path once implemented.
    public void Flatten()
    {
        // Just do nothing.
    }

    /// <summary>
    /// Converts each curve in this XGraphicsPath into a sequence of connected line segments.
    /// </summary>
    public void Flatten(XMatrix matrix)
    {
        // Just do nothing.
    }

    /// <summary>
    /// Converts each curve in this XGraphicsPath into a sequence of connected line segments.
    /// </summary>
    public void Flatten(XMatrix matrix, double flatness)
    {
        // Just do nothing.
    }
    #pragma warning restore CA1822

    // --------------------------------------------------------------------------------------------

    /// <summary>
    /// Replaces this path with curves that enclose the area that is filled when this path is drawn
    /// by the specified pen.
    /// </summary>
    #pragma warning disable CA1822 // Public API: making these no-op overloads static would break every caller, and the operation acts on this path once implemented.
    public void Widen(XPen pen)
    {
        // Just do nothing.
    }

    /// <summary>
    /// Replaces this path with curves that enclose the area that is filled when this path is drawn
    /// by the specified pen.
    /// </summary>
    public void Widen(XPen pen, XMatrix matrix)
    {
        // Just do nothing.
    }

    /// <summary>
    /// Replaces this path with curves that enclose the area that is filled when this path is drawn
    /// by the specified pen.
    /// </summary>
    public void Widen(XPen pen, XMatrix matrix, double flatness)
    {
        // Just do nothing.
    }
    #pragma warning restore CA1822

    /// <summary>
    /// Grants access to internal objects of this class.
    /// </summary>
    public XGraphicsPathInternals Internals => new();

    /// <summary>
    /// Gets access to underlying Core graphics path.
    /// </summary>
    internal CoreGraphicsPath CorePath;
}
