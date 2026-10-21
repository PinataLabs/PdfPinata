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
using System.Globalization;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a direct real value.
/// </summary>
[DebuggerDisplay("({Value})")]
public sealed class PdfReal : PdfNumber
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfReal"/> class.
    /// </summary>
    public PdfReal()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfReal"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is infinite, not a number, or outside the range of a
    /// single-precision real, none of which a PDF file can say (ISO 32000-1 Annex C).
    /// </exception>
    public PdfReal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                "{0} cannot be written as a PDF real, which has to be a finite number no larger in magnitude than {1}.",
                value, float.MaxValue), nameof(value));
        }
        Value = value;
    }

    /// <summary>
    /// A real read from a file, taken as the file said it. The parser cannot produce infinity or
    /// a NaN from a run of digits, but it can produce a number larger than a PDF real may be, and
    /// refusing it would make the whole document unreadable for one out-of-range value.
    /// </summary>
    private PdfReal(double value, bool _)
    {
        Value = value;
    }

    /// <summary>
    /// A real the parser has read, kept even when it is out of range.
    /// </summary>
    internal static PdfReal FromFile(double value) => new(value, false);

    /// <summary>
    /// Gets the value as double.
    /// </summary>
    // This class must behave like a value type. Therefore it cannot be changed (like System.String).
    public double Value { get; }

    /// <summary>
    /// Returns the real number as string.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString(Config.SignificantFigures3, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes the real value with up to three digits.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        writer.Write(this);
    }
}
