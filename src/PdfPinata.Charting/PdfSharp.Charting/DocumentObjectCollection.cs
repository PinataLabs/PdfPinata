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
using System.Collections;

namespace PdfPinata.Charting;

/// <summary>
/// Base class of all collections.
/// </summary>
public abstract class DocumentObjectCollection : DocumentObject, IList
{
  /// <summary>
  /// Initializes a new instance of the DocumentObjectCollection class.
  /// </summary>
  internal DocumentObjectCollection()
  {
    elements = new ArrayList();
  }

  /// <summary>
  /// Initializes a new instance of the DocumentObjectCollection class with the specified parent.
  /// </summary>
  internal DocumentObjectCollection(DocumentObject parent) : base(parent)
  {
    elements = new ArrayList();
  }

  /// <summary>
  /// Gets or sets the element at the specified index. An element set here belongs to the
  /// collection, as one added to it does; a null is a blank.
  /// </summary>
  public virtual DocumentObject this[int index]
  {
    get => elements[index] as DocumentObject;
    set
    {
      value?.parent = this;
      elements[index] = value;
    }
  }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new DocumentObjectCollection Clone()
  {
    return (DocumentObjectCollection)DeepCopy();
  }

  /// <summary>
  /// Implements the deep copy of the object.
  /// </summary>
  protected override object DeepCopy()
  {
    var coll = (DocumentObjectCollection)base.DeepCopy();

    var count = Count;
    coll.elements = new ArrayList(count);
    for (var index = 0; index < count; ++index)
    {
      // A blank is a null, and is copied as one.
      var element = this[index];
      if (element == null)
      {
        coll.elements.Add(null);
        continue;
      }
      var copy = (DocumentObject)element.Clone();
      copy.parent = coll;
      coll.elements.Add(copy);
    }
    return coll;
  }

  /// <summary>
  /// Copies the ArrayList or a portion of it to a one-dimensional array.
  /// </summary>
  public void CopyTo(Array array, int index)
  {
    elements.CopyTo(array, index);
  }

  /// <summary>
  /// Removes all elements from the collection.
  /// </summary>
  public void Clear()
  {
    elements.Clear();
  }

  /// <summary>
  /// Inserts an element into the collection at the specified position. The element then belongs
  /// to the collection, as one added to it does; a null is a blank.
  /// </summary>
  public virtual void InsertObject(int index, DocumentObject val)
  {
    val?.parent = this;
    elements.Insert(index, val);
  }

  /// <summary>
  /// Searches for the specified object and returns the zero-based index of the first occurrence.
  /// </summary>
  public int IndexOf(DocumentObject val)
  {
    return elements.IndexOf(val);
  }

  /// <summary>
  /// Removes the element at the specified index.
  /// </summary>
  public void RemoveObjectAt(int index)
  {
    elements.RemoveAt(index);
  }

  /// <summary>
  /// Adds the specified document object to the collection.
  /// </summary>
  public virtual void Add(DocumentObject value)
  {
    value?.parent = this;
    elements.Add(value);
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets the number of elements actually contained in the collection.
  /// </summary>
  public int Count => elements.Count;

  /// <summary>
  /// Gets the first value in the collection, if there is any, otherwise null.
  /// </summary>
  public DocumentObject First
  {
    get
    {
      return Count > 0 ? this[0] : null;
    }
  }

  /// <summary>
  /// Gets the last element or null, if no such element exists.
  /// </summary>
  public DocumentObject LastObject
  {
    get
    {
      var count = elements.Count;
      if (count > 0)
        return (DocumentObject)elements[count - 1];
      return null;
    }
  }
  #endregion

  #region IList
  bool IList.IsReadOnly => false;

  bool IList.IsFixedSize => false;

  // The non-generic members go through the typed ones, so that an element reaching the
  // collection this way belongs to it exactly as one added directly does, and a subclass
  // overriding Add or InsertObject sees it. A null is a blank and is accepted; anything that is
  // not a DocumentObject is refused on the way in and simply not found on a search.

  object IList.this[int index]
  {
    get => elements[index];
    set => this[index] = AsDocumentObject(value);
  }

  void IList.RemoveAt(int index) => RemoveObjectAt(index);

  void IList.Insert(int index, object value) => InsertObject(index, AsDocumentObject(value));

  void IList.Remove(object value)
  {
    var index = ((IList)this).IndexOf(value);
    if (index >= 0)
      RemoveObjectAt(index);
  }

  bool IList.Contains(object value) => ((IList)this).IndexOf(value) >= 0;

  int IList.IndexOf(object value) => value is null or DocumentObject
    ? IndexOf((DocumentObject)value)
    : -1;

  int IList.Add(object value)
  {
    Add(AsDocumentObject(value));
    return Count - 1;
  }

  private static DocumentObject AsDocumentObject(object value)
  {
    if (value is null or DocumentObject)
      return (DocumentObject)value;
    throw new ArgumentException(
      $"A chart collection holds document objects, not {value.GetType().Name}.", nameof(value));
  }
  #endregion

  #region ICollection
  bool ICollection.IsSynchronized => false;

  // ReSharper disable once AssignNullToNotNullAttribute
  object ICollection.SyncRoot => null;

  #endregion

  /// <summary>
  /// Returns an enumerator that iterates through a collection.
  /// </summary>
  /// <returns>
  /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
  /// </returns>
  public IEnumerator GetEnumerator()
  {
    return elements.GetEnumerator();
  }

  private ArrayList elements;
}
