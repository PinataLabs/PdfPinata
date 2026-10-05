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
  /// Returns the specific y axis renderer.
  /// </summary>
  protected override AxisRenderer CreateYAxisRenderer()
  {
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Column2D => new VerticalYAxisRenderer(rendererParms),
      ChartType.ColumnStacked2D => new VerticalStackedYAxisRenderer(rendererParms),
      _ => null
    };
  }

  /// <summary>
  /// Returns the specific plot area renderer.
  /// </summary>
  protected override PlotAreaRenderer CreatePlotAreaRenderer()
  {
    var chart = (Chart)rendererParms.DrawingItem;
    return chart.type switch
    {
      ChartType.Column2D => new ColumnClusteredPlotAreaRenderer(rendererParms, AxisOrientation.Horizontal),
      ChartType.ColumnStacked2D => new ColumnStackedPlotAreaRenderer(rendererParms, AxisOrientation.Horizontal),
      _ => null
    };
  }

  /// <inheritdoc/>
  protected override DataLabelRenderer CreateDataLabelRenderer() =>
    new ColumnDataLabelRenderer(rendererParms, AxisOrientation.Horizontal);

  /// <summary>
  /// Initializes all necessary data to draw all series for a column chart.
  /// </summary>
  internal override void InitSeries()
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
  /// Initializes the data to draw one column, which takes the series' formats unless it has its own.
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
