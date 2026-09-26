#region Copyright
//
// Authors:
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using PdfPinata.Charting;

namespace PinataLayout.Rendering.ChartMapper;

/// <summary>
/// The SeriesCollectionMapper class.
/// </summary>
public class SeriesCollectionMapper
{
  /// <summary>
  /// Initializes a new instance of the <see cref="SeriesCollectionMapper"/> class.
  /// </summary>
  public SeriesCollectionMapper()
  {
  }

  private static void MapObject(SeriesCollection seriesCollection, DocumentObjectModel.Shapes.Charts.SeriesCollection domSeriesCollection)
  {
    foreach (DocumentObjectModel.Shapes.Charts.Series domSeries in domSeriesCollection)
      MapSeries(seriesCollection.AddSeries(), domSeries);
  }

  private static void MapSeries(Series series, DocumentObjectModel.Shapes.Charts.Series domSeries)
  {
    series.Name = domSeries.Name;
    series.ChartType = ChartTypeOf(domSeries);

    if (!domSeries.IsNull("DataLabel"))
      DataLabelMapper.Map(series.DataLabel, domSeries.DataLabel);
    if (!domSeries.IsNull("LineFormat"))
      LineFormatMapper.Map(series.LineFormat, domSeries.LineFormat);
    if (!domSeries.IsNull("FillFormat"))
      FillFormatMapper.Map(series.FillFormat, domSeries.FillFormat);

    series.HasDataLabel = domSeries.HasDataLabel;
    series.MarkerBackgroundColor = ToXColorOrEmpty(domSeries.MarkerBackgroundColor, domSeries);
    series.MarkerForegroundColor = ToXColorOrEmpty(domSeries.MarkerForegroundColor, domSeries);
    series.MarkerSize = domSeries.MarkerSize.Point;
    if (!domSeries.IsNull("MarkerStyle"))
      series.MarkerStyle = (MarkerStyle)domSeries.MarkerStyle;

    foreach (DocumentObjectModel.Shapes.Charts.Point domPoint in domSeries.Elements)
      MapPoint(series, domPoint);
  }

  /// <summary>
  /// The series' own chart type, or else the type of the chart it belongs to.
  /// </summary>
  private static ChartType ChartTypeOf(DocumentObjectModel.Shapes.Charts.Series domSeries)
  {
    if (!domSeries.IsNull("ChartType"))
      return (ChartType)domSeries.ChartType;

    var chart = (DocumentObjectModel.Shapes.Charts.Chart)DocumentObjectModel.DocumentRelations.GetParentOfType(domSeries, typeof(DocumentObjectModel.Shapes.Charts.Chart));
    return (ChartType)chart.Type;
  }

  private static XColor ToXColorOrEmpty(DocumentObjectModel.Color color, DocumentObjectModel.Shapes.Charts.Series domSeries)
    => color.IsEmpty ? XColor.Empty : ColorHelper.ToXColor(color, domSeries.Document.UseCmykColor);

  /// <summary>
  /// Adds one point to the series; a blank point is added as NaN.
  /// </summary>
  private static void MapPoint(Series series, DocumentObjectModel.Shapes.Charts.Point domPoint)
  {
    if (domPoint == null)
    {
      series.Add(double.NaN);
      return;
    }

    var point = series.Add(domPoint.Value);
    FillFormatMapper.Map(point.FillFormat, domPoint.FillFormat);
    // Only a line format the caller set: a point with any line format is drawn with it rather than
    // with its series', and a mapped one always states a dash style, Solid when none was given.
    if (!domPoint.IsNull("LineFormat"))
      LineFormatMapper.Map(point.LineFormat, domPoint.LineFormat);
  }

  internal static void Map(SeriesCollection seriesCollection, DocumentObjectModel.Shapes.Charts.SeriesCollection domSeriesCollection)
  {
    MapObject(seriesCollection, domSeriesCollection);
  }
}
