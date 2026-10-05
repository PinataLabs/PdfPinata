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
/// <remarks>
/// It runs the same pipeline as the charts it combines, but its series are of several types and
/// each type is initialized, formatted and drawn by that type's own code over that type's own
/// series. So it overrides the steps that touch the series - and keeps its own <see cref="Draw"/> -
/// and between them always hands the series renderer infos back as the common set, which is what
/// the legend and the axes are built from.
/// </remarks>
internal class CombinationChartRenderer : CartesianChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the CombinationChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal CombinationChartRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <inheritdoc/>
  protected override AxisOrientation CategoryAxis => AxisOrientation.Horizontal;

  /// <inheritdoc/>
  protected override ChartRendererInfo CreateRendererInfo() => new CombinationRendererInfo();

  /// <inheritdoc/>
  protected override AxisRenderer CreateXAxisRenderer() => new HorizontalXAxisRenderer(rendererParms);

  /// <summary>
  /// Returns the y axis renderer. Stacked columns are scaled to their totals, which the stacked
  /// renderer works out from the column series alone before taking in every other value.
  /// </summary>
  protected override AxisRenderer CreateYAxisRenderer()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;
    return cri.ColumnsStacked
      ? new VerticalStackedYAxisRenderer(rendererParms)
      : new VerticalYAxisRenderer(rendererParms);
  }

  /// <summary>
  /// The plot area renderer that initializes the plot area's renderer info. Format and Draw use
  /// each series type's own plot area renderer instead, over that type's series.
  /// </summary>
  protected override PlotAreaRenderer CreatePlotAreaRenderer() => new AreaPlotAreaRenderer(rendererParms);

  /// <summary>
  /// Sorts the series by type and initializes each type with its own chart renderer, leaving the
  /// common set in place for the legend and the axes.
  /// </summary>
  internal override void InitSeries()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;

    DistributeSeries();

    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      var renderer = new AreaChartRenderer(rendererParms);
      renderer.InitSeries();
    }
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var renderer = new ColumnChartRenderer(rendererParms);
      renderer.InitSeries();
    }
    if (cri.LineSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.LineSeriesRendererInfos;
      var renderer = new LineChartRenderer(rendererParms);
      renderer.InitSeries();
    }
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;
  }

  /// <summary>
  /// Initializes the data labels of the column series, which are the only ones labelled.
  /// </summary>
  protected override void InitDataLabels()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;
    if (cri.ColumnSeriesRendererInfos == null)
      return;

    cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
    var dlr = new ColumnDataLabelRenderer(rendererParms, AxisOrientation.Horizontal);
    dlr.Init();

    // Format starts with the legend and the axes, which are formatted over every series.
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;
  }

  /// <summary>
  /// Calculates the chart layout.
  /// </summary>
  protected override void LayOut()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;

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
  }

  /// <summary>
  /// Formats each series type's plot area over its own series, then the column series' labels.
  /// </summary>
  protected override void FormatPlotArea()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;

    PlotAreaRenderer renderer;
    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      renderer = new AreaPlotAreaRenderer(rendererParms);
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
      renderer = new LinePlotAreaRenderer(rendererParms);
      renderer.Format();
    }

    // Format data labels.
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var dlr = new ColumnDataLabelRenderer(rendererParms, AxisOrientation.Horizontal);
      dlr.Format();
    }
  }

  /// <summary>
  /// Draws the combination chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;

    CreateLegendRenderer().Draw();

    var wr = new WallRenderer(rendererParms);
    wr.Draw();

    var glr = new ColumnLikeGridlinesRenderer(rendererParms, AxisOrientation.Horizontal);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(rendererParms);
    pabr.Draw();

    PlotAreaRenderer renderer;
    if (cri.AreaSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.AreaSeriesRendererInfos;
      renderer = new AreaPlotAreaRenderer(rendererParms);
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
      renderer = new LinePlotAreaRenderer(rendererParms);
      renderer.Draw();
    }

    // Draw data labels.
    if (cri.ColumnSeriesRendererInfos != null)
    {
      cri.SeriesRendererInfos = cri.ColumnSeriesRendererInfos;
      var dlr = new ColumnDataLabelRenderer(rendererParms, AxisOrientation.Horizontal);
      dlr.Draw();
    }

    // Draw axes.
    cri.SeriesRendererInfos = cri.CommonSeriesRendererInfos;
    if (cri.XAxisRendererInfo.Axis != null)
      CreateXAxisRenderer().Draw();
    if (cri.YAxisRendererInfo.Axis != null)
      CreateYAxisRenderer().Draw();
  }

  /// <summary>
  /// Returns the plot area renderer for the column series: stacked or clustered, as they are.
  /// </summary>
  private PlotAreaRenderer GetColumnPlotAreaRenderer()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;
    return cri.ColumnsStacked
      ? new ColumnStackedPlotAreaRenderer(rendererParms, AxisOrientation.Horizontal)
      : new ColumnClusteredPlotAreaRenderer(rendererParms, AxisOrientation.Horizontal);
  }

  /// <summary>
  /// Sort all series renderer info dependent on their chart type.
  /// </summary>
  private void DistributeSeries()
  {
    var cri = (CombinationRendererInfo)rendererParms.RendererInfo;

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
