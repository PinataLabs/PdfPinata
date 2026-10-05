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
/// Represents column like chart renderer: a chart whose category axis runs across the page and
/// whose value axis runs up it.
/// </summary>
internal abstract class ColumnLikeChartRenderer : CartesianChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnLikeChartRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnLikeChartRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <inheritdoc/>
  protected override AxisOrientation CategoryAxis => AxisOrientation.Horizontal;

  /// <inheritdoc/>
  protected override AxisRenderer CreateXAxisRenderer() => new HorizontalXAxisRenderer(rendererParms);

  /// <summary>
  /// Calculates the chart layout.
  /// </summary>
  protected override void LayOut()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    // Calculate rects and positions.
    var chartRect = LayoutLegend();
    cri.XAxisRendererInfo.X = chartRect.Left + cri.YAxisRendererInfo.Width;
    cri.XAxisRendererInfo.Y = chartRect.Bottom - cri.XAxisRendererInfo.Height;
    cri.XAxisRendererInfo.Width = chartRect.Width - cri.YAxisRendererInfo.Width;
    cri.YAxisRendererInfo.X = chartRect.Left;
    cri.YAxisRendererInfo.Y = chartRect.Top;
    cri.YAxisRendererInfo.Height = cri.XAxisRendererInfo.Y - chartRect.Top;
    cri.PlotAreaRendererInfo.X = cri.XAxisRendererInfo.X;
    cri.PlotAreaRendererInfo.Y = cri.YAxisRendererInfo.InnerRect.Y;
    cri.PlotAreaRendererInfo.Width = cri.XAxisRendererInfo.Width;
    cri.PlotAreaRendererInfo.Height = cri.YAxisRendererInfo.InnerRect.Height;
  }
}
