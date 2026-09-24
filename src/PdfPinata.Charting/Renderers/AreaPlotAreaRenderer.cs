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
/// Represents a plot area renderer of areas.
/// </summary>
internal class AreaPlotAreaRenderer : ColumnLikePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the AreaPlotAreaRenderer class
  /// with the specified renderer parameters.
  /// </summary>
  internal AreaPlotAreaRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Draws the content of the area plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    var plotAreaRect = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaRect))
      return;

    var gfx = rendererParms.Graphics;
    var state = gfx.Save();
    gfx.IntersectClip(plotAreaRect);

    var matrix = cri.PlotAreaRendererInfo.Matrix;
    var xMajorTick = cri.XAxisRendererInfo.MajorTick;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      var count = sri.PointRendererInfos.Length;
      var points = new XPoint[count + 2];
      points[0] = new XPoint(xMajorTick / 2, 0);
      for (var idx = 0; idx < count; idx++)
      {
        // Read through the renderer info rather than off the series, which would dereference a
        // blank. A blank reads as NaN and so joins the values that are already drawn at zero: an
        // area is a closed shape and has to have a point for every category to close over.
        var pointValue = sri.PointRendererInfos[idx].Value;
        if (double.IsNaN(pointValue))
          pointValue = 0;
        points[idx + 1] = new XPoint(idx + xMajorTick / 2, pointValue);
      }
      points[count + 1] = new XPoint(count - 1 + xMajorTick / 2, 0);
      matrix.TransformPoints(points);
      // A pen of width 0 is a hidden outline, and PDF would stroke it as a hairline.
      var outline = sri.LineFormat.Width > 0 ? sri.LineFormat : null;
      gfx.DrawPolygon(outline, sri.FillFormat, points, XFillMode.Winding);
    }

    gfx.Restore(state);
  }
}
