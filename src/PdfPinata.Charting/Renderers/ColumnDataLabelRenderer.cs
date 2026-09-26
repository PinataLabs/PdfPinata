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
/// Represents a data label renderer for column charts, and for bar charts, which are column charts
/// turned on their side. Which way the category axis runs is the orientation the renderer is given.
/// </summary>
internal class ColumnDataLabelRenderer : DataLabelRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnDataLabelRenderer class with the
  /// specified renderer parameters and the orientation of the chart's category axis.
  /// </summary>
  internal ColumnDataLabelRenderer(RendererParameters parms, AxisOrientation categoryAxis) : base(parms)
  {
    this.categoryAxis = categoryAxis;
  }

  /// <summary>
  /// Which way the chart's category axis runs: across for a column chart, up for a bar chart.
  /// </summary>
  private readonly AxisOrientation categoryAxis;
    
  /// <summary>
  /// Calculates the space used by the data labels.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var dlri = sri.DataLabelRendererInfo;
      dlri.Entries = new DataLabelEntryRendererInfo[sri.PointRendererInfos.Length];
      var index = 0;
      foreach (var column in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
        dlri.Entries[index++] = FormatLabel(dlri, column);
    }

    CalcPositions();
  }

  /// <summary>
  /// Writes and measures the data label of one column.
  /// </summary>
  private DataLabelEntryRendererInfo FormatLabel(DataLabelRendererInfo dlri, ColumnRendererInfo column)
  {
    var dleri = new DataLabelEntryRendererInfo();
    if (dlri.Type == DataLabelType.Percent)
      throw new InvalidOperationException(PSCSR.PercentNotSupportedByColumnDataLabel);

    // A column the plot area does not draw - a blank, or one off the scale - is left with no text
    // at all and Draw passes over it. A blank would write the word NaN, and one off the scale a
    // number with nothing under it, outside the plot area.
    if (dlri.Type != DataLabelType.Value || !column.Drawn)
      return dleri;

    dleri.Text = column.Value.ToString(dlri.Format);
    if (dleri.Text.Length > 0)
      dleri.Size = rendererParms.Graphics.MeasureString(dleri.Text, dlri.Font);
    return dleri;
  }

  /// <summary>
  /// Draws the data labels of the column or bar chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var gfx = rendererParms.Graphics;
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
  /// Calculates the data label positions specific for column and bar charts.
  /// </summary>
  internal override void CalcPositions()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (sri.DataLabelRendererInfo == null)
        continue;

      var columnIndex = 0;
      foreach (var column in sri.PointRendererInfos.Cast<ColumnRendererInfo>())
        PositionLabel(sri.DataLabelRendererInfo.Entries[columnIndex++], sri.DataLabelRendererInfo.Position, column);
    }
  }

  /// <summary>
  /// Places one column's data label where the position asks for it: centred on the column across
  /// the value axis, and along it wherever the position says.
  /// </summary>
  private void PositionLabel(DataLabelEntryRendererInfo dleri, DataLabelPosition position, ColumnRendererInfo column)
  {
    var rect = column.Rect;
    if (categoryAxis == AxisOrientation.Horizontal)
    {
      // The value axis runs up the page, so a positive value reaches the column's top edge, which
      // is the nearer one to the page's origin.
      dleri.X = rect.X + rect.Width / 2 - dleri.Width / 2; // Always the same...
      dleri.Y = AlongValueAxis(position, rect.Y, rect.Height, dleri.Height, reachesFarEdge: column.Value < 0) ?? dleri.Y;
    }
    else
    {
      // The value axis runs across the page, so a positive value reaches the bar's right edge.
      dleri.Y = rect.Y + rect.Height / 2 - dleri.Height / 2; // Always the same...
      dleri.X = AlongValueAxis(position, rect.X, rect.Width, dleri.Width, reachesFarEdge: column.Value >= 0) ?? dleri.X;
    }
  }

  /// <summary>
  /// Where along the value axis a label starts, the column running from <paramref name="start"/>
  /// for <paramref name="length"/> and the label being <paramref name="size"/> long that way. The
  /// end a column's value reaches is its far edge, the one further from the page's origin, when
  /// <paramref name="reachesFarEdge"/> is set, and its base is the other. A zero counts with the
  /// positive values, on either chart. Null for a position this does not know.
  /// </summary>
  private static double? AlongValueAxis(DataLabelPosition position, double start, double length, double size, bool reachesFarEdge)
  {
    return position switch
    {
      // Inner border of the column.
      DataLabelPosition.InsideEnd => reachesFarEdge ? start + length - size : start,

      // Centered inside the column.
      DataLabelPosition.Center => start + length / 2 - size / 2,

      // Aligned at the base of the column.
      DataLabelPosition.InsideBase => reachesFarEdge ? start : start + length - size,

      // Outer border of the column.
      DataLabelPosition.OutsideEnd => reachesFarEdge ? start + length : start - size,

      _ => null
    };
  }
}
