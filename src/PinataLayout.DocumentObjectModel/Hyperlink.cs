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
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Resources;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A Hyperlink is used to reference targets in the document (Local), on a drive (File) or a network (Web).
/// </summary>
public partial class Hyperlink : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Hyperlink class.
    /// </summary>
    public Hyperlink()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Hyperlink class with the specified parent.
    /// </summary>
    internal Hyperlink(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Initializes a new instance of the Hyperlink class with the text the hyperlink shall content.
    /// The type will be treated as Local by default.
    /// </summary>
    internal Hyperlink(string name, string text)
        : this()
    {
        Name = name;
        Elements.AddText(text);
    }

    /// <summary>
    /// Initializes a new instance of the Hyperlink class with the type and text the hyperlink shall
    /// represent.
    /// </summary>
    internal Hyperlink(string name, HyperlinkType type, string text)
        : this()
    {
        Name = name;
        Type = type;
        Elements.AddText(text);
    }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Hyperlink Clone()
    {
        return (Hyperlink)DeepCopy();
    }

    /// <summary>
    /// Adds a text phrase to the hyperlink.
    /// </summary>
    public Text AddText(String text)
    {
        return Elements.AddText(text);
    }

    /// <summary>
    /// Adds a single character repeated the specified number of times to the hyperlink.
    /// </summary>
    public Text AddChar(char ch, int count)
    {
        return Elements.AddChar(ch, count);
    }

    /// <summary>
    /// Adds a single character to the hyperlink.
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
    /// Adds a new Bookmark.
    /// </summary>
    public BookmarkField AddBookmark(string bookmarkName)
    {
        return Elements.AddBookmark(bookmarkName);
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
    public PageRefField AddPageRefField(string bookmarkName)
    {
        return Elements.AddPageRefField(bookmarkName);
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
            if (font == null)
                font = new Font(this);

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
    /// Gets or sets the target name of the Hyperlink, e.g. an URL or a bookmark's name.
    /// </summary>
    public string Name
    {
        get => name ?? "";
        set => name = value;
    }
    [DV]
    internal string name;

    /// <summary>
    /// Gets or sets the target type of the Hyperlink.
    /// </summary>
    public HyperlinkType Type
    {
        get => type ?? default;
        set => type = EnumGuard.Checked(value);
    }
    [DV]
    internal HyperlinkType? type;

    /// <summary>
    /// Gets the ParagraphElements of the Hyperlink specifying its 'clickable area'.
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
    #endregion

    #region Internal
    /// <summary>
    /// Converts Hyperlink into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        if ((name ?? "") == string.Empty)
            throw new InvalidOperationException(DomSR.MissingObligatoryProperty("Name", "Hyperlink"));
        serializer.Write("\\hyperlink");
        var str = "[Name = \"" + Name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        if (type != null)
            str += " Type = " + Type;
        if (IsNull("Font"))
            serializer.Write(str + "]");
        else
        {
            // The same attribute block a paragraph writes its font in, inside the brackets:
            // the reader takes "Font { ... }" there just as it takes "Name = ...".
            serializer.Write(str + " ");
            font.Serialize(serializer);
            serializer.Write("]");
        }
        serializer.Write("{");
        elements?.Serialize(serializer);
        serializer.Write("}");
    }

    #endregion

    #region IDomVisitable Members
    /// <summary>
    /// Lets a visitor visit this hyperlink, and its elements when
    /// <paramref name="visitChildren"/> says so.
    /// </summary>
    public void AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitHyperlink(this);
        if (visitChildren && elements != null)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
        }
    }
    #endregion
}
