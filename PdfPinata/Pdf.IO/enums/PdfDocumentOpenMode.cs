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

using PdfPinata.Pdf;

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
/// A new member goes after <see cref="Append"/> with the next free number, and 3 stays vacant.
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
    /// <b>Last on purpose, and 4 on purpose.</b> It has always been 4, and it stays 4 now that 3 is
    /// vacant: an assembly compiled against an earlier version passes the number it was compiled
    /// with, so closing the gap would silently redirect every such caller. Add a new member after
    /// this one, never in the middle, and never into 3.
    /// </para>
    /// </remarks>
    Append = 4,
}
