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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents one border in a borders collection. The type determines its position in a cell,
/// paragraph etc.
/// </summary>
public partial class Border : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the Border class.
  /// </summary>
  public Border()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Border class with the specified parent.
  /// </summary>
  internal Border(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Border Clone()
  {
    return (Border)DeepCopy();
  }

  /// <summary>
  /// Clears the Border object. Additionally 'Border = null'
  /// is written to the DDL stream when serialized.
  /// </summary>
  public void Clear()
  {
    fClear = true;
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets a value indicating whether the border visible is.
  /// </summary>
  public bool Visible
  {
    get => visible ?? false;
    set => visible = value;
  }
  [DV]
  internal bool? visible;

  /// <summary>
  /// Gets or sets the line style of the border.
  /// </summary>
  public BorderStyle Style
  {
    get => style ?? default;
    set => style = EnumGuard.Checked(value);
  }
  [DV]
  internal BorderStyle? style;

  /// <summary>
  /// Gets or sets the line width of the border.
  /// </summary>
  public Unit Width
  {
    get => width;
    set => width = value;
  }
  [DV]
  internal Unit width = Unit.NullValue;

  /// <summary>
  /// Gets or sets the color of the border.
  /// </summary>
  public Color Color
  {
    get => color;
    set => color = value;
  }
  [DV]
  internal Color color = Color.Empty;

  /// <summary>
  /// Gets the name of this border ("top", "bottom"....).
  /// </summary>
  public string Name => ((Borders)parent).GetMyName(this);

  /// <summary>
  /// Gets the information if the border is marked as cleared. Additionally 'xxx = null'
  /// is written to the DDL stream when serialized.
  /// </summary>
  public bool BorderCleared => fClear;

  internal bool fClear = false;
  #endregion

  #region Null handling
  /// <summary>
  /// Determines whether this instance is null (not set).
  /// </summary>
  /// <remarks>
  /// A cleared border is not null. Being cleared is what the border has to say - it writes
  /// 'Border = null' into the DDL so as to override what it would otherwise inherit - and every
  /// caller that decides whether to serialize a border asks this question first. fClear carries no
  /// [DV] attribute, so the value descriptors Meta.IsNull consults cannot see it, and a border that
  /// had only been cleared used to report itself null and be skipped.
  /// </remarks>
  public override bool IsNull()
  {
    return !fClear && base.IsNull();
  }

  /// <summary>
  /// Resets this instance, i.e. IsNull() will return true afterwards.
  /// </summary>
  public override void SetNull()
  {
    base.SetNull();
    fClear = false;
  }
  #endregion

  #region Internal
  /// <summary>
  /// Converts Border into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    throw new Exception("A Border cannot be serialized alone.");
  }

  /// <summary>
  /// Converts Border into DDL.
  /// </summary>
  internal void Serialize(Serializer serializer, string name, Border refBorder)
  {
    if (fClear)
      serializer.WriteLine(name + " = null");

    int pos = serializer.BeginContent(name);

    if (visible != null && (refBorder == null || (Visible != refBorder.Visible)))
      serializer.WriteSimpleAttribute("Visible", Visible);

    if (style != null && (refBorder == null || (Style != refBorder.Style)))
      serializer.WriteSimpleAttribute("Style", Style);

    if (!width.IsNull && (refBorder == null || (Width != refBorder.Width)))
      serializer.WriteSimpleAttribute("Width", Width);

    if (!color.IsNull && (refBorder == null || (Color != refBorder.Color)))
      serializer.WriteSimpleAttribute("Color", Color);

    serializer.EndContent(pos);
  }

  #endregion
}
