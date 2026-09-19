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
using System.Collections;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Visitors;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.Shapes;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a paragraph which is used to build up a document with text.
/// </summary>
public partial class Paragraph : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Paragraph class.
    /// </summary>
    public Paragraph()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Paragraph class with the specified parent.
    /// </summary>
    internal Paragraph(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Paragraph Clone()
    {
        return (Paragraph)DeepCopy();
    }

    /// <summary>
    /// Adds a text phrase to the paragraph.
    /// </summary>
    public Text AddText(String text)
    {
        return Elements.AddText(text);
    }

    /// <summary>
    /// Adds a single character repeated the specified number of times to the paragraph.
    /// </summary>
    public Text AddChar(char ch, int count)
    {
        return Elements.AddChar(ch, count);
    }

    /// <summary>
    /// Adds a single character to the paragraph.
    /// </summary>
    public Text AddChar(char ch)
    {
        return Elements.AddChar(ch);
    }

    /// <summary>
    /// Adds one or more Symbol objects.
    /// </summary>
    public Character AddCharacter(SymbolName symbolType, int count)
    {
        return Elements.AddCharacter(symbolType, count);
    }

    /// <summary>
    /// Adds a Symbol object.
    /// </summary>
    public Character AddCharacter(SymbolName symbolType)
    {
        return Elements.AddCharacter(symbolType);
    }

    /// <summary>
    /// Adds one or more Symbol objects defined by a character.
    /// </summary>
    public Character AddCharacter(char ch, int count)
    {
        return Elements.AddCharacter(ch, count);
    }

    /// <summary>
    /// Adds a Symbol object defined by a character.
    /// </summary>
    public Character AddCharacter(char ch)
    {
        return Elements.AddCharacter(ch);
    }

    /// <summary>
    /// Adds a space character as many as count.
    /// </summary>
    public Character AddSpace(int count)
    {
        return Elements.AddSpace(count);
    }

    /// <summary>
    /// Adds a horizontal tab.
    /// </summary>
    public void AddTab()
    {
        Elements.AddTab();
    }

    /// <summary>
    /// Adds a line break.
    /// </summary>
    public void AddLineBreak()
    {
        Elements.AddLineBreak();
    }

    /// <summary>
    /// Adds a new FormattedText.
    /// </summary>
    public FormattedText AddFormattedText()
    {
        return Elements.AddFormattedText();
    }

    /// <summary>
    /// Adds a new FormattedText object with the given format.
    /// </summary>
    public FormattedText AddFormattedText(TextFormat textFormat)
    {
        return Elements.AddFormattedText(textFormat);
    }

    /// <summary>
    /// Adds a new FormattedText with the given Font.
    /// </summary>
    public FormattedText AddFormattedText(Font font)
    {
        return Elements.AddFormattedText(font);
    }

    /// <summary>
    /// Adds a new FormattedText with the given text.
    /// </summary>
    public FormattedText AddFormattedText(string text)
    {
        return Elements.AddFormattedText(text);
    }

    /// <summary>
    /// Adds a new FormattedText object with the given text and format.
    /// </summary>
    public FormattedText AddFormattedText(string text, TextFormat textFormat)
    {
        return Elements.AddFormattedText(text, textFormat);
    }

    /// <summary>
    /// Adds a new FormattedText object with the given text and font.
    /// </summary>
    public FormattedText AddFormattedText(string text, Font font)
    {
        return Elements.AddFormattedText(text, font);
    }

    /// <summary>
    /// Adds a new FormattedText object with the given text and style.
    /// </summary>
    public FormattedText AddFormattedText(string text, string style)
    {
        return Elements.AddFormattedText(text, style);
    }

    /// <summary>
    /// Adds a new Hyperlink of Type "Local",
    /// i.e. the Target is a Bookmark within the Document
    /// </summary>
    public Hyperlink AddHyperlink(string name)
    {
        return Elements.AddHyperlink(name);
    }

    /// <summary>
    /// Adds a new Hyperlink
    /// </summary>
    public Hyperlink AddHyperlink(string name, HyperlinkType type)
    {
        return Elements.AddHyperlink(name, type);
    }

    /// <summary>
    /// Adds a new Bookmark.
    /// </summary>
    public BookmarkField AddBookmark(string name)
    {
        return Elements.AddBookmark(name);
    }

    /// <summary>
    /// Adds a new PageField.
    /// </summary>
    public PageField AddPageField()
    {
        return Elements.AddPageField();
    }

    /// <summary>
    /// Adds a new PageRefField.
    /// </summary>
    public PageRefField AddPageRefField(string name)
    {
        return Elements.AddPageRefField(name);
    }

    /// <summary>
    /// Adds a new NumPagesField.
    /// </summary>
    public NumPagesField AddNumPagesField()
    {
        return Elements.AddNumPagesField();
    }

    /// <summary>
    /// Adds a new SectionField.
    /// </summary>
    public SectionField AddSectionField()
    {
        return Elements.AddSectionField();
    }

    /// <summary>
    /// Adds a new SectionPagesField.
    /// </summary>
    public SectionPagesField AddSectionPagesField()
    {
        return Elements.AddSectionPagesField();
    }

    /// <summary>
    /// Adds a new DateField.
    /// </summary>
    public DateField AddDateField()
    {
        return Elements.AddDateField();
    }

    /// <summary>
    /// Adds a new DateField.
    /// </summary>
    public DateField AddDateField(string format)
    {
        return Elements.AddDateField(format);
    }

    /// <summary>
    /// Adds a new InfoField.
    /// </summary>
    public InfoField AddInfoField(InfoFieldType iType)
    {
        return Elements.AddInfoField(iType);
    }

    /// <summary>
    /// Adds a new Footnote with the specified text.
    /// </summary>
    public Footnote AddFootnote(string text)
    {
        return Elements.AddFootnote(text);
    }

    /// <summary>
    /// Adds a new Footnote.
    /// </summary>
    public Footnote AddFootnote()
    {
        return Elements.AddFootnote();
    }

    /// <summary>
    /// Adds a new Image object
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a new Bookmark
    /// </summary>
    public void Add(BookmarkField bookmark)
    {
        Elements.Add(bookmark);
    }

    /// <summary>
    /// Adds a new PageField
    /// </summary>
    public void Add(PageField pageField)
    {
        Elements.Add(pageField);
    }

    /// <summary>
    /// Adds a new PageRefField
    /// </summary>
    public void Add(PageRefField pageRefField)
    {
        Elements.Add(pageRefField);
    }

    /// <summary>
    /// Adds a new NumPagesField
    /// </summary>
    public void Add(NumPagesField numPagesField)
    {
        Elements.Add(numPagesField);
    }

    /// <summary>
    /// Adds a new SectionField
    /// </summary>
    public void Add(SectionField sectionField)
    {
        Elements.Add(sectionField);
    }

    /// <summary>
    /// Adds a new SectionPagesField
    /// </summary>
    public void Add(SectionPagesField sectionPagesField)
    {
        Elements.Add(sectionPagesField);
    }

    /// <summary>
    /// Adds a new DateField
    /// </summary>
    public void Add(DateField dateField)
    {
        Elements.Add(dateField);
    }

    /// <summary>
    /// Adds a new InfoField
    /// </summary>
    public void Add(InfoField infoField)
    {
        Elements.Add(infoField);
    }

    /// <summary>
    /// Adds a new Footnote
    /// </summary>
    public void Add(Footnote footnote)
    {
        Elements.Add(footnote);
    }

    /// <summary>
    /// Adds a new Text
    /// </summary>
    public void Add(Text text)
    {
        Elements.Add(text);
    }

    /// <summary>
    /// Adds a new FormattedText
    /// </summary>
    public void Add(FormattedText formattedText)
    {
        Elements.Add(formattedText);
    }

    /// <summary>
    /// Adds a new Hyperlink
    /// </summary>
    public void Add(Hyperlink hyperlink)
    {
        Elements.Add(hyperlink);
    }

    /// <summary>
    /// Adds a new Image
    /// </summary>
    public void Add(Image image)
    {
        Elements.Add(image);
    }

    /// <summary>
    /// Adds a new Character
    /// </summary>
    public void Add(Character character)
    {
        Elements.Add(character);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the style name.
    /// </summary>
    public string Style
    {
        get => style ?? "";
        set => style = value;
    }
    [DV]
    internal string style;

    /// <summary>
    /// Gets or sets the ParagraphFormat object of the paragraph.
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
    /// Gets the collection of document objects that defines the paragraph.
    /// </summary>
    public ParagraphElements Elements
    {
        get
        {
            if (elements == null)
                elements = new ParagraphElements(this);

            return elements;
        }
        set
        {
            SetParent(value);
            elements = value;
        }
    }
    [DV]
    internal ParagraphElements elements;

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
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitParagraph(this);

        if (visitChildren && elements != null)
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    internal bool SerializeContentOnly
    {
        get => serializeContentOnly;
        set => serializeContentOnly = value;
    }
    bool serializeContentOnly = false;

    /// <summary>
    /// Converts Paragraph into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        if (!serializeContentOnly)
        {
            serializer.WriteComment((comment ?? ""));
            serializer.WriteLine("\\paragraph");

            var pos = serializer.BeginAttributes();

            if ((style ?? "") != "")
                serializer.WriteLine("Style = \"" + (style ?? "") + "\"");

            if (!IsNull("Format"))
                format.Serialize(serializer, "Format", null);

            serializer.EndAttributes(pos);

            serializer.BeginContent();
            if (!IsNull("Elements"))
                Elements.Serialize(serializer);
            serializer.CloseUpLine();
            serializer.EndContent();
        }
        else
        {
            Elements.Serialize(serializer);
            serializer.CloseUpLine();
        }
    }

    /// <summary>
    /// Returns an array of Paragraphs that are separated by parabreaks. Null if no parabreak is found.
    /// </summary>
    internal Paragraph[] SplitOnParaBreak()
    {
        if (elements == null)
            return null;

        var startIdx = 0;
        var paragraphs = new ArrayList();
        for (var idx = 0; idx < Elements.Count; ++idx)
        {
            var element = Elements[idx];
            if (element is Character)
            {
                var character = (Character)element;
                if (character.SymbolName == SymbolName.ParaBreak)
                {
                    var paragraph = new Paragraph();
                    paragraph.Format = Format.Clone();
                    paragraph.Style = Style;
                    paragraph.Elements = SubsetElements(startIdx, idx - 1);
                    startIdx = idx + 1;
                    paragraphs.Add(paragraph);
                }
            }
        }
        if (startIdx == 0) //No paragraph breaks given.
            return null;
        else
        {
            var paragraph = new Paragraph();
            paragraph.Format = Format.Clone();
            paragraph.Style = Style;
            paragraph.Elements = SubsetElements(startIdx, elements.Count - 1);
            paragraphs.Add(paragraph);

            // Not ToArray(Type): it builds the array type at run time, which carries
            // RequiresDynamicCode and an AOT compiler cannot always have code for.
            var result = new Paragraph[paragraphs.Count];
            paragraphs.CopyTo(result);
            return result;
        }
    }

    /// <summary>
    /// Gets a subset of the paragraphs elements.
    /// </summary>
    /// <param name="startIdx">Start index of the required subset.</param>
    /// <param name="endIdx">End index of the required subset.</param>
    /// <returns>A ParagraphElements object with cloned elements.</returns>
    private ParagraphElements SubsetElements(int startIdx, int endIdx)
    {
        var paragraphElements = new ParagraphElements();
        for (var idx = startIdx; idx <= endIdx; ++idx)
        {
            paragraphElements.Add((DocumentObject)elements[idx].Clone());
        }
        return paragraphElements;
    }
    #endregion
}
