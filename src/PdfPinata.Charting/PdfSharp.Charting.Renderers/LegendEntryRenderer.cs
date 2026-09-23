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
/// Represents the renderer for a legend entry.
/// </summary>
internal class LegendEntryRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the LegendEntryRenderer class with the specified renderer
  /// parameters.
  /// </summary>
  internal LegendEntryRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Calculates the space used by the legend entry.
  /// </summary>
  internal override void Format()
  {
    var gfx = rendererParms.Graphics;
    var leri = (LegendEntryRendererInfo)rendererParms.RendererInfo;

    // Initialize
    leri.MarkerArea.Width = MaxLegendMarkerWidth;
    leri.MarkerArea.Height = MaxLegendMarkerHeight;
    leri.MarkerSize = new XSize
    {
      Width = leri.MarkerArea.Width,
      Height = leri.MarkerArea.Height
    };
    if (leri.SeriesRendererInfo.Series.chartType == ChartType.Line)
      leri.MarkerArea.Width *= 3;
    leri.Width = leri.MarkerArea.Width;
    leri.Height = leri.MarkerArea.Height;
    leri.LineHeight = leri.Height;
    leri.Lines = [];

    if (leri.EntryText == "")
      return;

    // A line break in the text starts a new line of the entry. DrawString draws one line and
    // drops a line feed, so a name written over two lines used to be drawn as one run-on word.
    leri.Lines = leri.EntryText.Split(LineBreaks, StringSplitOptions.None);
    Measure(gfx, leri);
    if (leri.SeriesRendererInfo.Series.chartType == ChartType.Line)
    {
      leri.MarkerSize.Width = leri.SeriesRendererInfo.MarkerRendererInfo.MarkerSize.Point;
      leri.MarkerArea.Width = Math.Max(3 * leri.MarkerSize.Width, leri.MarkerArea.Width);
    }

    leri.MarkerArea.Height = Math.Min(leri.MarkerArea.Height, leri.LineHeight);
    leri.MarkerSize.Height = Math.Min(leri.MarkerSize.Height, leri.LineHeight);
    leri.Width = leri.TextSize.Width + leri.MarkerArea.Width + SpacingBetweenMarkerAndText;
    leri.Height = leri.TextSize.Height;
  }

  /// <summary>
  /// Word wraps the entry's text so that the entry, marker and all, is no wider than the given
  /// width, and measures it again. An entry already narrow enough is left exactly as it was, and a
  /// single word wider than the room on its own is kept whole on a line of its own.
  /// </summary>
  internal void FitToWidth(double maxWidth)
  {
    var leri = (LegendEntryRendererInfo)rendererParms.RendererInfo;
    if (leri.Lines.Length == 0 || leri.Width <= maxWidth)
      return;

    var gfx = rendererParms.Graphics;
    var font = leri.LegendRendererInfo.Font;
    var textWidth = maxWidth - leri.MarkerArea.Width - SpacingBetweenMarkerAndText;
    var lines = new List<string>();
    foreach (var paragraph in leri.EntryText.Split(LineBreaks, StringSplitOptions.None))
    {
      if (gfx.MeasureString(paragraph, font).Width <= textWidth)
      {
        lines.Add(paragraph);
        continue;
      }

      string line = null;
      foreach (var word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
      {
        if (line == null)
        {
          line = word;
        }
        else if (gfx.MeasureString(line + " " + word, font).Width <= textWidth)
        {
          line += " " + word;
        }
        else
        {
          lines.Add(line);
          line = word;
        }
      }
      lines.Add(line ?? "");
    }

    leri.Lines = lines.ToArray();
    Measure(gfx, leri);
    leri.Width = leri.TextSize.Width + leri.MarkerArea.Width + SpacingBetweenMarkerAndText;
    leri.Height = Math.Max(leri.Height, leri.TextSize.Height);
  }

  /// <summary>
  /// Sets the entry's text size and line height from its lines: as wide as the widest, and one
  /// line height for each. A single line is measured exactly as the whole text always was.
  /// </summary>
  private static void Measure(XGraphics gfx, LegendEntryRendererInfo leri)
  {
    var font = leri.LegendRendererInfo.Font;
    var size = new XSize();
    foreach (var line in leri.Lines)
    {
      // An empty line still takes a line's height; measuring a space says how much.
      var measured = gfx.MeasureString(line.Length > 0 ? line : " ", font);
      size.Width = Math.Max(size.Width, line.Length > 0 ? measured.Width : 0);
      size.Height = Math.Max(size.Height, measured.Height);
    }

    leri.LineHeight = size.Height;
    size.Height *= leri.Lines.Length;
    leri.TextSize = size;
  }

  /// <summary>
  /// Draws one legend entry.
  /// </summary>
  internal override void Draw()
  {
    var gfx = rendererParms.Graphics;
    var leri = (LegendEntryRendererInfo)rendererParms.RendererInfo;

    // The marker keys the first line of the entry. For an entry of one line that is the middle
    // of the entry, as it always was; for one of several it is not.
    var keyHeight = leri.Lines.Length > 1 ? leri.LineHeight : leri.Height;

    XRect rect;
    if (leri.SeriesRendererInfo.Series.chartType == ChartType.Line)
    {
      // Draw line, unless the series' own line is hidden: a pen of width 0 is how a line format
      // that says Visible = false comes out of the converter.
      if (leri.SeriesRendererInfo.LineFormat.Width > 0)
      {
        var posLineStart = new XPoint(leri.X, leri.Y + keyHeight / 2);
        var posLineEnd = new XPoint(leri.X + leri.MarkerArea.Width, leri.Y + keyHeight / 2);
        gfx.DrawLine(new XPen(((XSolidBrush)leri.MarkerBrush).Color), posLineStart, posLineEnd);
      }

      // Draw marker.
      var x = leri.X + leri.MarkerArea.Width / 2;
      var posMarker = new XPoint(x, leri.Y + keyHeight / 2);
      MarkerRenderer.Draw(gfx, posMarker, leri.SeriesRendererInfo.MarkerRendererInfo);
    }
    else
    {
      // Draw series rectangle for column, bar or pie charts.
      rect = new XRect(leri.X, leri.Y, leri.MarkerArea.Width, leri.MarkerArea.Height);
      rect.Y += (keyHeight - leri.MarkerArea.Height) / 2;
      var border = leri.MarkerPen is { Width: > 0 } ? leri.MarkerPen : null;
      gfx.DrawRectangle(border, leri.MarkerBrush, rect);
    }

    // Draw text, one line under another.
    if (leri.EntryText.Length == 0)
      return;

    rect = leri.Rect;
    rect.X += leri.MarkerArea.Width + LegendEntryRenderer.SpacingBetweenMarkerAndText;
    var format = new XStringFormat { LineAlignment = XLineAlignment.Near };
    if (leri.Lines.Length > 1)
      rect.Height = leri.LineHeight;
    foreach (var line in leri.Lines)
    {
      if (line.Length > 0)
        gfx.DrawString(line, leri.LegendRendererInfo.Font, leri.LegendRendererInfo.FontColor, rect, format);
      rect.Y += leri.LineHeight;
    }
  }

  /// <summary>
  /// The line breaks a legend entry's text is split at.
  /// </summary>
  private static readonly string[] LineBreaks = ["\r\n", "\n", "\r"];

  /// <summary>
  /// Maximum legend marker width in point.
  /// </summary>
  private const double MaxLegendMarkerWidth = 7; // 2.5 mm

  /// <summary>
  /// Maximum legend marker height in point.
  /// </summary>
  private const double MaxLegendMarkerHeight = 7; // 2.5 mm

  /// <summary>
  /// Insert spacing between marker and text in point.
  /// </summary>
  private const double SpacingBetweenMarkerAndText = 4.3; // 1.5 mm
}
