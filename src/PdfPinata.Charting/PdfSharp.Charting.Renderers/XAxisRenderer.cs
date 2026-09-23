#region Copyright
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
/// becomes - the one place both are drawn from. <c>isHorizontal</c> says which axis this is.
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
    isHorizontal = orientation == AxisOrientation.Horizontal;
  }

  /// <summary>
  /// Whether this is the horizontal axis, cached once rather than compared for on every one of
  /// the several places <see cref="Draw"/> and <see cref="Format"/> branch on it.
  /// </summary>
  private readonly bool isHorizontal;

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
    var chart = (Chart)rendererParms.DrawingItem;

    var xari = new AxisRendererInfo { Axis = chart.xAxis };

    // Outside the test below, as the Y axis renderers calculate their scale outside theirs. The
    // scale is what the plot area divides its own width by, so a chart that was never asked for an
    // Axis object - Chart.XAxis creates one on first read - would otherwise be laid out against a
    // maximum of zero and drawn at infinite coordinates. What depends on the axis existing is the
    // labelling, not the scale.
    CalculateXAxisValues(chart, xari);

    if (xari.Axis == null)
      return xari;

    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    // The two orientations used to call these in different orders. The horizontal one needed
    // its own order, because InitXValues formats the default category labels with
    // TickLabelsFormat, which InitTickLabels sets; the vertical one formats them with the
    // invariant culture instead and so never depended on the order it was called in. Each is
    // kept exactly as it was rather than merged into one, since only the tick-mark pens are
    // this merge's intended behaviour change - see docs/specs/axis-renderer-duplication.md.
    if (isHorizontal)
    {
      InitTickLabels(xari, cri.DefaultFont, cri.DefaultFontColor);
      InitXValues(xari);
      InitAxisTitle(xari, cri.DefaultFont, cri.DefaultFontColor);
    }
    else
    {
      InitXValues(xari);
      InitAxisTitle(xari, cri.DefaultFont, cri.DefaultFontColor);
      InitTickLabels(xari, cri.DefaultFont, cri.DefaultFontColor);
    }
    InitAxisLineFormat(xari);
    InitGridlines(xari);
    return xari;
  }

  /// <summary>
  /// Calculates the space used for the X axis.
  /// </summary>
  internal override void Format()
  {
    var xari = ((ChartRendererInfo)rendererParms.RendererInfo).XAxisRendererInfo;
    if (xari.Axis == null)
      return;

    var atri = xari.AxisTitleRendererInfo;

    // Calculate space used for axis title, through the renderer that draws it rather than by
    // measuring the string here. Measuring it here took no account of the title's orientation,
    // so a caption turned on its side reserved the room it would have taken lying flat.
    var titleSize = new XSize(0, 0);
    if (atri is { AxisTitleText.Length: > 0 })
    {
      var parms = new RendererParameters
      {
        Graphics = rendererParms.Graphics,
        RendererInfo = xari
      };
      new AxisTitleRenderer(parms).Format();
      titleSize = atri.AxisTitleSize;
    }

    // Calculate space used for tick labels, from the one category series the axis is labelled
    // with - see CategoryLabels. The vertical axis used to measure every series it was given,
    // though only the first was ever meant to be drawn.
    var categories = CategoryLabels(xari);
    var size = new XSize(0, 0);
    if (isHorizontal)
    {
      if (categories != null)
      {
        foreach (XValue xv in categories)
        {
          if (xv == null)
            continue;

          var tickLabel = xv.Value;
          var valueSize = rendererParms.Graphics.MeasureString(tickLabel, xari.TickLabelsFont);
          size.Height = Math.Max(valueSize.Height, size.Height);
          size.Width += valueSize.Width;
        }
      }

      // Remember space for later drawing.
      xari.TickLabelsHeight = size.Height;
      xari.Height = titleSize.Height + size.Height + xari.MajorTickMarkWidth;
      xari.Width = Math.Max(titleSize.Width, size.Width);
    }
    else
    {
      if (categories != null)
      {
        foreach (XValue xv in categories)
        {
          // A category added with XSeries.AddBlank is a null, as it is in Draw below and as
          // the horizontal axis's own measuring already allows for.
          if (xv == null)
            continue;

          var valueSize = rendererParms.Graphics.MeasureString(xv.Value, xari.TickLabelsFont);
          size.Height += valueSize.Height;
          size.Width = Math.Max(valueSize.Width, size.Width);
        }
      }

      // Remember space for later drawing.
      atri?.AxisTitleSize = titleSize;
      xari.TickLabelsHeight = size.Height;
      xari.Height = size.Height;
      xari.Width = titleSize.Width + size.Width + xari.MajorTickMarkWidth;
    }
  }

  /// <summary>
  /// Draws the X axis.
  /// </summary>
  internal override void Draw()
  {
    var gfx = rendererParms.Graphics;
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    var xari = cri.XAxisRendererInfo;

    // Each tick label will be aligned centered.
    if (isHorizontal)
      DrawHorizontalTickLabels(gfx, xari);
    else
      DrawVerticalTickLabels(gfx, xari);

    // Draw axis.
    // First draw tick marks, second draw axis.
    GetTickMarkPos(xari, out var majorTickMarkStart, out var majorTickMarkEnd, out var minorTickMarkStart, out var minorTickMarkEnd);

    // The axis line itself is still stroked from LineFormat, but the tick marks now read the
    // pens the base class already computes for every axis - the fix this merge exists to make.
    // Before it, this orientation stroked its ticks with LineFormat too, which is null until a
    // caller sets one, so a category axis with no line format drew no tick marks at all.
    var lineFormatRenderer = new LineFormatRenderer(gfx, xari.LineFormat);
    var minorTickMarkLineFormat = new LineFormatRenderer(gfx, xari.MinorTickMarkLineFormat);
    var majorTickMarkLineFormat = new LineFormatRenderer(gfx, xari.MajorTickMarkLineFormat);

    // Minor ticks.
    if (xari.MinorTickMark != TickMarkType.None)
    {
      var countMinorTickMarks = (int)(xari.MaximumScale / xari.MinorTick);
      DrawTickMarks(minorTickMarkLineFormat, xari, countMinorTickMarks, minorTickMarkStart, minorTickMarkEnd);
    }

    // Major ticks.
    if (xari.MajorTickMark != TickMarkType.None)
    {
      var countMajorTickMarks = (int)(xari.MaximumScale / xari.MajorTick);
      DrawTickMarks(majorTickMarkLineFormat, xari, countMajorTickMarks, majorTickMarkStart, majorTickMarkEnd);
    }

    // Axis.
    if (xari.LineFormat != null)
      DrawAxisLine(lineFormatRenderer, xari);

    DrawAxisTitle(gfx, xari);
  }

  /// <summary>
  /// Draws the tick labels of a horizontal axis from left to right, each centred on its slot and
  /// below the tick marks.
  /// </summary>
  private static void DrawHorizontalTickLabels(XGraphics gfx, AxisRendererInfo xari)
  {
    var countTickLabels = (int)xari.MaximumScale;
    var categories = CategoryLabels(xari);

    var tickLabelStep = xari.Width;
    if (countTickLabels != 0)
      tickLabelStep = xari.Width / countTickLabels;

    var startPos = new XPoint(xari.X + tickLabelStep / 2, xari.Y + xari.TickLabelsHeight);
    if (xari.MajorTickMark != TickMarkType.None)
      startPos.Y += xari.MajorTickMarkWidth;
    for (var idx = 0; categories != null && idx < countTickLabels && idx < categories.Count; ++idx)
    {
      var xv = categories[idx];
      if (xv != null)
      {
        var tickLabel = xv.Value;
        var size = gfx.MeasureString(tickLabel, xari.TickLabelsFont);
        gfx.DrawString(tickLabel, xari.TickLabelsFont, xari.TickLabelsBrush, startPos.X - size.Width / 2, startPos.Y);
      }
      startPos.X += tickLabelStep;
    }
  }

  /// <summary>
  /// Draws the tick labels of a vertical axis from top to bottom - the last category first - each
  /// centred on its slot and to the left of the tick marks.
  /// </summary>
  private static void DrawVerticalTickLabels(XGraphics gfx, AxisRendererInfo xari)
  {
    var countTickLabels = (int)xari.MaximumScale;
    var categories = CategoryLabels(xari);

    var tickLabelStep = xari.Height / countTickLabels;
    var startPos = new XPoint(xari.X + xari.Width - xari.MajorTickMarkWidth, xari.Y + tickLabelStep / 2);
    for (var idx = countTickLabels - 1; categories != null && idx >= 0; --idx)
    {
      // Both conditions carried across from the horizontal orientation, which this is otherwise
      // a copy of. The count comes from the longest series rather than from the category list,
      // so there need not be a category at every index; and a category added with
      // XSeries.AddBlank is a null. Neither is unusual enough to throw over.
      var xv = idx < categories.Count ? categories[idx] : null;
      if (xv != null)
      {
        var tickLabel = xv.Value;
        var size = gfx.MeasureString(tickLabel, xari.TickLabelsFont);
        gfx.DrawString(tickLabel, xari.TickLabelsFont, xari.TickLabelsBrush, startPos.X - size.Width, startPos.Y + size.Height / 2);
      }
      startPos.Y += tickLabelStep;
    }
  }

  /// <summary>
  /// Draws <paramref name="count"/> + 1 tick marks spaced evenly along the axis, each running
  /// across it from <paramref name="start"/> to <paramref name="end"/>.
  /// </summary>
  /// <remarks>
  /// The step is guarded against a count of zero, for both kinds of tick and both orientations: a
  /// chart with nothing plotted scales to a maximum of zero, and dividing the axis length by zero
  /// ticks turned every tick position into NaN. For the minor ticks that is reachable only when a
  /// caller sets Axis.MinorTickMark explicitly - it defaults to None - but reachable all the same.
  /// For the vertical orientation's major ticks it was unreachable until the tick-mark pens fix,
  /// because they drew with a pen that was null until a caller set one; that fix made it
  /// reachable, and a chart with no series at all now has to survive it.
  /// </remarks>
  private void DrawTickMarks(LineFormatRenderer lineFormatRenderer, AxisRendererInfo xari, int count, double start, double end)
  {
    var length = isHorizontal ? xari.Width : xari.Height;
    var origin = isHorizontal ? xari.X : xari.Y;

    var step = length;
    if (count != 0)
      step = length / count;

    for (var x = 0; x <= count; x++)
    {
      var along = origin + step * x;
      if (isHorizontal)
        lineFormatRenderer.DrawLine(new XPoint(along, start), new XPoint(along, end));
      else
        lineFormatRenderer.DrawLine(new XPoint(start, along), new XPoint(end, along));
    }
  }

  /// <summary>
  /// Draws the axis line along the edge that faces the plot area, lengthened at each end by half
  /// its own width when there are major tick marks, so that it covers the outermost of them.
  /// </summary>
  private void DrawAxisLine(LineFormatRenderer lineFormatRenderer, AxisRendererInfo xari)
  {
    XPoint from, to;
    if (isHorizontal)
    {
      from = new XPoint(xari.X, xari.Y);
      to = new XPoint(xari.X + xari.Width, xari.Y);
      if (xari.MajorTickMark != TickMarkType.None)
      {
        from.X -= xari.LineFormat.Width / 2;
        to.X += xari.LineFormat.Width / 2;
      }
    }
    else
    {
      from = new XPoint(xari.X + xari.Width, xari.Y);
      to = new XPoint(xari.X + xari.Width, xari.Y + xari.Height);
      if (xari.MajorTickMark != TickMarkType.None)
      {
        from.Y -= xari.LineFormat.Width / 2;
        to.Y += xari.LineFormat.Width / 2;
      }
    }
    lineFormatRenderer.DrawLine(from, to);
  }

  /// <summary>
  /// Draws the axis title, through the renderer that draws it rather than by hand.
  /// </summary>
  /// <remarks>
  /// Drawing it by hand meant an axis title on this axis honoured neither its alignment nor its
  /// orientation, both of which are settable and both of which the value axis has always
  /// honoured. It also meant the caption was centred on half the axis's right edge instead of on
  /// the middle of the axis, which is the same thing only when the axis starts at zero.
  /// </remarks>
  private void DrawAxisTitle(XGraphics gfx, AxisRendererInfo xari)
  {
    var atri = xari.AxisTitleRendererInfo;
    if (atri is not { AxisTitleText.Length: > 0 })
      return;

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

    var parms = new RendererParameters
    {
      Graphics = gfx,
      RendererInfo = xari
    };
    new AxisTitleRenderer(parms).Draw();
  }

  /// <summary>
  /// Calculates the X axis describing values like minimum/maximum scale, major/minor tick and
  /// major/minor tick mark width.
  /// </summary>
  private static void CalculateXAxisValues(Chart chart, AxisRendererInfo rendererInfo)
  {
    // The chart is passed in rather than reached through rendererInfo.Axis.parent, because this
    // runs for a chart that has no axis to be reached through.
    var seriesCollection = chart.SeriesCollection;

    // Calculates the maximum number of data points over all series.
    var count = 0;
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
  /// The category series the axis is labelled with: the first of <see cref="Chart.XValues"/>, or
  /// null when that collection is empty.
  /// </summary>
  /// <remarks>
  /// The axis has one slot per category and one row of labels, so it shows one series of
  /// categories, and a chart given several is labelled from the first - the one the pie legend
  /// reads its entries from too, and the one Excel labels an axis with when each series names
  /// categories of its own. Measuring and drawing every series in turn was empira/PDFsharp#286:
  /// the pen was never taken back to the first slot, so the second series' labels carried on
  /// past the last category, off the right of a column chart and below the foot of a bar chart.
  /// </remarks>
  private static XSeries CategoryLabels(AxisRendererInfo rendererInfo)
  {
    var xValues = rendererInfo.XValues;
    return xValues is { Count: > 0 } ? xValues[0] : null;
  }

  /// <summary>
  /// Initializes the rendererInfo's xvalues. If not set by the user xvalues will be simply numbers
  /// from minimum scale + 1 to maximum scale.
  /// </summary>
  private void InitXValues(AxisRendererInfo rendererInfo)
  {
    rendererInfo.XValues = ((Chart)rendererInfo.Axis.parent).xValues;
    if (rendererInfo.XValues != null)
      return;

    rendererInfo.XValues = new XValues();
    var xs = rendererInfo.XValues.AddXSeries();
    if (isHorizontal)
    {
      for (var i = rendererInfo.MinimumScale + 1; i <= rendererInfo.MaximumScale; ++i)
        xs.Add(i.ToString(rendererInfo.TickLabelsFormat));
    }
    else
    {
      for (var i = rendererInfo.MinimumScale + 1; i <= rendererInfo.MaximumScale; ++i)
        xs.Add(i.ToString(CultureInfo.InvariantCulture));
    }
  }

  /// <summary>
  /// Calculates the starting and ending position for the minor and major tick marks, on the
  /// dimension this orientation's ticks run along.
  /// </summary>
  private void GetTickMarkPos(AxisRendererInfo rendererInfo,
    out double majorTickMarkStart, out double majorTickMarkEnd,
    out double minorTickMarkStart, out double minorTickMarkEnd)
  {
    // Outside adds the width to the edge for one orientation and subtracts it for the other -
    // the sign this flips - because the two edges face opposite ways relative to the plot area.
    var edge = isHorizontal
      ? rendererInfo.Rect.Y
      : rendererInfo.Rect.X + rendererInfo.Rect.Width;
    var direction = isHorizontal ? 1 : -1;

    GetTickMarkEndpoints(rendererInfo.MajorTickMark, edge, rendererInfo.MajorTickMarkWidth, direction,
      out majorTickMarkStart, out majorTickMarkEnd);
    GetTickMarkEndpoints(rendererInfo.MinorTickMark, edge, rendererInfo.MinorTickMarkWidth, direction,
      out minorTickMarkStart, out minorTickMarkEnd);
  }
}
