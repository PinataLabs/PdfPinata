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
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Content.Objects; // TODO: split into single files

/// <summary>
/// Base class for all PDF content stream objects.
/// </summary>
public abstract class CObject : ICloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CObject"/> class.
    /// </summary>
    protected CObject()
    {
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    object ICloneable.Clone()
    {
        return Copy();
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public CObject Clone()
    {
        return Copy();
    }

    /// <summary>
    /// Implements the copy mechanism. Must be overridden in derived classes.
    /// </summary>
    protected virtual CObject Copy()
    {
        return (CObject)MemberwiseClone();
    }

    /// <summary>
    ///
    /// </summary>
    internal abstract void WriteObject(ContentWriter writer);
}

/// <summary>
/// Represents a comment in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Text})")]
public class CComment : CObject
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CComment Clone()
    {
        return (CComment)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the comment text.
    /// </summary>
    public string Text
    {
        get => _text;
        set => _text = value;
    }

    string _text;

    /// <summary>
    /// Returns a string that represents the current comment.
    /// </summary>
    public override string ToString()
    {
        return "% " + _text;
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteLineRaw(ToString());
    }
}

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
    /// Adds the specified sequence.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <summary>
    /// Appends the <em>contents</em> of another sequence, not the sequence itself.
    /// </summary>
    /// <remarks>
    /// Worth knowing before reaching for it, because the overload below and the
    /// <c>IList&lt;CObject&gt;</c> one both add what they are handed as a single item, and
    /// <see cref="CArray"/> derives from this class - so adding an array through this overload
    /// spreads its items into the sequence and the brackets do not survive to be written. Kept as
    /// it is, spelling and all, because it is the shape upstream ships and a caller compiled
    /// against that is entitled to go on binding to it.
    /// </remarks>
    public void Add(CSequence sequence)
    {
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

    //bool IList.Contains(object value)
    //{
    //  return items.Contains(value);
    //}

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

    ///// <summary>
    ///// Gets a value indicating whether access to the sequence is synchronized (thread safe).
    ///// </summary>
    //public bool IsSynchronized
    //{
    //  get { return items.IsSynchronized; }
    //}

    ///// <summary>
    ///// Gets an object that can be used to synchronize access to the sequence.
    ///// </summary>
    //public object SyncRoot
    //{
    //  get { return items.SyncRoot; }
    //}

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

        for (var idx = 0; idx < _items.Count; idx++)
            s.Append(_items[idx]);

        return s.ToString();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal override void WriteObject(ContentWriter writer)
    {
        for (var idx = 0; idx < _items.Count; idx++)
            _items[idx].WriteObject(writer);
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

    List<CObject> _items = new();
}

/// <summary>
/// Represents the base class for numerical objects in a PDF content stream.
/// </summary>
public abstract class CNumber : CObject
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CNumber Clone()
    {
        return (CNumber)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    //internal override void WriteObject(ContentWriter writer)
    //{
    //  throw new Exception("Must not come here.");
    //}
}

/// <summary>
/// Represents an integer value in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Value})")]
public class CInteger : CNumber
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CInteger Clone()
    {
        return (CInteger)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public int Value
    {
        get => _value;
        set => _value = value;
    }

    int _value;

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString() + " ");
    }
}

/// <summary>
/// Represents a real value in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Value})")]
public class CReal : CNumber
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CReal Clone()
    {
        return (CReal)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public double Value
    {
        get => _value;
        set => _value = value;
    }

    double _value;

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    public override string ToString()
    {
        const string format = Config.SignificantFigures1Plus9;
        return _value.ToString(format, CultureInfo.InvariantCulture);
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString() + " ");
    }
}

/// <summary>
/// Type of the parsed string.
/// </summary>
public enum CStringType
{
    /// <summary>
    /// The string has the format "(...)".
    /// </summary>
    String,

    /// <summary>
    /// The string has the format "&lt;...&gt;".
    /// </summary>
    HexString,

    /// <summary>
    /// The string... TODO.
    /// </summary>
    UnicodeString,

    /// <summary>
    /// The string... TODO.
    /// </summary>
    UnicodeHexString,

    /// <summary>
    /// HACK: The string is the content of a dictionary.
    /// Currently there is no parser for dictionaries in Content Streams.
    /// </summary>
    Dictionary
}

