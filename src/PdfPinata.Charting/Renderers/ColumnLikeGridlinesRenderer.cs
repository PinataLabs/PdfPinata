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
/// Represents the gridlines of every chart with a category and a value axis: across the plot area
/// at each tick of the X axis and along it at each tick of the Y axis. On a column, line or area
/// chart the X axis's gridlines run from top to bottom and the Y axis's from left to right; on a bar
/// chart, which is the same chart turned on its side, the other way round. Which is the orientation
/// the renderer is given, and <see cref="PlotOrientation.At"/> is the only place it is read.
/// </summary>
internal class ColumnLikeGridlinesRenderer : GridlinesRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnLikeGridlinesRenderer class with the
  /// specified renderer parameters and the orientation of the chart's category axis.
  /// </summary>
  internal ColumnLikeGridlinesRenderer(RendererParameters parms, AxisOrientation categoryAxis)
    : base(parms)
  {
    this.categoryAxis = categoryAxis;
  }

  /// <summary>
  /// Which way the chart's category axis runs.
  /// </summary>
  private readonly AxisOrientation categoryAxis;

  /// <summary>
  /// Draws the gridlines into the plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var plotAreaRect = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaRect))
      return;

    var points = new XPoint[2];
    DrawXGridlines(cri, points, minor: true);
    DrawXGridlines(cri, points, minor: false);
    DrawYGridlines(cri, points, minor: true);
    DrawYGridlines(cri, points, minor: false);
  }

  /// <summary>
  /// Draws a gridline across the value axis at every minor or every major tick of the X axis.
  /// </summary>
  private void DrawXGridlines(ChartRendererInfo cri, XPoint[] points, bool minor)
  {
    var xari = cri.XAxisRendererInfo;
    var lineFormat = minor ? xari.MinorGridlinesLineFormat : xari.MajorGridlinesLineFormat;
    if (lineFormat == null)
      return;

    var yari = cri.YAxisRendererInfo;
    var matrix = cri.PlotAreaRendererInfo.Matrix;
    var lineFormatRenderer = new LineFormatRenderer(rendererParms.Graphics, lineFormat);
    var tick = minor ? xari.MinorTick : xari.MajorTick;
    for (var x = FirstGridline(xari, minor); IsOnGrid(x, xari.MaximumScale, minor); x += tick)
    {
      points[0] = categoryAxis.At(x, yari.MinimumScale);
      points[1] = categoryAxis.At(x, yari.MaximumScale);
      matrix.TransformPoints(points);
      lineFormatRenderer.DrawLine(points[0], points[1]);
    }
  }

  /// <summary>
  /// Draws a gridline across the category axis at every minor or every major tick of the Y axis.
  /// </summary>
  private void DrawYGridlines(ChartRendererInfo cri, XPoint[] points, bool minor)
  {
    var yari = cri.YAxisRendererInfo;
    var lineFormat = minor ? yari.MinorGridlinesLineFormat : yari.MajorGridlinesLineFormat;
    if (lineFormat == null)
      return;

    var xari = cri.XAxisRendererInfo;
    var matrix = cri.PlotAreaRendererInfo.Matrix;
    var lineFormatRenderer = new LineFormatRenderer(rendererParms.Graphics, lineFormat);
    var tick = minor ? yari.MinorTick : yari.MajorTick;
    for (var y = FirstGridline(yari, minor); IsOnGrid(y, yari.MaximumScale, minor); y += tick)
    {
      points[0] = categoryAxis.At(xari.MinimumScale, y);
      points[1] = categoryAxis.At(xari.MaximumScale, y);
      matrix.TransformPoints(points);
      lineFormatRenderer.DrawLine(points[0], points[1]);
    }
  }

  /// <summary>
  /// Where the first gridline goes. Minor gridlines leave out both ends of the scale, which is
  /// where the major ones fall.
  /// </summary>
  private static double FirstGridline(AxisRendererInfo ari, bool minor)
    => minor ? ari.MinimumScale + ari.MinorTick : ari.MinimumScale;

  /// <summary>
  /// Whether a gridline at this value is still on the scale.
  /// </summary>
  private static bool IsOnGrid(double value, double max, bool minor)
    => value < max || !minor && value == max;
}
