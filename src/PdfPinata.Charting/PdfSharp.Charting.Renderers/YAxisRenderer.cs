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
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the base class for all Y axis renderer, and - since a horizontal and a vertical
/// value axis compute the same things and differ only in which page coordinate a value becomes -
/// the one place both are drawn from. <c>isHorizontal</c> says which axis this is.
/// </summary>
internal abstract class YAxisRenderer : AxisRenderer
{
  /// <summary>
  /// Initializes a new instance of the YAxisRenderer class with the specified renderer
  /// parameters and orientation.
  /// </summary>
  internal YAxisRenderer(RendererParameters parms, AxisOrientation orientation)
    : base(parms)
  {
    this.isHorizontal = orientation == AxisOrientation.Horizontal;
  }

  /// <summary>
  /// Whether this is the horizontal axis, cached once rather than compared for on every one of
  /// the several places <see cref="Draw"/> and <see cref="Format"/> branch on it.
  /// </summary>
  private readonly bool isHorizontal;

  /// <summary>
  /// Returns a initialized rendererInfo based on the Y axis.
  /// </summary>
  internal override RendererInfo Init()
  {
    var chart = (Chart)this.rendererParms.DrawingItem;

    var yari = new AxisRendererInfo();
    yari.Axis = chart.yAxis;
    InitScale(yari);
    if (yari.Axis != null)
    {
      var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
      InitTickLabels(yari, cri.DefaultFont, cri.DefaultFontColor);
      InitAxisTitle(yari, cri.DefaultFont, cri.DefaultFontColor);
      InitAxisLineFormat(yari);
      InitGridlines(yari);
    }
    return yari;
  }

  /// <summary>
  /// Calculates the space used for the Y axis.
  /// </summary>
  internal override void Format()
  {
    var yari = ((ChartRendererInfo)this.rendererParms.RendererInfo).YAxisRendererInfo;
    if (yari.Axis != null)
    {
      var gfx = this.rendererParms.Graphics;

      var size = new XSize(0, 0);

      // height of all ticklabels
      var yMin = yari.MinimumScale;
      var yMax = yari.MaximumScale;
      var yMajorTick = yari.MajorTick;
      var lineHeight = Double.MinValue;
      var labelSize = new XSize(0, 0);
      for (var y = yMin; y <= yMax; y += yMajorTick)
      {
        var str = y.ToString(yari.TickLabelsFormat);
        labelSize = gfx.MeasureString(str, yari.TickLabelsFont);
        if (isHorizontal)
        {
          size.Width += labelSize.Width;
          size.Height = Math.Max(labelSize.Height, size.Height);
          lineHeight = Math.Max(lineHeight, labelSize.Width);
        }
        else
        {
          size.Height += labelSize.Height;
          size.Width = Math.Max(labelSize.Width, size.Width);
          lineHeight = Math.Max(lineHeight, labelSize.Height);
        }
      }

      // add space for tickmarks
      if (isHorizontal)
        size.Height += yari.MajorTickMarkWidth * 1.5;
      else
        size.Width += yari.MajorTickMarkWidth * 1.5;

      // Measure axis title
      var titleSize = new XSize(0, 0);
      if (yari.AxisTitleRendererInfo != null)
      {
        var parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        var atr = new AxisTitleRenderer(parms);
        atr.Format();
        titleSize.Height = yari.AxisTitleRendererInfo.Height;
        titleSize.Width = yari.AxisTitleRendererInfo.Width;
      }

      if (isHorizontal)
      {
        yari.Height = size.Height + titleSize.Height;
        yari.Width = Math.Max(size.Width, titleSize.Width);

        yari.InnerRect = yari.Rect;
      }
      else
      {
        yari.Height = Math.Max(size.Height, titleSize.Height);
        yari.Width = size.Width + titleSize.Width;

        // Compensates for the vertical axis centring its tick labels on their tick, which the
        // horizontal axis does not need to. Local to this orientation rather than a property of
        // every vertical axis - settled that way rather than carried across by assumption; see
        // docs/specs/axis-renderer-duplication.md.
        yari.InnerRect = yari.Rect;
        // ReSharper disable once PossibleLossOfFraction
        yari.InnerRect.Y += yari.TickLabelsFont.Height / 2;
      }

      yari.LabelSize = labelSize;
    }
  }

