#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
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
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel.Visitors;

/// <summary>
/// Represents a merged list of cells of a table.
/// </summary>
public class MergedCellList : List<Cell>
{
  /// <summary>
  /// Enumeration of neighbor positions of cells in a table.
  /// </summary>
  private enum NeighborPosition
  {
    Top,
    Left,
    Right,
    Bottom
  }

  /// <summary>
  /// Initializes a new instance of the MergedCellList class.
  /// </summary>
  public MergedCellList(Table table)
  {
    Init(table);
  }

  /// <summary>
  /// Initializes this instance from a table.
  /// </summary>
  private void Init(Table table)
  {
    for (var rwIdx = 0; rwIdx < table.Rows.Count; ++rwIdx)
    {
      for (var clmIdx = 0; clmIdx < table.Columns.Count; ++clmIdx)
      {
        var cell = table[rwIdx, clmIdx];
        if (!IsAlreadyCovered(cell))
          Add(cell);
      }
    }
  }

  /// <summary>
  /// Returns whether the given cell is already covered by a preceding cell in this instance.
  /// </summary>
  /// <remarks>
  /// Help function for Init().
  /// </remarks>
  private bool IsAlreadyCovered(Cell cell)
  {
    for (var index = Count - 1; index >= 0; --index)
    {

      var currentCell = this[index];
      if (currentCell.Column.Index > cell.Column.Index || currentCell.Column.Index + currentCell.MergeRight < cell.Column.Index)
        continue;

      if (currentCell.Row.Index <= cell.Row.Index && currentCell.Row.Index + currentCell.MergeDown >= cell.Row.Index)
        return true;
      else if (currentCell.Row.Index + currentCell.MergeDown == cell.Row.Index - 1)
        return false;
    }
    return false;
  }

  /// <summary>
  /// Gets the cell at the specified position.
  /// </summary>
  public new Cell this[int index] => base[index];

  /// <summary>
  /// Gets a borders object that should be used for rendering.
  /// </summary>
  /// <exception cref="System.ArgumentException">
  ///   Thrown when the cell is not in this list.
  ///   This situation occurs if the given cell is merged "away" by a previous one.
  /// </exception>
  public Borders GetEffectiveBorders(Cell cell)
  {
    var borders = OwnBordersCopy(cell);

    var cellIdx = BinarySearch(cell, new CellComparer());
    if (!(cellIdx >= 0 && cellIdx < Count))
      throw new ArgumentException(@"cell is not a relevant cell", nameof(cell));

    TakeOuterBordersOfMergedCells(cell, borders);

    var leftNeighbor = GetNeighbor(cellIdx, NeighborPosition.Left);
    var rightNeighbor = GetNeighbor(cellIdx, NeighborPosition.Right);
    var topNeighbor = GetNeighbor(cellIdx, NeighborPosition.Top);
    var bottomNeighbor = GetNeighbor(cellIdx, NeighborPosition.Bottom);

    // The heavier of two facing borders wins. On a tie the neighbour to the left or above wins,
    // and the one to the right or below does not.
    TakeNeighborBorderIfHeavier(borders, BorderType.Left, leftNeighbor, BorderType.Right, neighborWinsTie: true);
    TakeNeighborBorderIfHeavier(borders, BorderType.Right, rightNeighbor, BorderType.Left, neighborWinsTie: false);
    TakeNeighborBorderIfHeavier(borders, BorderType.Top, topNeighbor, BorderType.Bottom, neighborWinsTie: true);
    TakeNeighborBorderIfHeavier(borders, BorderType.Bottom, bottomNeighbor, BorderType.Top, neighborWinsTie: false);
    return borders;
  }

  /// <summary>
  /// A copy of the cell's own borders that the caller may change, or new borders when it has none.
  /// </summary>
  private static Borders OwnBordersCopy(Cell cell)
  {
    if (cell.GetValue("Borders", GV.ReadOnly) is not Borders borders)
      return new Borders(cell.parent);

    borders = borders.Clone();
    borders.parent = cell;
    return borders;
  }

