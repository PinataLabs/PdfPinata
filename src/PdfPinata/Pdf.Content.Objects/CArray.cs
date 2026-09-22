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

using System.Diagnostics;
using System.Text;

namespace PdfPinata.Pdf.Content.Objects;

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
