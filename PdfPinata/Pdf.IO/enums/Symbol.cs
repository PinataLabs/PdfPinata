#region PDFsharp - A .NET library for processing PDF
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

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Terminal symbols recognized by lexer.
/// </summary>
/// <remarks>
/// The twin of <see cref="PdfPinata.Pdf.Content.CSymbol"/>, which is what the content stream
/// lexer recognizes. The document body has structure a content stream has not — indirect objects,
/// streams, the cross-reference table — so this one has symbols for those and that one does not.
/// </remarks>
public enum Symbol
{
    /// <summary>No symbol has been scanned yet.</summary>
    None,
    /// <summary>A comment, from a percent sign to the end of the line.</summary>
    Comment,
    /// <summary>The <c>null</c> keyword.</summary>
    Null,
    /// <summary>An integer literal.</summary>
    Integer,
    /// <summary>An integer literal too large to be signed, scanned as unsigned.</summary>
    UInteger,
    /// <summary>A real literal.</summary>
    Real,
    /// <summary>The <c>true</c> or <c>false</c> keyword.</summary>
    Boolean,
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
    /// <summary>A keyword the lexer has no symbol of its own for.</summary>
    Keyword,
    /// <summary>The <c>stream</c> keyword, which begins the raw bytes of a stream.</summary>
    BeginStream,
    /// <summary>The <c>endstream</c> keyword.</summary>
    EndStream,
    /// <summary>An opening square bracket.</summary>
    BeginArray,
    /// <summary>A closing square bracket.</summary>
    EndArray,
    /// <summary>A double angle bracket opening a dictionary.</summary>
    BeginDictionary,
    /// <summary>A double angle bracket closing a dictionary.</summary>
    EndDictionary,
    /// <summary>The <c>obj</c> keyword, which begins an indirect object.</summary>
    Obj,
    /// <summary>The <c>endobj</c> keyword.</summary>
    EndObj,
    /// <summary>The <c>R</c> keyword, which makes the two integers before it an indirect reference.</summary>
    R,
    /// <summary>The <c>xref</c> keyword, which begins the cross-reference table.</summary>
    XRef,
    /// <summary>The <c>trailer</c> keyword.</summary>
    Trailer,
    /// <summary>The <c>startxref</c> keyword, followed by the offset of the cross-reference table.</summary>
    StartXRef,
    /// <summary>The end of the file.</summary>
    Eof,
    /// <summary>An integer literal too large for <see cref="Integer"/>, scanned as a 64-bit value.</summary>
    Long
}
