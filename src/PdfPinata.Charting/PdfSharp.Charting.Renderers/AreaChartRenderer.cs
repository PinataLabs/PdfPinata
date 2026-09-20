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

using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents an area chart renderer.
/// </summary>
internal class AreaChartRenderer : ColumnLikeChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the AreaChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal AreaChartRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = new ChartRendererInfo();
    cri.Chart = (Chart)this.rendererParms.DrawingItem;
    this.rendererParms.RendererInfo = cri;

    InitSeriesRendererInfo();

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    cri.XAxisRendererInfo = (AxisRendererInfo)xar.Init();

    var yar = new VerticalYAxisRenderer(this.rendererParms);
    cri.YAxisRendererInfo = (AxisRendererInfo)yar.Init();

    _ = cri.Chart.PlotArea; // creates the plot area on the chart, which the renderers below read
    var renderer = new AreaPlotAreaRenderer(this.rendererParms);
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)renderer.Init();

    return cri;
  }

  /// <summary>
  /// Layouts and calculates the space used by the line chart.
  /// </summary>
  internal override void Format()
  {
    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Format();

    // axes
    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    xar.Format();

    var yar = new VerticalYAxisRenderer(this.rendererParms);
    yar.Format();

    // Calculate rects and positions.
    CalcLayout();

    // Calculated remaining plot area, now it's safe to format.
    var renderer = new AreaPlotAreaRenderer(this.rendererParms);
    renderer.Format();
  }

  /// <summary>
  /// Draws the column chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Draw();

    // Draw wall.
    var wr = new WallRenderer(this.rendererParms);
    wr.Draw();

    // Draw gridlines.
    var glr = new ColumnLikeGridlinesRenderer(this.rendererParms);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(this.rendererParms);
    pabr.Draw();

    var renderer = new AreaPlotAreaRenderer(this.rendererParms);
    renderer.Draw();

    // Draw axes.
    if (cri.XAxisRendererInfo.Axis != null)
    {
      var xar = new HorizontalXAxisRenderer(this.rendererParms);
      xar.Draw();
    }

    if (cri.YAxisRendererInfo.Axis != null)
    {
      var yar = new VerticalYAxisRenderer(this.rendererParms);
      yar.Draw();
    }
  }

  /// <summary>
  /// Initializes all necessary data to draw a series for a area chart.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var seriesColl = cri.Chart.SeriesCollection;
    cri.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    for (var idx = 0; idx < seriesColl.Count; ++idx)
    {
      var sri = new SeriesRendererInfo();
      sri.Series = seriesColl[idx];
      cri.SeriesRendererInfos[idx] = sri;
    }

    InitSeries();
  }

  /// <summary>
  /// Initializes all necessary data to draw a series for a area chart.
  /// </summary>
  internal void InitSeries()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var seriesIndex = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      sri.LineFormat = Converter.ToXPen(sri.Series.lineFormat, XColors.Black, ChartRenderer.DefaultSeriesLineWidth);
      sri.FillFormat = Converter.ToXBrush(sri.Series.fillFormat, ColumnColors.Item(seriesIndex++));

      sri.PointRendererInfos = new PointRendererInfo[sri.Series.Elements.Count];
      for (var pointIdx = 0; pointIdx < sri.PointRendererInfos.Length; ++pointIdx)
      {
        var pri = new PointRendererInfo();
        var point = sri.Series.Elements[pointIdx];
        pri.Point = point;
        if (point != null)
        {
          pri.LineFormat = sri.LineFormat;
          pri.FillFormat = sri.FillFormat;
          if (point.lineFormat != null && !point.lineFormat.color.IsEmpty)
            pri.LineFormat = new XPen(point.lineFormat.color, point.lineFormat.width);
          if (point.fillFormat != null && point.lineFormat != null && !point.lineFormat.color.IsEmpty)
            pri.FillFormat = new XSolidBrush(point.fillFormat.color);
        }
        sri.PointRendererInfos[pointIdx] = pri;
      }
    }
  }
}
