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
using System.Collections.Generic;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Visitors;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Internals;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders a table to an XGraphics object.
/// </summary>
internal class TableRenderer : Renderer
{
  internal TableRenderer(XGraphics gfx, Table documentObject, FieldInfos fieldInfos)
    :
    base(gfx, documentObject, fieldInfos)
  {
    table = (Table)documentObject;
  }

  internal TableRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
    :
    base(gfx, renderInfo, fieldInfos)
  {
    table = (Table)this.renderInfo.DocumentObject;
  }

  internal override LayoutInfo InitialLayoutInfo
  {
    get
    {
      LayoutInfo layoutInfo = new LayoutInfo();
      layoutInfo.KeepTogether = table.KeepTogether;
      layoutInfo.KeepWithNext = false;
      layoutInfo.MarginBottom = 0;
      layoutInfo.MarginLeft = 0;
      layoutInfo.MarginTop = 0;
      layoutInfo.MarginRight = 0;
      return layoutInfo;
    }
  }


  void InitRendering()
  {
    TableFormatInfo formatInfo = (TableFormatInfo)renderInfo.FormatInfo;
    bottomBorderMap = formatInfo.bottomBorderMap;
    connectedRowsMap = formatInfo.connectedRowsMap;
    formattedCells = formatInfo.formattedCells;

    currRow = formatInfo.startRow;
    startRow = formatInfo.startRow;
    endRow = formatInfo.endRow;

    mergedCells = formatInfo.mergedCells;
    lastHeaderRow = formatInfo.lastHeaderRow;
    startX = renderInfo.LayoutInfo.ContentArea.X;
    startY = renderInfo.LayoutInfo.ContentArea.Y;
  }

  /// <summary>
  ///
  /// </summary>
  void RenderHeaderRows()
  {
    if (lastHeaderRow < 0)
      return;

    foreach (Cell cell in mergedCells)
    {
      if (cell.Row.Index <= lastHeaderRow)
        RenderCell(cell);
    }
  }

  void RenderCell(Cell cell)
  {
    Rectangle innerRect = GetInnerRect(CalcStartingHeight(), cell);

    using (Tagger.Enter(RowElementOf(cell)))
    using (Tagger.Container(gfx, cell, IsHeaderCell(cell) ? PdfTag.TH : PdfTag.TD, out var element))
    {
      DescribeCell(cell, element);

      // Shading and borders are decoration and go out as artifacts; only what is in the cell is
      // content. A reader that announced every rule would be unusable on a bordered table.
      using (Tagger.Artifact(gfx))
        RenderShading(cell, innerRect);

      RenderContent(cell, innerRect);

      using (Tagger.Artifact(gfx))
        RenderBorders(cell, innerRect);
    }
  }

  /// <summary>
  /// Whether a cell heads its column rather than holding data.
  /// </summary>
  /// <remarks>
  /// The same test the renderer uses to decide which rows to repeat at the top of a continuation
  /// page, so the two cannot disagree: a row repeated as a heading is tagged as one.
  /// </remarks>
  bool IsHeaderCell(Cell cell) => cell.Row.Index <= lastHeaderRow;

  /// <summary>
  /// Writes what a reader needs in order to place a cell: which way its heading reaches, and how far
  /// it spans when it has been merged with its neighbours.
  /// </summary>
  /// <remarks>
  /// Without these a table reads as a stream of values with nothing to attach them to. The scope is
  /// what lets a reader say "Total: 49.20" instead of "49.20", and the spans are what stop a merged
  /// cell shifting every value after it into the wrong column.
  /// </remarks>
  /// <param name="cell">The cell being described.</param>
  /// <param name="element">
  /// The element opened for it, which is null when it was not tagged — inside a header or footer,
  /// for instance. Passed in rather than read from the tagger, because a refused scope leaves the
  /// enclosing element current and these entries would then describe that.
  /// </param>
  void DescribeCell(Cell cell, PdfStructureElement element)
  {
    if (element == null)
      return;

    var columns = cell.MergeRight + 1;
    var rows = cell.MergeDown + 1;
    var header = IsHeaderCell(cell);

    if (!header && columns == 1 && rows == 1)
      return;

    var attributes = new PdfPinata.Pdf.PdfDictionary(element.Owner);
    attributes.Elements.SetName("/O", "/Table");

    if (header)
    {
      // /Column and not /Row: a heading row heads the columns beneath it. PinataLayout has no notion of
      // a heading column, so /Row never arises here — a table wanting one has to be tagged by hand.
      attributes.Elements.SetName("/Scope", "/Column");
    }

    if (columns > 1)
      attributes.Elements.SetInteger("/ColSpan", columns);

    if (rows > 1)
      attributes.Elements.SetInteger("/RowSpan", rows);

    element.Elements["/A"] = attributes;
  }

