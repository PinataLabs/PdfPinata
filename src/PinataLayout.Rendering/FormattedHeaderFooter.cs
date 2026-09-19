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
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted header or footer.
/// </summary>
internal class FormattedHeaderFooter : IAreaProvider
{
  internal FormattedHeaderFooter(HeaderFooter headerFooter, DocumentRenderer documentRenderer, FieldInfos fieldInfos)
  {
    this.headerFooter = headerFooter;
    this.fieldInfos = fieldInfos;
    this.documentRenderer = documentRenderer;
  }

  internal void Format(XGraphics gfx)
  {
    this.gfx = gfx;
    isFirstArea = true;
    formatter = new TopDownFormatter(this, documentRenderer, headerFooter.Elements);
    formatter.FormatOnAreas(gfx, false);
    contentHeight = RenderInfo.GetTotalHeight(GetRenderInfos());
  }

  Area IAreaProvider.GetNextArea()
  {
    if (isFirstArea)
      return new Rectangle(ContentRect.X, ContentRect.Y, ContentRect.Width, double.MaxValue);

    return null;
  }

  Area IAreaProvider.ProbeNextArea()
  {
    return null;
  }

  FieldInfos IAreaProvider.AreaFieldInfos => fieldInfos;

  void IAreaProvider.StoreRenderInfos(ArrayList renderInfos)
  {
    this.renderInfos = renderInfos;
  }

  bool IAreaProvider.IsAreaBreakBefore(LayoutInfo layoutInfo)
  {
    return false;
  }


  internal RenderInfo[] GetRenderInfos()
  {
    if (renderInfos != null)
    {
      // Not ToArray(Type): it builds the array type at run time, which carries
      // RequiresDynamicCode and an AOT compiler cannot always have code for.
      var result = new RenderInfo[renderInfos.Count];
      renderInfos.CopyTo(result);
      return result;
    }

    return Array.Empty<RenderInfo>();
  }

  internal Rectangle ContentRect
  {
    get => contentRect;
    set => contentRect = value;
  }
  private Rectangle contentRect;

  XUnit ContentHeight => contentHeight;

  bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
  {
    IAreaProvider formattedDoc = (IAreaProvider)documentRenderer.FormattedDocument;
    return formattedDoc.PositionVertically(layoutInfo);
  }

  bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
  {
    IAreaProvider formattedDoc = (IAreaProvider)documentRenderer.FormattedDocument;
    return formattedDoc.PositionHorizontally(layoutInfo); ;
  }

  private HeaderFooter headerFooter;
  private FieldInfos fieldInfos;
  private TopDownFormatter formatter;
  private ArrayList renderInfos;
  private XGraphics gfx;
  private bool isFirstArea;
  private XUnit contentHeight;
  private DocumentRenderer documentRenderer;
}
