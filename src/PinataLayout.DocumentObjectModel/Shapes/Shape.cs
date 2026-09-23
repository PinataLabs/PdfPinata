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
/// Base Class for all positionable Classes.
/// </summary>
public partial class Shape : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the Shape class.
  /// </summary>
  public Shape()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Shape class with the specified parent.
  /// </summary>
  internal Shape(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Shape Clone()
  {
    return (Shape)DeepCopy();
  }

  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the wrapping format of the shape.
  /// </summary>
  public WrapFormat WrapFormat
  {
    get
    {
      wrapFormat ??= new WrapFormat(this);

      return wrapFormat;
    }
    set
    {
      SetParent(value);
      wrapFormat = value;
    }
  }
  [DV]
  internal WrapFormat wrapFormat;

  /// <summary>
  /// Gets or sets the reference point of the Top property.
  /// </summary>
  public RelativeVertical RelativeVertical
  {
    get => relativeVertical ?? default;
    set => relativeVertical = EnumGuard.Checked(value);
  }
  [DV]
  internal RelativeVertical? relativeVertical;

  /// <summary>
  /// Gets or sets the reference point of the Left property.
  /// </summary>
  public RelativeHorizontal RelativeHorizontal
  {
    get => relativeHorizontal ?? default;
    set => relativeHorizontal = EnumGuard.Checked(value);
  }
  [DV]
  internal RelativeHorizontal? relativeHorizontal;

  /// <summary>
  /// Gets or sets the position of the top side of the shape.
  /// </summary>
  public TopPosition Top
  {
    get => top;
    set => top = value;
  }
  [DV]
  internal TopPosition top = TopPosition.NullValue;

  /// <summary>
  /// Gets or sets the position of the left side of the shape.
  /// </summary>
  public LeftPosition Left
  {
    get => left;
    set => left = value;
  }
  [DV]
  internal LeftPosition left = LeftPosition.NullValue;

  /// <summary>
  /// Gets the line format of the shape's border.
  /// </summary>
  public LineFormat LineFormat
  {
    get
    {
      lineFormat ??= new LineFormat(this);

      return lineFormat;
    }
    set
    {
      SetParent(value);
      lineFormat = value;
    }
  }
  [DV]
  internal LineFormat lineFormat;

  /// <summary>
  /// Gets the background filling format of the shape.
  /// </summary>
  public FillFormat FillFormat
  {
    get
    {
      fillFormat ??= new FillFormat(this);

      return fillFormat;
    }
    set
    {
      SetParent(value);
      fillFormat = value;
    }
  }
  [DV]
  internal FillFormat fillFormat;

  /// <summary>
  /// Gets or sets the height of the shape.
  /// </summary>
  public Unit Height
  {
    get => height;
    set => height = value;
  }
  [DV]
  internal Unit height = Unit.NullValue;

  /// <summary>
  /// Gets or sets the width of the shape.
  /// </summary>
  public Unit Width
  {
    get => width;
    set => width = value;
  }
  [DV]
  internal Unit width = Unit.NullValue;

  /// <summary>
  /// Gets or sets the text that stands in for this shape for a reader who cannot see it.
  /// </summary>
  /// <remarks>
  /// <para>
  /// What a tagged document writes as the <c>/Alt</c> of the shape's <c>/Figure</c> element. It is
  /// what decides whether the shape is tagged at all: described, it is a figure; left unset, it is
  /// drawn as an artifact and passed over in silence.
  /// </para>
  /// <para>
  /// That is the right way round. An undescribed figure tells a reader that something is there and
  /// then cannot say what, which leaves them knowing only that they have missed something; marked as
  /// decoration it is at least honest, and for the rule above a letterhead it is also correct.
  /// Nothing invents a description — what a picture is for is a fact about the document rather than
  /// about the pixels, and a guess would go into the one field nobody can check.
  /// </para>
  /// <para>
  /// Ignored by <see cref="TextFrame"/>, whose contents are paragraphs and tables that describe
  /// themselves.
  /// </para>
  /// </remarks>
  public string AlternativeText
  {
    get => alternativeText ?? "";
    set => alternativeText = value;
  }
  [DV]
  internal string alternativeText;
  #endregion

  #region Internal
  /// <summary>
  /// Converts Shape into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if (!height.IsNull)
      serializer.WriteSimpleAttribute("Height", Height);
    if (!width.IsNull)
      serializer.WriteSimpleAttribute("Width", Width);
    if (relativeHorizontal != null)
      serializer.WriteSimpleAttribute("RelativeHorizontal", RelativeHorizontal);
    if (relativeVertical != null)
      serializer.WriteSimpleAttribute("RelativeVertical", RelativeVertical);
    if (alternativeText != null)
      serializer.WriteSimpleAttribute("AlternativeText", AlternativeText);
    if (!IsNull("Left"))
      left.Serialize(serializer);
    if (!IsNull("Top"))
      top.Serialize(serializer);
    if (!IsNull("WrapFormat"))
      wrapFormat.Serialize(serializer);
    if (!IsNull("LineFormat"))
      lineFormat.Serialize(serializer);
    if (!IsNull("FillFormat"))
      fillFormat.Serialize(serializer);
  }

  #endregion
}