/// <summary>
/// Represents a string value in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Value})")]
public class CString : CObject
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CString Clone()
    {
        return (CString)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value
    {
        get => _value;
        set => _value = value;
    }

    string _value;

    /// <summary>
    /// Gets or sets the type of the content string.
    /// </summary>
    public CStringType CStringType
    {
        get => _cStringType;
        set => _cStringType = value;
    }

    CStringType _cStringType;

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    public override string ToString()
    {
        var s = new StringBuilder();
        switch (CStringType)
        {
            case CStringType.String:
                s.Append('(');
                var length = _value.Length;
                for (var ich = 0; ich < length; ich++)
                {
                    var ch = _value[ich];
                    switch (ch)
                    {
                        case Chars.LF:
                            s.Append("\\n");
                            break;

                        case Chars.CR:
                            s.Append("\\r");
                            break;

                        case Chars.HT:
                            s.Append("\\t");
                            break;

                        case Chars.BS:
                            s.Append("\\b");
                            break;

                        case Chars.FF:
                            s.Append("\\f");
                            break;

                        case Chars.ParenLeft:
                            s.Append("\\(");
                            break;

                        case Chars.ParenRight:
                            s.Append("\\)");
                            break;

                        case Chars.BackSlash:
                            s.Append("\\\\");
                            break;

                        default:
                            s.Append(ch);
                            break;
                    }
                }

                s.Append(')');
                break;


            case CStringType.HexString:
                throw new NotImplementedException();
            //break;

            case CStringType.UnicodeString:
                throw new NotImplementedException();
            //break;

            case CStringType.UnicodeHexString:
                throw new NotImplementedException();
            //break;

            case CStringType.Dictionary:
                s.Append(_value);
                break;

            default:
                #pragma warning disable S3877 // An undefined CStringType has no textual form, as the unfinished Unicode cases above already say by throwing.
                throw new ArgumentOutOfRangeException();
                #pragma warning restore S3877
        }

        return s.ToString();
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString());
    }
}

/// <summary>
/// Represents a name in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Name})")]
public class CName : CObject
{
    private const string NamePrefix = "/";

    /// <summary>
    /// Initializes a new instance of the <see cref="CName"/> class.
    /// </summary>
    public CName()
    {
        _name = NamePrefix;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CName"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    public CName(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CName Clone()
    {
        return (CName)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the content stream name. Names must start with a slash.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">If <paramref name="value"/> does not start with a forward slash</exception>
    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));
            if (!value.StartsWith(NamePrefix))
                throw new ArgumentException(PSSR.NameMustStartWithSlash, nameof(value));
            _name = value;
        }
    }

    private string _name;

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    public override string ToString()
    {
        return _name;
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString() + " ");
    }
}

/// <summary>
/// Represents an array of objects in a PDF content stream.
/// </summary>
[DebuggerDisplay("(count={Count})")]
public class CArray : CSequence
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CArray Clone()
    {
        return (CArray)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    /// <remarks>
    /// Unlike the sequence it derives from, this is the array's written form, so its items have
    /// to be told apart when read back: two items are separated by a blank unless one of them
    /// delimits itself. A string and an array do, and so stay packed against their neighbours as
    /// in <c>[(A)-250(B)]</c>; a number or a name ends in a regular character, so <c>[3 2]</c>
    /// written without the blank would read back as the one number 32.
    /// </remarks>
    public override string ToString()
    {
        var s = new StringBuilder("[");
        CObject previous = null;
        foreach (var item in this)
        {
            if (previous != null && !DelimitsItself(previous) && !DelimitsItself(item))
                s.Append(' ');
            s.Append(item);
            previous = item;
        }

        s.Append(']');
        return s.ToString();
    }

    static bool DelimitsItself(CObject item)
    {
        return item is CString or CArray;
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString());
    }
}

/// <summary>
/// Represents an operator a PDF content stream.
/// </summary>
[DebuggerDisplay("({Name}, operands={Operands.Count})")]
public class COperator : CObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="COperator"/> class.
    /// </summary>
    protected COperator()
    {
    }

    internal COperator(OpCode opcode)
    {
        _opcode = opcode;
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new COperator Clone()
    {
        return (COperator)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var copy = (COperator)base.Copy();
        copy._seqence = _seqence?.Clone();
        return copy;
    }

    /// <summary>
    /// Gets or sets the name of the operator
    /// </summary>
    /// <value>The name.</value>
    public virtual string Name => _opcode.Name;

    /// <summary>
    /// Gets or sets the operands.
    /// </summary>
    /// <value>The operands.</value>
    public CSequence Operands => _seqence ?? (_seqence = new CSequence());

    CSequence _seqence;

    /// <summary>
    /// Gets the operator description for this instance.
    /// </summary>
    public OpCode OpCode => _opcode;

    readonly OpCode _opcode;


    /// <summary>
    /// Returns a string that represents the current operator.
    /// </summary>
    public override string ToString()
    {
        if (_opcode.OpCodeName == OpCodeName.Dictionary)
            return " ";

        return Name;
    }

    internal override void WriteObject(ContentWriter writer)
    {
        var count = _seqence?.Count ?? 0;
        for (var idx = 0; idx < count; idx++)
        {
            // ReSharper disable once PossibleNullReferenceException because the loop is not entered if _sequence is null
            _seqence[idx].WriteObject(writer);
        }

        writer.WriteLineRaw(ToString());
    }
}
