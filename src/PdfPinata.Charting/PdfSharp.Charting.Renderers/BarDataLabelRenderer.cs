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
using System.Linq;
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a data label renderer for bar charts.
/// </summary>
internal class BarDataLabelRenderer : DataLabelRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarDataLabelRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarDataLabelRenderer(RendererParameters parms) : base(parms)
  {
  }
    
  /// <summary>
  /// Calculates the space used by the data labels.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var gfx = this.rendererParms.Graphics;

      sri.DataLabelRendererInfo.Entries = new DataLabelEntryRendererInfo[sri.PointRendererInfos.Length];
      var index = 0;
      foreach (var column in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      {
        var dleri = new DataLabelEntryRendererInfo();
        if (sri.DataLabelRendererInfo.Type == DataLabelType.Percent)
          throw new InvalidOperationException(PSCSR.PercentNotSupportedByColumnDataLabel);

        // A blank has no value to write, so it is left with no text at all and Draw passes over
        // it. Writing what NaN formats to would put the word NaN on the plot area.
        if (sri.DataLabelRendererInfo.Type == DataLabelType.Value && !double.IsNaN(column.Value))
        {
          dleri.Text = column.Value.ToString(sri.DataLabelRendererInfo.Format);

          if (dleri.Text.Length > 0)
            dleri.Size = gfx.MeasureString(dleri.Text, sri.DataLabelRendererInfo.Font);
        }

        sri.DataLabelRendererInfo.Entries[index++] = dleri;
      }
    }

    CalcPositions();
  }

  /// <summary>
  /// Draws the data labels of the bar chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var gfx = this.rendererParms.Graphics;
      var font = sri.DataLabelRendererInfo.Font;
      var fontColor = sri.DataLabelRendererInfo.FontColor;
      var format = XStringFormats.Center;
      format.LineAlignment = XLineAlignment.Center;
      foreach (var dataLabel in sri.DataLabelRendererInfo.Entries)
      {
        if (dataLabel.Text != null)
          gfx.DrawString(dataLabel.Text, font, fontColor, dataLabel.Rect, format);
      }
    }
  }

  /// <summary>
  /// Calculates the data label positions specific for column charts.
  /// </summary>
  internal override void CalcPositions()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var columnIndex = 0;
      foreach (var bar in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
      {
        var dleri = sri.DataLabelRendererInfo.Entries[columnIndex++];

        dleri.Y = bar.Rect.Y + (bar.Rect.Height - dleri.Height) / 2; // Always the same...
        switch (sri.DataLabelRendererInfo.Position)
        {
          case DataLabelPosition.InsideEnd:
            // Inner border of the column.
            dleri.X = bar.Rect.X;
            if (bar.Value > 0)
              dleri.X += bar.Rect.Width - dleri.Width;
            break;

          case DataLabelPosition.Center:
            // Centered inside the column.
            dleri.X = bar.Rect.X + (bar.Rect.Width - dleri.Width) / 2;
            break;

          case DataLabelPosition.InsideBase:
            // Aligned at the base of the column.
            dleri.X = bar.Rect.X;
            if (bar.Value < 0)
              dleri.X += bar.Rect.Width - dleri.Width;
            break;

          case DataLabelPosition.OutsideEnd:
            // Outer border of the column.
            dleri.X = bar.Rect.X;
            if (bar.Value > 0)
              dleri.X += bar.Rect.Width;
            else
              dleri.X -= dleri.Width;
            break;
        }
      }
    }
  }
}
