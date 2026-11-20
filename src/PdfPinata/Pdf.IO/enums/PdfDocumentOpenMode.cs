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

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Determines how a PDF document is opened. 
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member states its value, and 3 is deliberately missing.</b> 3 was
/// <c>InformationOnly</c>, a mode that named a fast partial read which was never written; it was
/// removed rather than left offering something it did not do. The values are written out because the
/// C# compiler inlines an enum constant at the call site, so an assembly compiled against an earlier
/// version goes on passing the number it was compiled with: letting <see cref="Append"/> slide from
/// 4 to 3 to close the gap would have silently redirected every such caller into the removed mode's
/// place. An old assembly passing 3 now hands over a value this enum does not define, which
/// <see cref="PdfDocument.IsReadOnly"/> answers as read-only - the same behaviour
/// <c>InformationOnly</c> always had.
/// </para>
/// <para>
/// A new member goes after the last one with the next free number, and 3 stays vacant.
/// </para>
/// </remarks>
public enum PdfDocumentOpenMode
{
    /// <summary>
    /// The PDF stream is completely read into memory and can be modified. Pages can be deleted or
    /// inserted, but it is not possible to extract pages. This mode is useful for modifying an
    /// existing PDF document.
    /// </summary>
    Modify = 0,

    /// <summary>
    /// The PDF stream is opened for importing pages from it. A document opened in this mode cannot
    /// be modified.
    /// </summary>
    Import = 1,

    /// <summary>
    /// The PDF stream is completely read into memory, but cannot be modified. This mode preserves the
    /// original internal structure of the document and is useful for analyzing existing PDF files.
    /// </summary>
    ReadOnly = 2,

    // 3 was InformationOnly. Left vacant on purpose - see the remarks on the enum.

    /// <summary>
    /// As <see cref="Modify"/>, but the document keeps the bytes it was read from and the object
    /// numbers it was read with, so that <see cref="PdfDocument.SaveIncremental(System.IO.Stream)"/>
    /// can append a new revision to it rather than rewrite it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Modify"/> cannot be used for this, and the reason is easy to miss: opening in that
    /// mode compacts the cross-reference table and <em>renumbers every object from one</em>. An
    /// incremental update shadows an object by writing a new definition under the same number, so a
    /// document whose numbers have been reassigned can no longer be appended to at all.
    /// </para>
    /// <para>
    /// The cost is memory. The original bytes are held for the lifetime of the document, so opening
    /// a 200 MB file this way holds 200 MB — which is exactly the case incremental update exists to
    /// help with, and still worth knowing before choosing this mode by default.
    /// </para>
    /// <para>
    /// <b>4 on purpose.</b> It has always been 4, and it stays 4 now that 3 is vacant: an assembly
    /// compiled against an earlier version passes the number it was compiled with, so closing the
    /// gap would silently redirect every such caller. Add a new member after the last one, never in
    /// the middle, and never into 3.
    /// </para>
    /// </remarks>
    Append = 4,

    /// <summary>
    /// The PDF is opened for reading and for importing pages from it, and each object is read from
    /// the file when it is first asked for. Opening reads the cross-reference sections and nothing
    /// else, so what a document costs grows with what is taken from it and not with the file. A
    /// document opened in this mode cannot be modified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The document reads from its source for as long as it lives.</b>
    /// <see cref="PdfReader.Open(string, PdfDocumentOpenMode)"/> keeps the file open and closes it
    /// when the document is disposed, so dispose the document. A stream handed to
    /// <see cref="PdfReader.Open(System.IO.Stream, PdfDocumentOpenMode)"/> stays the caller's: it
    /// has to be able to seek, has to stay open until the document is disposed, and is not closed
    /// by it. Once the document is disposed, an object that had been read still answers and one
    /// that had not throws an <see cref="System.ObjectDisposedException"/>.
    /// </para>
    /// <para>
    /// <b>What is read, and when.</b> Asking for the page count, or for any page, reads every node
    /// of the page tree and every page dictionary, and no content stream, image or font. An object
    /// read once is kept for the document's lifetime, <b>its stream data with it</b>: reading a
    /// page's content, or importing a page, holds every stream that page reaches. Memory therefore
    /// grows with what has been touched. A stream longer than 2 GiB cannot be read.
    /// </para>
    /// <para>
    /// <b>What it refuses.</b> An encrypted document, which has to be opened with
    /// <see cref="Import"/>. A stream that cannot seek. And everything a read-only mode refuses.
    /// </para>
    /// <para>
    /// <b>What is found later than in the other modes.</b> The other modes check every
    /// cross-reference entry against the object it points at as the file is opened. This one
    /// checks an entry when its object is asked for, so an entry pointing at nothing, or at
    /// another object under <see cref="PdfPinata.Pdf.IO.enums.PdfReadAccuracy.Strict"/>, throws a
    /// <see cref="PdfReaderException"/> from the read that needed it and not from
    /// <c>Open</c>. A table whose object numbers are all off by one, which the other modes repair,
    /// is refused that way too.
    /// </para>
    /// <para>
    /// A document is not thread-safe in any mode. In this one two threads reading the same
    /// document take turns rather than corrupt each other's reads, and no more is promised.
    /// </para>
    /// </remarks>
    Lazy = 5
}
