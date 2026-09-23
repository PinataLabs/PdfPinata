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

using System.Collections;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted text frame.
/// </summary>
internal class FormattedTextFrame : IAreaProvider
{
  internal FormattedTextFrame(TextFrame textframe, DocumentRenderer documentRenderer, FieldInfos fieldInfos)
  {
    this.textframe = textframe;
    this.fieldInfos = fieldInfos;
    this.documentRenderer = documentRenderer;
  }

  internal void Format(XGraphics graphics)
  {
    gfx = graphics;
    isFirstArea = true;
    formatter = new TopDownFormatter(this, documentRenderer, textframe.Elements);
    formatter.FormatOnAreas(graphics, false);
  }

  Area IAreaProvider.GetNextArea()
  {
    return isFirstArea ? CalcContentRect() : null;
  }

  Area IAreaProvider.ProbeNextArea()
  {
    return null;
  }

  FieldInfos IAreaProvider.AreaFieldInfos => fieldInfos;

  void IAreaProvider.StoreRenderInfos(ArrayList infos)
  {
    renderInfos = infos;
  }

  bool IAreaProvider.IsAreaBreakBefore(LayoutInfo layoutInfo)
  {
    return false;
  }

  internal RenderInfo[] GetRenderInfos()
  {
    if (renderInfos == null)
      return null;

    // Not ToArray(Type): it builds the array type at run time, which carries
    // RequiresDynamicCode and an AOT compiler cannot always have code for.
    var result = new RenderInfo[renderInfos.Count];
    renderInfos.CopyTo(result);
    return result;
  }

  private Rectangle CalcContentRect()
  {
    var lfr = new LineFormatRenderer(textframe.LineFormat, gfx);
    var lineWidth = lfr.GetWidth();
    XUnit width;
    XUnit xOffset = lineWidth / 2;
    XUnit yOffset = lineWidth / 2;

    if (textframe.Orientation == TextOrientation.Horizontal ||
        textframe.Orientation == TextOrientation.HorizontalRotatedFarEast)
    {
      width = textframe.Width.Point;
      xOffset += textframe.MarginLeft;
      yOffset += textframe.MarginTop;
      width -= xOffset;
      width -= textframe.MarginRight + lineWidth / 2;
    }
    else
    {
      width = textframe.Height.Point;
      if (textframe.Orientation == TextOrientation.Upward)
      {
        xOffset += textframe.MarginBottom;
        yOffset += textframe.MarginLeft;
        width -= xOffset;
        width -= textframe.MarginTop + lineWidth / 2;
      }
      else
      {
        xOffset += textframe.MarginTop;
        yOffset += textframe.MarginRight;
        width -= xOffset;
        width -= textframe.MarginBottom + lineWidth / 2;
      }
    }
    XUnit height = double.MaxValue;
    return new Rectangle(xOffset, yOffset, width, height);
  }

  bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
  {
    return false;
  }

  bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
  {
    var rect = CalcContentRect();
    switch (layoutInfo.HorizontalAlignment)
    {
      case ElementAlignment.Near:
        if (layoutInfo.Left == 0)
          return false;

        layoutInfo.ContentArea.X += layoutInfo.Left;
        return true;

      case ElementAlignment.Far:
        XUnit xPos = rect.X + rect.Width;
        xPos -= layoutInfo.ContentArea.Width;
        xPos -= layoutInfo.MarginRight;
        layoutInfo.ContentArea.X = xPos;
        return true;

      case ElementAlignment.Center:
        xPos = rect.Width;
        xPos -= layoutInfo.ContentArea.Width;
        xPos = rect.X + xPos / 2;
        layoutInfo.ContentArea.X = xPos;
        return true;
    }
    return false;
  }

  private readonly TextFrame textframe;
  private readonly FieldInfos fieldInfos;
  private TopDownFormatter formatter;
  private ArrayList renderInfos;
  private XGraphics gfx;
  private bool isFirstArea;
  private readonly DocumentRenderer documentRenderer;
}
