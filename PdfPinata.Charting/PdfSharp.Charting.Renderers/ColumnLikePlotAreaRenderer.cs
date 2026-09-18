#region PDFsharp Charting - A .NET charting library based on PDFsharp
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
internal abstract class ColumnLikePlotAreaRenderer : PlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnLikePlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnLikePlotAreaRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Layouts and calculates the space for column like plot areas.
  /// </summary>
  internal override void Format()
  {
    ChartRendererInfo cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    double xMin = cri.xAxisRendererInfo.MinimumScale;
    double xMax = cri.xAxisRendererInfo.MaximumScale;
    double yMin = cri.yAxisRendererInfo.MinimumScale;
    double yMax = cri.yAxisRendererInfo.MaximumScale;

    XRect plotAreaBox = cri.plotAreaRendererInfo.Rect;

    // A chart with nothing plotted has a category scale of zero, because that scale is the number
    // of points in its longest series. Dividing by it gives an infinity, and every coordinate
    // derived from the matrix is then NaN - which is written to the page as the word NaN and makes
    // the file unreadable. There is nothing to plot, so the matrix is left as the identity and the
    // renderers that would use it draw their nothing against it.
    if (xMax <= xMin || yMax <= yMin)
    {
      cri.plotAreaRendererInfo.matrix = new XMatrix();
      return;
    }

    // The width is divided by the span between the two, not by xMax. The translate above has
    // already moved xMin to the origin, so the distance left to fit across the plot area is
    // xMax - xMin; dividing by xMax alone agrees with it only while xMin is zero. That is true
    // today - the category axis fixes its minimum there, CalculateXAxisValues assigning it where
    // the value axis takes one from the Axis object - so this changes nothing now. It means the
    // pair goes on agreeing if the category axis ever learns to honour a minimum, rather than
    // quietly drawing the whole chart short of the edge it was scaled to reach.
    cri.plotAreaRendererInfo.matrix = new XMatrix();  //XMatrix.Identity;
    cri.plotAreaRendererInfo.matrix.TranslatePrepend(-xMin, yMax);
    cri.plotAreaRendererInfo.matrix.Scale(plotAreaBox.Width / (xMax - xMin), plotAreaBox.Height / (yMax - yMin), XMatrixOrder.Append);
    cri.plotAreaRendererInfo.matrix.ScalePrepend(1, -1);
    cri.plotAreaRendererInfo.matrix.Translate(plotAreaBox.X, plotAreaBox.Y, XMatrixOrder.Append);
  }
}
