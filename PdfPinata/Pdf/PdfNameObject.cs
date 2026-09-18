#region PDFsharp - A .NET library for processing PDF
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
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents an indirect name value. This type is not used by PdfPinata. If it is imported from
/// an external PDF file, the value is converted into a direct object. Acrobat sometime uses indirect
/// names to save space, because an indirect reference to a name may be shorter than a long name.
/// </summary>
[DebuggerDisplay("({Value})")]
public sealed class PdfNameObject : PdfObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNameObject"/> class.
    /// </summary>
    public PdfNameObject()
    {
        _value = "/";  // Empty name.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNameObject"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The value.</param>
    public PdfNameObject(PdfDocument document, string value)
        : base(document)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || value[0] != '/')
            throw new ArgumentException(PSSR.NameMustStartWithSlash);

        _value = value;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object. A name whose value
    /// is null equals nothing at all.
    /// </summary>
    /// <remarks>
    /// <see cref="Value"/> is settable and may be set to null, so neither this nor
    /// <see cref="GetHashCode"/> may read it without asking first: an ordinary equality test, or
    /// putting the name in a hash set, would otherwise throw
    /// <see cref="System.NullReferenceException"/> rather than answer.
    /// </remarks>
    public override bool Equals(object obj)
    {
        return _value != null && _value.Equals(obj);
    }

    /// <summary>
    /// Serves as a hash function for this type.
    /// </summary>
    public override int GetHashCode()
    {
        return _value?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Gets or sets the name value.
    /// </summary>
    public string Value
    {
        get => _value;
        set => _value = value;
    }
    string _value;

    /// <summary>
    /// Returns the name. The string always begins with a slash.
    /// </summary>
    public override string ToString()
    {
        // TODO: Encode characters.
        return _value;
    }

    /// <summary>
    /// Determines whether a name is equal to a string. A null name equals a null string and
    /// nothing else.
    /// </summary>
    /// <remarks>
    /// The null check is what makes <c>nameObject != null</c> safe at a call site. An untyped
    /// null binds to this overload rather than to reference equality, so without it every such
    /// comparison dereferences the name it is testing for null and throws
    /// <see cref="NullReferenceException"/>. <see cref="PdfName"/>, which this is the indirect
    /// twin of, has always guarded it.
    /// </remarks>
    public static bool operator ==(PdfNameObject name, string str)
    {
        if (ReferenceEquals(name, null))
            return str == null;

        // Value is settable and may itself be null, which must not make the object that holds it
        // look like a null object: only the reference check above answers that question.
        return str != null && name._value == str;
    }

    /// <summary>
    /// Determines whether a name is not equal to a string. A null name differs from every string
    /// but null.
    /// </summary>
    public static bool operator !=(PdfNameObject name, string str)
    {
        return !(name == str);
    }

    /// <summary>
    /// Writes the name including the leading slash.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        writer.WriteBeginObject(this);
        writer.Write(new PdfName(_value));
        writer.WriteEndObject();
    }
}
