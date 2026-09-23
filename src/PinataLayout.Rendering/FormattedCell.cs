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
using PinataLayout.DocumentObjectModel.Tables;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted cell.
/// </summary>
internal class FormattedCell : IAreaProvider
{
  internal FormattedCell(Cell cell, DocumentRenderer documentRenderer, Borders cellBorders, FieldInfos fieldInfos, XUnit xOffset, XUnit yOffset)
  {
    this.cell = cell;
    this.fieldInfos = fieldInfos;
    this.yOffset = yOffset;
    this.xOffset = xOffset;
    bordersRenderer = new BordersRenderer(cellBorders, null);
    this.documentRenderer = documentRenderer;
  }

  private bool isFirstArea = true;
  Area IAreaProvider.GetNextArea()
  {
    if (!isFirstArea)
      return null;

    var rect = CalcContentRect();
    isFirstArea = false;
    return rect;
  }

  Area IAreaProvider.ProbeNextArea()
  {
    return null;
  }

  internal void Format(XGraphics graphics)
  {
    gfx = graphics;
    formatter = new TopDownFormatter(this, documentRenderer, cell.Elements);
    formatter.FormatOnAreas(graphics, false);
    ContentHeight = CalcContentHeight(documentRenderer);
  }

  private Rectangle CalcContentRect()
  {
    var column = cell.Column;
    var width = InnerWidth;
    width -= column.LeftPadding.Point;
    var rightColumn = cell.Table.Columns[cell.MergedRightColumnIndex];
    width -= rightColumn.RightPadding.Point;

    XUnit height = double.MaxValue;
    return new Rectangle(xOffset, yOffset, width, height);
  }

  internal XUnit ContentHeight { get; private set; } = 0;

  internal XUnit InnerHeight
  {
    get
    {
      var row = cell.Row;
      XUnit verticalPadding = row.TopPadding.Point;
      verticalPadding += row.BottomPadding.Point;

      switch (row.HeightRule)
      {
        case RowHeightRule.Exactly:
          return row.Height.Point;

        case RowHeightRule.Auto:
          return verticalPadding + ContentHeight;

        case RowHeightRule.AtLeast:
        default:
          return Math.Max(row.Height, verticalPadding + ContentHeight);
      }
    }
  }

  internal XUnit InnerWidth
  {
    get
    {
      XUnit width = 0;
      var lastColumnIdx = cell.MergedRightColumnIndex;
      for (var columnIdx = cell.Column.Index; columnIdx <= lastColumnIdx; ++columnIdx)
        width += cell.Table.Columns[columnIdx].Width;
      width -= bordersRenderer.GetWidth(BorderType.Right);

      return width;
    }
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

  bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
  {
    return false;
  }

  bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
  {
    return false;
  }

  private XUnit CalcContentHeight(DocumentRenderer renderer)
  {
    var height = RenderInfo.GetTotalHeight(GetRenderInfos());
    if (height != 0)
      return height;

    height = ParagraphRenderer.GetLineHeight(cell.Format, gfx, renderer);
    height += cell.Format.SpaceBefore;
    height += cell.Format.SpaceAfter;
    return height;
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

  private readonly FieldInfos fieldInfos;
  private ArrayList renderInfos;
  private readonly XUnit xOffset;
  private readonly XUnit yOffset;
  private readonly Cell cell;
  private TopDownFormatter formatter;
  private readonly BordersRenderer bordersRenderer;
  private XGraphics gfx;
  private readonly DocumentRenderer documentRenderer;
}