  /// <summary>
  /// A cell merged right or down is drawn to the far side of the last cell it covers, so that
  /// cell's right or bottom border is the one it takes - or none, where that cell has none.
  /// </summary>
  private static void TakeOuterBordersOfMergedCells(Cell cell, Borders borders)
  {
    if (cell.mergeRight > 0)
    {
      var rightBorderCell = cell.Table[cell.Row.Index, cell.MergedRightColumnIndex];
      if (rightBorderCell.borders is { right: not null })
        borders.Right = rightBorderCell.borders.right.Clone();
      else
        borders.right = null;
    }

    if (cell.mergeDown > 0)
    {
      var bottomBorderCell = cell.Table[cell.MergedBottomRowIndex, cell.Column.Index];
      if (bottomBorderCell.borders is { bottom: not null })
        borders.Bottom = bottomBorderCell.borders.bottom.Clone();
      else
        borders.bottom = null;
    }
  }

  /// <summary>
  /// Replaces the border on one side of <paramref name="borders"/> with the facing border of
  /// <paramref name="neighbor"/>, when the neighbour has borders and its facing one is the heavier -
  /// or as heavy, where <paramref name="neighborWinsTie"/> says so.
  /// </summary>
  private static void TakeNeighborBorderIfHeavier(Borders borders, BorderType side, Cell neighbor, BorderType facingSide, bool neighborWinsTie)
  {
    if (neighbor?.GetValue("Borders", GV.ReadOnly) is not Borders neighborBorders)
      return;

    var neighborWidth = GetEffectiveBorderWidth(neighborBorders, facingSide);
    var ownWidth = GetEffectiveBorderWidth(borders, side);
    var isHeavier = neighborWinsTie ? neighborWidth >= ownWidth : neighborWidth > ownWidth;
    if (isHeavier)
      borders.SetValue(side.ToString(), GetBorderFromBorders(neighborBorders, facingSide));
  }

  /// <summary>
  /// Gets the cell that covers the given cell by merging. Usually the cell itself if not merged.
  /// </summary>
  public Cell GetCoveringCell(Cell cell)
  {
    var cellIdx = BinarySearch(cell, new CellComparer());
    if (cellIdx >= 0 && cellIdx < Count)
      return this[cellIdx];
    //Binary Search returns the complement of the next value, therefore, "~cellIdx - 1" is the previous cell.
    cellIdx = ~cellIdx - 1;
    for (var index = cellIdx; index >= 0; --index)
    {
      var currCell = this[index];
      if (currCell.Column.Index <= cell.Column.Index &&
          currCell.Column.Index + currCell.MergeRight >= cell.Column.Index &&
          currCell.Row.Index <= cell.Row.Index &&
          currCell.Row.Index + currCell.MergeDown >= cell.Row.Index)
          return currCell;
    }
    return null;
  }

  /// <summary>
  /// Returns a copy of the border of the given borders-object of the specified type (top, bottom, ...).
  /// If that border doesn't exist, it returns a new border object that inherits all properties from the given borders object.
  /// The copy matters: the caller hands the result to a throwaway Borders collection, which takes
  /// ownership of whatever it is given, and the neighbour's own border must not be carried off.
  /// </summary>
  private static Border GetBorderFromBorders(Borders borders, BorderType type)
  {
    var returnBorder = borders.GetValue(type.ToString(), GV.ReadOnly) as Border;
    if (returnBorder != null)
      return returnBorder.Clone();

    returnBorder = new Border
    {
      style = borders.style,
      width = borders.width,
      color = borders.color,
      visible = borders.visible
    };
    return returnBorder;
  }

  /// <summary>
  /// Returns the width of the border at the specified position.
  /// </summary>
  private static Unit GetEffectiveBorderWidth(Borders borders, BorderType type)
  {
    if (borders == null)
      return 0;

    var border = borders.GetValue(type.ToString(), GV.GetNull) as Border;

    DocumentObject relevantDocObj = border;
    if (relevantDocObj == null || relevantDocObj.IsNull("Width"))
      relevantDocObj = borders;

    var visible = relevantDocObj.GetValue("visible", GV.GetNull);
    var style = relevantDocObj.GetValue("style", GV.GetNull);
    var width = relevantDocObj.GetValue("width", GV.GetNull);
    var color = relevantDocObj.GetValue("color", GV.GetNull);

    if (visible == null && style == null && width == null && color == null)
      return 0;

    if (visible != null && !(bool)visible)
      return 0;
    if (width != null)
      return (Unit)width;

    return 0.5;
  }

