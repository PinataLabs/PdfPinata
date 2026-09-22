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
using System.Diagnostics;

namespace PdfPinata.Pdf.Content.Objects;

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
