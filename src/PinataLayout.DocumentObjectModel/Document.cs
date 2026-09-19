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
//using System.Drawing.Text;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Visitors;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a PinataLayout document.
/// </summary>
public sealed partial class Document : DocumentObject, IVisitable
{
  /// <summary>
  /// Initializes a new instance of the Document class.
  /// </summary>
  public Document()
  {
    styles = new Styles(this);
  }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Document Clone()
  {
    return (Document)DeepCopy();
  }

  /// <summary>
  /// A clone starts unbound, whatever the original's binding was - renderer carries no [DV] and so
  /// is untouched by the base DeepCopy, but BindToRenderer's own message promises a clone as the
  /// way to render the same document on a second renderer, and that promise needs this to hold.
  /// </summary>
  protected override object DeepCopy()
  {
    Document document = (Document)base.DeepCopy();
    document.renderer = null;
    return document;
  }

  /// <summary>
  /// Internal function used by renderers to bind this instance to it.
  /// </summary>
  public void BindToRenderer(object renderer)
  {
    //if (this.renderer != null && this.renderer != renderer)
    if (this.renderer != null && renderer != null && !ReferenceEquals(this.renderer, renderer))
    {
      throw new InvalidOperationException("The document is already bound to another renderer. " +
                                          "A PinataLayout document can be rendered by only one renderer, because the rendering process " +
                                          "modifies its internal structure. If you want to render a PinataLayout document on different renderers, " +
                                          "you must create a copy of it using the Clone function.");
    }
    this.renderer = renderer;
  }
  object renderer;

  /// <summary>
  /// Indicates whether the document is bound to a renderer. A bound document must not be modified anymore.
  /// Modifying it leads to undefined results of the rendering process.
  /// </summary>
  public bool IsBoundToRenderer => renderer != null;

  /// <summary>
  /// Adds a new section to the document.
  /// </summary>
  public Section AddSection()
  {
    return Sections.AddSection();
  }

  /// <summary>
  /// Adds a new style to the document styles.
  /// </summary>
  /// <param name="name">Name of the style.</param>
  /// <param name="baseStyle">Name of the base style.</param>
  public Style AddStyle(string name, string baseStyle)
  {
    if (name == null || baseStyle == null)
      throw new ArgumentNullException(name == null ? "name" : "baseStyle");
    if (name == "" || baseStyle == "")
      throw new ArgumentException(name == "" ? "name" : "baseStyle");

    return Styles.AddStyle(name, baseStyle);
  }

  /// <summary>
  /// Adds a new section to the document.
  /// </summary>
  public void Add(Section section)
  {
    Sections.Add(section);
  }

  /// <summary>
  /// Adds a new style to the document styles.
  /// </summary>
  public void Add(Style style)
  {
    Styles.Add(style);
  }
  #endregion

  #region Properties

  /// <summary>
  /// Gets the last section of the document, or null, if the document has no sections.
  /// </summary>
  public Section LastSection => (sections != null && sections.Count > 0) ?
    sections.LastObject as Section : null;

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
  /// Gets the document info.
  /// </summary>
  public DocumentInfo Info
  {
    get
    {
      if (info == null)
        info = new DocumentInfo(this);

      return info;
    }
    set
    {
      SetParent(value);
      info = value;
    }
  }
  [DV]
  internal DocumentInfo info;

  /// <summary>
  /// Gets or sets the styles of the document.
  /// </summary>
  public Styles Styles
  {
    get
    {
      if (styles == null)
        styles = new Styles(this);

      return styles;
    }
    set
    {
      SetParent(value);
      styles = value;
    }
  }
  [DV]
  internal Styles styles;

  /// <summary>
  /// Gets or sets the default tab stop position.
  /// </summary>
  public Unit DefaultTabStop
  {
    get => defaultTabStop;
    set => defaultTabStop = value;
  }
  [DV]
  internal Unit defaultTabStop = Unit.NullValue;

  /// <summary>
  /// Gets the default page setup.
  /// </summary>
  #pragma warning disable CA1822 // Public instance property: making it static would change the public API.
  public PageSetup DefaultPageSetup => PageSetup.DefaultPageSetup;
  #pragma warning restore CA1822

