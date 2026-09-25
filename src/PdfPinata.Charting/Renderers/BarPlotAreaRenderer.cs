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
/// Represents a plot area renderer for bars.
/// </summary>
internal abstract class BarPlotAreaRenderer : PlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Layouts and calculates the space for each bar.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var xMin = cri.XAxisRendererInfo.MinimumScale;
    var xMax = cri.XAxisRendererInfo.MaximumScale;
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;

    var plotAreaBox = cri.PlotAreaRendererInfo.Rect;

    // Nothing to plot means a category scale of zero, and dividing by it puts NaN on the page.
    // See ColumnLikePlotAreaRenderer.Format, which says the same of the same thing.
    if (xMax <= xMin || yMax <= yMin)
    {
      cri.PlotAreaRendererInfo.Matrix = new XMatrix();
      return;
    }

    cri.PlotAreaRendererInfo.Matrix = new XMatrix();  //XMatrix.Identity;
    cri.PlotAreaRendererInfo.Matrix.TranslatePrepend(-yMin, xMin);
    cri.PlotAreaRendererInfo.Matrix.Scale(plotAreaBox.Width / (yMax - yMin), plotAreaBox.Height / (xMax - xMin), XMatrixOrder.Append);
    cri.PlotAreaRendererInfo.Matrix.Translate(plotAreaBox.X, plotAreaBox.Y, XMatrixOrder.Append);

    CalcBars();
  }

  /// <summary>
  /// Draws the content of the bar plot area.
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
    DrawZeroBaseLine(cri, gfx, new XPoint(0, xMin), new XPoint(0, xMax));

    var state = gfx.Save();
    var bars = cri.SeriesRendererInfos
      .SelectMany(sri => sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      .ToList();

    // Draw bars. Do not draw a bar if its value is outside yMin/yMax range. Clipping does not make sense.
    foreach (var bar in bars.Where(bar => IsDataInside(yMin, yMax, bar)))
      gfx.DrawRectangle(bar.FillFormat, bar.Rect);

    // Draw borders around bar.
    // A border can overlap neighbor bars, so it is important to draw borders at the end.
    foreach (var bar in bars.Where(bar => IsDataInside(yMin, yMax, bar) && bar.LineFormat.Width is > 0))
      new LineFormatRenderer(gfx, bar.LineFormat).DrawRectangle(bar.Rect);

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
  /// Calculates the position, width and height of each bar of all series.
  /// </summary>
  protected abstract void CalcBars();

  /// <summary>
  /// Whether a point lies within the scale from yMin to yMax and is to be drawn. One that does not
  /// is left undrawn rather than clipped. A blank never does.
  /// </summary>
  protected abstract bool IsDataInside(double yMin, double yMax, ColumnRendererInfo point);
}
