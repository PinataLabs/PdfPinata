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
/// Renders the plot area used by line charts. 
/// </summary>
internal class LinePlotAreaRenderer : ColumnLikePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the LinePlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal LinePlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Draws the content of the line plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var plotAreaRect = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaRect))
      return;

    var gfx = this.rendererParms.Graphics;
    var state = gfx.Save();
    //gfx.SetClip(plotAreaRect, XCombineMode.Intersect);
    gfx.IntersectClip(plotAreaRect);

    //TODO null-Values müssen berücksichtigt werden.
    //     Verbindungspunkte können fehlen, je nachdem wie null-Values behandelt werden sollen.
    //     (NotPlotted, Interpolate etc.)

    // Draw lines and markers for each data series.
    var matrix = cri.PlotAreaRendererInfo.Matrix;

    var xMajorTick = cri.XAxisRendererInfo.MajorTick;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      var count = sri.Series.Elements.Count;

      // A line needs two points to be a line, and DrawLines says so by throwing. A series with
      // fewer has nothing to draw rather than something to complain about.
      if (count < 2)
        continue;

      var points = new XPoint[count];
      for (var idx = 0; idx < count; idx++)
      {
        // Off the series rather than through PointRendererInfos, which the line chart renderer
        // does not fill in. A blank is a null element, and joins the values that are already
        // drawn at zero - which is what the TODO above is about, and is not settled here.
        var element = sri.Series.Elements[idx];
        var v = element == null ? double.NaN : element.Value;
        if (double.IsNaN(v))
          v = 0;
        points[idx] = new XPoint(idx + xMajorTick / 2, v);
      }
      matrix.TransformPoints(points);

      // A line format that says Visible = false converts to a pen of width 0, which to PDF is the
      // thinnest line the device can draw rather than no line. The markers are still drawn.
      if (sri.LineFormat.Width > 0)
        gfx.DrawLines(sri.LineFormat, points);
      DrawMarker(gfx, points, sri);
    }

    //gfx.ResetClip();
    gfx.Restore(state);
  }

  /// <summary>
  /// Draws all markers given in rendererInfo at the positions specified by points.
  /// </summary>
  private static void DrawMarker(XGraphics graphics, XPoint[] points, SeriesRendererInfo rendererInfo)
  {
    foreach (var pos in points)
      MarkerRenderer.Draw(graphics, pos, rendererInfo.MarkerRendererInfo);
  }
}