  /// <summary>
  /// Gets or sets the location of the Footnote.
  /// </summary>
  public FootnoteLocation FootnoteLocation
  {
    get => footnoteLocation ?? default;
    set => footnoteLocation = EnumGuard.Checked(value);
  }
  [DV]
  internal FootnoteLocation? footnoteLocation;

  /// <summary>
  /// Gets or sets the rule which is used to determine the footnote number on a new page.
  /// </summary>
  public FootnoteNumberingRule FootnoteNumberingRule
  {
    get => footnoteNumberingRule ?? default;
    set => footnoteNumberingRule = EnumGuard.Checked(value);
  }
  [DV]
  internal FootnoteNumberingRule? footnoteNumberingRule;

  /// <summary>
  /// Gets or sets the type of number which is used for the footnote.
  /// </summary>
  public FootnoteNumberStyle FootnoteNumberStyle
  {
    get => footnoteNumberStyle ?? default;
    set => footnoteNumberStyle = EnumGuard.Checked(value);
  }
  [DV]
  internal FootnoteNumberStyle? footnoteNumberStyle;

  /// <summary>
  /// Gets or sets the starting number of the footnote.
  /// </summary>
  public int FootnoteStartingNumber
  {
    get => footnoteStartingNumber ?? 0;
    set => footnoteStartingNumber = value;
  }
  [DV]
  internal int? footnoteStartingNumber;

  /// <summary>
  /// Gets or sets the path for images used by the document.
  /// </summary>
  public string ImagePath
  {
    get => imagePath ?? "";
    set => imagePath = value;
  }
  [DV]
  internal string imagePath;

  /// <summary>
  /// Gets or sets a value indicating whether to use the CMYK color model when rendered as PDF.
  /// </summary>
  public bool UseCmykColor
  {
    get => useCmykColor ?? false;
    set => useCmykColor = value;
  }
  [DV]
  internal bool? useCmykColor;

  /// <summary>
  /// Gets the sections of the document.
  /// </summary>
  public Sections Sections
  {
    get
    {
      if (sections == null)
        sections = new Sections(this);
      return sections;
    }
    set
    {
      SetParent(value);
      sections = value;
    }
  }
  [DV]
  internal Sections sections;
  #endregion

  /// <summary>
  /// Gets the DDL file name.
  /// </summary>
  public string DdlFile => ddlFile;

  internal string ddlFile = "";

  #region Internal
  /// <summary>
  /// Converts Document into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteComment((comment ?? ""));
    serializer.WriteLine("\\document");

    int pos = serializer.BeginAttributes();
    if (!IsNull("Info"))
      Info.Serialize(serializer);
    if (!defaultTabStop.IsNull)
      serializer.WriteSimpleAttribute("DefaultTabStop", DefaultTabStop);
    if (footnoteLocation != null)
      serializer.WriteSimpleAttribute("FootnoteLocation", FootnoteLocation);
    if (footnoteNumberingRule != null)
      serializer.WriteSimpleAttribute("FootnoteNumberingRule", FootnoteNumberingRule);
    if (footnoteNumberStyle != null)
      serializer.WriteSimpleAttribute("FootnoteNumberStyle", FootnoteNumberStyle);
    if (footnoteStartingNumber != null)
      serializer.WriteSimpleAttribute("FootnoteStartingNumber", FootnoteStartingNumber);
    if (imagePath != null)
      serializer.WriteSimpleAttribute("ImagePath", ImagePath);
    if (useCmykColor != null)
      serializer.WriteSimpleAttribute("UseCmykColor", UseCmykColor);
    serializer.EndAttributes(pos);

    serializer.BeginContent();
    Styles.Serialize(serializer);

    if (!IsNull("Sections"))
      Sections.Serialize(serializer);
    serializer.EndContent();
    serializer.Flush();
  }

  /// <summary>
  /// Allows the visitor object to visit the document object and all it's child objects.
  /// </summary>
  void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
  {
    visitor.VisitDocument(this);
    if (visitChildren)
    {
      ((IVisitable)Styles).AcceptVisitor(visitor, visitChildren);
      ((IVisitable)Sections).AcceptVisitor(visitor, visitChildren);
    }
  }

  #endregion
}
