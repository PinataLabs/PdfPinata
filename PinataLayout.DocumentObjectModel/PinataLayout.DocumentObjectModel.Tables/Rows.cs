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
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Visitors;

namespace PinataLayout.DocumentObjectModel.Tables;

/// <summary>
/// Represents the collection of all rows of a table.
/// </summary>
public partial class Rows : DocumentObjectCollection, IVisitable
{
  /// <summary>
  /// Initializes a new instance of the Rows class.
  /// </summary>
  public Rows()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Rows class with the specified parent.
  /// </summary>
  internal Rows(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Rows Clone()
  {
    return (Rows)base.DeepCopy();
  }

  /// <summary>
  /// Adds a new row to the rows collection. Allowed only if at least one column exists.
  /// </summary>
  public Row AddRow()
  {
    if (Table.Columns.Count == 0)
      throw new InvalidOperationException("Cannot add row, because no columns exists.");

    Row row = new Row();
    Add(row);
    return row;
  }

  /// <summary>
  /// Adds a row to the collection and gives it a cell for every column of the table.
  /// </summary>
  public override void Add(DocumentObject value)
  {
    base.Add(value);
    GiveItACellPerColumn(value as Row);
  }

  /// <summary>
  /// Inserts a row into the collection and gives it a cell for every column of the table.
  /// </summary>
  public override void InsertObject(int index, DocumentObject val)
  {
    base.InsertObject(index, val);
    GiveItACellPerColumn(val as Row);
  }

  /// <summary>
  ///   A row of a table has as many cells as the table has columns. The cells used to be made
  ///   only as they were asked for by index, so a row reported however many of them something
  ///   had happened to touch: enumerating one reached no further than that, and a loop over
  ///   every cell of every row quietly passed some of them by.
  /// </summary>
  /// <remarks>
  ///   Done as the row is added, which is the first moment it knows its table. Columns cannot
  ///   be added to a table that already has rows, so the count cannot change afterwards.
  ///   Asking for the last cell makes the ones before it.
  /// </remarks>
  void GiveItACellPerColumn(Row row)
  {
    int columns = Table?.Columns.Count ?? 0;
    if (row != null && columns > 0)
      _ = row.Cells[columns - 1];
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets the table the rows collection belongs to.
  /// </summary>
  public Table Table => parent as Table;

  /// <summary>
  /// Gets a row by its index.
  /// </summary>
  public new Row this[int index] => base[index] as Row;

  /// <summary>
  /// Gets or sets the row alignment of the table.
  /// </summary>
  public RowAlignment Alignment
  {
    get => alignment ?? default;
    set => alignment = EnumGuard.Checked(value);
  }
  [DV]
  internal RowAlignment? alignment;

  /// <summary>
  /// Gets or sets the left indent of the table. If row alignment is not Left,
  /// the value is ignored.
  /// </summary>
  public Unit LeftIndent
  {
    get => leftIndent;
    set => leftIndent = value;
  }
  [DV]
  internal Unit leftIndent = Unit.NullValue;

  /// <summary>
  /// Gets or sets the default vertical alignment for all rows.
  /// </summary>
  public VerticalAlignment VerticalAlignment
  {
    get => verticalAlignment ?? default;
    set => verticalAlignment = EnumGuard.Checked(value);
  }
  [DV]
  internal VerticalAlignment? verticalAlignment;

  /// <summary>
  /// Gets or sets the height of the rows.
  /// </summary>
  public Unit Height
  {
    get => height;
    set => height = value;
  }
  [DV]
  internal Unit height = Unit.NullValue;

  /// <summary>
  /// Gets or sets the rule which is used to determine the height of the rows.
  /// </summary>
  public RowHeightRule HeightRule
  {
    get => heightRule ?? default;
    set => heightRule = EnumGuard.Checked(value);
  }
  [DV]
  internal RowHeightRule? heightRule;

  /// <summary>
  /// Gets or sets a comment associated with this object.
  /// </summary>
  public string Comment
  {
    get => comment ?? "";
    set => comment = value;
  }
  [DV]
  internal string comment;
  #endregion

  #region Internal
  /// <summary>
  /// Converts Rows into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteComment((comment ?? ""));
    serializer.WriteLine("\\rows");

    int pos = serializer.BeginAttributes();

    if (alignment != null)
      serializer.WriteSimpleAttribute("Alignment", Alignment);

    if (!height.IsNull)
      serializer.WriteSimpleAttribute("Height", Height);

    if (heightRule != null)
      serializer.WriteSimpleAttribute("HeightRule", HeightRule);

    if (!leftIndent.IsNull)
      serializer.WriteSimpleAttribute("LeftIndent", LeftIndent);

    if (verticalAlignment != null)
      serializer.WriteSimpleAttribute("VerticalAlignment", VerticalAlignment);

    serializer.EndAttributes(pos);

    serializer.BeginContent();
    int rows = Count;
    if (rows > 0)
    {
      for (int row = 0; row < rows; row++)
        this[row].Serialize(serializer);
    }
    else
      serializer.WriteComment("Invalid - no rows defined. Table will not render.");
    serializer.EndContent();
  }

  /// <summary>
  /// Allows the visitor object to visit the document object and it's child objects.
  /// </summary>
  void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
  {
    visitor.VisitRows(this);

    foreach (Row row in this)
      ((IVisitable)row).AcceptVisitor(visitor, visitChildren);
  }

  #endregion
}
