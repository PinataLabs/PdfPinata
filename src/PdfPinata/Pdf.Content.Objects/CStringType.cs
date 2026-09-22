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

namespace PdfPinata.Pdf.Content.Objects;

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
    /// The string has the format "(...)" and its bytes, once its escapes are resolved, open with a
    /// UTF-16 byte order mark - FE FF, or the little-endian FF FE Adobe Reader also accepts - so it
    /// is text rather than bytes. The value is the decoded text, without the byte order mark.
    /// </summary>
    /// <remarks>
    /// <see cref="CLexer"/> reports such a string as <see cref="CSymbol.UnicodeString"/>, but
    /// <see cref="CParser"/> does not carry the distinction over: it gives every string it reads
    /// <see cref="CStringType.String"/>. <see cref="CString.ToString"/> cannot write this type.
    /// </remarks>
    UnicodeString,

    /// <summary>
    /// The string has the format "&lt;...&gt;" and the bytes its digits spell open with the
    /// big-endian UTF-16 byte order mark FE FF, so it is text rather than bytes. The value is the
    /// decoded text, without the byte order mark.
    /// </summary>
    /// <remarks>
    /// <see cref="CLexer"/> reports such a string as <see cref="CSymbol.UnicodeHexString"/>, but
    /// <see cref="CParser"/> does not carry the distinction over: it gives every string it reads
    /// <see cref="CStringType.String"/>. <see cref="CString.ToString"/> cannot write this type.
    /// </remarks>
    UnicodeHexString,

    /// <summary>
    /// HACK: The string is the content of a dictionary.
    /// Currently there is no parser for dictionaries in Content Streams.
    /// </summary>
    Dictionary
}
