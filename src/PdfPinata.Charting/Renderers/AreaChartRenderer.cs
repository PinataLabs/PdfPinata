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

  /// <inheritdoc/>
  protected override AxisRenderer CreateYAxisRenderer() => new VerticalYAxisRenderer(rendererParms);

  /// <inheritdoc/>
  protected override PlotAreaRenderer CreatePlotAreaRenderer() => new AreaPlotAreaRenderer(rendererParms);

  /// <summary>
  /// Initializes all necessary data to draw all series for a area chart.
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
  /// Initializes the data to draw one point, which takes the series' formats unless it has its own.
  /// </summary>
  private static PointRendererInfo InitPoint(SeriesRendererInfo sri, Point point)
  {
    var pri = new PointRendererInfo();
    pri.Point = point;
    if (point == null)
      return pri;

    pri.LineFormat = sri.LineFormat;
    pri.FillFormat = sri.FillFormat;
    // A point's own line format is not read: an area is outlined once, as one polygon, with the
    // series' pen, so a point has no border of its own to draw.
    if (point.fillFormat is { color.IsEmpty: false })
      pri.FillFormat = new XSolidBrush(point.fillFormat.color);
    return pri;
  }
}
