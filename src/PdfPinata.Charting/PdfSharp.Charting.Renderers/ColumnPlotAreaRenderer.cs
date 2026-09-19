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
/// Represents a plot area renderer of clustered columns, i. e. all columns are drawn side by side.
/// </summary>
internal abstract class ColumnPlotAreaRenderer : ColumnLikePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the ColumnPlotAreaRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal ColumnPlotAreaRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Layouts and calculates the space for each column.
  /// </summary>
  internal override void Format()
  {
    base.Format();
    CalcColumns();
  }

  /// <summary>
  /// Draws the content of the column plot area.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)this.rendererParms.RendererInfo;

    var plotAreaBox = cri.PlotAreaRendererInfo.Rect;
    if (HasNoRoom(plotAreaBox))
      return;

    var gfx = this.rendererParms.Graphics;

    var xMin = cri.XAxisRendererInfo.MinimumScale;
    var xMax = cri.XAxisRendererInfo.MaximumScale;
    var yMin = cri.YAxisRendererInfo.MinimumScale;
    var yMax = cri.YAxisRendererInfo.MaximumScale;

    LineFormatRenderer lineFormatRenderer;

    // Under some circumstances it is possible that no zero base line will be drawn,
    // e. g. because of unfavourable minimum/maximum scale and/or major tick, so force to draw
    // a zero base line if necessary.
    if (cri.YAxisRendererInfo.MajorGridlinesLineFormat != null ||
        cri.YAxisRendererInfo.MinorGridlinesLineFormat != null)
    {
      if (yMin < 0 && yMax > 0)
      {
        var points = new XPoint[2];
        points[0].X = xMin;
        points[0].Y = 0;
        points[1].X = xMax;
        points[1].Y = 0;
        cri.PlotAreaRendererInfo.Matrix.TransformPoints(points);

        if (cri.YAxisRendererInfo.MinorGridlinesLineFormat != null)
          lineFormatRenderer = new LineFormatRenderer(gfx, cri.YAxisRendererInfo.MinorGridlinesLineFormat);
        else
          lineFormatRenderer = new LineFormatRenderer(gfx, cri.YAxisRendererInfo.MajorGridlinesLineFormat);

        lineFormatRenderer.DrawLine(points[0], points[1]);
      }
    }

    // Draw columns
    var state = gfx.Save();
    foreach (var sri in cri.SeriesRendererInfos)
    {
      // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
      foreach (ColumnRendererInfo column in sri.PointRendererInfos)
      {
        // Do not draw column if value is outside yMin/yMax range. Clipping does not make sense.
        if (IsDataInside(yMin, yMax, column.Value))
          gfx.DrawRectangle(column.FillFormat, column.Rect);
      }
    }

    // Draw borders around column.
    // A border can overlap neighbor columns, so it is important to draw borders at the end.
    foreach (var sri in cri.SeriesRendererInfos)
    {
      // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
      foreach (ColumnRendererInfo column in sri.PointRendererInfos)
      {
        // Do not draw column if value is outside yMin/yMax range. Clipping does not make sense.
        if (IsDataInside(yMin, yMax, column.Value) && column.LineFormat.Width > 0)
        {
          lineFormatRenderer = new LineFormatRenderer(gfx, column.LineFormat);
          lineFormatRenderer.DrawRectangle(column.Rect);
        }
      }
    }
    gfx.Restore(state);
  }

  /// <summary>
  /// Calculates the position, width and height of each column of all series.
  /// </summary>
  protected abstract void CalcColumns();

  /// <summary>
  /// If yValue is within the range from yMin to yMax returns true, otherwise false.
  /// </summary>
  protected abstract bool IsDataInside(double yMin, double yMax, double yValue);
}
