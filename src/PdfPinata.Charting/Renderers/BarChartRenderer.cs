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
/// Represents a bar chart renderer.
/// </summary>
internal class BarChartRenderer : ChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the BarChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal BarChartRenderer(RendererParameters parms) : base(parms)
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

    var lr = GetLegendRenderer();
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var xar = new VerticalXAxisRenderer(rendererParms);
    cri.XAxisRendererInfo = (AxisRendererInfo)xar.Init();

    var yar = GetYAxisRenderer();
    cri.YAxisRendererInfo = (AxisRendererInfo)yar.Init();

    var renderer = GetPlotAreaRenderer();
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)renderer.Init();

    var dlr = new BarDataLabelRenderer(rendererParms);
    dlr.Init();

    return cri;
  }
    
  /// <summary>
  /// Layouts and calculates the space used by the column chart.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var lr = GetLegendRenderer();
    lr.Format();

    // axes
    var xar = new VerticalXAxisRenderer(rendererParms);
    xar.Format();

    var yar = GetYAxisRenderer();
    yar.Format();

    // Calculate rects and positions.
    var chartRect = LayoutLegend();
    cri.XAxisRendererInfo.X = chartRect.Left;
    cri.XAxisRendererInfo.Y = chartRect.Top;
    cri.XAxisRendererInfo.Height = chartRect.Height - cri.YAxisRendererInfo.Height;
    cri.YAxisRendererInfo.X = chartRect.Left + cri.XAxisRendererInfo.Width;
    cri.YAxisRendererInfo.Y = chartRect.Bottom - cri.YAxisRendererInfo.Height;
    cri.YAxisRendererInfo.Width = chartRect.Width - cri.XAxisRendererInfo.Width;
    cri.PlotAreaRendererInfo.X = cri.YAxisRendererInfo.X;
    cri.PlotAreaRendererInfo.Y = cri.XAxisRendererInfo.Y;
    cri.PlotAreaRendererInfo.Width = cri.YAxisRendererInfo.InnerRect.Width;
    cri.PlotAreaRendererInfo.Height = cri.XAxisRendererInfo.Height;

    // Calculated remaining plot area, now it's safe to format.
    var renderer = GetPlotAreaRenderer();
    renderer.Format();

    var dlr = new BarDataLabelRenderer(rendererParms);
    dlr.Format();
  }

  /// <summary>
  /// Draws the column chart.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
      
    var lr = GetLegendRenderer();
    lr.Draw();

    var wr = new WallRenderer(rendererParms);
    wr.Draw();

    var glr = new ColumnLikeGridlinesRenderer(rendererParms, AxisOrientation.Vertical);
    glr.Draw();

    var pabr = new PlotAreaBorderRenderer(rendererParms);
    pabr.Draw();

    var renderer = GetPlotAreaRenderer();
    renderer.Draw();

    var dlr = new BarDataLabelRenderer(rendererParms);
    dlr.Draw();

    if (cri.XAxisRendererInfo.Axis != null)
    {
      var xar = new VerticalXAxisRenderer(rendererParms);
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
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Bar2D => new ColumnClusteredPlotAreaRenderer(rendererParms, AxisOrientation.Vertical),
      ChartType.BarStacked2D => new ColumnStackedPlotAreaRenderer(rendererParms, AxisOrientation.Vertical),
      _ => null
    };
  }

  /// <summary>
  /// Returns the specific legend renderer.
  /// </summary>
  private ColumnLikeLegendRenderer GetLegendRenderer()
  {
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Bar2D => new BarClusteredLegendRenderer(rendererParms),
      ChartType.BarStacked2D => new ColumnLikeLegendRenderer(rendererParms),
      _ => null
    };
  }

  /// <summary>
  /// Returns the specific plot area renderer.
  /// </summary>
  private HorizontalYAxisRenderer GetYAxisRenderer()
  {
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Bar2D => new HorizontalYAxisRenderer(rendererParms),
      ChartType.BarStacked2D => new HorizontalStackedYAxisRenderer(rendererParms),
      _ => null
    };
  }

  /// <summary>
  /// Initializes all necessary data to draw all series for a column chart.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var seriesColl = cri.Chart.SeriesCollection;
    cri.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    // Lowest series is the first, like in Excel 
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
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var seriesIndex = 0;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      sri.LineFormat = Converter.ToXPen(sri.Series.lineFormat, XColors.Black, DefaultSeriesLineWidth);
      sri.FillFormat = Converter.ToXBrush(sri.Series.fillFormat, ColumnColors.Item(seriesIndex++));

      sri.PointRendererInfos = new PointRendererInfo[sri.Series.Elements.Count];
      for (var pointIdx = 0; pointIdx < sri.PointRendererInfos.Length; ++pointIdx)
        sri.PointRendererInfos[pointIdx] = InitPoint(sri, sri.Series.Elements[pointIdx]);
    }
  }

  /// <summary>
  /// Initializes the data to draw one bar, which takes the series' formats unless it has its own.
  /// </summary>
  private static PointRendererInfo InitPoint(SeriesRendererInfo sri, Point point)
  {
    PointRendererInfo pri = new ColumnRendererInfo();
    pri.Point = point;
    if (point == null)
      return pri;

    pri.LineFormat = sri.LineFormat;
    pri.FillFormat = sri.FillFormat;
    // A line format the caller set on the point, resolved against the series' pen. One that was
    // only read into existence - Point.LineFormat creates it on first read - is not the point's.
    if (point.lineFormat is { isSet: true })
      pri.LineFormat = Converter.ToXPen(point.lineFormat, sri.LineFormat);
    if (point.fillFormat is { color.IsEmpty: false })
      pri.FillFormat = new XSolidBrush(point.fillFormat.color);
    return pri;
  }
}
