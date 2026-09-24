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


namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a data label renderer.
/// </summary>
internal abstract class DataLabelRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the DataLabelRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal DataLabelRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Creates a data label rendererInfo.
  /// Does not return any renderer info.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    foreach (var sri in cri.SeriesRendererInfos)
    {
      if (IsLabelled(cri.Chart, sri.Series))
        sri.DataLabelRendererInfo = CreateDataLabelRendererInfo(cri, sri.Series);
    }

    return null;
  }

  /// <summary>
  /// Whether a series is to be labelled at all: when either it or the chart asked for data labels,
  /// or has a data label object to describe them.
  /// </summary>
  private static bool IsLabelled(Chart chart, Series series) =>
    chart.hasDataLabel || chart.dataLabel != null ||
    series.hasDataLabel || series.dataLabel != null;

  /// <summary>
  /// Resolves how a series' data labels are drawn.
  /// </summary>
  /// <remarks>
  /// A series' data label answers what it sets and leaves the rest to the chart's, property by
  /// property, where it used to replace the chart's outright.
  /// </remarks>
  private static DataLabelRendererInfo CreateDataLabelRendererInfo(ChartRendererInfo cri, Series series)
  {
    var own = series.dataLabel;
    var shared = cri.Chart.dataLabel;

    // The series' font inherits from the chart's data label font itself (Font.ParentFont), so
    // it is only when the series has none that the chart's is looked at here.
    var font = own?.font ?? shared?.font;

    return new DataLabelRendererInfo
    {
      Format = ResolveFormat(own, shared),
      Position = ResolvePosition(own, shared),
      Type = ResolveType(own, shared, cri.Chart.type),
      Font = Converter.ToXFont(font, cri.DefaultDataLabelFont),
      FontColor = Converter.ToXBrush(font, cri.DefaultFontColor)
    };
  }

  private static string ResolveFormat(DataLabel own, DataLabel shared) =>
    !string.IsNullOrEmpty(own?.format) ? own.format
      : !string.IsNullOrEmpty(shared?.format) ? shared.format
      : "0";

  /// <remarks>
  /// Two defaults, both from upstream and both kept so that no label moves unasked: inside the
  /// end when there is no data label object at all, outside it when there is one that does not
  /// say.
  /// </remarks>
  private static DataLabelPosition ResolvePosition(DataLabel own, DataLabel shared)
  {
    if (own is { PositionInitialized: true })
      return own.position;
    if (shared is { PositionInitialized: true })
      return shared.position;

    return own == null && shared == null ? DataLabelPosition.InsideEnd : DataLabelPosition.OutsideEnd;
  }

  /// <remarks>
  /// A pie is labelled with percentages unless told otherwise, and everything else with values.
  /// </remarks>
  private static DataLabelType ResolveType(DataLabel own, DataLabel shared, ChartType chartType)
  {
    if (own is { TypeInitialized: true })
      return own.type;
    if (shared is { TypeInitialized: true })
      return shared.type;

    return chartType is ChartType.Pie2D or ChartType.PieExploded2D ? DataLabelType.Percent : DataLabelType.Value;
  }

  /// <summary>
  /// Calculates the specific positions for each data label.
  /// </summary>
  internal abstract void CalcPositions();
}
