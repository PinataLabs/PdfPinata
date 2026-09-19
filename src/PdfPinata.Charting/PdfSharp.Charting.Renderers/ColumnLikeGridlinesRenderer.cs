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
/// Represents gridlines used by column or line charts, i. e. X axis grid will be rendered
/// from top to bottom and Y axis grid will be rendered from left to right of the plot area.
/// </summary>
internal class ColumnLikeGridlinesRenderer : GridlinesRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnLikeGridlinesRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnLikeGridlinesRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Draws the gridlines into the plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var plotAreaRect = cri.plotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaRect))
      return;

    var xari = cri.xAxisRendererInfo;
    var yari = cri.yAxisRendererInfo;

    var xMin = xari.MinimumScale;
    var xMax = xari.MaximumScale;
    var yMin = yari.MinimumScale;
    var yMax = yari.MaximumScale;
    var xMajorTick = xari.MajorTick;
    var yMajorTick = yari.MajorTick;
    var xMinorTick = xari.MinorTick;
    var yMinorTick = yari.MinorTick;

    var matrix = cri.plotAreaRendererInfo.matrix;

    LineFormatRenderer lineFormatRenderer;
    var gfx = this.rendererParms.Graphics;

    var points = new XPoint[2];
    if (xari.MinorGridlinesLineFormat != null)
    {
      lineFormatRenderer = new LineFormatRenderer(gfx, xari.MinorGridlinesLineFormat);
      for (var x = xMin + xMinorTick; x < xMax; x += xMinorTick)
      {
        points[0].X = x;
        points[0].Y = yMin;
        points[1].X = x;
        points[1].Y = yMax;
        matrix.TransformPoints(points);
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }

    if (xari.MajorGridlinesLineFormat != null)
    {
      lineFormatRenderer = new LineFormatRenderer(gfx, xari.MajorGridlinesLineFormat);
      for (var x = xMin; x <= xMax; x += xMajorTick)
      {
        points[0].X = x;
        points[0].Y = yMin;
        points[1].X = x;
        points[1].Y = yMax;
        matrix.TransformPoints(points);
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }

    if (yari.MinorGridlinesLineFormat != null)
    {
      lineFormatRenderer = new LineFormatRenderer(gfx, yari.MinorGridlinesLineFormat);
      for (var y = yMin + yMinorTick; y < yMax; y += yMinorTick)
      {
        points[0].X = xMin;
        points[0].Y = y;
        points[1].X = xMax;
        points[1].Y = y;
        matrix.TransformPoints(points);
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }

    if (yari.MajorGridlinesLineFormat != null)
    {
      lineFormatRenderer = new LineFormatRenderer(gfx, yari.MajorGridlinesLineFormat);
      for (var y = yMin; y <= yMax; y += yMajorTick)
      {
        points[0].X = xMin;
        points[0].Y = y;
        points[1].X = xMax;
        points[1].Y = y;
        matrix.TransformPoints(points);
        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }
  }
}
