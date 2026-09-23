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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Font represents the formatting of characters in a paragraph.
/// </summary>
public sealed partial class Font : DocumentObject
{
    /// <summary>
    /// Initializes a new instance of the Font class that can be used as a template.
    /// </summary>
    public Font()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Font class with the specified parent.
    /// </summary>
    internal Font(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Initializes a new instance of the Font class with the specified name and size.
    /// </summary>
    public Font(string name, Unit size)
    {
        this.name = name;
        this.size.Value = size;
    }

    /// <summary>
    /// Initializes a new instance of the Font class with the specified name.
    /// </summary>
    public Font(string name)
    {
        this.name = name;
    }

    #region Methods
    /// <summary>
    /// Creates a copy of the Font.
    /// </summary>
    public new Font Clone()
    {
        return (Font)DeepCopy();
    }

    /// <summary>
    /// Applies all non-null properties of a font to this font.
    /// </summary>
    public void ApplyFont(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);

        if (!string.IsNullOrEmpty(font.name))
            Name = font.Name;

        if (!font.size.IsNull)
            Size = font.Size;

        if (font.bold != null)
            Bold = font.Bold;

        if (font.italic != null)
            Italic = font.Italic;

        if (font.subscript != null)
            Subscript = font.Subscript;
        else if (font.superscript != null)
            Superscript = font.Superscript;

        if (font.underline != null)
            Underline = font.Underline;

        if (font.strikethrough != null)
            Strikethrough = font.Strikethrough;

        if (!font.color.IsNull)
            Color = font.Color;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the name of the font.
    /// </summary>
    public string Name
    {
        get => name ?? "";
        set { ThrowIfReadOnly(); name = value; }
    }
    [DV]
    internal string name;

    /// <summary>
    /// Gets or sets the size of the font.
    /// </summary>
    public Unit Size
    {
        get => size;
        set { ThrowIfReadOnly(); size = value; }
    }
    [DV]
    internal Unit size = Unit.NullValue;

    /// <summary>
    /// Gets or sets the bold property.
    /// </summary>
    public bool Bold
    {
        get => bold ?? false;
        set { ThrowIfReadOnly(); bold = value; }
    }
    [DV]
    internal bool? bold;

    /// <summary>
    /// Gets or sets the italic property.
    /// </summary>
    public bool Italic
    {
        get => italic ?? false;
        set { ThrowIfReadOnly(); italic = value; }
    }
    [DV]
    internal bool? italic;

    /// <summary>
    /// Gets or sets the underline property.
    /// </summary>
    public Underline Underline
    {
        get => underline ?? default;
        set { ThrowIfReadOnly(); underline = EnumGuard.Checked(value); }
    }
    [DV]
    internal Underline? underline;

    /// <summary>
    /// Gets or sets the color property.
    /// </summary>
    public Color Color
    {
        get => color;
        set { ThrowIfReadOnly(); color = value; }
    }
    [DV]
    internal Color color = Color.Empty;

    /// <summary>
    /// Gets or sets the superscript property.
    /// </summary>
    public bool Superscript
    {
        get => superscript ?? false;
        set
        {
          ThrowIfReadOnly();
            superscript = value;
            subscript = null;
        }
    }
    [DV]
    internal bool? superscript;

    /// <summary>
    /// Gets or sets the subscript property.
    /// </summary>
    public bool Subscript
    {
        get => subscript ?? false;
        set
        {
          ThrowIfReadOnly();
            subscript = value;
            superscript = null;
        }
    }
    [DV]
    internal bool? subscript;

    /// <summary>Gets or sets how the text is struck through.</summary>
    public Strikethrough Strikethrough
    {
        get => strikethrough ?? default;
        set { ThrowIfReadOnly(); strikethrough = EnumGuard.Checked(value); }
    }
    [DV]
    internal Strikethrough? strikethrough;
    //  + .Name = "Verdana"
    //  + .Size = 8
    //  + .Bold = False
    //  + .Italic = False
    //  + .Underline = wdUnderlineDouble
    //  * .UnderlineColor = wdColorOrange
    //    .StrikeThrough = False
    //    .DoubleStrikeThrough = False
    //    .Outline = False
    //    .Emboss = False
    //    .Shadow = False
    //    .Hidden = False
    //  * .SmallCaps = False
    //  * .AllCaps = False
    //  + .Color = wdColorAutomatic
    //    .Engrave = False
    //  + .Superscript = False
    //  + .Subscript = False
    //  * .Spacing = 0
    //  * .Scaling = 100
    //  * .Position = 0
    //    .Kerning = 0
    //    .Animation = wdAnimationNone
    #endregion

    #region Internal
    /// <summary>
    /// Get a bitmask of all non-null properties.
    /// </summary>
    private FontProperties CheckWhatIsNotNull()
    {
        var fp = FontProperties.None;
        if (name != null)
            fp |= FontProperties.Name;
        if (!size.IsNull)
            fp |= FontProperties.Size;
        if (bold != null)
            fp |= FontProperties.Bold;
        if (italic != null)
            fp |= FontProperties.Italic;
        if (underline != null)
            fp |= FontProperties.Underline;
        if (strikethrough != null)
            fp |= FontProperties.Strikethrough;
        if (!color.IsNull)
            fp |= FontProperties.Color;
        if (superscript != null)
            fp |= FontProperties.Superscript;
        if (subscript != null)
            fp |= FontProperties.Subscript;
        return fp;
    }

    /// <summary>
    /// Converts Font into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        Serialize(serializer, null);
    }

