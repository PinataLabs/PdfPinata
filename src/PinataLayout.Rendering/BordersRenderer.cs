#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using System.Diagnostics;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Tables;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders a single Border.
/// </summary>
internal class BordersRenderer
{
  internal BordersRenderer(Borders borders, XGraphics gfx)
  {
    Debug.Assert(borders.Document != null);
    this.gfx = gfx;
    this.borders = borders;
  }

  private Border GetBorder(BorderType type)
  {
    return (Border)borders.GetValue(type.ToString(), GV.ReadOnly);
  }

  private XColor GetColor(BorderType type)
  {
    var clr = Colors.Black;

    var border = GetBorder(type);
    if (border is { Color.IsEmpty: false })
      clr = border.Color;
    else if (!borders.Color.IsEmpty)
      clr = borders.Color;

    return ColorHelper.ToXColor(clr, borders.Document.UseCmykColor);
  }

  private BorderStyle GetStyle(BorderType type)
  {
    var style = BorderStyle.Single;

    var border = GetBorder(type);
    if (border != null && !border.IsNull("Style"))
      style = border.Style;
    else if (!borders.IsNull("Style"))
      style = borders.Style;

    return style;
  }

  internal XUnit GetWidth(BorderType type)
  {
    if (borders == null)
      return 0;

    var border = GetBorder(type);

    if (border != null)
    {
      if (!border.IsNull("Visible") && !border.Visible)
        return 0;

      // ReSharper disable once ConditionIsAlwaysTrueOrFalse
      if (border != null && !border.IsNull("Width"))
        return border.Width.Point;

      if (!border.IsNull("Color") || !border.IsNull("Style") || border.Visible)
      {
        return !borders.IsNull("Width") ? borders.Width.Point : 0.5;
      }
    }
    else if (!(type == BorderType.DiagonalDown || type == BorderType.DiagonalUp))
    {
      if (!borders.IsNull("Visible") && !borders.Visible)
        return 0;

      if (!borders.IsNull("Width"))
        return borders.Width.Point;

      if (!borders.IsNull("Color") || !borders.IsNull("Style") || borders.Visible)
        return 0.5;
    }
    return 0;
  }

  /// <summary>
  /// Renders the border top down.
  /// </summary>
  /// <param name="type">The type of the border.</param>
  /// <param name="left">The left position of the border.</param>
  /// <param name="top">The top position of the border.</param>
  /// <param name="height">The height on which to render the border.</param>
  internal void RenderVertically(BorderType type, XUnit left, XUnit top, XUnit height)
  {
    var borderWidth = GetWidth(type);
    if (borderWidth == 0)
      return;

    left += borderWidth / 2;
    gfx.DrawLine(GetPen(type), left, top + height, left, top);
  }

  /// <summary>
  /// Renders the border top down.
  /// </summary>
  /// <param name="type">The type of the border.</param>
  /// <param name="left">The left position of the border.</param>
  /// <param name="top">The top position of the border.</param>
  /// <param name="width">The width on which to render the border.</param>
  internal void RenderHorizontally(BorderType type, XUnit left, XUnit top, XUnit width)
  {
    var borderWidth = GetWidth(type);
    if (borderWidth == 0)
      return;

    top += borderWidth / 2;
    gfx.DrawLine(GetPen(type), left + width, top, left, top);
  }


  internal void RenderDiagonally(BorderType type, XUnit left, XUnit top, XUnit width, XUnit height)
  {
    var borderWidth = GetWidth(type);
    if (borderWidth == 0)
      return;

    var state = gfx.Save();
    gfx.IntersectClip(new XRect(left, top, width, height));

    if (type == BorderType.DiagonalDown)
      gfx.DrawLine(GetPen(type), left, top, left + width, top + height);
    else if (type == BorderType.DiagonalUp)
      gfx.DrawLine(GetPen(type), left, top + height, left + width, top);

    gfx.Restore(state);
  }

  internal void RenderRounded(RoundedCorner roundedCorner, XUnit x, XUnit y, XUnit width, XUnit height)
  {
    if (roundedCorner == RoundedCorner.None)
      return;

    // As source we use the vertical borders.
    // If not set originally, they have been set to the horizontal border values in TableRenderer.EqualizeRoundedCornerBorders().
    var borderType = BorderType.Top;
    if (roundedCorner == RoundedCorner.TopLeft || roundedCorner == RoundedCorner.BottomLeft)
      borderType = BorderType.Left;
    if (roundedCorner == RoundedCorner.TopRight || roundedCorner == RoundedCorner.BottomRight)
      borderType = BorderType.Right;

    var borderWidth = GetWidth(borderType);
    var borderPen = GetPen(borderType);

    if (borderWidth == 0)
      return;

    x -= borderWidth / 2;
    y -= borderWidth / 2;
    XUnit ellipseWidth = width * 2 + borderWidth;
    XUnit ellipseHeight = height * 2 + borderWidth;

    switch (roundedCorner) {
      case RoundedCorner.TopLeft:
        gfx.DrawArc(borderPen, new XRect(x, y, ellipseWidth, ellipseHeight), 180, 90);
        break;
      case RoundedCorner.TopRight:
        gfx.DrawArc(borderPen, new XRect(x - width, y, ellipseWidth, ellipseHeight), 270, 90);
        break;
      case RoundedCorner.BottomRight:
        gfx.DrawArc(borderPen, new XRect(x - width, y - height, ellipseWidth, ellipseHeight), 0, 90);
        break;
      case RoundedCorner.BottomLeft:
        gfx.DrawArc(borderPen, new XRect(x, y - height, ellipseWidth, ellipseHeight), 90, 90);
        break;
    }
  }

  private XPen GetPen(BorderType type)
  {
    var borderWidth = GetWidth(type);
    if (borderWidth == 0)
      return null;

    var pen = new XPen(GetColor(type), borderWidth);
    var style = GetStyle(type);
    switch (style)
    {
      case BorderStyle.DashDot:
        pen.DashStyle = XDashStyle.DashDot;
        break;

      case BorderStyle.DashDotDot:
        pen.DashStyle = XDashStyle.DashDotDot;
        break;

      case BorderStyle.DashLargeGap:
        pen.DashPattern = [3, 3];
        break;

      case BorderStyle.DashSmallGap:
        pen.DashPattern = [5, 1];
        break;

      case BorderStyle.Dot:
        pen.DashStyle = XDashStyle.Dot;
        break;

      case BorderStyle.Single:
      default:
        pen.DashStyle = XDashStyle.Solid;
        break;
    }
    return pen;
  }

  private XGraphics gfx;
  private Borders borders;
}
