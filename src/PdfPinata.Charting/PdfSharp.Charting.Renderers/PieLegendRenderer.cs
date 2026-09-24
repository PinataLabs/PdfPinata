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

using System.Globalization;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the legend renderer specific to pie charts.
/// </summary>
internal class PieLegendRenderer : LegendRenderer
{
  /// <summary>
  /// Initializes a new instance of the PieLegendRenderer class with the specified renderer
  /// parameters.
  /// </summary>
  internal PieLegendRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Initializes the legend's renderer info. Each data point will be represented through
  /// a legend entry renderer info.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.Chart.legend == null)
      return null;

    var lri = NewLegendRendererInfo(cri);

    XSeries xseries = null;
    if (cri.Chart.xValues != null)
      xseries = cri.Chart.xValues[0];

    var index = 0;
    var sri = cri.SeriesRendererInfos[0];
    lri.Entries = new LegendEntryRendererInfo[sri.PointRendererInfos.Length];
    foreach (var pri in sri.PointRendererInfos)
    {
      var leri = new LegendEntryRendererInfo
      {
        SeriesRendererInfo = sri,
        LegendRendererInfo = lri,
        EntryText = EntryText(xseries, index)
      };
      leri.MarkerPen = pri.LineFormat;
      leri.MarkerBrush = pri.FillFormat;

      lri.Entries[index++] = leri;
    }
    return lri;
  }

  /// <summary>
  /// The text of the entry for the point at the index given: its category, or its number when
  /// the chart has no categories.
  /// </summary>
  private static string EntryText(XSeries xseries, int index)
  {
    if (xseries == null)
      return (index + 1).ToString(CultureInfo.InvariantCulture); // create default/dummy entry

    return xseries.Count > index ? xseries[index].Value : string.Empty;
  }
}
