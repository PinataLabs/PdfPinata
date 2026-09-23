#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PdfPinata.Pdf.Content.Objects;

/// <summary>
/// Represents a sequence of objects in a PDF content stream.
/// </summary>
[DebuggerDisplay("(count={Count})")]
public class CSequence : CObject, IList<CObject> // , ICollection<CObject>, IEnumerable<CObject>
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CSequence Clone()
    {
        return (CSequence)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var copy = (CSequence)base.Copy();
        copy._items = new List<CObject>(_items.Count);
        for (var idx = 0; idx < _items.Count; idx++)
            copy._items.Add(_items[idx].Clone());
        return copy;
    }

    /// <summary>
    /// Appends the <em>contents</em> of another sequence, or an array as a single item.
    /// </summary>
    /// <param name="sequence">The sequence whose items are appended, or the array to append.</param>
    /// <remarks>
    /// <see cref="CArray"/> derives from this class, so <c>Add(array)</c> binds here rather than to
    /// <see cref="Add(CObject)"/>. Spreading an array's items into the sequence lost its brackets,
    /// and <c>[(A)-250(B)] TJ</c> was written as <c>(A)-250(B)TJ</c>; an array is one operand, and
    /// is added as one. A plain sequence - a list of operands, as the content parser builds - is
    /// still appended item by item.
    /// </remarks>
    public void Add(CSequence sequence)
    {
        if (sequence is CArray array)
        {
            _items.Add(array);
            return;
        }

        var count = sequence.Count;
        for (var idx = 0; idx < count; idx++)
            _items.Add(sequence[idx]);
    }

    #region IList Members

    /// <summary>
    /// Adds the specified value add the end of the sequence.
    /// </summary>
    public void Add(CObject value)
    {
        _items.Add(value);
    }

    /// <summary>
    /// Removes all elements from the sequence.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Determines whether the specified value is in the sequence.
    /// </summary>
    public bool Contains(CObject value)
    {
        return _items.Contains(value);
    }

    /// <summary>
    /// Returns the index of the specified value in the sequence or -1, if no such value is in the sequence.
    /// </summary>
    public int IndexOf(CObject value)
    {
        return _items.IndexOf(value);
    }

    /// <summary>
    /// Inserts the specified value in the sequence.
    /// </summary>
    public void Insert(int index, CObject value)
    {
        _items.Insert(index, value);
    }

    /////// <summary>
    /////// Gets a value indicating whether the sequence has a fixed size.
    /////// </summary>
    ////public bool IsFixedSize
    ////{
    ////  get { return items.IsFixedSize; }
    ////}

    /////// <summary>
    /////// Gets a value indicating whether the sequence is read-only.
    /////// </summary>
    ////public bool IsReadOnly
    ////{
    ////  get { return items.IsReadOnly; }
    ////}

    /// <summary>
    /// Removes the specified value from the sequence.
    /// </summary>
    public bool Remove(CObject value)
    {
        return _items.Remove(value);
    }

    /// <summary>
    /// Removes the value at the specified index from the sequence.
    /// </summary>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
    }

    /// <summary>
    /// Gets or sets a CObject at the specified index.
    /// </summary>
    /// <value></value>
    public CObject this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    #endregion

    #region ICollection Members

    /// <summary>
    /// Copies the elements of the sequence to the specified array.
    /// </summary>
    public void CopyTo(CObject[] array, int index)
    {
        _items.CopyTo(array, index);
    }


    /// <summary>
    /// Gets the number of elements contained in the sequence.
    /// </summary>
    public int Count => _items.Count;

    #endregion

    #region IEnumerable Members

    /// <summary>
    /// Returns an enumerator that iterates through the sequence.
    /// </summary>
    public IEnumerator<CObject> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    #endregion

    /// <summary>
    /// Converts the sequence to a PDF content stream.
    /// </summary>
    public byte[] ToContent()
    {
        var stream = new MemoryStream();
        var writer = new ContentWriter(stream);
        WriteObject(writer);
        writer.Close(false);

        stream.Position = 0;
        var count = (int)stream.Length;
        var bytes = new byte[count];
        PdfPinata.Internal.StreamHelper.ReadUpTo(stream, bytes, 0, count);
        stream.Dispose();
        return bytes;
    }

    /// <summary>
    /// Returns a string containing all elements of the sequence.
    /// </summary>
    public override string ToString()
    {
        var s = new StringBuilder();

        foreach (var item in _items)
            s.Append(item);

        return s.ToString();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal override void WriteObject(ContentWriter writer)
    {
        foreach (var item in _items)
            item.WriteObject(writer);
    }

    /// <summary>
    /// Always false. A sequence read out of a content stream can be added to and edited.
    /// </summary>
    /// <remarks>
    /// The only member of the interfaces this class declares that it did not already satisfy with
    /// a public method of its own. Every other one was implemented twice over: once publicly and
    /// correctly, and once as an explicit interface implementation that threw
    /// <see cref="NotImplementedException"/>. C# binds an interface to the explicit implementation
    /// where there is one, so <c>IList&lt;CObject&gt;</c> and <c>IEnumerable&lt;CObject&gt;</c>
    /// threw for every member while the identical public methods beside them worked - and LINQ,
    /// which reaches a collection through <c>IEnumerable&lt;T&gt;</c>, could not be used on a
    /// content stream at all. The stubs are deleted; the public members satisfy the interfaces.
    /// </remarks>
    public bool IsReadOnly => false;

    private List<CObject> _items = [];
}
