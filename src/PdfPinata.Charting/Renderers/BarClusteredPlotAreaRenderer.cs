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

using System.Linq;
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a plot area renderer of clustered bars, i. e. all bars are drawn side by side.
/// </summary>
internal class BarClusteredPlotAreaRenderer : BarPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarClusteredPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarClusteredPlotAreaRenderer(RendererParameters parms) : base(parms)
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
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;

    // Space shared by one clustered bar.
    var groupWidth = cri.XAxisRendererInfo.MajorTick;

    // Space used by one bar.
    var columnWidth = groupWidth * 0.75 / cri.SeriesRendererInfos.Length;

    var seriesIdx = 0;
    var points = new XPoint[2];
    foreach (var sri in cri.SeriesRendererInfos)
    {
      // Set x to first clustered bar for each series.
      var x = xMax - groupWidth / 2;
        
      // Offset for bars of a particular series from the start of a clustered bar.
      var dx = columnWidth * seriesIdx - columnWidth / 2 * cri.SeriesRendererInfos.Length;

      foreach (var column in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      {
        if (!double.IsNaN(column.Value))
        {
          var (y0, y1) = ValueRange(yMin, column.Value, yMax);
          column.Rect = BarRect(cri.PlotAreaRendererInfo, points, x - dx, x - dx - columnWidth, y0, y1);
        }
        x--; // Next clustered bar.
      }
      seriesIdx++;
    }
  }

  /// <summary>
  /// The bottom and top of a bar running from y0 to y1.
  /// </summary>
  private static (double Y0, double Y1) ValueRange(double y0, double y1, double yMax)
  {
    // Draw from zero base line, if it exists.
    if (y0 < 0 && yMax >= 0)
      y0 = 0;

    // y0 should always be lower than y1, i. e. draw bar from bottom to top. That is so of a
    // value below zero, and of one above zero and below a minimum set above zero too: it is off
    // the scale and not drawn, but its rectangle is still made, and one running backwards is of
    // negative size, which XRect refuses.
    return y1 < y0 ? (y1, y0) : (y0, y1);
  }

  /// <summary>
  /// Transforms one bar's corners into the plot area and makes a rectangle of them.
  /// </summary>
  private static XRect BarRect(PlotAreaRendererInfo pari, XPoint[] points, double x0, double x1, double y0, double y1)
  {
    points[0].X = y0; // upper left
    points[0].Y = x0;
    points[1].X = y1; // lower right
    points[1].Y = x1;

    pari.Matrix.TransformPoints(points);

    return new XRect(points[0].X,
      points[1].Y,
      points[1].X - points[0].X,
      points[0].Y - points[1].Y);
  }

  /// <summary>
  /// Whether the point's value is within the range from yMin to yMax, which a blank's never is.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, ColumnRendererInfo point)
  {
    return point.Value <= yMax && point.Value >= yMin;
  }
}
