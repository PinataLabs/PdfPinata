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

namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// Defines the format of a line in a shape object.
/// </summary>
public partial class LineFormat : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the LineFormat class.
  /// </summary>
  public LineFormat()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Lineformat class with the specified parent.
  /// </summary>
  internal LineFormat(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new LineFormat Clone()
  {
    return (LineFormat)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets a value indicating whether the line should be visible.
  /// </summary>
  public bool Visible
  {
    get => this.visible ?? false;
    set => this.visible = value;
  }
  [DV]
  internal bool? visible;

  /// <summary>
  /// Gets or sets the width of the line in Unit.
  /// </summary>
  public Unit Width
  {
    get => this.width;
    set => this.width = value;
  }
  [DV]
  internal Unit width = Unit.NullValue;

  /// <summary>
  /// Gets or sets the color of the line.
  /// </summary>
  public Color Color
  {
    get => this.color;
    set => this.color = value;
  }
  [DV]
  internal Color color = Color.Empty;

  /// <summary>
  /// Gets or sets the dash style of the line.
  /// </summary>
  public DashStyle DashStyle
  {
    get => this.dashStyle ?? default;
    set => this.dashStyle = EnumGuard.Checked(value);
  }
  [DV]
  internal DashStyle? dashStyle;

  /// <summary>
  /// Gets or sets the style of the line.
  /// </summary>
  public LineStyle Style
  {
    get => this.style ?? default;
    set => this.style = EnumGuard.Checked(value);
  }
  [DV]
  internal LineStyle? style;
  #endregion

  #region Internal
  /// <summary>
  /// Converts LineFormat into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    int pos = serializer.BeginContent("LineFormat");
    if (this.visible != null)
      serializer.WriteSimpleAttribute("Visible", this.Visible);
    if (this.style != null)
      serializer.WriteSimpleAttribute("Style", this.Style);
    if (this.dashStyle != null)
      serializer.WriteSimpleAttribute("DashStyle", this.DashStyle);
    if (!this.width.IsNull)
      serializer.WriteSimpleAttribute("Width", this.Width);
    if (!this.color.IsNull)
      serializer.WriteSimpleAttribute("Color", this.Color);
    serializer.EndContent();
  }

  #endregion
}
