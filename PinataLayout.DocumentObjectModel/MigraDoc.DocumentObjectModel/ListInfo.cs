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

using System;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A ListInfo is the representation of a series of paragraphs as a list.
/// </summary>
public partial class ListInfo : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the ListInfo class.
  /// </summary>
  public ListInfo()
  {
  }

  /// <summary>
  /// Initializes a new instance of the ListInfo class with the specified parent.
  /// </summary>
  internal ListInfo(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new ListInfo Clone()
  {
    return (ListInfo)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the type of the list.
  /// </summary>
  public ListType ListType
  {
    get => listType ?? default;
    set => listType = EnumGuard.Checked(value);
  }
  [DV]
  internal ListType? listType;

  /// <summary>
  /// Gets or sets the left indent of the list symbol.
  /// </summary>
  public Unit NumberPosition
  {
    get => numberPosition;
    set => numberPosition = value;
  }
  [DV]
  internal Unit numberPosition = Unit.NullValue;

  /// <summary>
  /// Gets or sets a value indicating whether
  /// the previous list numbering should be continued.
  /// </summary>
  public bool ContinuePreviousList
  {
    get => continuePreviousList ?? false;
    set => continuePreviousList = value;
  }
  [DV]
  internal bool? continuePreviousList;

  /// <summary>
  /// Gets or sets how deep this list item is nested, one-based. An item that never sets this is
  /// level one, which is today's flat list; a nested item says it is at level two or deeper. This
  /// says nothing about indentation, which is controlled separately - a tagger reads it to build a
  /// list inside a list in the structure tree, and nothing about where the item is drawn changes.
  /// </summary>
  public int NestingLevel
  {
    get => nestingLevel ?? 1;
    set
    {
      if (value < 1)
        throw new ArgumentOutOfRangeException(nameof(value), value, "NestingLevel is one-based; the outermost level is 1.");
      nestingLevel = value;
    }
  }
  [DV]
  internal int? nestingLevel;
  #endregion

  #region Internal
  /// <summary>
  /// Converts ListInfo into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if (listType != null)
      serializer.WriteSimpleAttribute("ListInfo.ListType", ListType);
    if (!numberPosition.IsNull)
      serializer.WriteSimpleAttribute("ListInfo.NumberPosition", NumberPosition);
    if (continuePreviousList != null)
      serializer.WriteSimpleAttribute("ListInfo.ContinuePreviousList", ContinuePreviousList);
    if (nestingLevel != null)
      serializer.WriteSimpleAttribute("ListInfo.NestingLevel", NestingLevel);
  }

  #endregion
}
