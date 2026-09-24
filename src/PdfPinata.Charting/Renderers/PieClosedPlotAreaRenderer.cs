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

using System;
using System.Linq;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a closed pie plot area renderer.
/// </summary>
internal class PieClosedPlotAreaRenderer : PiePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the PiePlotAreaRenderer class
  /// with the specified renderer parameters.
  /// </summary>
  internal PieClosedPlotAreaRenderer(RendererParameters parms)
    : base(parms)
  { }

  /// <summary>
  /// Calculate angles for each sector.
  /// </summary>
  protected override void CalcSectors()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;
    if (cri.SeriesRendererInfos.Length == 0)
      return;

    var sri = cri.SeriesRendererInfos[0];

    var sumValues = sri.SumOfPoints;
    if (sumValues == 0)
      return;

    var pieRect = PieRect(cri, sri);

    double startAngle = 270;
    foreach (var sector in sri.PointRendererInfos.Cast<SectorRendererInfo>())
    {
      if (!HasShare(sector))
      {
        MarkUndrawn(sector);
        continue;
      }

      var sweepAngle = 360 / (sumValues / Math.Abs(sector.Value));

      sector.Rect = pieRect;
      sector.StartAngle = startAngle;
      sector.SweepAngle = sweepAngle;

      startAngle += sweepAngle;
    }
  }
}
