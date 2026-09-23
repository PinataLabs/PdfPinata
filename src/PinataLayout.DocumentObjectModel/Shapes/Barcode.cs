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
    get => orientation ?? default;
    set => orientation = EnumGuard.Checked(value);
  }
  [DV]
  internal TextOrientation? orientation;

  /// <summary>
  /// Gets or sets the type of the barcode.
  /// </summary>
  public BarcodeType Type
  {
    get => type ?? default;
    set => type = EnumGuard.Checked(value);
  }
  [DV]
  internal BarcodeType? type;

  /// <summary>
  /// Gets or sets a value indicating whether bars shall appear beside the barcode
  /// </summary>
  public bool BearerBars
  {
    get => bearerBars ?? false;
    set => bearerBars = value;
  }
  [DV]
  internal bool? bearerBars;

  /// <summary>
  /// Gets or sets the a value indicating whether the barcode's code is rendered.
  /// </summary>
  public bool Text
  {
    get => text ?? false;
    set => text = value;
  }
  [DV]
  internal bool? text;

  /// <summary>
  /// Gets or sets code the barcode represents.
  /// </summary>
  public string Code
  {
    get => code ?? "";
    set => code = value;
  }
  [DV]
  internal string code;

  /// <summary>
  /// ???
  /// </summary>
  public double LineRatio
  {
    get => lineRatio ?? 0;
    set => lineRatio = value;
  }
  [DV]
  internal double? lineRatio;

  /// <summary>
  /// ???
  /// </summary>
  public double LineHeight
  {
    get => lineHeight ?? 0;
    set => lineHeight = value;
  }
  [DV]
  internal double? lineHeight;

  /// <summary>
  /// ???
  /// </summary>
  public double NarrowLineWidth
  {
    get => narrowLineWidth ?? 0;
    set => narrowLineWidth = value;
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
    if ((code ?? "") == "")
      throw new InvalidOperationException(DomSR.MissingObligatoryProperty("Name", "BookmarkField"));

    serializer.WriteLine("\\barcode(\"" + Code + "\")");

    var pos = serializer.BeginAttributes();

    base.Serialize(serializer);

    if (orientation != null)
      serializer.WriteSimpleAttribute("Orientation", Orientation);
    if (bearerBars != null)
      serializer.WriteSimpleAttribute("BearerBars", BearerBars);
    if (text != null)
      serializer.WriteSimpleAttribute("Text", Text);
    if (type != null)
      serializer.WriteSimpleAttribute("Type", Type);
    if (lineRatio != null)
      serializer.WriteSimpleAttribute("LineRatio", LineRatio);
    if (lineHeight != null)
      serializer.WriteSimpleAttribute("LineHeight", LineHeight);
    if (narrowLineWidth != null)
      serializer.WriteSimpleAttribute("NarrowLineWidth", NarrowLineWidth);

    serializer.EndAttributes(pos);
  }

  #endregion
}
