#region PDFsharp Charting - A .NET charting library based on PDFsharp
//
// Authors:
//   Niklas Schneider (mailto:Niklas.Schneider@PdfPinata.com)
//
// Copyright (c) 2005-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
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
using System.Globalization;
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the base class for all X axis renderer, and - since a horizontal and a vertical
/// category axis compute the same things and differ only in which page coordinate a value
/// becomes - the one place both are drawn from. <see cref="orientation"/> says which axis this is.
/// </summary>
internal abstract class XAxisRenderer : AxisRenderer
{
  /// <summary>
  /// Initializes a new instance of the XAxisRenderer class with the specified renderer
  /// parameters and orientation.
  /// </summary>
  internal XAxisRenderer(RendererParameters parms, AxisOrientation orientation)
    : base(parms)
  {
    this.orientation = orientation;
    this.isHorizontal = orientation == AxisOrientation.Horizontal;
  }

  readonly AxisOrientation orientation;

  /// <summary>
  /// Whether this is the horizontal axis, cached once rather than compared for on every one of
  /// the several places <see cref="Draw"/> and <see cref="Format"/> branch on it.
  /// </summary>
  readonly bool isHorizontal;

  /// <summary>
  /// Returns the default tick labels format string.
  /// </summary>
  protected override string GetDefaultTickLabelsFormat()
  {
    return "0";
  }

  /// <summary>
  /// Returns an initialized rendererInfo based on the X axis.
  /// </summary>
  internal override RendererInfo Init()
  {
    Chart chart = (Chart)this.rendererParms.DrawingItem;

    AxisRendererInfo xari = new AxisRendererInfo();
    xari.axis = chart.xAxis;

    // Outside the test below, as the Y axis renderers calculate their scale outside theirs. The
    // scale is what the plot area divides its own width by, so a chart that was never asked for an
    // Axis object - Chart.XAxis creates one on first read - would otherwise be laid out against a
    // maximum of zero and drawn at infinite coordinates. What depends on the axis existing is the
    // labelling, not the scale.
    CalculateXAxisValues(chart, xari);

    if (xari.axis != null)
    {
      ChartRendererInfo cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

      // The two orientations used to call these in different orders. The horizontal one needed
      // its own order, because InitXValues formats the default category labels with
      // TickLabelsFormat, which InitTickLabels sets; the vertical one formats them with the
      // invariant culture instead and so never depended on the order it was called in. Each is
      // kept exactly as it was rather than merged into one, since only the tick-mark pens are
      // this merge's intended behaviour change - see docs/specs/axis-renderer-duplication.md.
      if (isHorizontal)
      {
        InitTickLabels(xari, cri.DefaultFont);
        InitXValues(xari);
        InitAxisTitle(xari, cri.DefaultFont);
      }
      else
      {
        InitXValues(xari);
        InitAxisTitle(xari, cri.DefaultFont);
        InitTickLabels(xari, cri.DefaultFont);
      }
      InitAxisLineFormat(xari);
      InitGridlines(xari);
    }
    return xari;
  }

  /// <summary>
  /// Calculates the space used for the X axis.
  /// </summary>
  internal override void Format()
  {
    AxisRendererInfo xari = ((ChartRendererInfo)this.rendererParms.RendererInfo).xAxisRendererInfo;
    if (xari.axis != null)
    {
      AxisTitleRendererInfo atri = xari.axisTitleRendererInfo;

      // Calculate space used for axis title, through the renderer that draws it rather than by
      // measuring the string here. Measuring it here took no account of the title's orientation,
      // so a caption turned on its side reserved the room it would have taken lying flat.
      XSize titleSize = new XSize(0, 0);
      if (atri != null && atri.AxisTitleText != null && atri.AxisTitleText.Length > 0)
      {
        RendererParameters parms = new RendererParameters();
        parms.Graphics = this.rendererParms.Graphics;
        parms.RendererInfo = xari;
        new AxisTitleRenderer(parms).Format();
        titleSize = atri.AxisTitleSize;
      }

      // Calculate space used for tick labels. The horizontal axis measures only its first
      // series - the categories are shared across series, so one is enough - while the vertical
      // axis measures every series it is given; kept as each was found rather than unified,
      // since it is not the tick-mark pens this merge is fixing.
      XSize size = new XSize(0, 0);
      if (isHorizontal)
      {
        if (xari.XValues.Count > 0)
        {
          XSeries xs = xari.XValues[0];
          foreach (XValue xv in xs)
          {
            if (xv != null)
            {
              string tickLabel = xv.Value;
              XSize valueSize = this.rendererParms.Graphics.MeasureString(tickLabel, xari.TickLabelsFont);
              size.Height = Math.Max(valueSize.Height, size.Height);
              size.Width += valueSize.Width;
            }
          }
        }

        // Remember space for later drawing.
        xari.TickLabelsHeight = size.Height;
        xari.Height = titleSize.Height + size.Height + xari.MajorTickMarkWidth;
        xari.Width = Math.Max(titleSize.Width, size.Width);
      }
      else
      {
        foreach (XSeries xs in xari.XValues)
        {
          foreach (XValue xv in xs)
          {
            // A category added with XSeries.AddBlank is a null, as it is in Draw below and as
            // the horizontal axis's own measuring already allows for.
            if (xv != null)
            {
              XSize valueSize = this.rendererParms.Graphics.MeasureString(xv.Value, xari.TickLabelsFont);
              size.Height += valueSize.Height;
              size.Width = Math.Max(valueSize.Width, size.Width);
            }
          }
        }

        // Remember space for later drawing.
        if (atri != null)
          atri.AxisTitleSize = titleSize;
        xari.TickLabelsHeight = size.Height;
        xari.Height = size.Height;
        xari.Width = titleSize.Width + size.Width + xari.MajorTickMarkWidth;
      }
    }
  }

