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

using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Visitors;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// Represents a text frame that can be freely placed.
/// </summary>
public partial class TextFrame : Shape, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the TextFrame class.
    /// </summary>
    public TextFrame()
    {
    }

    /// <summary>
    /// Initializes a new instance of the TextFrame class with the specified parent.
    /// </summary>
    internal TextFrame(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new TextFrame Clone()
    {
        return (TextFrame)DeepCopy();
    }

    /// <summary>
    /// Adds a new paragraph to the text frame.
    /// </summary>
    public Paragraph AddParagraph()
    {
        return Elements.AddParagraph();
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the text frame.
    /// </summary>
    public Paragraph AddParagraph(string _paragraphText)
    {
        return Elements.AddParagraph(_paragraphText);
    }

    /// <summary>
    /// Adds a new chart with the specified type to the text frame.
    /// </summary>
    public Chart AddChart(ChartType _type)
    {
        return Elements.AddChart(_type);
    }

    /// <summary>
    /// Adds a new chart to the text frame.
    /// </summary>
    public Chart AddChart()
    {
        return Elements.AddChart();
    }

    /// <summary>
    /// Adds a new table to the text frame.
    /// </summary>
    public Table AddTable()
    {
        return Elements.AddTable();
    }

    /// <summary>
    /// Adds a new Image to the text frame.
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new paragraph to the text frame.
    /// </summary>
    public void Add(Paragraph paragraph)
    {
        Elements.Add(paragraph);
    }

    /// <summary>
    /// Adds a new chart to the text frame.
    /// </summary>
    public void Add(Chart chart)
    {
        Elements.Add(chart);
    }

    /// <summary>
    /// Adds a new table to the text frame.
    /// </summary>
    public void Add(Table table)
    {
        Elements.Add(table);
    }

    /// <summary>
    /// Adds a new image to the text frame.
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the Margin between the textframes content and its left edge.
    /// </summary>
    public Unit MarginLeft
    {
        get => marginLeft;
        set => marginLeft = value;
    }
    [DV]
    internal Unit marginLeft = Unit.NullValue;

    /// <summary>
    /// Gets or sets the Margin between the textframes content and its right edge.
    /// </summary>
    public Unit MarginRight
    {
        get => marginRight;
        set => marginRight = value;
    }
    [DV]
    internal Unit marginRight = Unit.NullValue;

    /// <summary>
    /// Gets or sets the Margin between the textframes content and its top edge.
    /// </summary>
    public Unit MarginTop
    {
        get => marginTop;
        set => marginTop = value;
    }
    [DV]
    internal Unit marginTop = Unit.NullValue;

    /// <summary>
    /// Gets or sets the Margin between the textframes content and its bottom edge.
    /// </summary>
    public Unit MarginBottom
    {
        get => marginBottom;
        set => marginBottom = value;
    }
    [DV]
    internal Unit marginBottom = Unit.NullValue;

    /// <summary>
    /// Gets or sets the text orientation for the texframe content.
    /// </summary>
    public TextOrientation Orientation
    {
        get => orientation ?? default;
        set => orientation = EnumGuard.Checked(value);
    }
    [DV]
    internal TextOrientation? orientation;

    /// <summary>
    /// The document elements that build the textframe's content.
    /// </summary>
    public DocumentElements Elements
    {
        get
        {
            elements ??= new DocumentElements(this);

            return elements;
        }
        set
        {
            SetParent(value);
            elements = value;
        }
    }
    /// <summary>Backing field for <see cref="Elements"/>.</summary>
    [DV]
    protected DocumentElements elements;
    #endregion

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitTextFrame(this);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    #region Internal
    /// <summary>
    /// Converts TextFrame into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteLine("\\textframe");
        var pos = serializer.BeginAttributes();
        base.Serialize(serializer);
        if (!marginLeft.IsNull)
            serializer.WriteSimpleAttribute("MarginLeft", MarginLeft);
        if (!marginRight.IsNull)
            serializer.WriteSimpleAttribute("MarginRight", MarginRight);
        if (!marginTop.IsNull)
            serializer.WriteSimpleAttribute("MarginTop", MarginTop);
        if (!marginBottom.IsNull)
            serializer.WriteSimpleAttribute("MarginBottom", MarginBottom);
        if (orientation != null)
            serializer.WriteSimpleAttribute("Orientation", Orientation);
        serializer.EndAttributes(pos);

        serializer.BeginContent();
        elements?.Serialize(serializer);
        serializer.EndContent();
    }

    #endregion
}
