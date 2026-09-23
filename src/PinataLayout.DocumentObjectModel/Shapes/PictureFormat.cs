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
/// A PictureFormat object.
/// Used to set more detailed image attributes
/// </summary>
public partial class PictureFormat : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the PictureFormat class.
  /// </summary>
  public PictureFormat()
  {
  }

  /// <summary>
  /// Initializes a new instance of the PictureFormat class with the specified parent.
  /// </summary>
  internal PictureFormat(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new PictureFormat Clone()
  {
    return (PictureFormat)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the part cropped from the left of the image.
  /// </summary>
  public Unit CropLeft
  {
    get => cropLeft;
    set => cropLeft = value;
  }
  /// <summary>Backing field for <see cref="CropLeft"/>.</summary>
  [DV]
  protected Unit cropLeft = Unit.NullValue;

  /// <summary>
  /// Gets or sets the part cropped from the right of the image.
  /// </summary>
  public Unit CropRight
  {
    get => cropRight;
    set => cropRight = value;
  }
  /// <summary>Backing field for <see cref="CropRight"/>.</summary>
  [DV]
  protected Unit cropRight = Unit.NullValue;

  /// <summary>
  /// Gets or sets the part cropped from the top of the image.
  /// </summary>
  public Unit CropTop
  {
    get => cropTop;
    set => cropTop = value;
  }
  /// <summary>Backing field for <see cref="CropTop"/>.</summary>
  [DV]
  protected Unit cropTop = Unit.NullValue;

  /// <summary>
  /// Gets or sets the part cropped from the bottom of the image.
  /// </summary>
  public Unit CropBottom
  {
    get => cropBottom;
    set => cropBottom = value;
  }
  /// <summary>Backing field for <see cref="CropBottom"/>.</summary>
  [DV]
  protected Unit cropBottom = Unit.NullValue;
  #endregion

  #region Internal
  /// <summary>
  /// Converts PictureFormat into DDL
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.BeginContent("PictureFormat");
    if (!cropLeft.IsNull)
      serializer.WriteSimpleAttribute("CropLeft", CropLeft);
    if (!cropRight.IsNull)
      serializer.WriteSimpleAttribute("CropRight", CropRight);
    if (!cropTop.IsNull)
      serializer.WriteSimpleAttribute("CropTop", CropTop);
    if (!cropBottom.IsNull)
      serializer.WriteSimpleAttribute("CropBottom", CropBottom);
    serializer.EndContent();
  }

  #endregion
}
