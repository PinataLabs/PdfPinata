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
using PinataLayout.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents style templates for paragraph or character formatting
/// </summary>
public sealed partial class Style : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the Style class.
    /// </summary>
    internal Style()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Style class with the specified parent.
    /// </summary>
    internal Style(DocumentObject parent) : base(parent)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Style class with name and base style name.
    /// </summary>
    public Style(string name, string baseStyleName)
        : this()
    {
        // baseStyleName can be null or empty
        ArgumentNullException.ThrowIfNull(name);
        if (name == "")
            throw new ArgumentException(@"A name must not be empty.", nameof(name));

        this.name = name;
        baseStyle = baseStyleName;
    }

    #region Methods

    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Style Clone()
    {
        return (Style)DeepCopy();
    }

    #endregion

    /// <summary>
    /// Returns the value with the specified name and value access.
    /// </summary>
    // ReSharper disable once ParameterHidesMember
    public override object GetValue(string name, GV flags) //newStL
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name == "")
            throw new ArgumentException(@"A name must not be empty.", nameof(name));

        return name.StartsWith("font", StringComparison.OrdinalIgnoreCase)
            ? ParagraphFormat.GetValue(name)
            : base.GetValue(name, flags);
    }

    #region Properties

    /// <summary>
    /// Indicates whether the style is read-only.
    /// </summary>
    public bool IsReadOnly => readOnly;

    internal bool readOnly;

    /// <summary>
    /// Gets the font of ParagraphFormat.
    /// Calling style.Font is just a shortcut to style.ParagraphFormat.Font.
    /// </summary>
    [DV]
    public Font Font
    {
        get => ParagraphFormat.Font;
        // SetParent will be called inside ParagraphFormat.
        set => ParagraphFormat.Font = value;
    }

    /// <summary>
    /// Gets the name of the style.
    /// </summary>
    public string Name => name ?? "";

    [DV] internal string name;

    /// <summary>
    /// Gets the ParagraphFormat. To prevent read-only styles from being modified, a copy of its ParagraphFormat
    /// is returned in this case.
    /// </summary>
    public ParagraphFormat ParagraphFormat
    {
        get
        {
            paragraphFormat ??= new ParagraphFormat(this);
            if (!readOnly)
                return paragraphFormat;

            // The clone is what stops a caller mutating a built-in style through the real object. On
            // its own it stopped nothing - Clone() nulls the parent, so a write to the clone had no way
            // of knowing it was pointless, and simply vanished. Giving the clone its Style back is what
            // lets ThrowIfReadOnly find it.
            var copy = paragraphFormat.Clone();
            copy.parent = this;
            return copy;
        }
        set
        {
            SetParent(value);
            paragraphFormat = value;
        }
    }

    [DV] internal ParagraphFormat paragraphFormat;

    /// <summary>
    /// Gets or sets the name of the base style.
    /// </summary>
    public string BaseStyle
    {
        get => baseStyle ?? "";
        set
        {
            if (value == null ||
                value == "" && (baseStyle ?? "") != "") //!!!modTHHO 17.07.2007: Self assignment is allowed
                throw new ArgumentException(AppResources.EmptyBaseStyle);

            // Self assignment is allowed
            if (string.Compare(baseStyle ?? "", value, StringComparison.OrdinalIgnoreCase) == 0)
            {
                baseStyle = value; // character case may change...
                return;
            }

            AssertBaseStyleCanBeAltered();
            AssertBaseStyleIsValid(value);

            // Now setting new base style is save
            baseStyle = value;
        }
    }

    /// <summary>
    /// Throws for the two root styles, whose having no base style cannot be altered.
    /// </summary>
    private void AssertBaseStyleCanBeAltered()
    {
        if (string.Compare(name ?? "", DefaultParagraphName, StringComparison.OrdinalIgnoreCase) == 0 ||
            string.Compare(name ?? "", DefaultParagraphFontName, StringComparison.OrdinalIgnoreCase) == 0)
        {
            var msg = $"Style '{name}' has no base style and that cannot be altered.";
            throw new ArgumentException(msg);
        }
    }

    /// <summary>
    /// Throws unless the named base style exists and does not have this style in its own chain of
    /// base styles.
    /// </summary>
    private void AssertBaseStyleIsValid(string value)
    {
        var styles = (Styles)parent;
        // The base style must exists
        var idxBaseStyle = styles.GetIndex(value);
        if (idxBaseStyle == -1)
        {
            var msg = $"Base style '{value}' does not exist.";
            throw new ArgumentException(msg);
        }

        if (idxBaseStyle > 1)
            AssertNotInBaseStyleChain(styles, styles[idxBaseStyle], value);
    }

    /// <summary>
    /// Throws if this style is in the base style chain starting at style.
    /// </summary>
    private void AssertNotInBaseStyleChain(Styles styles, Style style, string value)
    {
        while (style != null)
        {
            if (style == this)
            {
                var msg = $"Base style '{value}' leads to a circular dependency.";
                throw new ArgumentException(msg);
            }

            style = styles[style.BaseStyle];
        }
    }

    [DV] internal string baseStyle;

    /// <summary>
    /// Gets the StyleType of the style.
    /// </summary>
    public StyleType Type
    {
        get
        {
            if (styleType != null)
                return styleType.Value;

            if (string.Compare(baseStyle ?? "", DefaultParagraphFontName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                styleType = StyleType.Character;
            }
            else
            {
                var baseStyleObj = GetBaseStyle();
                if (baseStyleObj == null)
                    throw new InvalidOperationException("User defined style has no valid base Style.");

                styleType = baseStyleObj.Type;
            }

            return styleType.Value;
        }
    }

    [DV] internal StyleType? styleType;

    /// <summary>
    /// Determines whether the style is the style Normal or DefaultParagraphFont.
    /// </summary>
    internal bool IsRootStyle => string.Compare(Name, DefaultParagraphFontName, StringComparison.OrdinalIgnoreCase) == 0 ||
                                 string.Compare(Name, DefaultParagraphName, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>
    /// Get the BaseStyle of the current style.
    /// </summary>
    public Style GetBaseStyle()
    {
        if (IsRootStyle)
            return null;

        if (Parent is not Styles styles)
            throw new InvalidOperationException(
                "This instance of 'style' is currently not owner of a parent; access failed");
        if ((baseStyle ?? "") == "")
            throw new ArgumentException("User defined Style defined without a BaseStyle");

        // REVIEW KlPo4StLa Special handling for DefaultParagraphFont is clumsy
        // (DefaultParagraphFont is not returned when accessed via styles["name"]).
        // You're right about that -> see IsReadOnly
        return (baseStyle ?? "") == DefaultParagraphFontName ? styles[0] : styles[baseStyle ?? ""];
    }

    /// <summary>
    /// Indicates whether the style is a predefined (build in) style.
    /// </summary>
    public bool BuildIn => buildIn ?? false;

    [DV] internal bool? buildIn;
    // THHO: muss dass nicht builtIn heißen?!?!?!?

    /// <summary>
    /// Gets or sets a comment associated with this object.
    /// </summary>
    public string Comment
    {
        get => comment ?? "";
        set => comment = value;
    }

    [DV] internal string comment;

    #endregion

    // Names of the root styles. Root styles have no BaseStyle.

    /// <summary>
    /// Name of the default character style.
    /// </summary>
    public const string DefaultParagraphFontName = "DefaultParagraphFont";

    /// <summary>
    /// Name of the default paragraph style.
    /// </summary>
    public const string DefaultParagraphName = "Normal";

    #region Internal

    /// <summary>
    /// Converts Style into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        // For build-in styles all properties that differ from their default values
        // are serialized.
        // For user-defined styles all non-null properties are serialized.
        var buildInStyles = Styles.BuildInStyles;

        serializer.WriteComment(comment ?? "");
        var quotedName = DdlEncoder.QuoteIfNameContainsBlanks(Name);
        var quotedBaseName = DdlEncoder.QuoteIfNameContainsBlanks(BaseStyle);
        var refFormat = (buildIn ?? false)
            ? WriteBuildInHeader(serializer, buildInStyles, quotedName, quotedBaseName)
            : WriteUserDefinedHeader(serializer, quotedName, quotedBaseName);

        serializer.BeginContent();

        if (!IsNull("ParagraphFormat"))
        {
            if (!ParagraphFormat.IsNull("Font"))
                Font.Serialize(serializer, refFormat?.Font);

            if (Type == StyleType.Paragraph)
                ParagraphFormat.Serialize(serializer, "ParagraphFormat", refFormat);
        }

        serializer.EndContent();
    }

    /// <summary>
    /// Writes the name line of a build-in style and answers the paragraph format its values are
    /// compared with, so that only what differs from it is written.
    /// </summary>
    private ParagraphFormat WriteBuildInHeader(Serializer serializer, Styles buildInStyles, string quotedName, string quotedBaseName)
    {
        // BaseStyle is never null, but empty only for "Normal" and "DefaultParagraphFont"
        if (BaseStyle == "")
            return WriteNormalHeader(serializer, buildInStyles, quotedName);

        // case: any build-in style except "Normal"
        var builtIn = buildInStyles[buildInStyles.GetIndex(Name)];
        if (string.Compare(BaseStyle, builtIn.BaseStyle, StringComparison.OrdinalIgnoreCase) == 0)
        {
            // case: build-in style with unmodified base style name
            serializer.WriteLineNoCommit(quotedName);
            // It's fine if we have the predefined base style, but ...
            // ... the base style may have been modified or may even have a modified base style.
            // Methinks it's wrong to compare with the built-in style, so let's compare with the
            // real base style.
            // Note: we must write "Underline = none" if the base style has "Underline = single" - we cannot
            // detect this if we compare with the built-in style that has no underline.
            // Known problem: Default values like "OutlineLevel = Level1" will now be serialized
        }
        else
        {
            // case: build-in style with modified base style name
            serializer.WriteLine(quotedName + " : " + quotedBaseName);
        }

        return Document.Styles[Document.Styles.GetIndex(baseStyle ?? "")].ParagraphFormat;
    }

    /// <summary>
    /// Writes the name line of "Normal", the one build-in paragraph style with no base, and answers
    /// the format it has before anybody changes it.
    /// </summary>
    private ParagraphFormat WriteNormalHeader(Serializer serializer, Styles buildInStyles, string quotedName)
    {
        if (string.Compare(name ?? "", DefaultParagraphName, StringComparison.OrdinalIgnoreCase) != 0)
            throw new ArgumentException("Internal Error: BaseStyle not set.");

        var refFormat = buildInStyles[buildInStyles.GetIndex(Name)].ParagraphFormat;
        serializer.WriteLineNoCommit(quotedName);
        return refFormat;
    }

    /// <summary>
    /// Writes the name line of a user-defined style, with its base, and answers the base style's
    /// paragraph format, or null when there is no such style.
    /// </summary>
    private ParagraphFormat WriteUserDefinedHeader(Serializer serializer, string quotedName, string quotedBaseName)
    {
        // case: user-defined style; base style always exists
        serializer.WriteLine(quotedName + " : " + quotedBaseName);
        return Document.Styles[baseStyle ?? ""]?.ParagraphFormat;
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitStyle(this);
    }

    #endregion
}
