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
/// Represents the plot area of a column chart or a bar chart, which is a column chart turned on its
/// side: each point a rectangle, standing on the category axis and reaching along the value axis.
/// Which way the category axis runs is the orientation the renderer is given. Every position is
/// worked out in chart space, as <see cref="PlotOrientation.At"/> places it, and the matrix
/// <see cref="ColumnLikePlotAreaRenderer.FormatMatrix"/> sets up takes it to the page, so that the
/// two orientations share every line of the arithmetic.
/// </summary>
internal abstract class ColumnPlotAreaRenderer : ColumnLikePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnPlotAreaRenderer class with the
  /// specified renderer parameters and the orientation of the chart's category axis.
  /// </summary>
  internal ColumnPlotAreaRenderer(RendererParameters parms, AxisOrientation categoryAxis) : base(parms)
  {
    this.categoryAxis = categoryAxis;
  }

  /// <summary>
  /// Which way the chart's category axis runs: across for a column chart, up for a bar chart.
  /// </summary>
  protected readonly AxisOrientation categoryAxis;

  /// <summary>
  /// Layouts and calculates the space for each column.
  /// </summary>
  internal override void Format()
  {
    FormatMatrix(categoryAxis);

    // Nothing to plot leaves the matrix the identity, and a column worked out against it would be
    // a rectangle in chart units rather than on the page. So the columns are not worked out at all,
    // and stay undrawn and unlabelled.
    if (NothingToPlot((ChartRendererInfo)rendererParms.RendererInfo))
      return;

    CalcColumns();
    DecideWhichAreDrawn();
  }

  /// <summary>
  /// Decides once, for every column, whether it is drawn: here, after the positions are worked out
  /// and before the data labels are formatted, so that Draw and the data label renderer ask the
  /// same question and get the same answer. A column off the scale is left undrawn rather than
  /// clipped, and takes its label with it.
  /// </summary>
  private void DecideWhichAreDrawn()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;
    foreach (var column in cri.SeriesRendererInfos.SelectMany(sri => sri.PointRendererInfos.Cast<ColumnRendererInfo>()))
      column.Drawn = IsDataInside(yMin, yMax, column);
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

    // Under some circumstances it is possible that no zero base line will be drawn,
    // e. g. because of unfavourable minimum/maximum scale and/or major tick, so force to draw
    // a zero base line if necessary.
    DrawZeroBaseLine(cri, gfx, categoryAxis.At(xMin, 0), categoryAxis.At(xMax, 0));

    var state = gfx.Save();
    var columns = cri.SeriesRendererInfos
      .SelectMany(sri => sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      .ToList();

    // Draw columns. A column off the scale is not drawn; Format decided which, and clipping does not make sense.
    foreach (var column in columns.Where(column => column.Drawn))
      gfx.DrawRectangle(column.FillFormat, column.Rect);

    // Draw borders around column.
    // A border can overlap neighbor columns, so it is important to draw borders at the end.
    foreach (var column in columns.Where(column => column.Drawn && column.LineFormat.Width is > 0))
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
  /// The rectangle on the page of a column reaching from category x0 to x1 and from value y0 to
  /// y1. The corners are taken through the matrix and the rectangle made of the two, whichever
  /// way round the orientation turns them, so a column and a bar come out of the same call.
  /// </summary>
  protected XRect ColumnRect(double x0, double y0, double x1, double y1)
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    corners[0] = categoryAxis.At(x0, y0);
    corners[1] = categoryAxis.At(x1, y1);
    cri.PlotAreaRendererInfo.Matrix.TransformPoints(corners);
    return new XRect(corners[0], corners[1]);
  }

  /// <summary>
  /// The two corners <see cref="ColumnRect"/> transforms, kept rather than made for every column.
  /// </summary>
  private readonly XPoint[] corners = new XPoint[2];

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected abstract void CalcColumns();

  /// <summary>
  /// Whether a point lies within the scale from yMin to yMax and is to be drawn. One that does not
  /// is left undrawn rather than clipped. A blank never does.
  /// </summary>
  protected abstract bool IsDataInside(double yMin, double yMax, ColumnRendererInfo point);
}
