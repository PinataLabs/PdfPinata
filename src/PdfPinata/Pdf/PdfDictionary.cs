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
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Internal;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf;

/// <summary>
/// Value creation flags. Specifies whether and how a value that does not exist is created.
/// </summary>
// ReSharper disable InconsistentNaming
public enum VCF
    // ReSharper restore InconsistentNaming
{
    /// <summary>
    /// Don't create the value.
    /// </summary>
    None,

    /// <summary>
    /// Create the value as direct object.
    /// </summary>
    Create,

    /// <summary>
    /// Create the value as indirect object.
    /// </summary>
    CreateIndirect
}

/// <summary>
/// Represents a PDF dictionary object.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public class PdfDictionary : PdfObject, IEnumerable<KeyValuePair<string, PdfItem>>
{
    // Reference: 3.2.6  Dictionary Objects / Page 59

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class.
    /// </summary>
    public PdfDictionary()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfDictionary(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Initializes a new instance from an existing dictionary. Used for object type transformation.
    /// </summary>
    protected PdfDictionary(PdfDictionary dict)
        : base(dict)
    {
        dict._elements?.ChangeOwner(this);
        dict._stream?.ChangeOwner(this);
    }

    /// <summary>
    /// Creates a copy of this dictionary. Direct values are deep copied. Indirect references are not
    /// modified.
    /// </summary>
    public new PdfDictionary Clone()
    {
        return (PdfDictionary)Copy();
    }

    /// <summary>
    /// This function is useful for importing objects from external documents. The returned object is not
    /// yet complete. irefs refer to external objects and directed objects are cloned but their document
    /// property is null. A cloned dictionary or array needs a 'fix-up' to be a valid object.
    /// </summary>
    protected override object Copy()
    {
        var dict = (PdfDictionary)base.Copy();
        if (dict._elements != null)
        {
            dict._elements = dict._elements.Clone();
            dict._elements.ChangeOwner(dict);
            var names = dict._elements.KeyNames;
            foreach (var name in names)
            {
                if (dict._elements[name] is not PdfObject obj)
                    continue;

                obj = obj.Clone();
                // Recall that obj.Document is now null.
                dict._elements[name] = obj;
            }
        }
        if (dict._stream != null)
        {
            dict._stream = dict._stream.Clone();
            dict._stream.ChangeOwner(dict);
        }
        return dict;
    }

    /// <summary>
    /// Gets the dictionary containing the elements of this dictionary.
    /// </summary>
    public DictionaryElements Elements => _elements ??= new DictionaryElements(this);

    /// <summary>
    /// The elements of the dictionary.
    /// </summary>
    internal DictionaryElements _elements;

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary elements.
    /// </summary>
    public IEnumerator<KeyValuePair<string, PdfItem>> GetEnumerator()
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
        // Get keys and sort.
        var keys = Elements.KeyNames;
        var list = new List<PdfName>(keys);
        list.Sort(PdfName.Comparer);
        list.CopyTo(keys, 0);

        var pdf = new StringBuilder();
        pdf.Append("<< ");
        foreach (var key in keys)
            pdf.Append(key + " " + Elements[key] + " ");
        pdf.Append(">>");

        return pdf.ToString();
    }

    internal override void WriteObject(PdfWriter writer)
    {
        writer.WriteBeginObject(this);

        // The stream's own length is the one the file has to declare, whatever the entry says by
        // now. A stream keeps /Length current only in the dictionary that owns it, so one shared
        // with this dictionary through the Stream setter can have changed since it was assigned.
        // The writer encrypts the data as it writes it, after this entry, which holds only because
        // RC4 — the one cipher this library writes with — keeps the length. Before the keys are
        // listed, or an entry this adds would not be among them.
        DeclareStreamLength();
        var keys = Elements.KeyNames;

        foreach (var key in keys)
            WriteDictionaryElement(writer, key);
        if (Stream != null)
            WriteDictionaryStream(writer);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes a key/value pair of this dictionary. This function is intended to be overridden
    /// in derived classes.
    /// </summary>
    internal virtual void WriteDictionaryElement(PdfWriter writer, PdfName key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var item = Elements[key];
        if (item is PdfObject { IsIndirect: true } indirect)
        {
            // Replace an indirect object by its Reference. The Elements setter does this on the way
            // in, so getting here means something else put the object there.
            item = indirect.Reference;
        }
        key.WriteObject(writer);
        item.WriteObject(writer);
        writer.NewLine();
    }

    /// <summary>
    /// Writes the stream of this dictionary. This function is intended to be overridden
    /// in a derived class.
    /// </summary>
    internal virtual void WriteDictionaryStream(PdfWriter writer)
    {
        writer.WriteStream(this, (writer.Options & PdfWriterOptions.OmitStream) == PdfWriterOptions.OmitStream);
    }

    /// <summary>
    /// Gets or sets the PDF stream belonging to this dictionary. Returns null if the dictionary has
    /// no stream. To create the stream, call the CreateStream function.
    /// </summary>
    /// <remarks>
    /// Assigning a stream writes its length into <c>/Length</c>. A stream belonging to no
    /// dictionary, such as the one <see cref="PdfStream.Clone"/> answers, becomes this one's.
    /// </remarks>
    public PdfStream Stream
    {
        get => _stream;
        set
        {
            _stream = value;
            if (value == null)
                return;

            value.AdoptIfUnowned(this);
            DeclareStreamLength();
        }
    }
    private PdfStream _stream;

    /// <summary>
    /// Writes the stream's length into <c>/Length</c>, unless the entry already says it — so that
    /// a dictionary read from a file, whose entry is right, is not marked as changed.
    /// </summary>
    private void DeclareStreamLength()
    {
        if (_stream == null)
            return;

        var length = _stream.Length;
        if (Elements[PdfStream.Keys.Length] is PdfInteger declared && declared.Value == length)
            return;

        Elements.SetInteger(PdfStream.Keys.Length, length);
    }

    /// <summary>
    /// Creates the stream of this dictionary and initializes it with the specified byte array.
    /// The function must not be called if the dictionary already has a stream.
    /// </summary>
    public PdfStream CreateStream(byte[] value)
    {
        if (_stream != null)
            throw new InvalidOperationException("The dictionary already has a stream.");

        _stream = new PdfStream(value, this);
        // Always set the length.
        Elements[PdfStream.Keys.Length] = new PdfInteger(_stream.Length);
        return _stream;
    }

    /// <summary>
    /// When overridden in a derived class, gets the KeysMeta of this dictionary type.
    /// </summary>
    internal virtual DictionaryMeta Meta => null;

    /// <summary>
    /// Represents the interface to the elements of a PDF dictionary.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay}")]
    public sealed class DictionaryElements : IDictionary<string, PdfItem>, ICloneable
    {
        internal DictionaryElements(PdfDictionary ownerDictionary)
        {
            _elements = new Dictionary<string, PdfItem>();
            _ownerDictionary = ownerDictionary;
        }

        object ICloneable.Clone()
        {
            var dictionaryElements = (DictionaryElements)MemberwiseClone();
            dictionaryElements._elements = new Dictionary<string, PdfItem>(dictionaryElements._elements);
            dictionaryElements._ownerDictionary = null;
            return dictionaryElements;
        }

        /// <summary>
        /// Creates a shallow copy of this object. The clone is not owned by a dictionary anymore.
        /// </summary>
        public DictionaryElements Clone()
        {
            return (DictionaryElements)((ICloneable)this).Clone();
        }

        /// <summary>
        /// Moves this instance to another dictionary during object type transformation.
        /// </summary>
        internal void ChangeOwner(PdfDictionary ownerDictionary)
        {
            if (_ownerDictionary != null)
            {
                // ???
            }

            // Set new owner.
            _ownerDictionary = ownerDictionary;

            // Set owners elements to this.
            ownerDictionary._elements = this;
        }

        /// <summary>
        /// Gets the dictionary to which this elements object belongs to.
        /// </summary>
        internal PdfDictionary Owner => _ownerDictionary;

        /// <summary>
        ///   The entry under the given key, reading a null value as no entry at all. The
        ///   specification says that giving an entry the null object as its value is the same
        ///   as leaving the entry out, and that a reference to an object the file never defines
        ///   is that null object, so a dangling reference reads here as an absent entry rather
        ///   than as a value of the wrong type.
        /// </summary>
        /// <remarks>
        ///   The item itself is returned rather than what it refers to, because every caller
        ///   resolves the reference the way it needs to.
        /// </remarks>
        private PdfItem ValueOf(string key)
        {
            var item = this[key];
            var value = item is PdfReference reference ? reference.Value : item;
            return value is PdfNull or PdfNullObject ? null : item;
        }

        /// <summary>
        /// Converts the specified value to boolean.
        /// If the value does not exist, the function returns false.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public bool GetBoolean(string key, bool create)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = new PdfBoolean();
                return false;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfBoolean boolean)
                return boolean.Value;

            if (obj is PdfBooleanObject booleanObject)
                return booleanObject.Value;
            throw new InvalidCastException("GetBoolean: Object is not a boolean.");
        }

        /// <summary>
        /// Converts the specified value to boolean.
        /// If the value does not exist, the function returns false.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public bool GetBoolean(string key)
        {
            return GetBoolean(key, false);
        }

        /// <summary>
        /// Sets the entry to a direct boolean value.
        /// </summary>
        public void SetBoolean(string key, bool value)
        {
            this[key] = new PdfBoolean(value);
        }

        /// <summary>
        /// Converts the specified value to integer.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public int GetInteger(string key, bool create)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = new PdfInteger();
                return 0;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfInteger integer)
                return integer.Value;

            if (obj is PdfIntegerObject integerObject)
                return integerObject.Value;

            if (obj is PdfUInteger uinteger)
                return (int)uinteger.Value;
            throw new InvalidCastException("GetInteger: Object is not an integer.");
        }

        /// <summary>
        /// Converts the specified value to integer.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public int GetInteger(string key)
        {
            return GetInteger(key, false);
        }

        /// <summary>
        /// Sets the entry to a direct integer value.
        /// </summary>
        public void SetInteger(string key, int value)
        {
            this[key] = new PdfInteger(value);
        }

        /// <summary>
        /// Converts the specified value to double.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public double GetReal(string key, bool create)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = new PdfReal();
                return 0;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

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
        /// Converts the specified value to double.
        /// If the value does not exist, the function returns 0.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public double GetReal(string key)
        {
            return GetReal(key, false);
        }

        /// <summary>
        /// Sets the entry to a direct double value.
        /// </summary>
        public void SetReal(string key, double value)
        {
            this[key] = new PdfReal(value);
        }

        /// <summary>
        /// Converts the specified value to String.
        /// If the value does not exist, the function returns the empty string.
        /// </summary>
        public string GetString(string key, bool create)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = new PdfString();
                return "";
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfString str)
                return str.Value;

            if (obj is PdfStringObject strObject)
                return strObject.Value;

            var name = obj as PdfName;
            if (name != null)
                return name.Value;

            var nameObject = obj as PdfNameObject;
            if (nameObject != null)
                return nameObject.Value;

            throw new InvalidCastException("GetString: Object is not a string.");
        }

        /// <summary>
        /// Converts the specified value to String.
        /// If the value does not exist, the function returns the empty string.
        /// </summary>
        public string GetString(string key)
        {
            return GetString(key, false);
        }

        /// <summary>
        /// Tries to get the string.
        /// </summary>
        public bool TryGetString(string key, out string value)
        {
            value = null;
            object obj = ValueOf(key);
            if (obj == null)
                return false;

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfString str)
            {
                value = str.Value;
                return true;
            }

            if (obj is PdfStringObject strObject)
            {
                value = strObject.Value;
                return true;
            }

            var name = obj as PdfName;
            if (name != null)
            {
                value = name.Value;
                return true;
            }

            var nameObject = obj as PdfNameObject;
            if (nameObject != null)
            {
                value = nameObject.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the entry to a direct string value, encoded as Unicode unless ASCII can
        /// spell it. Entries holding bytes rather than text name
        /// <see cref="PdfStringEncoding.RawEncoding"/> instead.
        /// </summary>
        public void SetString(string key, string value)
        {
            this[key] = new PdfString(value);
        }

        /// <summary>
        /// Sets the entry to a string value with the specified encoding.
        /// </summary>
        public void SetString(string key, string value, PdfStringEncoding encoding)
        {
            this[key] = new PdfString(value, encoding);
        }

        /// <summary>
        /// Converts the specified value to a name.
        /// If the value does not exist, the function returns the empty string.
        /// </summary>
        public string GetName(string key)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                return string.Empty;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            var name = obj as PdfName;
            if (name != null)
                return name.Value;

            var nameObject = obj as PdfNameObject;
            if (nameObject != null)
                return nameObject.Value;

            throw new InvalidCastException("GetName: Object is not a name.");
        }

        /// <summary>
        /// Sets the specified name value.
        /// If the value doesn't start with a slash, it is added automatically.
        /// </summary>
        public void SetName(string key, string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Length == 0 || value[0] != '/')
                value = "/" + value;

            this[key] = new PdfName(value);
        }

        /// <summary>
        /// Converts the specified value to PdfRectangle.
        /// If the value does not exist, the function returns an empty rectangle.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public PdfRectangle GetRectangle(string key, bool create)
        {
            var value = new PdfRectangle();
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = value = new PdfRectangle();
                return value;
            }
            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfArray array && array.Elements.Count == 4)
            {
                value = new PdfRectangle(array.Elements.GetReal(0), array.Elements.GetReal(1),
                    array.Elements.GetReal(2), array.Elements.GetReal(3));
                this[key] = value;
            }
            else
            {
                value = (PdfRectangle)obj;
            }
            return value;
        }

        /// <summary>
        /// Converts the specified value to PdfRectangle.
        /// If the value does not exist, the function returns an empty rectangle.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public PdfRectangle GetRectangle(string key)
        {
            return GetRectangle(key, false);
        }

        /// <summary>
        /// Sets the entry to a direct rectangle value, represented by an array with four values.
        /// </summary>
        public void SetRectangle(string key, PdfRectangle rect)
        {
            _elements[key] = rect;
            MarkOwnerAsChanged();
        }

        /// Converts the specified value to XMatrix.
        /// If the value does not exist, the function returns an identity matrix.
        /// If the value is not convertible, the function throws an InvalidCastException.
        public XMatrix GetMatrix(string key, bool create)
        {
            var value = new XMatrix();
            object obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = PdfLiteral.FromMatrix(value);
                return value;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfArray array && array.Elements.Count == 6)
            {
                value = new XMatrix(array.Elements.GetReal(0), array.Elements.GetReal(1), array.Elements.GetReal(2),
                    array.Elements.GetReal(3), array.Elements.GetReal(4), array.Elements.GetReal(5));
            }
            else if (obj is PdfLiteral literal)
            {
                // A matrix is written as a literal, by SetMatrix and by the create branch above,
                // so this is the shape this method most often meets - including every /Matrix
                // PdfFormXObject, PdfShadingPattern and PdfGradientSoftMask write.
                value = MatrixFromLiteral(literal);
            }
            else
                throw new InvalidCastException("Element is not an array with 6 values.");
            return value;
        }

        /// <summary>
        /// The white space the numbers of a matrix literal are separated by.
        /// </summary>
        private static readonly char[] MatrixLiteralSeparators = [' ', '\t', '\r', '\n'];

        /// <summary>
        /// Reads the six numbers of a matrix written as the literal "[a b c d e f]".
        /// </summary>
        private static XMatrix MatrixFromLiteral(PdfLiteral literal)
        {
            var text = (literal.Value ?? "").Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
                text = text.Substring(1, text.Length - 2);

            var parts = text.Split(MatrixLiteralSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6)
                throw new InvalidCastException("Element is not an array with 6 values.");

            var numbers = new double[6];
            for (var index = 0; index < 6; index++)
            {
                if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]))
                    throw new InvalidCastException("Element is not an array with 6 values.");
            }
            return new XMatrix(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]);
        }

        /// Converts the specified value to XMatrix.
        /// If the value does not exist, the function returns an identity matrix.
        /// If the value is not convertible, the function throws an InvalidCastException.
        public XMatrix GetMatrix(string key)
        {
            return GetMatrix(key, false);
        }

        /// <summary>
        /// Sets the entry to a direct matrix value, represented by an array with six values.
        /// </summary>
        public void SetMatrix(string key, XMatrix matrix)
        {
            _elements[key] = PdfLiteral.FromMatrix(matrix);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Converts the specified value to DateTime.
        /// If the value does not exist, the function returns the specified default value.
        /// If the value is not convertible, the function throws an InvalidCastException.
        /// </summary>
        public DateTime GetDateTime(string key, DateTime defaultValue)
        {
            object obj = ValueOf(key);
            if (obj == null)
            {
                return defaultValue;
            }

            if (obj is PdfReference reference)
                obj = reference.Value;

            if (obj is PdfDate date)
                return date.Value;

            string strDate;
            if (obj is PdfString pdfString)
            {
                strDate = pdfString.Value;
            }
            else
            {
                if (obj is PdfStringObject stringObject)
                    strDate = stringObject.Value;
                else
                    throw new InvalidCastException("GetName: Object is not a name.");
            }

            if (strDate != "" && Parser.TryParseDateTime(strDate, out var parsed))
                return parsed;
            return defaultValue;
        }

        /// <summary>
        /// Sets the entry to a direct datetime value.
        /// </summary>
        public void SetDateTime(string key, DateTime value)
        {
            _elements[key] = new PdfDate(value);
            MarkOwnerAsChanged();
        }

        internal int GetEnumFromName(string key, object defaultValue, bool create)
        {
            if (defaultValue is not Enum)
                throw new ArgumentException("The default value must be an enumeration value.", nameof(defaultValue));

            var obj = ValueOf(key);
            if (obj == null)
            {
                if (create)
                    this[key] = new PdfName(defaultValue.ToString());

                // ReSharper disable once PossibleInvalidCastException because Enum objects can always be casted to int.
                return (int)defaultValue;
            }
            Debug.Assert(obj is PdfName);
            return (int)Enum.Parse(defaultValue.GetType(), obj.ToString().AsSpan(1), false);
        }

        internal int GetEnumFromName(string key, object defaultValue)
        {
            return GetEnumFromName(key, defaultValue, false);
        }

        internal void SetEnumAsName(string key, object value)
        {
            if (!(value is Enum))
                throw new ArgumentException("The value must be an enumeration value.", nameof(value));
            _elements[key] = new PdfName("/" + value);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Gets the value for the specified key. If the value does not exist, it is optionally created.
        /// </summary>
        public PdfItem GetValue(string key, VCF options)
        {
            var value = ValueOf(key);
            if (value == null)
            {
                if (options != VCF.None)
                {
                    var type = GetValueType(key);
                    if (type != null)
                    {
                        var typeInfo = type.GetTypeInfo();
                        Debug.Assert(typeof(PdfItem).GetTypeInfo().IsAssignableFrom(typeInfo), "Type not allowed.");
                        PdfObject obj;
                        if (typeof(PdfDictionary).GetTypeInfo().IsAssignableFrom(typeInfo))
                        {
                            value = obj = CreateDictionary(type, null);
                        }
                        else if (typeof(PdfArray).GetTypeInfo().IsAssignableFrom(typeInfo))
                        {
                            value = obj = CreateArray(type, null);
                        }
                        else
                            throw new NotImplementedException("Type other than array or dictionary.");
                        if (options == VCF.CreateIndirect)
                        {
                            _ownerDictionary.Owner._irefTable.Add(obj);
                            this[key] = obj.Reference;
                        }
                        else
                        {
                            this[key] = obj;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException("Cannot create value for key: " + key);
                    }
                }
            }
            else
            {
                // The value exists and can be returned. But for imported documents check for necessary
                // object type transformation.
                PdfReference iref;
                if ((iref = value as PdfReference) != null)
                {
                    // Case: value is an indirect reference.
                    value = iref.Value;
                    if (value == null)
                    {
                        // If we come here PDF file is corrupted.
                        throw new InvalidOperationException("Indirect reference without value.");
                    }

                    if (true) // || _owner.Document.IsImported)
                    {
                        var type = GetValueType(key);
                        var typeInfo = type.GetTypeInfo();
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (type != null && type != value.GetType())
                        {
                            if (typeof(PdfDictionary).GetTypeInfo().IsAssignableFrom(typeInfo))
                            {
                                value = CreateDictionary(type, (PdfDictionary)value);
                            }
                            else if (typeof(PdfArray).GetTypeInfo().IsAssignableFrom(typeInfo))
                            {
                                value = CreateArray(type, (PdfArray)value);
                            }
                            else
                                throw new NotImplementedException("Type other than array or dictionary.");
                        }
                    }
                    return value;
                }

                // Transformation is only possible after PDF import.
                if (true) // || _owner.Document.IsImported)
                {
                    // Case: value is a direct object
                    PdfDictionary dict;
                    if ((dict = value as PdfDictionary) != null)
                    {
                        Debug.Assert(!dict.IsIndirect);

                        var type = GetValueType(key);
                        if (dict.GetType() != type)
                            dict = CreateDictionary(type, dict);
                        return dict;
                    }

                    PdfArray array;
                    if ((array = value as PdfArray) != null)
                    {
                        Debug.Assert(!array.IsIndirect);

                        var type = GetValueType(key);
                        // This is more complicated. If type is null do nothing
                        if (type != null && type != array.GetType())
                            array = CreateArray(type, array);
                        return array;
                    }
                }
            }
            return value;
        }

        /// <summary>
        /// Shortcut for GetValue(key, VCF.None).
        /// </summary>
        public PdfItem GetValue(string key)
        {
            return GetValue(key, VCF.None);
        }

        /// <summary>
        /// Returns the type of the object to be created as value of the specified key.
        /// </summary>
        [return:DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        private Type GetValueType(string key)
        {
            Type type = null;
            var meta = _ownerDictionary.Meta;
            var kd = meta?[key];
            if (kd != null)
                type = kd.GetValueType();
            return type;
        }

        private PdfArray CreateArray([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]Type type, PdfArray oldArray)
        {
            PdfArray array = null;
            if (oldArray == null)
            {
                // Use constructor with signature 'Ctor(PdfDocument owner)'.
                var ctorInfos = type.GetTypeInfo().DeclaredConstructors;
                foreach (var ctorInfo in ctorInfos)
                {
                    var parameters = ctorInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PdfDocument))
                    {
                        array = ctorInfo.Invoke([_ownerDictionary.Owner]) as PdfArray;
                        break;
                    }
                }
            }
            else
            {
                // Use contstructor with signature 'Ctor(PdfDictionary dict)'.
                var ctorInfos = type.GetTypeInfo().DeclaredConstructors;
                foreach (var ctorInfo in ctorInfos)
                {
                    var parameters = ctorInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PdfArray))
                    {
                        array = ctorInfo.Invoke([oldArray]) as PdfArray;
                        break;
                    }
                }
            }

            Debug.Assert(array != null, "No appropriate constructor found for type: " + type.Name);
            return array;
        }

        private PdfDictionary CreateDictionary([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
            Type type, PdfDictionary oldDictionary)
        {
            PdfDictionary dict = null;
            if (oldDictionary == null)
            {
                // Use constructor with signature 'Ctor(PdfDocument owner)'.
                var ctorInfos = type.GetTypeInfo().DeclaredConstructors;
                foreach (var ctorInfo in ctorInfos)
                {
                    var parameters = ctorInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PdfDocument))
                    {
                        dict = ctorInfo.Invoke([_ownerDictionary.Owner]) as PdfDictionary;
                        break;
                    }
                }
            }
            else
            {
                var ctorInfos = type.GetTypeInfo().DeclaredConstructors;
                foreach (var ctorInfo in ctorInfos)
                {
                    var parameters = ctorInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PdfDictionary))
                    {
                        dict = ctorInfo.Invoke([oldDictionary]) as PdfDictionary;
                        break;
                    }
                }
            }

            Debug.Assert(dict != null, "No appropriate constructor found for type: " + type.Name);
            return dict;
        }

        /// <summary>
        /// Sets the entry with the specified value. DON'T USE THIS FUNCTION - IT MAY BE REMOVED.
        /// </summary>
        public void SetValue(string key, PdfItem value)
        {
            Debug.Assert(value is not PdfObject { Reference: not null },
                "You try to set an indirect object directly into a dictionary.");

            // HACK?
            _elements[key] = value;
            Contain(value, _ownerDictionary);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Gets the PdfObject with the specified key, or null, if no such object exists. If the key refers to
        /// a reference, the referenced PdfObject is returned.
        /// </summary>
        public PdfObject GetObject(string key)
        {
            var item = ValueOf(key);
            if (item is PdfReference reference)
                return reference.Value;
            return item as PdfObject;
        }

        /// <summary>
        /// Gets the PdfDictionary with the specified key, or null, if no such object exists. If the key refers to
        /// a reference, the referenced PdfDictionary is returned.
        /// </summary>
        public PdfDictionary GetDictionary(string key)
        {
            return GetObject(key) as PdfDictionary;
        }

        /// <summary>
        /// Gets the PdfArray with the specified key, or null, if no such object exists. If the key refers to
        /// a reference, the referenced PdfArray is returned.
        /// </summary>
        public PdfArray GetArray(string key)
        {
            return GetObject(key) as PdfArray;
        }

        /// <summary>
        /// Gets the PdfReference with the specified key, or null, if no such object exists.
        /// </summary>
        public PdfReference GetReference(string key)
        {
            var item = ValueOf(key);
            return item as PdfReference;
        }

        /// <summary>
        /// Sets the entry to the specified object. The object must not be an indirect object,
        /// otherwise an exception is raised.
        /// </summary>
        public void SetObject(string key, PdfObject obj)
        {
            if (obj.Reference != null)
                throw new ArgumentException("PdfObject must not be an indirect object.", nameof(obj));
            this[key] = obj;
        }

        /// <summary>
        /// Sets the entry as a reference to the specified object. The object must be an indirect object,
        /// otherwise an exception is raised.
        /// </summary>
        public void SetReference(string key, PdfObject obj)
        {
            if (obj.Reference == null)
                throw new ArgumentException("PdfObject must be an indirect object.", nameof(obj));
            this[key] = obj.Reference;
        }

        /// <summary>
        /// Sets the entry as a reference to the specified iref.
        /// </summary>
        public void SetReference(string key, PdfReference iref)
        {
            ArgumentNullException.ThrowIfNull(iref);
            this[key] = iref;
        }

        #region IDictionary Members

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary"></see> object is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Returns an <see cref="T:System.Collections.IDictionaryEnumerator"></see> object for the <see cref="T:System.Collections.IDictionary"></see> object.
        /// </summary>
        public IEnumerator<KeyValuePair<string, PdfItem>> GetEnumerator()
        {
            return _elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ICollection)_elements).GetEnumerator();
        }

        /// <summary>
        /// Gets or sets an entry in the dictionary. The specified key must be a valid PDF name
        /// starting with a slash '/'. This property provides full access to the elements of the
        /// PDF dictionary. Wrong use can lead to errors or corrupt PDF files.
        /// </summary>
        public PdfItem this[string key]
        {
            get
            {
                _elements.TryGetValue(key, out var item);
                return item;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (value is PdfObject { IsIndirect: true } obj)
                    value = obj.Reference;
                _elements[key] = value;
                Contain(value, _ownerDictionary);
                MarkOwnerAsChanged();
            }
        }

        /// <summary>
        /// Gets or sets an entry in the dictionary identified by a PdfName object.
        /// </summary>
        public PdfItem this[PdfName key]
        {
            get { return this[key.Value]; }
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                // An indirect object is stored as its reference and so is never a direct value:
                // it has to be replaced before asking whether what is left can be one. Asking
                // first rejected an indirect stream - a content stream, an image - that the
                // this[string] overload beside this one stores without complaint.
                if (value is PdfObject { IsIndirect: true } obj)
                    value = obj.Reference;
                else if (value is PdfDictionary { _stream: not null })
                    throw new ArgumentException("A dictionary with stream cannot be a direct value.");

                _elements[key.Value] = value;
                Contain(value, _ownerDictionary);
                MarkOwnerAsChanged();
            }
        }

        /// <summary>
        /// Removes the value with the specified key.
        /// </summary>
        public bool Remove(string key)
        {
            var removed = _elements.Remove(key);
            if (removed)
                MarkOwnerAsChanged();
            return removed;
        }


        /// <summary>
        /// Records that the object owning these elements has changed, so that an incremental save
        /// knows to write it out again.
        /// </summary>
        /// <remarks>
        /// Every path that mutates the collection has to call this, and six of them once did not:
        /// <c>SetRectangle</c>, <c>SetMatrix</c>, <c>SetDateTime</c>, <c>SetEnumAsName</c>,
        /// <c>SetValue</c> and the <see cref="PdfName"/> indexer all wrote to the backing dictionary
        /// directly. An object changed only through one of those was reported clean, so the appended
        /// revision did not contain it and the reader went on resolving the old definition — the
        /// change lost with no error. Page boxes go through <c>SetRectangle</c> and <c>/ModDate</c>
        /// through <c>SetDateTime</c>, so it was reachable from ordinary edits.
        /// </remarks>
        private void MarkOwnerAsChanged() => _ownerDictionary?.MarkAsChanged();

        /// <summary>
        /// Removes the value with the specified key.
        /// </summary>
        public bool Remove(KeyValuePair<string, PdfItem> item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified name.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return _elements.ContainsKey(key);
        }

        /// <summary>
        /// Determines whether the dictionary contains a specific value.
        /// </summary>
        public bool Contains(KeyValuePair<string, PdfItem> item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all elements from the dictionary.
        /// </summary>
        public void Clear()
        {
            _elements.Clear();
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Adds the specified value to the dictionary.
        /// </summary>
        public void Add(string key, PdfItem value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (key[0] != '/')
                throw new ArgumentException("The key must start with a slash '/'.");

            // If object is indirect automatically convert value to reference.
            if (value is PdfObject { IsIndirect: true } obj)
                value = obj.Reference;

            _elements.Add(key, value);
            Contain(value, _ownerDictionary);
            MarkOwnerAsChanged();
        }

        /// <summary>
        /// Adds an item to the dictionary.
        /// </summary>
        public void Add(KeyValuePair<string, PdfItem> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// Gets all keys currently in use in this dictionary as an array of PdfName objects.
        /// </summary>
        public PdfName[] KeyNames
        {
            get
            {
                ICollection values = _elements.Keys;
                var count = values.Count;
                var strings = new string[count];
                values.CopyTo(strings, 0);
                var names = new PdfName[count];
                for (var idx = 0; idx < count; idx++)
                    names[idx] = new PdfName(strings[idx]);
                return names;
            }
        }

        /// <summary>
        /// Get all keys currently in use in this dictionary as an array of string objects.
        /// </summary>
        public ICollection<string> Keys
        {
            // It is by design not to return _elements.Keys, but a copy.
            get
            {
                ICollection values = _elements.Keys;
                var count = values.Count;
                var keys = new string[count];
                values.CopyTo(keys, 0);
                return keys;
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        public bool TryGetValue(string key, out PdfItem value)
        {
            return _elements.TryGetValue(key, out value);
        }

        /// <summary>
        /// Gets all values currently in use in this dictionary as an array of PdfItem objects.
        /// </summary>
        public ICollection<PdfItem> Values
        {
            // It is by design not to return _elements.Values, but a copy.
            get
            {
                ICollection values = _elements.Values;
                var items = new PdfItem[values.Count];
                values.CopyTo(items, 0);
                return items;
            }
        }

        /// <summary>
        /// Return false.
        /// </summary>
        #pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public bool IsFixedSize => false;
        #pragma warning restore CA1822

        #endregion

        #region ICollection Members

        /// <summary>
        /// Return false.
        /// </summary>
        #pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public bool IsSynchronized => false;
        #pragma warning restore CA1822

        /// <summary>
        /// Gets the number of elements contained in the dictionary.
        /// </summary>
        public int Count => _elements.Count;

        /// <summary>
        /// Copies the elements of the dictionary to an array, starting at a particular index.
        /// </summary>
        public void CopyTo(KeyValuePair<string, PdfItem>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The current implementation returns null.
        /// </summary>
        #pragma warning disable CA1822 // Public API: making it static would break every caller that reads it through an instance.
        public object SyncRoot => null;
        #pragma warning restore CA1822

        #endregion

        /// <summary>
        /// Gets the DebuggerDisplayAttribute text.
        /// </summary>
        // ReSharper disable UnusedMember.Local
        internal string DebuggerDisplay
            // ReSharper restore UnusedMember.Local
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendFormat(CultureInfo.InvariantCulture, "key={0}:(", _elements.Count);
                var addSpace = false;
                ICollection<string> keys = _elements.Keys;
                foreach (var key in keys)
                {
                    if (addSpace)
                        sb.Append(' ');
                    addSpace = true;
                    sb.Append(key);
                }
                sb.Append(')');
                return sb.ToString();
            }
        }

        /// <summary>
        /// The elements of the dictionary with a string as key.
        /// Because the string is a name it starts always with a '/'.
        /// </summary>
        private Dictionary<string, PdfItem> _elements;

        /// <summary>
        /// The dictionary this objects belongs to.
        /// </summary>
        private PdfDictionary _ownerDictionary;
    }

    /// <summary>
    /// The PDF stream objects.
    /// </summary>
    public sealed class PdfStream
    {
        internal PdfStream(PdfDictionary ownerDictionary)
        {
            ArgumentNullException.ThrowIfNull(ownerDictionary);
            _ownerDictionary = ownerDictionary;
        }

        /// <summary>
        /// A .NET string can contain char(0) as a valid character.
        /// </summary>
        internal PdfStream(byte[] value, PdfDictionary owner)
            : this(owner)
        {
            _value = value;
        }

        /// <summary>
        /// Clones this stream by creating a deep copy.
        /// </summary>
        public PdfStream Clone()
        {
            var stream = (PdfStream)MemberwiseClone();
            stream._ownerDictionary = null;
            if (stream._value != null)
            {
                stream._value = new byte[stream._value.Length];
                _value.CopyTo(stream._value, 0);
            }
            return stream;
        }

        /// <summary>
        /// Moves this instance to another dictionary during object type transformation.
        /// </summary>
        internal void ChangeOwner(PdfDictionary dict)
        {
            if (_ownerDictionary != null)
            {
                // ???
            }

            // Set new owner.
            _ownerDictionary = dict;

            // Set owners stream to this.
            _ownerDictionary._stream = this;
        }

        /// <summary>
        /// Makes <paramref name="dict"/> the owner of a stream that has none, which is what
        /// <see cref="Clone"/> answers. A stream that already belongs to a dictionary keeps it.
        /// </summary>
        internal void AdoptIfUnowned(PdfDictionary dict)
        {
            _ownerDictionary ??= dict;
        }

        /// <summary>
        /// The dictionary the stream belongs to.
        /// </summary>
        private PdfDictionary _ownerDictionary;

        /// <summary>
        /// Gets the length of the stream, i.e. the actual number of bytes in the stream.
        /// </summary>
        public int Length => _value != null ? _value.Length : 0;

        /// <summary>
        /// Get or sets the bytes of the stream as they are, i.e. if one or more filters exist the bytes are
        /// not unfiltered.
        /// </summary>
        public byte[] Value
        {
            get => _value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _value = value;
                _ownerDictionary.Elements.SetInteger(Keys.Length, value.Length);
            }
        }
        private byte[] _value;

        /// <summary>
        /// Gets the value of the stream unfiltered. The stream content is not modified by this operation.
        /// </summary>
        public byte[] UnfilteredValue
        {
            get
            {
                byte[] bytes = null;
                if (_value != null)
                {
                    var filter = _ownerDictionary.Elements[Keys.Filter];
                    if (filter != null)
                    {
                        var decodeParms = _ownerDictionary.Elements[Keys.DecodeParms];
                        bytes = Filtering.Decode(_value, filter, decodeParms);
                        if (bytes == null)
                        {
                            var message = $"«Cannot decode filter '{filter}'»";
                            bytes = PdfEncoders.RawEncoding.GetBytes(message);
                        }
                    }
                    else
                    {
                        bytes = new byte[_value.Length];
                        _value.CopyTo(bytes, 0);
                    }
                }
                return bytes ?? [];
            }
        }

        /// <summary>
        /// The file this stream says its data is really in, or null when it says nothing about one.
        /// <para>
        /// ISO 32000-1 Table 5 lets a stream keep its data in another file, named by <c>/F</c>; the
        /// bytes between the keywords are then to be ignored, and the filters to apply to the file
        /// are <c>/FFilter</c> rather than <c>/Filter</c>. Nothing here goes and fetches that file.
        /// Where it is, and what a document may make a reader do by naming it, is the application's
        /// decision rather than this library's — see <c>docs/specs/external-file-streams.md</c>.
        /// </para>
        /// <para>
        /// A <c>/F</c> the document holds as a dictionary is answered as itself, so the same
        /// specification comes back each time and writing to it writes to the document. A <c>/F</c>
        /// written as a bare name — the simple form of 7.11.2 — is answered as a specification
        /// carrying that name and nothing else, made on the spot and standing outside the document:
        /// holding it would mean turning the name into a dictionary, which is not this property's
        /// business to do on a read. It is a way of reading where the data is, which is what the
        /// entry is for.
        /// </para>
        /// </summary>
        public PdfFileSpecification ExternalFile
        {
            get
            {
                var entry = _ownerDictionary.Elements[Keys.F];
                if (entry == null)
                    return null;

                if (entry is PdfString name)
                {
                    // A file specification string stays one byte per character, the way FileName
                    // writes one and the way the file holds it.
                    var carrier = new PdfDictionary();
                    carrier.Elements.SetString(PdfFileSpecification.Keys.F, name.Value,
                        PdfStringEncoding.RawEncoding);
                    return new PdfFileSpecification(carrier);
                }

                var specification = PdfAttachments.Resolve(entry);
                // Resolving transforms the dictionary into the specification, and re-points the
                // reference at it — so a specification that is an object of its own is found again
                // by itself next time. One written out inside this dictionary has no reference to
                // re-point, and has to be put back under the key for that to hold.
                if (specification != null && entry is not PdfReference)
                    _ownerDictionary.Elements[Keys.F] = specification;

                return specification;
            }
        }

        /// <summary>
        /// Tries to unfilter the bytes of the stream. If the stream is filtered and PDFsharp knows the filter
        /// algorithm, the stream content is replaced by its unfiltered value and the function returns true.
        /// Otherwise the content remains untouched and the function returns false.
        /// The function is useful for analyzing existing PDF files.
        /// </summary>
        public bool TryUnfilter()
        {
            if (_value == null)
                return true;

            var filter = _ownerDictionary.Elements[Keys.Filter];
            if (filter == null)
                return true;

            var decodeParms = _ownerDictionary.Elements[Keys.DecodeParms];
            // PDFsharp can only uncompress streams that are compressed with the ZIP or LZH algorithm.
            var bytes = Filtering.Decode(_value, filter, decodeParms);
            if (bytes != null)
            {
                _ownerDictionary.Elements.Remove(Keys.Filter);
                _ownerDictionary.Elements.Remove(Keys.DecodeParms);
                Value = bytes;
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Compresses the stream with the FlateDecode filter.
        /// If a filter is already defined, the function has no effect.
        /// </summary>
        public void Zip()
        {
            if (_value == null)
                return;

            if (_ownerDictionary.Elements.ContainsKey(Keys.Filter))
                return;

            _value = Filtering.FlateDecode.Encode(_value, _ownerDictionary._document.Options.FlateEncodeMode);
            _ownerDictionary.Elements[Keys.Filter] = new PdfName("/FlateDecode");
            _ownerDictionary.Elements[Keys.Length] = new PdfInteger(_value.Length);
        }

        /// <summary>
        /// Returns the stream content as a raw string.
        /// </summary>
        public override string ToString()
        {
            if (_value == null)
                return "«null»";

            string stream;
            var filter = _ownerDictionary.Elements[Keys.Filter];
            if (filter != null)
            {
                var decodeParms = _ownerDictionary.Elements[Keys.DecodeParms];
                var bytes = Filtering.Decode(_value, filter, decodeParms);
                if (bytes != null)
                    stream = PdfEncoders.RawEncoding.GetString(bytes, 0, bytes.Length);
                else
                    throw new NotImplementedException("Unknown filter");
            }
            else
            {
                stream = PdfEncoders.RawEncoding.GetString(_value, 0, _value.Length);
            }

            return stream;
        }

        /// <summary>
        /// Common keys for all streams.
        /// </summary>
        public class Keys : KeysBase
        {
            // ReSharper disable InconsistentNaming

            /// <summary>
            /// (Required) The number of bytes from the beginning of the line following the keyword
            /// stream to the last byte just before the keyword endstream. (There may be an additional
            /// EOL marker, preceding endstream, that is not included in the count and is not logically
            /// part of the stream data.)
            /// </summary>
            [KeyInfo(KeyType.Integer | KeyType.Required)]
            public const string Length = "/Length";

            /// <summary>
            /// (Optional) The name of a filter to be applied in processing the stream data found between
            /// the keywords stream and endstream, or an array of such names. Multiple filters should be
            /// specified in the order in which they are to be applied.
            /// </summary>
            [KeyInfo(KeyType.NameOrArray | KeyType.Optional)]
            public const string Filter = "/Filter";

            /// <summary>
            /// (Optional) A parameter dictionary or an array of such dictionaries, used by the filters
            /// specified by Filter. If there is only one filter and that filter has parameters, DecodeParms
            /// must be set to the filter’s parameter dictionary unless all the filter’s parameters have
            /// their default values, in which case the DecodeParms entry may be omitted. If there are
            /// multiple filters and any of the filters has parameters set to nondefault values, DecodeParms
            /// must be an array with one entry for each filter: either the parameter dictionary for that
            /// filter, or the null object if that filter has no parameters (or if all of its parameters have
            /// their default values). If none of the filters have parameters, or if all their parameters
            /// have default values, the DecodeParms entry may be omitted.
            /// </summary>
            [KeyInfo(KeyType.ArrayOrDictionary | KeyType.Optional)]
            public const string DecodeParms = "/DecodeParms";

            /// <summary>
            /// (Optional; PDF 1.2) The file containing the stream data. If this entry is present, the bytes
            /// between stream and endstream are ignored, the filters are specified by FFilter rather than
            /// Filter, and the filter parameters are specified by FDecodeParms rather than DecodeParms.
            /// However, the Length entry should still specify the number of those bytes. (Usually, there are
            /// no bytes and Length is 0.)
            /// </summary>
            [KeyInfo("1.2", KeyType.String | KeyType.Optional)]
            public const string F = "/F";

            /// <summary>
            /// (Optional; PDF 1.2) The name of a filter to be applied in processing the data found in the
            /// stream’s external file, or an array of such names. The same rules apply as for Filter.
            /// </summary>
            [KeyInfo("1.2", KeyType.NameOrArray | KeyType.Optional)]
            public const string FFilter = "/FFilter";

            /// <summary>
            /// (Optional; PDF 1.2) A parameter dictionary, or an array of such dictionaries, used by the
            /// filters specified by FFilter. The same rules apply as for DecodeParms.
            /// </summary>
            [KeyInfo("1.2", KeyType.ArrayOrDictionary | KeyType.Optional)]
            public const string FDecodeParms = "/FDecodeParms";

            /// <summary>
            /// Optional; PDF 1.5) A non-negative integer representing the number of bytes in the decoded
            /// (defiltered) stream. It can be used to determine, for example, whether enough disk space is
            /// available to write a stream to a file.
            /// This value should be considered a hint only; for some stream filters, it may not be possible
            /// to determine this value precisely.
            /// </summary>
            [KeyInfo("1.5", KeyType.Integer | KeyType.Optional)]
            public const string DL = "/DL";

            // ReSharper restore InconsistentNaming
        }
    }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSharper disable UnusedMember.Local
    private string DebuggerDisplay => string.Format(CultureInfo.InvariantCulture, "dictionary({0},[{1}])={2}",
        ObjectID.DebuggerDisplay,
        Elements.Count,
        _elements.DebuggerDisplay); // ReSharper restore UnusedMember.Local
}
