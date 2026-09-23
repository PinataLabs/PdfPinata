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

using System;
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the legend renderer for all chart types.
/// </summary>
internal abstract class LegendRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the LegendRenderer class with the specified renderer parameters.
  /// </summary>
  internal LegendRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Layouts and calculates the space used by the legend.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    var lri = cri.LegendRendererInfo;
    if (lri == null)
      return;

    var parms = new RendererParameters { Graphics = rendererParms.Graphics };

    var verticalLegend = lri.Legend.docking == DockingType.Left || lri.Legend.docking == DockingType.Right;
    var maxMarkerArea = new XSize();
    var ler = new LegendEntryRenderer(parms);
    foreach (var leri in lri.Entries)
    {
      parms.RendererInfo = leri;
      ler.Format();

      maxMarkerArea.Width = Math.Max(leri.MarkerArea.Width, maxMarkerArea.Width);
      maxMarkerArea.Height = Math.Max(leri.MarkerArea.Height, maxMarkerArea.Height);
    }

    // Widen every entry to the widest marker *before* measuring, not after. Draw lays the entries
    // out by advancing along leri.Width and paints each marker at the equalized MarkerArea, so
    // totalling the widths first and equalizing afterwards measured a legend that was never the one
    // drawn. It only shows when the entries disagree about their marker width, which is a
    // combination chart: LegendEntryRenderer.Format gives a Line entry three times the marker of a
    // column, every column entry was then drawn three times wider than it had been measured, and
    // the entries printed on top of one another.
    foreach (var leri in lri.Entries)
    {
      leri.Width += maxMarkerArea.Width - leri.MarkerArea.Width;
      leri.Height = Math.Max(leri.Height, maxMarkerArea.Height);
      leri.MarkerArea = maxMarkerArea;
    }

    var paddingFactor = 1;
    if (lri.BorderPen != null)
      paddingFactor = 2;

    // The room the entries have across the chart: its width, less the legend's padding either
    // side. An entry wider than that is word wrapped to fit, whichever side the legend is docked
    // to - a single entry longer than the chart used to push the legend off both sides of it.
    var maxWidth = rendererParms.Box.Width
      - (LeftPadding + RightPadding) * paddingFactor;
    if (maxWidth > 0)
    {
      foreach (var leri in lri.Entries)
      {
        parms.RendererInfo = leri;
        ler.FitToWidth(maxWidth);
      }
    }

    if (verticalLegend)
    {
      foreach (var leri in lri.Entries)
      {
        lri.Width = Math.Max(lri.Width, leri.Width);
        lri.Height += leri.Height;
      }
      lri.Height += EntrySpacing * (lri.Entries.Length - 1);
    }
    else
    {
      LayoutRows(lri, maxWidth);
    }

    // Add padding to left, right, top and bottom
    lri.Width += (LeftPadding + RightPadding) * paddingFactor;
    lri.Height += (TopPadding + BottomPadding) * paddingFactor;
  }

  /// <summary>
  /// Sets out the entries of a legend docked above or below the chart side by side, starting a
  /// new row whenever the next entry would not fit in the room across the chart, and centres each
  /// row across the widest. The legend is as wide as its widest row and as tall as its rows.
  /// </summary>
  /// <remarks>
  /// All the entries used to go in one row however many there were, and the legend is centred on
  /// the chart, so a row wider than the chart ran off both sides of it - and off the page, for a
  /// chart as wide as the page (empira/PDFsharp#306). A legend that fits in one row is laid out
  /// exactly as it always was.
  /// </remarks>
  private static void LayoutRows(LegendRendererInfo lri, double maxWidth)
  {
    var rows = new List<(int First, int End, double Width)>();
    var first = 0;
    double x = 0, y = 0, rowHeight = 0;
    for (var idx = 0; idx < lri.Entries.Length; idx++)
    {
      var leri = lri.Entries[idx];
      if (idx > first && maxWidth > 0 && x + leri.Width > maxWidth)
      {
        rows.Add((first, idx, x - EntrySpacing));
        y += rowHeight + EntrySpacing;
        x = 0;
        rowHeight = 0;
        first = idx;
      }

      leri.Offset = new XPoint(x, y);
      x += leri.Width + EntrySpacing;
      rowHeight = Math.Max(rowHeight, leri.Height);
    }
    rows.Add((first, lri.Entries.Length, x - EntrySpacing));

    foreach (var row in rows)
      lri.Width = Math.Max(lri.Width, row.Width);
    lri.Height = y + rowHeight;

    foreach (var row in rows)
    {
      var shift = (lri.Width - row.Width) / 2;
      for (var idx = row.First; idx < row.End; idx++)
        lri.Entries[idx].Offset.X += shift;
    }
  }

  /// <summary>
  /// Draws the legend.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    var lri = cri.LegendRendererInfo;
    if (lri == null)
      return;

    var gfx = rendererParms.Graphics;
    var parms = new RendererParameters { Graphics = gfx };

    var ler = new LegendEntryRenderer(parms);

    var verticalLegend = lri.Legend.docking == DockingType.Left || lri.Legend.docking == DockingType.Right;
    var paddingFactor = 1;
    if (lri.BorderPen != null)
      paddingFactor = 2;
    var legendRect = lri.Rect;
    legendRect.X += LeftPadding * paddingFactor;
    legendRect.Y += TopPadding * paddingFactor;
    foreach (var leri in cri.LegendRendererInfo.Entries)
    {
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
        legendRect.Y += entryRect.Height + EntrySpacing;
    }

    // Draw border around legend
    if (lri.BorderPen != null)
    {
      var borderRect = lri.Rect;
      borderRect.X += LeftPadding;
      borderRect.Y += TopPadding;
      borderRect.Width -= LeftPadding + RightPadding;
      borderRect.Height -= TopPadding + BottomPadding;
      gfx.DrawRectangle(lri.BorderPen, borderRect);
    }
  }

  /// <summary>
  /// Used to insert a padding on the left.
  /// </summary>
  protected const double LeftPadding = 6;

  /// <summary>
  /// Used to insert a padding on the right.
  /// </summary>
  protected const double RightPadding = 6;

  /// <summary>
  /// Used to insert a padding at the top.
  /// </summary>
  protected const double TopPadding = 6;

  /// <summary>
  /// Used to insert a padding at the bottom.
  /// </summary>
  protected const double BottomPadding = 6;

  /// <summary>
  /// Used to insert a padding between entries.
  /// </summary>
  protected const double EntrySpacing = 5;

  /// <summary>
  /// Default line width used for the legend's border.
  /// </summary>
  protected const double DefaultLineWidth = 0.14; // 0.05 mm
}
