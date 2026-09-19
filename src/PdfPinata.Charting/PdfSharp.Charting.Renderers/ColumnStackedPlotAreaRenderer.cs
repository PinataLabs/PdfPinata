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
/// Represents a plot area renderer of stacked columns, i. e. all columns are drawn one on another.
/// </summary>
internal class ColumnStackedPlotAreaRenderer : ColumnPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnStackedPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnStackedPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected override void CalcColumns()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
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

    var points = new XPoint[2];
    for (var pointIdx = 0; pointIdx < maxPoints; ++pointIdx)
    {
      // Set x to first clustered column for each series.
      double yMin = 0, yMax = 0, y0, y1;
      var x0 = x - columnWidth;
      var x1 = x + columnWidth;

      foreach (var sri in cri.SeriesRendererInfos)
      {
        if (sri.PointRendererInfos.Length <= pointIdx)
          break;

        var column = (ColumnRendererInfo)sri.PointRendererInfos[pointIdx];
        if (!double.IsNaN(column.Value))
        {
          var y = column.Value;
          if (y < 0)
          {
            y0 = yMin + y;
            y1 = yMin;
            yMin += y;
          }
          else
          {
            y0 = yMax;
            y1 = yMax + y;
            yMax += y;
          }

          points[0].X = x0; // upper left
          points[0].Y = y1;
          points[1].X = x1; // lower right
          points[1].Y = y0;

          cri.PlotAreaRendererInfo.Matrix.TransformPoints(points);

          column.Rect = new XRect(points[0].X,
            points[0].Y,
            points[1].X - points[0].X,
            points[1].Y - points[0].Y);
        }
      }
      x++; // Next stacked column.
    }
  }

  /// <summary>
  /// Stacked columns are always inside.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, double yValue)
  {
    // A stacked column is inside the scale by construction - the scale was worked out from the
    // totals it is part of - so the range is not tested. A blank still is: there is no column for
    // it, and nothing to draw one with.
    return !double.IsNaN(yValue);
  }
}
