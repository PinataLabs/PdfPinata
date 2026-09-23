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
          this.Add(cell);
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
    for (var index = this.Count - 1; index >= 0; --index)
    {

      var currentCell = this[index];
      if (currentCell.Column.Index <= cell.Column.Index && currentCell.Column.Index + currentCell.MergeRight >= cell.Column.Index)
      {
        if (currentCell.Row.Index <= cell.Row.Index && currentCell.Row.Index + currentCell.MergeDown >= cell.Row.Index)
          return true;
        if (currentCell.Row.Index + currentCell.MergeDown == cell.Row.Index - 1)
          return false;

      }
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
    var borders = cell.GetValue("Borders", GV.ReadOnly) as Borders;
    if (borders != null)
    {
      borders = borders.Clone();
      borders.parent = cell;
    }
    else
      borders = new Borders(cell.parent);

    var cellIdx = this.BinarySearch(cell, new CellComparer());
    if (!(cellIdx >= 0 && cellIdx < this.Count))
      throw new ArgumentException(@"cell is not a relevant cell", nameof(cell));

    if (cell.mergeRight > 0)
    {
      var rightBorderCell = cell.Table[cell.Row.Index, cell.MergedRightColumnIndex];
      if (rightBorderCell.borders != null && rightBorderCell.borders.right != null)
        borders.Right = rightBorderCell.borders.right.Clone();
      else
        borders.right = null;
    }

    if (cell.mergeDown > 0)
    {
      var bottomBorderCell = cell.Table[cell.MergedBottomRowIndex, cell.Column.Index];
      if (bottomBorderCell.borders != null && bottomBorderCell.borders.bottom != null)
        borders.Bottom = bottomBorderCell.borders.bottom.Clone();
      else
        borders.bottom = null;
    }

    var leftNeighbor = GetNeighbor(cellIdx, NeighborPosition.Left);
    var rightNeighbor = GetNeighbor(cellIdx, NeighborPosition.Right);
    var topNeighbor = GetNeighbor(cellIdx, NeighborPosition.Top);
    var bottomNeighbor = GetNeighbor(cellIdx, NeighborPosition.Bottom);
    if (leftNeighbor != null)
    {
      var nbrBrdrs = leftNeighbor.GetValue("Borders", GV.ReadOnly) as Borders;
      if (nbrBrdrs != null && GetEffectiveBorderWidth(nbrBrdrs, BorderType.Right) >= GetEffectiveBorderWidth(borders, BorderType.Left))
        borders.SetValue("Left", GetBorderFromBorders(nbrBrdrs, BorderType.Right));
    }
    if (rightNeighbor != null)
    {
      var nbrBrdrs = rightNeighbor.GetValue("Borders", GV.ReadOnly) as Borders;
      if (nbrBrdrs != null && GetEffectiveBorderWidth(nbrBrdrs, BorderType.Left) > GetEffectiveBorderWidth(borders, BorderType.Right))
        borders.SetValue("Right", GetBorderFromBorders(nbrBrdrs, BorderType.Left));
    }
    if (topNeighbor != null)
    {
      var nbrBrdrs = topNeighbor.GetValue("Borders", GV.ReadOnly) as Borders;
      if (nbrBrdrs != null && GetEffectiveBorderWidth(nbrBrdrs, BorderType.Bottom) >= GetEffectiveBorderWidth(borders, BorderType.Top))
        borders.SetValue("Top", GetBorderFromBorders(nbrBrdrs, BorderType.Bottom));
    }
    if (bottomNeighbor != null)
    {
      var nbrBrdrs = bottomNeighbor.GetValue("Borders", GV.ReadOnly) as Borders;
      if (nbrBrdrs != null && GetEffectiveBorderWidth(nbrBrdrs, BorderType.Top) > GetEffectiveBorderWidth(borders, BorderType.Bottom))
        borders.SetValue("Bottom", GetBorderFromBorders(nbrBrdrs, BorderType.Top));
    }
    return borders;
  }

  /// <summary>
  /// Gets the cell that covers the given cell by merging. Usually the cell itself if not merged.
  /// </summary>
  public Cell GetCoveringCell(Cell cell)
  {
    var cellIdx = this.BinarySearch(cell, new CellComparer());
    if (cellIdx >= 0 && cellIdx < this.Count)
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

    returnBorder = new Border();
    returnBorder.style = borders.style;
    returnBorder.width = borders.width;
    returnBorder.color = borders.color;
    returnBorder.visible = borders.visible;
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

    if (visible != null || style != null || width != null || color != null)
    {
      if (visible != null && !(bool)visible)
        return 0;
      if (width != null)
        return (Unit)width;

      return 0.5;
    }
    return 0;
  }

  /// <summary>
  /// Gets the specified cell's uppermost neighbor at the specified position.
  /// </summary>
  private Cell GetNeighbor(int cellIdx, NeighborPosition position)
  {
    var cell = this[cellIdx];
    if (cell.Column.Index == 0 && position == NeighborPosition.Left ||
        cell.Row.Index == 0 && position == NeighborPosition.Top ||
        cell.MergedBottomRowIndex == cell.Table.Rows.Count - 1 && position == NeighborPosition.Bottom ||
        cell.MergedRightColumnIndex == cell.Table.Columns.Count - 1 && position == NeighborPosition.Right)
        return null;

    switch (position)
    {
      case NeighborPosition.Top:
      case NeighborPosition.Left:
        for (var index = cellIdx - 1; index >= 0; --index)
        {
          var currCell = this[index];
          if (IsNeighbor(cell, currCell, position))
            return currCell;
        }
        break;

      case NeighborPosition.Right:
        if (cellIdx + 1 < this.Count)
        {
          var cell2 = this[cellIdx + 1];
          if (cell2.Row.Index == cell.Row.Index)
            return cell2;
        }
        for (var index = cellIdx - 1; index >= 0; --index)
        {
          var currCell = this[index];
          if (IsNeighbor(cell, currCell, position))
            return currCell;
        }
        break;

      case NeighborPosition.Bottom:
        for (var index = cellIdx + 1; index < this.Count; ++index)
        {
          var currCell = this[index];
          if (IsNeighbor(cell, currCell, position))
            return currCell;
        }
        break;
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
