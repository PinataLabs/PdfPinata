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
/// Represents a plot area renderer of stacked bars, i. e. all bars are drawn one on another.
/// </summary>
internal class BarStackedPlotAreaRenderer : BarPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarStackedPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarStackedPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Calculates the position, width and height of each bar of all series.
  /// </summary>
  protected override void CalcBars()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var xMax = cri.XAxisRendererInfo.MaximumScale;
    var xMajorTick = cri.XAxisRendererInfo.MajorTick;

    var maxPoints = 0;
    foreach (var sri in cri.SeriesRendererInfos)
      maxPoints = Math.Max(maxPoints, sri.Series.Elements.Count);

    // Space used by one bar.
    var x = xMax - xMajorTick / 2;
    var columnWidth = xMajorTick * 0.75 / 2;

    var points = new XPoint[2];
    for (var pointIdx = 0; pointIdx < maxPoints; ++pointIdx)
    {
      StackBars(cri, points, pointIdx, x - columnWidth, x + columnWidth);
      x--; // Next stacked column.
    }
  }

  /// <summary>
  /// Stacks the bars every series has at one point, negative values below zero and the rest above.
  /// </summary>
  private static void StackBars(ChartRendererInfo cri, XPoint[] points, int pointIdx, double x0, double x1)
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

      points[0].Y = x0; // oben links
      points[0].X = y0;
      points[1].Y = x1; // unten rechts
      points[1].X = y1;

      cri.PlotAreaRendererInfo.Matrix.TransformPoints(points);

      column.Rect = new XRect(points[0].X,
        points[0].Y,
        points[1].X - points[0].X,
        points[1].Y - points[0].Y);
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
  /// If yValue is within the range from yMin to yMax returns true, otherwise false.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, double yValue)
  {
    return yValue <= yMax && yValue >= yMin;
  }
}