  /// <summary>
  /// Draws the X axis.
  /// </summary>
  internal override void Draw()
  {
    XGraphics gfx = this.rendererParms.Graphics;
    ChartRendererInfo cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    AxisRendererInfo xari = cri.xAxisRendererInfo;

    double xMax = xari.MaximumScale;
    double xMajorTick = xari.MajorTick;
    double xMinorTick = xari.MinorTick;

    // Draw tick labels. Each tick label will be aligned centered.
    int countTickLabels = (int)xMax;
    XPoint startPos;

    if (isHorizontal)
    {
      double tickLabelStep = xari.Width;
      if (countTickLabels != 0)
        tickLabelStep = xari.Width / countTickLabels;

      startPos = new XPoint(xari.X + tickLabelStep / 2, xari.Y + xari.TickLabelsHeight);
      if (xari.MajorTickMark != TickMarkType.None)
        startPos.Y += xari.MajorTickMarkWidth;
      foreach (XSeries xs in xari.XValues)
      {
        for (int idx = 0; idx < countTickLabels && idx < xs.Count; ++idx)
        {
          XValue xv = xs[idx];
          if (xv != null)
          {
            string tickLabel = xv.Value;
            XSize size = gfx.MeasureString(tickLabel, xari.TickLabelsFont);
            gfx.DrawString(tickLabel, xari.TickLabelsFont, xari.TickLabelsBrush, startPos.X - size.Width / 2, startPos.Y);
          }
          startPos.X += tickLabelStep;
        }
      }
    }
    else
    {
      double tickLabelStep = xari.Height / countTickLabels;
      startPos = new XPoint(xari.X + xari.Width - xari.MajorTickMarkWidth, xari.Y + tickLabelStep / 2);
      foreach (XSeries xs in xari.XValues)
      {
        for (int idx = countTickLabels - 1; idx >= 0; --idx)
        {
          // Both conditions carried across from the horizontal orientation, which this branch
          // is otherwise a copy of. The count comes from the longest series rather than from the
          // category list, so there need not be a category at every index; and a category added
          // with XSeries.AddBlank is a null. Neither is unusual enough to throw over.
          XValue xv = idx < xs.Count ? xs[idx] : null;
          if (xv != null)
          {
            string tickLabel = xv.Value;
            XSize size = gfx.MeasureString(tickLabel, xari.TickLabelsFont);
            gfx.DrawString(tickLabel, xari.TickLabelsFont, xari.TickLabelsBrush, startPos.X - size.Width, startPos.Y + size.Height / 2);
          }
          startPos.Y += tickLabelStep;
        }
      }
    }

    // Draw axis.
    // First draw tick marks, second draw axis.
    double majorTickMarkStart = 0, majorTickMarkEnd = 0,
      minorTickMarkStart = 0, minorTickMarkEnd = 0;
    GetTickMarkPos(xari, ref majorTickMarkStart, ref majorTickMarkEnd, ref minorTickMarkStart, ref minorTickMarkEnd);

    // The axis line itself is still stroked from LineFormat, but the tick marks now read the
    // pens the base class already computes for every axis - the fix this merge exists to make.
    // Before it, this orientation stroked its ticks with LineFormat too, which is null until a
    // caller sets one, so a category axis with no line format drew no tick marks at all.
    LineFormatRenderer lineFormatRenderer = new LineFormatRenderer(gfx, xari.LineFormat);
    LineFormatRenderer minorTickMarkLineFormat = new LineFormatRenderer(gfx, xari.MinorTickMarkLineFormat);
    LineFormatRenderer majorTickMarkLineFormat = new LineFormatRenderer(gfx, xari.MajorTickMarkLineFormat);
    XPoint[] points = new XPoint[2];

    // Minor ticks.
    if (xari.MinorTickMark != TickMarkType.None)
    {
      int countMinorTickMarks = (int)(xMax / xMinorTick);
      if (isHorizontal)
      {
        // The same guard the major ticks take just below: a chart with nothing plotted scales to
        // a maximum of zero, and dividing the axis length by zero minor ticks turned every tick
        // position into NaN. Only reachable when a caller sets Axis.MinorTickMark explicitly -
        // it defaults to None, which the block above already skips - but reachable all the same.
        double minorTickMarkStep = xari.Width;
        if (countMinorTickMarks != 0)
          minorTickMarkStep = xari.Width / countMinorTickMarks;
        startPos.X = xari.X;
        for (int x = 0; x <= countMinorTickMarks; x++)
        {
          points[0].X = startPos.X + minorTickMarkStep * x;
          points[0].Y = minorTickMarkStart;
          points[1].X = points[0].X;
          points[1].Y = minorTickMarkEnd;
          minorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }
      }
      else
      {
        double minorTickMarkStep = xari.Height;
        if (countMinorTickMarks != 0)
          minorTickMarkStep = xari.Height / countMinorTickMarks;
        startPos.Y = xari.Y;
        for (int x = 0; x <= countMinorTickMarks; x++)
        {
          points[0].X = minorTickMarkStart;
          points[0].Y = startPos.Y + minorTickMarkStep * x;
          points[1].X = minorTickMarkEnd;
          points[1].Y = points[0].Y;
          minorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }
      }
    }

    // Major ticks.
    if (xari.MajorTickMark != TickMarkType.None)
    {
      int countMajorTickMarks = (int)(xMax / xMajorTick);
      if (isHorizontal)
      {
        double majorTickMarkStep = xari.Width;
        if (countMajorTickMarks != 0)
          majorTickMarkStep = xari.Width / countMajorTickMarks;
        startPos.X = xari.X;
        for (int x = 0; x <= countMajorTickMarks; x++)
        {
          points[0].X = startPos.X + majorTickMarkStep * x;
          points[0].Y = majorTickMarkStart;
          points[1].X = points[0].X;
          points[1].Y = majorTickMarkEnd;
          majorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }
      }
      else
      {
        // The same guard the horizontal orientation already has: a chart with nothing plotted
        // scales to a maximum of zero, and dividing the axis length by zero major ticks turned
        // every tick position into NaN. It was unreachable before this merge, because this
        // orientation's ticks drew with a pen that was null until a caller set one; the tick-mark
        // pens fix made it reachable, and a chart with no series at all now has to survive it.
        double majorTickMarkStep = xari.Height;
        if (countMajorTickMarks != 0)
          majorTickMarkStep = xari.Height / countMajorTickMarks;
        startPos.Y = xari.Y;
        for (int x = 0; x <= countMajorTickMarks; x++)
        {
          points[0].X = majorTickMarkStart;
          points[0].Y = startPos.Y + majorTickMarkStep * x;
          points[1].X = majorTickMarkEnd;
          points[1].Y = points[0].Y;
          majorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }
      }
    }

    // Axis.
    if (xari.LineFormat != null)
    {
      if (isHorizontal)
      {
        points[0].X = xari.X;
        points[0].Y = xari.Y;
        points[1].X = xari.X + xari.Width;
        points[1].Y = xari.Y;
        if (xari.MajorTickMark != TickMarkType.None)
        {
          points[0].X -= xari.LineFormat.Width / 2;
          points[1].X += xari.LineFormat.Width / 2;
        }
      }
      else
      {
        points[0].X = xari.X + xari.Width;
        points[0].Y = xari.Y;
        points[1].X = xari.X + xari.Width;
        points[1].Y = xari.Y + xari.Height;
        if (xari.MajorTickMark != TickMarkType.None)
        {
          points[0].Y -= xari.LineFormat.Width / 2;
          points[1].Y += xari.LineFormat.Width / 2;
        }
      }
      lineFormatRenderer.DrawLine(points[0], points[1]);
    }

    // Draw axis title, through the renderer that draws it rather than by hand. Drawing it here
    // meant an axis title on this axis honoured neither its alignment nor its orientation, both
    // of which are settable and both of which the value axis has always honoured. It also meant
    // the caption was centred on half the axis's right edge instead of on the middle of the axis,
    // which is the same thing only when the axis starts at zero.
    AxisTitleRendererInfo atri = xari.axisTitleRendererInfo;
    if (atri != null && atri.AxisTitleText != null && atri.AxisTitleText.Length > 0)
    {
      if (isHorizontal)
      {
        // The strip below the tick labels, the full width of the axis, so that an alignment has
        // somewhere to move the caption to.
        atri.Rect = new XRect(xari.Rect.Left, xari.Rect.Bottom - atri.AxisTitleSize.Height,
          xari.Rect.Width, atri.AxisTitleSize.Height);
      }
      else
      {
        // The strip to the left of the tick labels, the full height of the axis, so that an
        // alignment has somewhere to move the caption to.
        atri.Rect = new XRect(xari.Rect.Left, xari.Rect.Top,
          atri.AxisTitleSize.Width, xari.Rect.Height);
      }

      RendererParameters parms = new RendererParameters();
      parms.Graphics = gfx;
      parms.RendererInfo = xari;
      new AxisTitleRenderer(parms).Draw();
    }
  }