  private static void EqualizeRoundedCornerBorders(Cell cell) {
    // If any of a corner relevant border is set, we want to copy its values to the second corner relevant border,
    // to ensure the innerWidth of the cell is the same, regardless of which border is used.
    // If set, we use the vertical borders as source for the values, otherwise we use the horizontal borders.
    RoundedCorner roundedCorner = cell.RoundedCorner;

    if (roundedCorner == RoundedCorner.None)
      return;

    BorderType primaryBorderType = BorderType.Top, secondaryBorderType = BorderType.Top;

    if (roundedCorner == RoundedCorner.TopLeft || roundedCorner == RoundedCorner.BottomLeft)
      primaryBorderType = BorderType.Left;
    if (roundedCorner == RoundedCorner.TopRight || roundedCorner == RoundedCorner.BottomRight)
      primaryBorderType = BorderType.Right;

    if (roundedCorner == RoundedCorner.TopLeft || roundedCorner == RoundedCorner.TopRight)
      secondaryBorderType = BorderType.Top;
    if (roundedCorner == RoundedCorner.BottomLeft || roundedCorner == RoundedCorner.BottomRight)
      secondaryBorderType = BorderType.Bottom;

    // If both borders don't exist, there's nothing to do and we should not create one by accessing it.
    if (!cell.Borders.HasBorder(primaryBorderType) && !cell.Borders.HasBorder(secondaryBorderType))
      return;

    // Get the borders. By using GV.ReadWrite we create the border, if not existing.
    Border primaryBorder = (Border) cell.Borders.GetValue(primaryBorderType.ToString(), GV.ReadWrite);
    Border secondaryBorder = (Border) cell.Borders.GetValue(secondaryBorderType.ToString(), GV.ReadWrite);

    Border source = primaryBorder.Visible ? primaryBorder : secondaryBorder.Visible ? secondaryBorder : null;
    Border target = primaryBorder.Visible ? secondaryBorder : secondaryBorder.Visible ? primaryBorder : null;

    if (source == null || target == null)
      return;

    target.Visible = source.Visible;
    target.Width = source.Width;
    target.Style = source.Style;
    target.Color = source.Color;
  }

  void RenderShading(Cell cell, Rectangle innerRect)
  {
    ShadingRenderer shadeRenderer = new ShadingRenderer(gfx, cell.Shading);
    shadeRenderer.Render(innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height, cell.RoundedCorner);
  }

