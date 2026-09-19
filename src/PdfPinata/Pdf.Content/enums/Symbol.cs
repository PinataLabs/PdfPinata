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

namespace PdfPinata.Pdf.Content;

/// <summary>
/// Terminal symbols recognized by PDF content stream lexer.
/// </summary>
public enum CSymbol
{
    /// <summary>No symbol has been scanned yet.</summary>
    None,
    /// <summary>A comment, from a percent sign to the end of the line.</summary>
    Comment,
    /// <summary>An integer literal.</summary>
    Integer,
    /// <summary>A real literal.</summary>
    Real,
    /*Boolean?,*/
    /// <summary>A literal string, written in parentheses.</summary>
    String,
    /// <summary>A string written in angle brackets as pairs of hexadecimal digits.</summary>
    HexString,
    /// <summary>A literal string whose bytes carry a byte order mark, so it is text rather than bytes.</summary>
    UnicodeString,
    /// <summary>A hexadecimal string whose bytes carry a byte order mark.</summary>
    UnicodeHexString,
    /// <summary>A name, written with a leading solidus.</summary>
    Name,
    /// <summary>A content stream operator.</summary>
    Operator,
    /// <summary>An opening square bracket.</summary>
    BeginArray,
    /// <summary>A closing square bracket.</summary>
    EndArray,
    /// <summary>A dictionary. Scanned as a string literal rather than parsed, which is why it is one symbol.</summary>
    Dictionary,  // HACK: << ... >> is scanned as string literal.
    /// <summary>The end of the content stream.</summary>
    Eof,
    /// <summary>The scanner could not make a symbol of what it read.</summary>
    Error = -1
}
