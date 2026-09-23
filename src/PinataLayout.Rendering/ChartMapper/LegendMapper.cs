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

internal static class LegendMapper
{
  private static void MapObject(Chart chart, DocumentObjectModel.Shapes.Charts.Chart domChart)
  {
    DocumentObjectModel.Shapes.Charts.Legend domLegend = null;
    DocumentObjectModel.Shapes.Charts.TextArea textArea = null;

    // Every area is walked, in this order, and the last legend found is the one mapped - so where
    // a chart has legends in two areas, the later area decides where the legend docks.
    FindLegend(chart, domChart.BottomArea, DockingType.Bottom, ref domLegend, ref textArea);
    FindLegend(chart, domChart.RightArea, DockingType.Right, ref domLegend, ref textArea);
    FindLegend(chart, domChart.LeftArea, DockingType.Left, ref domLegend, ref textArea);
    FindLegend(chart, domChart.TopArea, DockingType.Top, ref domLegend, ref textArea);
    FindLegend(chart, domChart.HeaderArea, DockingType.Top, ref domLegend, ref textArea);
    FindLegend(chart, domChart.FooterArea, DockingType.Bottom, ref domLegend, ref textArea);

    if (domLegend == null)
      return;

    if (!domLegend.IsNull("LineFormat"))
      LineFormatMapper.Map(chart.Legend.LineFormat, domLegend.LineFormat);
    if (!textArea.IsNull("Style"))
      FontMapper.Map(chart.Legend.Font, textArea.Document, textArea.Style);
    if (!domLegend.IsNull("Format.Font"))
      FontMapper.Map(chart.Legend.Font, domLegend.Format.Font);
  }

  /// <summary>
  /// Takes the last legend in an area, if it has one, as the legend to map, and docks the chart's
  /// legend on the side that area stands for.
  /// </summary>
  private static void FindLegend(Chart chart, DocumentObjectModel.Shapes.Charts.TextArea area, DockingType docking,
    ref DocumentObjectModel.Shapes.Charts.Legend domLegend, ref DocumentObjectModel.Shapes.Charts.TextArea textArea)
  {
    foreach (DocumentObjectModel.DocumentObject domObj in area.Elements)
    {
      if (domObj is not DocumentObjectModel.Shapes.Charts.Legend legend)
        continue;

      chart.Legend.Docking = docking;
      domLegend = legend;
      textArea = area;
    }
  }

  internal static void Map(Chart chart, DocumentObjectModel.Shapes.Charts.Chart domChart)
  {
    MapObject(chart, domChart);
  }
}
