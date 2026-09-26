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
    FormatMatrix(AxisOrientation.Horizontal);
  }

  /// <summary>
  /// Sets up the matrix that takes a point of chart space, as <see cref="PlotOrientation.At"/>
  /// places it for a category axis running as <paramref name="categoryAxis"/> says, to the page.
  /// </summary>
  /// <remarks>
  /// Both orientations are the one matrix with chart space's two coordinates swapped: the first is
  /// fitted across the plot area from its left edge, the second up it from its foot. So a column
  /// chart's categories run left to right and its values upwards, and a bar chart's values run left
  /// to right and its categories upwards - the first category at the foot, as in Excel.
  /// </remarks>
  protected void FormatMatrix(AxisOrientation categoryAxis)
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var plotAreaBox = cri.PlotAreaRendererInfo.Rect;

    // A chart with nothing plotted has a category scale of zero, because that scale is the number
    // of points in its longest series. Dividing by it gives an infinity, and every coordinate
    // derived from the matrix is then NaN - which is written to the page as the word NaN and makes
    // the file unreadable. There is nothing to plot, so the matrix is left as the identity and the
    // renderers that would use it draw their nothing against it.
    if (NothingToPlot(cri))
    {
      cri.PlotAreaRendererInfo.Matrix = new XMatrix();
      return;
    }

    // The two corners of the scales, in chart space.
    var min = categoryAxis.At(cri.XAxisRendererInfo.MinimumScale, cri.YAxisRendererInfo.MinimumScale);
    var max = categoryAxis.At(cri.XAxisRendererInfo.MaximumScale, cri.YAxisRendererInfo.MaximumScale);

    // Each extent is divided by the span of its scale, not by its maximum. The translate has
    // already moved the minimum to the origin, so the distance left to fit across the plot area is
    // the span; dividing by the maximum alone agrees with it only while the minimum is zero. That is
    // true of the category axis today - it fixes its minimum there, CalculateXAxisValues assigning
    // it where the value axis takes one from the Axis object - so this changes nothing now. It means
    // the chart goes on filling its plot area if the category axis ever learns to honour a minimum,
    // rather than quietly drawing short of the edge it was scaled to reach.
    cri.PlotAreaRendererInfo.Matrix = new XMatrix();  //XMatrix.Identity;
    cri.PlotAreaRendererInfo.Matrix.TranslatePrepend(-min.X, max.Y);
    cri.PlotAreaRendererInfo.Matrix.Scale(plotAreaBox.Width / (max.X - min.X), plotAreaBox.Height / (max.Y - min.Y), XMatrixOrder.Append);
    cri.PlotAreaRendererInfo.Matrix.ScalePrepend(1, -1);
    cri.PlotAreaRendererInfo.Matrix.Translate(plotAreaBox.X, plotAreaBox.Y, XMatrixOrder.Append);
  }

  /// <summary>
  /// Whether either scale spans nothing, which leaves the plot area nothing to plot against: a
  /// chart with no points, or a value axis whose minimum the caller set at or above its maximum.
  /// </summary>
  protected static bool NothingToPlot(ChartRendererInfo cri)
  {
    return cri.XAxisRendererInfo.MaximumScale <= cri.XAxisRendererInfo.MinimumScale ||
           cri.YAxisRendererInfo.MaximumScale <= cri.YAxisRendererInfo.MinimumScale;
  }
}