    /// <summary>
    /// Converts Font into DDL. Properties with the same value as in an optionally given
    /// font are not serialized.
    /// </summary>
    internal void Serialize(Serializer serializer, Font font)
    {
        if (Parent is FormattedText formattedText)
        {
            var fontStyle = "";
            if (formattedText.style == null)
            {
                // Check if we can use a DDL keyword.
                var notNull = CheckWhatIsNotNull();
                if (notNull == FontProperties.Size)
                {
                    serializer.Write("\\fontsize(" + size.ToString() + ")");
                    return;
                }
                if (notNull == FontProperties.Bold && (bold ?? false))
                {
                    serializer.Write("\\bold");
                    return;
                }
                if (notNull == FontProperties.Italic && (italic ?? false))
                {
                    serializer.Write("\\italic");
                    return;
                }
                if (notNull == FontProperties.Color)
                {
                    serializer.Write("\\fontcolor(" + color.ToString() + ")");
                    return;
                }
            }
            else
            {
                fontStyle = "(\"" + formattedText.Style + "\")";
            }

            serializer.Write("\\font" + fontStyle + "[");

            if (name != null && (name ?? "") != "")
                serializer.WriteSimpleAttribute("Name", Name);

            if (!size.IsNull)
                serializer.WriteSimpleAttribute("Size", Size);

            if (bold != null)
                serializer.WriteSimpleAttribute("Bold", Bold);

            if (italic != null)
                serializer.WriteSimpleAttribute("Italic", Italic);

            if (underline != null)
                serializer.WriteSimpleAttribute("Underline", Underline);

            if (strikethrough != null)
                serializer.WriteSimpleAttribute("Strikethrough", Strikethrough);

            if (superscript != null)
                serializer.WriteSimpleAttribute("Superscript", Superscript);

            if (subscript != null)
                serializer.WriteSimpleAttribute("Subscript", Subscript);

            if (!color.IsNull)
                serializer.WriteSimpleAttribute("Color", Color);

            serializer.Write("]");
        }
        else
        {
            var pos = serializer.BeginContent("Font");

            // Don't write null values if font is null.
            // Do write null values if font is not null!
            if ((name != null && Name != string.Empty && font == null) ||
                (font != null && name != null && Name != string.Empty && Name != font.Name))
                serializer.WriteSimpleAttribute("Name", Name);

            if (!size.IsNull &&
                (font == null || Size != font.Size))
                serializer.WriteSimpleAttribute("Size", Size);
            // NBool and NEnum have to be compared directly to check whether the value Null is
            if (bold != null && (font == null || Bold != font.Bold || font.bold == null))
                serializer.WriteSimpleAttribute("Bold", Bold);

            if (italic != null && (font == null || Italic != font.Italic || font.italic == null))
                serializer.WriteSimpleAttribute("Italic", Italic);

            if (underline != null && (font == null || Underline != font.Underline || font.underline == null))
                serializer.WriteSimpleAttribute("Underline", Underline);

            if (strikethrough != null && (font == null || Strikethrough != font.Strikethrough || font.strikethrough == null))
                serializer.WriteSimpleAttribute("Strikethrough", Strikethrough);

            if (superscript != null && (font == null || Superscript != font.Superscript || font.superscript == null))
                serializer.WriteSimpleAttribute("Superscript", Superscript);

            if (subscript != null && (font == null || Subscript != font.Subscript || font.subscript == null))
                serializer.WriteSimpleAttribute("Subscript", Subscript);

            if (!color.IsNull && (font == null || Color.Argb != font.Color.Argb))// && this.Color.RGB != Color.Transparent.RGB)
                serializer.WriteSimpleAttribute("Color", Color);

            serializer.EndContent(pos);
        }
    }

    #endregion
}
