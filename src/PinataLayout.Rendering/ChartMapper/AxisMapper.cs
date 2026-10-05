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

using PdfPinata.Charting;

namespace PinataLayout.Rendering.ChartMapper;

/// <summary>
/// The AxisMapper class.
/// </summary>
public class AxisMapper
{
  /// <summary>
  /// Initializes a new instance of the <see cref="AxisMapper"/> class.
  /// </summary>
  public AxisMapper()
  {
  }

  private static void MapObject(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    MapTickLabels(axis, domAxis);
    MapTicks(axis, domAxis);

    if (!domAxis.IsNull(nameof(domAxis.Title)))
      MapTitle(axis, domAxis);

    MapGridlines(axis, domAxis);
    MapScale(axis, domAxis);

    if (!domAxis.IsNull(nameof(domAxis.LineFormat)))
      LineFormatMapper.Map(axis.LineFormat, domAxis.LineFormat);
  }

  private static void MapTickLabels(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    if (!domAxis.IsNull($"{nameof(domAxis.TickLabels)}.{nameof(domAxis.TickLabels.Format)}"))
      axis.TickLabels.Format = domAxis.TickLabels.Format;
    if (!domAxis.IsNull($"{nameof(domAxis.TickLabels)}.{nameof(domAxis.TickLabels.Style)}"))
      FontMapper.Map(axis.TickLabels.Font, domAxis.TickLabels.Document, domAxis.TickLabels.Style);
    if (!domAxis.IsNull($"{nameof(domAxis.TickLabels)}.{nameof(domAxis.TickLabels.Font)}"))
      FontMapper.Map(axis.TickLabels.Font, domAxis.TickLabels.Font);
  }

  /// <summary>
  /// Maps the shape of the tick marks and the distance between them.
  /// </summary>
  private static void MapTicks(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    if (!domAxis.IsNull(nameof(domAxis.MajorTickMark)))
      axis.MajorTickMark = (TickMarkType)domAxis.MajorTickMark;
    if (!domAxis.IsNull(nameof(domAxis.MinorTickMark)))
      axis.MinorTickMark = (TickMarkType)domAxis.MinorTickMark;

    if (!domAxis.IsNull(nameof(domAxis.MajorTick)))
      axis.MajorTick = domAxis.MajorTick;
    if (!domAxis.IsNull(nameof(domAxis.MinorTick)))
      axis.MinorTick = domAxis.MinorTick;
  }

  private static void MapTitle(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    axis.Title.Caption = domAxis.Title.Caption;
    if (!domAxis.IsNull($"{nameof(domAxis.Title)}.{nameof(domAxis.Title.Style)}"))
      FontMapper.Map(axis.Title.Font, domAxis.Title.Document, domAxis.Title.Style);
    if (!domAxis.IsNull($"{nameof(domAxis.Title)}.{nameof(domAxis.Title.Font)}"))
      FontMapper.Map(axis.Title.Font, domAxis.Title.Font);
    axis.Title.Orientation = domAxis.Title.Orientation.Value;
    axis.Title.Alignment = (HorizontalAlignment)domAxis.Title.Alignment;
    axis.Title.VerticalAlignment = (VerticalAlignment)domAxis.Title.VerticalAlignment;
  }

  private static void MapGridlines(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    axis.HasMajorGridlines = domAxis.HasMajorGridlines;
    axis.HasMinorGridlines = domAxis.HasMinorGridlines;

    if (!domAxis.IsNull(nameof(domAxis.MajorGridlines)) && !domAxis.MajorGridlines.IsNull(nameof(domAxis.MajorGridlines.LineFormat)))
      LineFormatMapper.Map(axis.MajorGridlines.LineFormat, domAxis.MajorGridlines.LineFormat);
    if (!domAxis.IsNull(nameof(domAxis.MinorGridlines)) && !domAxis.MinorGridlines.IsNull(nameof(domAxis.MinorGridlines.LineFormat)))
      LineFormatMapper.Map(axis.MinorGridlines.LineFormat, domAxis.MinorGridlines.LineFormat);
  }

  private static void MapScale(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    if (!domAxis.IsNull(nameof(domAxis.MaximumScale)))
      axis.MaximumScale = domAxis.MaximumScale;
    if (!domAxis.IsNull(nameof(domAxis.MinimumScale)))
      axis.MinimumScale = domAxis.MinimumScale;
  }

  internal static void Map(Axis axis, DocumentObjectModel.Shapes.Charts.Axis domAxis)
  {
    MapObject(axis, domAxis);
  }
}
