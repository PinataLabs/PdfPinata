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
/// Represents a column chart renderer.
/// </summary>
internal class ColumnChartRenderer : ColumnLikeChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnChartRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = new ChartRendererInfo { Chart = (Chart)this.rendererParms.DrawingItem };
    this.rendererParms.RendererInfo = cri;

    InitSeriesRendererInfo();

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    cri.XAxisRendererInfo = (AxisRendererInfo)xar.Init();

    var yar = GetYAxisRenderer();
    cri.YAxisRendererInfo = (AxisRendererInfo)yar.Init();

    var renderer = GetPlotAreaRenderer();
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)renderer.Init();

    var dlr = new ColumnDataLabelRenderer(this.rendererParms);
    dlr.Init();

    return cri;
  }
    
  /// <summary>
  /// Layouts and calculates the space used by the column chart.
  /// </summary>
  internal override void Format()
  {
    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Format();

    // axes
    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    xar.Format();

    var yar = GetYAxisRenderer();
    yar.Format();

    // Calculate rects and positions.
    CalcLayout();

    // Calculated remaining plot area, now it's safe to format.
    var renderer = GetPlotAreaRenderer();
    renderer.Format();

    var dlr = new ColumnDataLabelRenderer(this.rendererParms);
    dlr.Format();
  }

  /// <summary>
  /// Draws the column chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;
      
    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Draw();

    var wr = new WallRenderer(this.rendererParms);
    wr.Draw();

    var glr = new ColumnLikeGridlinesRenderer(this.rendererParms);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(this.rendererParms);
    pabr.Draw();

    var renderer = GetPlotAreaRenderer();
    renderer.Draw();

    var dlr = new ColumnDataLabelRenderer(this.rendererParms);
    dlr.Draw();

    if (cri.XAxisRendererInfo.Axis != null)
    {
      var xar = new HorizontalXAxisRenderer(this.rendererParms);
      xar.Draw();
    }

    if (cri.YAxisRendererInfo.Axis != null)
    {
      var yar = GetYAxisRenderer();
      yar.Draw();
    }
  }

  /// <summary>
  /// Returns the specific plot area renderer.
  /// </summary>
  private PlotAreaRenderer GetPlotAreaRenderer()
  {
    var chart = (Chart)this.rendererParms.DrawingItem;
    switch (chart.type)
    {
      case ChartType.Column2D:
        return new ColumnClusteredPlotAreaRenderer(this.rendererParms);

      case ChartType.ColumnStacked2D:
        return new ColumnStackedPlotAreaRenderer(this.rendererParms);
    }
    return null;
  }

  /// <summary>
  /// Returns the specific y axis renderer.
  /// </summary>
  private YAxisRenderer GetYAxisRenderer()
  {
    var chart = (Chart)this.rendererParms.DrawingItem;
    switch (chart.type)
    {
      case ChartType.Column2D:
        return new VerticalYAxisRenderer(this.rendererParms);

      case ChartType.ColumnStacked2D:
        return new VerticalStackedYAxisRenderer(this.rendererParms);
    }
    return null;
  }

  /// <summary>
  /// Initializes all necessary data to draw all series for a column chart.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

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
  /// Initializes all necessary data to draw all series for a column chart.
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
        PointRendererInfo pri = new ColumnRendererInfo();
        var point = sri.Series.Elements[pointIdx];
        pri.Point = point;
        if (point != null)
        {
          pri.LineFormat = sri.LineFormat;
          pri.FillFormat = sri.FillFormat;
          if (point.lineFormat != null)
            pri.LineFormat = Converter.ToXPen(point.lineFormat, sri.LineFormat);
          if (point.fillFormat != null && !point.fillFormat.color.IsEmpty)
            pri.FillFormat = new XSolidBrush(point.fillFormat.color);
        }
        sri.PointRendererInfos[pointIdx] = pri;
      }
    }
  }
}
