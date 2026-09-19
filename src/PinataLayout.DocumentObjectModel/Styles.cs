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
            var count = Count;
            for (var index = 0; index < count; ++index)
            {
                var style = (Style)this[index];
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

        var count = Count;
        for (var index = 0; index < count; ++index)
        {
            var style = (Style)this[index];
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

        var style = new Style
        {
            name = name,
            baseStyle = baseStyleName
        };
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

        var style = value as Style;
        if (style == null)
            throw new InvalidOperationException(AppResources.StyleExpected);

        var isRootStyle = style.IsRootStyle;

        if (style.BaseStyle == "" && !isRootStyle)
            throw new ArgumentException(DomSR.UndefinedBaseStyle(style.BaseStyle));

        Style baseStyle = null;
        var styleIndex = GetIndex(style.BaseStyle);

        if (styleIndex != -1)
            baseStyle = this[styleIndex] as Style;
        else if (!isRootStyle)
            throw new ArgumentException(DomSR.UndefinedBaseStyle(style.BaseStyle));

        if (baseStyle != null)
            style.styleType = baseStyle.Type;

        var index = GetIndex(style.Name);

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
            readOnly = true,
            styleType = StyleType.Character,
            buildIn = true
        };
        Add(style);

        // Normal 'Standard' (Paragraph Style)
        style = new Style(Style.DefaultParagraphName, null)
        {
            styleType = StyleType.Paragraph,
            buildIn = true,
            Font =
            {
                Name = GlobalFontSettings.FontResolver.DefaultFontName,
                Size = 10,
                Bold = false,
                Italic = false,
                Underline = Underline.None,
                Color = Colors.Black,
                Subscript = false,
                Superscript = false,
                Strikethrough = Strikethrough.None
            },
            ParagraphFormat =
            {
                Alignment = ParagraphAlignment.Left,
                FirstLineIndent = 0,
                LeftIndent = 0,
                RightIndent = 0,
                KeepTogether = false,
                KeepWithNext = false,
                SpaceBefore = 0,
                SpaceAfter = 0,
                LineSpacing = 10,
                LineSpacingRule = LineSpacingRule.Single,
                OutlineLevel = OutlineLevel.BodyText,
                PageBreakBefore = false,
                WidowControl = true
            }
        };
        Add(style);

        // Heading1 'Überschrift 1' (Paragraph Style)
        style = new Style("Heading1", "Normal")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level1
            }
        };
        Add(style);

        // Heading2 'Überschrift 2' (Paragraph Style)
        style = new Style("Heading2", "Heading1")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level2
            }
        };
        Add(style);

        // Heading3 'Überschrift 3' (Paragraph Style)
        style = new Style("Heading3", "Heading2")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level3
            }
        };
        Add(style);

        // Heading4 'Überschrift 4' (Paragraph Style)
        style = new Style("Heading4", "Heading3")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level4
            }
        };
        Add(style);

        // Heading5 'Überschrift 5' (Paragraph Style)
        style = new Style("Heading5", "Heading4")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level5
            }
        };
        Add(style);

        // Heading6 'Überschrift 6' (Paragraph Style)
        style = new Style("Heading6", "Heading5")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level6
            }
        };
        Add(style);

        // Heading7 'Überschrift 7' (Paragraph Style)
        style = new Style("Heading7", "Heading6")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level7
            }
        };
        Add(style);

        // Heading8 'Überschrift 8' (Paragraph Style)
        style = new Style("Heading8", "Heading7")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level8
            }
        };
        Add(style);

        // Heading9 'Überschrift 9' (Paragraph Style)
        style = new Style("Heading9", "Heading8")
        {
            buildIn = true,
            ParagraphFormat =
            {
                OutlineLevel = OutlineLevel.Level9
            }
        };
        Add(style);

        // List 'Liste' (Paragraph Style)
        style = new Style("List", "Normal")
        {
            buildIn = true
        };
        Add(style);

        // Footnote 'Fußnote' (Paragraph Style)
        style = new Style("Footnote", "Normal")
        {
            buildIn = true
        };
        Add(style);

        // Header 'Kopfzeile' (Paragraph Style)
        style = new Style("Header", "Normal")
        {
            buildIn = true
        };
        Add(style);

        // -33: Footer 'Fußzeile' (Paragraph Style)
        style = new Style("Footer", "Normal")
        {
            buildIn = true
        };
        Add(style);

        // Hyperlink 'Hyperlink' (Character Style)
        style = new Style("Hyperlink", "DefaultParagraphFont")
        {
            buildIn = true
        };
        Add(style);

        // InvalidStyleName 'Ungültiger Formatvorlagenname' (Paragraph Style)
        style = new Style("InvalidStyleName", "Normal")
        {
            buildIn = true,
            Font =
            {
                Bold = true,
                Underline = Underline.Dash,
                Color = new Color(0xFF00FF00)
            }
        };
        Add(style);
    }

    #region Internal
    /// <summary>
    /// Converts Styles into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteComment((comment ?? ""));
        var pos = serializer.BeginContent("\\styles");

        // A style can only be added to Styles if its base style exists. Therefore the
        // styles collection is consistent at any one time by definition. But because it
        // is possible  to change the base style of a style, the sequence of the styles
        // in the styles collection can be in an order that a style comes before its base
        // style. The styles in an DDL file must be ordered such that each style appears
        // after its base style. We cannot simple reorder the styles collection, because
        // the predefined styles are expected at a fixed position.
        // The solution is to reorder the styles during serialization.
        var count = Count;
        var fSerialized = new bool[count];  // already serialized
        fSerialized[0] = true;                       // consider DefaultParagraphFont as serialized
        var fSerializePending = new bool[count];  // currently serializing
        var newLine = false;  // gets true if at least one style was written
        //Start from 1 and do not serialize DefaultParagraphFont
        for (var index = 1; index < count; index++)
        {
            if (!fSerialized[index])
            {
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
        var style = this[index];

        // It is not possible to modify the default paragraph font
        if (style.Name == Style.DefaultParagraphFontName)
            return;

        // Circular dependencies cannot occur if changing the base style is implemented
        // correctly. But before we proof that, we check it here.
        if (fSerializePending[index])
        {
            var message = $"Circular dependency detected according to style '{style.Name}'.";
            throw new Exception(message);
        }

        // Only style 'Normal' has no base style
        if (style.BaseStyle != "")
        {
            var idxBaseStyle = GetIndex(style.BaseStyle);
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
        var pos2 = serializer.BeginBlock();
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

        var visitedStyles = new Hashtable();
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
            var baseStyle = style.GetBaseStyle();
            if (baseStyle != null && !visitedStyles.Contains(baseStyle)) //baseStyle != ""
                VisitStyle(visitedStyles, baseStyle, visitor, visitChildren);
            ((IVisitable)style).AcceptVisitor(visitor, visitChildren);
            visitedStyles.Add(style, null);
        }
    }

    internal static readonly Styles BuildInStyles = new Styles();

    #endregion
}
