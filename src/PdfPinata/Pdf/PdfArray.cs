#region Copyright

//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Globalization;
using System.Text;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a PDF array object.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public class PdfArray : PdfObject, IEnumerable<PdfItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfArray"/> class.
    /// </summary>
    public PdfArray()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfArray"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfArray(PdfDocument document)
        : base(document)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfArray"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="items">The items.</param>
    public PdfArray(PdfDocument document, params PdfItem[] items)
        : base(document)
    {
        foreach (var item in items)
            Elements.Add(item);
    }

    /// <summary>
    /// Initializes a new instance from an existing dictionary. Used for object type transformation.
    /// </summary>
    /// <param name="array">The array.</param>
    protected PdfArray(PdfArray array)
        : base(array)
    {
        if (array._elements != null)
            array._elements.ChangeOwner(this);
    }

    /// <summary>
    /// Creates a copy of this array. Direct elements are deep copied.
    /// Indirect references are not modified.
    /// </summary>
    public new PdfArray Clone()
    {
        return (PdfArray)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism.
    /// </summary>
    protected override object Copy()
    {
        var array = (PdfArray)base.Copy();
        if (array._elements == null)
            return array;

        array._elements = array._elements.Clone();
        var count = array._elements.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var item = array._elements[idx];
            if (item is PdfObject)
                array._elements[idx] = item.Clone();
        }

        return array;
    }

    /// <summary>
    /// Gets the collection containing the elements of this object.
    /// </summary>
    public ArrayElements Elements => _elements ?? (_elements = new ArrayElements(this));

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    public virtual IEnumerator<PdfItem> GetEnumerator()
    {
        return EnumerateItems();
    }

    /// <summary>
    /// What <see cref="GetEnumerator"/> answers. A separate member so that a derived collection can
    /// hide <see cref="GetEnumerator"/> behind a better-typed one - which it cannot also override -
    /// and still decide what it yields when it is enumerated as an array.
    /// </summary>
    private protected virtual IEnumerator<PdfItem> EnumerateItems()
    {
        return Elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Returns a string with the content of this object in a readable form. Useful for debugging purposes only.
    /// </summary>
    public override string ToString()
    {
        var pdf = new StringBuilder();
        pdf.Append("[ ");
        var count = Elements.Count;
        for (var idx = 0; idx < count; idx++)
            pdf.Append(Elements[idx] + " ");
        pdf.Append(']');
        return pdf.ToString();
    }

    internal override void WriteObject(PdfWriter writer)
    {
        writer.WriteBeginObject(this);
        var count = Elements.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var value = Elements[idx];
            value.WriteObject(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Represents the elements of an PdfArray.
    /// </summary>
    public sealed class ArrayElements : IList<PdfItem>, ICloneable
    {
        internal ArrayElements(PdfArray array)
        {
            _elements = new List<PdfItem>();
            _ownerArray = array;
        }

        object ICloneable.Clone()
        {
            var elements = (ArrayElements)MemberwiseClone();
            elements._elements = new List<PdfItem>(elements._elements);
            elements._ownerArray = null;
            return elements;
        }

        /// <summary>
        /// Creates a shallow copy of this object.
        /// </summary>
        public ArrayElements Clone()
        {
            return (ArrayElements)((ICloneable)this).Clone();
        }

        /// <summary>
        /// Moves this instance to another array during object type transformation.
        /// </summary>
        internal void ChangeOwner(PdfArray array)
        {
            if (_ownerArray != null)
            {
                // ???
            }

            // Set new owner.
            _ownerArray = array;

            // Set owners elements to this.
            array._elements = this;
        }

        /// <summary>
        /// Converts the specified value to boolean.
        /// If the value does not exist, the function returns false.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// If the index is out of range, the function throws an ArgumentOutOfRangeException.
        /// </summary>
        public bool GetBoolean(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            object obj = this[index];
            switch (obj)
            {
                case null or PdfNull:
                    return false;
                // Follow an indirect reference the way DictionaryElements does for the same five
                // accessors. Without this an array holding "3 0 R" threw InvalidCastException where
                // the identical entry in a dictionary read back its value.
                case PdfReference reference:
                    obj = reference.Value;
                    break;
            }

            if (obj is PdfBoolean boolean)
                return boolean.Value;

            if (obj is PdfBooleanObject booleanObject)
                return booleanObject.Value;

            throw new InvalidCastException("GetBoolean: Object is not a boolean.");
        }

        /// <summary>
        /// Converts the specified value to integer.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// If the index is out of range, the function throws an ArgumentOutOfRangeException.
        /// </summary>
        public int GetInteger(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            object obj = this[index];
            if (obj is null or PdfNull)
                return 0;

            // Follow an indirect reference the way DictionaryElements does for the same five
            // accessors. Without this an array holding "3 0 R" threw InvalidCastException where
            // the identical entry in a dictionary read back its value.
            if (obj is PdfReference reference)
                obj = reference.Value;

            return obj switch
            {
                PdfInteger integer => integer.Value,
                PdfIntegerObject integerObject => integerObject.Value,
                _ => throw new InvalidCastException("GetInteger: Object is not an integer.")
            };
        }

        /// <summary>
        /// Converts the specified value to double.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// If the index is out of range, the function throws an ArgumentOutOfRangeException.
        /// </summary>
        public double GetReal(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            object obj = this[index];
            switch (obj)
            {
                case null or PdfNull:
                    return 0;
                // Follow an indirect reference the way DictionaryElements does for the same five
                // accessors. Without this an array holding "3 0 R" threw InvalidCastException where
                // the identical entry in a dictionary read back its value.
                case PdfReference reference:
                    obj = reference.Value;
                    break;
            }

            if (obj is PdfReal real)
                return real.Value;

            if (obj is PdfRealObject realObject)
                return realObject.Value;

            if (obj is PdfInteger integer)
                return integer.Value;

            if (obj is PdfIntegerObject integerObject)
                return integerObject.Value;

            throw new InvalidCastException("GetReal: Object is not a number.");
        }

        /// <summary>
        /// Converts the specified value to string.
        /// If the value does not exist, the function returns the empty string.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// If the index is out of range, the function throws an ArgumentOutOfRangeException.
        /// </summary>
        public string GetString(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            object obj = this[index];
            switch (obj)
            {
                case null or PdfNull:
                    return string.Empty;
                // Follow an indirect reference the way DictionaryElements does for the same five
                // accessors. Without this an array holding "3 0 R" threw InvalidCastException where
                // the identical entry in a dictionary read back its value.
                case PdfReference reference:
                    obj = reference.Value;
                    break;
            }

            if (obj is PdfString str)
                return str.Value;

            if (obj is PdfStringObject strObject)
                return strObject.Value;

            throw new InvalidCastException("GetString: Object is not a string.");
        }

        /// <summary>
        /// Converts the specified value to a name.
        /// If the value does not exist, the function returns the empty string.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// If the index is out of range, the function throws an ArgumentOutOfRangeException.
        /// </summary>
        public string GetName(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            object obj = this[index];
            switch (obj)
            {
                case null or PdfNull:
                    return string.Empty;
                // Follow an indirect reference the way DictionaryElements does for the same five
                // accessors. Without this an array holding "3 0 R" threw InvalidCastException where
                // the identical entry in a dictionary read back its value.
                case PdfReference reference:
                    obj = reference.Value;
                    break;
            }

            var name = obj as PdfName;
            if (name != null)
                return name.Value;

            var nameObject = obj as PdfNameObject;
            if (nameObject != null)
                return nameObject.Value;

            throw new InvalidCastException("GetName: Object is not a name.");
        }

        /// <summary>
        /// Gets the PdfObject with the specified index, or null, if no such object exists. If the index refers to
        /// a reference, the referenced PdfObject is returned.
        /// </summary>
        public PdfObject GetObject(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.IndexOutOfRange);

            var item = this[index];
            if (item is PdfReference reference)
                return reference.Value;

            return item as PdfObject;
        }

        /// <summary>
        /// Gets the PdfArray with the specified index, or null, if no such object exists. If the index refers to
        /// a reference, the referenced PdfArray is returned.
        /// </summary>
        public PdfDictionary GetDictionary(int index)
        {
            return GetObject(index) as PdfDictionary;
        }

        /// <summary>
        /// Gets the PdfArray with the specified index, or null, if no such object exists. If the index refers to
        /// a reference, the referenced PdfArray is returned.
        /// </summary>
        public PdfArray GetArray(int index)
        {
            return GetObject(index) as PdfArray;
        }

        /// <summary>
        /// Gets the PdfReference with the specified index, or null, if no such object exists.
        /// </summary>
        public PdfReference GetReference(int index)
        {
            var item = this[index];
            return item as PdfReference;
        }

        /// <summary>
        /// Gets all items of this array.
        /// </summary>
        public PdfItem[] Items => _elements.ToArray();

        #region IList Members

        /// <summary>
        /// Returns false.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets or sets an item at the specified index.
        /// </summary>
        /// <value></value>
        public PdfItem this[int index]
        {
            get => _elements[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _elements[index] = value;
                PdfObject.Contain(value, _ownerArray);
                MarkOwnerAsChanged();
            }
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        public void RemoveAt(int index)
        {
            _elements.RemoveAt(index);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the array/>.
        /// </summary>
        public bool Remove(PdfItem item)
        {
            var removed = _elements.Remove(item);
            if (removed)
                MarkOwnerAsChanged();
            return removed;
        }

        /// <summary>
        /// Inserts the item the specified index.
        /// </summary>
        public void Insert(int index, PdfItem value)
        {
            _elements.Insert(index, value);
            Contain(value, _ownerArray);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Determines whether the specified value is in the array.
        /// </summary>
        public bool Contains(PdfItem value)
        {
            return _elements.Contains(value);
        }

        /// <summary>
        /// Removes all items from the array.
        /// </summary>
        public void Clear()
        {
            _elements.Clear();
            MarkOwnerAsChanged();
        }


        /// <summary>
        /// Records that the object owning these elements has changed, so that an incremental save
        /// knows to write it out again. Every path that mutates the collection goes through here.
        /// </summary>
        /// <remarks>
        /// Through <see cref="PdfObject.MarkAsChanged"/>, which walks up to the nearest indirect
        /// object. An array held directly inside a page dictionary is not in the cross-reference
        /// table, so marking only the array would tell an incremental save nothing: adding an
        /// annotation to a page's <c>/Annots</c> has to make the <em>page</em> get written again.
        /// </remarks>
        private void MarkOwnerAsChanged() => _ownerArray?.MarkAsChanged();

        /// <summary>
        /// Gets the index of the specified item.
        /// </summary>
        public int IndexOf(PdfItem value)
        {
            return _elements.IndexOf(value);
        }

        /// <summary>
        /// Appends the specified object to the array.
        /// </summary>
        public void Add(PdfItem value)
        {
            if (value is PdfObject { IsIndirect: true } obj)
                _elements.Add(obj.Reference);
            else
                _elements.Add(value);
            Contain(value, _ownerArray);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Returns false.
        /// </summary>
#pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public bool IsFixedSize => false;
#pragma warning restore CA1822

        #endregion

        #region ICollection Members

        /// <summary>
        /// Returns false.
        /// </summary>
#pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public bool IsSynchronized => false;
#pragma warning restore CA1822

        /// <summary>
        /// Gets the number of elements in the array.
        /// </summary>
        public int Count => _elements.Count;

        /// <summary>
        /// Copies the elements of the array to the specified array.
        /// </summary>
        public void CopyTo(PdfItem[] array, int index)
        {
            _elements.CopyTo(array, index);
        }

        /// <summary>
        /// The current implementation return null.
        /// </summary>
#pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public object SyncRoot => null;
#pragma warning restore CA1822

        #endregion

        /// <summary>
        /// Returns an enumerator that iterates through the array.
        /// </summary>
        public IEnumerator<PdfItem> GetEnumerator()
        {
            return _elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _elements.GetEnumerator();
        }

        /// <summary>
        /// The elements of the array.
        /// </summary>
        private List<PdfItem> _elements;

        /// <summary>
        /// The array this objects belongs to.
        /// </summary>
        private PdfArray _ownerArray;
    }

    private ArrayElements _elements;

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSharper disable UnusedMember.Local
    private string DebuggerDisplay =>
        String.Format(CultureInfo.InvariantCulture, "array({0},[{1}])", ObjectID.DebuggerDisplay,
            _elements?.Count ?? 0); // ReSharper restore UnusedMember.Local
}
