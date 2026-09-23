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
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.IO;
using PdfPinata.Internal;
using PdfPinata.Pdf.Internal;
using System.Collections.Generic;
using System.Linq;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Lexical analyzer for PDF files. Technically a PDF file is a stream of bytes. Some chunks
/// of bytes represent strings in several encodings. The actual encoding depends on the
/// context where the string is used. Therefore the bytes are 'raw encoded' into characters,
/// i.e. a character or token read by the lexer has always character values in the range from
/// 0 to 255.
/// </summary>
public class Lexer
{
    /// <summary>
    /// Initializes a new instance of the Lexer class.
    /// </summary>
    public Lexer(Stream pdfInputStream)
    {
        PdfStream = pdfInputStream;
        PdfLength = PdfStream.Length;
        _idxChar = 0;
        _readNextRawByte = ReadNextRawByte;
        _scanNextCharFolding = ScanNextCharFolding;
        Position = 0;
    }

    /// <summary>
    /// Gets the stream the PDF file is read from.
    /// </summary>
    internal Stream PdfStream { get; }

    /// <summary>
    /// Gets or sets the position within the PDF stream.
    /// </summary>
    public long Position
    {
        get => _idxChar;
        set
        {
            _idxChar = value;
            PdfStream.Position = value;
            // ReadByte return -1 (eof) at the end of the stream.
            _currChar = (char)PdfStream.ReadByte();
            _nextChar = (char)PdfStream.ReadByte();
            _token = new StringBuilder();
        }
    }

    /// <summary>
    /// Reads the next token and returns its type. If the token starts with a digit, the parameter
    /// testReference specifies how to treat it. If it is false, the lexer scans for a single integer.
    /// If it is true, the lexer checks if the digit is the prefix of a reference. If it is a reference,
    /// the token is set to the object ID followed by the generation number separated by a blank
    /// (the 'R' is omitted from the token).
    /// </summary>
    // /// <param name="testReference">Indicates whether to test the next token if it is a reference.</param>
    public Symbol ScanNextToken()
    {
        Again:
        _token = new StringBuilder();

        var ch = MoveToNonWhiteSpace();
        switch (ch)
        {
            case '%':
                // Eat comments, the parser doesn't handle them
                ScanComment();
                goto Again;

            case '/':
                return Symbol = ScanName();

            case '+':
            case '-':
                return Symbol = ScanNumber();

            case '(':
                return Symbol = ScanLiteralString();

            case '[':
                ScanNextChar(true);
                return Symbol = Symbol.BeginArray;

            case ']':
                ScanNextChar(true);
                return Symbol = Symbol.EndArray;

            case '<':
                if (_nextChar == '<')
                {
                    ScanNextChar(true);
                    ScanNextChar(true);
                    return Symbol = Symbol.BeginDictionary;
                }
                return Symbol = ScanHexadecimalString();

            case '>':
                if (_nextChar == '>')
                {
                    ScanNextChar(true);
                    ScanNextChar(true);
                    return Symbol = Symbol.EndDictionary;
                }
                ParserDiagnostics.HandleUnexpectedCharacter(_nextChar);
                break;

            case '.':
                return Symbol = ScanNumber();
        }
        if (char.IsDigit(ch))
            return Symbol = ScanNumber();

        if (char.IsLetter(ch))
            return Symbol = ScanKeyword();

        if (ch == Chars.EOF)
            return Symbol = Symbol.Eof;

        ParserDiagnostics.HandleUnexpectedCharacter(ch);
        return Symbol = Symbol.None;
    }

    /// <summary>
    /// Reads the raw content of a stream.
    /// </summary>
    public byte[] ReadStream(int length)
    {
        var pos = MoveToStartOfStream();
        PdfStream.Position = pos;
        var bytes = new byte[length];
        // A stream whose dictionary declares more bytes than the file holds is read as the bytes
        // that are there. What it is really as long as is then the caller's problem, which it
        // was going to be anyway.
        // Named in full: this namespace has a StreamHelper of its own, in Parser.cs.
        var read = PdfPinata.Internal.StreamHelper.ReadUpTo(PdfStream, bytes, 0, length);
        if (read < length)
            Array.Resize(ref bytes, read);

        // Synchronize idxChar etc.
        Position = pos + read;
        return bytes;
    }