  void RenderBorders(Cell cell, Rectangle innerRect)
  {
    XUnit leftPos = innerRect.X;
    XUnit rightPos = leftPos + innerRect.Width;
    XUnit topPos = innerRect.Y;
    XUnit bottomPos = innerRect.Y + innerRect.Height;
    Borders mergedBorders = mergedCells.GetEffectiveBorders(cell);

    BordersRenderer bordersRenderer = new BordersRenderer(mergedBorders, gfx);
    XUnit bottomWidth = bordersRenderer.GetWidth(BorderType.Bottom);
    XUnit leftWidth = bordersRenderer.GetWidth(BorderType.Left);
    XUnit topWidth = bordersRenderer.GetWidth(BorderType.Top);
    XUnit rightWidth = bordersRenderer.GetWidth(BorderType.Right);

    if (cell.RoundedCorner == RoundedCorner.TopLeft)
      bordersRenderer.RenderRounded(cell.RoundedCorner, innerRect.X, innerRect.Y, innerRect.Width + rightWidth, innerRect.Height + bottomWidth);
    else if (cell.RoundedCorner == RoundedCorner.TopRight)
      bordersRenderer.RenderRounded(cell.RoundedCorner, innerRect.X - leftWidth, innerRect.Y, innerRect.Width + leftWidth, innerRect.Height + bottomWidth);
    else if (cell.RoundedCorner == RoundedCorner.BottomLeft)
      bordersRenderer.RenderRounded(cell.RoundedCorner, innerRect.X, innerRect.Y - topWidth, innerRect.Width + rightWidth, innerRect.Height + topWidth);
    else if (cell.RoundedCorner == RoundedCorner.BottomRight)
      bordersRenderer.RenderRounded(cell.RoundedCorner, innerRect.X - leftWidth, innerRect.Y - topWidth, innerRect.Width + leftWidth, innerRect.Height + topWidth);

    // Render horizontal and vertical borders only if touching no rounded corner.
    if (cell.RoundedCorner != RoundedCorner.TopRight && cell.RoundedCorner != RoundedCorner.BottomRight)
      bordersRenderer.RenderVertically(BorderType.Right, rightPos, topPos, bottomPos + bottomWidth - topPos);

    if (cell.RoundedCorner != RoundedCorner.TopLeft && cell.RoundedCorner != RoundedCorner.BottomLeft)
      bordersRenderer.RenderVertically(BorderType.Left, leftPos - leftWidth, topPos, bottomPos + bottomWidth - topPos);

    if (cell.RoundedCorner != RoundedCorner.BottomLeft && cell.RoundedCorner != RoundedCorner.BottomRight)
      bordersRenderer.RenderHorizontally(BorderType.Bottom, leftPos - leftWidth, bottomPos, rightPos + rightWidth + leftWidth - leftPos);

    if (cell.RoundedCorner != RoundedCorner.TopLeft && cell.RoundedCorner != RoundedCorner.TopRight)
      bordersRenderer.RenderHorizontally(BorderType.Top, leftPos - leftWidth, topPos - topWidth, rightPos + rightWidth + leftWidth - leftPos);

    RenderDiagonalBorders(mergedBorders, innerRect);
  }

  void RenderDiagonalBorders(Borders mergedBorders, Rectangle innerRect)
  {
    BordersRenderer bordersRenderer = new BordersRenderer(mergedBorders, gfx);
    bordersRenderer.RenderDiagonally(BorderType.DiagonalDown, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height);
    bordersRenderer.RenderDiagonally(BorderType.DiagonalUp, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height);
  }

  void RenderContent(Cell cell, Rectangle innerRect)
  {
    FormattedCell formattedCell = (FormattedCell)formattedCells[cell];
    RenderInfo[] renderInfos = formattedCell.GetRenderInfos();

    if (renderInfos == null)
      return;

    VerticalAlignment verticalAlignment = cell.VerticalAlignment;
    XUnit contentHeight = formattedCell.ContentHeight;
    XUnit innerHeight = innerRect.Height;
    XUnit targetX = innerRect.X + cell.Column.LeftPadding;

    XUnit targetY;
    if (verticalAlignment == VerticalAlignment.Bottom)
    {
      targetY = innerRect.Y + innerRect.Height;
      targetY -= cell.Row.BottomPadding;
      targetY -= contentHeight;
    }
    else if (verticalAlignment == VerticalAlignment.Center)
    {
      targetY = innerRect.Y + cell.Row.TopPadding;
      targetY += innerRect.Y + innerRect.Height - cell.Row.BottomPadding;
      targetY -= contentHeight;
      targetY /= 2;
    }
    else
      targetY = innerRect.Y + cell.Row.TopPadding;

    RenderByInfos(targetX, targetY, renderInfos);
  }



