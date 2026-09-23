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
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.Shapes;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents the format of a text.
/// </summary>
public partial class FormattedText : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the FormattedText class.
    /// </summary>
    public FormattedText()
    {
    }

    /// <summary>
    /// Initializes a new instance of the FormattedText class with the specified parent.
    /// </summary>
    internal FormattedText(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new FormattedText Clone()
    {
        return (FormattedText)DeepCopy();
    }

    /// <summary>
    /// Adds a new Bookmark.
    /// </summary>
    public BookmarkField AddBookmark(string name)
    {
        return Elements.AddBookmark(name);
    }

    /// <summary>
    /// Adds a single character repeated the specified number of times to the formatted text.
    /// </summary>
    public Text AddChar(char ch, int count)
    {
        return Elements.AddChar(ch, count);
    }

    /// <summary>
    /// Adds a single character to the formatted text.
    /// </summary>
    public Text AddChar(char ch)
    {
        return Elements.AddChar(ch);
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
    /// Adds a text phrase to the formatted text.
    /// </summary>
    /// <param name="text">Content of the new text object.</param>
    /// <returns>Returns a new Text object.</returns>
    public Text AddText(string text)
    {
        return Elements.AddText(text);
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
    public FormattedText AddFormattedText(Font textFont)
    {
        return Elements.AddFormattedText(textFont);
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
    public FormattedText AddFormattedText(string text, Font textFont)
    {
        return Elements.AddFormattedText(text, textFont);
    }

    /// <summary>
    /// Adds a new FormattedText object with the given text and style.
    /// </summary>
    public FormattedText AddFormattedText(string text, string styleName)
    {
        return Elements.AddFormattedText(text, styleName);
    }

    /// <summary>
    /// Adds a new Hyperlink of Type "Local", 
    /// i.e. the target is a Bookmark within the Document
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
    /// Adds a new Image object
    /// </summary>
    public Image AddImage(IImageSource imageSource)
    {
        return Elements.AddImage(imageSource);
    }

    /// <summary>
    /// Adds a Symbol object.
    /// </summary>
    public Character AddCharacter(SymbolName symbolType)
    {
        return Elements.AddCharacter(symbolType);
    }

    /// <summary>
    /// Adds one or more Symbol objects.
    /// </summary>
    public Character AddCharacter(SymbolName symbolType, int count)
    {
        return Elements.AddCharacter(symbolType, count);
    }

    /// <summary>
    /// Adds a Symbol object defined by a character.
    /// </summary>
    public Character AddCharacter(char ch)
    {
        return Elements.AddCharacter(ch);
    }

    /// <summary>
    /// Adds one or more Symbol objects defined by a character.
    /// </summary>
    public Character AddCharacter(char ch, int count)
    {
        return Elements.AddCharacter(ch, count);
    }

    /// <summary>
    /// Adds one or more Symbol objects defined by a character.
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
    /// Gets or sets the font object.
    /// </summary>
    public Font Font
    {
        get
        {
            font ??= new Font(this);

            return font;
        }
        set
        {
            SetParent(value);
            font = value;
        }
    }
    [DV]
    internal Font font;

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
    /// Gets or sets the name of the font.
    /// </summary>
    public string FontName
    {
        get => Font.Name;
        set => Font.Name = value;
    }

    /// <summary>
    /// Gets or sets the name of the font.
    /// For internal use only.
    /// </summary>
    internal string Name
    {
        get => Font.Name;
        set => Font.Name = value;
    }

    /// <summary>
    /// Gets or sets the size in point.
    /// </summary>
    public Unit Size
    {
        get => Font.Size;
        set => Font.Size = value;
    }

    /// <summary>
    /// Gets or sets the bold property.
    /// </summary>
    public bool Bold
    {
        get => Font.Bold;
        set => Font.Bold = value;
    }

    /// <summary>
    /// Gets or sets the italic property.
    /// </summary>
    public bool Italic
    {
        get => Font.Italic;
        set => Font.Italic = value;
    }

    /// <summary>
    /// Gets or sets the underline property.
    /// </summary>
    public Underline Underline
    {
        get => Font.Underline;
        set => Font.Underline = value;
    }

    /// <summary>
    /// Gets or sets the color property.
    /// </summary>
    public Color Color
    {
        get => Font.Color;
        set => Font.Color = value;
    }

    /// <summary>
    /// Gets or sets the superscript property.
    /// </summary>
    public bool Superscript
    {
        get => Font.Superscript;
        set => Font.Superscript = value;
    }

    /// <summary>
    /// Gets or sets the subscript property.
    /// </summary>
    public bool Subscript
    {
        get => Font.Subscript;
        set => Font.Subscript = value;
    }

    /// <summary>
    /// Gets the collection of paragraph elements that defines the FormattedText.
    /// </summary>
    public ParagraphElements Elements
    {
        get
        {
            elements ??= new ParagraphElements(this);

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
    #endregion

    #region Internal
    /// <summary>
    /// Converts FormattedText into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        var isFormatted = false;
        if (!IsNull("Font"))
        {
            Font.Serialize(serializer);
            isFormatted = true;
        }
        else
        {
            if (style != null)
            {
                serializer.Write("\\font(\"" + Style + "\")");
                isFormatted = true;
            }
        }

        if (isFormatted)
            serializer.Write("{");

        if (!IsNull("Elements"))
            Elements.Serialize(serializer);

        if (isFormatted)
            serializer.Write("}");
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitFormattedText(this);

        if (visitChildren && elements != null)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
    }

    #endregion
}
