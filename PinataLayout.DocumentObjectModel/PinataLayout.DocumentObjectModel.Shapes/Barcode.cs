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
using PinataLayout.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// Represents a barcode in the document or paragraph. !!!Still under Construction!!!
/// </summary>
public partial class Barcode : Shape
{
  /// <summary>
  /// Initializes a new instance of the Barcode class.
  /// </summary>
  internal Barcode()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Barcode class with the specified parent.
  /// </summary>
  internal Barcode(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Barcode Clone()
  {
    return (Barcode)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the text orientation for the barcode content.
  /// </summary>
  public TextOrientation Orientation
  {
    get => this.orientation ?? default;
    set => this.orientation = EnumGuard.Checked(value);
  }
  [DV]
  internal TextOrientation? orientation;

  /// <summary>
  /// Gets or sets the type of the barcode.
  /// </summary>
  public BarcodeType Type
  {
    get => this.type ?? default;
    set => this.type = EnumGuard.Checked(value);
  }
  [DV]
  internal BarcodeType? type;

  /// <summary>
  /// Gets or sets a value indicating whether bars shall appear beside the barcode
  /// </summary>
  public bool BearerBars
  {
    get => this.bearerBars ?? false;
    set => this.bearerBars = value;
  }
  [DV]
  internal bool? bearerBars;

  /// <summary>
  /// Gets or sets the a value indicating whether the barcode's code is rendered.
  /// </summary>
  public bool Text
  {
    get => this.text ?? false;
    set => this.text = value;
  }
  [DV]
  internal bool? text;

  /// <summary>
  /// Gets or sets code the barcode represents.
  /// </summary>
  public string Code
  {
    get => this.code ?? "";
    set => this.code = value;
  }
  [DV]
  internal string code;

  /// <summary>
  /// ???
  /// </summary>
  public double LineRatio
  {
    get => this.lineRatio ?? 0;
    set => this.lineRatio = value;
  }
  [DV]
  internal double? lineRatio;

  /// <summary>
  /// ???
  /// </summary>
  public double LineHeight
  {
    get => this.lineHeight ?? 0;
    set => this.lineHeight = value;
  }
  [DV]
  internal double? lineHeight;

  /// <summary>
  /// ???
  /// </summary>
  public double NarrowLineWidth
  {
    get => this.narrowLineWidth ?? 0;
    set => this.narrowLineWidth = value;
  }
  [DV]
  internal double? narrowLineWidth;
  #endregion

  #region Internal
  /// <summary>
  /// Converts Barcode into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if ((this.code ?? "") == "")
      throw new InvalidOperationException(DomSR.MissingObligatoryProperty("Name", "BookmarkField"));

    serializer.WriteLine("\\barcode(\"" + this.Code + "\")");

    int pos = serializer.BeginAttributes();

    base.Serialize(serializer);

    if (this.orientation != null)
      serializer.WriteSimpleAttribute("Orientation", this.Orientation);
    if (this.bearerBars != null)
      serializer.WriteSimpleAttribute("BearerBars", this.BearerBars);
    if (this.text != null)
      serializer.WriteSimpleAttribute("Text", this.Text);
    if (this.type != null)
      serializer.WriteSimpleAttribute("Type", this.Type);
    if (this.lineRatio != null)
      serializer.WriteSimpleAttribute("LineRatio", this.LineRatio);
    if (this.lineHeight != null)
      serializer.WriteSimpleAttribute("LineHeight", this.LineHeight);
    if (this.narrowLineWidth != null)
      serializer.WriteSimpleAttribute("NarrowLineWidth", this.NarrowLineWidth);

    serializer.EndAttributes(pos);
  }

  #endregion
}
