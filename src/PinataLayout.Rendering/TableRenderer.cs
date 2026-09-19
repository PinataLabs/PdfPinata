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
    _table = documentObject;
  }

  internal TableRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
    :
    base(gfx, renderInfo, fieldInfos)
  {
    _table = (Table)this.renderInfo.DocumentObject;
  }

  internal override LayoutInfo InitialLayoutInfo
  {
    get
    {
      var layoutInfo = new LayoutInfo();
      layoutInfo.KeepTogether = _table.KeepTogether;
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
    var formatInfo = (TableFormatInfo)renderInfo.FormatInfo;
    _bottomBorderMap = formatInfo.bottomBorderMap;
    _connectedRowsMap = formatInfo.connectedRowsMap;
    _formattedCells = formatInfo.formattedCells;

    _currRow = formatInfo.startRow;
    _startRow = formatInfo.startRow;
    _endRow = formatInfo.endRow;

    _mergedCells = formatInfo.mergedCells;
    _lastHeaderRow = formatInfo.lastHeaderRow;
    _startX = renderInfo.LayoutInfo.ContentArea.X;
    _startY = renderInfo.LayoutInfo.ContentArea.Y;
  }

  /// <summary>
  ///
  /// </summary>
  void RenderHeaderRows()
  {
    if (_lastHeaderRow < 0)
      return;

    foreach (var cell in _mergedCells)
    {
      if (cell.Row.Index <= _lastHeaderRow)
        RenderCell(cell);
    }
  }

  void RenderCell(Cell cell)
  {
    var innerRect = GetInnerRect(CalcStartingHeight(), cell);

    using (Tagger.Enter(RowElementOf(cell)))
    using (Tagger.Container(Gfx, cell, IsHeaderCell(cell) ? PdfTag.TH : PdfTag.TD, out var element))
    {
      DescribeCell(cell, element);

      // Shading and borders are decoration and go out as artifacts; only what is in the cell is
      // content. A reader that announced every rule would be unusable on a bordered table.
      using (Tagger.Artifact(Gfx))
        RenderShading(cell, innerRect);

      RenderContent(cell, innerRect);

      using (Tagger.Artifact(Gfx))
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
  bool IsHeaderCell(Cell cell) => cell.Row.Index <= _lastHeaderRow;

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
    var roundedCorner = cell.RoundedCorner;

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
    var primaryBorder = (Border) cell.Borders.GetValue(primaryBorderType.ToString(), GV.ReadWrite);
    var secondaryBorder = (Border) cell.Borders.GetValue(secondaryBorderType.ToString(), GV.ReadWrite);

    var source = primaryBorder.Visible ? primaryBorder : secondaryBorder.Visible ? secondaryBorder : null;
    var target = primaryBorder.Visible ? secondaryBorder : secondaryBorder.Visible ? primaryBorder : null;

    if (source == null || target == null)
      return;

    target.Visible = source.Visible;
    target.Width = source.Width;
    target.Style = source.Style;
    target.Color = source.Color;
  }

  void RenderShading(Cell cell, Rectangle innerRect)
  {
    var shadeRenderer = new ShadingRenderer(Gfx, cell.Shading);
    shadeRenderer.Render(innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height, cell.RoundedCorner);
  }

  void RenderBorders(Cell cell, Rectangle innerRect)
  {
    var leftPos = innerRect.X;
    XUnit rightPos = leftPos + innerRect.Width;
    var topPos = innerRect.Y;
    XUnit bottomPos = innerRect.Y + innerRect.Height;
    var mergedBorders = _mergedCells.GetEffectiveBorders(cell);

    var bordersRenderer = new BordersRenderer(mergedBorders, Gfx);
    var bottomWidth = bordersRenderer.GetWidth(BorderType.Bottom);
    var leftWidth = bordersRenderer.GetWidth(BorderType.Left);
    var topWidth = bordersRenderer.GetWidth(BorderType.Top);
    var rightWidth = bordersRenderer.GetWidth(BorderType.Right);

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
    var bordersRenderer = new BordersRenderer(mergedBorders, Gfx);
    bordersRenderer.RenderDiagonally(BorderType.DiagonalDown, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height);
    bordersRenderer.RenderDiagonally(BorderType.DiagonalUp, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height);
  }

  void RenderContent(Cell cell, Rectangle innerRect)
  {
    var formattedCell = _formattedCells[cell];
    var renderInfos = formattedCell.GetRenderInfos();

    if (renderInfos == null)
      return;

    var verticalAlignment = cell.VerticalAlignment;
    var contentHeight = formattedCell.ContentHeight;
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
    var bordersRenderer = new BordersRenderer(_mergedCells.GetEffectiveBorders(cell), Gfx);
    var formattedCell = _formattedCells[cell];
    var width = formattedCell.InnerWidth;

    var y = _startY;
    if (cell.Row.Index > _lastHeaderRow)
      y += startingHeight;
    else
      y += CalcMaxTopBorderWidth(0);

    var upperBorderPos = _bottomBorderMap[cell.Row.Index];

    y += upperBorderPos;
    if (cell.Row.Index > _lastHeaderRow)
      y -= _bottomBorderMap[_startRow];

    var lowerBorderPos = _bottomBorderMap[cell.Row.Index + cell.MergeDown + 1];


    XUnit height = lowerBorderPos - upperBorderPos;
    height -= bordersRenderer.GetWidth(BorderType.Bottom);

    var x = _startX;
    for (var clmIdx = 0; clmIdx < cell.Column.Index; ++clmIdx)
    {
      x += _table.Columns[clmIdx].Width;
    }
    x += LeftBorderOffset;

    return new Rectangle(x, y, width, height);
  }

  internal override void Render()
  {
    InitRendering();

    Tagger.EndList();
    using (Tagger.Container(Gfx, _table, PdfTag.Table, out var element))
    {
      DescribeTable(element);
      RenderHeaderRows();

      if (_startRow < _table.Rows.Count)
      {
        var cellIdx = _mergedCells.BinarySearch(_table[_startRow, 0], new CellComparer());
        while (cellIdx < _mergedCells.Count)
        {
          var cell = _mergedCells[cellIdx];
          if (cell.Row.Index > _endRow)
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
    if (element == null || _table.IsNull("Summary"))
      return;

    element.Elements.SetString("/Summary", _table.Summary);
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

  void InitFormat(FormatInfo previousFormatInfo)
  {
    var prevTableFormatInfo = (TableFormatInfo)previousFormatInfo;
    var tblRenderInfo = new TableRenderInfo
    {
      table = _table
    };

    // Equalize the two borders, that are used to determine a rounded corner's border.
    // This way the innerWidth of the cell, which is got by the saved _formattedCells, is the same regardless of which corner relevant border is set.
    foreach (Row row in _table.Rows)
    foreach (Cell cell in row.Cells)
      EqualizeRoundedCornerBorders(cell);

    renderInfo = tblRenderInfo;

    if (prevTableFormatInfo != null)
    {
      _mergedCells = prevTableFormatInfo.mergedCells;
      _formattedCells = prevTableFormatInfo.formattedCells;
      _bottomBorderMap = prevTableFormatInfo.bottomBorderMap;
      _lastHeaderRow = prevTableFormatInfo.lastHeaderRow;
      _connectedRowsMap = prevTableFormatInfo.connectedRowsMap;
      _startRow = prevTableFormatInfo.endRow + 1;
    }
    else
    {
      _mergedCells = new MergedCellList(_table);
      FormatCells();
      CalcLastHeaderRow();
      CreateConnectedRows();
      CreateBottomBorderMap();
      if (_doHorizontalBreak)
      {
        CalcLastHeaderColumn();
        CreateConnectedColumns();
      }
      _startRow = _lastHeaderRow + 1;
    }
    ((TableFormatInfo)tblRenderInfo.FormatInfo).mergedCells = _mergedCells;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).formattedCells = _formattedCells;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).bottomBorderMap = _bottomBorderMap;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).connectedRowsMap = _connectedRowsMap;
    ((TableFormatInfo)tblRenderInfo.FormatInfo).lastHeaderRow = _lastHeaderRow;
  }

  void FormatCells()
  {
    _formattedCells = new SortedList<Cell, FormattedCell>(new CellComparer());
    foreach (var cell in _mergedCells)
    {
      var formattedCell = new FormattedCell(cell, DocumentRenderer, _mergedCells.GetEffectiveBorders(cell), fieldInfos, 0, 0);
      formattedCell.Format(Gfx);
      _formattedCells.Add(cell, formattedCell);
    }
  }

  /// <summary>
  /// Formats (measures) the table.
  /// </summary>
  /// <param name="area">The area on which to fit the table.</param>
  /// <param name="previousFormatInfo"></param>
  internal override void Format(Area area, FormatInfo previousFormatInfo)
  {
    if (DocumentRelations.GetParent(_table) is DocumentElements elements)
    {
      if (DocumentRelations.GetParent(elements) is Section section)
        _doHorizontalBreak = section.PageSetup.HorizontalPageBreak;
    }

    renderInfo = new TableRenderInfo();
    InitFormat(previousFormatInfo);

    // Don't take any Rows higher then MaxElementHeight
    var topHeight = CalcStartingHeight();
    XUnit offset;
    if (_startRow > _lastHeaderRow + 1 &&
        _startRow < _table.Rows.Count)
      offset = _bottomBorderMap[_startRow] - topHeight;
    else
      offset = -CalcMaxTopBorderWidth(0);

    var probeRow = _startRow;
    XUnit currentHeight = 0;
    XUnit startingHeight = 0;
    var isEmpty = false;

    while (probeRow < _table.Rows.Count)
    {
      var firstProbe = probeRow == _startRow;
      probeRow = _connectedRowsMap[probeRow];
      // Don't take any Rows higher then MaxElementHeight
      XUnit probeHeight = _bottomBorderMap[probeRow + 1] - offset;
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

      _currRow = probeRow;
      currentHeight = probeHeight;
      ++probeRow;
    }
    if (!isEmpty)
    {
      var formatInfo = (TableFormatInfo)renderInfo.FormatInfo;
      formatInfo.startRow = _startRow;
      formatInfo.isEnding = _currRow >= _table.Rows.Count - 1;
      formatInfo.endRow = _currRow;
    }
    FinishLayoutInfo(area, currentHeight, startingHeight);
  }

  void FinishLayoutInfo(Area area, XUnit currentHeight, XUnit startingHeight)
  {
    var layoutInfo = renderInfo.LayoutInfo;
    layoutInfo.StartingHeight = startingHeight;
    //REM: Trailing height would have to be calculated in case tables had a keep with next property.
    layoutInfo.TrailingHeight = 0;
    if (_currRow >= 0)
    {
      layoutInfo.ContentArea = new Rectangle(area.X, area.Y, 0, currentHeight);
      var width = LeftBorderOffset;
      foreach (Column clm in _table.Columns)
      {
        width += clm.Width;
      }
      layoutInfo.ContentArea.Width = width;
    }
    layoutInfo.MinWidth = layoutInfo.ContentArea.Width;

    if (!_table.Rows.IsNull("LeftIndent"))
      layoutInfo.Left = _table.Rows.LeftIndent.Point;

    else if (_table.Rows.Alignment == RowAlignment.Left)
    {
      if (_table.Columns.Count > 0) // Errors in Wiki syntax can lead to tables w/o columns ...
      {
        var leftOffset = LeftBorderOffset;
        leftOffset += _table.Columns[0].LeftPadding;
        layoutInfo.Left = -leftOffset;
      }
    }

    switch (_table.Rows.Alignment)
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
      if (field < 0)
      {
        if (_table.Rows.Count > 0 && _table.Columns.Count > 0)
        {
          var borders = _mergedCells.GetEffectiveBorders(_table[0, 0]);
          var bordersRenderer = new BordersRenderer(borders, Gfx);
          field = bordersRenderer.GetWidth(BorderType.Left);
        }
        else
          field = 0;
      }
      return field;
    }
  } = -1;

  /// <summary>
  /// Calcs either the height of the header rows or the height of the uppermost top border.
  /// </summary>
  /// <returns></returns>
  XUnit CalcStartingHeight()
  {
    XUnit height = 0;
    if (_lastHeaderRow >= 0)
    {
      height = _bottomBorderMap[_lastHeaderRow + 1];
      height += CalcMaxTopBorderWidth(0);
    }
    else
    {
      if (_table.Rows.Count > _startRow)
        height = CalcMaxTopBorderWidth(_startRow);
    }

    return height;
  }


  void CalcLastHeaderColumn()
  {
    _lastHeaderColumn = -1;
    foreach (Column clm in _table.Columns)
    {
      if (clm.HeadingFormat)
        _lastHeaderColumn = clm.Index;
      else break;
    }
    if (_lastHeaderColumn >= 0)
      _lastHeaderRow = CalcLastConnectedColumn(_lastHeaderColumn);

    // Ignore heading format if all the table is heading:
    if (_lastHeaderRow == _table.Rows.Count - 1)
      _lastHeaderRow = -1;

  }

  void CalcLastHeaderRow()
  {
    _lastHeaderRow = -1;
    foreach (Row row in _table.Rows)
    {
      if (row.HeadingFormat)
        _lastHeaderRow = row.Index;
      else break;
    }
    if (_lastHeaderRow >= 0)
      _lastHeaderRow = CalcLastConnectedRow(_lastHeaderRow);

    CheckHeadingRowsFormAnUnbrokenRun();

    //Ignore heading format if all the table is heading:
    if (_lastHeaderRow == _table.Rows.Count - 1)
      _lastHeaderRow = -1;

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
    for (var index = _lastHeaderRow + 1; index < _table.Rows.Count; ++index)
    {
      if (!_table.Rows[index].HeadingFormat)
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
    _connectedRowsMap = new SortedList<int, int>();
    foreach (var cell in _mergedCells)
    {
      if (!_connectedRowsMap.ContainsKey(cell.Row.Index))
      {
        var lastConnectedRow = CalcLastConnectedRow(cell.Row.Index);
        _connectedRowsMap[cell.Row.Index] = lastConnectedRow;
      }
    }
  }

  void CreateConnectedColumns()
  {
    _connectedColumnsMap = new SortedList<int, int>();
    foreach (var cell in _mergedCells)
    {
      if (!_connectedColumnsMap.ContainsKey(cell.Column.Index))
      {
        var lastConnectedColumn = CalcLastConnectedColumn(cell.Column.Index);
        _connectedColumnsMap[cell.Column.Index] = lastConnectedColumn;
      }
    }
  }

  void CreateBottomBorderMap()
  {
    _bottomBorderMap = new SortedList<int, XUnit>();
    _bottomBorderMap.Add(0, XUnit.FromPoint(0));
    while (!_bottomBorderMap.ContainsKey(_table.Rows.Count))
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
    if (_table.Rows.Count > row)
    {
      var cellIdx = _mergedCells.BinarySearch(_table[row, 0], new CellComparer());
      while (cellIdx < _mergedCells.Count)
      {
        var rowCell = _mergedCells[cellIdx];
        if (rowCell.Row.Index > row)
          break;

        if (!rowCell.IsNull("Borders"))
        {
          var bordersRenderer = new BordersRenderer(rowCell.Borders, Gfx);
          var width = bordersRenderer.GetWidth(BorderType.Top);
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
    var lastIdx = _bottomBorderMap.Count - 1;
    var lastBorderRow = _bottomBorderMap.Keys[lastIdx];
    var lastPos = _bottomBorderMap.Values[lastIdx];
    var minMergedCell = GetMinMergedCell(lastBorderRow);
    var minMergedFormattedCell = _formattedCells[minMergedCell];
    XUnit maxBottomBorderPosition = lastPos + minMergedFormattedCell.InnerHeight;
    maxBottomBorderPosition += CalcBottomBorderWidth(minMergedCell);

    // Note: Caching the indices does speed up this function for large tables greatly.
    var minMergedCellRowIndex = minMergedCell.Row.Index;
    var minMergedCellMergeDown = minMergedCell.MergeDown;
    var mergedIndexPlusDown = minMergedCellRowIndex + minMergedCellMergeDown;
    foreach (var cell in _mergedCells)
    {
      var rowIndex = cell.Row.Index;
      if (rowIndex > mergedIndexPlusDown)
        break;

      if (rowIndex + cell.MergeDown == mergedIndexPlusDown)
      {
        var formattedCell = _formattedCells[cell];
        var topBorderPos = _bottomBorderMap[rowIndex];
        XUnit bottomBorderPos = topBorderPos + formattedCell.InnerHeight;
        bottomBorderPos += CalcBottomBorderWidth(cell);
        if (bottomBorderPos > maxBottomBorderPosition)
          maxBottomBorderPosition = bottomBorderPos;
      }
    }
    _bottomBorderMap.Add(mergedIndexPlusDown + 1, maxBottomBorderPosition);
  }

  /// <summary>
  /// Calculates bottom border width of a cell.
  /// </summary>
  /// <param name="cell">The cell the bottom border of the row that is probed.</param>
  /// <returns>The calculated border width.</returns>
  XUnit CalcBottomBorderWidth(Cell cell)
  {
    var borders = _mergedCells.GetEffectiveBorders(cell);
    if (borders != null)
    {
      var bordersRenderer = new BordersRenderer(borders, Gfx);
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
    var minMerge = _table.Rows.Count;
    Cell minCell = null;
    foreach (var cell in _mergedCells)
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
    var lastConnectedRow = row;
    var lastRow = _table.Rows.Count - 1;
    foreach (var cell in _mergedCells)
    {
      var index = cell.Row.Index; // Note: Caching index here for speedup for large tables.
      if (index <= lastConnectedRow)
      {
        var downConnection = Math.Min(Math.Max(cell.Row.KeepWith, cell.MergeDown), lastRow - index);
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
    var lastConnectedColumn = column;
    var lastColumn = _table.Columns.Count - 1;
    foreach (var cell in _mergedCells)
    {
      var index = cell.Column.Index;
      if (index <= lastConnectedColumn)
      {
        var rightConnection = Math.Min(Math.Max(cell.Column.KeepWith, cell.MergeRight), lastColumn - index);
        if (lastConnectedColumn < index + rightConnection)
          lastConnectedColumn = index + rightConnection;
      }
    }
    return lastConnectedColumn;
  }


  private readonly Table _table;
  private MergedCellList _mergedCells;
  private SortedList<Cell, FormattedCell> _formattedCells;
  private SortedList<int, XUnit> _bottomBorderMap;
  private SortedList<int, int> _connectedRowsMap;
  private SortedList<int, int> _connectedColumnsMap;

  private int _lastHeaderRow;
  private int _lastHeaderColumn;
  private int _startRow;
  private int _currRow;
  private int _endRow = -1;

  private bool _doHorizontalBreak;
  private XUnit _startX;
  private XUnit _startY;

}