  /// <summary>
  /// Draws the Y axis.
  /// </summary>
  internal override void Draw()
  {
    var yari = ((ChartRendererInfo)this.rendererParms.RendererInfo).YAxisRendererInfo;

    var yMin = yari.MinimumScale;
    var yMax = yari.MaximumScale;
    var yMajorTick = yari.MajorTick;
    var yMinorTick = yari.MinorTick;

    var matrix = new XMatrix();
    if (isHorizontal)
    {
      matrix.TranslatePrepend(-yMin, -yari.Y);
      matrix.Scale(yari.InnerRect.Width / (yMax - yMin), 1, XMatrixOrder.Append);
      matrix.Translate(yari.X, yari.Y, XMatrixOrder.Append);
    }
    else
    {
      matrix.TranslatePrepend(-yari.InnerRect.X, yMax);
      matrix.Scale(1, yari.InnerRect.Height / (yMax - yMin), XMatrixOrder.Append);
      matrix.ScalePrepend(1, -1); // mirror horizontal
      matrix.Translate(yari.InnerRect.X, yari.InnerRect.Y, XMatrixOrder.Append);
    }

    // Draw axis.
    // First draw tick marks, second draw axis.
    double majorTickMarkStart, majorTickMarkEnd,
      minorTickMarkStart, minorTickMarkEnd;
    GetTickMarkPos(yari, out majorTickMarkStart, out majorTickMarkEnd, out minorTickMarkStart, out minorTickMarkEnd);

    var gfx = this.rendererParms.Graphics;
    var lineFormatRenderer = new LineFormatRenderer(gfx, yari.LineFormat);

    // The tick marks now read the pens the base class already computes for every axis - the fix
    // this merge exists to make. Before it, the horizontal orientation stroked its ticks with
    // LineFormat too, which is null until a caller sets one, so a value axis running across the
    // bottom of a bar chart drew no tick marks at all unless a line format had been given it.
    var minorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MinorTickMarkLineFormat);
    var majorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MajorTickMarkLineFormat);
    var points = new XPoint[2];

    // Draw minor tick marks.
    if (yari.MinorTickMark != TickMarkType.None)
    {
      for (var y = yMin + yMinorTick; y < yMax; y += yMinorTick)
      {
        if (isHorizontal)
        {
          points[0].X = y;
          points[0].Y = minorTickMarkStart;
          points[1].X = y;
          points[1].Y = minorTickMarkEnd;
        }
        else
        {
          points[0].X = minorTickMarkStart;
          points[0].Y = y;
          points[1].X = minorTickMarkEnd;
          points[1].Y = y;
        }
        matrix.TransformPoints(points);
        minorTickMarkLineFormat.DrawLine(points[0], points[1]);
      }
    }

    if (isHorizontal)
    {
      var xsf = new XStringFormat();
      xsf.LineAlignment = XLineAlignment.Near;
      var countTickLabels = (int)((yMax - yMin) / yMajorTick) + 1;
      for (var i = 0; i < countTickLabels; ++i)
      {
        var y = yMin + yMajorTick * i;
        var str = y.ToString(yari.TickLabelsFormat);

        var labelSize = gfx.MeasureString(str, yari.TickLabelsFont);
        if (yari.MajorTickMark != TickMarkType.None)
        {
          labelSize.Height += 1.5f * yari.MajorTickMarkWidth;
          points[0].X = y;
          points[0].Y = majorTickMarkStart;
          points[1].X = y;
          points[1].Y = majorTickMarkEnd;
          matrix.TransformPoints(points);
          majorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }

        var layoutText = new XPoint[1];
        layoutText[0].X = y;
        layoutText[0].Y = yari.Y + 1.5 * yari.MajorTickMarkWidth;
        matrix.TransformPoints(layoutText);
        layoutText[0].X -= labelSize.Width / 2; // Center text vertically.
        gfx.DrawString(str, yari.TickLabelsFont, yari.TickLabelsBrush, layoutText[0], xsf);
      }
    }
    else
    {
      var lineSpace = yari.TickLabelsFont.GetHeight();
      var cellSpace = yari.TickLabelsFont.FontFamily.GetLineSpacing(yari.TickLabelsFont.Style);
      double xHeight = yari.TickLabelsFont.Metrics.XHeight;

      var labelSize = new XSize(0, 0);
      labelSize.Height = lineSpace * xHeight / cellSpace;

      var countTickLabels = (int)((yMax - yMin) / yMajorTick) + 1;
      for (var i = 0; i < countTickLabels; ++i)
      {
        var y = yMin + yMajorTick * i;
        var str = y.ToString(yari.TickLabelsFormat);

        labelSize.Width = gfx.MeasureString(str, yari.TickLabelsFont).Width;

        // Draw major tick marks.
        if (yari.MajorTickMark != TickMarkType.None)
        {
          labelSize.Width += yari.MajorTickMarkWidth * 1.5;
          points[0].X = majorTickMarkStart;
          points[0].Y = y;
          points[1].X = majorTickMarkEnd;
          points[1].Y = y;
          matrix.TransformPoints(points);
          majorTickMarkLineFormat.DrawLine(points[0], points[1]);
        }
        else
          labelSize.Width += SpaceBetweenLabelAndTickmark;

        // Draw label text.
        var layoutText = new XPoint[1];
        layoutText[0].X = yari.InnerRect.X + yari.InnerRect.Width - labelSize.Width;
        layoutText[0].Y = y;
        matrix.TransformPoints(layoutText);
        layoutText[0].Y += labelSize.Height / 2; // Center text vertically.
        gfx.DrawString(str, yari.TickLabelsFont, yari.TickLabelsBrush, layoutText[0]);
      }
    }

    // Draw axis.
    if (isHorizontal)
    {
      if (yari.LineFormat != null)
      {
        points[0].X = yMin;
        points[0].Y = yari.Y;
        points[1].X = yMax;
        points[1].Y = yari.Y;
        matrix.TransformPoints(points);
        if (yari.MajorTickMark != TickMarkType.None)
        {
          // yMax is at the upper side of the axis
          points[0].X -= yari.LineFormat.Width / 2;
          points[1].X += yari.LineFormat.Width / 2;
        }
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }
    else
    {
      if (yari.LineFormat != null && yari.LineFormat.Width > 0)
      {
        points[0].X = yari.InnerRect.X + yari.InnerRect.Width;
        points[0].Y = yMin;
        points[1].X = yari.InnerRect.X + yari.InnerRect.Width;
        points[1].Y = yMax;
        matrix.TransformPoints(points);
        if (yari.MajorTickMark != TickMarkType.None)
        {
          // yMax is at the upper side of the axis
          points[1].Y -= yari.LineFormat.Width / 2;
          points[0].Y += yari.LineFormat.Width / 2;
        }
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }

    // Draw axis title
    if (isHorizontal)
    {
      if (yari.AxisTitleRendererInfo != null)
      {
        var parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        var rcTitle = yari.Rect;
        rcTitle.Height = yari.AxisTitleRendererInfo.Height;
        rcTitle.Y += yari.Rect.Height - rcTitle.Height;
        yari.AxisTitleRendererInfo.Rect = rcTitle;
        var atr = new AxisTitleRenderer(parms);
        atr.Draw();
      }
    }
    else
    {
      if (yari.AxisTitleRendererInfo != null && yari.AxisTitleRendererInfo.AxisTitleText != "")
      {
        var parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        var width = yari.AxisTitleRendererInfo.Width;
        yari.AxisTitleRendererInfo.Rect = yari.InnerRect;
        yari.AxisTitleRendererInfo.Width = width;
        var atr = new AxisTitleRenderer(parms);
        atr.Draw();
      }
    }
  }

  /// <summary>
  /// Calculates all values necessary for scaling the axis like minimum/maximum scale or
  /// minor/major tick.
  /// </summary>
  private void InitScale(AxisRendererInfo rendererInfo)
  {
    double yMin, yMax;
    CalcYAxis(out yMin, out yMax);
    FineTuneYAxis(rendererInfo, yMin, yMax);

    rendererInfo.MajorTickMarkWidth = DefaultMajorTickMarkWidth;
    rendererInfo.MinorTickMarkWidth = DefaultMinorTickMarkWidth;
  }

  /// <summary>
  /// Gets the top and bottom, or left and right, position of the major and minor tick marks
  /// depending on the tick mark type, on the dimension this orientation's ticks run across.
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

  /// <summary>
  /// Determines the smallest and the largest number from all series of the chart.
  /// </summary>
  protected virtual void CalcYAxis(out double yMin, out double yMax) => CalcValueYAxis(out yMin, out yMax);

  /// <summary>
  /// Determines the smallest and the largest number from all series of the chart. Not virtual,
  /// so that the stacked renderers, which override <see cref="CalcYAxis"/>, can still reach it.
  /// </summary>
  private void CalcValueYAxis(out double yMin, out double yMax)
  {
    yMin = double.MaxValue;
    yMax = double.MinValue;

    foreach (Series series in ((Chart)this.rendererParms.DrawingItem).SeriesCollection)
    {
      foreach (Point point in series.Elements)
      {
        // Series.AddBlank puts a null here, which is the whole of what a blank is. The stacked
        // renderers' overrides of this method already test for it.
        if (point != null && !double.IsNaN(point.value))
        {
          yMin = Math.Min(yMin, point.Value);
          yMax = Math.Max(yMax, point.Value);
        }
      }
    }
  }

  /// <summary>
  /// Determines the sum of the smallest and the largest stacked column or bar from all series of
  /// the chart. Shared by both stacked renderers, which otherwise differed only in which
  /// orientation's YAxisRenderer they built on.
  /// </summary>
  /// <remarks>
  /// In a combination chart only the column series are stacked, so only they are summed, and the
  /// scale is then widened to every value of every series: an area or a line drawn beside the
  /// stack is plotted at its own value, and may reach past the tallest stack or below the lowest.
  /// A value of a stacked series always lies between the two sums of its category, so taking it
  /// in again changes nothing.
  /// </remarks>
  protected void CalcStackedYAxis(out double yMin, out double yMax)
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    if (cri is CombinationRendererInfo combination)
    {
      CalcStackedYAxis(combination.ColumnSeriesRendererInfos ?? [], out var stackedMin, out var stackedMax);
      CalcValueYAxis(out var valueMin, out var valueMax);
      yMin = Math.Min(stackedMin, valueMin);
      yMax = Math.Max(stackedMax, valueMax);
      return;
    }

    CalcStackedYAxis(cri.SeriesRendererInfos, out yMin, out yMax);
  }

  /// <summary>
  /// Determines the sum of the smallest and the largest stacked column or bar of the series given.
  /// </summary>
  private static void CalcStackedYAxis(SeriesRendererInfo[] stacked, out double yMin, out double yMax)
  {
    yMin = double.MaxValue;
    yMax = double.MinValue;

    var maxPoints = 0;
    foreach (var sri in stacked)
      maxPoints = Math.Max(maxPoints, sri.Series.Elements.Count);

    for (var pointIdx = 0; pointIdx < maxPoints; ++pointIdx)
    {
      double valueSumPos = 0, valueSumNeg = 0;
      foreach (var sri in stacked)
      {
        if (sri.PointRendererInfos.Length <= pointIdx)
          break;

        var column = (ColumnRendererInfo)sri.PointRendererInfos[pointIdx];
        if (!double.IsNaN(column.Value))
        {
          if (column.Value < 0)
            valueSumNeg += column.Value;
          else
            valueSumPos += column.Value;
        }
      }
      yMin = Math.Min(valueSumNeg, yMin);
      yMax = Math.Max(valueSumPos, yMax);
    }
  }

  /// <summary>
  /// Calculates optimal minimum/maximum scale and minor/major tick based on yMin and yMax.
  /// </summary>
  protected static void FineTuneYAxis(AxisRendererInfo rendererInfo, double yMin, double yMax)
  {
    #pragma warning disable S1244 // Exact on purpose: compared with a sentinel the value is set to, never with the result of arithmetic.
    // ReSharper disable CompareOfFloatsByEqualityOperator
    if (yMin == double.MaxValue && yMax == double.MinValue)
    // ReSharper restore CompareOfFloatsByEqualityOperator
    #pragma warning restore S1244
    {
      // No series data given.
      yMin = 0.0f;
      yMax = 0.9f;
    }

    #pragma warning disable S1244 // Exact on purpose: the two are equal only when every value is the same one, and then the axis needs widening.
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    if (yMin == yMax)
    #pragma warning restore S1244
    {
      if (yMin == 0)
        yMax = 0.9f;
      else if (yMin < 0)
        yMax = 0;
      else if (yMin > 0)
        yMax = yMin + 1;
    }

    // If the ratio between yMax to yMin is more than 1.2, the smallest number will be set too zero.
    // It's Excel's behavior.
    if (yMin != 0)
    {
      if (yMin < 0 && yMax < 0)
      {
        if (yMin / yMax >= 1.2)
          yMax = 0;
      }
      else if (yMax / yMin >= 1.2)
        yMin = 0;
    }

    var deltaYRaw = yMax - yMin;

    var digits = (int)(Math.Log(deltaYRaw, 10) + 1);
    var normed = deltaYRaw / Math.Pow(10, digits) * 10;

    double normedStepWidth = 1;
    if (normed < 2)
      normedStepWidth = 0.2f;
    else if (normed < 5)
      normedStepWidth = 0.5f;

    var yari = rendererInfo;
    var stepWidth = normedStepWidth * Math.Pow(10.0, digits - 1.0);
    if (yari.Axis == null || double.IsNaN(yari.Axis.majorTick))
      yari.MajorTick = stepWidth;
    else
      yari.MajorTick = yari.Axis.majorTick;

    var roundFactor = stepWidth * 0.5;
    if (yari.Axis == null || double.IsNaN(yari.Axis.minimumScale))
    {
      var signumMin = (yMin != 0) ? yMin / Math.Abs(yMin) : 0;
      yari.MinimumScale = (int)(Math.Abs((yMin - roundFactor) / stepWidth) - (1 * signumMin)) * stepWidth * signumMin;
    }
    else
      yari.MinimumScale = yari.Axis.minimumScale;

    if (yari.Axis == null || double.IsNaN(yari.Axis.maximumScale))
    {
      var signumMax = (yMax != 0) ? yMax / Math.Abs(yMax) : 0;
      yari.MaximumScale = (int)(Math.Abs((yMax + roundFactor) / stepWidth) + (1 * signumMax)) * stepWidth * signumMax;
    }
    else
      yari.MaximumScale = yari.Axis.maximumScale;

    if (yari.Axis == null || double.IsNaN(yari.Axis.minorTick))
      yari.MinorTick = yari.MajorTick / 5;
    else
      yari.MinorTick = yari.Axis.minorTick;
  }

  /// <summary>
  /// Returns the default tick labels format string.
  /// </summary>
  protected override string GetDefaultTickLabelsFormat()
  {
    return "0.0";
  }
}
