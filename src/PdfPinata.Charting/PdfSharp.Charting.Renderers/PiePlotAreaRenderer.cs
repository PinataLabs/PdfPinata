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
/// Represents the base for all pie plot area renderer.
/// </summary>
internal abstract class PiePlotAreaRenderer : PlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the PiePlotAreaRenderer class
  /// with the specified renderer parameters.
  /// </summary>
  internal PiePlotAreaRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Layouts and calculates the space used by the pie plot area.
  /// </summary>
  internal override void Format()
  {
    CalcSectors();
  }

  /// <summary>
  /// Draws the content of the pie plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    var plotAreaRect = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaRect))
      return;

    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var gfx = this.rendererParms.Graphics;
    var state = gfx.Save();

    // Draw sectors.
    var sri = cri.SeriesRendererInfos[0];
    foreach (var sector in sri.PointRendererInfos.Cast<SectorRendererInfo>())
    {
      if (!double.IsNaN(sector.StartAngle) && !double.IsNaN(sector.SweepAngle))
        gfx.DrawPie(sector.FillFormat, sector.Rect, sector.StartAngle, sector.SweepAngle);
    }

    // Draw border of the sectors. A pen of width 0 is a hidden border, and PDF would stroke it
    // as a hairline.
    foreach (var sector in sri.PointRendererInfos.Cast<SectorRendererInfo>())
    {
      if (!double.IsNaN(sector.StartAngle) && !double.IsNaN(sector.SweepAngle) && sector.LineFormat.Width > 0)
        gfx.DrawPie(sector.LineFormat, sector.Rect, sector.StartAngle, sector.SweepAngle);
    }

    gfx.Restore(state);
  }

  /// <summary>
  /// Calculates the specific positions for each sector.
  /// </summary>
  protected abstract void CalcSectors();
}
