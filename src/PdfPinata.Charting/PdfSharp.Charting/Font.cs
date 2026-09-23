#region Copyright
//
// Authors:
//   Niklas Schneider (mailto:Niklas.Schneider@PdfPinata.com)
//
// Copyright (c) 2005-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
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
using PdfPinata.Drawing;

namespace PdfPinata.Charting;

/// <summary>
/// Font represents the formatting of characters in a paragraph.
/// </summary>
public sealed class Font : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the Font class that can be used as a template.
  /// </summary>
  public Font()
  {}

  /// <summary>
  /// Initializes a new instance of the Font class with the specified parent.
  /// </summary>
  internal Font(DocumentObject parent) : base(parent)
  {}

  /// <summary>
  /// Initializes a new instance of the Font class with the specified name and size.
  /// </summary>
  public Font(string name, XUnit size) : this()
  {
    this.name = name;
    this.size = size;
  }

  #region Methods
  /// <summary>
  /// Creates a copy of the Font.
  /// </summary>
  public new Font Clone()
  {
    return (Font)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the name of the font.
  /// </summary>
  public string Name 
  {
    get => this.name;
    set => this.name = value;
  }
  internal string name = String.Empty;

  /// <summary>
  /// Gets or sets the size of the font.
  /// </summary>
  public XUnit Size
  {
    get => this.size;
    set => this.size = value;
  }
  internal XUnit size;

  /// <summary>
  /// Gets or sets the bold property.
  /// </summary>
  public bool Bold
  {
    get => this.bold ?? false;
    set => this.bold = value;
  }
  // Null is unset, so that an explicit false can be told from a false nobody wrote and win over
  // a bold the chart's font has.
  internal bool? bold;

  /// <summary>
  /// Gets or sets the italic property.
  /// </summary>
  public bool Italic
  {
    get => this.italic ?? false;
    set => this.italic = value;
  }
  internal bool? italic;
    
  /// <summary>
  /// Gets or sets the underline property.
  /// </summary>
  public Underline Underline
  {
    get => this.underline;
    set => this.underline = value;
  }
  internal Underline underline;

  /// <summary>
  /// Gets or sets the strikethrough property.
  /// </summary>
  public Strikethrough Strikethrough
  {
    get => this.strikethrough;
    set => this.strikethrough = value;
  }
  internal Strikethrough strikethrough;

  /// <summary>
  /// Gets or sets the color property.
  /// </summary>
  public XColor Color
  {
    get => this.color;
    set => this.color = value;
  }
  internal XColor color = XColor.Empty;

  /// <summary>
  /// Gets or sets the superscript property.
  /// </summary>
  public bool Superscript
  {
    get => this.superscript;
    set 
    {
      this.superscript = value;
      this.subscript = false;
    }    
  }
  internal bool superscript;

  /// <summary>
  /// Gets or sets the subscript property.
  /// </summary>
  public bool Subscript
  {
    get => this.subscript;
    set 
    {
      this.subscript = value;
      this.superscript = false;
    }
  }
  internal bool subscript;
  #endregion

  #region Inheritance
  // What the renderers draw with. Each answers this font's own value where it has one and asks
  // ParentFont otherwise, so a title, a tick label, a legend or a data label leaving something
  // unset takes it from its chart. Only the five properties a renderer reads inherit; the public
  // getters above answer what was set on this font alone, as they always have. An empty name, a
  // zero size and an empty colour are how those three have always said "unset".

  internal string ResolvedName => this.name.Length > 0 ? this.name : ParentFont?.ResolvedName ?? "";

  internal double ResolvedSize => this.size.Point != 0 ? this.size.Point : ParentFont?.ResolvedSize ?? 0;

  internal bool? ResolvedBold => this.bold ?? ParentFont?.ResolvedBold;

  internal bool? ResolvedItalic => this.italic ?? ParentFont?.ResolvedItalic;

  internal XColor ResolvedColor => !this.color.IsEmpty ? this.color : ParentFont?.ResolvedColor ?? XColor.Empty;

  /// <summary>
  /// The font this one inherits from: the nearest ancestor of its owner that has a font. A series'
  /// data label is the exception, because the chart's data label it defaults to is not its
  /// ancestor but a sibling of the series collection, so it is asked first.
  /// </summary>
  internal Font ParentFont
  {
    get
    {
      var owner = this.parent;
      if (owner == null)
        return null;

      if (owner is DataLabel && owner.parent is Series)
      {
        for (var ancestor = owner.parent; ancestor != null; ancestor = ancestor.parent)
        {
          if (ancestor is Chart chart && chart.dataLabel?.font != null)
            return chart.dataLabel.font;
        }
      }

      for (var ancestor = owner.parent; ancestor != null; ancestor = ancestor.parent)
      {
        var font = FontOf(ancestor);
        if (font != null)
          return font;
      }
      return null;
    }
  }

  private static Font FontOf(DocumentObject owner) => owner switch
  {
    Chart chart => chart.font,
    Legend legend => legend.font,
    DataLabel dataLabel => dataLabel.font,
    AxisTitle title => title.font,
    TickLabels tickLabels => tickLabels.font,
    _ => null,
  };
  #endregion
}
