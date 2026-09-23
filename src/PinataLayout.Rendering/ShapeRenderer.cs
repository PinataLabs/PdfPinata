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
      var layoutInfo = new LayoutInfo
      {
        MarginTop = shape.WrapFormat.DistanceTop.Point,
        MarginLeft = shape.WrapFormat.DistanceLeft.Point,
        MarginBottom = shape.WrapFormat.DistanceBottom.Point,
        MarginRight = shape.WrapFormat.DistanceRight.Point,
        KeepTogether = true,
        KeepWithNext = false,
        PageBreakBefore = false,
        VerticalReference = GetVerticalReference(),
        HorizontalReference = GetHorizontalReference(),
        Floating = GetFloating()
      };
      if (layoutInfo.Floating == Floating.TopBottom &&!shape.Top.Position.IsEmpty)
      {
        layoutInfo.MarginTop = Math.Max(layoutInfo.MarginTop, shape.Top.Position);
      }
      return layoutInfo;
    }
  }

  private Floating GetFloating()
  {
    if (shape.RelativeVertical != RelativeVertical.Line &&
        shape.RelativeVertical != RelativeVertical.Paragraph)
        return Floating.None;

    return shape.WrapFormat.Style switch
    {
      WrapStyle.None or WrapStyle.Through => Floating.None,
      // The wrap style names the side the text runs down; Floating names the same side. Both
      // enumerations read the same way round, which is the point of saying so in each of them.
      WrapStyle.Left => Floating.Left,
      WrapStyle.Right => Floating.Right,
      // A line is given one span of the width available to it rather than every span, so asking
      // for the roomier side and asking for either side come to the same thing. See the remarks
      // on WrapStyle.Both.
      WrapStyle.Largest or WrapStyle.Both => Floating.BothSides,
      _ => Floating.TopBottom
    };
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


  private void FinishLayoutInfo(Area area)
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

  private HorizontalReference GetHorizontalReference()
  {
    return shape.RelativeHorizontal switch
    {
      RelativeHorizontal.Margin => HorizontalReference.PageMargin,
      RelativeHorizontal.Page => HorizontalReference.Page,
      _ => HorizontalReference.AreaBoundary
    };
  }

  private VerticalReference GetVerticalReference()
  {
    return shape.RelativeVertical switch
    {
      RelativeVertical.Margin => VerticalReference.PageMargin,
      RelativeVertical.Page => VerticalReference.Page,
      _ => VerticalReference.PreviousElement
    };
  }

  private ElementAlignment GetVerticalAlignment()
  {
    return shape.Top.ShapePosition switch
    {
      ShapePosition.Center => ElementAlignment.Center,
      ShapePosition.Bottom => ElementAlignment.Far,
      _ => ElementAlignment.Near
    };
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

  private ElementAlignment GetHorizontalAlignment()
  {
    return shape.Left.ShapePosition switch
    {
      ShapePosition.Center => ElementAlignment.Center,
      ShapePosition.Right => ElementAlignment.Far,
      ShapePosition.Outside => ElementAlignment.Outside,
      ShapePosition.Inside => ElementAlignment.Inside,
      _ => ElementAlignment.Near
    };
  }
  protected LineFormatRenderer lineFormatRenderer;
  protected FillFormatRenderer fillFormatRenderer;
  protected Shape shape;
}
