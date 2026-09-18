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
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the base class for all Y axis renderer, and - since a horizontal and a vertical
/// value axis compute the same things and differ only in which page coordinate a value becomes -
/// the one place both are drawn from. <see cref="orientation"/> says which axis this is.
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
  /// Returns a initialized rendererInfo based on the Y axis.
  /// </summary>
  internal override RendererInfo Init()
  {
    Chart chart = (Chart)this.rendererParms.DrawingItem;

    AxisRendererInfo yari = new AxisRendererInfo();
    yari.axis = chart.yAxis;
    InitScale(yari);
    if (yari.axis != null)
    {
      ChartRendererInfo cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
      InitTickLabels(yari, cri.DefaultFont);
      InitAxisTitle(yari, cri.DefaultFont);
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
    AxisRendererInfo yari = ((ChartRendererInfo)this.rendererParms.RendererInfo).yAxisRendererInfo;
    if (yari.axis != null)
    {
      XGraphics gfx = this.rendererParms.Graphics;

      XSize size = new XSize(0, 0);

      // height of all ticklabels
      double yMin = yari.MinimumScale;
      double yMax = yari.MaximumScale;
      double yMajorTick = yari.MajorTick;
      double lineHeight = Double.MinValue;
      XSize labelSize = new XSize(0, 0);
      for (double y = yMin; y <= yMax; y += yMajorTick)
      {
        string str = y.ToString(yari.TickLabelsFormat);
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
      XSize titleSize = new XSize(0, 0);
      if (yari.axisTitleRendererInfo != null)
      {
        RendererParameters parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        AxisTitleRenderer atr = new AxisTitleRenderer(parms);
        atr.Format();
        titleSize.Height = yari.axisTitleRendererInfo.Height;
        titleSize.Width = yari.axisTitleRendererInfo.Width;
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
    AxisRendererInfo yari = ((ChartRendererInfo)this.rendererParms.RendererInfo).yAxisRendererInfo;

    double yMin = yari.MinimumScale;
    double yMax = yari.MaximumScale;
    double yMajorTick = yari.MajorTick;
    double yMinorTick = yari.MinorTick;

    XMatrix matrix = new XMatrix();
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
    double majorTickMarkStart = 0, majorTickMarkEnd = 0,
      minorTickMarkStart = 0, minorTickMarkEnd = 0;
    GetTickMarkPos(yari, ref majorTickMarkStart, ref majorTickMarkEnd, ref minorTickMarkStart, ref minorTickMarkEnd);

    XGraphics gfx = this.rendererParms.Graphics;
    LineFormatRenderer lineFormatRenderer = new LineFormatRenderer(gfx, yari.LineFormat);

    // The tick marks now read the pens the base class already computes for every axis - the fix
    // this merge exists to make. Before it, the horizontal orientation stroked its ticks with
    // LineFormat too, which is null until a caller sets one, so a value axis running across the
    // bottom of a bar chart drew no tick marks at all unless a line format had been given it.
    LineFormatRenderer minorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MinorTickMarkLineFormat);
    LineFormatRenderer majorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MajorTickMarkLineFormat);
    XPoint[] points = new XPoint[2];

    // Draw minor tick marks.
    if (yari.MinorTickMark != TickMarkType.None)
    {
      for (double y = yMin + yMinorTick; y < yMax; y += yMinorTick)
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
      XStringFormat xsf = new XStringFormat();
      xsf.LineAlignment = XLineAlignment.Near;
      int countTickLabels = (int)((yMax - yMin) / yMajorTick) + 1;
      for (int i = 0; i < countTickLabels; ++i)
      {
        double y = yMin + yMajorTick * i;
        string str = y.ToString(yari.TickLabelsFormat);

        XSize labelSize = gfx.MeasureString(str, yari.TickLabelsFont);
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

        XPoint[] layoutText = new XPoint[1];
        layoutText[0].X = y;
        layoutText[0].Y = yari.Y + 1.5 * yari.MajorTickMarkWidth;
        matrix.TransformPoints(layoutText);
        layoutText[0].X -= labelSize.Width / 2; // Center text vertically.
        gfx.DrawString(str, yari.TickLabelsFont, yari.TickLabelsBrush, layoutText[0], xsf);
      }
    }
    else
    {
      double lineSpace = yari.TickLabelsFont.GetHeight();
      int cellSpace = yari.TickLabelsFont.FontFamily.GetLineSpacing(yari.TickLabelsFont.Style);
      double xHeight = yari.TickLabelsFont.Metrics.XHeight;

      XSize labelSize = new XSize(0, 0);
      labelSize.Height = lineSpace * xHeight / cellSpace;

      int countTickLabels = (int)((yMax - yMin) / yMajorTick) + 1;
      for (int i = 0; i < countTickLabels; ++i)
      {
        double y = yMin + yMajorTick * i;
        string str = y.ToString(yari.TickLabelsFormat);

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
        XPoint[] layoutText = new XPoint[1];
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
      if (yari.axisTitleRendererInfo != null)
      {
        RendererParameters parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        XRect rcTitle = yari.Rect;
        rcTitle.Height = yari.axisTitleRendererInfo.Height;
        rcTitle.Y += yari.Rect.Height - rcTitle.Height;
        yari.axisTitleRendererInfo.Rect = rcTitle;
        AxisTitleRenderer atr = new AxisTitleRenderer(parms);
        atr.Draw();
      }
    }
    else
    {
      if (yari.axisTitleRendererInfo != null && yari.axisTitleRendererInfo.AxisTitleText != "")
      {
        RendererParameters parms = new RendererParameters();
        parms.Graphics = gfx;
        parms.RendererInfo = yari;
        double width = yari.axisTitleRendererInfo.Width;
        yari.axisTitleRendererInfo.Rect = yari.InnerRect;
        yari.axisTitleRendererInfo.Width = width;
        AxisTitleRenderer atr = new AxisTitleRenderer(parms);
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

  /// <summary>
  /// Determines the smallest and the largest number from all series of the chart.
  /// </summary>
  protected virtual void CalcYAxis(out double yMin, out double yMax)
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
  protected void CalcStackedYAxis(out double yMin, out double yMax)
  {
    yMin = double.MaxValue;
    yMax = double.MinValue;

    ChartRendererInfo cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    int maxPoints = 0;
    foreach (SeriesRendererInfo sri in cri.seriesRendererInfos)
      maxPoints = Math.Max(maxPoints, sri.series.Elements.Count);

    for (int pointIdx = 0; pointIdx < maxPoints; ++pointIdx)
    {
      double valueSumPos = 0, valueSumNeg = 0;
      foreach (SeriesRendererInfo sri in cri.seriesRendererInfos)
      {
        if (sri.pointRendererInfos.Length <= pointIdx)
          break;

        ColumnRendererInfo column = (ColumnRendererInfo)sri.pointRendererInfos[pointIdx];
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
  protected void FineTuneYAxis(AxisRendererInfo rendererInfo, double yMin, double yMax)
  {
    if (yMin == double.MaxValue && yMax == double.MinValue)
    {
      // No series data given.
      yMin = 0.0f;
      yMax = 0.9f;
    }

    if (yMin == yMax)
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

    double deltaYRaw = yMax - yMin;

    int digits = (int)(Math.Log(deltaYRaw, 10) + 1);
    double normed = deltaYRaw / Math.Pow(10, digits) * 10;

    double normedStepWidth = 1;
    if (normed < 2)
      normedStepWidth = 0.2f;
    else if (normed < 5)
      normedStepWidth = 0.5f;

    AxisRendererInfo yari = rendererInfo;
    double stepWidth = normedStepWidth * Math.Pow(10.0, digits - 1.0);
    if (yari.axis == null || double.IsNaN(yari.axis.majorTick))
      yari.MajorTick = stepWidth;
    else
      yari.MajorTick = yari.axis.majorTick;

    double roundFactor = stepWidth * 0.5;
    if (yari.axis == null || double.IsNaN(yari.axis.minimumScale))
    {
      double signumMin = (yMin != 0) ? yMin / Math.Abs(yMin) : 0;
      yari.MinimumScale = (int)(Math.Abs((yMin - roundFactor) / stepWidth) - (1 * signumMin)) * stepWidth * signumMin;
    }
    else
      yari.MinimumScale = yari.axis.minimumScale;

    if (yari.axis == null || double.IsNaN(yari.axis.maximumScale))
    {
      double signumMax = (yMax != 0) ? yMax / Math.Abs(yMax) : 0;
      yari.MaximumScale = (int)(Math.Abs((yMax + roundFactor) / stepWidth) + (1 * signumMax)) * stepWidth * signumMax;
    }
    else
      yari.MaximumScale = yari.axis.maximumScale;

    if (yari.axis == null || double.IsNaN(yari.axis.minorTick))
      yari.MinorTick = yari.MajorTick / 5;
    else
      yari.MinorTick = yari.axis.minorTick;
  }

  /// <summary>
  /// Returns the default tick labels format string.
  /// </summary>
  protected override string GetDefaultTickLabelsFormat()
  {
    return "0.0";
  }
}
