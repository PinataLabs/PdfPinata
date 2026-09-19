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
            throw new ArgumentException("A name must not be empty.", nameof(name));

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
    public override object GetValue(string name, GV flags) //newStL
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name == "")
            throw new ArgumentException("A name must not be empty.", nameof(name));

        if (name.StartsWith("font", StringComparison.OrdinalIgnoreCase))
        {
            return ParagraphFormat.GetValue(name);
        }

        return base.GetValue(name, flags);
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
            if (paragraphFormat == null)
                paragraphFormat = new ParagraphFormat(this);
            if (readOnly)
            {
                // The clone is what stops a caller mutating a built-in style through the real object. On
                // its own it stopped nothing - Clone() nulls the parent, so a write to the clone had no way
                // of knowing it was pointless, and simply vanished. Giving the clone its Style back is what
                // lets ThrowIfReadOnly find it.
                var copy = paragraphFormat.Clone();
                copy.parent = this;
                return copy;
            }

            return paragraphFormat;
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
            if (String.Compare(baseStyle ?? "", value, true) == 0)
            {
                baseStyle = value; // character case may change...
                return;
            }

            if (String.Compare((name ?? ""), DefaultParagraphName, true) == 0 ||
                String.Compare((name ?? ""), DefaultParagraphFontName, true) == 0)
            {
                var msg = $"Style '{name}' has no base style and that cannot be altered.";
                throw new ArgumentException(msg);
            }

            var styles = (Styles)parent;
            // The base style must exists
            var idxBaseStyle = styles.GetIndex(value);
            if (idxBaseStyle == -1)
            {
                var msg = $"Base style '{value}' does not exist.";
                throw new ArgumentException(msg);
            }

            if (idxBaseStyle > 1)
            {
                // Is this style in the base style chain of the new base style
                var style = styles[idxBaseStyle] as Style;
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

            // Now setting new base style is save
            baseStyle = value;
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
            //old
            //if (IsNull("Type"))
            //{
            //  if (String.Compare ((this.baseStyle ?? ""), DefaultParagraphFontName, true) == 0)
            //    SetValue("Type", StyleType.Character);
            //  else
            //  {
            //    Style bsStyle = GetBaseStyle();
            //    if (bsStyle == null)
            //      throw new ArgumentException("User defined style has no valid base Style.");
            //
            //    SetValue("Type", bsStyle.Type);
            //  }
            //}
            //return styleType;

            if (styleType == null)
            {
                if (String.Compare((this.baseStyle ?? ""), DefaultParagraphFontName, true) == 0)
                    styleType = StyleType.Character;
                else
                {
                    var baseStyle = GetBaseStyle();
                    if (baseStyle == null)
                        throw new InvalidOperationException("User defined style has no valid base Style.");

                    styleType = baseStyle.Type;
                }
            }

            return styleType.Value;
        }
    }

    [DV] internal StyleType? styleType;

    /// <summary>
    /// Determines whether the style is the style Normal or DefaultParagraphFont.
    /// </summary>
    internal bool IsRootStyle => String.Compare(Name, DefaultParagraphFontName, true) == 0 ||
                                 String.Compare(Name, DefaultParagraphName, true) == 0;

    /// <summary>
    /// Get the BaseStyle of the current style.
    /// </summary>
    public Style GetBaseStyle()
    {
        if (IsRootStyle)
            return null;

        var styles = Parent as Styles;
        if (styles == null)
            //??? 'owner of a parent'? eher 'owned by a parent' oder einfach: "A parent object is required for this operation."
            throw new InvalidOperationException(
                "This instance of 'style' is currently not owner of a parent; access failed");
        if ((baseStyle ?? "") == "")
            throw new ArgumentException("User defined Style defined without a BaseStyle");

        // REVIEW KlPo4StLa Special handling for DefaultParagraphFont is clumsy
        // (DefaultParagraphFont is not returned when accessed via styles["name"]).
        // You're right about that -> see IsReadOnly
        return (baseStyle ?? "") == DefaultParagraphFontName ? styles[0] : styles[(baseStyle ?? "")];
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
        //!!!newTHHO 26.07.2007 Modified method for built-in styles.
        //!!!newTHHO 26.07.2007 Modified method for user-defined styles.
        var buildInStyles = Styles.BuildInStyles;
        Style refStyle = null;
        ParagraphFormat refFormat = null;

        serializer.WriteComment((comment ?? ""));
        if ((buildIn ?? false))
        {
            // BaseStyle is never null, but empty only for "Normal" and "DefaultParagraphFont"
            if (BaseStyle == "")
            {
                // case: style is "Normal"
                if (String.Compare((this.name ?? ""), DefaultParagraphName, true) != 0)
                    throw new ArgumentException("Internal Error: BaseStyle not set.");

                refStyle = buildInStyles[buildInStyles.GetIndex(Name)];
                refFormat = refStyle.ParagraphFormat;
                var name = DdlEncoder.QuoteIfNameContainsBlanks(Name);
                serializer.WriteLineNoCommit(name);
            }
            else
            {
                // case: any build-in style except "Normal"
                refStyle = buildInStyles[buildInStyles.GetIndex(Name)];
                refFormat = refStyle.ParagraphFormat;
                if (String.Compare(BaseStyle, refStyle.BaseStyle, true) == 0)
                {
                    // case: build-in style with unmodified base style name
                    var name = DdlEncoder.QuoteIfNameContainsBlanks(Name);
                    serializer.WriteLineNoCommit(name);
                    // It's fine if we have the predefined base style, but ...
                    // ... the base style may have been modified or may even have a modified base style.
                    // Methinks it's wrong to compare with the built-in style, so let's compare with the
                    // real base style:
                    refStyle = Document.Styles[Document.Styles.GetIndex((baseStyle ?? ""))];
                    refFormat = refStyle.ParagraphFormat;
                    // Note: we must write "Underline = none" if the base style has "Underline = single" - we cannot
                    // detect this if we compare with the built-in style that has no underline.
                    // Known problem: Default values like "OutlineLevel = Level1" will now be serialized
                    // TODO: optimize...
                }
                else
                {
                    // case: build-in style with modified base style name
                    var name = DdlEncoder.QuoteIfNameContainsBlanks(Name);
                    var baseName = DdlEncoder.QuoteIfNameContainsBlanks(BaseStyle);
                    serializer.WriteLine(name + " : " + baseName);
                    refStyle = Document.Styles[Document.Styles.GetIndex((baseStyle ?? ""))];
                    refFormat = refStyle.ParagraphFormat;
                }
            }
        }
        else
        {
            // case: user-defined style; base style always exists

            var name = DdlEncoder.QuoteIfNameContainsBlanks(Name);
            var baseName = DdlEncoder.QuoteIfNameContainsBlanks(BaseStyle);
            serializer.WriteLine(name + " : " + baseName);
            refStyle = Document.Styles[(baseStyle ?? "")];
            refFormat = refStyle != null ? refStyle.ParagraphFormat : null;
        }

        serializer.BeginContent();

        if (!IsNull("ParagraphFormat"))
        {
            if (!ParagraphFormat.IsNull("Font"))
                Font.Serialize(serializer, refFormat != null ? refFormat.Font : null);

            if (Type == StyleType.Paragraph)
                ParagraphFormat.Serialize(serializer, "ParagraphFormat", refFormat);
        }

        serializer.EndContent();
    }

    /// <summary>
    /// Sets all properties to Null that have the same value as the base style.
    /// </summary>
    private static void Optimize()
    {
        // just here as a reminder to do it...
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