  /// <summary>
  /// Gets the specified cell's uppermost neighbor at the specified position.
  /// </summary>
  private Cell GetNeighbor(int cellIdx, NeighborPosition position)
  {
    var cell = this[cellIdx];
    if (IsOnTableEdge(cell, position))
      return null;

    return position switch
    {
      NeighborPosition.Top or NeighborPosition.Left => FindNeighborBefore(cell, cellIdx, position),
      // The next cell in the list is the one to the right, if it is on the same row. Otherwise
      // the neighbour is a cell merged down from a row above.
      NeighborPosition.Right => NextCellInSameRow(cell, cellIdx) ?? FindNeighborBefore(cell, cellIdx, position),
      NeighborPosition.Bottom => FindNeighborAfter(cell, cellIdx, position),
      _ => null
    };
  }

  /// <summary>
  /// Returns whether the cell has no neighbour at the specified position because it lies along
  /// that edge of the table.
  /// </summary>
  private static bool IsOnTableEdge(Cell cell, NeighborPosition position)
  {
    return position switch
    {
      NeighborPosition.Left => cell.Column.Index == 0,
      NeighborPosition.Top => cell.Row.Index == 0,
      NeighborPosition.Bottom => cell.MergedBottomRowIndex == cell.Table.Rows.Count - 1,
      NeighborPosition.Right => cell.MergedRightColumnIndex == cell.Table.Columns.Count - 1,
      _ => false
    };
  }

  /// <summary>
  /// Returns the cell after the given one in this list, if it is on the same row.
  /// </summary>
  private Cell NextCellInSameRow(Cell cell, int cellIdx)
  {
    if (cellIdx + 1 >= Count)
      return null;

    var next = this[cellIdx + 1];
    return next.Row.Index == cell.Row.Index ? next : null;
  }

  /// <summary>
  /// Searches the cells before the given one in this list, nearest first, for its neighbour at the
  /// specified position.
  /// </summary>
  private Cell FindNeighborBefore(Cell cell, int cellIdx, NeighborPosition position)
  {
    for (var index = cellIdx - 1; index >= 0; --index)
    {
      var currCell = this[index];
      if (IsNeighbor(cell, currCell, position))
        return currCell;
    }
    return null;
  }

  /// <summary>
  /// Searches the cells after the given one in this list, nearest first, for its neighbour at the
  /// specified position.
  /// </summary>
  private Cell FindNeighborAfter(Cell cell, int cellIdx, NeighborPosition position)
  {
    for (var index = cellIdx + 1; index < Count; ++index)
    {
      var currCell = this[index];
      if (IsNeighbor(cell, currCell, position))
        return currCell;
    }
    return null;
  }

  /// <summary>
  /// Returns whether cell2 is a neighbor of cell1 at the specified position.
  /// </summary>
  private static bool IsNeighbor(Cell cell1, Cell cell2, NeighborPosition position)
  {
    var isNeighbor = false;
    switch (position)
    {
      case NeighborPosition.Bottom:
        var bottomRowIdx = cell1.Row.Index + cell1.MergeDown + 1;
        isNeighbor = cell2.Row.Index == bottomRowIdx &&
                     cell2.Column.Index <= cell1.Column.Index &&
                     cell2.Column.Index + cell2.MergeRight >= cell1.Column.Index;
        break;

      case NeighborPosition.Left:
        var leftClmIdx = cell1.Column.Index - 1;
        isNeighbor = cell2.Row.Index <= cell1.Row.Index &&
                     cell2.Row.Index + cell2.MergeDown >= cell1.Row.Index &&
                     cell2.Column.Index + cell2.MergeRight == leftClmIdx;
        break;

      case NeighborPosition.Right:
        var rightClmIdx = cell1.Column.Index + cell1.MergeRight + 1;
        isNeighbor = cell2.Row.Index <= cell1.Row.Index &&
                     cell2.Row.Index + cell2.MergeDown >= cell1.Row.Index &&
                     cell2.Column.Index == rightClmIdx;
        break;

      case NeighborPosition.Top:
        var topRowIdx = cell1.Row.Index - 1;
        isNeighbor = cell2.Row.Index + cell2.MergeDown == topRowIdx &&
                     cell2.Column.Index + cell2.MergeRight >= cell1.Column.Index &&
                     cell2.Column.Index <= cell1.Column.Index;
        break;
    }
    return isNeighbor;
  }
}
