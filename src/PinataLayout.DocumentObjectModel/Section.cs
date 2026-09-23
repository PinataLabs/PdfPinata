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
using PinataLayout.DocumentObjectModel.Shapes;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A Section is a collection of document objects sharing the same header, footer, 
/// and page setup.
/// </summary>
public partial class Section : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Section class.
    /// </summary>
    public Section()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Section class with the specified parent.
    /// </summary>
    internal Section(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Section Clone()
    {
        return (Section)DeepCopy();
    }

    /// <summary>
    /// Gets the previous section.
    /// </summary>
    public Section PreviousSection()
    {
        var sections = Parent as Sections;
        // ReSharper disable once PossibleNullReferenceException
        var index = sections.IndexOf(this);
        return index > 0 ? sections[index - 1] : null;
    }

    /// <summary>
    /// Adds a new paragraph to the section.
    /// </summary>
    public Paragraph AddParagraph()
    {
        return Elements.AddParagraph();
    }

    /// <summary>
    /// Adds a new paragraph with the specified text to the section.
    /// </summary>
    public Paragraph AddParagraph(string paragraphText)
    {
        return Elements.AddParagraph(paragraphText);
    }

    /// <summary>
    /// Adds a new paragraph with the specified text and style to the section.
    /// </summary>
    public Paragraph AddParagraph(string paragraphText, string style)
    {
        return Elements.AddParagraph(paragraphText, style);
    }

    /// <summary>
    /// Adds a new chart with the specified type to the section.
    /// </summary>
    public Chart AddChart(ChartType type)
    {
        return Elements.AddChart(type);
    }

    /// <summary>
    /// Adds a new chart to the section.
    /// </summary>
    public Chart AddChart()
    {
        return Elements.AddChart();
    }

    /// <summary>
    /// Adds a new table to the section.
    /// </summary>
    public Table AddTable()
    {
        return Elements.AddTable();
    }

    /// <summary>
    /// Adds a manual page break.
    /// </summary>
    public void AddPageBreak()
    {
        Elements.AddPageBreak();
    }

    /// <summary>
    /// Adds a new Image to the section.
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new textframe to the section.
    /// </summary>
    public TextFrame AddTextFrame()
    {
        return Elements.AddTextFrame();
    }

    /// <summary>
    /// Adds a new paragraph to the section.
    /// </summary>
    public void Add(Paragraph paragraph)
    {
        Elements.Add(paragraph);
    }

    /// <summary>
    /// Adds a new chart to the section.
    /// </summary>
    public void Add(Chart chart)
    {
        Elements.Add(chart);
    }

    /// <summary>
    /// Adds a new table to the section.
    /// </summary>
    public void Add(Table table)
    {
        Elements.Add(table);
    }

    /// <summary>
    /// Adds a new image to the section.
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }

    /// <summary>
    /// Adds a new text frame to the section.
    /// </summary>
    public void Add(TextFrame textFrame)
    {
        Elements.Add(textFrame);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the PageSetup object
    /// </summary>
    public PageSetup PageSetup
    {
        get
        {
            if (pageSetup == null)
                pageSetup = new PageSetup(this);

            return pageSetup;
        }
        set
        {
            SetParent(value);
            pageSetup = value;
        }
    }
    [DV]
    internal PageSetup pageSetup;

    /// <summary>
    /// Gets the HeadersFooters collection containing the headers.
    /// </summary>
    public HeadersFooters Headers
    {
        get
        {
            if (headers == null)
                headers = new HeadersFooters(this);

            return headers;
        }
        set
        {
            SetParent(value);
            headers = value;
        }
    }
    [DV]
    internal HeadersFooters headers;

    /// <summary>
    /// Gets the HeadersFooters collection containing the footers.
    /// </summary>
    public HeadersFooters Footers
    {
        get
        {
            if (footers == null)
                footers = new HeadersFooters(this);

            return footers;
        }
        set
        {
            SetParent(value);
            footers = value;
        }
    }
    [DV]
    internal HeadersFooters footers;

    /// <summary>
    /// Gets the document elements that build the section's content.
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

    /// <summary>
    /// Gets the last paragraph of this section, or null, if no paragraph exists is this section.
    /// </summary>
    public Paragraph LastParagraph
    {
        get
        {
            // Through the property rather than the field. A section nobody has added anything to
            // has not built its element collection yet, and the field is null until it does - so
            // asking the field for its count threw where the summary above promises a null.
            var count = Elements.Count;
            for (var idx = count - 1; idx >= 0; idx--)
            {
                if (Elements[idx] is Paragraph paragraph)
                    return paragraph;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the last table of this section, or null, if no table exists is this section.
    /// </summary>
    public Table LastTable
    {
        get
        {
            // Through the property, for the reason given on LastParagraph above.
            var count = Elements.Count;
            for (var idx = count - 1; idx >= 0; idx--)
            {
                if (Elements[idx] is Table table)
                    return table;
            }
            return null;
        }
    }
    #endregion

    #region Internal
    /// <summary>
    /// Converts Section into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteComment((comment ?? ""));
        serializer.WriteLine("\\section");

        var pos = serializer.BeginAttributes();
        if (!IsNull("PageSetup"))
            PageSetup.Serialize(serializer);
        serializer.EndAttributes(pos);

        serializer.BeginContent();
        var wroteHeadersOrFooters = !IsNull("headers") || !IsNull("footers");
        if (!IsNull("headers"))
            headers.Serialize(serializer);
        if (!IsNull("footers"))
            footers.Serialize(serializer);
        // Bare paragraph text is read only straight after the section's opening brace.
        if (!IsNull("elements"))
            elements.Serialize(serializer, omitParagraphKeyword: !wroteHeadersOrFooters);

        serializer.EndContent();
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitSection(this);

        if (visitChildren && headers != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)headers).AcceptVisitor(visitor, visitChildren);
        if (visitChildren && footers != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)footers).AcceptVisitor(visitor, visitChildren);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    #endregion
}
