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
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the ASCIIHexDecode filter.
/// </summary>
public class AsciiHexDecode : Filter
{
    // Reference: 3.3.1  ASCIIHexDecode Filter / Page 69

    /// <summary>
    /// Encodes the specified data.
    /// </summary>
    public override byte[] Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var count = data.Length;
        var bytes = new byte[2 * count];
        for (int i = 0, j = 0; i < count; i++)
        {
            var b = data[i];
            bytes[j++] = (byte)((b >> 4) + (b >> 4 < 10 ? (byte)'0' : 'A' - 10));
            bytes[j++] = (byte)((b & 0xF) + ((b & 0xF) < 10 ? (byte)'0' : 'A' - 10));
        }
        return bytes;
    }

    /// <summary>
    /// Decodes the specified data.
    /// <para>
    /// ISO 32000-1 7.4.2: white space is ignored, <c>&gt;</c> is the end of the data wherever it
    /// appears and nothing after it is read, an odd number of digits before the end is read as
    /// though a 0 followed the last one, and any other character is an error.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The data holds a character that is neither a
    /// hexadecimal digit, white space nor the end-of-data marker.</exception>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Two digits to a byte, so half the input rounded up is as many bytes as there can be.
        var bytes = new byte[(data.Length + 1) / 2];
        var count = 0;
        var hi = -1;
        foreach (var ch in data)
        {
            if (ch == '>')
                break;

            int digit;
            if (ch >= '0' && ch <= '9')
                digit = ch - '0';
            else if (ch >= 'A' && ch <= 'F')
                digit = ch - 'A' + 10;
            else if (ch >= 'a' && ch <= 'f')
                digit = ch - 'a' + 10;
            else if (IsWhiteSpace(ch))
                continue;
            else
                throw new ArgumentException($"Illegal character 0x{ch:X2} in ASCIIHexDecode data.", nameof(data));

            if (hi < 0)
            {
                hi = digit;
            }
            else
            {
                bytes[count++] = (byte)(hi << 4 | digit);
                hi = -1;
            }
        }

        // "If the filter encounters the EOD marker after reading an odd number of hexadecimal
        // digits, it shall behave as if a 0 (zero) followed the last digit." Data that ends
        // without a marker is read the same way.
        if (hi >= 0)
            bytes[count++] = (byte)(hi << 4);

        if (count < bytes.Length)
            Array.Resize(ref bytes, count);
        return bytes;
    }

    // The six characters ISO 32000-1 Table 1 calls white space.
    private static bool IsWhiteSpace(byte ch) =>
        ch is (byte)Chars.NUL or (byte)Chars.HT or (byte)Chars.LF or (byte)Chars.FF or (byte)Chars.CR or (byte)Chars.SP;
}
