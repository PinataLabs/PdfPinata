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
/// Represents a pie chart renderer.
/// </summary>
internal class PieChartRenderer : ChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the PieChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal PieChartRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = new ChartRendererInfo { Chart = (Chart)rendererParms.DrawingItem };
    rendererParms.RendererInfo = cri;

    InitSeries(cri);

    var lr = new PieLegendRenderer(rendererParms);
    cri.LegendRendererInfo = (LegendRendererInfo)lr.Init();

    var renderer = GetPlotAreaRenderer();
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)renderer.Init();

    var dlr = new PieDataLabelRenderer(rendererParms);
    dlr.Init();

    return cri;
  }
    
  /// <summary>
  /// Layouts and calculates the space used by the pie chart.
  /// </summary>
  internal override void Format()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var lr = new PieLegendRenderer(rendererParms);
    lr.Format();

    // Calculate rects and positions.
    var chartRect = LayoutLegend();
    cri.PlotAreaRendererInfo.Rect = chartRect;
    var edge = Math.Min(chartRect.Width, chartRect.Height);
    cri.PlotAreaRendererInfo.X += (chartRect.Width - edge) / 2;
    cri.PlotAreaRendererInfo.Y += (chartRect.Height - edge) / 2;
    cri.PlotAreaRendererInfo.Width = edge;
    cri.PlotAreaRendererInfo.Height = edge;

    var dlr = new PieDataLabelRenderer(rendererParms);
    dlr.Format();

    // Calculated remaining plot area, now it's safe to format.
    var renderer = GetPlotAreaRenderer();
    renderer.Format();

    dlr.CalcPositions();
  }

  /// <summary>
  /// Draws the pie chart.
  /// </summary>
  internal override void Draw()
  {
    var lr = new PieLegendRenderer(rendererParms);
    lr.Draw();

    var wr = new WallRenderer(rendererParms);
    wr.Draw();

    var pabr = new PlotAreaBorderRenderer(rendererParms);
    pabr.Draw();

    var renderer = GetPlotAreaRenderer();
    renderer.Draw();

    var dlr = new PieDataLabelRenderer(rendererParms);
    dlr.Draw();
  }

  /// <summary>
  /// Returns the specific plot area renderer.
  /// </summary>
  private PlotAreaRenderer GetPlotAreaRenderer()
  {
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Pie2D => new PieClosedPlotAreaRenderer(rendererParms),
      ChartType.PieExploded2D => new PieExplodedPlotAreaRenderer(rendererParms),
      _ => null
    };
  }

  /// <summary>
  /// Initializes all necessary data to draw a series for a pie chart.
  /// </summary>
  protected static void InitSeries(ChartRendererInfo rendererInfo)
  {
    var seriesColl = rendererInfo.Chart.SeriesCollection;
    rendererInfo.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    for (var idx = 0; idx < seriesColl.Count; ++idx)
    {
      var sri = new SeriesRendererInfo();
      rendererInfo.SeriesRendererInfos[idx] = sri;
      sri.Series = seriesColl[idx];

      sri.LineFormat = Converter.ToXPen(sri.Series.lineFormat, XColors.Black, DefaultSeriesLineWidth);
      sri.FillFormat = Converter.ToXBrush(sri.Series.fillFormat, ColumnColors.Item(idx));

      sri.PointRendererInfos = new PointRendererInfo[sri.Series.Elements.Count];
      for (var pointIdx = 0; pointIdx < sri.PointRendererInfos.Length; ++pointIdx)
        sri.PointRendererInfos[pointIdx] = InitSector(sri, sri.Series.Elements[pointIdx], pointIdx);
    }
  }

  /// <summary>
  /// Initializes the data to draw one sector, which is coloured by its position unless it has a
  /// fill of its own.
  /// </summary>
  private static PointRendererInfo InitSector(SeriesRendererInfo sri, Point point, int pointIdx)
  {
    PointRendererInfo pri = new SectorRendererInfo();
    pri.Point = point;
    if (point == null)
      return pri;

    pri.LineFormat = sri.LineFormat;
    if (point.lineFormat is { color.IsEmpty: false })
      pri.LineFormat = new XPen(point.lineFormat.color);
    if (point.fillFormat is { color.IsEmpty: false })
      pri.FillFormat = new XSolidBrush(point.fillFormat.color);
    else
      pri.FillFormat = new XSolidBrush(PieColors.Item(pointIdx));
    pri.LineFormat.LineJoin = XLineJoin.Round;
    return pri;
  }
}