  Rectangle GetInnerRect(XUnit startingHeight, Cell cell)
  {
    BordersRenderer bordersRenderer = new BordersRenderer(mergedCells.GetEffectiveBorders(cell), gfx);
    FormattedCell formattedCell = (FormattedCell)formattedCells[cell];
    XUnit width = formattedCell.InnerWidth;

    XUnit y = startY;
    if (cell.Row.Index > lastHeaderRow)
      y += startingHeight;
    else
      y += CalcMaxTopBorderWidth(0);

    XUnit upperBorderPos = (XUnit)bottomBorderMap[cell.Row.Index];

    y += upperBorderPos;
    if (cell.Row.Index > lastHeaderRow)
      y -= (XUnit)bottomBorderMap[startRow];

    XUnit lowerBorderPos = (XUnit)bottomBorderMap[cell.Row.Index + cell.MergeDown + 1];


    XUnit height = lowerBorderPos - upperBorderPos;
    height -= bordersRenderer.GetWidth(BorderType.Bottom);

    XUnit x = startX;
    for (int clmIdx = 0; clmIdx < cell.Column.Index; ++clmIdx)
    {
      x += table.Columns[clmIdx].Width;
    }
    x += LeftBorderOffset;

    return new Rectangle(x, y, width, height);
  }

  internal override void Render()
  {
    InitRendering();

    Tagger.EndList();
    using (Tagger.Container(gfx, table, PdfTag.Table, out var element))
    {
      DescribeTable(element);
      RenderHeaderRows();

      if (startRow < table.Rows.Count)
      {
        Cell cell = table[startRow, 0];

        int cellIdx = mergedCells.BinarySearch(table[startRow, 0], new CellComparer());
        while (cellIdx < mergedCells.Count)
        {
          cell = (Cell)mergedCells[cellIdx];
          if (cell.Row.Index > endRow)
            break;

          RenderCell(cell);
          ++cellIdx;
        }
      }
    }
  }

  /// <summary>
  /// Writes the table's summary onto its element, once.
  /// </summary>
  /// <remarks>
  /// Header cells and their scope let a reader walk a table one cell at a time. The summary is what
  /// tells it, before it starts, whether the table is worth walking — so it is the one thing here
  /// that has to come from the caller, and <see cref="Table.Summary"/> is where they put it.
  /// </remarks>
  /// <param name="element">
  /// The element opened for the table, which is null when it was not tagged. Passed in for the same
  /// reason as in <see cref="DescribeCell"/>.
  /// </param>
  void DescribeTable(PdfStructureElement element)
  {
    if (element == null || table.IsNull("Summary"))
      return;

    element.Elements.SetString("/Summary", table.Summary);
  }

  /// <summary>
  /// The row a cell belongs to, as an element of the tree.
  /// </summary>
  /// <remarks>
  /// Cells are drawn out of a flat list rather than row by row — the list is in row-major order, so
  /// asking for the row of each cell in turn builds the rows in the order a reader wants them, and
  /// asking twice for the same row hands back the one already built. That last part is what carries
  /// a table over a page boundary: the heading rows are drawn again at the top of every page the
  /// table continues onto, and they have to stay the same rows.
  /// </remarks>
  PdfStructureElement RowElementOf(Cell cell) =>
    Tagger.Element(cell.Row, PdfTag.TR, Tagger.Parent);

