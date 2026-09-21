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
using System.Collections;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a renderer for combinations of charts.
/// </summary>
internal class CombinationChartRenderer : ChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the CombinationChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal CombinationChartRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = new CombinationRendererInfo();
    cri.Chart = (Chart)this.rendererParms.DrawingItem;
    this.rendererParms.RendererInfo = cri;

    InitSeriesRendererInfo();
    DistributeSeries();

    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      var renderer = new AreaChartRenderer(this.rendererParms);
      renderer.InitSeries();
    }
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var renderer = new ColumnChartRenderer(this.rendererParms);
      renderer.InitSeries();
    }
    if (cri.LineSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.LineSeriesRendererInfos;
      var renderer = new LineChartRenderer(this.rendererParms);
      renderer.InitSeries();
    }
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    cri.XAxisRendererInfo = (AxisRendererInfo)xar.Init();

    var yar = GetYAxisRenderer();
    cri.YAxisRendererInfo = (AxisRendererInfo)yar.Init();

    var apar = new AreaPlotAreaRenderer(this.rendererParms);
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)apar.Init();

    // Draw data labels.
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var dlr = new ColumnDataLabelRenderer(this.rendererParms);
      dlr.Init();
    }

    return cri;
  }
    
  /// <summary>
  /// Layouts and calculates the space used by the combination chart.
  /// </summary>
  internal override void Format()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Format();

    // axes
    var xar = new HorizontalXAxisRenderer(this.rendererParms);
    xar.Format();

    var yar = GetYAxisRenderer();
    yar.Format();

    // Calculate rects and positions.
    var chartRect = LayoutLegend();
    cri.XAxisRendererInfo.X = chartRect.Left + cri.YAxisRendererInfo.Width;
    cri.XAxisRendererInfo.Y = chartRect.Bottom - cri.XAxisRendererInfo.Height;
    cri.XAxisRendererInfo.Width = chartRect.Width - cri.YAxisRendererInfo.Width;
    cri.YAxisRendererInfo.X = chartRect.Left;
    cri.YAxisRendererInfo.Y = chartRect.Top;
    cri.YAxisRendererInfo.Height = chartRect.Height - cri.XAxisRendererInfo.Height;
    cri.PlotAreaRendererInfo.X = cri.XAxisRendererInfo.X;
    cri.PlotAreaRendererInfo.Y = cri.YAxisRendererInfo.InnerRect.Y;
    cri.PlotAreaRendererInfo.Width = cri.XAxisRendererInfo.Width;
    cri.PlotAreaRendererInfo.Height = cri.YAxisRendererInfo.InnerRect.Height;

    // Calculated remaining plot area, now it's safe to format.
    PlotAreaRenderer renderer;
    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      renderer = new AreaPlotAreaRenderer(this.rendererParms);
      renderer.Format();
    }
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      renderer = GetColumnPlotAreaRenderer();
      renderer.Format();
    }
    if (cri.LineSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.LineSeriesRendererInfos;
      renderer = new LinePlotAreaRenderer(this.rendererParms);
      renderer.Format();
    }

    // Draw data labels.
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var dlr = new ColumnDataLabelRenderer(this.rendererParms);
      dlr.Format();
    }
  }

  /// <summary>
  /// Draws the column chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;

    var lr = new ColumnLikeLegendRenderer(this.rendererParms);
    lr.Draw();

    var wr = new WallRenderer(this.rendererParms);
    wr.Draw();

    var glr = new ColumnLikeGridlinesRenderer(this.rendererParms);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(this.rendererParms);
    pabr.Draw();

    PlotAreaRenderer renderer;
    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      renderer = new AreaPlotAreaRenderer(this.rendererParms);
      renderer.Draw();
    }
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      renderer = GetColumnPlotAreaRenderer();
      renderer.Draw();
    }
    if (cri.LineSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.LineSeriesRendererInfos;
      renderer = new LinePlotAreaRenderer(this.rendererParms);
      renderer.Draw();
    }

    // Draw data labels.
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var dlr = new ColumnDataLabelRenderer(this.rendererParms);
      dlr.Draw();
    }

    // Draw axes.
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;
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
  /// Returns the plot area renderer for the column series: stacked or clustered, as they are.
  /// </summary>
  private PlotAreaRenderer GetColumnPlotAreaRenderer()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;
    return cri.ColumnsStacked
      ? new ColumnStackedPlotAreaRenderer(this.rendererParms)
      : new ColumnClusteredPlotAreaRenderer(this.rendererParms);
  }

  /// <summary>
  /// Returns the y axis renderer. Stacked columns are scaled to their totals, which the stacked
  /// renderer works out from the column series alone before taking in every other value.
  /// </summary>
  private YAxisRenderer GetYAxisRenderer()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;
    return cri.ColumnsStacked
      ? new VerticalStackedYAxisRenderer(this.rendererParms)
      : new VerticalYAxisRenderer(this.rendererParms);
  }

  /// <summary>
  /// Initializes all necessary data to draw series for a combination chart.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;
    var seriesColl = cri.Chart.SeriesCollection;
    cri.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    for (var idx = 0; idx < seriesColl.Count; ++idx)
    {
      var sri = new SeriesRendererInfo();
      sri.Series = seriesColl[idx];
      cri.SeriesRendererInfos[idx] = sri;
    }
  }

  /// <summary>
  /// Sort all series renderer info dependent on their chart type.
  /// </summary>
  private void DistributeSeries()
  {
    var cri = (CombinationRendererInfo)this.rendererParms.RendererInfo;

    var areaSeries = new ArrayList();
    var columnSeries = new ArrayList();
    var lineSeries = new ArrayList();
    var clustered = false;
    var stacked = false;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      switch (sri.Series.chartType)
      {
        case ChartType.Area2D:
          areaSeries.Add(sri);
          break;

        case ChartType.Column2D:
          clustered = true;
          columnSeries.Add(sri);
          break;

        case ChartType.ColumnStacked2D:
          stacked = true;
          columnSeries.Add(sri);
          break;

        case ChartType.Line:
          lineSeries.Add(sri);
          break;

        default:
          throw new InvalidOperationException(PSCSR.InvalidChartTypeForCombination(sri.Series.chartType));
      }
    }

    // One set of columns shares one slot per category, and it is either divided between the
    // series or stacked up in it - there is no drawing both in the same place.
    if (clustered && stacked)
      throw new InvalidOperationException(PSCSR.ClusteredAndStackedColumnsInCombination);
    cri.ColumnsStacked = stacked;

    cri.CommonSeriesRendererInfos = cri.SeriesRendererInfos;
    if (areaSeries.Count > 0)
    {
      cri.AreaSeriesRendererInfos = new SeriesRendererInfo[areaSeries.Count];
      areaSeries.CopyTo(cri.AreaSeriesRendererInfos);
    }
    if (columnSeries.Count > 0)
    {
      cri.ColumnSeriesRendererInfos = new SeriesRendererInfo[columnSeries.Count];
      columnSeries.CopyTo(cri.ColumnSeriesRendererInfos);
    }
    if (lineSeries.Count > 0)
    {
      cri.LineSeriesRendererInfos = new SeriesRendererInfo[lineSeries.Count];
      lineSeries.CopyTo(cri.LineSeriesRendererInfos);
    }
  }
}
