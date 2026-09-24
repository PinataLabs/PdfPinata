#region Copyright
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
/// Represents a table in a document.
/// </summary>
public partial class Table : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Table class.
    /// </summary>
    public Table()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Table class with the specified parent.
    /// </summary>
    internal Table(DocumentObject parent) : base(parent)
    {
    }

    #region Methods

    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Table Clone()
    {
        return (Table)DeepCopy();
    }

    /// <summary>
    /// Adds a new column to the table. Allowed only before any row was added.
    /// </summary>
    public Column AddColumn()
    {
        return Columns.AddColumn();
    }

    /// <summary>
    /// Adds a new column of the specified width to the table. Allowed only before any row was added.
    /// </summary>
    public Column AddColumn(Unit width)
    {
        var clm = Columns.AddColumn();
        clm.Width = width;
        return clm;
    }

    /// <summary>
    /// Adds a new row to the table. Allowed only if at least one column was added.
    /// </summary>
    public Row AddRow()
    {
        return rows.AddRow();
    }

    /// <summary>
    /// Returns true if no cell exists in the table.
    /// </summary>
    public bool IsEmpty => Rows.Count == 0 || Columns.Count == 0;

    /// <summary>
    /// Sets a shading of the specified Color in the specified Tablerange.
    /// </summary>
    // ReSharper disable once ParameterHidesMember
    public void SetShading(int clm, int row, int clms, int rows, Color clr)
    {
        AssertCellRange(clm, row, clms, rows);

        var maxRow = row + rows - 1;
        var maxClm = clm + clms - 1;
        for (var r = row; r <= maxRow; r++)
        {
            var currentRow = this.rows[r];
            for (var c = clm; c <= maxClm; c++)
                currentRow[c].Shading.Color = clr;
        }
    }

    /// <summary>
    /// Throws unless the range of cells lies wholly within the table.
    /// </summary>
    // ReSharper disable once ParameterHidesMember
    private void AssertCellRange(int clm, int row, int clms, int rows)
    {
        // Through the properties rather than the fields: the fields are null until something has
        // asked for the collection, so a table that has had no row or column added to it threw
        // NullReferenceException here and never reached the range checks below.
        var rowsCount = Rows.Count;
        var clmsCount = Columns.Count;

        if (row < 0 || row >= rowsCount)
            throw new ArgumentOutOfRangeException(nameof(row), row, "Invalid row index.");

        if (clm < 0 || clm >= clmsCount)
            throw new ArgumentOutOfRangeException(nameof(clm), clm, "Invalid column index.");

        if (rows <= 0 || row + rows > rowsCount)
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "Invalid row count.");

        if (clms <= 0 || clm + clms > clmsCount)
            throw new ArgumentOutOfRangeException(nameof(clms), clms, "Invalid column count.");
    }

    /// <summary>
    /// Sets the borders surrounding the specified range of the table.
    /// </summary>
    /// <remarks>
    ///   An interior edge lies between two cells and both of them describe it: the renderer draws
    ///   the thicker of the two facing borders, so the edge is only as this call asks for it to be
    ///   once both sides say so. Writing one side alone leaves the other inheriting whatever the
    ///   table, row or column says, which is how a cleared interior border used to survive.
    /// </remarks>
    public void SetEdge(int clm, int row, int clms, int rowCount,
        Edge edge, BorderStyle borderStyle, Unit width, Color clr)
    {
        var request = new EdgeRequest(row, clm, row + rowCount - 1, clm + clms - 1, edge, borderStyle, width, clr);

        for (var r = row; r <= request.LastRow; r++)
        {
            var currentRow = rows[r];
            for (var c = clm; c <= request.LastColumn; c++)
            {
                var currentCell = currentRow[c];
                SetOuterEdges(currentCell, r, c, request);
                SetInteriorEdges(currentRow, currentCell, r, c, request);
                SetDiagonals(currentCell, request);
            }
        }
    }

    /// <summary>
    /// Sets the borders of a cell that lie on the outline of the range.
    /// </summary>
    private static void SetOuterEdges(Cell cell, int r, int c, EdgeRequest request)
    {
        if (request.Includes(Edge.Top) && r == request.FirstRow)
            request.Apply(cell.Borders.Top);

        if (request.Includes(Edge.Left) && c == request.FirstColumn)
            request.Apply(cell.Borders.Left);

        if (request.Includes(Edge.Bottom) && r == request.LastRow)
            request.Apply(cell.Borders.Bottom);

        if (request.Includes(Edge.Right) && c == request.LastColumn)
            request.Apply(cell.Borders.Right);
    }

    /// <summary>
    /// Sets the borders a cell shares with the cell below it and the cell to its right, where that
    /// cell is inside the range too - both sides of each, so that the edge is drawn as asked.
    /// </summary>
    private void SetInteriorEdges(Row currentRow, Cell cell, int r, int c, EdgeRequest request)
    {
        // The row below is inside the range because this edge is an interior one.
        if (request.Includes(Edge.Horizontal) && r < request.LastRow)
        {
            request.Apply(cell.Borders.Bottom);
            request.Apply(rows[r + 1][c].Borders.Top);
        }

        if (request.Includes(Edge.Vertical) && c < request.LastColumn)
        {
            request.Apply(cell.Borders.Right);
            request.Apply(currentRow[c + 1].Borders.Left);
        }
    }

    /// <summary>
    /// Sets the diagonals of a cell, which every cell in the range has its own of.
    /// </summary>
    private static void SetDiagonals(Cell cell, EdgeRequest request)
    {
        if (request.Includes(Edge.DiagonalDown))
            request.Apply(cell.Borders.DiagonalDown);

        if (request.Includes(Edge.DiagonalUp))
            request.Apply(cell.Borders.DiagonalUp);
    }

    /// <summary>
    /// What a SetEdge call asks for: the range of cells, the edges within it, and the border to draw them with.
    /// </summary>
    private readonly struct EdgeRequest
    {
        private readonly Edge edge;
        private readonly BorderStyle borderStyle;
        private readonly Unit width;
        private readonly Color color;

        public EdgeRequest(int firstRow, int firstColumn, int lastRow, int lastColumn,
            Edge edge, BorderStyle borderStyle, Unit width, Color color)
        {
            FirstRow = firstRow;
            FirstColumn = firstColumn;
            LastRow = lastRow;
            LastColumn = lastColumn;
            this.edge = edge;
            this.borderStyle = borderStyle;
            this.width = width;
            this.color = color;
        }

        public int FirstRow { get; }

        public int FirstColumn { get; }

        public int LastRow { get; }

        public int LastColumn { get; }

        public bool Includes(Edge flag) => (edge & flag) == flag;

        /// <summary>
        /// Gives a border the requested style and width, and the colour too unless none was given.
        /// </summary>
        public void Apply(Border border)
        {
            border.Style = borderStyle;
            border.Width = width;
            if (color != Color.Empty)
                border.Color = color;
        }
    }

    /// <summary>
    /// Sets the borders surrounding the specified range of the table.
    /// </summary>
    public void SetEdge(int clm, int row, int clms, int rowCount, Edge edge, BorderStyle borderStyle, Unit width)
    {
        SetEdge(clm, row, clms, rowCount, edge, borderStyle, width, Color.Empty);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the Columns collection of the table.
    /// </summary>
    public Columns Columns
    {
        get
        {
            columns ??= new Columns(this);

            return columns;
        }
        set
        {
            SetParent(value);
            columns = value;
        }
    }

    [DV] internal Columns columns;

    /// <summary>
    /// Gets the Rows collection of the table.
    /// </summary>
    public Rows Rows
    {
        get
        {
            rows ??= new Rows(this);

            return rows;
        }
        set
        {
            SetParent(value);
            rows = value;
        }
    }

    [DV] internal Rows rows;

    /// <summary>
    /// Sets or gets the default style name for all rows and columns of the table.
    /// </summary>
    public string Style
    {
        get => style ?? "";
        set => style = value;
    }

    [DV] internal string style;

    /// <summary>
    /// Gets the default ParagraphFormat for all rows and columns of the table.
    /// </summary>
    public ParagraphFormat Format
    {
        get
        {
            format ??= new ParagraphFormat(this);

            return format;
        }
        set
        {
            SetParent(value);
            format = value;
        }
    }

    [DV] internal ParagraphFormat format;

    /// <summary>
    /// Gets or sets the default top padding for all cells of the table.
    /// </summary>
    public Unit TopPadding
    {
        get => topPadding;
        set => topPadding = value;
    }

    [DV] internal Unit topPadding = Unit.NullValue;

    /// <summary>
    /// Gets or sets the default bottom padding for all cells of the table.
    /// </summary>
    public Unit BottomPadding
    {
        get => bottomPadding;
        set => bottomPadding = value;
    }

    [DV] internal Unit bottomPadding = Unit.NullValue;

    /// <summary>
    /// Gets or sets the default left padding for all cells of the table.
    /// </summary>
    public Unit LeftPadding
    {
        get => leftPadding;
        set => leftPadding = value;
    }

    [DV] internal Unit leftPadding = Unit.NullValue;

    /// <summary>
    /// Gets or sets the default right padding for all cells of the table.
    /// </summary>
    public Unit RightPadding
    {
        get => rightPadding;
        set => rightPadding = value;
    }

    [DV] internal Unit rightPadding = Unit.NullValue;

    /// <summary>
    /// Gets the default Borders object for all cells of the column.
    /// </summary>
    public Borders Borders
    {
        get
        {
            borders ??= new Borders(this);

            return borders;
        }
        set
        {
            SetParent(value);
            borders = value;
        }
    }

    [DV] internal Borders borders;

    /// <summary>
    /// Gets the default Shading object for all cells of the column.
    /// </summary>
    public Shading Shading
    {
        get
        {
            shading ??= new Shading(this);

            return shading;
        }
        set
        {
            SetParent(value);
            shading = value;
        }
    }

    [DV] internal Shading shading;

    /// <summary>
    /// Gets or sets a value indicating whether
    /// to keep all the table rows on the same page.
    /// </summary>
    public bool KeepTogether
    {
        get => keepTogether ?? false;
        set => keepTogether = value;
    }

    [DV] internal bool? keepTogether;

    /// <summary>
    /// Gets or sets a description of what the table holds and how it is arranged, for a reader who
    /// cannot see its shape.
    /// </summary>
    /// <remarks>
    /// What a tagged document writes as the <c>/Summary</c> of the <c>/Table</c> element. Header
    /// cells and their scope let a reader navigate a table one cell at a time; a summary is what
    /// tells it, before it starts, whether the table is worth navigating. Leave it unset for a table
    /// used for layout rather than for data — those should not be tables at all, but they are.
    /// <para>
    /// Distinct from <see cref="Comment"/>, which is a note to whoever reads the MDDDL and never
    /// reaches the PDF.
    /// </para>
    /// </remarks>
    public string Summary
    {
        get => summary ?? "";
        set => summary = value;
    }

    [DV] internal string summary;

    /// <summary>
    /// Gets or sets a comment associated with this object.
    /// </summary>
    public string Comment
    {
        get => comment ?? "";
        set => comment = value;
    }

    [DV] internal string comment;

    #endregion

    #region Internal

    /// <summary>
    /// Converts Table into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteComment(comment ?? "");

        serializer.WriteLine("\\table");

        var pos = serializer.BeginAttributes();

        if ((style ?? "") != string.Empty)
            serializer.WriteSimpleAttribute("Style", Style);

        if (summary != null)
            serializer.WriteSimpleAttribute("Summary", Summary);

        serializer.WriteSimpleAttributeIfSet("KeepTogether", keepTogether);

        if (!IsNull("Format"))
            format.Serialize(serializer, "Format", null);

        if (!topPadding.IsNull)
            serializer.WriteSimpleAttribute("TopPadding", TopPadding);

        if (!leftPadding.IsNull)
            serializer.WriteSimpleAttribute("LeftPadding", LeftPadding);

        if (!rightPadding.IsNull)
            serializer.WriteSimpleAttribute("RightPadding", RightPadding);

        if (!bottomPadding.IsNull)
            serializer.WriteSimpleAttribute("BottomPadding", BottomPadding);

        if (!IsNull("Borders"))
            borders.Serialize(serializer, null);

        serializer.SerializeUnlessNull(this, "Shading", shading);

        serializer.EndAttributes(pos);

        serializer.BeginContent();
        Columns.Serialize(serializer);
        Rows.Serialize(serializer);
        serializer.EndContent();
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitTable(this);

        ((IVisitable)columns).AcceptVisitor(visitor, visitChildren);
        ((IVisitable)rows).AcceptVisitor(visitor, visitChildren);
    }

    /// <summary>
    /// Gets the cell with the given row and column indices.
    /// </summary>
    public Cell this[int rwIdx, int clmIdx] => Rows[rwIdx].Cells[clmIdx];


    #endregion
}
