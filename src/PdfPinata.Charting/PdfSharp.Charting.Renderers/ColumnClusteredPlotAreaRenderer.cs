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

using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a plot area renderer of clustered columns, i. e. all columns are drawn side by side.
/// </summary>
internal class ColumnClusteredPlotAreaRenderer : ColumnPlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnClusteredPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnClusteredPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected override void CalcColumns()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    if (cri.seriesRendererInfos.Length == 0)
      return;

    var xMin = cri.xAxisRendererInfo.MinimumScale;
    var yMin = cri.yAxisRendererInfo.MinimumScale;
    var yMax = cri.yAxisRendererInfo.MaximumScale;

    var pointCount = 0;
    foreach (var sr in cri.seriesRendererInfos)
      pointCount += sr.series.Elements.Count;

    // Space shared by one clustered column.
    var groupWidth = cri.xAxisRendererInfo.MajorTick;

    // Space used by one column.
    var columnWidth = groupWidth * 3 / 4 / cri.seriesRendererInfos.Length;

    var seriesIdx = 0;
    var points = new XPoint[2];
    foreach (var sri in cri.seriesRendererInfos)
    {
      // Set x to first clustered column for each series.
      var x = xMin + groupWidth / 2;
        
      // Offset for columns of a particular series from the start of a clustered cloumn.
      var dx = (columnWidth * seriesIdx) - (columnWidth / 2 * cri.seriesRendererInfos.Length);

      foreach (ColumnRendererInfo column in sri.pointRendererInfos)
      {
        if (!double.IsNaN(column.Value))
        {
          var x0 = x + dx;
          var x1 = x + dx + columnWidth;
          var y0 = yMin;
          var y1 = column.Value;

          // Draw from zero base line, if it exists.
          if (y0 < 0 && yMax >= 0)
            y0 = 0;

          // y0 should always be lower than y1, i. e. draw column from bottom to top.
          if (y1 < 0 && y1 < y0)
          {
            var y = y0;
            y0 = y1;
            y1 = y;
          }

          points[0].X = x0; // upper left
          points[0].Y = y1;
          points[1].X = x1; // lower right
          points[1].Y = y0;

          cri.plotAreaRendererInfo.matrix.TransformPoints(points);

          column.Rect = new XRect(points[0].X,
            points[0].Y,
            points[1].X - points[0].X,
            points[1].Y - points[0].Y);
        }
        x++; // Next clustered column.
      }
      seriesIdx++;
    }
  }

  /// <summary>
  /// If yValue is within the range from yMin to yMax returns true, otherwise false.
  /// </summary>
  protected override bool IsDataInside(double yMin, double yMax, double yValue)
  {
    return yValue <= yMax && yValue >= yMin;
  }
}
