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

using System.Collections;
using PdfPinata.Drawing;
using PdfPinata.Charting.Renderers;

namespace PdfPinata.Charting;

/// <summary>
/// Represents the frame which holds one or more charts.
/// </summary>
public class ChartFrame
{
  /// <summary>
  /// Initializes a new instance of the ChartFrame class.
  /// </summary>
  public ChartFrame()
  {
  }

  /// <summary>
  /// Initializes a new instance of the ChartFrame class with the specified rectangle.
  /// </summary>
  public ChartFrame(XRect rect)
  {
    location = rect.Location;
    size = rect.Size;
  }

  /// <summary>
  /// Gets or sets the location of the ChartFrame.
  /// </summary>
  public XPoint Location
  {
    get => location;
    set => location = value;
  }
  private XPoint location;

  /// <summary>
  /// Gets or sets the size of the ChartFrame.
  /// </summary>
  public XSize Size
  {
    get => size;
    set => size = value;
  }
  private XSize size;

  /// <summary>
  /// Adds a chart to the ChartFrame.
  /// </summary>
  public void Add(Chart chart)
  {
    chartList ??= new ArrayList();
    chartList.Add(chart);
  }

  /// <summary>
  /// Draws all charts inside the ChartFrame.
  /// </summary>
  public void Draw(XGraphics gfx)
  {
    // Draw frame of ChartFrame. First shadow frame.
    var dx = 5;
    var dy = 5;
    gfx.DrawRoundedRectangle(XBrushes.Gainsboro,
      location.X + dx, location.Y + dy,
      size.Width, size.Height, 20, 20);

    var chartRect = new XRect(location.X, location.Y, size.Width, size.Height);
    var brush = new XLinearGradientBrush(chartRect, XColor.FromArgb(0xFFD0DEEF), XColors.White,
      XLinearGradientMode.Vertical);
    var penBorder = new XPen(XColors.SteelBlue, 2.5);
    gfx.DrawRoundedRectangle(penBorder, brush,
      location.X, location.Y, size.Width, size.Height,
      15, 15);

    var state = gfx.Save();
    gfx.TranslateTransform(location.X, location.Y);

    // Calculate rectangle for all charts. Y-Position will be moved for each chart.
    var charts = chartList.Count;
    uint dxChart = 20;
    uint dyChart = 20;
    uint dyBetweenCharts = 30;
    var rect = new XRect(dxChart, dyChart,
      size.Width - 2 * dxChart,
      (size.Height - (charts - 1) * dyBetweenCharts - 2 * dyChart) / charts);

    // draw each chart in list
    foreach (Chart chart in chartList)
    {
      var parms = new RendererParameters(gfx, rect) { DrawingItem = chart };

      var renderer = GetChartRenderer(chart, parms);
      renderer.Init();
      renderer.Format();
      renderer.Draw();

      rect.Y += rect.Height + dyBetweenCharts;
    }
    gfx.Restore(state);
  }

  /// <summary>
  /// Draws first chart only.
  /// </summary>
  public void DrawChart(XGraphics gfx)
  {
    var state = gfx.Save();
    gfx.TranslateTransform(location.X, location.Y);

    if (chartList.Count > 0)
    {
      var chartRect = new XRect(0, 0, size.Width, size.Height);
      var chart = (Chart)chartList[0];
      var parms = new RendererParameters(gfx, chartRect) { DrawingItem = chart };

      var renderer = GetChartRenderer(chart, parms);
      renderer.Init();
      renderer.Format();
      renderer.Draw();
    }
    gfx.Restore(state);
  }

  /// <summary>
  /// Returns the chart renderer appropriate for the chart.
  /// </summary>
  private static ChartRenderer GetChartRenderer(Chart chart, RendererParameters parms)
  {
    var chartType = chart.Type;
    var useCombinationRenderer = false;

    // The property rather than the field: SeriesCollection creates the collection the first time
    // it is asked for, so a chart nothing was ever added to has none at all, and reading the field
    // threw here before any renderer had run. The same lazy shape as Chart.XAxis.
    foreach (Series series in chart.SeriesCollection)
    {
      if (series.chartType != chartType)
      {
        useCombinationRenderer = true;
        break;
      }
    }

    if (useCombinationRenderer)
      return new CombinationChartRenderer(parms);

    switch (chartType)
    {
      case ChartType.Line:
        return new LineChartRenderer(parms);

      case ChartType.Column2D:
      case ChartType.ColumnStacked2D:
        return new ColumnChartRenderer(parms);

      case ChartType.Bar2D:
      case ChartType.BarStacked2D:
        return new BarChartRenderer(parms);

      case ChartType.Area2D:
        return new AreaChartRenderer(parms);

      case ChartType.Pie2D:
      case ChartType.PieExploded2D:
        return new PieChartRenderer(parms);
    }

    return null;
  }

  /// <summary>
  /// Holds the charts which will be drawn inside the ChartFrame.
  /// </summary>
  private ArrayList chartList;
}
