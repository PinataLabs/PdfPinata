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
using System.IO;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Content;

/// <summary>
/// Represents a writer for generation of PDF streams.
/// </summary>
internal class ContentWriter
{
    public ContentWriter(Stream contentStream)
    {
        _stream = contentStream;
    }

    public void Close(bool closeUnderlyingStream)
    {
        if (_stream != null && closeUnderlyingStream)
        {
            _stream.Dispose();
            _stream = null;
        }
    }

    public void Close()
    {
        Close(true);
    }

    public int Position => (int)_stream.Position;

    // -----------------------------------------------------------

    public void WriteRaw(string rawString)
    {
        if (String.IsNullOrEmpty(rawString))
            return;
        var bytes = PdfEncoders.RawEncoding.GetBytes(rawString);
        _stream.Write(bytes, 0, bytes.Length);
        _wroteAnything = true;
    }

    public void WriteLineRaw(string rawString)
    {
        if (String.IsNullOrEmpty(rawString))
            return;
        var bytes = PdfEncoders.RawEncoding.GetBytes(rawString);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Write([(byte)'\n'], 0, 1);
        _wroteAnything = true;
    }

    public void WriteRaw(char ch)
    {
        Debug.Assert(ch < 256, "Raw character greater than 255 detected.");
        _stream.WriteByte((byte)ch);
        _wroteAnything = true;
    }

    /// <summary>
    /// Gets or sets the indentation for a new indentation level.
    /// </summary>
    internal int Indent
    {
        get => _indent;
        set => _indent = value;
    }
    protected int _indent = 2;

    /// <summary>
    /// Ends the line, unless nothing has been written yet.
    /// </summary>
    /// <remarks>
    /// This does not look at whether the last thing written was already a line feed, and never
    /// did: the category it once tracked said "character" for everything the moment anything had
    /// been written. What it does skip is a line feed at the very start of the stream.
    /// </remarks>
    public void NewLine()
    {
        if (_wroteAnything)
            WriteRaw('\n');
    }

    private bool _wroteAnything;

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    internal Stream Stream => _stream;

    private Stream _stream;
}
