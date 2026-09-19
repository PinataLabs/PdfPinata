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
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Tables;

/// <summary>
/// Represents a cell of a table.
/// </summary>
public partial class Cell : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Cell class.
    /// </summary>
    public Cell()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Cell class with the specified parent.
    /// </summary>
    internal Cell(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Cell Clone()
    {
        return (Cell)DeepCopy();
    }

    /// <summary>
    /// Resets the cached values.
    /// </summary>
    internal override void ResetCachedValues()
    {
        row = null;
        clm = null;
    }

    /// <summary>
    /// Adds a new paragraph to the cell.
    /// </summary>
    public Paragraph AddParagraph()
    {
        return Elements.AddParagraph();
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the cell.
    /// </summary>
    public Paragraph AddParagraph(string paragraphText)
    {
        return Elements.AddParagraph(paragraphText);
    }

    /// <summary>
    /// Adds a new chart with the specified type to the cell.
    /// </summary>
    public Chart AddChart(ChartType type)
    {
        return Elements.AddChart(type);
    }

    /// <summary>
    /// Adds a new chart to the cell.
    /// </summary>
    public Chart AddChart()
    {
        return Elements.AddChart();
    }

    /// <summary>
    /// Adds a new Image to the cell.
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new text-frame to the cell.
    /// </summary>
    public TextFrame AddTextFrame()
    {
        return Elements.AddTextFrame();
    }

    /// <summary>
    /// Adds a new paragraph to the cell.
    /// </summary>
    public void Add(Paragraph paragraph)
    {
        Elements.Add(paragraph);
    }

    /// <summary>
    /// Adds a new chart to the cell.
    /// </summary>
    public void Add(Chart chart)
    {
        Elements.Add(chart);
    }

    /// <summary>
    /// Adds a new image to the cell.
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }

    /// <summary>
    /// Adds a new text frame to the cell.
    /// </summary>
    public void Add(TextFrame textFrame)
    {
        Elements.Add(textFrame);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the table the cell belongs to.
    /// </summary>
    public Table Table
    {
        get
        {
            if (table == null)
            {
                var cls = Parent as Cells;
                if (cls != null)
                    table = cls.Table;
            }
            return table;
        }
    }
    Table table;

    /// <summary>
    /// Gets the column the cell belongs to.
    /// </summary>
    public Column Column
    {
        get
        {
            if (clm == null)
            {
                var cells = Parent as Cells;
                // ReSharper disable once PossibleNullReferenceException
                for (var index = 0; index < cells.Count; ++index)
                {
                    if (cells[index] == this)
                        clm = Table.Columns[index];
                }
            }
            return clm;
        }
    }
    Column clm;

    /// <summary>
    /// Gets the row the cell belongs to.
    /// </summary>
    public Row Row
    {
        get
        {
            if (row == null)
            {
                var cells = Parent as Cells;
                // ReSharper disable once PossibleNullReferenceException
                row = cells.Row;
            }
            return row;
        }
    }
    Row row;

    /// <summary>
    /// Sets or gets the style name.
    /// </summary>
    public string Style
    {
        get => style ?? "";
        set => style = value;
    }
    [DV]
    internal string style;

    /// <summary>
    /// Gets the ParagraphFormat object of the paragraph.
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
    /// Gets or sets the vertical alignment of the cell.
    /// </summary>
    public VerticalAlignment VerticalAlignment
    {
        get => verticalAlignment ?? default;
        set => verticalAlignment = EnumGuard.Checked(value);
    }
    [DV]
    internal VerticalAlignment? verticalAlignment;

    /// <summary>
    /// Gets the Borders object.
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
    /// Gets the shading object.
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
    /// Specifies if the Cell should be rendered as a rounded corner.
    /// </summary>
    public RoundedCorner RoundedCorner {
        get => roundedCorner ?? default;
        set => roundedCorner = EnumGuard.Checked(value);
    }
    [DV]
    internal RoundedCorner? roundedCorner;

    /// <summary>
    /// Gets or sets the number of cells to be merged right.
    /// </summary>
    public int MergeRight
    {
        get => mergeRight ?? 0;
        set => mergeRight = value;
    }
    [DV]
    internal int? mergeRight;

    /// <summary>
    /// Gets or sets the number of cells to be merged down.
    /// </summary>
    public int MergeDown
    {
        get => mergeDown ?? 0;
        set => mergeDown = value;
    }
    [DV]
    internal int? mergeDown;

    /// <summary>
    /// Gets the collection of document objects that defines the cell.
    /// </summary>
    public DocumentElements Elements
    {
        get
        {
            if (elements == null)
                elements = new DocumentElements(this);

            return elements;
        }
        set
        {
            SetParent(value);
            elements = value;
        }
    }
    [DV]
    internal DocumentElements elements;

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
    /// Converts Cell into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteComment((comment ?? ""));
        serializer.WriteLine("\\cell");

        var pos = serializer.BeginAttributes();

        if ((style ?? "") != String.Empty)
            serializer.WriteSimpleAttribute("Style", Style);

        if (!IsNull("Format"))
            format.Serialize(serializer, "Format", null);

        if (mergeDown.HasValue)
            serializer.WriteSimpleAttribute("MergeDown", MergeDown);

        if (mergeRight.HasValue)
            serializer.WriteSimpleAttribute("MergeRight", MergeRight);

        if (verticalAlignment != null)
            serializer.WriteSimpleAttribute("VerticalAlignment", VerticalAlignment);

        if (!IsNull("Borders"))
            borders.Serialize(serializer, null);

        if (!IsNull("Shading"))
            shading.Serialize(serializer);

        if (roundedCorner != null)
            serializer.WriteSimpleAttribute("RoundedCorner", RoundedCorner);

        serializer.EndAttributes(pos);

        pos = serializer.BeginContent();
        if (!IsNull("Elements"))
            elements.Serialize(serializer);
        serializer.EndContent(pos);
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitCell(this);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    #endregion
}