    internal long MoveToStartOfStream()
    {
        long pos;

        // Skip illegal blanks behind �stream�.
        while (_currChar == Chars.SP)
            ScanNextChar(true);

        // Skip new line behind �stream�.
        if (_currChar == Chars.CR)
        {
            if (_nextChar == Chars.LF)
                pos = _idxChar + 2;
            else
                pos = _idxChar + 1;
        }
        else
        {
            pos = _idxChar + 1;
        }
        return pos;
    }

    /// <summary>
    /// Scans the input stream for the specified marker.<br></br>
    /// Returns the bytes from the current position up to the start of the marker or the end of the stream.<br></br>
    /// The position of the input-stream is the byte right after the marker (if found) or the end of the stream.
    /// </summary>
    /// <param name="marker">The marker to scan for</param>
    /// <param name="markerFound">Receives a boolean that indicates whether the marker was found</param>
    /// <returns></returns>
    internal byte[] ScanUntilMarker(byte[] marker, out bool markerFound)
    {
        markerFound = false;
        var result = new List<byte>();
        while (true)
        {
            var markerIndex = 0;
            while (_currChar != Chars.EOF && _currChar != marker[markerIndex])
            {
                result.Add((byte)_currChar);
                ScanNextChar(false);
            }
            while (_currChar != Chars.EOF && markerIndex < marker.Length && _currChar == marker[markerIndex])
            {
                markerIndex++;
                ScanNextChar(false);
            }
            if (_currChar == Chars.EOF || markerIndex == marker.Length)
            {
                if (markerIndex == marker.Length)
                    markerFound = true;
                break;
            }
            // only part of the marker was found, add to result and continue
            result.AddRange(marker.Take(markerIndex));
        }

        return [..result];
    }

