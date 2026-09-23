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
/// Base class for all plot area renderers.
/// </summary>
internal abstract class PlotAreaRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the PlotAreaRenderer class with the specified renderer parameters.
  /// </summary>
  internal PlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Returns an initialized PlotAreaRendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var pari = new PlotAreaRendererInfo
    {
      // Through the property, not the field. The field is filled in lazily, and a chart whose plot
      // area was never asked for has none, so reading the field here worked only because every chart
      // renderer's Init had touched the property first, on a line that read as dead code.
      PlotArea = ((ChartRendererInfo)rendererParms.RendererInfo).Chart.PlotArea
    };
    InitLineFormat(pari);
    InitFillFormat(pari);
    return pari;
  }

  /// <summary>
  /// Initializes the plot area's line format common to all derived plot area renderers.
  /// If line format is given all uninitialized values will be set.
  /// </summary>
  protected static void InitLineFormat(PlotAreaRendererInfo rendererInfo)
  {
    if (rendererInfo.PlotArea.lineFormat != null)
      rendererInfo.LineFormat = Converter.ToXPen(rendererInfo.PlotArea.lineFormat, XColors.Black, DefaultLineWidth);
  }

  /// <summary>
  /// Initializes the plot area's fill format common to all derived plot area renderers.
  /// If fill format is given all uninitialized values will be set.
  /// </summary>
  protected static void InitFillFormat(PlotAreaRendererInfo rendererInfo)
  {
    if (rendererInfo.PlotArea.fillFormat != null)
      rendererInfo.FillFormat = Converter.ToXBrush(rendererInfo.PlotArea.fillFormat, XColors.White);
  }

  /// <summary>
  /// Represents the default line width for the plot area's border.
  /// </summary>
  protected const double DefaultLineWidth = 0.15;
}
