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
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a line chart renderer.
/// </summary>
internal class LineChartRenderer : ColumnLikeChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the LineChartRenderer class with the specified renderer parameters.
  /// </summary>
  internal LineChartRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <inheritdoc/>
  protected override AxisRenderer CreateYAxisRenderer() => new VerticalYAxisRenderer(rendererParms);

  /// <inheritdoc/>
  protected override PlotAreaRenderer CreatePlotAreaRenderer() => new LinePlotAreaRenderer(rendererParms);

  /// <summary>
  /// Initializes all necessary data to draw a series for a line chart.
  /// </summary>
  internal override void InitSeries()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var seriesIndex = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      var lineColor = sri.Series.markerBackgroundColor.IsEmpty
        ? LineColors.Item(seriesIndex)
        : sri.Series.markerBackgroundColor;
      sri.LineFormat = Converter.ToXPen(sri.Series.lineFormat, lineColor, DefaultSeriesLineWidth);
      sri.LineFormat.LineJoin = XLineJoin.Bevel;

      sri.MarkerRendererInfo = InitMarker(sri, seriesIndex);
      ++seriesIndex;
    }
  }

  /// <summary>
  /// Initializes the markers of one series, filling in whatever the series leaves unset.
  /// </summary>
  private static MarkerRendererInfo InitMarker(SeriesRendererInfo sri, int seriesIndex)
  {
    var mri = new MarkerRendererInfo();

    mri.MarkerForegroundColor = sri.Series.markerForegroundColor;
    if (mri.MarkerForegroundColor.IsEmpty)
      mri.MarkerForegroundColor = XColors.Black;

    mri.MarkerBackgroundColor = sri.Series.markerBackgroundColor;
    if (mri.MarkerBackgroundColor.IsEmpty)
      mri.MarkerBackgroundColor = sri.LineFormat.Color;

    mri.MarkerSize = sri.Series.markerSize;
    if (mri.MarkerSize == 0)
      mri.MarkerSize = 7;

    if (!sri.Series.MarkerStyleInitialized)
      mri.MarkerStyle = (MarkerStyle)(seriesIndex % (Enum.GetNames<MarkerStyle>().Length - 1) + 1);
    else
      mri.MarkerStyle = sri.Series.markerStyle;
    return mri;
  }
}