  /// <summary>
  /// Calculates the X axis describing values like minimum/maximum scale, major/minor tick and
  /// major/minor tick mark width.
  /// </summary>
  private void CalculateXAxisValues(Chart chart, AxisRendererInfo rendererInfo)
  {
    // The chart is passed in rather than reached through rendererInfo.axis.parent, because this
    // runs for a chart that has no axis to be reached through.
    SeriesCollection seriesCollection = chart.SeriesCollection;

    // Calculates the maximum number of data points over all series.
    int count = 0;
    foreach (Series series in seriesCollection)
      count = Math.Max(count, series.Count);

    rendererInfo.MinimumScale = 0;
    rendererInfo.MaximumScale = count; // At least 0
    rendererInfo.MajorTick = 1;
    rendererInfo.MinorTick = 0.5;
    rendererInfo.MajorTickMarkWidth = DefaultMajorTickMarkWidth;
    rendererInfo.MinorTickMarkWidth = DefaultMinorTickMarkWidth;
  }

  /// <summary>
  /// Initializes the rendererInfo's xvalues. If not set by the user xvalues will be simply numbers
  /// from minimum scale + 1 to maximum scale.
  /// </summary>
  private void InitXValues(AxisRendererInfo rendererInfo)
  {
    rendererInfo.XValues = ((Chart)rendererInfo.axis.parent).xValues;
    if (rendererInfo.XValues == null)
    {
      rendererInfo.XValues = new XValues();
      XSeries xs = rendererInfo.XValues.AddXSeries();
      if (isHorizontal)
      {
        for (double i = rendererInfo.MinimumScale + 1; i <= rendererInfo.MaximumScale; ++i)
          xs.Add(i.ToString(rendererInfo.TickLabelsFormat));
      }
      else
      {
        for (double i = rendererInfo.MinimumScale + 1; i <= rendererInfo.MaximumScale; ++i)
          xs.Add(i.ToString(CultureInfo.InvariantCulture));
      }
    }
  }

  /// <summary>
  /// Calculates the starting and ending position for the minor and major tick marks, on the
  /// dimension this orientation's ticks run along.
  /// </summary>
  private void GetTickMarkPos(AxisRendererInfo rendererInfo,
    ref double majorTickMarkStart, ref double majorTickMarkEnd,
    ref double minorTickMarkStart, ref double minorTickMarkEnd)
  {
    // Outside adds the width to the edge for one orientation and subtracts it for the other -
    // the sign this flips - because the two edges face opposite ways relative to the plot area.
    double edge = isHorizontal
      ? rendererInfo.Rect.Y
      : rendererInfo.Rect.X + rendererInfo.Rect.Width;
    int direction = isHorizontal ? 1 : -1;

    GetTickMarkEndpoints(rendererInfo.MajorTickMark, edge, rendererInfo.MajorTickMarkWidth, direction,
      out majorTickMarkStart, out majorTickMarkEnd);
    GetTickMarkEndpoints(rendererInfo.MinorTickMark, edge, rendererInfo.MinorTickMarkWidth, direction,
      out minorTickMarkStart, out minorTickMarkEnd);
  }
}
