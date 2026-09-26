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

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a plot area renderer of stacked columns, i. e. all columns are drawn one on another -
/// or of stacked bars, which are the same columns turned on their side.
/// </summary>
internal class ColumnStackedPlotAreaRenderer : ColumnPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnStackedPlotAreaRenderer class with the
  /// specified renderer parameters and the orientation of the chart's category axis.
  /// </summary>
  internal ColumnStackedPlotAreaRenderer(RendererParameters parms, AxisOrientation categoryAxis)
    : base(parms, categoryAxis)
  {
  }

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected override void CalcColumns()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var xMin = cri.XAxisRendererInfo.MinimumScale;
    var xMajorTick = cri.XAxisRendererInfo.MajorTick;

    var maxPoints = 0;
    foreach (var sri in cri.SeriesRendererInfos)
      maxPoints = Math.Max(maxPoints, sri.Series.Elements.Count);

    var x = xMin + xMajorTick / 2;

    // Space used by one column.
    var columnWidth = xMajorTick * 0.75 / 2;

    for (var pointIdx = 0; pointIdx < maxPoints; ++pointIdx)
    {
      StackColumns(cri, pointIdx, x - columnWidth, x + columnWidth);
      x++; // Next stacked column.
    }
  }

  /// <summary>
  /// Stacks the columns every series has at one point, negative values below zero and the rest above.
  /// </summary>
  private void StackColumns(ChartRendererInfo cri, int pointIdx, double x0, double x1)
  {
    double yMin = 0, yMax = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.PointRendererInfos.Length <= pointIdx)
        break;

      var column = (ColumnRendererInfo)sri.PointRendererInfos[pointIdx];
      if (double.IsNaN(column.Value))
        continue;

      var (y0, y1) = StackOnto(column.Value, ref yMin, ref yMax);
      column.StackedFrom = y0;
      column.StackedTo = y1;
      column.Rect = ColumnRect(x0, y0, x1, y1);
    }
  }

  /// <summary>
  /// The bottom and top of a value stacked onto the negative or the positive pile, which it grows.
  /// </summary>
  private static (double Y0, double Y1) StackOnto(double y, ref double yMin, ref double yMax)
  {
    if (y < 0)
    {
      var bottom = yMin;
      yMin += y;
      return (bottom + y, bottom);
    }

    var top = yMax;
    yMax += y;
    return (top, top + y);
  }

  /// <summary>
  /// Whether the whole of a stacked column, from where it starts on its pile to where the pile
  /// reaches with it, lies within the scale from yMin to yMax.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, ColumnRendererInfo point)
  {
    // A scale worked out from the data holds every pile, but one the caller set need not, and a
    // stacked column's own value is a length rather than a position on it. A blank has no extent,
    // and NaN fails both tests.
    //
    // A pile is a running sum, and a sum of decimals lands a rounding error either side of the
    // total it spells: 0.1 + 0.2 is a hair over 0.3, and a scale set to end at the total would
    // lose its top segment for it. A billionth of the scale is far below anything drawn.
    var slack = (yMax - yMin) * 1e-9;
    return point.StackedFrom >= yMin - slack && point.StackedTo <= yMax + slack;
  }
}