    /// <summary>
    /// Reads a string in raw encoding.
    /// </summary>
    public string ReadRawString(long position, int length)
    {
        PdfStream.Position = position;
        var bytes = new byte[length];
        PdfPinata.Internal.StreamHelper.ReadUpTo(PdfStream, bytes, 0, length);
        return PdfEncoders.RawEncoding.GetString(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Gets the position of the last occurrence of the marker given, or -1 when the file does not
    /// contain it. The marker is matched against the raw bytes, one character to one byte, the way
    /// the rest of the lexer reads them.
    /// </summary>
    /// <remarks>
    /// The file is read backwards a chunk at a time and no part of it is ever turned into a string,
    /// so looking for a marker costs one fixed buffer however long the file is. Reading the whole
    /// file in to search it - which is what this replaced - cannot work at all beyond 1,073,741,791
    /// bytes, because that is as long as a <see cref="string" /> gets.
    /// </remarks>
    internal long FindLastMarker(string marker)
    {
        Debug.Assert(marker.Length is > 0 and < BackwardScanChunkSize);

        var pattern = new byte[marker.Length];
        for (var i = 0; i < marker.Length; i++)
            pattern[i] = (byte)marker[i];

        // A marker straddling a chunk boundary is in neither chunk, so each chunk reaches the
        // length of the marker less one byte into the chunk already searched.
        var overlap = pattern.Length - 1;
        var buffer = new byte[BackwardScanChunkSize + overlap];
        var end = PdfLength;

        while (end > 0)
        {
            var start = Math.Max(0, end - BackwardScanChunkSize);
            var count = (int)(Math.Min(end + overlap, PdfLength) - start);

            PdfStream.Position = start;
            var read = PdfPinata.Internal.StreamHelper.ReadUpTo(PdfStream, buffer, 0, count);

            var idx = buffer.AsSpan(0, read).LastIndexOf(pattern.AsSpan());
            if (idx >= 0)
                return start + idx;

            end = start;
        }

        return -1;
    }

    /// <summary>
    /// Scans a comment line.
    /// </summary>
    public Symbol ScanComment()
    {
        Debug.Assert(_currChar == Chars.Percent);

        _token = new StringBuilder();
        while (true)
        {
            var ch = AppendAndScanNextChar();
            if (ch is Chars.LF or Chars.EOF)
                break;
        }
        // The end-of-file marker is reported as such to a caller scanning comments itself.
        // ScanNextToken discards every comment, this one included: a file that has been
        // updated incrementally carries one %%EOF per revision, so the body ends only where
        // the stream does.
        if (_token.ToString().StartsWith("%%EOF", StringComparison.Ordinal))
            return Symbol = Symbol.Eof;
        return Symbol = Symbol.Comment;
    }

    /// <summary>
    /// Scans a name.
    /// </summary>
    public Symbol ScanName()
    {
        Debug.Assert(_currChar == Chars.Slash);

        _token = new StringBuilder();
        while (true)
        {
            var ch = AppendAndScanNextChar();
            if (IsWhiteSpace(ch) || IsDelimiter(ch) || ch == Chars.EOF)
                return Symbol = Symbol.Name;

            // #hh is the byte hh (ISO 32000-1 7.3.5). A '#' followed by anything else - one hex
            // digit, none, or the end of the file - is not an escape, and is kept as the
            // character it is rather than refused, as readers do.
            if (ch != '#' || !IsHexChar(_nextChar))
                continue;

            ScanNextChar(true);
            if (!IsHexChar(_nextChar))
            {
                // Only one digit: the '#' stands for itself and the digit is scanned next.
                _token.Append('#');
                continue;
            }
            var high = _currChar;
            ScanNextChar(true);
            _currChar = (char)(HexValue(high) << 4 | HexValue(_currChar));
        }
    }

    /// <summary>
    /// Scans a number.
    /// </summary>
    public Symbol ScanNumber()
    {
        // I found a PDF file created with Acrobat 7 with this entry
        //   /Checksum 2996984786
        // What is this? It is neither an integer nor a real.
        // I introduced an UInteger...
        var period = false;

        _token = new StringBuilder();
        var ch = _currChar;
        if (ch is '+' or '-')
        {
            _token.Append(ch);
            ch = ScanNextChar(true);
        }
        while (true)
        {
            if (char.IsDigit(ch))
            {
                _token.Append(ch);
            }
            else if (ch == '.')
            {
                if (period)
                    ParserDiagnostics.ThrowParserException("More than one period in number.");

                period = true;
                _token.Append(ch);
            }
            else
            {
                break;
            }
            ch = ScanNextChar(true);
        }

        if (period)
            return Symbol.Real;
        var l = long.Parse(_token.ToString(), CultureInfo.InvariantCulture);
        if (l is >= int.MinValue and <= int.MaxValue)
            return Symbol.Integer;
        // ReSharper disable ConditionIsAlwaysTrueOrFalse
        if (l >= long.MinValue && l <= long.MaxValue)
            return Symbol.Long;
        // ReSharper restore ConditionIsAlwaysTrueOrFalse

        // Got an AutoCAD PDF file that contains this: /C 264584027963392
        // Best we can do is to convert it to real value.
        return Symbol.Real;
    }

    /// <summary>Scans a number, which may turn out to be the first part of an indirect reference.</summary>
    public Symbol ScanNumberOrReference()
    {
        var result = ScanNumber();
        return result;
    }

    /// <summary>
    /// Scans a keyword.
    /// </summary>
    public Symbol ScanKeyword()
    {
        _token = new StringBuilder();
        var ch = _currChar;
        // Scan token
        while (true)
        {
            if (char.IsLetter(ch))
                _token.Append(ch);
            else
                break;
            ch = ScanNextChar(false);
        }

        // Check known tokens.
        return Symbol = _token.ToString() switch
        {
            "obj" => Symbol.Obj,
            "endobj" => Symbol.EndObj,
            "null" => Symbol.Null,
            "true" or "false" => Symbol.Boolean,
            "R" => Symbol.R,
            "stream" => Symbol.BeginStream,
            "endstream" => Symbol.EndStream,
            "xref" => Symbol.XRef,
            "trailer" => Symbol.Trailer,
            "startxref" => Symbol.StartXRef,
            // Anything else is treated as a keyword. Samples are f or n in iref.
            _ => Symbol.Keyword
        };
    }

    /// <summary>
    /// Scans a literal string, contained between "(" and ")".
    /// </summary>
    public Symbol ScanLiteralString()
    {
        // Reference: 3.2.3  String Objects / Page 53
        // Reference: TABLE 3.32  String Types / Page 157

        Debug.Assert(_currChar == Chars.ParenLeft);
        _token = new StringBuilder();
        var parenLevel = 0;
        var ch = ScanNextChar(false);

        // Phase 1: deal with escape characters.
        while (ch != Chars.EOF)
        {
            switch (ch)
            {
                case '(':
                    parenLevel++;
                    break;

                case ')':
                    if (parenLevel == 0)
                    {
                        ScanNextChar(false);
                        // Is goto evil? We could move Phase 2 code here or create a subroutine for Phase 1.
                        goto Phase2;
                    }
                    parenLevel--;
                    break;

                case '\\':
                {
                    ch = ScanNextChar(false);
                    switch (ch)
                    {
                        case 'n':
                            ch = Chars.LF;
                            break;

                        case 'r':
                            ch = Chars.CR;
                            break;

                        case 't':
                            ch = Chars.HT;
                            break;

                        case 'b':
                            ch = Chars.BS;
                            break;

                        case 'f':
                            ch = Chars.FF;
                            break;

                        case '(':
                            ch = Chars.ParenLeft;
                            break;

                        case ')':
                            ch = Chars.ParenRight;
                            break;

                        case '\\':
                            ch = Chars.BackSlash;
                            break;

                        // AutoCAD PDFs my contain such strings: (\ )
                        case ' ':
                            ch = ' ';
                            break;

                        case Chars.CR:
                        case Chars.LF:
                            ch = ScanNextChar(false);
                            continue;

                        default:
                            if (char.IsDigit(ch))  // First octal character.
                            {
                                // Octal character code.
                                if (ch >= '8')
                                    break; // Since the first possible octal character is not valid,
                                // the backslash is ignored.

                                var n = ch - '0';
                                if (char.IsDigit(_nextChar))  // Second octal character.
                                {
                                    ch = ScanNextChar(false);
                                    if (ch >= '8')
                                        ParserDiagnostics.HandleUnexpectedCharacter(ch);

                                    n = n * 8 + ch - '0';
                                    if (char.IsDigit(_nextChar))  // Third octal character.
                                    {
                                        ch = ScanNextChar(false);
                                        if (ch >= '8')
                                            ParserDiagnostics.HandleUnexpectedCharacter(ch);

                                        n = n * 8 + ch - '0';
                                    }
                                }
                                ch = (char)n;
                            }
                            break;
                    }
                    break;
                }
            }

            _token.Append(ch);
            ch = ScanNextChar(false);
        }

        // Phase 2: deal with UTF-16BE if necessary.
        // UTF-16BE Unicode strings start with U+FEFF ("��"). There can be empty strings with UTF-16BE prefix.
        Phase2:
        if (_token.Length >= 2 && _token[0] == '\xFE' && _token[1] == '\xFF')
        {
            // Combine two ANSI characters to get one Unicode character.
            var temp = _token;
            var length = temp.Length;
            if ((length & 1) == 1)
            {
                // A UTF-16 string with an odd number of bytes is short of the low byte of its last
                // character. The reference says nothing about it, so the missing byte is taken to
                // be a zero - the same reading ScanHexadecimalString gives a hex string missing
                // its final digit, which the reference does specify.
                //
                // '\0' and not 0: the latter binds to Append(int), which appends the digit zero
                // rather than the character. See LexerUnicodeStringTests.
                temp.Append('\0');
                ++length;
            }
            _token = new StringBuilder();
            for (var i = 2; i < length; i += 2)
            {
                _token.Append((char)(256 * temp[i] + temp[i + 1]));
            }
            return Symbol = Symbol.UnicodeString;
        }
        // Adobe Reader also supports UTF-16LE.
        if (_token.Length >= 2 && _token[0] == '\xFF' && _token[1] == '\xFE')
        {
            // Combine two ANSI characters to get one Unicode character.
            var temp = _token;
            var length = temp.Length;
            if ((length & 1) == 1)
            {
                // As above, for the little endian order Adobe Reader also accepts. The digit this
                // used to append did more damage here than in the big endian case: it landed in
                // the *high* half of the last character, so a byte short of "I" read as U+3049
                // rather than as the "I" that was all but complete.
                temp.Append('\0');
                ++length;
            }
            _token = new StringBuilder();
            for (var i = 2; i < length; i += 2)
            {
                _token.Append((char)(256 * temp[i + 1] + temp[i]));
            }
            return Symbol = Symbol.UnicodeString;
        }
        return Symbol = Symbol.String;
    }

    /// <summary>Scans a string written in angle brackets as pairs of hexadecimal digits.</summary>
    public Symbol ScanHexadecimalString()
    {
        Debug.Assert(_currChar == Chars.Less);

        _token = new StringBuilder();
        var hex = new char[2];
        ScanNextChar(true);
        while (true)
        {
            MoveToNonWhiteSpace();
            // A truncated hex string never sees its closing '>', so give up at the end of
            // the file rather than spinning on Chars.EOF.
            if (_currChar == Chars.EOF)
                break;

            if (_currChar == '>')
            {
                ScanNextChar(true);
                break;
            }
            if (!IsHexChar(_currChar))
            {
                // Neither '>' nor a hex digit: step over it rather than never advancing.
                ScanNextChar(true);
                continue;
            }

            hex[0] = _currChar;
            ScanNextChar(true);
            // What may come between the two digits of a byte is what may come before one:
            // white space, and anything else that is not a digit. Only the end of the
            // string decides that the second digit is missing rather than merely late.
            while (!IsHexChar(_currChar) && _currChar != '>' && _currChar != Chars.EOF)
                ScanNextChar(true);

            if (IsHexChar(_currChar))
            {
                hex[1] = _currChar;
                ScanNextChar(true);
            }
            else
            {
                // A hex string with an odd number of digits ends in a zero.
                hex[1] = '0';
            }
            _token.Append((char)int.Parse(new string(hex), NumberStyles.AllowHexSpecifier));
        }
        var chars = _token.ToString();
        var count = chars.Length;
        if (count <= 2 || chars[0] != (char)0xFE || chars[1] != (char)0xFF)
            return Symbol = Symbol.HexString;

        // The last character of the string may be short of its low byte, which is a zero
        // for the same reason the last byte is short of its low digit. Reading on for a
        // byte that is not there walked off the end of the string.
        if ((count & 1) == 1)
        {
            chars += '\0';
            ++count;
        }
        _token.Length = 0;
        for (var idx = 2; idx < count; idx += 2)
            _token.Append((char)(chars[idx] * 256 + chars[idx + 1]));
        return Symbol = Symbol.UnicodeHexString;
    }

    internal static bool IsHexChar(char c) => CharacterScanning.IsHexChar(c);

    /// <summary>The value of a character <see cref="IsHexChar"/> accepts.</summary>
    private static int HexValue(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;

    /// <summary>
    /// Move current position one character further in PDF stream.
    /// </summary>
    internal char ScanNextChar(bool handleCRLF)
    {
        if (PdfLength <= _idxChar)
        {
            _currChar = Chars.EOF;
            _nextChar = Chars.EOF;
        }
        else
        {
            CharacterScanning.Advance(ref _currChar, ref _nextChar, handleCRLF, _readNextRawByte);
        }
        return _currChar;
    }

    /// <summary>
    /// Reads the next raw byte from the PDF stream and advances <see cref="_idxChar"/> past it.
    /// A cached delegate rather than a method group conversion at each call site, since
    /// <see cref="CharacterScanning.Advance"/> is called once per character scanned.
    /// </summary>
    private char ReadNextRawByte()
    {
        _idxChar++;
        return (char)PdfStream.ReadByte();
    }

    /// <summary>
    /// Appends current character to the token and reads next one.
    /// </summary>
    internal char AppendAndScanNextChar()
    {
        if (_currChar == Chars.EOF)
            ParserDiagnostics.ThrowParserException("Undetected EOF reached.");

        _token.Append(_currChar);
        return ScanNextChar(true);
    }

    /// <summary>
    /// If the current character is not a white space, the function immediately returns it.
    /// Otherwise the PDF cursor is moved forward to the first non-white space or EOF. White
    /// spaces are NUL, HT, LF, FF, CR, SP, a vertical tab, and a soft hyphen.
    /// </summary>
    public char MoveToNonWhiteSpace() =>
        _currChar = CharacterScanning.SkipWhiteSpace(_currChar, _scanNextCharFolding);

    private char ScanNextCharFolding() => ScanNextChar(true);

    /// <summary>
    /// Gets the current symbol.
    /// </summary>
    public Symbol Symbol { get; set; } = Symbol.None;

    /// <summary>
    /// Gets the current token.
    /// </summary>
    public string Token => _token.ToString();

    /// <summary>
    /// Interprets current token as boolean literal.
    /// </summary>
    public bool TokenToBoolean
    {
        get
        {
            Debug.Assert(_token.ToString() == "true" || _token.ToString() == "false");
            return _token.ToString()[0] == 't';
        }
    }

    /// <summary>
    /// Interprets current token as integer literal.
    /// </summary>
    public int TokenToInteger => int.Parse(_token.ToString(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Interprets current token as unsigned integer literal.
    /// </summary>
    public uint TokenToUInteger => uint.Parse(_token.ToString(), CultureInfo.InvariantCulture);

    /// <summary>Interprets the current token as a long integer literal.</summary>
    public long TokenToLong => long.Parse(_token.ToString(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Interprets current token as real or integer literal.
    /// </summary>
    public double TokenToReal => double.Parse(_token.ToString(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Interprets current token as object ID.
    /// </summary>
    public PdfObjectID TokenToObjectID
    {
        get
        {
            var numbers = Token.Split('|');
            var objectNumber = int.Parse(numbers[0]);
            var generationNumber = int.Parse(numbers[1]);
            return new PdfObjectID(objectNumber, generationNumber);
        }
    }

    /// <summary>
    /// Indicates whether the specified character is a PDF white-space character.
    /// </summary>
    internal static bool IsWhiteSpace(char ch) => CharacterScanning.IsWhiteSpace(ch);

    /// <summary>
    /// Indicates whether the specified character is a PDF delimiter character.
    /// </summary>
    internal static bool IsDelimiter(char ch) => CharacterScanning.IsDelimiter(ch);

    /// <summary>
    /// Gets the length of the PDF output.
    /// </summary>
    public long PdfLength { get; }

    /// <summary>
    /// How much of the file <see cref="FindLastMarker" /> holds at a time.
    /// </summary>
    private const int BackwardScanChunkSize = 64 * 1024;

    private long _idxChar;
    private char _currChar;
    private char _nextChar;
    private StringBuilder _token;

    private readonly Func<char> _readNextRawByte;
    private readonly Func<char> _scanNextCharFolding;
}
