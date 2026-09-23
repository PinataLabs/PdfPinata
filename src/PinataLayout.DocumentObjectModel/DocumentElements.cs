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

using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.Visitors;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Shapes;
using MigraDocImage = PinataLayout.DocumentObjectModel.Shapes.Image;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a collection of document elements.
/// </summary>
public partial class DocumentElements : DocumentObjectCollection, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the DocumentElements class.
    /// </summary>
    public DocumentElements()
    {
    }

    /// <summary>
    /// Initializes a new instance of the DocumentElements class with the specified parent.
    /// </summary>
    internal DocumentElements(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Gets a document object by its index.
    /// </summary>
    public new DocumentObject this[int index] => base[index];

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new DocumentElements Clone()
    {
        return (DocumentElements)DeepCopy();
    }

    /// <summary>
    /// Adds a new paragraph to the collection.
    /// </summary>
    public Paragraph AddParagraph()
    {
        var paragraph = new Paragraph();
        Add(paragraph);
        return paragraph;
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the collection.
    /// </summary>
    public Paragraph AddParagraph(string text)
    {
        var paragraph = new Paragraph();
        paragraph.AddText(text);
        Add(paragraph);
        return paragraph;
    }

    /// <summary>
    /// Adds a new paragraph with the specified text and style to the collection.
    /// </summary>
    public Paragraph AddParagraph(string text, string style)
    {
        var paragraph = new Paragraph();
        paragraph.AddText(text);
        paragraph.Style = style;
        Add(paragraph);
        return paragraph;
    }

    /// <summary>
    /// Adds a bookmark at this point in the flow of the document, to be referenced later by a
    /// Hyperlink or a PageRef.
    /// </summary>
    /// <remarks>
    /// A bookmark is a place, not something drawn, so this adds nothing to the page. It is not
    /// an entry in the outline a reader shows in its bookmarks panel: those come from
    /// ParagraphFormat.OutlineLevel, which the built-in Heading1 to Heading9 styles set.
    /// Where the bookmark belongs on the heading it names — which is usually — put it there
    /// with Paragraph.AddBookmark instead, so that the two cannot drift apart.
    /// </remarks>
    public BookmarkField AddBookmark(string name)
    {
        var bookmark = new BookmarkField(name);
        Add(bookmark);
        return bookmark;
    }

    /// <summary>
    /// Adds a new table to the collection.
    /// </summary>
    public Table AddTable()
    {
        var tbl = new Table();
        Add(tbl);
        return tbl;
    }

    /// <summary>
    /// Adds a new legend to the collection.
    /// </summary>
    public Legend AddLegend()
    {
        var legend = new Legend();
        Add(legend);
        return legend;
    }

    /// <summary>
    /// Add a manual page break.
    /// </summary>
    public void AddPageBreak()
    {
        var pageBreak = new PageBreak();
        Add(pageBreak);
    }

    /// <summary>
    /// Adds a new barcode to the collection.
    /// </summary>
    public Barcode AddBarcode()
    {
        var barcode = new Barcode();
        Add(barcode);
        return barcode;
    }

    /// <summary>
    /// Adds a new chart with the specified type to the collection.
    /// </summary>
    public Chart AddChart(ChartType type)
    {
        var chart = AddChart();
        chart.Type = type;
        return chart;
    }

    /// <summary>
    /// Adds a new chart with the specified type to the collection.
    /// </summary>
    public Chart AddChart()
    {
        var chart = new Chart();
        chart.Type = ChartType.Line;
        Add(chart);
        return chart;
    }

    /// <summary>Adds an image already decoded by the registered image source.</summary>
    public MigraDocImage AddImage(IImageSource image)
    {
        var img = new MigraDocImage()
        {
            Source = image
        };
        Add(img);
        return img;
    }

    /// <summary>
    /// Adds a new text frame to the collection.
    /// </summary>
    public TextFrame AddTextFrame()
    {
        var textFrame = new TextFrame();
        Add(textFrame);
        return textFrame;
    }
    #endregion

    #region Internal
    /// <summary>
    /// Converts DocumentElements into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer) => Serialize(serializer, omitParagraphKeyword: true);

    /// <summary>
    /// Converts DocumentElements into DDL. A lone plain paragraph is written as bare text only when
    /// <paramref name="omitParagraphKeyword"/> allows it: the parser accepts bare text only straight
    /// after its container's opening brace, and a section writes its headers and footers first.
    /// </summary>
    internal void Serialize(Serializer serializer, bool omitParagraphKeyword)
    {
        var count = Count;
        if (omitParagraphKeyword && count == 1 && this[0] is Paragraph)
        {
            // Omit keyword if paragraph has no attributes set.
            var paragraph = (Paragraph)this[0];
            if (paragraph.Style == "" && paragraph.IsNull("Format"))
            {
                paragraph.SerializeContentOnly = true;
                paragraph.Serialize(serializer);
                paragraph.SerializeContentOnly = false;
                return;
            }
        }
        for (var index = 0; index < count; index++)
        {
            var documentElement = this[index];
            documentElement.Serialize(serializer);
        }
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitDocumentElements(this);

        foreach (DocumentObject docObject in this)
        {
            (docObject as IVisitable)?.AcceptVisitor(visitor, visitChildren);
        }
    }

    #endregion
}
