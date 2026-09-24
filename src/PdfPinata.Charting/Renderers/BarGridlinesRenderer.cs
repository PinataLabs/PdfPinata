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
/// Represents gridlines used by bar charts, i. e. X axis grid will be rendered
/// from left to right and Y axis grid will be rendered from top to bottom of the plot area.
/// </summary>
internal class BarGridlinesRenderer : GridlinesRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarGridlinesRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarGridlinesRenderer(RendererParameters parms) : base(parms)
  {
  }

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
  /// Draws a gridline from left to right at every minor or every major tick of the X axis.
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
      points[0].Y = x;
      points[0].X = yari.MinimumScale;
      points[1].Y = x;
      points[1].X = yari.MaximumScale;
      matrix.TransformPoints(points);
      lineFormatRenderer.DrawLine(points[0], points[1]);
    }
  }

  /// <summary>
  /// Draws a gridline from top to bottom at every minor or every major tick of the Y axis.
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
      points[0].Y = xari.MinimumScale;
      points[0].X = y;
      points[1].Y = xari.MaximumScale;
      points[1].X = y;
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
