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
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Shapes;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a footnote in a paragraph.
/// </summary>
public partial class Footnote : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Footnote class.
    /// </summary>
    public Footnote()
    {
        //NYI: Nested footnote check!
    }

    /// <summary>
    /// Initializes a new instance of the Footnote class with the specified parent.
    /// </summary>
    internal Footnote(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Initializes a new instance of the Footnote class with a text the Footnote shall content.
    /// </summary>
    internal Footnote(string content)
        : this()
    {
        Elements.AddParagraph(content);
    }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Footnote Clone()
    {
        return (Footnote)DeepCopy();
    }

    /// <summary>
    /// Adds a new paragraph to the footnote.
    /// </summary>
    public Paragraph AddParagraph()
    {
        return Elements.AddParagraph();
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the footnote.
    /// </summary>
    public Paragraph AddParagraph(string text)
    {
        return Elements.AddParagraph(text);
    }

    /// <summary>
    /// Adds a new table to the footnote.
    /// </summary>
    public Table AddTable()
    {
        return Elements.AddTable();
    }

    /// <summary>
    /// Adds a new image to the footnote.
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new paragraph to the footnote.
    /// </summary>
    public void Add(Paragraph paragraph)
    {
        Elements.Add(paragraph);
    }

    /// <summary>
    /// Adds a new table to the footnote.
    /// </summary>
    public void Add(Table table)
    {
        Elements.Add(table);
    }

    /// <summary>
    /// Adds a new image to the footnote.
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the collection of paragraph elements that defines the footnote.
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
    /// Gets or sets the character to be used to mark the footnote.
    /// </summary>
    public string Reference
    {
        get => reference ?? "";
        set => reference = value;
    }
    [DV]
    internal string reference;

    /// <summary>
    /// Gets or sets the style name of the footnote.
    /// </summary>
    public string Style
    {
        get => style ?? "";
        set => style = value;
    }
    [DV]
    internal string style;

    /// <summary>
    /// Gets or sets the identifier this note is known by in a tagged document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a tagged document writes as the <c>/ID</c> of the <c>/Note</c> element, which ISO
    /// 14289-1 7.9 requires of every note: a note exists to be pointed at from the mark that cited
    /// it, and an element with no identifier cannot be pointed at.
    /// </para>
    /// <para>
    /// Left unset the renderer generates one — <c>note1</c>, <c>note2</c>, in the order the notes are
    /// cited — which is what most documents want. Set it where the identifier has to mean something
    /// outside this document, because something else refers to it. It has to be unique across the
    /// whole document: two elements under one name is refused when the identifier tree is written,
    /// and the generated names are the obvious ones to collide with.
    /// </para>
    /// </remarks>
    public string Identifier
    {
        get => identifier ?? "";
        set => identifier = value;
    }
    [DV]
    internal string identifier;

    /// <summary>
    /// Gets the format of the footnote.
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
    #endregion

    #region Internal
    /// <summary>
    /// Converts Footnote into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteLine("\\footnote");

        var pos = serializer.BeginAttributes();
        if ((reference ?? "") != string.Empty)
            serializer.WriteSimpleAttribute("Reference", Reference);
        if ((style ?? "") != string.Empty)
            serializer.WriteSimpleAttribute("Style", Style);
        if ((identifier ?? "") != string.Empty)
            serializer.WriteSimpleAttribute("Identifier", Identifier);

        if (!IsNull("Format"))
            format.Serialize(serializer, "Format", null);

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
        visitor.VisitFootnote(this);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    #endregion
}
