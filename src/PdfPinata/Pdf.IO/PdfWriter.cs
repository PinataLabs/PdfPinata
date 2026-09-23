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
    public PdfWriterLayout Layout { get; set; }

    public PdfWriterOptions Options { get; set; }

    // -----------------------------------------------------------

    /// <summary>
    /// Writes the specified value to the PDF stream.
    /// </summary>
    public void Write(bool value)
    {
        WriteSeparator();
        // Lowercase, because ISO 32000-1 7.3.2 spells the two boolean keywords that way and a
        // reader looking for them finds nothing else. This wrote bool.TrueString - "True" - and
        // got away with it because its only caller is PdfBooleanObject, which nothing in this
        // library creates: an indirect boolean was the one value written as a token no PDF
        // defines. The PdfBoolean overload below always had it right.
        WriteRaw(value ? "true" : "false");
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
        WriteRaw(PdfEncoders.ToNameLiteral(value.Value));
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
        var bytes = PdfEncoders.DocEncoding.GetBytes(text);
        bytes = PdfEncoders.FormatStringLiteral(bytes, false, false, false, _securityHandler);
        Write(bytes);
        _lastCat = CharCat.Delimiter;
    }

    public void WriteDocStringHex(string text)
    {
        WriteSeparator();
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
            _securityHandler?.SetHashKey(obj.ObjectID);
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
    private bool _omitIndirectFraming;

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
        if (string.IsNullOrEmpty(rawString))
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

    private void WriteObjectAddress(PdfObject value)
    {
        WriteRaw($"{value.ObjectID.ObjectNumber} {value.ObjectID.GenerationNumber} obj\n");
    }

    public void WriteFileHeader(PdfDocument document)
    {
        var header = new StringBuilder("%PDF-");
        var version = document._version;
        header.Append((version / 10).ToString(CultureInfo.InvariantCulture) + "." +
                      (version % 10).ToString(CultureInfo.InvariantCulture) + "\n%\xD3\xF4\xCC\xE1" + "\n");
        WriteRaw(header.ToString());
    }

    public void WriteEof(long startxref)
    {
        WriteRaw("startxref\n");
        WriteRaw(startxref.ToString(CultureInfo.InvariantCulture));
        WriteRaw("\n%%EOF\n");
    }

    private void WriteSeparator()
    {
        switch (_lastCat)
        {
            case CharCat.NewLine:
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

    private static CharCat GetCategory(char ch)
    {
        if (Lexer.IsDelimiter(ch))
            return CharCat.Delimiter;
        return ch == Chars.LF ? CharCat.NewLine : CharCat.Character;
    }

    private enum CharCat
    {
        NewLine,
        Character,
        Delimiter
    }
    private CharCat _lastCat;

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    internal Stream Stream => _stream;

    private Stream _stream;

    internal PdfStandardSecurityHandler SecurityHandler
    {
        get => _securityHandler;
        set => _securityHandler = value;
    }
    private PdfStandardSecurityHandler _securityHandler;

    private class StackItem
    {
        public StackItem(PdfObject value)
        {
            Object = value;
        }

        public readonly PdfObject Object;
        public bool HasStream;
    }

    private readonly List<StackItem> _stack = [];
}
