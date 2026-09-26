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

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a plot area renderer of clustered columns, i. e. all columns are drawn side by side -
/// or of clustered bars, which are the same columns turned on their side.
/// </summary>
internal class ColumnClusteredPlotAreaRenderer : ColumnPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnClusteredPlotAreaRenderer class with the
  /// specified renderer parameters and the orientation of the chart's category axis.
  /// </summary>
  internal ColumnClusteredPlotAreaRenderer(RendererParameters parms, AxisOrientation categoryAxis)
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
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;

    // Space shared by one clustered column.
    var groupWidth = cri.XAxisRendererInfo.MajorTick;

    // Space used by one column.
    var columnWidth = groupWidth * 3 / 4 / cri.SeriesRendererInfos.Length;

    var seriesIdx = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      // Set x to first clustered column for each series.
      var x = xMin + groupWidth / 2;
        
      // Offset for columns of a particular series from the start of a clustered column. The first
      // series is nearest the start of the category axis: leftmost on a column chart, lowest on a
      // bar chart, as in Excel.
      var dx = columnWidth * seriesIdx - columnWidth / 2 * cri.SeriesRendererInfos.Length;

      foreach (var column in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      {
        if (!double.IsNaN(column.Value))
        {
          var (y0, y1) = ValueRange(yMin, column.Value, yMax);
          column.Rect = ColumnRect(x + dx, y0, x + dx + columnWidth, y1);
        }
        x++; // Next clustered column.
      }
      seriesIdx++;
    }
  }

  /// <summary>
  /// The bottom and top of a column running from y0 to y1.
  /// </summary>
  private static (double Y0, double Y1) ValueRange(double y0, double y1, double yMax)
  {
    // Draw from zero base line, if it exists.
    if (y0 < 0 && yMax >= 0)
      y0 = 0;

    // y0 should always be lower than y1, i. e. draw column from bottom to top.
    return y1 < y0 ? (y1, y0) : (y0, y1);
  }

  /// <summary>
  /// Whether the point's value is within the range from yMin to yMax, which a blank's never is.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, ColumnRendererInfo point)
  {
    return point.Value <= yMax && point.Value >= yMin;
  }
}
