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

namespace PinataLayout.DocumentObjectModel.Tables;

/// <summary>
/// Represents a column of a table.
/// </summary>
[SuppressSerializeCheck("index is this column's position in Columns, recomputed from that " +
    "collection on read rather than stored - there is nothing for Serialize to write")]
public partial class Column : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the Column class.
  /// </summary>
  public Column()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Column class with the specified parent.
  /// </summary>
  internal Column(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Column Clone()
  {
    return (Column)DeepCopy();
  }

  #endregion

  #region Properties
  /// <summary>
  /// Gets the table the Column belongs to.
  /// </summary>
  public Table Table
  {
    get
    {
      if (table != null)
        return table;

      var clms = Parent as Columns;
      if (clms != null)
        table = clms.Parent as Table;
      return table;
    }
  }
  private Table table;

  /// <summary>
  /// Gets the index of the column. First column has index 0.
  /// </summary>
  public int Index
  {
    get
    {
      if (index.HasValue)
        return index.Value;

      var clms = (Columns)Parent;
      // One for all and all for one.
      for (var i = 0; i < clms.Count; ++i)
      {
        clms[i].index = i;
      }
      return index ?? 0;
    }
  }
  [DV]
  internal int? index;

  /// <summary>
  /// Gets a cell by its row index. The first cell has index 0.
  /// </summary>
  public Cell this[int rowIndex] => Table.Rows[rowIndex][index ?? 0];

  /// <summary>
  /// Sets or gets the default style name for all cells of the column.
  /// </summary>
  public string Style
  {
    get => style ?? "";
    set => style = value;
  }
  [DV]
  internal string style;

  /// <summary>
  /// Gets the default ParagraphFormat for all cells of the column.
  /// </summary>
  public ParagraphFormat Format
  {
    get
    {
      if (format == null)
        format = new ParagraphFormat(this);

      return format;
    }
    set
    {
      SetParent(value);
      format = value;
    }
  }
  [DV]
  internal ParagraphFormat format;

  /// <summary>
  /// Gets or sets the width of a column.
  /// </summary>
  public Unit Width
  {
    get => width;
    set => width = value;
  }
  [DV]
  internal Unit width = Unit.NullValue;

  /// <summary>
  /// Gets or sets the default left padding for all cells of the column.
  /// </summary>
  public Unit LeftPadding
  {
    get => leftPadding;
    set => leftPadding = value;
  }
  [DV]
  internal Unit leftPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the default right padding for all cells of the column.
  /// </summary>
  public Unit RightPadding
  {
    get => rightPadding;
    set => rightPadding = value;
  }
  [DV]
  internal Unit rightPadding = Unit.NullValue;

  /// <summary>
  /// Gets the default Borders object for all cells of the column.
  /// </summary>
  public Borders Borders
  {
    get
    {
      if (borders == null)
        borders = new Borders(this);

      return borders;
    }
    set
    {
      SetParent(value);
      borders = value;
    }
  }
  [DV]
  internal Borders borders;

  /// <summary>
  /// Gets or sets the number of columns that should be kept together with
  /// current column in case of a page break.
  /// </summary>
  public int KeepWith
  {
    get => keepWith ?? 0;
    set => keepWith = value;
  }
  [DV]
  internal int? keepWith;

  /// <summary>
  /// Gets or sets a value which define whether the column is a header.
  /// </summary>
  public bool HeadingFormat
  {
    get => headingFormat ?? false;
    set => headingFormat = value;
  }
  [DV]
  internal bool? headingFormat;

  /// <summary>
  /// Gets the default Shading object for all cells of the column.
  /// </summary>
  public Shading Shading
  {
    get
    {
      if (shading == null)
        shading = new Shading(this);

      return shading;
    }
    set
    {
      SetParent(value);
      shading = value;
    }
  }
  [DV]
  internal Shading shading;

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
  /// Converts Column into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteComment(comment ?? "");
    serializer.WriteLine("\\column");

    var pos = serializer.BeginAttributes();

    if ((style ?? "") != string.Empty)
      serializer.WriteSimpleAttribute("Style", Style);

    if (!IsNull("Format"))
      format.Serialize(serializer, "Format", null);

    if (headingFormat != null)
      serializer.WriteSimpleAttribute("HeadingFormat", HeadingFormat);

    if (!leftPadding.IsNull)
      serializer.WriteSimpleAttribute("LeftPadding", LeftPadding);

    if (!rightPadding.IsNull)
      serializer.WriteSimpleAttribute("RightPadding", RightPadding);

    if (!width.IsNull)
      serializer.WriteSimpleAttribute("Width", Width);

    if (keepWith.HasValue)
      serializer.WriteSimpleAttribute("KeepWith", KeepWith);

    if (!IsNull("Borders"))
      borders.Serialize(serializer, null);

    if (!IsNull("Shading"))
      shading.Serialize(serializer);

    serializer.EndAttributes(pos);

    // columns has no content
  }

  #endregion
}
