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


namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the legend renderer specific to bar charts.
/// </summary>
internal class BarClusteredLegendRenderer : ColumnLikeLegendRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarClusteredLegendRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarClusteredLegendRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Draws the legend.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    var lri = cri.LegendRendererInfo;
    if (lri == null)
      return;

    var gfx = this.rendererParms.Graphics;
    var parms = new RendererParameters();
    parms.Graphics = gfx;

    var ler = new LegendEntryRenderer(parms);

    var verticalLegend = lri.Legend.docking == DockingType.Left || lri.Legend.docking == DockingType.Right;
    var paddingFactor = 1;
    if (lri.BorderPen != null)
      paddingFactor = 2;
    var legendRect = lri.Rect;
    legendRect.X += LegendRenderer.LeftPadding * paddingFactor;
    if (verticalLegend)
      legendRect.Y = legendRect.Bottom - LegendRenderer.BottomPadding * paddingFactor;
    else
      legendRect.Y += LegendRenderer.TopPadding * paddingFactor;

    foreach (var leri in cri.LegendRendererInfo.Entries)
    {
      if (verticalLegend)
        legendRect.Y -= leri.Height;

      var entryRect = legendRect;
      if (!verticalLegend)
      {
        entryRect.X += leri.Offset.X;
        entryRect.Y += leri.Offset.Y;
      }
      entryRect.Width = leri.Width;
      entryRect.Height = leri.Height;

      leri.Rect = entryRect;
      parms.RendererInfo = leri;
      ler.Draw();

      if (verticalLegend)
        legendRect.Y -= LegendRenderer.EntrySpacing;
    }

    // Draw border around legend
    if (lri.BorderPen != null)
    {
      var borderRect = lri.Rect;
      borderRect.X += LegendRenderer.LeftPadding;
      borderRect.Y += LegendRenderer.TopPadding;
      borderRect.Width -= LegendRenderer.LeftPadding + LegendRenderer.RightPadding;
      borderRect.Height -= LegendRenderer.TopPadding + LegendRenderer.BottomPadding;
      gfx.DrawRectangle(lri.BorderPen, borderRect);
    }
  }
}