  void InitFormat(Area area, FormatInfo previousFormatInfo)
  {
    TableFormatInfo prevTableFormatInfo = (TableFormatInfo)previousFormatInfo;
    TableRenderInfo tblRenderInfo = new TableRenderInfo();
    tblRenderInfo.table = table;

    // Equalize the two borders, that are used to determine a rounded corner's border.
    // This way the innerWidth of the cell, which is got by the saved _formattedCells, is the same regardless of which corner relevant border is set.
    foreach (Row row in table.Rows)
    foreach (Cell cell in row.Cells)
      EqualizeRoundedCornerBorders(cell);

    renderInfo = tblRenderInfo;

    if (prevTableFormatInfo != null)
    {
      mergedCells = prevTableFormatInfo.mergedCells;
      formattedCells = prevTableFormatInfo.formattedCells;
      bottomBorderMap = prevTableFormatInfo.bottomBorderMap;
      lastHeaderRow = prevTableFormatInfo.lastHeaderRow;
      connectedRowsMap = prevTableFormatInfo.connectedRowsMap;
      startRow = prevTableFormatInfo.endRow + 1;
    }
    else
    {
      mergedCells = new MergedCellList(table);
      FormatCells();
      CalcLastHeaderRow();
      CreateConnectedRows();
      CreateBottomBorderMap();
      if (doHorizontalBreak)
      {
        CalcLastHeaderColumn();
        CreateConnectedColumns();
      }
      startRow = lastHeaderRow + 1;
    }
    ((TableFormatInfo)tblRenderInfo.FormatInfo).mergedCells = mergedCells;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).formattedCells = formattedCells;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).bottomBorderMap = bottomBorderMap;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).connectedRowsMap = connectedRowsMap;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).lastHeaderRow = lastHeaderRow;
  }

  void FormatCells()
  {
    formattedCells = new SortedList<Cell, FormattedCell>(new CellComparer());
    foreach (Cell cell in mergedCells)
    {
      FormattedCell formattedCell = new FormattedCell(cell, documentRenderer, mergedCells.GetEffectiveBorders(cell), fieldInfos, 0, 0);
      formattedCell.Format(gfx);
      formattedCells.Add(cell, formattedCell);
    }
  }

  /// <summary>
  /// Formats (measures) the table.
  /// </summary>
  /// <param name="area">The area on which to fit the table.</param>
  /// <param name="previousFormatInfo"></param>
  internal override void Format(Area area, FormatInfo previousFormatInfo)
  {
    DocumentElements elements = DocumentRelations.GetParent(table) as DocumentElements;
    if (elements != null)
    {
      Section section = DocumentRelations.GetParent(elements) as Section;
      if (section != null)
        doHorizontalBreak = section.PageSetup.HorizontalPageBreak;
    }

    renderInfo = new TableRenderInfo();
    InitFormat(area, previousFormatInfo);

    // Don't take any Rows higher then MaxElementHeight
    XUnit topHeight = CalcStartingHeight();
    XUnit probeHeight = topHeight;
    XUnit offset = 0;
    if (startRow > lastHeaderRow + 1 &&
        startRow < table.Rows.Count)
      offset = (XUnit)bottomBorderMap[startRow] - topHeight;
    else
      offset = -CalcMaxTopBorderWidth(0);

    int probeRow = startRow;
    XUnit currentHeight = 0;
    XUnit startingHeight = 0;
    bool isEmpty = false;

    while (probeRow < table.Rows.Count)
    {
      bool firstProbe = probeRow == startRow;
      probeRow = (int)connectedRowsMap[probeRow];
      // Don't take any Rows higher then MaxElementHeight
      probeHeight = (XUnit)bottomBorderMap[probeRow + 1] - offset;
      if (firstProbe && probeHeight > MaxElementHeight - Tolerance)
        probeHeight = MaxElementHeight - Tolerance;

      //The height for the first new row(s) + headerrows:
      if (startingHeight == 0)
      {
        if (probeHeight > area.Height)
        {
          isEmpty = true;
          break;
        }
        startingHeight = probeHeight;
      }

      if (probeHeight > area.Height)
        break;

      else
      {
        currRow = probeRow;
        currentHeight = probeHeight;
        ++probeRow;
      }
    }
    if (!isEmpty)
    {
      TableFormatInfo formatInfo = (TableFormatInfo)renderInfo.FormatInfo;
      formatInfo.startRow = startRow;
      formatInfo.isEnding = currRow >= table.Rows.Count - 1;
      formatInfo.endRow = currRow;
    }
    FinishLayoutInfo(area, currentHeight, startingHeight);
  }

  void FinishLayoutInfo(Area area, XUnit currentHeight, XUnit startingHeight)
  {
    LayoutInfo layoutInfo = renderInfo.LayoutInfo;
    layoutInfo.StartingHeight = startingHeight;
    //REM: Trailing height would have to be calculated in case tables had a keep with next property.
    layoutInfo.TrailingHeight = 0;
    if (currRow >= 0)
    {
      layoutInfo.ContentArea = new Rectangle(area.X, area.Y, 0, currentHeight);
      XUnit width = LeftBorderOffset;
      foreach (Column clm in table.Columns)
      {
        width += clm.Width;
      }
      layoutInfo.ContentArea.Width = width;
    }
    layoutInfo.MinWidth = layoutInfo.ContentArea.Width;

    if (!table.Rows.IsNull("LeftIndent"))
      layoutInfo.Left = table.Rows.LeftIndent.Point;

    else if (table.Rows.Alignment == RowAlignment.Left)
    {
      if (table.Columns.Count > 0) // Errors in Wiki syntax can lead to tables w/o columns ...
      {
        XUnit leftOffset = LeftBorderOffset;
        leftOffset += table.Columns[0].LeftPadding;
        layoutInfo.Left = -leftOffset;
      }
    }

    switch (table.Rows.Alignment)
    {
      case RowAlignment.Left:
        layoutInfo.HorizontalAlignment = ElementAlignment.Near;
        break;

      case RowAlignment.Right:
        layoutInfo.HorizontalAlignment = ElementAlignment.Far;
        break;

      case RowAlignment.Center:
        layoutInfo.HorizontalAlignment = ElementAlignment.Center;
        break;
    }
  }

  XUnit LeftBorderOffset
  {
    get
    {
      if (leftBorderOffset < 0)
      {
        if (table.Rows.Count > 0 && table.Columns.Count > 0)
        {
          Borders borders = mergedCells.GetEffectiveBorders(table[0, 0]);
          BordersRenderer bordersRenderer = new BordersRenderer(borders, gfx);
          leftBorderOffset = bordersRenderer.GetWidth(BorderType.Left);
        }
        else
          leftBorderOffset = 0;
      }
      return leftBorderOffset;
    }
  }
  private XUnit leftBorderOffset = -1;

  /// <summary>
  /// Calcs either the height of the header rows or the height of the uppermost top border.
  /// </summary>
  /// <returns></returns>
  XUnit CalcStartingHeight()
  {
    XUnit height = 0;
    if (lastHeaderRow >= 0)
    {
      height = (XUnit)bottomBorderMap[lastHeaderRow + 1];
      height += CalcMaxTopBorderWidth(0);
    }
    else
    {
      if (table.Rows.Count > startRow)
        height = CalcMaxTopBorderWidth(startRow);
    }

    return height;
  }


  void CalcLastHeaderColumn()
  {
    lastHeaderColumn = -1;
    foreach (Column clm in table.Columns)
    {
      if (clm.HeadingFormat)
        lastHeaderColumn = clm.Index;
      else break;
    }
    if (lastHeaderColumn >= 0)
      lastHeaderRow = CalcLastConnectedColumn(lastHeaderColumn);

    //Ignore heading format if all the table is heading:
    if (lastHeaderRow == table.Rows.Count - 1)
      lastHeaderRow = -1;

  }

  void CalcLastHeaderRow()
  {
    lastHeaderRow = -1;
    foreach (Row row in table.Rows)
    {
      if (row.HeadingFormat)
        lastHeaderRow = row.Index;
      else break;
    }
    if (lastHeaderRow >= 0)
      lastHeaderRow = CalcLastConnectedRow(lastHeaderRow);

    CheckHeadingRowsFormAnUnbrokenRun();

    //Ignore heading format if all the table is heading:
    if (lastHeaderRow == table.Rows.Count - 1)
      lastHeaderRow = -1;

  }

  /// <summary>
  /// Refuses a row marked as a heading which is not part of the heading, rather than discarding it.
  /// </summary>
  /// <remarks>
  /// A heading repeats at the top of every page the table continues onto, so it can only be the
  /// rows at the top of the table. A row marked anywhere else was silently ignored, which left a
  /// document that asked for a repeating heading looking exactly like one that never asked.
  /// Called before the whole-table heading is discarded, so a table that is entirely heading -
  /// which repeats nothing, having nothing to head - is not refused for it.
  /// </remarks>
  void CheckHeadingRowsFormAnUnbrokenRun()
  {
    for (int index = lastHeaderRow + 1; index < table.Rows.Count; ++index)
    {
      if (!table.Rows[index].HeadingFormat)
        continue;

      throw new InvalidOperationException(
        "Row " + index + " of the table is marked with HeadingFormat but cannot be part of the " +
        "heading. Heading rows repeat at the top of every page the table continues onto, so they " +
        "must form an unbroken run beginning at the first row. Mark every row from row 0 to row " +
        index + " as well, or clear HeadingFormat on row " + index + ".");
    }
  }

  void CreateConnectedRows()
  {
    connectedRowsMap = new SortedList<int, int>();
    foreach (Cell cell in mergedCells)
    {
      if (!connectedRowsMap.ContainsKey(cell.Row.Index))
      {
        int lastConnectedRow = CalcLastConnectedRow(cell.Row.Index);
        connectedRowsMap[cell.Row.Index] = lastConnectedRow;
      }
    }
  }

  void CreateConnectedColumns()
  {
    connectedColumnsMap = new SortedList<int, int>();
    foreach (Cell cell in mergedCells)
    {
      if (!connectedColumnsMap.ContainsKey(cell.Column.Index))
      {
        int lastConnectedColumn = CalcLastConnectedColumn(cell.Column.Index);
        connectedColumnsMap[cell.Column.Index] = lastConnectedColumn;
      }
    }
  }

  void CreateBottomBorderMap()
  {
    bottomBorderMap = new SortedList<int, XUnit>();
    bottomBorderMap.Add(0, XUnit.FromPoint(0));
    while (!bottomBorderMap.ContainsKey(table.Rows.Count))
    {
      CreateNextBottomBorderPosition();
    }
  }

  /// <summary>
  /// Calculates the top border width for the first row that is rendered or formatted.
  /// </summary>
  /// <param name="row">The row index.</param>
  XUnit CalcMaxTopBorderWidth(int row)
  {
    XUnit maxWidth = 0;
    if (table.Rows.Count > row)
    {
      int cellIdx = mergedCells.BinarySearch(table[row, 0], new CellComparer());
      Cell rowCell = mergedCells[cellIdx];
      while (cellIdx < mergedCells.Count)
      {
        rowCell = mergedCells[cellIdx];
        if (rowCell.Row.Index > row)
          break;

        if (!rowCell.IsNull("Borders"))
        {
          BordersRenderer bordersRenderer = new BordersRenderer(rowCell.Borders, gfx);
          XUnit width = 0;
          width = bordersRenderer.GetWidth(BorderType.Top);
          if (width > maxWidth)
            maxWidth = width;
        }
        ++cellIdx;
      }
    }
    return maxWidth;
  }

  /// <summary>
  /// Creates the next bottom border position.
  /// </summary>
  void CreateNextBottomBorderPosition()
  {
    int lastIdx = bottomBorderMap.Count - 1;
    int lastBorderRow = (int)bottomBorderMap.Keys[lastIdx];
    XUnit lastPos = (XUnit)bottomBorderMap.Values[lastIdx];
    Cell minMergedCell = GetMinMergedCell(lastBorderRow);
    FormattedCell minMergedFormattedCell = (FormattedCell)formattedCells[minMergedCell];
    XUnit maxBottomBorderPosition = lastPos + minMergedFormattedCell.InnerHeight;
    maxBottomBorderPosition += CalcBottomBorderWidth(minMergedCell);

    // Note: Caching the indices does speed up this function for large tables greatly.
    var minMergedCellRowIndex = minMergedCell.Row.Index;
    var minMergedCellMergeDown = minMergedCell.MergeDown;
    var mergedIndexPlusDown = minMergedCellRowIndex + minMergedCellMergeDown;
    foreach (Cell cell in mergedCells)
    {
      var rowIndex = cell.Row.Index;
      if (rowIndex > mergedIndexPlusDown)
        break;

      if (rowIndex + cell.MergeDown == mergedIndexPlusDown)
      {
        FormattedCell formattedCell = (FormattedCell)formattedCells[cell];
        XUnit topBorderPos = (XUnit)bottomBorderMap[rowIndex];
        XUnit bottomBorderPos = topBorderPos + formattedCell.InnerHeight;
        bottomBorderPos += CalcBottomBorderWidth(cell);
        if (bottomBorderPos > maxBottomBorderPosition)
          maxBottomBorderPosition = bottomBorderPos;
      }
    }
    bottomBorderMap.Add(mergedIndexPlusDown + 1, maxBottomBorderPosition);
  }

  /// <summary>
  /// Calculates bottom border width of a cell.
  /// </summary>
  /// <param name="cell">The cell the bottom border of the row that is probed.</param>
  /// <returns>The calculated border width.</returns>
  XUnit CalcBottomBorderWidth(Cell cell)
  {
    Borders borders = mergedCells.GetEffectiveBorders(cell);
    if (borders != null)
    {
      BordersRenderer bordersRenderer = new BordersRenderer(borders, gfx);
      return bordersRenderer.GetWidth(BorderType.Bottom);
    }
    return 0;
  }

  /// <summary>
  /// Gets the first cell in the given row that is merged down minimally.
  /// </summary>
  /// <param name="row">The row to prope.</param>
  /// <returns>The first cell with minimal vertical merge.</returns>
  Cell GetMinMergedCell(int row)
  {
    int minMerge = table.Rows.Count;
    Cell minCell = null;
    foreach (Cell cell in mergedCells)
    {
      var rowIndex = cell.Row.Index; // Note: Taking index only once speeds up large tables.
      if (rowIndex <= row && rowIndex + cell.MergeDown >= row)
      {
        if (rowIndex == row && cell.MergeDown == 0)
        {
          // Perfect match: non-merged cell in the desired row.
          minCell = cell;
          break;
        }
        else if (rowIndex + cell.MergeDown - row < minMerge)
        {
          minMerge = rowIndex + cell.MergeDown - row;
          minCell = cell;
        }
      }
      else if (rowIndex > row)
        break;
    }
    return minCell;
  }


  /// <summary>
  /// Calculates the last row that is connected with the given row.
  /// </summary>
  /// <param name="row">The row that is probed for downward connection.</param>
  /// <returns>The last row that is connected with the given row.</returns>
  /// <remarks>
  ///   A row can ask to be kept with more rows than follow it: a table built a row at a time
  ///   does not know, while it is being built, how many more rows there are going to be. There
  ///   is nothing below the last row to keep it with, so the answer stops there. The request is
  ///   cut down to the rows that follow before it is added to the index, so that a row asking to
  ///   be kept with <see cref="int.MaxValue"/> more is read as asking for all of them rather than
  ///   wrapping round to none.
  /// </remarks>
  int CalcLastConnectedRow(int row)
  {
    int lastConnectedRow = row;
    int lastRow = table.Rows.Count - 1;
    foreach (Cell cell in mergedCells)
    {
      var index = cell.Row.Index; // Note: Caching index here for speedup for large tables.
      if (index <= lastConnectedRow)
      {
        int downConnection = Math.Min(Math.Max(cell.Row.KeepWith, cell.MergeDown), lastRow - index);
        if (lastConnectedRow < index + downConnection)
          lastConnectedRow = index + downConnection;
      }
    }
    return lastConnectedRow;
  }

  /// <summary>
  /// Calculates the last column that is connected with the specified column.
  /// </summary>
  /// <param name="column">The column that is probed for downward connection.</param>
  /// <returns>The last column that is connected with the given column.</returns>
  /// <remarks>
  ///   As with <see cref="CalcLastConnectedRow"/>, a column can ask to be kept with more columns
  ///   than stand to the right of it, and there is nothing beyond the last one to keep it with.
  /// </remarks>
  int CalcLastConnectedColumn(int column)
  {
    int lastConnectedColumn = column;
    int lastColumn = table.Columns.Count - 1;
    foreach (Cell cell in mergedCells)
    {
      var index = cell.Column.Index;
      if (index <= lastConnectedColumn)
      {
        int rightConnection = Math.Min(Math.Max(cell.Column.KeepWith, cell.MergeRight), lastColumn - index);
        if (lastConnectedColumn < index + rightConnection)
          lastConnectedColumn = index + rightConnection;
      }
    }
    return lastConnectedColumn;
  }



  Table table;
  MergedCellList mergedCells;
  SortedList<Cell, FormattedCell> formattedCells;
  SortedList<int, XUnit> bottomBorderMap;
  SortedList<int, int> connectedRowsMap;
  SortedList<int, int> connectedColumnsMap;

  int lastHeaderRow;
  int lastHeaderColumn;
  int startRow;
  int currRow;
  int endRow = -1;

  bool doHorizontalBreak = false;
  XUnit startX;
  XUnit startY;

}
