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
    isHorizontal = orientation == AxisOrientation.Horizontal;
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
    var chart = (Chart)rendererParms.DrawingItem;

    var yari = new AxisRendererInfo { Axis = chart.yAxis };
    InitScale(yari);
    if (yari.Axis == null)
      return yari;

    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    InitTickLabels(yari, cri.DefaultFont, cri.DefaultFontColor);
    InitAxisTitle(yari, cri.DefaultFont, cri.DefaultFontColor);
    InitAxisLineFormat(yari);
    InitGridlines(yari);
    return yari;
  }

  /// <summary>
  /// Calculates the space used for the Y axis.
  /// </summary>
  internal override void Format()
  {
    var yari = ((ChartRendererInfo)rendererParms.RendererInfo).YAxisRendererInfo;
    if (yari.Axis == null)
      return;

    var gfx = rendererParms.Graphics;

    var size = MeasureTickLabels(gfx, yari, out var labelSize);

    // add space for tickmarks
    if (isHorizontal)
      size.Height += yari.MajorTickMarkWidth * 1.5;
    else
      size.Width += yari.MajorTickMarkWidth * 1.5;

    var titleSize = MeasureAxisTitle(gfx, yari);

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

  /// <summary>
  /// The space all tick labels take laid end to end along the axis, and the size of the last of them.
  /// </summary>
  private XSize MeasureTickLabels(XGraphics gfx, AxisRendererInfo yari, out XSize labelSize)
  {
    var size = new XSize(0, 0);

    // height of all ticklabels
    var yMin = yari.MinimumScale;
    var yMax = yari.MaximumScale;
    var yMajorTick = yari.MajorTick;
    labelSize = new XSize(0, 0);
    for (var y = yMin; y <= yMax; y += yMajorTick)
    {
      var str = y.ToString(yari.TickLabelsFormat);
      labelSize = gfx.MeasureString(str, yari.TickLabelsFont);
      if (isHorizontal)
      {
        size.Width += labelSize.Width;
        size.Height = Math.Max(labelSize.Height, size.Height);
      }
      else
      {
        size.Height += labelSize.Height;
        size.Width = Math.Max(labelSize.Width, size.Width);
      }
    }
    return size;
  }

  /// <summary>
  /// Formats the axis title, if there is one, and answers its size.
  /// </summary>
  private static XSize MeasureAxisTitle(XGraphics gfx, AxisRendererInfo yari)
  {
    var titleSize = new XSize(0, 0);
    if (yari.AxisTitleRendererInfo == null)
      return titleSize;

    var parms = new RendererParameters
    {
      Graphics = gfx,
      RendererInfo = yari
    };
    var atr = new AxisTitleRenderer(parms);
    atr.Format();
    titleSize.Height = yari.AxisTitleRendererInfo.Height;
    titleSize.Width = yari.AxisTitleRendererInfo.Width;
    return titleSize;
  }

  /// <summary>
  /// Draws the Y axis.
  /// </summary>
  internal override void Draw()
  {
    var yari = ((ChartRendererInfo)rendererParms.RendererInfo).YAxisRendererInfo;
    var matrix = ValueToPageTransform(yari);

    // Draw axis.
    // First draw tick marks, second draw axis.
    GetTickMarkPos(yari, out var majorTickMarkStart, out var majorTickMarkEnd, out var minorTickMarkStart, out var minorTickMarkEnd);

    var gfx = rendererParms.Graphics;
    var lineFormatRenderer = new LineFormatRenderer(gfx, yari.LineFormat);

    // The tick marks now read the pens the base class already computes for every axis - the fix
    // this merge exists to make. Before it, the horizontal orientation stroked its ticks with
    // LineFormat too, which is null until a caller sets one, so a value axis running across the
    // bottom of a bar chart drew no tick marks at all unless a line format had been given it.
    var minorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MinorTickMarkLineFormat);
    var majorTickMarkLineFormat = new LineFormatRenderer(gfx, yari.MajorTickMarkLineFormat);

    // Draw minor tick marks.
    if (yari.MinorTickMark != TickMarkType.None)
    {
      for (var y = yari.MinimumScale + yari.MinorTick; y < yari.MaximumScale; y += yari.MinorTick)
        DrawTickMark(minorTickMarkLineFormat, matrix, y, minorTickMarkStart, minorTickMarkEnd);
    }

    // Draw the major tick marks and the labels beside them.
    var majorTickMarks = new TickMarkPen(majorTickMarkLineFormat, majorTickMarkStart, majorTickMarkEnd);
    if (isHorizontal)
      DrawHorizontalTickLabels(gfx, yari, matrix, majorTickMarks);
    else
      DrawVerticalTickLabels(gfx, yari, matrix, majorTickMarks);

    DrawAxisLine(lineFormatRenderer, yari, matrix);

    if (isHorizontal)
      DrawHorizontalAxisTitle(gfx, yari);
    else
      DrawVerticalAxisTitle(gfx, yari);
  }

  /// <summary>
  /// The transformation that carries a value on the axis to where it lies on the page: rightwards
  /// from the minimum scale for the horizontal orientation, and upwards from it for the vertical.
  /// </summary>
  private XMatrix ValueToPageTransform(AxisRendererInfo yari)
  {
    var yMin = yari.MinimumScale;
    var yMax = yari.MaximumScale;

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
    return matrix;
  }

  /// <summary>
  /// The pen the major tick marks are drawn with and where across the axis they run, carried
  /// together into the tick-label loops that draw a tick beside each label.
  /// </summary>
  private readonly record struct TickMarkPen(LineFormatRenderer LineFormat, double Start, double End);

  /// <summary>
  /// Draws one tick mark across the axis at <paramref name="value"/>, from
  /// <paramref name="start"/> to <paramref name="end"/>.
  /// </summary>
  private void DrawTickMark(LineFormatRenderer lineFormatRenderer, XMatrix matrix, double value, double start, double end)
  {
    var points = new XPoint[2];
    if (isHorizontal)
    {
      points[0] = new XPoint(value, start);
      points[1] = new XPoint(value, end);
    }
    else
    {
      points[0] = new XPoint(start, value);
      points[1] = new XPoint(end, value);
    }
    matrix.TransformPoints(points);
    lineFormatRenderer.DrawLine(points[0], points[1]);
  }

  /// <summary>
  /// Draws a horizontal axis's major tick marks and its tick labels, each label centred below
  /// its tick.
  /// </summary>
  private void DrawHorizontalTickLabels(XGraphics gfx, AxisRendererInfo yari, XMatrix matrix, TickMarkPen majorTickMarks)
  {
    var yMin = yari.MinimumScale;
    var yMajorTick = yari.MajorTick;

    var xsf = new XStringFormat { LineAlignment = XLineAlignment.Near };
    var countTickLabels = (int)((yari.MaximumScale - yMin) / yMajorTick) + 1;
    for (var i = 0; i < countTickLabels; ++i)
    {
      var y = yMin + yMajorTick * i;
      var str = y.ToString(yari.TickLabelsFormat);

      var labelSize = gfx.MeasureString(str, yari.TickLabelsFont);
      if (yari.MajorTickMark != TickMarkType.None)
      {
        labelSize.Height += 1.5f * yari.MajorTickMarkWidth;
        DrawTickMark(majorTickMarks.LineFormat, matrix, y, majorTickMarks.Start, majorTickMarks.End);
      }

      var layoutText = new XPoint[1];
      layoutText[0].X = y;
      layoutText[0].Y = yari.Y + 1.5 * yari.MajorTickMarkWidth;
      matrix.TransformPoints(layoutText);
      layoutText[0].X -= labelSize.Width / 2; // Center text vertically.
      gfx.DrawString(str, yari.TickLabelsFont, yari.TickLabelsBrush, layoutText[0], xsf);
    }
  }

  /// <summary>
  /// Draws a vertical axis's major tick marks and its tick labels, each label set against its
  /// tick and centred on it vertically.
  /// </summary>
  private void DrawVerticalTickLabels(XGraphics gfx, AxisRendererInfo yari, XMatrix matrix, TickMarkPen majorTickMarks)
  {
    var yMin = yari.MinimumScale;
    var yMajorTick = yari.MajorTick;

    var lineSpace = yari.TickLabelsFont.GetHeight();
    var cellSpace = yari.TickLabelsFont.FontFamily.GetLineSpacing(yari.TickLabelsFont.Style);
    double xHeight = yari.TickLabelsFont.Metrics.XHeight;

    var labelSize = new XSize(0, 0) { Height = lineSpace * xHeight / cellSpace };

    var countTickLabels = (int)((yari.MaximumScale - yMin) / yMajorTick) + 1;
    for (var i = 0; i < countTickLabels; ++i)
    {
      var y = yMin + yMajorTick * i;
      var str = y.ToString(yari.TickLabelsFormat);

      labelSize.Width = gfx.MeasureString(str, yari.TickLabelsFont).Width;

      // Draw major tick marks.
      if (yari.MajorTickMark != TickMarkType.None)
      {
        labelSize.Width += yari.MajorTickMarkWidth * 1.5;
        DrawTickMark(majorTickMarks.LineFormat, matrix, y, majorTickMarks.Start, majorTickMarks.End);
      }
      else
      {
        labelSize.Width += SpaceBetweenLabelAndTickmark;
      }

      // Draw label text.
      var layoutText = new XPoint[1];
      layoutText[0].X = yari.InnerRect.X + yari.InnerRect.Width - labelSize.Width;
      layoutText[0].Y = y;
      matrix.TransformPoints(layoutText);
      layoutText[0].Y += labelSize.Height / 2; // Center text vertically.
      gfx.DrawString(str, yari.TickLabelsFont, yari.TickLabelsBrush, layoutText[0]);
    }
  }

  /// <summary>
  /// Draws the axis line from the minimum scale to the maximum, lengthened at each end by half
  /// its own width when there are major tick marks, so that it covers the outermost of them.
  /// </summary>
  /// <remarks>
  /// The two orientations test for a line format differently, and are kept that way: the
  /// horizontal one draws whenever there is one, the vertical one only when it has a width.
  /// </remarks>
  private void DrawAxisLine(LineFormatRenderer lineFormatRenderer, AxisRendererInfo yari, XMatrix matrix)
  {
    var points = isHorizontal ? HorizontalAxisLine(yari, matrix) : VerticalAxisLine(yari, matrix);
    if (points == null)
      return;

    lineFormatRenderer.DrawLine(points[0], points[1]);
  }

  /// <summary>
  /// The two ends of a horizontal axis line, or null when there is no line format.
  /// </summary>
  private static XPoint[] HorizontalAxisLine(AxisRendererInfo yari, XMatrix matrix)
  {
    if (yari.LineFormat == null)
      return null;

    var points = new XPoint[2];
    points[0] = new XPoint(yari.MinimumScale, yari.Y);
    points[1] = new XPoint(yari.MaximumScale, yari.Y);
    matrix.TransformPoints(points);
    if (yari.MajorTickMark != TickMarkType.None)
    {
      // yMax is at the upper side of the axis
      points[0].X -= yari.LineFormat.Width / 2;
      points[1].X += yari.LineFormat.Width / 2;
    }
    return points;
  }

  /// <summary>
  /// The two ends of a vertical axis line, or null when there is no line format with a width.
  /// </summary>
  private static XPoint[] VerticalAxisLine(AxisRendererInfo yari, XMatrix matrix)
  {
    if (yari.LineFormat is not { Width: > 0 })
      return null;

    var points = new XPoint[2];
    points[0] = new XPoint(yari.InnerRect.X + yari.InnerRect.Width, yari.MinimumScale);
    points[1] = new XPoint(yari.InnerRect.X + yari.InnerRect.Width, yari.MaximumScale);
    matrix.TransformPoints(points);
    if (yari.MajorTickMark != TickMarkType.None)
    {
      // yMax is at the upper side of the axis
      points[1].Y -= yari.LineFormat.Width / 2;
      points[0].Y += yari.LineFormat.Width / 2;
    }
    return points;
  }

  /// <summary>
  /// Draws a horizontal axis's title in the strip along the bottom of the axis.
  /// </summary>
  private static void DrawHorizontalAxisTitle(XGraphics gfx, AxisRendererInfo yari)
  {
    if (yari.AxisTitleRendererInfo == null)
      return;

    var parms = new RendererParameters
    {
      Graphics = gfx,
      RendererInfo = yari
    };
    var rcTitle = yari.Rect;
    rcTitle.Height = yari.AxisTitleRendererInfo.Height;
    rcTitle.Y += yari.Rect.Height - rcTitle.Height;
    yari.AxisTitleRendererInfo.Rect = rcTitle;
    var atr = new AxisTitleRenderer(parms);
    atr.Draw();
  }

  /// <summary>
  /// Draws a vertical axis's title over the axis's inner rectangle, at the width it was
  /// measured at.
  /// </summary>
  private static void DrawVerticalAxisTitle(XGraphics gfx, AxisRendererInfo yari)
  {
    if (yari.AxisTitleRendererInfo == null || yari.AxisTitleRendererInfo.AxisTitleText == "")
      return;

    var parms = new RendererParameters
    {
      Graphics = gfx,
      RendererInfo = yari
    };
    var width = yari.AxisTitleRendererInfo.Width;
    yari.AxisTitleRendererInfo.Rect = yari.InnerRect;
    yari.AxisTitleRendererInfo.Width = width;
    var atr = new AxisTitleRenderer(parms);
    atr.Draw();
  }

  /// <summary>
  /// Calculates all values necessary for scaling the axis like minimum/maximum scale or
  /// minor/major tick.
  /// </summary>
  private void InitScale(AxisRendererInfo rendererInfo)
  {
    CalcYAxis(out var yMin, out var yMax);
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

    foreach (Series series in ((Chart)rendererParms.DrawingItem).SeriesCollection)
    {
      foreach (Point point in series.Elements)
      {
        // Series.AddBlank puts a null here, which is the whole of what a blank is. The stacked
        // renderers' overrides of this method already test for it.
        if (point == null || double.IsNaN(point.value))
          continue;

        yMin = Math.Min(yMin, point.Value);
        yMax = Math.Max(yMax, point.Value);
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
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
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
      var (valueSumNeg, valueSumPos) = StackedSums(stacked, pointIdx);
      yMin = Math.Min(valueSumNeg, yMin);
      yMax = Math.Max(valueSumPos, yMax);
    }
  }

  /// <summary>
  /// The sums of the negative and of the positive values the series stack at one point.
  /// </summary>
  private static (double Negative, double Positive) StackedSums(SeriesRendererInfo[] stacked, int pointIdx)
  {
    double valueSumPos = 0, valueSumNeg = 0;
    foreach (var sri in stacked)
    {
      if (sri.PointRendererInfos.Length <= pointIdx)
        break;

      var column = (ColumnRendererInfo)sri.PointRendererInfos[pointIdx];
      if (double.IsNaN(column.Value))
        continue;

      if (column.Value < 0)
        valueSumNeg += column.Value;
      else
        valueSumPos += column.Value;
    }
    return (valueSumNeg, valueSumPos);
  }

  /// <summary>
  /// Calculates optimal minimum/maximum scale and minor/major tick based on yMin and yMax.
  /// </summary>
  protected static void FineTuneYAxis(AxisRendererInfo rendererInfo, double yMin, double yMax)
  {
    WidenEmptyOrFlatRange(ref yMin, ref yMax);
    StartFromZeroWhenFarFromIt(ref yMin, ref yMax);

    var stepWidth = StepWidth(yMax - yMin);
    var roundFactor = stepWidth * 0.5;

    // Whatever the axis was given explicitly wins over what is calculated here, one value at a
    // time; a chart with no axis object at all is given nothing.
    var axis = rendererInfo.Axis;
    rendererInfo.MajorTick = GivenOrCalculated(axis?.majorTick, stepWidth);
    rendererInfo.MinimumScale = GivenOrCalculated(axis?.minimumScale, RoundedMinimum(yMin, stepWidth, roundFactor));
    rendererInfo.MaximumScale = GivenOrCalculated(axis?.maximumScale, RoundedMaximum(yMax, stepWidth, roundFactor));
    rendererInfo.MinorTick = GivenOrCalculated(axis?.minorTick, rendererInfo.MajorTick / 5);
  }

  /// <summary>
  /// Gives a chart with no data a range of its own, and widens a range of one value into one
  /// that can be drawn against.
  /// </summary>
  private static void WidenEmptyOrFlatRange(ref double yMin, ref double yMax)
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
  }

  /// <summary>
  /// Starts the range at zero when the values are far enough from it: when the ratio between the
  /// larger and the smaller magnitude is 1.2 or more, the end nearer zero is moved to it.
  /// It's Excel's behavior.
  /// </summary>
  private static void StartFromZeroWhenFarFromIt(ref double yMin, ref double yMax)
  {
    if (yMin == 0)
      return;

    var allNegative = yMin < 0 && yMax < 0;
    if (allNegative)
    {
      if (yMin / yMax >= 1.2)
        yMax = 0;
    }
    else if (yMax / yMin >= 1.2)
    {
      yMin = 0;
    }
  }

  /// <summary>
  /// The distance between two major ticks for a range this wide: 1, 2 or 5 times a power of ten,
  /// whichever divides the range into a readable number of steps.
  /// </summary>
  private static double StepWidth(double deltaYRaw)
  {
    var digits = (int)(Math.Log(deltaYRaw, 10) + 1);
    var normed = deltaYRaw / Math.Pow(10, digits) * 10;

    double normedStepWidth = 1;
    if (normed < 2)
      normedStepWidth = 0.2f;
    else if (normed < 5)
      normedStepWidth = 0.5f;

    return normedStepWidth * Math.Pow(10.0, digits - 1.0);
  }

  /// <summary>
  /// The smallest value rounded outwards, away from the data, to a whole number of steps.
  /// </summary>
  private static double RoundedMinimum(double yMin, double stepWidth, double roundFactor)
  {
    var signumMin = yMin != 0 ? yMin / Math.Abs(yMin) : 0;
    return (int)(Math.Abs((yMin - roundFactor) / stepWidth) - 1 * signumMin) * stepWidth * signumMin;
  }

  /// <summary>
  /// The largest value rounded outwards, away from the data, to a whole number of steps.
  /// </summary>
  private static double RoundedMaximum(double yMax, double stepWidth, double roundFactor)
  {
    var signumMax = yMax != 0 ? yMax / Math.Abs(yMax) : 0;
    return (int)(Math.Abs((yMax + roundFactor) / stepWidth) + 1 * signumMax) * stepWidth * signumMax;
  }

  /// <summary>
  /// The value the axis was given, where it was given one - an axis leaves a value it was not
  /// given as NaN - and the calculated one otherwise.
  /// </summary>
  private static double GivenOrCalculated(double? given, double calculated) =>
    given is { } value && !double.IsNaN(value) ? value : calculated;

  /// <summary>
  /// Returns the default tick labels format string.
  /// </summary>
  protected override string GetDefaultTickLabelsFormat()
  {
    return "0.0";
  }
}
