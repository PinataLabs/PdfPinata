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

using System.Collections.Generic;
using System.IO;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Content;

/// <summary>
/// Represents the functionality for reading PDF content streams.
/// </summary>
public static class ContentReader
{
    /// <summary>
    /// Reads the content stream(s) of the specified page.
    /// </summary>
    /// <param name="page">The page.</param>
    static public CSequence ReadContent(PdfPage page)
    {
        var parser = new CParser(page);
        var sequence = parser.ReadContent();

        return sequence;
    }

    /// <summary>
    /// Reads the specified content.
    /// </summary>
    /// <param name="content">The content.</param>
    static public CSequence ReadContent(byte[] content)
    {
        var parser = new CParser(content);
        var sequence = parser.ReadContent();
        return sequence;
    }

    /// <summary>
    /// Reads content for page rendering: every string keeps its bytes, and a fault is recorded in
    /// <paramref name="faults"/> and read past rather than thrown.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="faults">Receives one entry for each place the reading failed.</param>
    /// <param name="maximumFaults">How many faults are met before the rest is left unread.</param>
    /// <param name="leftUnread">Whether the reading stopped at that limit, short of the end.</param>
    internal static CSequence ReadPastFaults(byte[] content, List<ContentFault> faults, int maximumFaults,
        out bool leftUnread)
    {
        var lexer = new CLexer(content) { KeepStringBytes = true };
        return new CParser(lexer).ReadContentPastFaults(faults, maximumFaults, out leftUnread);
    }

    /// <summary>
    /// Reads the specified content.
    /// </summary>
    /// <param name="content">The content.</param>
    static public CSequence ReadContent(MemoryStream content)
    {
        var parser = new CParser(content);
        var sequence = parser.ReadContent();
        return sequence;
    }
}
