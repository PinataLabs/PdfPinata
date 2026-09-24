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
/// Represents a data label renderer for pie charts.
/// </summary>
internal class PieDataLabelRenderer : DataLabelRenderer
{
  /// <summary>
  /// Initializes a new instance of the PieDataLabelRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal PieDataLabelRenderer(RendererParameters parms) : base(parms)
  {
  }
    
  /// <summary>
  /// Calculates the space used by the data labels.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var sri = cri.SeriesRendererInfos[0];
    if (sri.DataLabelRendererInfo == null)
      return;

    var sumValues = sri.SumOfPoints;
    var dlri = sri.DataLabelRendererInfo;
    dlri.Entries = new DataLabelEntryRendererInfo[sri.PointRendererInfos.Length];
    var index = 0;
    foreach (var sector in sri.PointRendererInfos.Cast<SectorRendererInfo>())
      dlri.Entries[index++] = FormatLabel(dlri, sector, sumValues);
  }

  /// <summary>
  /// Writes and measures the data label of one sector.
  /// </summary>
  private DataLabelEntryRendererInfo FormatLabel(DataLabelRendererInfo dlri, SectorRendererInfo sector, double sumValues)
  {
    var dleri = new DataLabelEntryRendererInfo();

    // A blank draws no wedge, so it is left with no text either and Draw passes over it.
    // Writing what NaN formats to would label a wedge that is not there.
    if (dlri.Type == DataLabelType.None || double.IsNaN(sector.Value))
      return dleri;

    if (dlri.Type == DataLabelType.Percent)
      dleri.Text = PercentText(Math.Abs(sector.Value) / sumValues, dlri.Format);
    else if (dlri.Type == DataLabelType.Value)
      dleri.Text = sector.Value.ToString(dlri.Format);

    if (dleri.Text.Length > 0)
      dleri.Size = rendererParms.Graphics.MeasureString(dleri.Text, dlri.Font);
    return dleri;
  }

  /// <summary>
  /// Writes a sector's share of the whole as a percentage.
  /// </summary>
  private static string PercentText(double share, string format)
  {
    // Two ways of asking for a percentage, and the caller's format says which. A format
    // carrying '%' is a .NET percent format, which scales by a hundred and writes the sign
    // itself, so it is handed the fraction and its result is used as it stands. Anything else
    // is a plain numeric format, is handed the number out of a hundred, and has the sign
    // appended - which is what this always did.
    //
    // Appending unconditionally made the natural format the broken one: "0%" over a share of
    // 0.1875 produced "1875%%" rather than "19%", because 18.75 was scaled by a hundred a
    // second time and signed twice. It read back exactly as it was set and printed nonsense.
    return format != null && format.Contains('%')
      ? share.ToString(format)
      : (share * 100).ToString(format) + "%";
  }

  /// <summary>
  /// Draws the data labels of the pie chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var sri = cri.SeriesRendererInfos[0];
    if (sri.DataLabelRendererInfo == null)
      return;

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    if (sri == null)
      return;

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

  /// <summary>
  /// Calculates the data label positions specific for pie charts.
  /// </summary>
  internal override void CalcPositions()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var sri = cri.SeriesRendererInfos[0];
    if (sri is not { DataLabelRendererInfo: not null })
      return;

    var sectorIndex = 0;
    foreach (var sector in sri.PointRendererInfos.Cast<SectorRendererInfo>())
    {
      var dleri = sri.DataLabelRendererInfo.Entries[sectorIndex++];
      PositionLabel(dleri, sri.DataLabelRendererInfo.Position, sector);
    }
  }

  /// <summary>
  /// Places one sector's data label where the position asks for it.
  /// </summary>
  private static void PositionLabel(DataLabelEntryRendererInfo dleri, DataLabelPosition position, SectorRendererInfo sector)
  {
    // Determine output rectangle
    var midAngle = sector.StartAngle + sector.SweepAngle / 2;
    var radMidAngle = midAngle / 180 * Math.PI;
    var origin = new XPoint(sector.Rect.X + sector.Rect.Width / 2,
      sector.Rect.Y + sector.Rect.Height / 2);
    var radius = sector.Rect.Width / 2;
    var halfradius = radius / 2;

    // The two "end" positions put a corner of the label exactly on the arc, which draws the
    // text hard against the edge of the wedge - and, on the outside, hard against whatever is
    // beyond it. Both are moved off the arc along their own radius by a third of the label's
    // own height, so the gap is in proportion to the text rather than to the chart, and a
    // large pie and a small one look alike.
    var inset = dleri.Height / 3;

    switch (position)
    {
      case DataLabelPosition.OutsideEnd:
        // Just beyond the outer border of the circle.
        PlaceOnRadius(dleri, origin, radius + inset, radMidAngle);
        if (dleri.X < origin.X)
          dleri.X -= dleri.Width;
        if (dleri.Y < origin.Y)
          dleri.Y -= dleri.Height;
        break;

      case DataLabelPosition.InsideEnd:
        // Just within the outer border of the circle. Never past the middle, however tall
        // the label: a pie small enough for that is one whose labels have nowhere to go.
        PlaceOnRadius(dleri, origin, Math.Max(radius - inset, halfradius), radMidAngle);
        if (dleri.X > origin.X)
          dleri.X -= dleri.Width;
        if (dleri.Y > origin.Y)
          dleri.Y -= dleri.Height;
        break;

      case DataLabelPosition.Center:
        // Centered
        PlaceOnRadius(dleri, origin, halfradius, radMidAngle);
        dleri.X -= dleri.Width / 2;
        dleri.Y -= dleri.Height / 2;
        break;

      case DataLabelPosition.InsideBase:
        PlaceAtBase(dleri, origin, radMidAngle);
        break;
    }
  }

  /// <summary>
  /// Puts the label's corner at the given distance from the centre along the sector's middle.
  /// </summary>
  private static void PlaceOnRadius(DataLabelEntryRendererInfo dleri, XPoint origin, double distance, double radMidAngle)
  {
    dleri.X = origin.X + distance * Math.Cos(radMidAngle);
    dleri.Y = origin.Y + distance * Math.Sin(radMidAngle);
  }

  private static void PlaceAtBase(DataLabelEntryRendererInfo dleri, XPoint origin, double radMidAngle)
  {
    // Aligned at the base of the sector, which for a pie is the centre of the circle.
    // The label is laid out away from that point along its own sector, so that the
    // corner of it nearest the centre is the one that sits there.
    //
    // The two tests are on the direction the sector runs in. They used to be on the
    // label's own position, which had just been set to the centre and so could not be
    // to the left of it or above it - meaning neither adjustment ever ran, and every
    // label of every sector was drawn at one point on top of the others.
    dleri.X = origin.X;
    dleri.Y = origin.Y;
    if (Math.Cos(radMidAngle) < 0)
      dleri.X -= dleri.Width;
    if (Math.Sin(radMidAngle) < 0)
      dleri.Y -= dleri.Height;
  }
}
