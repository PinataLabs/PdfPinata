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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Security;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Represents a writer for generation of PDF streams.
/// </summary>
internal class PdfWriter
{
    public PdfWriter(Stream pdfStream, PdfStandardSecurityHandler securityHandler)
    {
        _stream = pdfStream;
        _securityHandler = securityHandler;
    }

    public void Close(bool closeUnderlyingStream)
    {
        if (_stream != null && closeUnderlyingStream)
            _stream.Dispose();
        _stream = null;
    }

    public void Close()
    {
        Close(true);
    }

    public long Position => _stream.Position;

    /// <summary>
    /// Gets or sets the kind of layout.
    /// </summary>
    public PdfWriterLayout Layout
    {
        get => _layout;
        set => _layout = value;
    }
    PdfWriterLayout _layout;

    public PdfWriterOptions Options
    {
        get => _options;
        set => _options = value;
    }
    PdfWriterOptions _options;

    // -----------------------------------------------------------

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(bool value)
    {
        WriteSeparator();
        WriteRaw(value ? bool.TrueString : bool.FalseString);
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfBoolean value)
    {
        WriteSeparator();
        WriteRaw(value.Value ? "true" : "false");
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(int value)
    {
        WriteSeparator();
        WriteRaw(value.ToString(CultureInfo.InvariantCulture));
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(long value)
    {
        WriteSeparator();
        WriteRaw(value.ToString(CultureInfo.InvariantCulture));
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(uint value)
    {
        WriteSeparator();
        WriteRaw(value.ToString(CultureInfo.InvariantCulture));
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfInteger value)
    {
        WriteSeparator();
        _lastCat = CharCat.Character;
        WriteRaw(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfLong value)
    {
        WriteSeparator();
        _lastCat = CharCat.Character;
        WriteRaw(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfUInteger value)
    {
        WriteSeparator();
        _lastCat = CharCat.Character;
        WriteRaw(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(double value)
    {
        WriteSeparator();
        WriteRaw(value.ToString(Config.SignificantFigures7, CultureInfo.InvariantCulture));
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfReal value)
    {
        WriteSeparator();
        WriteRaw(value.Value.ToString(Config.SignificantFigures7, CultureInfo.InvariantCulture));
        _lastCat = CharCat.Character;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfString value)
    {
        WriteSeparator();
        var encoding = (PdfStringEncoding)(value.Flags & PdfStringFlags.EncodingMask);
        var pdf = (value.Flags & PdfStringFlags.HexLiteral) == 0 ?
            PdfEncoders.ToStringLiteral(value.EncryptionValue, encoding == PdfStringEncoding.Unicode, SecurityHandler) :
            PdfEncoders.ToHexStringLiteral(value.EncryptionValue, encoding == PdfStringEncoding.Unicode, SecurityHandler);
        WriteRaw(pdf);

        _lastCat = CharCat.Delimiter;
    }

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(PdfName value)
    {
        WriteSeparator();
        var name = value.Value;

        var pdf = new StringBuilder("/");
        for (var idx = 1; idx < name.Length; idx++)
        {
            var ch = name[idx];
            Debug.Assert(ch < 256);
            if (ch > ' ')
                switch (ch)
                {
                    // TODO: is this all?
                    case '%':
                    case '/':
                    case '<':
                    case '>':
                    case '(':
                    case ')':
                    case '#':
                        break;

                    default:
                        pdf.Append(name[idx]);
                        continue;
                }
            pdf.AppendFormat("#{0:X2}", (int)name[idx]);
        }
        WriteRaw(pdf.ToString());
        _lastCat = CharCat.Character;
    }

    public void Write(PdfLiteral value)
    {
        WriteSeparator();
        WriteRaw(value.Value);
        _lastCat = CharCat.Character;
    }

    public void Write(PdfRectangle rect)
    {
        const string format = Config.SignificantFigures3;
        WriteSeparator();
        WriteRaw(PdfEncoders.Format("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}]", rect.X1, rect.Y1, rect.X2, rect.Y2));
        _lastCat = CharCat.Delimiter;
    }

    public void Write(PdfReference iref)
    {
        WriteSeparator();
        WriteRaw(iref.ToString());
        _lastCat = CharCat.Character;
    }

    public void WriteDocString(string text, bool unicode)
    {
        WriteSeparator();
        //WriteRaw(PdfEncoders.DocEncode(text, unicode));
        byte[] bytes;
        if (!unicode)
            bytes = PdfEncoders.DocEncoding.GetBytes(text);
        else
            bytes = PdfEncoders.UnicodeEncoding.GetBytes(text);
        bytes = PdfEncoders.FormatStringLiteral(bytes, unicode, true, false, _securityHandler);
        Write(bytes);
        _lastCat = CharCat.Delimiter;
    }

    public void WriteDocString(string text)
    {
        WriteSeparator();
        //WriteRaw(PdfEncoders.DocEncode(text, false));
        var bytes = PdfEncoders.DocEncoding.GetBytes(text);
        bytes = PdfEncoders.FormatStringLiteral(bytes, false, false, false, _securityHandler);
        Write(bytes);
        _lastCat = CharCat.Delimiter;
    }

    public void WriteDocStringHex(string text)
    {
        WriteSeparator();
        //WriteRaw(PdfEncoders.DocEncodeHex(text));
        var bytes = PdfEncoders.DocEncoding.GetBytes(text);
        bytes = PdfEncoders.FormatStringLiteral(bytes, false, false, true, _securityHandler);
        _stream.Write(bytes, 0, bytes.Length);
        _lastCat = CharCat.Delimiter;
    }

    /// <summary>
    /// Begins a direct or indirect dictionary or array.
    /// </summary>
    public void WriteBeginObject(PdfObject obj)
    {
        var indirect = obj.IsIndirect;
        if (indirect && !_omitIndirectFraming)
        {
            WriteObjectAddress(obj);
            if (_securityHandler != null)
                _securityHandler.SetHashKey(obj.ObjectID);
        }
        _stack.Add(new StackItem(obj));
        if (indirect)
        {
            if (obj is PdfArray)
                WriteRaw("[\n");
            else if (obj is PdfDictionary)
                WriteRaw("<<\n");
            _lastCat = CharCat.NewLine;
        }
        else
        {
            if (obj is PdfArray)
            {
                WriteSeparator();
                WriteRaw('[');
                _lastCat = CharCat.Delimiter;
            }
            else if (obj is PdfDictionary)
            {
                NewLine();
                WriteSeparator();
                WriteRaw("<<\n");
                _lastCat = CharCat.NewLine;
            }
        }
        if (_layout == PdfWriterLayout.Verbose)
            IncreaseIndent();
    }

    /// <summary>
    /// Ends a direct or indirect dictionary or array.
    /// </summary>
    public void WriteEndObject()
    {
        var count = _stack.Count;
        Debug.Assert(count > 0, "PdfWriter stack underflow.");

        var stackItem = _stack[count - 1];
        _stack.RemoveAt(count - 1);

        var value = stackItem.Object;
        var indirect = value.IsIndirect;
        if (_layout == PdfWriterLayout.Verbose)
            DecreaseIndent();
        if (value is PdfArray)
        {
            if (indirect)
            {
                WriteRaw("\n]\n");
                _lastCat = CharCat.NewLine;
            }
            else
            {
                WriteRaw("]");
                _lastCat = CharCat.Delimiter;
            }
        }
        else if (value is PdfDictionary)
        {
            if (indirect)
            {
                if (!stackItem.HasStream)
                    WriteRaw(_lastCat == CharCat.NewLine ? ">>\n" : " >>\n");
            }
            else
            {
                Debug.Assert(!stackItem.HasStream, "Direct object with stream??");
                WriteSeparator();
                WriteRaw(">>\n");
                _lastCat = CharCat.NewLine;
            }
        }
        if (indirect && !_omitIndirectFraming)
        {
            NewLine();
            WriteRaw("endobj\n");
            if (_layout == PdfWriterLayout.Verbose)
                WriteRaw("%--------------------------------------------------------------------------------------------------\n");
        }
    }

    /// <summary>
    /// Gets or sets a value indicating that an indirect object is written without the
    /// "<c>N G obj</c>" and "<c>endobj</c>" that normally frame it — its body alone, and nothing
    /// saying whose body it is.
    /// <para>
    /// That is what an object stream holds: the bodies run together and a table at the front says
    /// where each one starts, so the framing would be worse than redundant. It is the only reason
    /// this exists, and a writer in this mode must not be used for anything else, because the
    /// result is not a PDF file.
    /// </para>
    /// <para>
    /// The address is not all that is skipped. <see cref="WriteBeginObject"/> also sets the
    /// encryption hash key from the object's ID, and inside an object stream it must not: strings
    /// there are covered by the encryption of the containing stream and are not encrypted again.
    /// A writer built for this is given no security handler at all, so this is belt and braces.
    /// </para>
    /// </summary>
    internal bool OmitIndirectFraming
    {
        get => _omitIndirectFraming;
        set => _omitIndirectFraming = value;
    }
    bool _omitIndirectFraming;

    /// <summary>
    /// Writes the stream of the specified dictionary.
    /// </summary>
    public void WriteStream(PdfDictionary value, bool omitStream)
    {
        var stackItem = _stack[^1];
        Debug.Assert(stackItem.Object is PdfDictionary);
        Debug.Assert(stackItem.Object.IsIndirect);
        stackItem.HasStream = true;

        WriteRaw(_lastCat == CharCat.NewLine ? ">>\nstream\n" : " >>\nstream\n");

        if (omitStream)
        {
            WriteRaw("  «...stream content omitted...»\n");  // useful for debugging only
        }
        else
        {
            var bytes = value.Stream.Value;
            if (bytes.Length != 0)
            {
                if (_securityHandler != null)
                {
                    bytes = (byte[])bytes.Clone();
                    bytes = _securityHandler.EncryptBytes(bytes);
                }
                Write(bytes);

                // Unconditionally, even when the data already ends with one. /Length counts the
                // data and nothing else, and ISO 32000-1 7.3.8.1 puts this end-of-line marker
                // *after* those bytes, outside the count — so a validator finds the data by taking
                // Length bytes and expects a separator before "endstream". Written only when the
                // data did not end with a newline, the data's own last byte has to serve as that
                // separator, and the file then declares one byte more than it appears to hold.
                // veraPDF fails it under PDF/A-1 6.1.7 and PDF/A-2/3 6.1.7.1; the XMP packet, which
                // ends with a newline by construction, was the case that showed it up.
                WriteRaw('\n');
            }
        }
        WriteRaw("endstream\n");
    }

    public void WriteRaw(string rawString)
    {
        if (String.IsNullOrEmpty(rawString))
            return;

        var bytes = PdfEncoders.RawEncoding.GetBytes(rawString);
        _stream.Write(bytes, 0, bytes.Length);
        _lastCat = GetCategory((char)bytes[^1]);
    }

    public void WriteRaw(char ch)
    {
        Debug.Assert(ch < 256, "Raw character greater than 255 detected.");

        _stream.WriteByte((byte)ch);
        _lastCat = GetCategory(ch);
    }

    public void Write(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        _stream.Write(bytes, 0, bytes.Length);
        _lastCat = GetCategory((char)bytes[^1]);
    }

    void WriteObjectAddress(PdfObject value)
    {
        if (_layout == PdfWriterLayout.Verbose)
            WriteRaw(
                $"{value.ObjectID.ObjectNumber} {value.ObjectID.GenerationNumber} obj   % {value.GetType().FullName}\n");
        else
            WriteRaw($"{value.ObjectID.ObjectNumber} {value.ObjectID.GenerationNumber} obj\n");
    }

    public void WriteFileHeader(PdfDocument document)
    {
        var header = new StringBuilder("%PDF-");
        var version = document._version;
        header.Append((version / 10).ToString(CultureInfo.InvariantCulture) + "." +
                      (version % 10).ToString(CultureInfo.InvariantCulture) + "\n%\xD3\xF4\xCC\xE1" + "\n");
        WriteRaw(header.ToString());

        if (_layout == PdfWriterLayout.Verbose)
        {
            WriteRaw($"% PDFsharp Version {VersionInfo.Version} (verbose mode)\n");
            // Keep some space for later fix-up.
            _commentPosition = (int)_stream.Position + 2;
            WriteRaw("%                                                \n");
            WriteRaw("%                                                \n");
            WriteRaw("%                                                \n");
            WriteRaw("%                                                \n");
            WriteRaw("%                                                \n");
            WriteRaw("%--------------------------------------------------------------------------------------------------\n");
        }
    }

    public void WriteEof(PdfDocument document, long startxref)
    {
        WriteRaw("startxref\n");
        WriteRaw(startxref.ToString(CultureInfo.InvariantCulture));
        WriteRaw("\n%%EOF\n");
        var fileSize = _stream.Position;

        // Only when this writer wrote the header those comments live in. Patching them means
        // seeking backwards to a position the header recorded, and a writer that never wrote one
        // has no such position — an incremental save appends to a file it did not write, and
        // patching there scribbles over the start of somebody else's document. Verbose is the
        // default in a debug build, so this would only ever have gone wrong where it is hardest to
        // credit: in development, and never in release.
        if (_layout == PdfWriterLayout.Verbose && _commentPosition >= 0)
        {
            var duration = GlobalTimeSettings.Now - document._creation;

            _stream.Position = _commentPosition;
            // Without InvariantCulture parameter the following line fails if the current culture is e.g.
            // a Far East culture, because the date string contains non-ASCII characters.
            // So never never never never use ToString without a culture info.
            WriteRaw("Creation date: " + document._creation.ToString("G", CultureInfo.InvariantCulture));
            _stream.Position = _commentPosition + 50;
            WriteRaw("Creation time: " + duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " seconds");
            _stream.Position = _commentPosition + 100;
            WriteRaw("File size: " + fileSize.ToString(CultureInfo.InvariantCulture) + " bytes");
            _stream.Position = _commentPosition + 150;
            WriteRaw("Pages: " + document.Pages.Count.ToString(CultureInfo.InvariantCulture));
            _stream.Position = _commentPosition + 200;
            WriteRaw("Objects: " + document._irefTable.ObjectTable.Count.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Gets or sets the indentation for a new indentation level.
    /// </summary>
    internal int Indent
    {
        get => _indent;
        set => _indent = value;
    }
    int _indent = 2;
    int _writeIndent;

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

    ///// <summary>
    ///// Returns an indent string of blanks.
    ///// </summary>
    //static string Ind(int _indent)
    //{
    //  return new String(' ', _indent);
    //}

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
            case CharCat.NewLine:
                if (_layout == PdfWriterLayout.Verbose)
                    WriteIndent();
                break;

            case CharCat.Delimiter:
                break;

            case CharCat.Character:
                _stream.WriteByte((byte)' ');
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void NewLine()
    {
        if (_lastCat != CharCat.NewLine)
            WriteRaw('\n');
    }

    static CharCat GetCategory(char ch)
    {
        if (Lexer.IsDelimiter(ch))
            return CharCat.Delimiter;
        if (ch == Chars.LF)
            return CharCat.NewLine;
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

    internal PdfStandardSecurityHandler SecurityHandler
    {
        get => _securityHandler;
        set => _securityHandler = value;
    }
    PdfStandardSecurityHandler _securityHandler;

    class StackItem
    {
        public StackItem(PdfObject value)
        {
            Object = value;
        }

        public readonly PdfObject Object;
        public bool HasStream;
    }

    readonly List<StackItem> _stack = new();
    /// <summary>
    /// Where the header comments this writer wrote begin, or -1 when it wrote none.
    /// </summary>
    int _commentPosition = -1;
}
