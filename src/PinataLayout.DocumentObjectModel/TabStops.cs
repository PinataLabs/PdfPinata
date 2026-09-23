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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A TabStops collection represents all TabStop objects in a paragraph.
/// </summary>
public partial class TabStops : DocumentObjectCollection
{
  /// <summary>
  /// Specifies the minimal spacing between two TabStop positions.
  /// </summary>
  public static readonly double TabStopPrecision = 1.5;

  /// <summary>
  /// Initializes a new instance of the TabStops class.
  /// </summary>
  public TabStops()
  {
  }

  /// <summary>
  /// Initializes a new instance of the TabStops class with the specified parent.
  /// </summary>
  internal TabStops(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new TabStops Clone()
  {
    return (TabStops)DeepCopy();
  }

  /// <summary>
  /// Gets a TabStop by its index.
  /// </summary>
  public new TabStop this[int index] => base[index] as TabStop;

  /// <summary>
  /// Gets a TabStop by its position.
  /// Note that also Removed TabStops are taken into account.
  /// </summary>
  public TabStop GetTabStopAt(Unit position)
  {
    var count = Count;
    for (var index = 0; index < count; index++)
    {
      var tabStop = this[index];
      if (Math.Abs(tabStop.Position.Point - position.Point) < TabStopPrecision)
        return tabStop;
    }
    return null;
  }

  /// <summary>
  /// Returns whether a TabStop exists at the given position.
  /// Note that also Removed TabStops are taken into account
  /// </summary>
  public bool TabStopExists(Unit position)
  {
    return GetTabStopAt(position) != null;
  }

  /// <summary>
  /// Adds a TabStop object to the collection. If a TabStop with the same position
  /// already exists, it is replaced by the new TabStop.
  /// </summary>
  public TabStop AddTabStop(TabStop tabStop)
  {
    ArgumentNullException.ThrowIfNull(tabStop);

    if (TabStopExists(tabStop.Position))
    {
      var index = IndexOf(GetTabStopAt(tabStop.Position));
      RemoveObjectAt(index);
      InsertObject(index, tabStop);
    }
    else
    {
      var count = Count;
      for (var index = 0; index < count; index++)
      {
        if (tabStop.Position.Point < this[index].Position.Point)
        {
          InsertObject(index, tabStop);
          return tabStop;
        }
      }
      Add(tabStop);
    }
    return tabStop;
  }

  /// <summary>
  /// Adds a TabStop object at the specified position to the collection. If a TabStop with the
  /// same position already exists, it is replaced by the new TabStop.
  /// </summary>
  public TabStop AddTabStop(Unit position)
  {
    if (TabStopExists(position))
      return GetTabStopAt(position);

    var tab = new TabStop(position);
    return AddTabStop(tab);
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its alignment and leader.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabAlignment alignment, TabLeader leader)
  {
    var tab = AddTabStop(position);
    tab.Alignment = alignment;
    tab.Leader = leader;
    return tab;
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its leader.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabLeader leader)
  {
    var tab = AddTabStop(position);
    tab.Leader = leader;
    return tab;
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its alignment.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabAlignment alignment)
  {
    var tab = AddTabStop(position);
    tab.Alignment = alignment;
    return tab;
  }

  /// <summary>
  /// Adds a TabStop object to the collection marked to remove the tab stop at
  /// the given position.
  /// </summary>
  public void RemoveTabStop(Unit position)
  {
    var tab = AddTabStop(position);
    tab.AddTab = false;
  }

  /// <summary>
  /// Clears all TabStop objects from the collection. Additionally 'TabStops = null'
  /// is written to the DDL stream when serialized.
  /// </summary>
  public void ClearAll()
  {
    Clear();
    fClearAll = true;
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets the information if the collection is marked as cleared. Additionally 'TabStops = null'
  /// is written to the DDL stream when serialized.
  /// </summary>
  public bool TabsCleared => fClearAll;

  internal bool fClearAll;
  #endregion

  #region Internal
  /// <summary>
  /// Converts TabStops into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if (fClearAll)
      serializer.WriteLine("TabStops = null");

    var count = Count;
    for (var index = 0; index < count; index++)
    {
      var tabstop = this[index];
      tabstop.Serialize(serializer);
    }
  }

  /// <summary>
  /// Determines whether this instance is null (not set).
  /// </summary>
  public override bool IsNull()
  {
    // Only non empty and not cleared tabstops (TabStops = null) are null.
    if (base.IsNull())
      return !fClearAll;
    return false;
  }

  #endregion
}
