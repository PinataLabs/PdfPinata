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
using System.Collections;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted text area.
/// </summary>
internal class FormattedTextArea : IAreaProvider
{
  internal FormattedTextArea(DocumentRenderer documentRenderer, TextArea textArea, FieldInfos fieldInfos)
  {
    this.textArea = textArea;
    this.fieldInfos = fieldInfos;
    this.documentRenderer = documentRenderer;
  }

  internal void Format(XGraphics graphics)
  {
    gfx = graphics;
    isFirstArea = true;
    formatter = new TopDownFormatter(this, documentRenderer, textArea.Elements);
    formatter.FormatOnAreas(graphics, false);
  }

  internal XUnit InnerWidth
  {
    set => innerWidth = value;
    get
    {
      if (!double.IsNaN(innerWidth))
        return innerWidth;

      if (!textArea.IsNull("Width"))
        innerWidth = textArea.Width.Point;
      else
        innerWidth = CalcInherentWidth();
      return innerWidth;
    }
  }
  private XUnit innerWidth = double.NaN;

  internal XUnit InnerHeight
  {
    get
    {
      if (textArea.IsNull("Height"))
        return ContentHeight + textArea.TopPadding + textArea.BottomPadding;
      return textArea.Height.Point;
    }
  }


  private XUnit CalcInherentWidth()
  {
    XUnit inherentWidth = 0;
    foreach (DocumentObject obj in textArea.Elements)
    {
      var renderer = Renderer.Create(gfx, documentRenderer, obj, fieldInfos);
      if (renderer == null)
        continue;

      renderer.Format(new Rectangle(0, 0, double.MaxValue, double.MaxValue), null);
      inherentWidth = Math.Max(renderer.RenderInfo.LayoutInfo.MinWidth, inherentWidth);
    }
    inherentWidth += textArea.LeftPadding;
    inherentWidth += textArea.RightPadding;
    return inherentWidth;
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

  internal XUnit ContentHeight => RenderInfo.GetTotalHeight(GetRenderInfos());

  private Rectangle CalcContentRect()
  {
    XUnit width = InnerWidth - textArea.LeftPadding - textArea.RightPadding;
    XUnit height = double.MaxValue;
    return new Rectangle(0, 0, width, height);
  }

  bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
  {
    return false;
  }

  bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
  {
    return false;
  }

  internal TextArea textArea;
  private FieldInfos fieldInfos;
  private TopDownFormatter formatter;
  private ArrayList renderInfos;
  private XGraphics gfx;
  private bool isFirstArea;
  private DocumentRenderer documentRenderer;
}
