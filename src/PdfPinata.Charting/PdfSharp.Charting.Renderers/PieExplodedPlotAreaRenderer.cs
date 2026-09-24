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
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents a exploded pie plot area renderer.
/// </summary>
internal class PieExplodedPlotAreaRenderer : PiePlotAreaRenderer
{
  /// <summary>
  /// Initializes a new instance of the PieExplodedPlotAreaRenderer class
  /// with the specified renderer parameters.
  /// </summary>
  internal PieExplodedPlotAreaRenderer(RendererParameters parms)
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
    var origin = new XPoint(pieRect.X + pieRect.Width / 2, pieRect.Y + pieRect.Height / 2);

    double deltaAngle = 2, startAngle = 270,
      rInnerCircle = pieRect.Width / 15,
      rOuterCircle = pieRect.Width / 2;

    // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
    foreach (SectorRendererInfo sector in sri.PointRendererInfos)
    {
      if (!HasShare(sector))
      {
        MarkUndrawn(sector);
        continue;
      }

      var sweepAngle = 360 / (sumValues / Math.Abs(sector.Value));
      var midAngle = startAngle + sweepAngle / 2;

      sector.Rect = ExplodedRect(origin, midAngle, rInnerCircle, rOuterCircle);
      sector.StartAngle = Math.Max(0, startAngle + deltaAngle);
      sector.SweepAngle = Math.Max(sweepAngle, sweepAngle - deltaAngle);

      startAngle += sweepAngle;
    }
  }

  /// <summary>
  /// The rectangle of a sector's circle, pushed out from the pie's centre along the middle of the
  /// sector by the radius of the inner circle.
  /// </summary>
  private static XRect ExplodedRect(XPoint origin, double midAngle, double rInnerCircle, double rOuterCircle)
  {
    var p1 = new XPoint();
    p1.X = origin.X + rInnerCircle * Math.Cos(midAngle / 180 * Math.PI);
    p1.Y = origin.Y + rInnerCircle * Math.Sin(midAngle / 180 * Math.PI);

    var innerRect = new XRect();
    innerRect.X = p1.X - rOuterCircle + rInnerCircle;
    innerRect.Y = p1.Y - rOuterCircle + rInnerCircle;
    innerRect.Width = (rOuterCircle - rInnerCircle) * 2;
    innerRect.Height = innerRect.Width;
    return innerRect;
  }
}
