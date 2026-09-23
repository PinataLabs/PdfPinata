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

using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the legend renderer specific to charts like column, line, or bar.
/// </summary>
internal class ColumnLikeLegendRenderer : LegendRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnLikeLegendRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnLikeLegendRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Initializes the legend's renderer info. Each data series will be represented through
  /// a legend entry renderer info.
  /// </summary>
  internal override RendererInfo Init()
  {
    LegendRendererInfo lri = null;
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.Chart.legend == null)
      return null;

    lri = new LegendRendererInfo { Legend = cri.Chart.legend };

    lri.Font = Converter.ToXFont(lri.Legend.font, cri.DefaultFont);
    lri.FontColor = Converter.ToXBrush(lri.Legend.font, cri.DefaultFontColor);

    if (lri.Legend.lineFormat != null)
      lri.BorderPen = Converter.ToXPen(lri.Legend.lineFormat, XColors.Black, DefaultLineWidth, XDashStyle.Solid);

    lri.Entries = new LegendEntryRendererInfo[cri.SeriesRendererInfos.Length];
    var index = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      var leri = new LegendEntryRendererInfo
      {
        SeriesRendererInfo = sri,
        LegendRendererInfo = lri,
        EntryText = sri.Series.name
      };
      if (sri.MarkerRendererInfo != null)
      {
        leri.MarkerSize.Width = leri.MarkerSize.Height = sri.MarkerRendererInfo.MarkerSize.Point;
        leri.MarkerPen = new XPen(sri.MarkerRendererInfo.MarkerForegroundColor);
        leri.MarkerBrush = new XSolidBrush(sri.MarkerRendererInfo.MarkerBackgroundColor);
      }
      else
      {
        leri.MarkerPen = sri.LineFormat;
        leri.MarkerBrush = sri.FillFormat;
      }

      if (cri.Chart.type == ChartType.ColumnStacked2D)
        // stacked columns are revers ordered
        lri.Entries[cri.SeriesRendererInfos.Length - index++ - 1] = leri;
      else
        lri.Entries[index++] = leri;
    }

    if (cri is CombinationRendererInfo { ColumnsStacked: true })
      ReverseStackedEntries(lri.Entries);
    return lri;
  }

  /// <summary>
  /// Reverses the entries of the stacked column series among themselves, leaving every other
  /// series where it was, so that in a combination chart the stacked columns are listed top of the
  /// stack first, as a chart of stacked columns alone lists them.
  /// </summary>
  private static void ReverseStackedEntries(LegendEntryRendererInfo[] entries)
  {
    var stacked = new List<int>();
    for (var idx = 0; idx < entries.Length; ++idx)
    {
      if (entries[idx].SeriesRendererInfo.Series.chartType == ChartType.ColumnStacked2D)
        stacked.Add(idx);
    }

    for (int low = 0, high = stacked.Count - 1; low < high; ++low, --high)
      (entries[stacked[low]], entries[stacked[high]]) = (entries[stacked[high]], entries[stacked[low]]);
  }
}
