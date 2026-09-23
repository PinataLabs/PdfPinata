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
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Shapes;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a header or footer object in a section.
/// </summary>
public partial class HeaderFooter : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the HeaderFooter class.
    /// </summary>
    public HeaderFooter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the HeaderFooter class with the specified parent.
    /// </summary>
    internal HeaderFooter(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new HeaderFooter Clone()
    {
        return (HeaderFooter)DeepCopy();
    }

    /// <summary>
    /// Adds a new paragraph to the header or footer.
    /// </summary>
    public Paragraph AddParagraph()
    {
        return Elements.AddParagraph();
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the header or footer.
    /// </summary>
    public Paragraph AddParagraph(string paragraphText)
    {
        return Elements.AddParagraph(paragraphText);
    }

    /// <summary>
    /// Adds a new chart with the specified type to the header or footer.
    /// </summary>
    public Chart AddChart(ChartType type)
    {
        return Elements.AddChart(type);
    }

    /// <summary>
    /// Adds a new chart to the header or footer.
    /// </summary>
    public Chart AddChart()
    {
        return Elements.AddChart();
    }

    /// <summary>
    /// Adds a new table to the header or footer.
    /// </summary>
    public Table AddTable()
    {
        return Elements.AddTable();
    }

    /// <summary>
    /// Adds a new Image to the header or footer.
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new textframe to the header or footer.
    /// </summary>
    public TextFrame AddTextFrame()
    {
        return Elements.AddTextFrame();
    }

    /// <summary>
    /// Adds a new paragraph to the header or footer.
    /// </summary>
    public void Add(Paragraph paragraph)
    {
        Elements.Add(paragraph);
    }

    /// <summary>
    /// Adds a new chart to the header or footer.
    /// </summary>
    public void Add(Chart chart)
    {
        Elements.Add(chart);
    }

    /// <summary>
    /// Adds a new table to the header or footer.
    /// </summary>
    public void Add(Table table)
    {
        Elements.Add(table);
    }

    /// <summary>
    /// Adds a new image to the header or footer.
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }

    /// <summary>
    /// Adds a new text frame to the header or footer.
    /// </summary>
    public void Add(TextFrame textFrame)
    {
        Elements.Add(textFrame);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Returns true if this is a headers, false otherwise.
    /// </summary>
    public bool IsHeader => ((HeadersFooters)parent).IsHeader;

    /// <summary>
    /// Returns true if this is a footer, false otherwise.
    /// </summary>
    public bool IsFooter => ((HeadersFooters)parent).IsFooter;

    /// <summary>
    /// Returns true if this is a first page header or footer, false otherwise.
    /// </summary>
    public bool IsFirstPage => ((HeadersFooters)parent).firstPage == this;

    /// <summary>
    /// Returns true if this is an even page header or footer, false otherwise.
    /// </summary>
    public bool IsEvenPage => ((HeadersFooters)parent).evenPage == this;

    /// <summary>
    /// Returns true if this is a primary header or footer, false otherwise.
    /// </summary>
    public bool IsPrimary => ((HeadersFooters)parent).primary == this;

    /// <summary>
    /// Gets or sets the style name.
    /// </summary>
    public string Style
    {
        get => style ?? "";
        set
        {
            // Just save style name.
            var styleObj = Document.Styles[value];
            if (styleObj != null)
                this.style = value;
            else
                throw new ArgumentException("Invalid style name '" + value + "'.");
        }
    }
    [DV]
    internal string style;

    /// <summary>
    /// Gets or sets the paragraph format.
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
    [DV]
    internal ParagraphFormat format;

    /// <summary>
    /// Gets the collection of document objects that defines the header or footer.
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
    /// Converts HeaderFooter into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        var headersfooters = parent as HeadersFooters;
        // ReSharper disable once PossibleNullReferenceException
        if (headersfooters.Primary == this)
            Serialize(serializer, "primary");
        else if (headersfooters.EvenPage == this)
            Serialize(serializer, "evenpage");
        else if (headersfooters.FirstPage == this)
            Serialize(serializer, "firstpage");
    }

    /// <summary>
    /// Converts HeaderFooter into DDL.
    /// </summary>
    internal void Serialize(Serializer serializer, string prefix)
    {
        serializer.WriteComment(comment ?? "");
        serializer.WriteLine("\\" + prefix + (IsHeader ? "header" : "footer"));

        var pos = serializer.BeginAttributes();
        if ((style ?? "") != string.Empty)
            serializer.WriteSimpleAttribute("Style", Style);
        if (!IsNull("Format"))
            format.Serialize(serializer, "Format", null);
        serializer.EndAttributes(pos);

        serializer.BeginContent();
        if (!IsNull("Elements"))
            elements.Serialize(serializer);
        serializer.EndContent();
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitHeaderFooter(this);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    public override bool IsNull()
    {
        return false;
    }

    #endregion
}
