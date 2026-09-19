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

using System;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders a shape to an XGraphics object.
/// </summary>
internal abstract class ShapeRenderer : Renderer
{

  internal ShapeRenderer(XGraphics gfx, Shape shape, FieldInfos fieldInfos)
    : base(gfx, shape, fieldInfos)
  {
    this.shape = shape;
    var lf = (LineFormat)this.shape.GetValue("LineFormat", GV.ReadOnly);
    lineFormatRenderer = new LineFormatRenderer(lf, gfx);
  }

  internal ShapeRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
    : base(gfx, renderInfo, fieldInfos)
  {
    shape = (Shape)renderInfo.DocumentObject;
    var lf = (LineFormat)shape.GetValue("LineFormat", GV.ReadOnly);
    lineFormatRenderer = new LineFormatRenderer(lf, gfx);
    var ff = (FillFormat)shape.GetValue("FillFormat", GV.ReadOnly);
    fillFormatRenderer = new FillFormatRenderer(ff, gfx);
  }

  internal override LayoutInfo InitialLayoutInfo
  {
    get
    {
      var layoutInfo = new LayoutInfo();

      layoutInfo.MarginTop = shape.WrapFormat.DistanceTop.Point;
      layoutInfo.MarginLeft = shape.WrapFormat.DistanceLeft.Point;
      layoutInfo.MarginBottom = shape.WrapFormat.DistanceBottom.Point;
      layoutInfo.MarginRight = shape.WrapFormat.DistanceRight.Point;
      layoutInfo.KeepTogether = true;
      layoutInfo.KeepWithNext = false;
      layoutInfo.PageBreakBefore = false;
      layoutInfo.VerticalReference = GetVerticalReference();
      layoutInfo.HorizontalReference = GetHorizontalReference();
      layoutInfo.Floating = GetFloating();
      if (layoutInfo.Floating == Floating.TopBottom &&!shape.Top.Position.IsEmpty)
      {
        layoutInfo.MarginTop = Math.Max(layoutInfo.MarginTop, shape.Top.Position);
      }
      return layoutInfo;
    }
  }

  Floating GetFloating()
  {
    if (shape.RelativeVertical != RelativeVertical.Line &&
        shape.RelativeVertical != RelativeVertical.Paragraph)
      return Floating.None;

    switch (shape.WrapFormat.Style)
    {
      case WrapStyle.None:
      case WrapStyle.Through:
        return Floating.None;

      // The wrap style names the side the text runs down; Floating names the same side. Both
      // enumerations read the same way round, which is the point of saying so in each of them.
      case WrapStyle.Left:
        return Floating.Left;

      case WrapStyle.Right:
        return Floating.Right;

      // A line is given one span of the width available to it rather than every span, so asking
      // for the roomier side and asking for either side come to the same thing. See the remarks
      // on WrapStyle.Both.
      case WrapStyle.Largest:
      case WrapStyle.Both:
        return Floating.BothSides;
    }
    return Floating.TopBottom;
  }

  /// <summary>
  /// Gets the shape width including line width.
  /// </summary>
  protected virtual XUnit ShapeWidth => shape.Width + lineFormatRenderer.GetWidth();

  /// <summary>
  /// Gets the shape height including line width.
  /// </summary>
  protected virtual XUnit ShapeHeight => shape.Height + lineFormatRenderer.GetWidth();

  /// <summary>
  /// Formats the shape.
  /// </summary>
  /// <param name="area">The area to fit in the shape.</param>
  /// <param name="previousFormatInfo"></param>
  internal override void Format(Area area, FormatInfo previousFormatInfo)
  {
    var floating = GetFloating();
    var fits = floating == Floating.None || ShapeHeight <= area.Height;
    ((ShapeFormatInfo)renderInfo.FormatInfo).fits = fits;
    FinishLayoutInfo(area);
  }


  void FinishLayoutInfo(Area area)
  {
    var layoutInfo = renderInfo.LayoutInfo;
    Area contentArea = new Rectangle(area.X, area.Y, ShapeWidth, ShapeHeight);
    layoutInfo.ContentArea = contentArea;
    layoutInfo.MarginTop = shape.WrapFormat.DistanceTop.Point;
    layoutInfo.MarginLeft = shape.WrapFormat.DistanceLeft.Point;
    layoutInfo.MarginBottom = shape.WrapFormat.DistanceBottom.Point;
    layoutInfo.MarginRight = shape.WrapFormat.DistanceRight.Point;
    layoutInfo.KeepTogether = true;
    layoutInfo.KeepWithNext = false;
    layoutInfo.PageBreakBefore = false;
    layoutInfo.MinWidth = ShapeWidth;

    if (shape.Top.ShapePosition == ShapePosition.Undefined)
      layoutInfo.Top = shape.Top.Position.Point;

    layoutInfo.VerticalAlignment = GetVerticalAlignment();
    layoutInfo.HorizontalAlignment = GetHorizontalAlignment();

    if (shape.Left.ShapePosition == ShapePosition.Undefined)
      layoutInfo.Left = shape.Left.Position.Point;

    layoutInfo.HorizontalReference = GetHorizontalReference();
    layoutInfo.VerticalReference = GetVerticalReference();
    layoutInfo.Floating = GetFloating();
  }

  HorizontalReference GetHorizontalReference()
  {
    switch (shape.RelativeHorizontal)
    {
      case RelativeHorizontal.Margin:
        return HorizontalReference.PageMargin;
      case RelativeHorizontal.Page:
        return HorizontalReference.Page;
    }
    return HorizontalReference.AreaBoundary;
  }

  VerticalReference GetVerticalReference()
  {
    switch (shape.RelativeVertical)
    {
      case RelativeVertical.Margin:
        return VerticalReference.PageMargin;

      case RelativeVertical.Page:
        return VerticalReference.Page;
    }
    return VerticalReference.PreviousElement;
  }

  ElementAlignment GetVerticalAlignment()
  {
    switch (shape.Top.ShapePosition)
    {
      case ShapePosition.Center:
        return ElementAlignment.Center;

      case ShapePosition.Bottom:
        return ElementAlignment.Far;
    }
    return ElementAlignment.Near;
  }

  protected void RenderFilling()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    fillFormatRenderer.Render(contentArea.X, contentArea.Y, contentArea.Width, contentArea.Height);
  }

  protected void RenderLine()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var lineWidth = lineFormatRenderer.GetWidth();
    XUnit width = contentArea.Width - lineWidth;
    XUnit height = contentArea.Height - lineWidth;
    lineFormatRenderer.Render(contentArea.X, contentArea.Y, width, height);
  }

  ElementAlignment GetHorizontalAlignment()
  {
    switch (shape.Left.ShapePosition)
    {
      case ShapePosition.Center:
        return ElementAlignment.Center;

      case ShapePosition.Right:
        return ElementAlignment.Far;

      case ShapePosition.Outside:
        return ElementAlignment.Outside;

      case ShapePosition.Inside:
        return ElementAlignment.Inside;
    }
    return ElementAlignment.Near;
  }
  protected LineFormatRenderer lineFormatRenderer;
  protected FillFormatRenderer fillFormatRenderer;
  protected Shape shape;
}
