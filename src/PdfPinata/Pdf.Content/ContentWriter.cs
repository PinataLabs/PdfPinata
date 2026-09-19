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

    //public PdfWriterLayout Layout
    //{
    //  get { return layout; }
    //  set { layout = value; }
    //}
    //PdfWriterLayout layout;

    //public PdfWriterOptions Options
    //{
    //  get { return options; }
    //  set { options = value; }
    //}
    //PdfWriterOptions options;

    // -----------------------------------------------------------

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public static void Write(bool value)
    {
        //WriteSeparator(CharCat.Character);
        //WriteRaw(value ? bool.TrueString : bool.FalseString);
        //lastCat = CharCat.Character;
    }

    public void WriteRaw(string rawString)
    {
        if (String.IsNullOrEmpty(rawString))
            return;
        //AppendBlank(rawString[0]);
        var bytes = PdfEncoders.RawEncoding.GetBytes(rawString);
        _stream.Write(bytes, 0, bytes.Length);
        _lastCat = GetCategory();
    }

    public void WriteLineRaw(string rawString)
    {
        if (String.IsNullOrEmpty(rawString))
            return;
        //AppendBlank(rawString[0]);
        var bytes = PdfEncoders.RawEncoding.GetBytes(rawString);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Write(new[] { (byte)'\n' }, 0, 1);
        _lastCat = GetCategory();
    }

    public void WriteRaw(char ch)
    {
        Debug.Assert(ch < 256, "Raw character greater than 255 detected.");
        _stream.WriteByte((byte)ch);
        _lastCat = GetCategory();
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
    protected int _writeIndent;

    /// <summary>
    /// Increases indent level.
    /// </summary>
    void IncreaseIndent()
    {
        _writeIndent += _indent;
    }

    /// <summary>
    /// Decreases indent level.
    /// </summary>
    void DecreaseIndent()
    {
        _writeIndent -= _indent;
    }

    /// <summary>
    /// Gets an indent string of current indent.
    /// </summary>
    string IndentBlanks => new(' ', _writeIndent);

    void WriteIndent()
    {
        WriteRaw(IndentBlanks);
    }

    void WriteSeparator()
    {
        switch (_lastCat)
        {
            //case CharCat.NewLine:
            //  if (this.layout == PdfWriterLayout.Verbose)
            //    WriteIndent();
            //  break;

            case CharCat.Delimiter:
                break;

            //case CharCat.Character:
            //  if (this.layout == PdfWriterLayout.Verbose)
            //  {
            //    //if (cat == CharCat.Character || ch == '/')
            //    this.stream.WriteByte((byte)' ');
            //  }
            //  else
            //  {
            //    if (cat == CharCat.Character)
            //      this.stream.WriteByte((byte)' ');
            //  }
            //  break;
        }
    }

    public void NewLine()
    {
        if (_lastCat != CharCat.NewLine)
            WriteRaw('\n');
    }

    static CharCat GetCategory()
    {
        //if (Lexer.IsDelimiter(ch))
        //  return CharCat.Delimiter;
        //if (ch == Chars.LF)
        //  return CharCat.NewLine;
        return CharCat.Character;
    }

    enum CharCat
    {
        NewLine,
        Character,
        Delimiter
    };
    CharCat _lastCat;

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    internal Stream Stream => _stream;

    Stream _stream;
}
