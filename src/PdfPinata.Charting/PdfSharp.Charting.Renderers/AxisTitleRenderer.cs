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
/// Represents a axis title renderer used for x and y axis titles.
/// </summary>
internal class AxisTitleRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the AxisTitleRenderer class with the
  /// specified renderer parameters.
  /// </summary>
  internal AxisTitleRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Calculates the space used for the axis title.
  /// </summary>
  internal override void Format()
  {
    var gfx = this.rendererParms.Graphics;

    var atri = ((AxisRendererInfo)this.rendererParms.RendererInfo).AxisTitleRendererInfo;
    if (atri.AxisTitleText == "")
      return;

    var size = gfx.MeasureString(atri.AxisTitleText, atri.AxisTitleFont);
    if (atri.AxisTitleOrientation != 0)
    {
      var points = new XPoint[2];
      points[0].X = 0;
      points[0].Y = 0;
      points[1].X = size.Width;
      points[1].Y = size.Height;

      var matrix = new XMatrix();  //XMatrix.Identity;
      matrix.RotatePrepend(-atri.AxisTitleOrientation);
      matrix.TransformPoints(points);

      size.Width  = Math.Abs(points[1].X - points[0].X);
      size.Height = Math.Abs(points[1].Y - points[0].Y);
    }

    atri.X = 0;
    atri.Y = 0;
    atri.Height = size.Height;
    atri.Width = size.Width;

    // Kept as well as being written to the rectangle, because the rectangle does not stay the
    // title's own size: an axis about to draw its title replaces it with the strip the title
    // is to be placed within, and Draw then needs both - the strip to align inside, and the
    // size of the thing being aligned.
    atri.AxisTitleSize = size;
  }

  /// <summary>
  /// Draws the axis title.
  /// </summary>
  internal override void Draw()
  {
    var ari = (AxisRendererInfo)this.rendererParms.RendererInfo;
    var atri = ari.AxisTitleRendererInfo;
    if (atri.AxisTitleText == "")
      return;

    var gfx = this.rendererParms.Graphics;
    if (atri.AxisTitleOrientation != 0)
    {
      // The box the caption occupies, centred on the origin: the surface is moved and turned
      // under it, and the caption is centred within it, so this decides the caption's size and
      // the transform decides where it lands.
      //
      // The caption's own size rather than the rectangle's, which is the strip the axis set
      // aside to place it in. Halving the strip instead is what used to make Right and Bottom
      // land where Center does - the strip is what the offsets below are measured against, so
      // using it on both sides of the subtraction cancelled it out.
      var caption = atri.AxisTitleSize;
      var layout = new XRect(-(caption.Width / 2), -(caption.Height / 2),
        caption.Width, caption.Height);

      var position = AxisTitleGeometry.RotatedCaption(
        atri.Rect, caption, atri.AxisTitleOrientation, atri.AxisTitleAlignment, atri.AxisTitleVerticalAlignment);

      var xsf = new XStringFormat();
      xsf.Alignment = XStringAlignment.Center;
      xsf.LineAlignment = XLineAlignment.Center;

      var state = gfx.Save();
      gfx.TranslateTransform(position.Anchor.X, position.Anchor.Y);
      gfx.RotateTransform(position.RotationDegrees);
      gfx.DrawString(atri.AxisTitleText, atri.AxisTitleFont, atri.AxisTitleBrush, layout, xsf);
      gfx.Restore(state);
    }
    else
    {
      var format = new XStringFormat();
      switch (atri.AxisTitleAlignment)
      {
        case HorizontalAlignment.Center:
          format.Alignment = XStringAlignment.Center;
          break;

        case HorizontalAlignment.Right:
          format.Alignment = XStringAlignment.Far;
          break;

        case HorizontalAlignment.Left:
        default:
          format.Alignment = XStringAlignment.Near;
          break;
      }

      switch (atri.AxisTitleVerticalAlignment)
      {
        case VerticalAlignment.Center:
          format.LineAlignment = XLineAlignment.Center;
          break;

        case VerticalAlignment.Bottom:
          format.LineAlignment = XLineAlignment.Far;
          break;

        case VerticalAlignment.Top:
        default:
          format.LineAlignment = XLineAlignment.Near;
          break;
      }

      gfx.DrawString(atri.AxisTitleText, atri.AxisTitleFont, atri.AxisTitleBrush, atri.Rect, format);
    }
  }
}
