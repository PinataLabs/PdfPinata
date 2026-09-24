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
/// Represents a plot area renderer of clustered columns, i. e. all columns are drawn side by side.
/// </summary>
internal abstract class ColumnPlotAreaRenderer : ColumnLikePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Layouts and calculates the space for each column.
  /// </summary>
  internal override void Format()
  {
    base.Format();
    CalcColumns();
  }

  /// <summary>
  /// Draws the content of the column plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var plotAreaBox = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaBox))
      return;

    var gfx = rendererParms.Graphics;

    var xMin = cri.XAxisRendererInfo.MinimumScale;
    var xMax = cri.XAxisRendererInfo.MaximumScale;
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;

    // Under some circumstances it is possible that no zero base line will be drawn,
    // e. g. because of unfavourable minimum/maximum scale and/or major tick, so force to draw
    // a zero base line if necessary.
    DrawZeroBaseLine(cri, gfx, new XPoint(xMin, 0), new XPoint(xMax, 0));

    var state = gfx.Save();
    var columns = cri.SeriesRendererInfos
      .SelectMany(sri => sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      .ToList();

    // Draw columns. Do not draw a column if its value is outside yMin/yMax range. Clipping does not make sense.
    foreach (var column in columns.Where(column => IsDataInside(yMin, yMax, column.Value)))
      gfx.DrawRectangle(column.FillFormat, column.Rect);

    // Draw borders around column.
    // A border can overlap neighbor columns, so it is important to draw borders at the end.
    foreach (var column in columns.Where(column => IsDataInside(yMin, yMax, column.Value) && column.LineFormat.Width is > 0))
      new LineFormatRenderer(gfx, column.LineFormat).DrawRectangle(column.Rect);

    gfx.Restore(state);
  }

  /// <summary>
  /// Draws the zero base line from one point to another, in chart coordinates, when the Y axis has
  /// gridlines and its scale spans zero.
  /// </summary>
  private static void DrawZeroBaseLine(ChartRendererInfo cri, XGraphics gfx, XPoint from, XPoint to)
  {
    var yAxis = cri.YAxisRendererInfo;
    var gridlines = yAxis.MinorGridlinesLineFormat ?? yAxis.MajorGridlinesLineFormat;
    if (gridlines == null || yAxis.MinimumScale >= 0 || yAxis.MaximumScale <= 0)
      return;

    var points = new[] { from, to };
    cri.PlotAreaRendererInfo.Matrix.TransformPoints(points);
    new LineFormatRenderer(gfx, gridlines).DrawLine(points[0], points[1]);
  }

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected abstract void CalcColumns();

  /// <summary>
  /// If yValue is within the range from yMin to yMax returns true, otherwise false.
  /// </summary>
  protected abstract bool IsDataInside(double yMin, double yMax, double yValue);
}
