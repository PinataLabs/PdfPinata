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
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Internal;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.Advanced;

// ReSharper disable RedundantNameQualifier
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace PdfPinata.Drawing.Pdf;

/// <summary>
/// Represents a drawing surface for PdfPages.
/// </summary>
internal class XGraphicsPdfRenderer : IXGraphicsRenderer
{
    public XGraphicsPdfRenderer(PdfPage page, XGraphics gfx, XGraphicsPdfPageOptions options)
    {
        Page = page;
        ColorMode = page._document.Options.ColorMode;
        _options = options;
        _gfx = gfx;
        _content = new StringBuilder();
        page.RenderContent.PdfRenderer = this;
        _gfxState = new PdfGraphicsState(this);
    }

    public XGraphicsPdfRenderer(XForm form, XGraphics gfx)
    {
        Form = form;
        ColorMode = form.Owner.Options.ColorMode;
        _gfx = gfx;
        _content = new StringBuilder();
        form.PdfRenderer = this;
        _gfxState = new PdfGraphicsState(this);
    }

    /// <summary>
    /// Gets the content created by this renderer.
    /// </summary>
    string GetContent()
    {
        EndPage();
        return _content.ToString();
    }

    public XGraphicsPdfPageOptions PageOptions => _options;

    public void Close()
    {
        if (Page != null)
        {
            var content2 = Page.RenderContent;
            content2.CreateStream(PdfEncoders.RawEncoding.GetBytes(GetContent()));

            _gfx = null;
            Page.RenderContent.PdfRenderer = null;
            Page.RenderContent = null;
            Page = null;
        }
        else if (Form != null)
        {
            Form._pdfForm.CreateStream(PdfEncoders.RawEncoding.GetBytes(GetContent()));
            _gfx = null;
            Form.PdfRenderer = null;
            Form = null;
        }
    }

    // --------------------------------------------------------------------------------------------

    #region  Drawing

    // ----- DrawLine -----------------------------------------------------------------------------

    /// <summary>
    /// Strokes a single connection of two points.
    /// </summary>
    public void DrawLine(XPen pen, double x1, double y1, double x2, double y2)
    {
        DrawLines(pen, new XPoint[] { new(x1, y1), new(x2, y2) });
    }

    // ----- DrawLines ----------------------------------------------------------------------------

    /// <summary>
    /// Strokes a series of connected points.
    /// </summary>
    public void DrawLines(XPen pen, XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count == 0)
            return;

