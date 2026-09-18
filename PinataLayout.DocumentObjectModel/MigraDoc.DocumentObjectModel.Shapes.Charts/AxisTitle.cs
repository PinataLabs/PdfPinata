#region MigraDoc - Creating Documents on the Fly
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
using PinataLayout.DocumentObjectModel.Tables;

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// Represents the title of an axis.
/// </summary>
public partial class AxisTitle : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the AxisTitle class.
  /// </summary>
  public AxisTitle()
  {
  }

  /// <summary>
  /// Initializes a new instance of the AxisTitle class with the specified parent.
  /// </summary>
  internal AxisTitle(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new AxisTitle Clone()
  {
    return (AxisTitle)DeepCopy();
  }

  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the style name of the axis.
  /// </summary>
  public string Style
  {
    get => this.style ?? "";
    set => this.style = value;
  }
  [DV]
  internal string style;

  /// <summary>
  /// Gets or sets the caption of the title.
  /// </summary>
  public string Caption
  {
    get => this.caption ?? "";
    set => this.caption = value;
  }
  [DV]
  internal string caption;

  /// <summary>
  /// Gets the font object of the title.
  /// </summary>
  public Font Font
  {
    get
    {
      if (this.font == null)
        this.font = new Font(this);

      return this.font;
    }
    set
    {
      SetParent(value);
      this.font = value;
    }
  }
  [DV]
  internal Font font;

  /// <summary>
  /// Gets or sets the orientation of the caption.
  /// </summary>
  public Unit Orientation
  {
    get => this.orientation;
    set => this.orientation = value;
  }
  [DV]
  internal Unit orientation = Unit.NullValue;

  /// <summary>
  /// Gets or sets the alignment of the caption.
  /// </summary>
  public HorizontalAlignment Alignment
  {
    get => this.alignment ?? default;
    set => this.alignment = EnumGuard.Checked(value);
  }
  [DV]
  internal HorizontalAlignment? alignment;

  /// <summary>
  /// Gets or sets the alignment of the caption.
  /// </summary>
  public VerticalAlignment VerticalAlignment
  {
    get => this.verticalAlignment ?? default;
    set => this.verticalAlignment = EnumGuard.Checked(value);
  }
  [DV]
  internal VerticalAlignment? verticalAlignment;
  #endregion

  #region Internal
  /// <summary>
  /// Converts AxisTitle into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    int pos = serializer.BeginContent("Title");

    if (this.style != null)
      serializer.WriteSimpleAttribute("Style", this.Style);

    if (!this.IsNull("Font"))
      this.font.Serialize(serializer);

    // IsNull rather than != null: Unit is a value type, so "!= null" compiles only by way of the
    // implicit string conversion, which converts the null literal and throws. Every other Unit in
    // the charting DOM is tested this way; this one was the exception, and it made an axis title
    // impossible to write.
    if (!this.orientation.IsNull)
      serializer.WriteSimpleAttribute("Orientation", this.Orientation);

    if (this.alignment != null)
      serializer.WriteSimpleAttribute("Alignment", this.Alignment);

    if (this.verticalAlignment != null)
      serializer.WriteSimpleAttribute("VerticalAlignment", this.VerticalAlignment);

    if (this.caption != null)
      serializer.WriteSimpleAttribute("Caption", this.Caption);

    serializer.EndContent();
  }

  #endregion
}
