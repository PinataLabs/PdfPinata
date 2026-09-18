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
using PinataLayout.DocumentObjectModel.Resources;
using PdfPinata.Fonts;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents the collection of all styles.
/// </summary>
public partial class Styles : DocumentObjectCollection, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Styles class.
    /// </summary>
    public Styles()
    {
        SetupStyles();
    }

    /// <summary>
    /// Initializes a new instance of the Styles class with the specified parent.
    /// </summary>
    internal Styles(DocumentObject parent)
        : base(parent)
    {
        SetupStyles();
    }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Styles Clone()
    {
        return (Styles)base.DeepCopy();
    }

    /// <summary>
    /// Gets a style by its name.
    /// </summary>
    public Style this[string styleName]
    {
        get
        {
            // From 0, so this agrees with GetIndex. It used to start at 1 to hide
            // DefaultParagraphFont, on the grounds that it "cannot be modified" - protection by
            // being unreachable, which GetIndex and the integer indexer both saw through anyway.
            // It is genuinely unmodifiable now: every setter on its ParagraphFormat and Font throws.
            int count = Count;
            for (int index = 0; index < count; ++index)
            {
                Style style = (Style)this[index];
                if (String.Compare(style.Name, styleName, true) == 0)
                    return style;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets a style by index. 
    /// </summary>
    internal new Style this[int index] => (Style)base[index];

    /// <summary>
    /// Gets the index of a style by name.
    /// </summary>
    /// <param name="styleName">Name of the style looking for.</param>
    /// <returns>Index or -1 if not exists.</returns>
    public int GetIndex(string styleName)
    {
        ArgumentNullException.ThrowIfNull(styleName);

        int count = Count;
        for (int index = 0; index < count; ++index)
        {
            Style style = (Style)this[index];
            if (String.Compare(style.Name, styleName, true) == 0)
                return index;
        }
        return -1;
    }

    /// <summary>
    /// Adds a new style to the styles collection.
    /// </summary>
    /// <param name="name">Name of the style.</param>
    /// <param name="baseStyleName">Name of the base style.</param>
    public Style AddStyle(string name, string baseStyleName)
    {
        if (name == null || baseStyleName == null)
            throw new ArgumentNullException(name == null ? "name" : "baseStyleName");
        if (name == "" || baseStyleName == "")
            throw new ArgumentException(name == "" ? "name" : "baseStyleName");

        Style style = new Style();
        style.name = name;
        style.baseStyle = baseStyleName;
        Add(style);

        // Not the style just built: Add replaces an existing style of the same name with a clone
        // of the one handed to it, so redefining a style used to return an object the collection
        // was not holding. Writing to it reached nothing. Reading the name back gets whichever of
        // the two the document ended up with, and is the same object on the ordinary path.
        return this[name];
    }

    /// <summary>
    /// Adds a DocumentObject to the styles collection.
    /// </summary>
    public override void Add(DocumentObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Style style = value as Style;
        if (style == null)
            throw new InvalidOperationException(AppResources.StyleExpected);

        bool isRootStyle = style.IsRootStyle;

        if (style.BaseStyle == "" && !isRootStyle)
            throw new ArgumentException(DomSR.UndefinedBaseStyle(style.BaseStyle));

        Style baseStyle = null;
        int styleIndex = GetIndex(style.BaseStyle);

        if (styleIndex != -1)
            baseStyle = this[styleIndex] as Style;
        else if (!isRootStyle)
            throw new ArgumentException(DomSR.UndefinedBaseStyle(style.BaseStyle));

        if (baseStyle != null)
            style.styleType = baseStyle.Type;

        int index = GetIndex(style.Name);

        if (index >= 0)
        {
            style = style.Clone();
            style.parent = this;
            ((IList)this)[index] = style;
        }
        else
            base.Add(value);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the default paragraph style.
    /// </summary>
    public Style Normal => this[Style.DefaultParagraphName];

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

    /// <summary>
    /// Initialize the built in styles.
    /// </summary>
    internal void SetupStyles()
    {
        Style style;

        // First standard style
        style = new Style(Style.DefaultParagraphFontName, null)
        {
            readOnly = true
        };
        style.styleType = StyleType.Character;
        style.buildIn = true;
        Add(style);

        // Normal 'Standard' (Paragraph Style)
        style = new Style(Style.DefaultParagraphName, null);
        style.styleType = StyleType.Paragraph;
        style.buildIn = true;
        style.Font.Name = GlobalFontSettings.FontResolver.DefaultFontName;
        style.Font.Size = 10;
        style.Font.Bold = false;
        style.Font.Italic = false;
        style.Font.Underline = Underline.None;
        style.Font.Color = Colors.Black;
        style.Font.Subscript = false;
        style.Font.Superscript = false;
        style.Font.Strikethrough = Strikethrough.None;
        style.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        style.ParagraphFormat.FirstLineIndent = 0;
        style.ParagraphFormat.LeftIndent = 0;
        style.ParagraphFormat.RightIndent = 0;
        style.ParagraphFormat.KeepTogether = false;
        style.ParagraphFormat.KeepWithNext = false;
        style.ParagraphFormat.SpaceBefore = 0;
        style.ParagraphFormat.SpaceAfter = 0;
        style.ParagraphFormat.LineSpacing = 10;
        style.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.BodyText;
        style.ParagraphFormat.PageBreakBefore = false;
        style.ParagraphFormat.WidowControl = true;
        Add(style);

        // Heading1 'Überschrift 1' (Paragraph Style)
        style = new Style("Heading1", "Normal");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;
        Add(style);

        // Heading2 'Überschrift 2' (Paragraph Style)
        style = new Style("Heading2", "Heading1");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;
        Add(style);

        // Heading3 'Überschrift 3' (Paragraph Style)
        style = new Style("Heading3", "Heading2");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level3;
        Add(style);

        // Heading4 'Überschrift 4' (Paragraph Style)
        style = new Style("Heading4", "Heading3");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level4;
        Add(style);

        // Heading5 'Überschrift 5' (Paragraph Style)
        style = new Style("Heading5", "Heading4");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level5;
        Add(style);

        // Heading6 'Überschrift 6' (Paragraph Style)
        style = new Style("Heading6", "Heading5");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level6;
        Add(style);

        // Heading7 'Überschrift 7' (Paragraph Style)
        style = new Style("Heading7", "Heading6");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level7;
        Add(style);

        // Heading8 'Überschrift 8' (Paragraph Style)
        style = new Style("Heading8", "Heading7");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level8;
        Add(style);

        // Heading9 'Überschrift 9' (Paragraph Style)
        style = new Style("Heading9", "Heading8");
        style.buildIn = true;
        style.ParagraphFormat.OutlineLevel = OutlineLevel.Level9;
        Add(style);

        // List 'Liste' (Paragraph Style)
        style = new Style("List", "Normal");
        style.buildIn = true;
        Add(style);

        // Footnote 'Fußnote' (Paragraph Style)
        style = new Style("Footnote", "Normal");
        style.buildIn = true;
        Add(style);

        // Header 'Kopfzeile' (Paragraph Style)
        style = new Style("Header", "Normal");
        style.buildIn = true;
        Add(style);

        // -33: Footer 'Fußzeile' (Paragraph Style)
        style = new Style("Footer", "Normal");
        style.buildIn = true;
        Add(style);

        // Hyperlink 'Hyperlink' (Character Style)
        style = new Style("Hyperlink", "DefaultParagraphFont");
        style.buildIn = true;
        Add(style);

        // InvalidStyleName 'Ungültiger Formatvorlagenname' (Paragraph Style)
        style = new Style("InvalidStyleName", "Normal");
        style.buildIn = true;
        style.Font.Bold = true;
        style.Font.Underline = Underline.Dash;
        style.Font.Color = new Color(0xFF00FF00);
        Add(style);
    }

    #region Internal
    /// <summary>
    /// Converts Styles into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteComment((comment ?? ""));
        int pos = serializer.BeginContent("\\styles");

        // A style can only be added to Styles if its base style exists. Therefore the
        // styles collection is consistent at any one time by definition. But because it 
        // is possible  to change the base style of a style, the sequence of the styles 
        // in the styles collection can be in an order that a style comes before its base
        // style. The styles in an DDL file must be ordered such that each style appears
        // after its base style. We cannot simple reorder the styles collection, because
        // the predefined styles are expected at a fixed position.
        // The solution is to reorder the styles during serialization.
        int count = Count;
        bool[] fSerialized = new bool[count];  // already serialized
        fSerialized[0] = true;                       // consider DefaultParagraphFont as serialized
        bool[] fSerializePending = new bool[count];  // currently serializing
        bool newLine = false;  // gets true if at least one style was written
        //Start from 1 and do not serialize DefaultParagraphFont
        for (int index = 1; index < count; index++)
        {
            if (!fSerialized[index])
            {
                Style style = this[index];
                SerializeStyle(serializer, index, ref fSerialized, ref fSerializePending, ref newLine);
            }
        }
        serializer.EndContent(pos);
    }

    /// <summary>
    /// Serialize a style, but serialize its base style first (if that was not yet done).
    /// </summary>
    void SerializeStyle(Serializer serializer, int index, ref bool[] fSerialized, ref bool[] fSerializePending,
        ref bool newLine)
    {
        Style style = this[index];

        // It is not possible to modify the default paragraph font
        if (style.Name == Style.DefaultParagraphFontName)
            return;

        // Circular dependencies cannot occur if changing the base style is implemented
        // correctly. But before we proof that, we check it here.
        if (fSerializePending[index])
        {
            string message = String.Format("Circular dependency detected according to style '{0}'.", style.Name);
            throw new Exception(message);
        }

        // Only style 'Normal' has no base style
        if (style.BaseStyle != "")
        {
            int idxBaseStyle = GetIndex(style.BaseStyle);
            if (idxBaseStyle != -1)
            {
                if (!fSerialized[idxBaseStyle])
                {
                    fSerializePending[index] = true;
                    SerializeStyle(serializer, idxBaseStyle, ref fSerialized, ref fSerializePending, ref newLine);
                    fSerializePending[index] = false;
                }
            }
        }
        int pos2 = serializer.BeginBlock();
        if (newLine)
            serializer.WriteLineNoCommit();
        style.Serialize(serializer);
        if (serializer.EndBlock(pos2))
            newLine = true;
        fSerialized[index] = true;
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitStyles(this);

        Hashtable visitedStyles = new Hashtable();
        foreach (Style style in this)
            VisitStyle(visitedStyles, style, visitor, visitChildren);
    }

    /// <summary>
    /// Ensures that base styles are visited first.
    /// </summary>
    static void VisitStyle(Hashtable visitedStyles, Style style, DocumentObjectVisitor visitor, bool visitChildren)
    {
        if (!visitedStyles.Contains(style))
        {
            Style baseStyle = style.GetBaseStyle();
            if (baseStyle != null && !visitedStyles.Contains(baseStyle)) //baseStyle != ""
                VisitStyle(visitedStyles, baseStyle, visitor, visitChildren);
            ((IVisitable)style).AcceptVisitor(visitor, visitChildren);
            visitedStyles.Add(style, null);
        }
    }

    internal static readonly Styles BuildInStyles = new Styles();

    #endregion
}
