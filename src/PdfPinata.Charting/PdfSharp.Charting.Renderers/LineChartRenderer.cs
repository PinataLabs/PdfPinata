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

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = new ChartRendererInfo { Chart = (Chart)rendererParms.DrawingItem };
    rendererParms.RendererInfo = cri;

    InitSeriesRendererInfo();

    var lr = new ColumnLikeLegendRenderer(rendererParms);
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var xar = new HorizontalXAxisRenderer(rendererParms);
    cri.XAxisRendererInfo = (AxisRendererInfo)xar.Init();

    var yar = new VerticalYAxisRenderer(rendererParms);
    cri.YAxisRendererInfo = (AxisRendererInfo)yar.Init();

    var lpar = new LinePlotAreaRenderer(rendererParms);
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)lpar.Init();

    return cri;
  }

  /// <summary>
  /// Layouts and calculates the space used by the line chart.
  /// </summary>
  internal override void Format()
  {
    var lr = new ColumnLikeLegendRenderer(rendererParms);
    lr.Format();

    // axes
    var xar = new HorizontalXAxisRenderer(rendererParms);
    xar.Format();

    var yar = new VerticalYAxisRenderer(rendererParms);
    yar.Format();

    // Calculate rects and positions.
    CalcLayout();

    // Calculated remaining plot area, now it's safe to format.
    var lpar = new LinePlotAreaRenderer(rendererParms);
    lpar.Format();
  }

  /// <summary>
  /// Draws the line chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var lr = new ColumnLikeLegendRenderer(rendererParms);
    lr.Draw();

    // Draw wall.
    var wr = new WallRenderer(rendererParms);
    wr.Draw();

    // Draw gridlines.
    var glr = new ColumnLikeGridlinesRenderer(rendererParms);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(rendererParms);
    pabr.Draw();

    // Draw line chart's plot area.
    var lpar = new LinePlotAreaRenderer(rendererParms);
    lpar.Draw();

    // Draw x- and y-axis.
    if (cri.XAxisRendererInfo.Axis != null)
    {
      var xar = new HorizontalXAxisRenderer(rendererParms);
      xar.Draw();
    }

    if (cri.YAxisRendererInfo.Axis != null)
    {
      var yar = new VerticalYAxisRenderer(rendererParms);
      yar.Draw();
    }
  }

  /// <summary>
  /// Initializes all necessary data to draw a series for a line chart.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var seriesColl = cri.Chart.SeriesCollection;
    cri.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    for (var idx = 0; idx < seriesColl.Count; ++idx)
    {
      var sri = new SeriesRendererInfo { Series = seriesColl[idx] };
      cri.SeriesRendererInfos[idx] = sri;
    }

    InitSeries();
  }

  /// <summary>
  /// Initializes all necessary data to draw a series for a line chart.
  /// </summary>
  internal void InitSeries()
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