        Realize(pen);

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx++)
            AppendFormatPoint("{0:" + format + "} {1:" + format + "} l\n", points[idx].X, points[idx].Y);
        _content.Append("S\n");
    }

    // ----- DrawBezier ---------------------------------------------------------------------------

    public void DrawBezier(XPen pen, double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        DrawBeziers(pen, new XPoint[] { new(x1, y1), new(x2, y2), new(x3, y3), new(x4, y4) });
    }

    // ----- DrawBeziers --------------------------------------------------------------------------

    public void DrawBeziers(XPen pen, XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count == 0)
            return;

        if ((count - 1) % 3 != 0)
            throw new ArgumentException("Invalid number of points for bezier curves. Number must fulfil 4+3n.", nameof(points));

        Realize(pen);

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx += 3)
            AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
                points[idx].X, points[idx].Y,
                points[idx + 1].X, points[idx + 1].Y,
                points[idx + 2].X, points[idx + 2].Y);

        AppendStrokeFill(pen, null, XFillMode.Alternate, false);
    }

    // ----- DrawCurve ----------------------------------------------------------------------------

    public void DrawCurve(XPen pen, XPoint[] points, double tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count == 0)
            return;
        if (count < 2)
            throw new ArgumentException("Not enough points", nameof(points));

        // See http://pubpages.unh.edu/~cs770/a5/cardinal.html  // Link is down...
        tension /= 3;

        Realize(pen);

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[0].X, points[0].Y);
        if (count == 2)
        {
            // Just draws a line.
            AppendCurveSegment(points[0], points[0], points[1], points[1], tension);
        }
        else
        {
            AppendCurveSegment(points[0], points[0], points[1], points[2], tension);
            for (var idx = 1; idx < count - 2; idx++)
                AppendCurveSegment(points[idx - 1], points[idx], points[idx + 1], points[idx + 2], tension);
            AppendCurveSegment(points[count - 3], points[count - 2], points[count - 1], points[count - 1], tension);
        }
        AppendStrokeFill(pen, null, XFillMode.Alternate, false);
    }

    // ----- DrawArc ------------------------------------------------------------------------------

    public void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);

        Realize(pen);

        AppendPartialArc(x, y, width, height, startAngle, sweepAngle, PathStart.MoveTo1st, new XMatrix());
        AppendStrokeFill(pen, null, XFillMode.Alternate, false);
    }

    // ----- DrawRectangle ------------------------------------------------------------------------

    public void DrawRectangle(XPen pen, XBrush brush, double x, double y, double width, double height)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen));

        const string format = Config.SignificantFigures3;

        Realize(pen, brush);
        AppendFormatRect("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} re\n", x, y + height, width, height);

        if (pen != null && brush != null)
            _content.Append("B\n");
        else if (pen != null)
            _content.Append("S\n");
        else
            _content.Append("f\n");
    }

    // ----- DrawRectangles -----------------------------------------------------------------------

    public void DrawRectangles(XPen pen, XBrush brush, XRect[] rects)
    {
        var count = rects.Length;
        for (var idx = 0; idx < count; idx++)
        {
            var rect = rects[idx];
            DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    // ----- DrawRoundedRectangle -----------------------------------------------------------------

    public void DrawRoundedRectangle(XPen pen, XBrush brush, double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
    {
        var path = new XGraphicsPath();
        path.AddRoundedRectangle(x, y, width, height, ellipseWidth, ellipseHeight);
        DrawPath(pen, brush, path);
    }

    // ----- DrawEllipse --------------------------------------------------------------------------

    public void DrawEllipse(XPen pen, XBrush brush, double x, double y, double width, double height)
    {
        Realize(pen, brush);

        // Useful information is here http://home.t-online.de/home/Robert.Rossmair/ellipse.htm (note: link was dead on November 2, 2015)
        // or here http://www.whizkidtech.redprince.net/bezier/circle/
        // Deeper but more difficult: http://www.tinaja.com/cubic01.asp
        var rect = new XRect(x, y, width, height);
        var δx = rect.Width / 2;
        var δy = rect.Height / 2;
        var fx = δx * Const.κ;
        var fy = δy * Const.κ;
        var x0 = rect.X + δx;
        var y0 = rect.Y + δy;

        // Approximate an ellipse by drawing four cubic splines.
        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", x0 + δx, y0);
        AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
            x0 + δx, y0 + fy, x0 + fx, y0 + δy, x0, y0 + δy);
        AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
            x0 - fx, y0 + δy, x0 - δx, y0 + fy, x0 - δx, y0);
        AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
            x0 - δx, y0 - fy, x0 - fx, y0 - δy, x0, y0 - δy);
        AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
            x0 + fx, y0 - δy, x0 + δx, y0 - fy, x0 + δx, y0);
        AppendStrokeFill(pen, brush, XFillMode.Winding, true);
    }

    // ----- DrawPolygon --------------------------------------------------------------------------

    public void DrawPolygon(XPen pen, XBrush brush, XPoint[] points, XFillMode fillmode)
    {
        Realize(pen, brush);

        var count = points.Length;
        if (points.Length < 2)
            throw new ArgumentException(PSSR.PointArrayAtLeast(2), nameof(points));

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[0].X, points[0].Y);
        for (var idx = 1; idx < count; idx++)
            AppendFormatPoint("{0:" + format + "} {1:" + format + "} l\n", points[idx].X, points[idx].Y);

        AppendStrokeFill(pen, brush, fillmode, true);
    }

    // ----- DrawPie ------------------------------------------------------------------------------

    public void DrawPie(XPen pen, XBrush brush, double x, double y, double width, double height,
        double startAngle, double sweepAngle)
    {
        Realize(pen, brush);

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", x + width / 2, y + height / 2);
        AppendPartialArc(x, y, width, height, startAngle, sweepAngle, PathStart.LineTo1st, new XMatrix());
        AppendStrokeFill(pen, brush, XFillMode.Alternate, true);
    }

    // ----- DrawClosedCurve ----------------------------------------------------------------------

    public void DrawClosedCurve(XPen pen, XBrush brush, XPoint[] points, double tension, XFillMode fillmode)
    {
        var count = points.Length;
        if (count == 0)
            return;
        if (count < 2)
            throw new ArgumentException("Not enough points.", nameof(points));

        // Simply tried out. Not proofed why it is correct.
        tension /= 3;

        Realize(pen, brush);

        const string format = Config.SignificantFigures4;
        AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[0].X, points[0].Y);
        if (count == 2)
        {
            // Just draw a line.
            AppendCurveSegment(points[0], points[0], points[1], points[1], tension);
        }
        else
        {
            AppendCurveSegment(points[count - 1], points[0], points[1], points[2], tension);
            for (var idx = 1; idx < count - 2; idx++)
                AppendCurveSegment(points[idx - 1], points[idx], points[idx + 1], points[idx + 2], tension);
            AppendCurveSegment(points[count - 3], points[count - 2], points[count - 1], points[0], tension);
            AppendCurveSegment(points[count - 2], points[count - 1], points[0], points[1], tension);
        }
        AppendStrokeFill(pen, brush, fillmode, true);
    }

    // ----- DrawPath -----------------------------------------------------------------------------

    public void DrawPath(XPen pen, XBrush brush, XGraphicsPath path)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen));

        Realize(pen, brush);
        AppendPath(path.CorePath);
        AppendStrokeFill(pen, brush, path.FillMode, false);
    }

    // ----- DrawString ---------------------------------------------------------------------------

    public void DrawString(string s, XFont font, XPen pen, XBrush brush, XRect rect, XStringFormat format)
    {
        // Filtered before anything at all reads it: a tab becomes the space MeasureString has
        // always measured it as, and every other character below 32 is dropped rather than looked
        // up in the cmap and drawn as the box the face keeps at .notdef. The width measured for
        // alignment below, the shaping, the glyphs embedded, what /ToUnicode says they mean and the
        // WinAnsi branch's bytes all read this one local, so measuring and drawing cannot disagree
        // about which characters there were. See TextNormalization - and note that \n is dropped
        // here rather than split on: this draws one line, and XTextFormatter draws several.
        s = TextNormalization.NormalizeLine(s);

        // A string of nothing but control characters is now nothing. Without this the renderer
        // would still realize a font, move the pen and write an empty Tj.
        if (s.Length == 0)
            return;

        var lineSpace = font.GetHeight();
        // Measured through the same format the text is drawn with: alignment and the underline and
        // strikeout rules below are all placed from this width.
        var width = _gfx.MeasureString(s, font, format).Width;

        var italicSimulation = (font.GlyphTypeface.StyleSimulations & XStyleSimulations.ItalicSimulation) != 0;
        var boldSimulation = FontHelper.SimulatesBold(font);
        // The format's decoration wins; leaving it at None keeps whatever the font's style asks
        // for, which is where underlining lived before the format could carry it.
        var underline = format.Underline != XTextDecoration.None
            ? format.Underline
            : (font.Style & XFontStyle.Underline) != 0 ? XTextDecoration.Single : XTextDecoration.None;
        var strikeout = format.Strikeout != XTextDecoration.None
            ? format.Strikeout
            : (font.Style & XFontStyle.Strikeout) != 0 ? XTextDecoration.Single : XTextDecoration.None;

        // Shaped before the font is realized rather than after, because what the text state has to
        // be set up for depends on every face the string is drawn with and not only on the one that
        // was asked for. A face with no bold file has its boldness simulated by stroking, and a
        // string that fell back may be part simulated and part not - so the stroking colour and
        // width have to be ready if any segment needs them, and only the mode and spacing then vary
        // per segment. The descriptor is the one the measuring path uses, from the same cache,
        // because measuring and drawing shaping the string differently is the one thing that must
        // not happen.
        ShapedText shaped = null;
        var anySimulatesBold = boldSimulation;
        if (font.Unicode)
        {
            var shapingDescriptor = FontDescriptorCache.GetOrCreateDescriptorFor(font) as OpenTypeDescriptor;
            shaped = TextShaping.ShapeText(s.AsSpan(), font, shapingDescriptor, format.TextDirection);

            for (var idx = 0; idx < shaped.Segments.Count && !anySimulatesBold; idx++)
                anySimulatesBold = FontHelper.SimulatesBold(shaped.Segments[idx].Font);
        }

        // Identical to boldSimulation whenever the string is all one face, which is every string
        // that needed no fallback - so the common path realizes exactly what it always did.
        Realize(font, brush, pen, anySimulatesBold, format);

        // The same arithmetic XGraphicsPath.AddString places its glyphs by, so that a string added
        // to a path lands where the same string drawn here lands.
        var origin = TextOrigin.For(rect, width, font, format, Gfx.PageDirection == XPageDirection.Downwards);
        var x = origin.X;
        var y = origin.Y;

        var realizedFont = _gfxState.RealizedFont;
        Debug.Assert(realizedFont != null);

        const string format2 = Config.SignificantFigures4;

        // The whole show-text operation, its operator included: usually a Tj, but a TJ array when
        // the words have to be spaced out by hand. See PdfGraphicsState.NeedsWordSpacingByHand.
        string text;
        if (font.Unicode)
        {
            // Shaped above, before the font was realized. Asked of the shaping seam rather than
            // looked up one character at a time, so that the glyphs drawn are the glyphs
            // MeasureString measured. With no shaper registered this is the same cmap lookup per
            // character it has always been - except that a right-to-left run comes back in the
            // order it is drawn rather than the order it was written.
            #pragma warning disable S2259 // shaped is set exactly when font.Unicode is true, which is the branch this is in.
            // ReSharper disable PossibleNullReferenceException
            if (shaped.IsAllOneFont(font))
            #pragma warning restore S2259
            {
            // ReSharper restore PossibleNullReferenceException
                // The glyphs the run really drew, rather than the ones the characters would have
                // been looked up as. This is what decides both which glyphs are embedded and what
                // /ToUnicode says they mean, and a shaper's choices have to reach it or the page
                // draws a glyph the file neither carries nor describes.
                foreach (var segment in shaped.Segments)
                    realizedFont.AddShapedRun(segment.Run, segment.TextIn(s));

                text = ShowTextOperators(s, shaped, font, format);
            }
            else
            {
                text = FallenBackTextOperators(s, shaped, font, brush, pen, format);
            }
        }
        else
        {
            realizedFont.AddChars(s);
            var bytes = PdfEncoders.WinAnsiEncoding.GetBytes(s);
            text = PdfEncoders.ToStringLiteral(bytes, false, null) + " Tj";
        }

        // Map absolute position to PDF world space.
        var pos = new XPoint(x, y);
        pos = WorldToView(pos);

        // Not adjusted for bold simulation, because that would change the center of the glyphs.
        double verticalOffset = 0;

        // How far the glyphs lean, as the tangent of the angle. Italic simulation contributes a
        // fixed lean and the caller may ask for one of their own; two shears compose by adding
        // their tangents, so the two are one number from here on.
        var skew = SkewOf(italicSimulation, format.ObliqueAngle);

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (skew == _gfxState.RealizedTextSkew)
        #pragma warning restore S1244
        {
            // The text matrix already leans the right amount, so moving to the next position is
            // all that is needed - and Td is shorter than Tm.
            AdjustTdOffset(ref pos, verticalOffset, _gfxState.RealizedTextSkew);
            AppendFormatArgs("{0:" + format2 + "} {1:" + format2 + "} Td {2}\n", pos.X, pos.Y, text);
        }
        else
        {
            // Only Tm can set the lean, and it sets the position absolutely while it is there.
            var m = new XMatrix(1, 0, skew, 1, pos.X, pos.Y);
            AppendFormatArgs("{0:" + format2 + "} {1:" + format2 + "} {2:" + format2 + "} {3:" + format2 + "} {4:" + format2 + "} {5:" + format2 + "} Tm\n{6}\n",
                m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY, text);
            _gfxState.RealizedTextSkew = skew;
            AdjustTdOffset(ref pos, verticalOffset, 0);
        }

        // The rules below are rectangles drawn in graphics mode, so they do not go through the
        // text matrix and have to be moved by the text rise themselves. Raising text moves it up
        // the page, which is towards smaller y only when y runs downwards.
        var rise = Gfx.PageDirection == XPageDirection.Downwards ? -format.TextRise : format.TextRise;

        // Built only where there is a rule to draw, which is almost never - every string drawn
        // otherwise paid for a brush nothing used.
        var ruleBrush = underline == XTextDecoration.None && strikeout == XTextDecoration.None
            ? null
            : RuleBrushFor(brush, pen, format);

        if (underline != XTextDecoration.None)
        {
            var underlinePosition = lineSpace * realizedFont.FontDescriptor.Descriptor.UnderlinePosition / font.CellSpace;
            var underlineThickness = lineSpace * realizedFont.FontDescriptor.Descriptor.UnderlineThickness / font.CellSpace;
            var underlineRectY = Gfx.PageDirection == XPageDirection.Downwards
                ? y - underlinePosition
                : y + underlinePosition - underlineThickness;
            DrawTextRule(underline, s, font, format, ruleBrush, x, underlineRectY + rise, width, underlineThickness);
        }

        if (strikeout != XTextDecoration.None)
        {
            var strikeoutPosition = lineSpace * realizedFont.FontDescriptor.Descriptor.StrikeoutPosition / font.CellSpace;
            var strikeoutSize = lineSpace * realizedFont.FontDescriptor.Descriptor.StrikeoutSize / font.CellSpace;
            var strikeoutRectY = Gfx.PageDirection == XPageDirection.Downwards
                ? y - strikeoutPosition
                : y + strikeoutPosition - strikeoutSize;
            DrawTextRule(strikeout, s, font, format, ruleBrush, x, strikeoutRectY + rise, width, strikeoutSize);
        }
    }

    /// <summary>
    /// What an underline or strikeout rule is painted with.
    /// </summary>
    /// <remarks>
    /// A colour asked for outright wins. Failing that the rule follows the text, which is the brush
    /// filling it, or the pen outlining it when there is no brush - and a pen carrying a brush of
    /// its own leaves its Color empty, so that has to be looked at before the colour is.
    /// </remarks>
    static XBrush RuleBrushFor(XBrush brush, XPen pen, XStringFormat format)
    {
        if (!format.DecorationColor.IsEmpty)
            return new XSolidBrush(format.DecorationColor);

        if (brush != null)
            return brush;

        return pen.Brush ?? new XSolidBrush(pen.Color);
    }

    /// <summary>
    /// Draws the rule that underlines or strikes out a run of text.
    /// </summary>
    /// <param name="decoration">Which rule to draw, and whether it skips the spaces between words.</param>
    /// <param name="s">The run of text the rule goes under or through.</param>
    /// <param name="font">The font the run is set in, used to measure the words.</param>
    /// <param name="format">The string format the run was drawn with, which decides where it starts.</param>
    /// <param name="brush">The brush the rule is painted with.</param>
    /// <param name="x">Where the run starts, in world coordinates.</param>
    /// <param name="top">Where the top of the rule sits, in world coordinates.</param>
    /// <param name="width">How wide the run is.</param>
    /// <param name="thickness">How thick the rule is, from the font's own metrics.</param>
    void DrawTextRule(XTextDecoration decoration, string s, XFont font, XStringFormat format,
        XBrush brush, double x, double top, double width, double thickness)
    {
        if (decoration == XTextDecoration.Words)
        {
            // Under the words and not under the spaces between them, so the run has to be broken
            // up and each piece measured to find out where it starts.
            foreach (var (wordX, wordWidth) in WordRunsOf(s, font, format, x))
                DrawTextRule(XTextDecoration.Single, null, font, format, brush, wordX, top, wordWidth, thickness);
            return;
        }

        var dashStyle = DashStyleOf(decoration);
        if (dashStyle == XDashStyle.Solid)
        {
            // Filled rather than stroked, which is how this has always been drawn and what every
            // document made with it looks like.
            DrawRectangle(null, brush, x, top, width, thickness);
            return;
        }

        // A broken rule has to be stroked, since a rectangle cannot be dotted. The pen is as thick
        // as the rule and runs down the middle of where the rectangle would have been.
        var colour = brush is XSolidBrush solid ? solid.Color : XColors.Black;
        var pen = new XPen(colour, thickness) { DashStyle = dashStyle };
        var middle = top + thickness / 2;
        DrawLine(pen, x, middle, x + width, middle);
    }

    static XDashStyle DashStyleOf(XTextDecoration decoration)
    {
        switch (decoration)
        {
            case XTextDecoration.Dotted: return XDashStyle.Dot;
            case XTextDecoration.Dash: return XDashStyle.Dash;
            case XTextDecoration.DotDash: return XDashStyle.DashDot;
            case XTextDecoration.DotDotDash: return XDashStyle.DashDotDot;
            default: return XDashStyle.Solid;
        }
    }

    /// <summary>
    /// Where each run of non-blank characters in <paramref name="s"/> starts and how wide it is,
    /// measured through the same format the text is drawn with.
    /// </summary>
    /// <remarks>
    /// Each stretch of the string - blank or not - is measured once and the widths added up, which
    /// is exact rather than close enough: a glyph advances by its own width plus the character
    /// spacing, and a space by the word spacing on top, so the width of a run really is the sum of
    /// the widths of its parts. Measuring each word's prefix from the start of the string instead
    /// would answer the same and cost a measurement of the whole string per word.
    /// </remarks>
    IEnumerable<(double X, double Width)> WordRunsOf(string s, XFont font, XStringFormat format, double x)
    {
        var idx = 0;
        while (idx < s.Length)
        {
            var blankStart = idx;
            while (idx < s.Length && char.IsWhiteSpace(s[idx]))
                idx++;
            if (idx > blankStart)
                x += _gfx.MeasureString(s.Substring(blankStart, idx - blankStart), font, format).Width;
            if (idx == s.Length)
                yield break;

            var wordStart = idx;
            while (idx < s.Length && !char.IsWhiteSpace(s[idx]))
                idx++;

            var width = _gfx.MeasureString(s.Substring(wordStart, idx - wordStart), font, format).Width;
            yield return (x, width);
            x += width;
        }
    }

    /// <summary>
    /// How far text leans to the right, as the tangent of the angle, given whether the font is
    /// having its italic drawn on for it and what the caller asked for on top of that.
    /// </summary>
    /// <remarks>
    /// Italic simulation contributes sin(20°) rather than tan(20°). That is what PDFsharp has
    /// always skewed by and what every document built with it looks like, so it is left alone; a
    /// caller asking for an angle gets its tangent, which is the skew that angle actually means
    /// and what PDFKit's oblique produces. The two add because shearing by a and then by b is
    /// shearing by a + b.
    /// </remarks>
    static double SkewOf(bool italicSimulation, double obliqueAngle)
    {
        var skew = italicSimulation ? Const.ItalicSkewAngleSinus : 0;
        if (obliqueAngle != 0)
            skew += Math.Tan(obliqueAngle * Math.PI / 180);
        return skew;
    }

    // ----- DrawImage ----------------------------------------------------------------------------

    public void DrawImage(XImage image, double x, double y, double width, double height)
    {
        const string format = Config.SignificantFigures4;

        var name = Realize(image);
        if (!(image is XForm))
        {
            if (_gfx.PageDirection == XPageDirection.Downwards)
            {
                AppendFormatImage("q {2:" + format + "} 0 0 {3:" + format + "} {0:" + format + "} {1:" + format + "} cm {4} Do Q\n",
                    x, y + height, width, height, name);
            }
            else
            {
                AppendFormatImage("q {2:" + format + "} 0 0 {3:" + format + "} {0:" + format + "} {1:" + format + "} cm {4} Do Q\n",
                    x, y, width, height, name);
            }
        }
        else
        {
            BeginPage();

            var form = (XForm)image;
            form.Finish();

            Owner.FormTable.GetForm(form);

            var cx = width / image.PointWidth;
            var cy = height / image.PointHeight;

            if (cx != 0 && cy != 0)
            {
                var xForm = image as XPdfForm;
                if (_gfx.PageDirection == XPageDirection.Downwards)
                {
                    // If we have an XPdfForm, then we take the MediaBox into account.
                    var xDraw = x;
                    var yDraw = y;
                    if (xForm != null)
                    {
                        // Yes, it is an XPdfForm - adjust the position where the page will be drawn.
                        xDraw -= xForm.Page.MediaBox.X1;
                        yDraw += xForm.Page.MediaBox.Y1;
                    }
                    AppendFormatImage("q {2:" + format + "} 0 0 {3:" + format + "} {0:" + format + "} {1:" + format + "} cm 100 Tz {4} Do Q\n",
                        xDraw, yDraw + height, cx, cy, name);
                }
                else
                {
                    // No MediaBox offset here, unlike Downwards: Upwards is obsolete and was never finished.
                    AppendFormatImage("q {2:" + format + "} 0 0 {3:" + format + "} {0:" + format + "} {1:" + format + "} cm {4} Do Q\n",
                        x, y, cx, cy, name);
                }
            }
        }
    }

    /// <summary>
    /// Draws the part of an image that <paramref name="srcRect"/> names, measured in
    /// <paramref name="srcUnit"/> from the image's top left corner, scaled to fill
    /// <paramref name="destRect"/>.
    /// </summary>
    /// <remarks>
    /// PDF has no operator that draws part of an image, so the whole image is drawn, scaled and
    /// moved so that the part asked for lands on the destination, and clipped to the destination
    /// so that nothing else shows. Asked for the whole image, this is the plain overload exactly,
    /// with no clip written.
    /// </remarks>
    public void DrawImage(XImage image, XRect destRect, XRect srcRect, XGraphicsUnit srcUnit)
    {
        if (!Enum.IsDefined(srcUnit))
            throw new ArgumentException("The unit is not a member of XGraphicsUnit.", nameof(srcUnit));

        var srcX = new XUnit(srcRect.X, srcUnit).Point;
        var srcY = new XUnit(srcRect.Y, srcUnit).Point;
        var srcWidth = new XUnit(srcRect.Width, srcUnit).Point;
        var srcHeight = new XUnit(srcRect.Height, srcUnit).Point;

        var imageWidth = image.PointWidth;
        var imageHeight = image.PointHeight;

        if (IsAbout(srcX, 0) && IsAbout(srcY, 0) && IsAbout(srcWidth, imageWidth) && IsAbout(srcHeight, imageHeight))
        {
            DrawImage(image, destRect.X, destRect.Y, destRect.Width, destRect.Height);
            return;
        }

        // No part of the image, or not a number: there is nothing to scale to the destination.
        if (!(srcWidth > 0) || !(srcHeight > 0))
            return;

        var scaleX = destRect.Width / srcWidth;
        var scaleY = destRect.Height / srcHeight;

        BeginPage();
        BeginGraphicMode();
        RealizeTransform();
        SaveState();

        // Restored whatever happens, or a failed image would leave everything after it on the page
        // clipped to where the image was to go.
        try
        {
            var clip = new XGraphicsPath();
            clip.AddRectangle(destRect);
            _gfxState.SetAndRealizeClipPath(clip);

            DrawImage(image, destRect.X - srcX * scaleX, destRect.Y - srcY * scaleY,
                imageWidth * scaleX, imageHeight * scaleY);
        }
        finally
        {
            BeginGraphicMode();
            RestoreState();
        }

        static bool IsAbout(double value, double expected) =>
            Math.Abs(value - expected) <= 1e-6 * Math.Max(1, Math.Abs(expected));
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Save and Restore

    /// <summary>
    /// Clones the current graphics state and push it on a stack.
    /// </summary>
    /// <remarks>
    /// The line below is one of the two synchronization points between the two graphics state stacks
    /// — this renderer's <c>_gfxStateStack</c> of realized PDF content-stream state, and
    /// <see cref="XGraphics"/>'s own stack of <see cref="InternalGraphicsState"/> holding the
    /// caller-facing transform and handle validity. Naming the current frame's owner is what lets
    /// the matching <see cref="Restore(XGraphicsState)"/> find the frame to pop back to. It happens
    /// exactly once per save, immediately before <see cref="SaveState"/> pushes, and it is sound only
    /// because <c>XGraphics.Save</c> pushes onto its own stack before calling in here: a handle that
    /// is not on that stack is refused by <see cref="GraphicsStateStack"/> and never reaches us.
    /// Anything that pushes onto one stack has to push onto the other.
    /// </remarks>
    public void Save(XGraphicsState state)
    {
        // Before saving, the current transformation matrix must be completely realized.
        BeginGraphicMode();
        RealizeTransform();
        // Associate the XGraphicsState with the current PdfGraphicsState.
        _gfxState.InternalState = state.InternalState;
        SaveState();
    }

    public void Restore(XGraphicsState state)
    {
        BeginGraphicMode();
        RestoreState(state.InternalState);
    }

    /// <summary>
    /// Saves the current graphics state for a container, the same way <see cref="Save"/> does.
    /// </summary>
    /// <remarks>
    /// The line below is the other synchronization point between the two graphics state stacks; see
    /// <see cref="Save"/> for what it depends on. A container is saved and restored exactly as a
    /// state is — the only difference above this seam is which token the caller hands back.
    /// </remarks>
    public void BeginContainer(XGraphicsContainer container, XRect dstrect, XRect srcrect, XGraphicsUnit unit)
    {
        // Before saving, the current transformation matrix must be completely realized.
        BeginGraphicMode();
        RealizeTransform();
        // Associate the XGraphicsContainer with the current PdfGraphicsState.
        _gfxState.InternalState = container.InternalState;
        SaveState();
    }

    public void EndContainer(XGraphicsContainer container)
    {
        BeginGraphicMode();
        RestoreState(container.InternalState);
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Transformation

    public XMatrix Transform
    {
        get
        {
            if (_gfxState.UnrealizedCtm.IsIdentity)
                return _gfxState.EffectiveCtm;
            return _gfxState.UnrealizedCtm * _gfxState.RealizedCtm;
        }
    }

    public void AddTransform(XMatrix value, XMatrixOrder matrixOrder)
    {
        _gfxState.AddTransform(value, matrixOrder);
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Clipping

    public void SetClip(XGraphicsPath path, XCombineMode combineMode)
    {
        if (path == null)
            throw new NotImplementedException("SetClip with no path.");

        // Ensure that the graphics state stack level is at least 2, because otherwise an error
        // occurs when someone set the clip region before something was drawn.
        if (_gfxState.Level < GraphicsStackLevelWorldSpace)
            RealizeTransform();

        if (combineMode == XCombineMode.Replace)
        {
            if (_clipLevel != 0)
            {
                if (_clipLevel != _gfxState.Level)
                    throw new NotImplementedException("Cannot set new clip region in an inner graphic state level.");
                else
                    ResetClip();
            }
            _clipLevel = _gfxState.Level;
        }
        else if (combineMode == XCombineMode.Intersect)
        {
            if (_clipLevel == 0)
                _clipLevel = _gfxState.Level;
        }
        else
        {
            Debug.Assert(false, "Invalid XCombineMode in internal function.");
        }
        _gfxState.SetAndRealizeClipPath(path);
    }

    /// <summary>
    /// Sets the clip path empty. Only possible if graphic state level has the same value as it has when
    /// the first time SetClip was invoked.
    /// </summary>
    public void ResetClip()
    {
        // No clip level means no clipping occurs and nothing is to do.
        if (_clipLevel == 0)
            return;

        // Only at the clipLevel the clipping can be reset.
        if (_clipLevel != _gfxState.Level)
            throw new NotImplementedException("Cannot reset clip region in an inner graphic state level.");

        // Must be in graphical mode before popping the graphics state.
        BeginGraphicMode();

        // Save InternalGraphicsState and transformation of the current graphical state.
        var state = _gfxState.InternalState;
        var effectiveCtm = _gfxState.RealizedCtm;
        effectiveCtm.Prepend(_gfxState.UnrealizedCtm);
        var worldTransform = _gfxState.WorldTransform;
        // Empty clip path by switching back to the previous state.
        RestoreState();
        SaveState();
        // Save internal state
        _gfxState.InternalState = state;
        // The Q that emptied the clip also discarded every cm written since the clip was set, so what
        // XGraphics.Transform says is no longer what the page has. Whatever the restored state lacks
        // becomes unrealized again, and the next drawing operation writes it back.
        var restoredInverse = _gfxState.RealizedCtm;
        if (restoredInverse.HasInverse)
        {
            restoredInverse.Invert();
            var unrealized = effectiveCtm;
            unrealized.Append(restoredInverse);
            _gfxState.UnrealizedCtm = unrealized;
            _gfxState.WorldTransform = worldTransform;
        }
    }

    /// <summary>
    /// The nesting level of the PDF graphics state stack when the clip region was set to non empty.
    /// Because of the way PDF is made the clip region can only be reset at this level.
    /// </summary>
    int _clipLevel;

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Miscellaneous

    /// <summary>
    /// Writes a comment to the PDF content stream. May be useful for debugging purposes.
    /// </summary>
    public void WriteComment(string comment)
    {
        // A PDF comment runs to the end of the line, and a carriage return ends a line as surely as a
        // line feed does - so every line break, of whichever kind, has to start another comment, or
        // what follows it is read as content.
        comment = comment.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\n% ");
        Append("% " + comment + "\n");
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Append to PDF stream

    /// <summary>
    /// Appends one or up to five Bézier curves that interpolate the arc.
    /// </summary>
    void AppendPartialArc(double x, double y, double width, double height, double startAngle, double sweepAngle, PathStart pathStart, XMatrix matrix)
    {
        // Normalize the angles
        var α = startAngle;
        if (α < 0)
            α = α + (1 + Math.Floor((Math.Abs(α) / 360))) * 360;
        else if (α > 360)
            α = α - Math.Floor(α / 360) * 360;
        Debug.Assert(α >= 0 && α <= 360);

        var β = sweepAngle;
        if (β < -360)
            β = -360;
        else if (β > 360)
            β = 360;

        if (α == 0 && β < 0)
            α = 360;
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        else if (α == 360 && β > 0)
            α = 0;
        #pragma warning restore S1244

        // Is it possible that the arc is small starts and ends in same quadrant?
        var smallAngle = Math.Abs(β) <= 90;

        β = α + β;
        if (β < 0)
            β = β + (1 + Math.Floor((Math.Abs(β) / 360))) * 360;

        var clockwise = sweepAngle > 0;
        var startQuadrant = Quadrant(α, true, clockwise);
        var endQuadrant = Quadrant(β, false, clockwise);

        if (startQuadrant == endQuadrant && smallAngle)
            AppendPartialArcQuadrant(x, y, width, height, α, β, pathStart, matrix);
        else
        {
            var currentQuadrant = startQuadrant;
            var firstLoop = true;
            do
            {
                if (currentQuadrant == startQuadrant && firstLoop)
                {
                    double ξ = currentQuadrant * 90 + (clockwise ? 90 : 0);
                    AppendPartialArcQuadrant(x, y, width, height, α, ξ, pathStart, matrix);
                }
                else if (currentQuadrant == endQuadrant)
                {
                    double ξ = currentQuadrant * 90 + (clockwise ? 0 : 90);
                    AppendPartialArcQuadrant(x, y, width, height, ξ, β, PathStart.Ignore1st, matrix);
                }
                else
                {
                    double ξ1 = currentQuadrant * 90 + (clockwise ? 0 : 90);
                    double ξ2 = currentQuadrant * 90 + (clockwise ? 90 : 0);
                    AppendPartialArcQuadrant(x, y, width, height, ξ1, ξ2, PathStart.Ignore1st, matrix);
                }

                // Don't stop immediately if arc is greater than 270 degrees
                if (currentQuadrant == endQuadrant && smallAngle)
                    break;

                smallAngle = true;

                if (clockwise)
                    currentQuadrant = currentQuadrant == 3 ? 0 : currentQuadrant + 1;
                else
                    currentQuadrant = currentQuadrant == 0 ? 3 : currentQuadrant - 1;

                firstLoop = false;
            } while (true);
        }
    }

    /// <summary>
    /// Gets the quadrant (0 through 3) of the specified angle. If the angle lies on an edge
    /// (0, 90, 180, etc.) the result depends on the details how the angle is used.
    /// </summary>
    static int Quadrant(double φ, bool start, bool clockwise)
    {
        Debug.Assert(φ >= 0);
        if (φ > 360)
            φ = φ - Math.Floor(φ / 360) * 360;

        var quadrant = (int)(φ / 90);
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        if (quadrant * 90 == φ)
        #pragma warning restore S1244
        {
            if ((start && !clockwise) || (!start && clockwise))
                quadrant = quadrant == 0 ? 3 : quadrant - 1;
        }
        else
            quadrant = clockwise ? ((int)Math.Floor(φ / 90)) % 4 : (int)Math.Floor(φ / 90);
        return quadrant;
    }

    /// <summary>
    /// Appends a Bézier curve for an arc within a quadrant.
    /// </summary>
    void AppendPartialArcQuadrant(double x, double y, double width, double height, double α, double β, PathStart pathStart, XMatrix matrix)
    {
        Debug.Assert(α >= 0 && α <= 360);
        Debug.Assert(β >= 0);
        if (β > 360)
            β = β - Math.Floor(β / 360) * 360;
        Debug.Assert(Math.Abs(α - β) <= 90);

        // Scanling factor
        var δx = width / 2;
        var δy = height / 2;

        // Center of ellipse
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

        double sinα, sinβ;
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        if (width == height)
        #pragma warning restore S1244
        {
            // Circular arc needs no correction.
            α = α * Calc.Deg2Rad;
            β = β * Calc.Deg2Rad;
        }
        else
        {
            // Elliptic arc needs the angles to be adjusted such that the scaling transformation is compensated.
            α = α * Calc.Deg2Rad;
            sinα = Math.Sin(α);
            if (Math.Abs(sinα) > 1E-10)
                α = Math.PI / 2 - Math.Atan(δy * Math.Cos(α) / (δx * sinα));
            β = β * Calc.Deg2Rad;
            sinβ = Math.Sin(β);
            if (Math.Abs(sinβ) > 1E-10)
                β = Math.PI / 2 - Math.Atan(δy * Math.Cos(β) / (δx * sinβ));
        }

        var κ = 4 * (1 - Math.Cos((α - β) / 2)) / (3 * Math.Sin((β - α) / 2));
        sinα = Math.Sin(α);
        var cosα = Math.Cos(α);
        sinβ = Math.Sin(β);
        var cosβ = Math.Cos(β);

        const string format = Config.SignificantFigures3;
        XPoint pt1, pt2, pt3;
        if (!reflect)
        {
            // Calculation for quarter 0 and 1
            switch (pathStart)
            {
                case PathStart.MoveTo1st:
                    pt1 = matrix.Transform(new XPoint(x0 + δx * cosα, y0 + δy * sinα));
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", pt1.X, pt1.Y);
                    break;

                case PathStart.LineTo1st:
                    pt1 = matrix.Transform(new XPoint(x0 + δx * cosα, y0 + δy * sinα));
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} l\n", pt1.X, pt1.Y);
                    break;

                case PathStart.Ignore1st:
                    break;
            }
            pt1 = matrix.Transform(new XPoint(x0 + δx * (cosα - κ * sinα), y0 + δy * (sinα + κ * cosα)));
            pt2 = matrix.Transform(new XPoint(x0 + δx * (cosβ + κ * sinβ), y0 + δy * (sinβ - κ * cosβ)));
            pt3 = matrix.Transform(new XPoint(x0 + δx * cosβ, y0 + δy * sinβ));
            AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
                pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y);
        }
        else
        {
            // Calculation for quarter 2 and 3.
            switch (pathStart)
            {
                case PathStart.MoveTo1st:
                    pt1 = matrix.Transform(new XPoint(x0 - δx * cosα, y0 - δy * sinα));
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", pt1.X, pt1.Y);
                    break;

                case PathStart.LineTo1st:
                    pt1 = matrix.Transform(new XPoint(x0 - δx * cosα, y0 - δy * sinα));
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} l\n", pt1.X, pt1.Y);
                    break;

                case PathStart.Ignore1st:
                    break;
            }
            pt1 = matrix.Transform(new XPoint(x0 - δx * (cosα - κ * sinα), y0 - δy * (sinα + κ * cosα)));
            pt2 = matrix.Transform(new XPoint(x0 - δx * (cosβ + κ * sinβ), y0 - δy * (sinβ - κ * cosβ)));
            pt3 = matrix.Transform(new XPoint(x0 - δx * cosβ, y0 - δy * sinβ));
            AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
                pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y);
        }
    }

    /// <summary>
    /// Appends a Bézier curve for a cardinal spline through pt1 and pt2.
    /// </summary>
    void AppendCurveSegment(XPoint pt0, XPoint pt1, XPoint pt2, XPoint pt3, double tension3)
    {
        const string format = Config.SignificantFigures4;
        AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n",
            pt1.X + tension3 * (pt2.X - pt0.X), pt1.Y + tension3 * (pt2.Y - pt0.Y),
            pt2.X - tension3 * (pt3.X - pt1.X), pt2.Y - tension3 * (pt3.Y - pt1.Y),
            pt2.X, pt2.Y);
    }

    /// <summary>
    /// Appends the content of a GraphicsPath object.
    /// </summary>
    internal void AppendPath(CoreGraphicsPath path)
    {
        AppendPath(path.PathPoints, path.PathTypes);
    }

    void AppendPath(XPoint[] points, Byte[] types)
    {
        const string format = Config.SignificantFigures4;
        var count = points.Length;
        if (count == 0)
            return;

        for (var idx = 0; idx < count; idx++)
        {
            // ReSharper disable InconsistentNaming
            // From GDI+ documentation:
            const byte PathPointTypeStart = 0; // move
            const byte PathPointTypeLine = 1; // line
            const byte PathPointTypeBezier = 3; // default Bezier (= cubic Bezier)
            const byte PathPointTypePathTypeMask = 0x07; // type mask (lowest 3 bits).
            const byte PathPointTypeCloseSubpath = 0x80; // closed flag
            // ReSharper restore InconsistentNaming

            var type = types[idx];
            switch (type & PathPointTypePathTypeMask)
            {
                case PathPointTypeStart:
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} m\n", points[idx].X, points[idx].Y);
                    break;

                case PathPointTypeLine:
                    AppendFormatPoint("{0:" + format + "} {1:" + format + "} l\n", points[idx].X, points[idx].Y);
                    if ((type & PathPointTypeCloseSubpath) != 0)
                        Append("h\n");
                    break;

                case PathPointTypeBezier:
                    Debug.Assert(idx + 2 < count);
                    AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} c\n", points[idx].X, points[idx].Y,
                        points[++idx].X, points[idx].Y, points[++idx].X, points[idx].Y);
                    if ((types[idx] & PathPointTypeCloseSubpath) != 0)
                        Append("h\n");
                    break;
            }
        }
    }

    internal void Append(string value)
    {
        _content.Append(value);
    }

    internal void AppendFormatArgs(string format, params object[] args)
    {
        foreach (var arg in args)
        {
            if (arg is double number && !IsWritable(number))
                throw NotAFiniteNumber(format, args);
        }

        _content.AppendFormat(CultureInfo.InvariantCulture, format, args);
    }

    internal void AppendFormatString(string format, string s)
    {
        _content.AppendFormat(CultureInfo.InvariantCulture, format, s);
    }

    internal void AppendFormatFont(string format, string s, double d)
    {
        if (!IsWritable(d))
            throw NotAFiniteNumber(format, s, d);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, s, d);
    }

    internal void AppendFormatInt(string format, int n)
    {
        _content.AppendFormat(CultureInfo.InvariantCulture, format, n);
    }

    internal void AppendFormatDouble(string format, double d)
    {
        if (!IsWritable(d))
            throw NotAFiniteNumber(format, d);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, d);
    }

    internal void AppendFormatPoint(string format, double x, double y)
    {
        var result = WorldToView(new XPoint(x, y));
        if (!IsWritable(result.X) || !IsWritable(result.Y))
            throw NotAFiniteNumber(format, result.X, result.Y);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, result.X, result.Y);
    }

    internal void AppendFormatRect(string format, double x, double y, double width, double height)
    {
        var point1 = WorldToView(new XPoint(x, y));
        if (!IsWritable(point1.X) || !IsWritable(point1.Y) || !IsWritable(width) || !IsWritable(height))
            throw NotAFiniteNumber(format, point1.X, point1.Y, width, height);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, point1.X, point1.Y, width, height);
    }

    internal void AppendFormat3Points(string format, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        var point1 = WorldToView(new XPoint(x1, y1));
        var point2 = WorldToView(new XPoint(x2, y2));
        var point3 = WorldToView(new XPoint(x3, y3));
        if (!IsWritable(point1.X) || !IsWritable(point1.Y) || !IsWritable(point2.X)
            || !IsWritable(point2.Y) || !IsWritable(point3.X) || !IsWritable(point3.Y))
        {
            throw NotAFiniteNumber(format,
                point1.X, point1.Y, point2.X, point2.Y, point3.X, point3.Y);
        }

        _content.AppendFormat(CultureInfo.InvariantCulture, format, point1.X, point1.Y, point2.X, point2.Y, point3.X, point3.Y);
    }

    internal void AppendFormat(string format, XPoint point)
    {
        var result = WorldToView(point);
        if (!IsWritable(result.X) || !IsWritable(result.Y))
            throw NotAFiniteNumber(format, result.X, result.Y);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, result.X, result.Y);
    }

    internal void AppendFormat(string format, double x, double y, string s)
    {
        var result = WorldToView(new XPoint(x, y));
        if (!IsWritable(result.X) || !IsWritable(result.Y))
            throw NotAFiniteNumber(format, result.X, result.Y, s);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, result.X, result.Y, s);
    }

    internal void AppendFormatImage(string format, double x, double y, double width, double height, string name)
    {
        var result = WorldToView(new XPoint(x, y));
        if (!IsWritable(result.X) || !IsWritable(result.Y) || !IsWritable(width) || !IsWritable(height))
            throw NotAFiniteNumber(format, result.X, result.Y, width, height, name);

        _content.AppendFormat(CultureInfo.InvariantCulture, format, result.X, result.Y, width, height, name);
    }

    /// <summary>
    /// Whether a number can be written into a content stream at all.
    /// </summary>
    /// <remarks>
    /// PDF has no syntax for NaN or for infinity. Writing one produces an operand a viewer cannot
    /// parse, and a viewer's response to that is to stop drawing - silently, and usually for the
    /// rest of the content stream rather than for the one operator. A page that arrives blank is
    /// the symptom, which is a very long way from the cause.
    /// </remarks>
    static bool IsWritable(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Refuses a number on a path that assembles its operator as text rather than formatting it,
    /// and so cannot be checked on the way through one of the Append methods.
    /// </summary>
    internal static void EnsureWritable(double value, string writing)
    {
        if (!IsWritable(value))
            throw RefuseOperator(writing);
    }

    /// <summary>
    /// The refusal, quoting the operator that was about to be written.
    /// </summary>
    /// <remarks>
    /// Formatting the doomed operator into the message is the whole value of this: "NaN NaN m"
    /// names the operator, and the position of the NaN among the operands says which coordinate
    /// went wrong. This is only reached on the way to throwing, so it costs nothing to draw with.
    /// </remarks>
    static InvalidOperationException NotAFiniteNumber(string format, params object[] args)
    {
        string operatorText;
        try
        {
            // Numbers only. The show-text operators are assembled elsewhere and handed here whole,
            // so a string operand is a run of the document's own text - and this message names an
            // exception that a caller will log. The numbers are what the message exists to carry.
            var withoutText = new object[args.Length];
            for (var idx = 0; idx < args.Length; idx++)
                withoutText[idx] = args[idx] is string ? "..." : args[idx];

            operatorText = string.Format(CultureInfo.InvariantCulture, format, withoutText).Trim();
        }
        catch (FormatException)
        {
            // The message is a diagnostic, not a contract. If the format and the arguments
            // disagree, the refusal still has to happen.
            operatorText = format.Trim();
        }

        return RefuseOperator(operatorText);
    }

    static InvalidOperationException RefuseOperator(string operatorText) =>
        new InvalidOperationException(
            $"Cannot write \"{operatorText}\" into a content stream: an operand is not a finite "
            + "number. PDF cannot express NaN or infinity, and a viewer handed one stops drawing "
            + "rather than complaining, so the page arrives blank or half-finished with nothing to "
            + "say why. Either a coordinate passed to the drawing call was already not a number, or "
            + "a transform made it one - a scale derived from a range of zero width divides by zero "
            + "and turns every point that goes through it into NaN.");

    void AppendStrokeFill(XPen pen, XBrush brush, XFillMode fillMode, bool closePath)
    {
        if (closePath)
            _content.Append("h ");

        if (fillMode == XFillMode.Winding)
        {
            if (pen != null && brush != null)
                _content.Append("B\n");
            else if (pen != null)
                _content.Append("S\n");
            else
                _content.Append("f\n");
        }
        else
        {
            if (pen != null && brush != null)
                _content.Append("B*\n");
            else if (pen != null)
                _content.Append("S\n");
            else
                _content.Append("f*\n");
        }
    }
    #endregion

    // --------------------------------------------------------------------------------------------

    #region Realizing graphical state

    /// <summary>
    /// Initializes the default view transformation, i.e. the transformation from the user page
    /// space to the PDF page space.
    /// </summary>
    void BeginPage()
    {
        if (_gfxState.Level == GraphicsStackLevelInitial)
        {
            // Flip page horizontally and mirror text.

            // PDF uses a standard right-handed Cartesian coordinate system with the y axis directed up
            // and the rotation counterclockwise. Windows uses the opposite convertion with y axis
            // directed down and rotation clockwise. When I started with PDFsharp I flipped pages horizontally
            // and then mirrored text to compensate the effect that the fipping turns text upside down.
            // I found this technique during analysis of PDF documents generated with PDFlib. Unfortunately
            // this technique leads to several problems with programms that compose or view PDF documents
            // generated with PdfPinata.
            // In PDFsharp 1.4 I implement a revised technique that does not need text mirroring any more.

            DefaultViewMatrix = new XMatrix();
            if (_gfx.PageDirection == XPageDirection.Downwards)
            {
                // Take TrimBox into account.
                PageHeightPt = VisiblePageSize.Height;
                var trimOffset = new XPoint();
                if (Page != null && Page.TrimMargins.AreSet)
                {
                    // The sheet is the page plus the bleed plus the room for printer's marks, and
                    // the origin is the corner of the page rather than of the sheet. Both come
                    // from the page so that this and XGraphics.Initialize cannot disagree.
                    PageHeightPt += Page.SheetExtraHeight;
                    trimOffset = Page.SheetOffset;
                }

                // Scale with page units.
                switch (_gfx.PageUnit)
                {
                    case XGraphicsUnit.Point:
                        // Factor is 1.
                        break;

                    case XGraphicsUnit.Presentation:
                        DefaultViewMatrix.ScalePrepend(XUnit.PresentationFactor);
                        break;

                    case XGraphicsUnit.Inch:
                        DefaultViewMatrix.ScalePrepend(XUnit.InchFactor);
                        break;

                    case XGraphicsUnit.Millimeter:
                        DefaultViewMatrix.ScalePrepend(XUnit.MillimeterFactor);
                        break;

                    case XGraphicsUnit.Centimeter:
                        DefaultViewMatrix.ScalePrepend(XUnit.CentimeterFactor);
                        break;
                }

                if (trimOffset != new XPoint())
                {
                    Debug.Assert(_gfx.PageUnit == XGraphicsUnit.Point, "With TrimMargins set the page units must be Point. Ohter cases nyi.");
                    DefaultViewMatrix.TranslatePrepend(trimOffset.X, -trimOffset.Y);
                }

                // Save initial graphic state.
                SaveState();

                // Turn the page the way the viewer will show it, so that the origin is the
                // corner the reader sees first. It has to be concatenated before the matrix
                // below, because that one still works in the units of the caller.
                AppendPageRotation();

                // Set default page transformation, if any.
                if (!DefaultViewMatrix.IsIdentity)
                {
                    Debug.Assert(_gfxState.RealizedCtm.IsIdentity);
                    const string format = Config.SignificantFigures7;
                    var cm = DefaultViewMatrix.GetElements();
                    AppendFormatArgs("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} cm ",
                        cm[0], cm[1], cm[2], cm[3], cm[4], cm[5]);
                }
            }
            else
            {
                // Scale with page units.
                switch (_gfx.PageUnit)
                {
                    case XGraphicsUnit.Point:
                        // Factor is 1.
                        break;

                    case XGraphicsUnit.Presentation:
                        DefaultViewMatrix.ScalePrepend(XUnit.PresentationFactor);
                        break;

                    case XGraphicsUnit.Inch:
                        DefaultViewMatrix.ScalePrepend(XUnit.InchFactor);
                        break;

                    case XGraphicsUnit.Millimeter:
                        DefaultViewMatrix.ScalePrepend(XUnit.MillimeterFactor);
                        break;

                    case XGraphicsUnit.Centimeter:
                        DefaultViewMatrix.ScalePrepend(XUnit.CentimeterFactor);
                        break;
                }

                // Save initial graphic state.
                SaveState();
                AppendPageRotation();
                // Set page transformation.
                const string format = Config.SignificantFigures7;
                var cm = DefaultViewMatrix.GetElements();
                AppendFormat3Points("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} cm ",
                    cm[0], cm[1], cm[2], cm[3], cm[4], cm[5]);
            }
        }
    }

    /// <summary>
    /// Concatenates the matrix that maps the page as the viewer shows it onto the page as it is
    /// stored, undoing the /Rotate entry of the page. Without it everything drawn on a rotated
    /// page ends up turned, and on a page rotated by 90 or 270 degrees it is also displaced,
    /// because such a page reports the width and the height the viewer shows, not the ones the
    /// media box holds.
    /// </summary>
    void AppendPageRotation()
    {
        var rotation = PageRotationMatrix();
        if (rotation.IsIdentity)
            return;

        const string format = Config.SignificantFigures7;
        var cm = rotation.GetElements();
        AppendFormatArgs("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} cm ",
            cm[0], cm[1], cm[2], cm[3], cm[4], cm[5]);
    }

    /// <summary>
    /// Gets the matrix that turns the page the way the viewer shows it. The corners of the
    /// visible page are mapped onto the corners of the media box that the /Rotate entry sends
    /// them to.
    /// </summary>
    XMatrix PageRotationMatrix()
    {
        var mediaBox = StoredPageSize;
        switch (PageRotation)
        {
            case 90:
                return new XMatrix(0, 1, -1, 0, mediaBox.Width, 0);
            case 180:
                return new XMatrix(-1, 0, 0, -1, mediaBox.Width, mediaBox.Height);
            case 270:
                return new XMatrix(0, -1, 1, 0, 0, mediaBox.Height);
            default:
                return new XMatrix();
        }
    }

    /// <summary>
    /// Gets the /Rotate entry of the page, normalized to 0, 90, 180 or 270.
    /// </summary>
    int PageRotation
    {
        get
        {
            if (Page == null)
                return 0;
            var rotation = Page.Rotate % 360;
            return rotation < 0 ? rotation + 360 : rotation;
        }
    }

    /// <summary>
    /// The extended graphics state that takes a gradient's soft mask off again, made once per
    /// page or form so that the reset is one resource however many gradients ask for it.
    /// </summary>
    internal PdfExtGState NoSoftMaskState
    {
        get
        {
            if (_noSoftMaskState == null)
            {
                _noSoftMaskState = new PdfExtGState(Owner);
                _noSoftMaskState.Elements.SetName(PdfExtGState.Keys.SMask, "/None");
            }
            return _noSoftMaskState;
        }
    }
    PdfExtGState _noSoftMaskState;

    /// <summary>
    /// Gets the size of this page or form as it is written to the file. It is the area drawing
    /// ends up in, before the viewer turns the page.
    /// </summary>
    internal XSize StoredPageSize => Page != null ? Page.StoredSize : Form.Size;

    /// <summary>
    /// The transform the content stream has in force where something is drawn, given the matrix
    /// that maps the caller's world onto the page. It is that matrix with the turn a rotated page
    /// is drawn through concatenated after it, because that turn is written straight into the
    /// content stream rather than tracked as part of the graphics state.
    /// </summary>
    /// <remarks>
    /// Wanted by anything that has to undo the transform rather than work within it - a soft mask
    /// group, which a reader evaluates under whatever transform was in force when the mask was
    /// set, while the pattern that paints it is anchored to the page and ignores that transform.
    /// </remarks>
    internal XMatrix RealizedTransformOf(XMatrix worldToPage)
    {
        var realized = PageRotationMatrix();
        realized.Prepend(worldToPage);
        return realized;
    }

    /// <summary>
    /// Gets the size of this page or form as the viewer shows it, which is the size the caller
    /// draws on. It differs from the stored size when the page is turned by a quarter.
    /// </summary>
    XSize VisiblePageSize => Page != null ? new XSize(Page.Width, Page.Height) : Form.Size;

    /// <summary>
    /// Ends the content stream, i.e. ends the text mode and balances the graphic state stack.
    /// </summary>
    void EndPage()
    {
        if (_streamMode == StreamMode.Text)
        {
            _content.Append("ET\n");
            _streamMode = StreamMode.Graphic;
        }

        while (_gfxStateStack.Count != 0)
            RestoreState();
    }

    /// <summary>
    /// Opens a marked-content sequence carrying the identifier that ties these marks to a structure
    /// element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In graphic mode, always. A <c>BDC</c> between <c>BT</c> and <c>ET</c> would nest a
    /// marked-content sequence inside a text object, which is legal in the grammar and wrong here:
    /// the sequence has to be able to contain the whole text object, not sit inside it.
    /// </para>
    /// <para>
    /// And the page has to have been begun first. <c>BeginPage</c> writes the opening <c>q</c> and
    /// the view matrix, and it runs on the first thing that draws — so a scope opened before
    /// anything had been drawn would have that <c>q</c> land inside it, while the matching <c>Q</c>
    /// is written by <c>EndPage</c> after the <c>EMC</c>. The two pairs would cross rather than
    /// nest, which is not allowed. Every <c>Realize</c> overload orders it this way already.
    /// </para>
    /// </remarks>
    /// <param name="tag">The structure type of the element the sequence belongs to.</param>
    /// <param name="mcid">The identifier this sequence is known by within the page.</param>
    /// <param name="removableIfEmpty">
    /// Whether the sequence may be taken back again if nothing is drawn inside it. True only for one
    /// resumed around a nested sequence: a paragraph whose last leaf is a link resumes after the link
    /// and is closed immediately, and an empty pair of operators there would put a content item into
    /// the tree covering no content at all. A sequence the caller opened is never taken back, empty
    /// or not - it is the caller's statement that something on the page belongs to that element, and
    /// silently dropping it is how an unopened scope comes to look like a balanced one.
    /// </param>
    /// <param name="actualText">
    /// What the element's own <c>/ActualText</c> says its marks really spell, or null when the
    /// element declares none. Written beside <c>/MCID</c> in the same properties dictionary, so that
    /// a page-scoped reader of the content stream alone - one that never resolves the parent tree
    /// back to the element - reads the same replacement a reader of the structure tree does. The
    /// element keeps its own <c>/ActualText</c> as well; this is additional, not instead.
    /// </param>
    internal void BeginMarkedContent(string tag, int mcid, bool removableIfEmpty = false,
        string actualText = null)
    {
        BeginPage();
        BeginGraphicMode();
        _markedContentStarts.Push(removableIfEmpty ? _content.Length : NotRemovable);
        _content.Append(tag).Append(" <</MCID ").Append(mcid);
        if (actualText != null)
        {
            // Null for the security handler: a string inside a content stream is not encrypted on
            // its own, because the stream around it already is - the same reasoning the ligature
            // path beside this one uses for the same call.
            _content.Append(" /ActualText ")
                .Append(PdfEncoders.ToStringLiteral(actualText, PdfStringEncoding.Unicode, null));
        }
        _content.Append(">> BDC\n");
    }

    /// <summary>
    /// Where in the content stream each open structural sequence began, or <see cref="NotRemovable"/>
    /// for one that is to be written whether or not anything is drawn inside it.
    /// </summary>
    readonly Stack<int> _markedContentStarts = new Stack<int>();

    const int NotRemovable = -1;

    /// <summary>
    /// Opens an artifact sequence: content that is on the page but is not part of what the page
    /// says — a running head, a folio, a rule, the shading behind a table.
    /// </summary>
    /// <remarks>
    /// This matters as much as tagging the real content does. Everything on a page is either
    /// content or an artifact, and something that is neither is a PDF/UA failure, so marking the
    /// furniture is half the rule rather than a tidiness measure.
    /// </remarks>
    internal void BeginArtifact()
    {
        BeginPage();
        BeginGraphicMode();
        _artifactStarts.Push(_content.Length);
        _content.Append(ArtifactPrologue);
    }

    /// <summary>
    /// Closes the innermost marked-content sequence.
    /// </summary>
    /// <summary>
    /// Closes the innermost structural sequence, or removes it when nothing was drawn inside it.
    /// </summary>
    /// <returns>
    /// True when an <c>EMC</c> was written, false when the sequence was taken back instead. The
    /// caller needs to know because a sequence that was never written cannot have been the one a
    /// suspended parent is resuming after.
    /// </returns>
    internal bool EndMarkedContent()
    {
        BeginPage();
        BeginGraphicMode();

        if (_markedContentStarts.Count > 0)
        {
            var start = _markedContentStarts.Pop();
            if (start != NotRemovable)
            {
                var opened = _content.ToString(start, _content.Length - start);
                if (opened.IndexOf("BDC\n", StringComparison.Ordinal) == opened.Length - 4)
                {
                    _content.Length = start;
                    return false;
                }
            }
        }

        _content.Append("EMC\n");
        return true;
    }

    /// <summary>
    /// Closes the innermost artifact sequence, or takes it back if nothing was drawn inside it.
    /// </summary>
    /// <remarks>
    /// Taking it back matters because of who opens these. Automatic tagging wraps the decoration of
    /// every paragraph — its shading and its borders — in an artifact scope, and the overwhelming
    /// majority of paragraphs have neither, so an empty <c>/Artifact BMC EMC</c> pair per paragraph
    /// would be the single largest thing tagging added to a document and would mean nothing.
    /// <para>
    /// Safe because the test is exact. If anything at all was appended since the <c>BMC</c> — even
    /// the <c>ET</c> that closing a text object writes — the lengths differ and the pair stands. So
    /// a scope is only ever rewound when the bytes between its ends are none.
    /// </para>
    /// </remarks>
    internal void EndArtifact()
    {
        BeginPage();
        BeginGraphicMode();

        if (_artifactStarts.Count > 0)
        {
            var start = _artifactStarts.Pop();
            if (start + ArtifactPrologue.Length == _content.Length)
            {
                _content.Length = start;
                return;
            }
        }

        _content.Append("EMC\n");
    }

    const string ArtifactPrologue = "/Artifact BMC\n";

    /// <summary>
    /// Where each open artifact sequence began in the content, so that an empty one can be undone.
    /// </summary>
    readonly Stack<int> _artifactStarts = new Stack<int>();

    internal void BeginGraphicMode()
    {
        if (_streamMode != StreamMode.Graphic)
        {
            if (_streamMode == StreamMode.Text)
                _content.Append("ET\n");

            _streamMode = StreamMode.Graphic;
        }
    }

    /// <summary>
    /// Begins the graphic mode (i.e. ends the text mode).
    /// </summary>
    internal void BeginTextMode()
    {
        if (_streamMode != StreamMode.Text)
        {
            _streamMode = StreamMode.Text;
            _content.Append("BT\n");
            // Text matrix is empty after BT
            _gfxState.RealizedTextPosition = new XPoint();
            _gfxState.RealizedTextSkew = 0;
        }
    }

    StreamMode _streamMode;

    /// <summary>
    /// Makes the specified pen and brush to the current graphics objects.
    /// </summary>
    private void Realize(XPen pen, XBrush brush)
    {
        BeginPage();
        BeginGraphicMode();
        RealizeTransform();

        if (pen != null)
            _gfxState.RealizePen(pen, ColorMode); // page.document.Options.ColorMode);

        if (brush != null)
        {
            // Render mode is 0 except for bold simulation.
            _gfxState.RealizeBrush(brush, ColorMode, 0, 0); // page.document.Options.ColorMode);
        }
    }

    /// <summary>
    /// Makes the specified pen to the current graphics object.
    /// </summary>
    void Realize(XPen pen)
    {
        Realize(pen, null);
    }

    /// <summary>
    /// Makes the specified font and brush to the current graphics objects.
    /// </summary>
    void Realize(XFont font, XBrush brush, XPen pen, bool boldSimulation, XStringFormat format)
    {
        BeginPage();
        RealizeTransform();
        BeginTextMode();
        _gfxState.RealizeFont(font, brush, pen, boldSimulation, format);
    }

    /// <summary>
    /// Encodes a run of glyph identifiers as the hexadecimal string a Tj or a TJ array takes.
    /// </summary>
    static string GlyphRunToHexString(string glyphs)
    {
        var bytes = PdfEncoders.RawUnicodeEncoding.GetBytes(glyphs);
        bytes = PdfEncoders.FormatStringLiteral(bytes, true, false, true, null);
        return PdfEncoders.RawEncoding.GetString(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// The show-text operators for a whole string, one segment at a time in the order the segments
    /// are drawn.
    /// </summary>
    /// <remarks>
    /// PDF has no notion of direction: a show-text operator paints glyphs at the pen and moves the
    /// pen along. So drawing the segments back to back in visual order is all reordering takes, and
    /// a string of one segment - which is nearly all of them - produces exactly the operator it
    /// produced before there were segments at all.
    /// </remarks>
    static string ShowTextOperators(string text, ShapedText shaped, XFont font, XStringFormat format)
    {
        var segments = shaped.Segments;
        if (segments.Count == 1)
            return SegmentOperators(segments[0].TextIn(text), segments[0].Run, font, format);

        var parts = new StringBuilder();
        for (var idx = 0; idx < segments.Count; idx++)
        {
            if (idx > 0)
                parts.Append('\n');
            parts.Append(SegmentOperators(segments[idx].TextIn(text), segments[idx].Run, font, format));
        }

        return parts.ToString();
    }

    /// <summary>
    /// The show-text operators for a string some of which is drawn with another face, with the
    /// <c>Tf</c> that selects each face written between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selecting a font does not move the pen, so a <c>Tf</c> can go between two show operators and
    /// the second carries on exactly where the first stopped. That is the whole mechanism: the
    /// segments are already in the order they are drawn and already carry the face each was shaped
    /// against.
    /// </para>
    /// <para>
    /// The face the caller asked for is selected again at the end, whether or not it was the last
    /// one used, so that <see cref="PdfGraphicsState"/> goes on being right about what the content
    /// stream has selected. The alternative - telling the graphics state what happened - is one
    /// more thing to keep in step and buys one <c>Tf</c> on the rare string that needed a fallback
    /// at all.
    /// </para>
    /// <para>
    /// Style simulation <em>is</em> reconsidered per face, which is why the rendering mode and the
    /// character spacing are written per segment here. A caller whose primary face has no bold file
    /// gets bold simulated - stroked and widened - but a fallback that has a real bold does not,
    /// and used to be stroked and widened anyway for no better reason than that the face beside it
    /// needed it. The stroking colour and width are realized once, before any of this, for whichever
    /// faces turn out to need them; only the mode and the spacing vary here, and both are text state
    /// that costs one short operator to change.
    /// </para>
    /// <para>
    /// Both are put back to what <see cref="PdfGraphicsState"/> believes before this returns, for
    /// the same reason the face is: this writes into the middle of a content stream that the
    /// graphics state goes on keeping track of, and a state that has been lied to writes the wrong
    /// thing for the next string rather than for this one, which is far harder to find.
    /// <see cref="Drawing.FontHelper.BoldSimulationSpacing"/> is the one rule the measuring path
    /// reads too, so a line is drawn at the width it was laid out at.
    /// </para>
    /// </remarks>
    string FallenBackTextOperators(string text, ShapedText shaped, XFont font, XBrush brush,
        XPen pen, XStringFormat format)
    {
        const string sizeFormat = Config.SignificantFigures3;
        const string numberFormat = Config.SignificantFigures3;

        var parts = new StringBuilder();
        var selected = font;

        // What the graphics state believes the stream has been told, and so what has to be true
        // again by the end of it.
        var stateMode = _gfxState.RealizedRenderingMode;
        var stateCharSpace = _gfxState.RealizedCharSpace;
        var mode = stateMode;
        var charSpace = stateCharSpace;

        foreach (var segment in shaped.Segments)
        {
            var name = GetFontName(segment.Font, out var pdfFont);

            // Whether this face is having its boldness simulated, which decides both whether its
            // glyphs are stroked as well as filled and how far apart they sit.
            var wantedMode = PdfGraphicsState.TextRenderingMode(
                brush, pen, FontHelper.SimulatesBold(segment.Font));
            var wantedCharSpace =
                format.CharacterSpacing + FontHelper.BoldSimulationSpacing(segment.Font);

            if (wantedMode != mode)
            {
                parts.AppendFormat(CultureInfo.InvariantCulture, "{0} Tr\n", wantedMode);
                mode = wantedMode;
            }

            #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
            if (wantedCharSpace != charSpace)
            #pragma warning restore S1244
            {
                parts.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:" + numberFormat + "} Tc\n", wantedCharSpace);
                charSpace = wantedCharSpace;
            }

            if (!ReferenceEquals(segment.Font, selected))
            {
                parts.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1:" + sizeFormat + "} Tf\n", name, segment.Font.Size);
                selected = segment.Font;
            }

            var of = segment.TextIn(text);
            pdfFont.AddShapedRun(segment.Run, of);
            parts.Append(SegmentOperators(of, segment.Run, segment.Font, format));
            parts.Append('\n');
        }

        if (mode != stateMode)
            parts.AppendFormat(CultureInfo.InvariantCulture, "{0} Tr\n", stateMode);

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (charSpace != stateCharSpace)
        #pragma warning restore S1244
        {
            parts.AppendFormat(CultureInfo.InvariantCulture,
                "{0:" + numberFormat + "} Tc\n", stateCharSpace);
        }

        if (!ReferenceEquals(selected, font))
        {
            parts.AppendFormat(CultureInfo.InvariantCulture,
                "{0} {1:" + sizeFormat + "} Tf", GetFontName(font, out _), font.Size);
        }

        return parts.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// The show-text operators for one shaped run: the glyphs, the room a word spacing asks for
    /// after every space, and the displacements a shaper asked for to place its glyphs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run with nothing to say beyond its glyphs is still a plain <c>Tj</c>, which is what every
    /// run was before there was a shaper and what nearly every run still is. The rest of this is
    /// what the other cases cost.
    /// </para>
    /// <para>
    /// A number in a TJ array moves the pen back by n/1000 of the font size. That one mechanism
    /// pays for both of the horizontal cases: the number that buys one word spacing is
    /// <c>-wordSpacing * 1000 / size</c>, and a glyph a shaper wants drawn <c>dx</c> to the right
    /// of the pen is written as <c>-dx</c> before it and <c>+dx</c> after, so that it is displaced
    /// without the run growing. The horizontal scaling multiplies that displacement and the one Tw
    /// produces alike, so it cancels and is not compensated for here.
    /// </para>
    /// <para>
    /// A vertical displacement has no such mechanism, and needs <c>Ts</c>, which is text state
    /// rather than an operand - so a run whose glyphs sit at different heights has to be shown in
    /// one piece per height. <c>Ts</c> is <b>not</b> zero on entry:
    /// <see cref="PdfGraphicsState.RealizeFont"/> has already written
    /// <see cref="XStringFormat.TextRise"/> there and believes it is still there. So a glyph a
    /// shaper wants raised is raised from that rise rather than from the baseline, and the rise is
    /// put back to it rather than to zero - otherwise a superscript containing an attached mark
    /// draws the mark at the wrong height, and every string drawn after it loses its rise
    /// altogether because the graphics state has been lied to.
    /// </para>
    /// </remarks>
    static string SegmentOperators(string text, ShapedRun run, XFont font, XStringFormat format)
    {
        var ligature = FirstLigature(text, run, 0);

        // Nothing swallowed anything, which is every run of every document written before there was
        // a shaper and nearly every run written since. Straight through, byte for byte as before.
        if (ligature < 0)
            return PlacedOperators(text, run, font, format, 0, run.Glyphs.Count);

        return LigatureOperators(text, run, font, format, ligature);
    }

    /// <summary>
    /// The operators for a run some of whose glyphs stand for more than one character, with each such
    /// glyph wrapped in a marked-content sequence saying which characters it stands for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>/ToUnicode</c> already says the same thing, and says it to a text extractor. This says it
    /// to everything that reads marked content instead — which is what assistive technology reads,
    /// and what PDF/UA asks for. The two have to agree, so both are answered from
    /// <see cref="TextShaping.CharactersOf"/>.
    /// </para>
    /// <para>
    /// <b>The sequence stays inside the text object</b>, which is the opposite of what
    /// <see cref="BeginMarkedContent"/> does and is deliberate. A structural sequence carries an
    /// <c>/MCID</c> and has to be able to contain a whole text object, so it is written in graphic
    /// mode. This one carries no identifier and is not a structure element at all: it has to wrap one
    /// show-text operator and nothing else, because what it claims is true of one glyph. Ending the
    /// text object around each ligature would also restart the pen from the origin, which is the trap
    /// the tagging path already documents.
    /// </para>
    /// <para>
    /// A ligature is not always one glyph, so the span covers every glyph sharing the cluster. And the
    /// glyphs either side of it are shown separately, which costs one show-text operator per ligature
    /// rather than one per run — the price of saying anything at all about a glyph in the middle.
    /// </para>
    /// </remarks>
    static string LigatureOperators(string text, ShapedRun run, XFont font, XStringFormat format, int first)
    {
        var shaped = run.Glyphs;
        var parts = new StringBuilder();
        var at = 0;

        for (var ligature = first; ligature >= 0; ligature = FirstLigature(text, run, at))
        {
            // Whatever lies between the last ligature and this one is shown as it always was.
            if (ligature > at)
            {
                parts.Append(PlacedOperators(text, run, font, format, at, ligature));
                parts.Append('\n');
            }

            var end = ClusterEnd(run, ligature);

            // Null for the security handler: a string inside a content stream is not encrypted on its
            // own, because the stream around it already is.
            parts.Append("/Span <</ActualText ");
            parts.Append(PdfEncoders.ToStringLiteral(
                LigatureTextOf(run, ligature, text), PdfStringEncoding.Unicode, null));
            parts.Append(">> BDC\n");
            parts.Append(PlacedOperators(text, run, font, format, ligature, end));
            parts.Append("\nEMC\n");

            at = end;
        }

        if (at < shaped.Count)
            parts.Append(PlacedOperators(text, run, font, format, at, shaped.Count));

        return parts.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// The first glyph at or after <paramref name="from"/> that swallowed a character — that is, whose
    /// cluster is drawn with fewer glyphs than it has characters — or -1 if there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fewer glyphs than characters is the whole test</b>, and neither half of it alone will do.
    /// Counting only the characters calls every cluster of more than one character a ligature, and the
    /// commonest such cluster is a base and the marks attached to it: Devanagari <c>कि</c> and an
    /// Arabic letter carrying a vowel sign are each two characters drawn with two glyphs, nothing
    /// swallowed anything, and <c>/ToUnicode</c> already says what each glyph stands for. Wrapping
    /// those would put a marked-content sequence and its own show-text operator around every syllable
    /// of a page of Hindi, where the run used to be a single <c>Tj</c>.
    /// </para>
    /// <para>
    /// Whole clusters are stepped over rather than single glyphs, so that what comes back is always
    /// the first glyph of its cluster. Reported from the middle of one, the span that follows would
    /// cover the tail of a ligature and leave its head outside — saying of some of the glyphs what is
    /// only true of all of them together.
    /// </para>
    /// </remarks>
    static int FirstLigature(string text, ShapedRun run, int from)
    {
        var shaped = run.Glyphs;
        for (var idx = from; idx < shaped.Count;)
        {
            var end = ClusterEnd(run, idx);
            if (LigatureTextOf(run, idx, text).Length > end - idx)
                return idx;

            idx = end;
        }

        return -1;
    }

    /// <summary>
    /// One past the last glyph sharing the cluster of the glyph at <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// Equality rather than an ordering, so that it holds for a right-to-left run too: those glyphs
    /// come back in visual order and their clusters descend.
    /// </remarks>
    static int ClusterEnd(ShapedRun run, int from)
    {
        var shaped = run.Glyphs;
        var end = from + 1;
        while (end < shaped.Count && shaped[end].Cluster == shaped[from].Cluster)
            end++;

        return end;
    }

    /// <summary>
    /// What a glyph would have to report as its <c>/ActualText</c>: the characters of its cluster,
    /// without the joining controls among them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controls come out for two reasons that point the same way. U+200C and U+200D are zero width
    /// by definition and <c>TextShaping.Unshaped</c> already draws no glyph for either, so a
    /// reader told that a glyph spells "letter, zero-width joiner" is being told about a character
    /// nothing on the page stands for.
    /// </para>
    /// <para>
    /// And a joining control is what makes a cluster span two characters without anything having been
    /// ligated: a letter followed by one is a single letter that was told how to join, not a pair that
    /// became one glyph. Counting it would wrap most of a word of Arabic in a sequence claiming a
    /// ligature that is not there — and would split a run that has been one show-text operator since
    /// before there was a shaper.
    /// </para>
    /// </remarks>
    static string LigatureTextOf(ShapedRun run, int index, string text)
    {
        var characters = TextShaping.CharactersOf(run, index, text);

        var controls = 0;
        for (var idx = 0; idx < characters.Length; idx++)
        {
            if (Text.UnicodeProperties.IsJoiningControl(characters[idx]))
                controls++;
        }

        if (controls == 0)
            return characters;

        var kept = new StringBuilder(characters.Length - controls);
        for (var idx = 0; idx < characters.Length; idx++)
        {
            if (!Text.UnicodeProperties.IsJoiningControl(characters[idx]))
                kept.Append(characters[idx]);
        }

        return kept.ToString();
    }

    /// <summary>
    /// Glyphs <paramref name="from"/> up to <paramref name="to"/> of a run, with the room a word
    /// spacing asks for and the displacements the shaper asked for.
    /// </summary>
    static string PlacedOperators(string text, ShapedRun run, XFont font, XStringFormat format,
        int from, int to)
    {
        var glyphs = TextShaping.GlyphIds(run);
        Debug.Assert(run.Glyphs.Count == glyphs.Length, "One character of the glyph run per glyph.");

        var shaped = run.Glyphs;
        var fontSize = font.Size;

        // A font of no size has no displacement to divide into, and nothing to show either.
        var wordSpacing = fontSize > 0 && PdfGraphicsState.NeedsWordSpacingByHand(font, format)
            ? format.WordSpacing
            : 0;

        bool displacedSideways = false, displacedUpwards = false;
        for (var idx = from; idx < to; idx++)
        {
            displacedSideways |= shaped[idx].OffsetX != 0;
            displacedUpwards |= shaped[idx].OffsetY != 0;
        }

        if (wordSpacing == 0 && !displacedSideways && !displacedUpwards)
            return GlyphRunToHexString(glyphs.Substring(from, to - from)) + " Tj";

        if (!displacedUpwards || fontSize <= 0)
            return ShownGlyphs(text, run, glyphs, fontSize, wordSpacing, from, to);

        // Where the graphics state already has the rise, and where it has to be left.
        var baseline = format.TextRise;

        var parts = new StringBuilder();
        var realized = baseline;
        var start = from;
        while (start < to)
        {
            var rise = Rise(shaped[start], run, fontSize, baseline);
            var end = start + 1;
            #pragma warning disable S1244 // Exact on purpose: groups glyphs whose rise came out of the same arithmetic.
            while (end < to && Rise(shaped[end], run, fontSize, baseline) == rise)
                end++;
            #pragma warning restore S1244

            #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
            if (rise != realized)
            #pragma warning restore S1244
            {
                parts.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:" + Config.SignificantFigures4 + "} Ts\n", rise);
                realized = rise;
            }

            parts.Append(ShownGlyphs(text, run, glyphs, fontSize, wordSpacing, start, end));
            parts.Append('\n');
            start = end;
        }

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (realized != baseline)
            parts.AppendFormat(CultureInfo.InvariantCulture,
                "{0:" + Config.SignificantFigures4 + "} Ts", baseline);
        #pragma warning restore S1244

        return parts.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// How far above the baseline a glyph is drawn, in points: the rise the text state is already
    /// at plus whatever the shaper asked for on top of it.
    /// </summary>
    static double Rise(ShapedGlyph glyph, ShapedRun run, double fontSize, double baseline)
        => baseline + glyph.OffsetY * fontSize / run.UnitsPerEm;

    /// <summary>
    /// Glyphs <paramref name="from"/> up to <paramref name="to"/>, as a Tj if nothing has to be
    /// displaced between them and as a TJ array if something does.
    /// </summary>
    static string ShownGlyphs(string text, ShapedRun run, string glyphs, double fontSize,
        double wordSpacing, int from, int to)
    {
        var shaped = run.Glyphs;
        var emUnits = 1000.0 / run.UnitsPerEm;
        var wordAdjustment = fontSize > 0 ? -wordSpacing * 1000 / fontSize : 0;

        var tj = new StringBuilder("[");
        var pending = from;
        var adjusted = false;

        void Show(int end)
        {
            if (end > pending)
                tj.Append(GlyphRunToHexString(glyphs.Substring(pending, end - pending)));
            pending = end;
        }

        void Move(double amount)
        {
            tj.Append(' ');
            tj.Append(amount.ToString(Config.SignificantFigures3, CultureInfo.InvariantCulture));
            tj.Append(' ');
            adjusted = true;
        }

        for (var idx = from; idx < to; idx++)
        {
            var sideways = shaped[idx].OffsetX * emUnits;

            // Rightwards before the glyph, and back again after it, so that the displacement does
            // not carry into whatever follows.
            if (sideways != 0)
            {
                Show(idx);
                Move(-sideways);
            }

            var after = sideways;
            if (wordAdjustment != 0 && IsLastGlyphOfWordSpace(text, shaped, idx))
                after += wordAdjustment;

            if (after != 0)
            {
                Show(idx + 1);
                Move(after);
            }
        }

        Show(to);

        #pragma warning disable S2583 // adjusted is set by the local function Move, which the analysis does not follow.
        if (!adjusted)
        #pragma warning restore S2583
        {
            // Nothing needed moving after all, so the array would only be a longer way of saying
            // the same thing.
            return GlyphRunToHexString(glyphs.Substring(from, to - from)) + " Tj";
        }

        // A run ending on a displacement leaves a space behind it, which is legal and untidy.
        if (tj[^1] == ' ')
            tj.Length--;

        tj.Append("] TJ");
        return tj.ToString();
    }

    /// <summary>
    /// Whether the glyph at <paramref name="index"/> is the last one drawn for a character a word
    /// spacing is paid out for - which is where the extra room goes.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="ShapedGlyph.Cluster"/> rather than by indexing the text, because there
    /// is no longer one glyph per character and a ligature earlier in the string would put the
    /// room inside the wrong word. Several glyphs may share the space's cluster, and the room goes
    /// after the last of them rather than inside the group.
    /// </remarks>
    static bool IsLastGlyphOfWordSpace(string text, IReadOnlyList<ShapedGlyph> shaped, int index)
    {
        var cluster = shaped[index].Cluster;
        if (cluster < 0 || cluster >= text.Length || !IsWordSpace(text[cluster]))
            return false;

        return index + 1 >= shaped.Count || shaped[index + 1].Cluster != cluster;
    }

    /// <summary>
    /// True for the characters a word spacing is paid out for. Kept in step with
    /// FontHelper.MeasureString, which maps a tab to a space before it counts one.
    /// </summary>
    static bool IsWordSpace(char ch) => ch == ' ' || ch == '\t';

    /// <summary>
    /// PDFsharp uses the Td operator to set the text position. Td just sets the offset of the text matrix
    /// and produces lesser code as Tm.
    /// </summary>
    /// <param name="pos">The absolute text position.</param>
    /// <param name="dy">The dy.</param>
    /// <param name="skew">
    /// How far the text matrix currently leans, as the tangent of the angle, or 0 for upright
    /// text. Td's offset is taken through that lean, so it has to be corrected for it.
    /// </param>
    void AdjustTdOffset(ref XPoint pos, double dy, double skew)
    {
        pos.Y += dy;
        // Reference: TABLE 5.5  Text-positioning operators / Page 406
        var posSave = pos;
        // Map from absolute to relative position.
        pos = pos - new XVector(_gfxState.RealizedTextPosition.X, _gfxState.RealizedTextPosition.Y);
        if (skew != 0)
        {
            // A leaning text matrix carries the Td offset sideways by the height it moves through,
            // so that much has to come off the offset for the text to land where it was asked for.
            pos.X -= skew * pos.Y;
        }
        _gfxState.RealizedTextPosition = posSave;
    }

    /// <summary>
    /// Makes the specified image to the current graphics object.
    /// </summary>
    string Realize(XImage image)
    {
        BeginPage();
        BeginGraphicMode();
        RealizeTransform();

        // The transparency set for a brush also applies to images. Set opacity to 100% so image will be drawn without transparency.
        _gfxState.RealizeNonStrokeTransparency(1, ColorMode);

        var form = image as XForm;
        return form != null ? GetFormName(form) : GetImageName(image);
    }

    /// <summary>
    /// Realizes the current transformation matrix, if necessary.
    /// </summary>
    void RealizeTransform()
    {
        BeginPage();

        if (_gfxState.Level == GraphicsStackLevelPageSpace)
        {
            BeginGraphicMode();
            SaveState();
        }

        //if (gfxState.MustRealizeCtm)
        if (!_gfxState.UnrealizedCtm.IsIdentity)
        {
            BeginGraphicMode();
            _gfxState.RealizeCtm();
        }
    }

    /// <summary>
    /// Convert a point from Windows world space to PDF world space.
    /// </summary>
    internal XPoint WorldToView(XPoint point)
    {
        // If EffectiveCtm is not yet realized InverseEffectiveCtm is invalid.
        Debug.Assert(_gfxState.UnrealizedCtm.IsIdentity, "Somewhere a RealizeTransform is missing.");
        // See in #else case why this is correct.
        var pt = _gfxState.WorldTransform.Transform(point);
        return _gfxState.InverseEffectiveCtm.Transform(new XPoint(pt.X, PageHeightPt / DefaultViewMatrix.M22 - pt.Y));
    }
    #endregion

    /// <summary>
    /// Gets the owning PdfDocument of this page or form.
    /// </summary>
    internal PdfDocument Owner
    {
        get
        {
            if (Page != null)
                return Page.Owner;
            return Form.Owner;
        }
    }

    internal XGraphics Gfx => _gfx;

    /// <summary>
    /// Gets the PdfResources of this page or form.
    /// </summary>
    internal PdfResources Resources
    {
        get
        {
            if (Page != null)
                return Page.Resources;
            return Form.Resources;
        }
    }

    /// <summary>
    /// Gets the resource name of the specified font within this page or form.
    /// </summary>
    internal string GetFontName(XFont font, out PdfFont pdfFont)
    {
        if (Page != null)
            return Page.GetFontName(font, out pdfFont);
        return Form.GetFontName(font, out pdfFont);
    }

    /// <summary>
    /// Gets the resource name of the specified image within this page or form.
    /// </summary>
    internal string GetImageName(XImage image)
    {
        if (Page != null)
            return Page.GetImageName(image);
        return Form.GetImageName(image);
    }

    /// <summary>
    /// Gets the resource name of the specified form within this page or form.
    /// </summary>
    internal string GetFormName(XForm form)
    {
        if (Page != null)
            return Page.GetFormName(form);
        return Form.GetFormName(form);
    }

    internal PdfPage Page;
    internal XForm Form;
    internal PdfColorMode ColorMode;
    XGraphicsPdfPageOptions _options;
    XGraphics _gfx;
    readonly StringBuilder _content;

    /// <summary>
    /// The q/Q nesting level is 0.
    /// </summary>
    const int GraphicsStackLevelInitial = 0;

    /// <summary>
    /// The q/Q nesting level is 1.
    /// </summary>
    const int GraphicsStackLevelPageSpace = 1;

    /// <summary>
    /// The q/Q nesting level is 2.
    /// </summary>
    const int GraphicsStackLevelWorldSpace = 2;

    #region PDF Graphics State

    /// <summary>
    /// Saves the current graphical state.
    /// </summary>
    void SaveState()
    {
        Debug.Assert(_streamMode == StreamMode.Graphic, "Cannot save state in text mode.");

        _gfxStateStack.Push(_gfxState);
        _gfxState = _gfxState.Clone();
        _gfxState.Level = _gfxStateStack.Count;
        Append("q\n");
    }

    /// <summary>
    /// Restores the previous graphical state.
    /// </summary>
    void RestoreState()
    {
        Debug.Assert(_streamMode == StreamMode.Graphic, "Cannot restore state in text mode.");

        _gfxState = _gfxStateStack.Pop();
        Append("Q\n");
    }

    /// <summary>
    /// Restores the graphical state saved for the given <see cref="InternalGraphicsState"/>, closing
    /// every state saved after it on the way.
    /// </summary>
    /// <remarks>
    /// The frame is found by reference identity against the cross-link <see cref="Save"/> and
    /// <see cref="BeginContainer"/> write. No caller can drive that search past the bottom of the
    /// stack: <see cref="GraphicsStateStack.Restore"/> refuses a handle that is not on
    /// <see cref="XGraphics"/>'s own stack before this renderer is called at all. So running out of
    /// frames means the two stacks have been pushed out of lockstep by a change inside this class,
    /// and saying that is worth more than the bare "Stack empty." <see cref="Stack{T}"/> would throw
    /// from a frame naming nothing.
    /// </remarks>
    void RestoreState(InternalGraphicsState state)
    {
        var top = Pop();
        while (top.InternalState != state)
        {
            Append("Q\n");
            top = Pop();
        }
        Append("Q\n");
        _gfxState = top;

        PdfGraphicsState Pop() =>
            _gfxStateStack.Count != 0
                ? _gfxStateStack.Pop()
                : throw new InvalidOperationException(
                    "The two graphics state stacks are out of sync: the state being restored was " +
                    "never saved on the renderer's stack. Every push onto XGraphics's stack must be " +
                    "matched by one onto this renderer's, cross-linked in Save and BeginContainer.");
    }

    /// <summary>
    /// The current graphical state.
    /// </summary>
    PdfGraphicsState _gfxState;

    /// <summary>
    /// The graphical state stack.
    /// </summary>
    readonly Stack<PdfGraphicsState> _gfxStateStack = new();

    #endregion

    /// <summary>
    /// The height of the PDF page in point including the trim box.
    /// </summary>
    public double PageHeightPt;

    /// <summary>
    /// The final transformation from the world space to the default page space.
    /// </summary>
    public XMatrix DefaultViewMatrix;
}
