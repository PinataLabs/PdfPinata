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
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Content;

/// <summary>
/// Lexical analyzer for PDF content files. Adobe specifies no grammar, but it seems that it
/// is a simple post-fix notation.
/// </summary>
public class CLexer
{
    /// <summary>
    /// Initializes a new instance of the Lexer class.
    /// </summary>
    public CLexer(byte[] content)
    {
        _content = content;
        _charIndex = 0;
        _readNextRawByte = ReadNextRawByte;
        _scanNextCharFolding = ScanNextCharFolding;
    }

    /// <summary>
    /// Initializes a new instance of the Lexer class.
    /// </summary>
    public CLexer(MemoryStream content) : this(content.ToArray())
    {
    }

    /// <summary>
    /// Reads the next token and returns its type.
    /// </summary>
    public CSymbol ScanNextToken()
    {
        while (true)
        {
            ClearToken();
            var ch = MoveToNonWhiteSpace();
            _tokenStart = CurrentCharIndex;
            if (ch != '%')
                return Symbol = ScanTokenStartingWith(ch);

            // Eat comments, the parser doesn't handle them
            ScanComment();
        }
    }

    /// <summary>
    /// Scans the token the character given begins, which is the current one and not white space
    /// or the start of a comment.
    /// </summary>
    private CSymbol ScanTokenStartingWith(char ch)
    {
        switch (ch)
        {
            case '/':
                return ScanName();

            case '+':
            case '-':
            case '.':
                return ScanNumber();

            case '[':
                ScanNextChar();
                return CSymbol.BeginArray;

            case ']':
                ScanNextChar();
                return CSymbol.EndArray;

            case '(':
                return ScanLiteralString();

            case '<':
                return _nextChar == '<' ? ScanDictionary() : ScanHexadecimalString();

            case '"':
            case '\'':
                return ScanOperator();
        }
        if (char.IsDigit(ch))
            return ScanNumber();

        if (char.IsLetter(ch))
            return ScanOperator();

        if (ch == Chars.EOF)
            return CSymbol.Eof;

        ContentReaderDiagnostics.HandleUnexpectedCharacter(ch);
        return CSymbol.None;
    }

    /// <summary>
    /// Scans a comment line. (Not yet used, comments are skipped by lexer.)
    /// </summary>
    public CSymbol ScanComment()
    {
        Debug.Assert(_currChar == Chars.Percent);

        ClearToken();
        char ch;
        while ((ch = AppendAndScanNextChar()) != Chars.LF && ch != Chars.EOF) { }
        return Symbol = CSymbol.Comment;
    }

    /// <summary>
    /// Scans an inline image, from just after its <c>BI</c> to just after its <c>EI</c>, and keeps
    /// what it holds in <see cref="InlineImageDictionary"/> and <see cref="InlineImageData"/>:
    /// <code>
    /// BI
    /// … key-value pairs …
    /// ID
    /// … image data …
    /// EI
    /// </code>
    /// </summary>
    /// <remarks>
    /// Nothing says how long the image data is short of decoding it, so its end is found by looking
    /// for the bytes <c>EI</c> - after the <c>~&gt;</c> that ends ASCII85 data, which may itself hold
    /// them. Binary data can hold them too, and then the guess is wrong. A content stream that ends
    /// before the image does leaves the image running to the end of the content.
    /// </remarks>
    public CSymbol ScanInlineImage()
    {
        var dictionaryStart = CurrentCharIndex;
        var foundData = ScanToImageData(out var dictionaryEnd, out var ascii85);
        InlineImageDictionary = RawText(dictionaryStart, dictionaryEnd).Trim(WhiteSpaceCharacters);

        var dataStart = foundData ? StartOfImageData(dictionaryEnd) : ContLength;

        // Look for '~>' because 'EI' may be part of the encoded image.
        if (ascii85)
            SkipToPair('~', '>');

        // Look for 'EI'.
        SkipToPair('E', 'I');

        var dataEnd = EndOfImageData(foundData, dataStart);
        InlineImageData = ContentBetween(dataStart, dataEnd);

        // Step over the EI itself, so that it is not read again as an operator of its own.
        if (_currChar != Chars.EOF)
        {
            ScanNextChar();
            ScanNextChar();
        }

        return CSymbol.None;
    }

    /// <summary>
    /// Reads the entries of an inline image up to and including its <c>ID</c>, and says whether
    /// there was one. Where the entries end is where the <c>ID</c> begins, or the end of the
    /// content when there is none.
    /// </summary>
    private bool ScanToImageData(out int dictionaryEnd, out bool ascii85)
    {
        dictionaryEnd = ContLength;
        ascii85 = false;
        while (ScanNextToken() != CSymbol.Eof)
        {
            // HACK: Is image ASCII85 decoded?
            if (!ascii85 && NamesAscii85Filter())
                ascii85 = true;

            if (Symbol == CSymbol.Operator && Token == "ID")
            {
                dictionaryEnd = _tokenStart;
                return true;
            }
        }
        return false;
    }

    private bool NamesAscii85Filter() => Symbol == CSymbol.Name && (Token is "/ASCII85Decode" or "/A85");

    /// <summary>
    /// Where the data of an inline image begins, given where its <c>ID</c> does. ID is followed by
    /// a single white-space character, which separates it from the data rather than belonging to it.
    /// </summary>
    private int StartOfImageData(int dictionaryEnd)
    {
        var dataStart = dictionaryEnd + 2;
        if (dataStart < ContLength && IsWhiteSpace((char)_content[dataStart]))
            dataStart++;
        return dataStart;
    }

    /// <summary>
    /// Where the data of an inline image ends, the reader standing on its <c>EI</c> or at the end of
    /// the content. The white space before EI separates it from the data, as the one after ID does,
    /// and is not kept: CInlineImage writes a separator of its own, so keeping this one too would
    /// add a byte to the data every time the content was read and written back.
    /// </summary>
    private int EndOfImageData(bool foundData, int dataStart)
    {
        var dataEnd = CurrentCharIndex;
        var foundEndOfImage = foundData && _currChar != Chars.EOF;
        if (foundEndOfImage && dataEnd > dataStart && IsWhiteSpace((char)_content[dataEnd - 1]))
            dataEnd--;
        return dataEnd;
    }

    /// <summary>
    /// Moves on until the current character and the one after it are the pair given, or to the end
    /// of the content. Stopping at the end addresses issue #354 - malformed PDF that ends without
    /// closing an inline image.
    /// </summary>
    private void SkipToPair(char first, char second)
    {
        while (_currChar != Chars.EOF && (_currChar != first || _nextChar != second))
            ScanNextChar();
    }

    /// <summary>A copy of the bytes of the content from one index up to another.</summary>
    private byte[] ContentBetween(int start, int end)
    {
        var bytes = new byte[Math.Max(0, end - start)];
        if (bytes.Length > 0)
            Array.Copy(_content, start, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// The entries of the inline image <see cref="ScanInlineImage"/> last read, as they were
    /// written between its <c>BI</c> and <c>ID</c>, one character per byte and without the white
    /// space around them.
    /// </summary>
    internal string InlineImageDictionary { get; private set; } = "";

    /// <summary>
    /// The bytes of the inline image <see cref="ScanInlineImage"/> last read, from after the white
    /// space that follows its <c>ID</c> to just before the white space that precedes its <c>EI</c>.
    /// </summary>
    internal byte[] InlineImageData { get; private set; } = [];

    private static readonly char[] WhiteSpaceCharacters = [Chars.NUL, Chars.HT, Chars.LF, Chars.FF, Chars.CR, Chars.SP];

    /// <summary>The bytes of the content from one index up to another, one character per byte.</summary>
    private string RawText(int start, int end)
    {
        if (end <= start)
            return "";

        var text = new StringBuilder(end - start);
        for (var idx = start; idx < end; idx++)
            text.Append((char)_content[idx]);
        return text.ToString();
    }

    /// <summary>
    /// The index in the content of <see cref="_currChar"/>, or the length of the content once it is
    /// exhausted. <see cref="_nextChar"/> is read one byte ahead, so the current character is two
    /// behind <see cref="_charIndex"/> - except at the very end, where nothing is read to follow it.
    /// A carriage return folded together with the line feed after it is at the line feed's index.
    /// </summary>
    private int CurrentCharIndex =>
        _currChar == Chars.EOF ? ContLength
        : _nextChar == Chars.EOF ? ContLength - 1
        : _charIndex - 2;

    /// <summary>
    /// Scans a name.
    /// </summary>
    public CSymbol ScanName()
    {
        Debug.Assert(_currChar == Chars.Slash);

        ClearToken();
        while (true)
        {
            var ch = AppendAndScanNextChar();
            // A name that ends the content stream never sees a delimiter, so give up at the
            // end of the content as well rather than appending Chars.EOF for ever.
            if (IsWhiteSpace(ch) || IsDelimiter(ch) || ch == Chars.EOF)
                return Symbol = CSymbol.Name;

            // A '#' followed by two hexadecimal digits stands for the byte they spell. Anything
            // else after it - one digit, a character that is not a digit, or the end of the
            // content - leaves the '#' as an ordinary character of the name, which is what it was
            // before PDF 1.2 gave it a meaning. int.Parse used to be handed whatever two characters
            // came next, and threw on /A#ZZ, taking the whole content stream down with it.
            if (ch != '#' || !IsHexChar(_nextChar) || !IsHexChar(PeekAfterNextChar()))
                continue;

            var high = ScanNextChar();
            var low = ScanNextChar();
            _currChar = (char)(HexValue(high) * 16 + HexValue(low));
        }
    }

    /// <summary>
    /// Scans a dictionary as one opaque token, from its opening <c>&lt;&lt;</c> to the
    /// <c>&gt;&gt;</c> that closes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The closing <c>&gt;&gt;</c> is the one that matches, which is not the same as the first
    /// <c>&gt;</c> that comes along. A hex string holds one, so does a nested dictionary, and so does
    /// a hex string inside a nested dictionary. Ending at the first one leaves the rest of the
    /// dictionary to be read as operators, and the stray <c>&gt;</c> then stops the whole content
    /// stream — which is what <c>/Span &lt;&lt;/ActualText &lt;FEFF00660069&gt;&gt;&gt; BDC</c> did,
    /// the sequence that says a ligature stands for several characters.
    /// </para>
    /// <para>
    /// A literal string and a comment are stepped over for the same reason: either may hold a
    /// <c>&gt;</c> that closes nothing. A string needs its escapes honoured to find where it ends, or
    /// an escaped closing parenthesis would end it early; a comment simply runs to the end of the
    /// line. A comment is legal wherever whitespace is, which includes between a dictionary's keys.
    /// </para>
    /// </remarks>
    protected CSymbol ScanDictionary()
    {
        ClearToken();

        // Both angle brackets of the opening '<<', taken before the loop starts. Left to the loop the
        // second of them reads as the start of a hex string, and everything up to the next '>' is
        // then skipped as if it were hex digits.
        _token.Append(_currChar);
        _token.Append(ScanNextChar());

        // One for the '<<' being opened here. A dictionary nested inside this one adds another, and
        // only the '>>' that takes the count back to nothing ends the token.
        var depth = 1;

        while (true)
        {
            var ch = ScanNextChar();

            // A truncated dictionary never sees its closing '>>', so give up at the end of the
            // content rather than appending Chars.EOF for ever.
            if (ch == Chars.EOF)
                return CSymbol.Dictionary;

            _token.Append(ch);

            if (!TryAppendInDictionary(ch, ref depth))
                return CSymbol.Dictionary;
        }
    }

    /// <summary>
    /// Appends whatever the character just appended to a dictionary opens, and tracks how deeply
    /// dictionaries are nested.
    /// </summary>
    /// <returns>False when the dictionary is over: closed, or cut off by the end of the content.</returns>
    private bool TryAppendInDictionary(char ch, ref int depth)
    {
        switch (ch)
        {
            case '<':
                return TryAppendAngleBracketInDictionary(ref depth);

            case '(':
                return TryAppendLiteralStringInDictionary();

            case '%':
                AppendCommentInDictionary();
                return true;

            case '>':
                return !ClosesDictionary(ref depth);

            default:
                return true;
        }
    }

    /// <summary>
    /// Appends a nested dictionary's '&lt;&lt;' or a hex string, its first '&lt;' already appended.
    /// </summary>
    /// <returns>False when the content ends first.</returns>
    private bool TryAppendAngleBracketInDictionary(ref int depth)
    {
        if (_nextChar != '<')
            return TryAppendHexStringInDictionary();

        _token.Append(ScanNextChar());
        depth++;
        return true;
    }

    /// <summary>
    /// Appends the second '&gt;' of a '&gt;&gt;', the first already appended, and says whether it
    /// closed the dictionary being scanned rather than one nested in it.
    /// </summary>
    private bool ClosesDictionary(ref int depth)
    {
        if (_nextChar != '>')
            return false;

        _token.Append(ScanNextChar());
        if (--depth > 0)
            return false;

        // Left standing on the character after the dictionary, which is where every
        // other scan leaves the reader.
        ScanNextChar();
        return true;
    }

    /// <summary>
    /// Appends a hex string inside a dictionary to the token, its '&lt;' already appended. Its
    /// '&gt;' is not the dictionary's, so it is read past.
    /// </summary>
    /// <returns>False when the content ends first.</returns>
    private bool TryAppendHexStringInDictionary()
    {
        while (true)
        {
            var ch = ScanNextChar();
            if (ch == Chars.EOF)
                return false;

            _token.Append(ch);
            if (ch == '>')
                return true;
        }
    }

    /// <summary>
    /// Appends a literal string inside a dictionary to the token, its '(' already appended. It ends
    /// at the parenthesis that balances that one - counting the nested pairs it is allowed to hold,
    /// and skipping whatever a backslash escapes so that an escaped parenthesis does not close it.
    /// </summary>
    /// <returns>False when the content ends first.</returns>
    private bool TryAppendLiteralStringInDictionary()
    {
        var parentheses = 1;
        while (parentheses > 0)
        {
            var ch = ScanNextChar();
            if (ch == Chars.EOF)
                return false;

            _token.Append(ch);

            if (ch == '\\' && !TryAppendNextChar())
                return false;

            parentheses += ch switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0
            };
        }
        return true;
    }

    /// <summary>
    /// Appends the next character to the token, whatever it is.
    /// </summary>
    /// <returns>False when the content ends first.</returns>
    private bool TryAppendNextChar()
    {
        var ch = ScanNextChar();
        if (ch == Chars.EOF)
            return false;

        _token.Append(ch);
        return true;
    }

    /// <summary>
    /// Appends a comment inside a dictionary to the token, its '%' already appended. It runs to the
    /// end of the line. Whatever it says is not syntax, so a '&gt;&gt;' inside one closes nothing.
    /// </summary>
    private void AppendCommentInDictionary()
    {
        while (_nextChar != Chars.CR && _nextChar != Chars.LF && _nextChar != Chars.EOF)
            _token.Append(ScanNextChar());
    }

    /// <summary>
    /// Scans an integer or real number.
    /// </summary>
    public CSymbol ScanNumber()
    {
        ClearToken();
        var negative = ScanSign();
        var value = ScanDigits(out var period, out var decimalDigits, out var overflow);

        if (negative)
            value = -value;
        if (period)
        {
            if (decimalDigits > 0 || overflow)
            {
                // Read from the token rather than worked out from the digits gathered above.
                // Those stop at the tenth decimal place, and a matrix written out to the
                // precision of a double - which is what a writer of PDF puts out for a
                // rotation - would otherwise be read as a slightly different matrix, or run
                // off the end of a table of powers of ten trying.
                _tokenAsReal = double.Parse(_token.ToString(), CultureInfo.InvariantCulture);
            }
            else
            {
                _tokenAsReal = value;
                _tokenAsLong = value;
            }
            return CSymbol.Real;
        }

        if (overflow)
        {
            // More digits than any Int64 holds - not merely out of range for CSymbol.Integer,
            // which the check below would otherwise degrade to a real anyway, but out of range
            // for the accumulator itself. _tokenAsLong is left unset: nothing reads it once the
            // symbol is Real.
            _tokenAsReal = double.Parse(_token.ToString(), CultureInfo.InvariantCulture);
            return CSymbol.Real;
        }

        return ScannedInteger(value);
    }

    /// <summary>
    /// Appends the sign a number may begin with to the token and steps over it, and says whether
    /// it was a minus.
    /// </summary>
    private bool ScanSign()
    {
        var ch = _currChar;
        if (ch is not ('+' or '-'))
            return false;

        _token.Append(ch);
        ScanNextChar();
        return ch == '-';
    }

    /// <summary>
    /// Appends the digits of a number and its decimal point to the token, and works out the value
    /// they spell as far as a long and ten decimal places will hold it, the decimal point left out.
    /// </summary>
    /// <param name="period">Whether the number has a decimal point.</param>
    /// <param name="decimalDigits">How many of the digits after it are in the value.</param>
    /// <param name="overflow">
    /// Set once the integer part alone would no longer fit in a long - a nineteen-or-more
    /// digit token, which unchecked arithmetic would otherwise wrap silently rather than
    /// report. Once set, the value is no longer trustworthy and the token text is read directly
    /// instead, the same way a real with more than ten decimal digits already is.
    /// </param>
    private long ScanDigits(out bool period, out int decimalDigits, out bool overflow)
    {
        long value = 0;
        period = false;
        decimalDigits = 0;
        overflow = false;

        var ch = _currChar;
        while (true)
        {
            if (char.IsDigit(ch))
            {
                _token.Append(ch);
                AccumulateDigit(ch, period, ref value, ref decimalDigits, ref overflow);
            }
            else if (ch == '.')
            {
                if (period)
                    ContentReaderDiagnostics.ThrowContentReaderException("More than one period in number.");

                period = true;
                _token.Append(ch);
            }
            else
            {
                return value;
            }
            ch = ScanNextChar();
        }
    }

    /// <summary>
    /// Adds a digit to the value <see cref="ScanDigits"/> is working out. Digits beyond the tenth
    /// decimal place are left to the token alone.
    /// </summary>
    private static void AccumulateDigit(char digit, bool period, ref long value, ref int decimalDigits, ref bool overflow)
    {
        if (decimalDigits >= 10)
            return;

        if (!period && value > (long.MaxValue - 9) / 10)
            overflow = true;
        else
            value = 10 * value + digit - '0';
        if (period)
            decimalDigits++;
    }

    /// <summary>
    /// Keeps the value of a number with no decimal point that fits in a long, and says which
    /// symbol it is.
    /// </summary>
    private CSymbol ScannedInteger(long value)
    {
        _tokenAsLong = value;
        _tokenAsReal = Convert.ToDouble(value);

        Debug.Assert(long.Parse(_token.ToString(), CultureInfo.InvariantCulture) == value);

        if (value is >= int.MinValue and < int.MaxValue)
            return CSymbol.Integer;

        // Out of range for CSymbol.Integer, which a content operand is expected to fit. The
        // document lexer degrades a too-large integer to a real rather than refuse it outright -
        // CSymbol has no separate "long integer" symbol to reach for instead, so a real is the
        // same fallback here. _tokenAsReal was already set to this value above.
        return CSymbol.Real;
    }

    /// <summary>
    /// Scans an operator.
    /// </summary>
    public CSymbol ScanOperator()
    {
        ClearToken();
        var ch = _currChar;
        // Scan token
        while (IsOperatorChar(ch))
            ch = AppendAndScanNextChar();

        // d0 and d1 are the only content operators with a digit in them, and a Type 3 glyph
        // description has to begin with one of the two. IsOperatorChar cannot take digits in
        // general - an operator written hard against its successor's operand would swallow it -
        // so the pair is spelled out here. Without this, "1000 0 0 0 200 200 d1 /Im1 Do" read as
        // the setdash operator with six operands followed by Do with two, and every operator
        // after the glyph's first was handed one operand too many.
        if (_token.Length == 1 && _token[0] == 'd' && (ch is '0' or '1'))
            AppendAndScanNextChar();

        return Symbol = CSymbol.Operator;
    }

    /// <summary>Scans a string written in parentheses, resolving the escapes inside it.</summary>
    /// <remarks>
    /// <para>
    /// The escapes are resolved on the bytes first, and a string that opens with a UTF-16 byte
    /// order mark is decoded afterwards, from the bytes that leaves - the order the document
    /// lexer's <c>ScanLiteralString</c> works in. An escape in a literal string acts on bytes
    /// whatever they go on to spell: <c>\n</c> is two bytes standing for one, and a backslash
    /// before an end of line is two or three bytes standing for none, neither of which is a whole
    /// number of UTF-16 code units.
    /// </para>
    /// <para>
    /// A wide string used to be read two bytes at a time by a second copy of the loop, with each
    /// escape worked out on a code unit. A line continuation then left every pair after it
    /// straddling two characters, and the string ran on past the parenthesis that closed it.
    /// </para>
    /// </remarks>
    public CSymbol ScanLiteralString()
    {
        Debug.Assert(_currChar == Chars.ParenLeft);

        ClearToken();
        var parenLevel = 0;
        // Read with folding off throughout: the document lexer's ScanLiteralString does the same,
        // so a raw carriage return inside the string is kept rather than turned into a line feed,
        // and only an escaped one - '\' before either end-of-line spelling - continues the line.
        var ch = ScanNextChar(false);
        while (true)
        {
            // An unterminated string never sees its closing ')', so give up at the end
            // of the content rather than appending Chars.EOF for ever.
            if (ch == Chars.EOF)
                return Symbol = DecodeLiteralString(terminated: false);

            if (ClosesLiteralString(ch, ref parenLevel))
            {
                ScanNextChar(false);
                return Symbol = DecodeLiteralString(terminated: true);
            }

            // A backslash right before either spelling of an end of line continues the
            // string onto the next one; neither the backslash nor the line ending becomes
            // part of it, and ch is then what follows the line ending.
            if (ch == '\\' && !TryReadEscapedChar(out ch))
                continue;

            _token.Append(ch);
            ch = ScanNextChar(false);
        }
    }

    /// <summary>
    /// Counts an unescaped parenthesis of a literal string, and says whether it is the one that
    /// closes the string.
    /// </summary>
    private static bool ClosesLiteralString(char ch, ref int parenLevel)
    {
        switch (ch)
        {
            case '(':
                parenLevel++;
                return false;

            case ')' when parenLevel == 0:
                return true;

            case ')':
                parenLevel--;
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Reads what follows a backslash in a literal string, the backslash being the current
    /// character, and resolves it to the character it stands for.
    /// </summary>
    /// <returns>
    /// False when the backslash continues the line instead, in which case <paramref name="ch"/> is
    /// the character after the line ending and still to be read as part of the string - and false
    /// too when the content ends right after the backslash, leaving <paramref name="ch"/> the
    /// end-of-file marker for the caller to stop at.
    /// </returns>
    private bool TryReadEscapedChar(out char ch)
    {
        ch = ScanNextChar(false);

        // The end-of-file marker is not a character of the string. Resolved as an escape it
        // stood for itself, and appending it put U+FFFF in the middle of the text.
        if (ch == Chars.EOF)
            return false;

        if (ch is Chars.CR or Chars.LF)
        {
            // CR LF is one line ending, not a CR ending the line and an LF opening the next.
            var lineEnding = ch;
            ch = ScanNextChar(false);
            if (lineEnding == Chars.CR && ch == Chars.LF)
                ch = ScanNextChar(false);
            return false;
        }

        if (TryResolveSimpleEscape(ch, out var resolved))
            ch = resolved;
        else if (IsOctalDigit(ch))
            ch = ReadOctalEscape(ch);

        // Anything else stands for itself, and the backslash is dropped.
        return true;
    }

    /// <summary>
    /// Resolves the escapes a literal string writes as a backslash and one character.
    /// </summary>
    private static bool TryResolveSimpleEscape(char ch, out char resolved)
    {
        switch (ch)
        {
            case 'n':
                resolved = Chars.LF;
                return true;

            case 'r':
                resolved = Chars.CR;
                return true;

            case 't':
                resolved = Chars.HT;
                return true;

            case 'b':
                resolved = Chars.BS;
                return true;

            case 'f':
                resolved = Chars.FF;
                return true;

            case '(':
                resolved = Chars.ParenLeft;
                return true;

            case ')':
                resolved = Chars.ParenRight;
                return true;

            case '\\':
                resolved = Chars.BackSlash;
                return true;

            default:
                resolved = ch;
                return false;
        }
    }

    /// <summary>
    /// Reads an octal character code of up to three digits, the first of which has just been read.
    /// </summary>
    private char ReadOctalEscape(char first)
    {
        var n = first - '0';
        if (IsOctalDigit(_nextChar))
        {
            n = n * 8 + ScanNextChar(false) - '0';
            if (IsOctalDigit(_nextChar))
                n = n * 8 + ScanNextChar(false) - '0';
        }
        return (char)n;
    }

    /// <summary>
    /// Decodes the bytes <see cref="ScanLiteralString"/> has gathered in the token as UTF-16 when
    /// they open with a byte order mark, and says which kind of string they turned out to be.
    /// </summary>
    /// <param name="terminated">
    /// Whether the string reached its closing parenthesis. It decides what becomes of a lone
    /// byte left over at the end: a string that ended properly is short of the low byte of its
    /// last character, which is taken to be a zero as the document lexer takes it, while a string
    /// the content cut off lost the rest of that character, and the half of it is dropped rather
    /// than turned into a character nobody wrote.
    /// </param>
    private CSymbol DecodeLiteralString(bool terminated)
    {
        // The reference only names the big-endian byte order mark, but Adobe Reader also accepts
        // the little-endian one - the document lexer does too, and a byte-swapped string here
        // should read the same text it does there.
        var bigEndian = _token.Length >= 2 && _token[0] == '\xFE' && _token[1] == '\xFF';
        var littleEndian = _token.Length >= 2 && _token[0] == '\xFF' && _token[1] == '\xFE';
        if (!bigEndian && !littleEndian)
            return CSymbol.String;

        var bytes = _token.ToString();
        var length = EvenLength(ref bytes, terminated);

        _token.Length = 0;
        for (var idx = 2; idx < length; idx += 2)
        {
            _token.Append(bigEndian
                ? (char)(bytes[idx] * 256 + bytes[idx + 1])
                : (char)(bytes[idx + 1] * 256 + bytes[idx]));
        }
        return CSymbol.UnicodeString;
    }

    /// <summary>
    /// Answers the even number of bytes a UTF-16 string is decoded from. An odd byte left over
    /// at the end is completed with a zero when the string was terminated, and dropped when it
    /// was cut off.
    /// </summary>
    private static int EvenLength(ref string bytes, bool terminated)
    {
        var length = bytes.Length;
        if ((length & 1) == 0)
            return length;

        if (!terminated)
            return length - 1;

        bytes += '\0';
        return length + 1;
    }

    /// <summary>Scans a string written in angle brackets as pairs of hexadecimal digits.</summary>
    public CSymbol ScanHexadecimalString()
    {
        Debug.Assert(_currChar == Chars.Less);

        ClearToken();
        var hex = new char[2];
        ScanNextChar();
        while (true)
        {
            MoveToNonWhiteSpace();
            // A truncated hex string never sees its closing '>', so give up at the end of
            // the content rather than spinning on Chars.EOF.
            if (_currChar == Chars.EOF)
                break;

            if (_currChar == '>')
            {
                ScanNextChar();
                break;
            }
            if (!IsHexChar(_currChar))
            {
                // Neither '>' nor a hex digit: step over it rather than never advancing.
                ScanNextChar();
                continue;
            }

            _token.Append(ReadHexByte(hex));
        }
        return Symbol = DecodeHexString();
    }

    /// <summary>
    /// Reads the byte whose first hex digit is the current character, into and out of
    /// <paramref name="hex"/>.
    /// </summary>
    private char ReadHexByte(char[] hex)
    {
        hex[0] = _currChar;
        ScanNextChar();
        // What may come between the two digits of a byte is what may come before one:
        // white space, and anything else that is not a digit. Only the end of the
        // string decides that the second digit is missing rather than merely late.
        while (!IsHexChar(_currChar) && _currChar != '>' && _currChar != Chars.EOF)
            ScanNextChar();

        if (IsHexChar(_currChar))
        {
            hex[1] = _currChar;
            ScanNextChar();
        }
        else
        {
            // A hex string with an odd number of digits ends in a zero.
            hex[1] = '0';
        }
        return (char)int.Parse(new string(hex), NumberStyles.AllowHexSpecifier);
    }

    /// <summary>
    /// Decodes the bytes <see cref="ScanHexadecimalString"/> has gathered in the token as UTF-16
    /// when they open with a byte order mark, and says which kind of string they turned out to be.
    /// </summary>
    private CSymbol DecodeHexString()
    {
        var chars = _token.ToString();
        if (chars.Length <= 2 || chars[0] != (char)0xFE || chars[1] != (char)0xFF)
            return CSymbol.HexString;

        // A Unicode hex string missing half of its last character is short of the low byte
        // of that character, which is taken to be a zero - the same reading a hex string
        // missing its final digit gets, just above. Debug.Assert(count % 2 == 0) stood here
        // instead: it caught the odd count in a Debug build and did nothing in a Release
        // build, where the loop below read one character past the end of the string.
        var count = EvenLength(ref chars, terminated: true);
        _token.Length = 0;
        for (var idx = 2; idx < count; idx += 2)
            _token.Append((char)(chars[idx] * 256 + chars[idx + 1]));
        return CSymbol.UnicodeHexString;
    }

    /// <summary>
    /// Move current position one character further in content stream, folding a carriage return
    /// into a line feed when <paramref name="handleCrlf"/> is set - CR LF becomes LF, and a lone
    /// CR becomes LF as well. A literal string reads its characters with it clear, the way the
    /// document lexer's <c>Lexer.ScanNextChar</c> does, so that a raw carriage return inside the
    /// string is kept rather than folded, and only an escaped one - <c>\</c> followed by either
    /// end-of-line spelling - continues the line.
    /// </summary>
    internal char ScanNextChar(bool handleCrlf = true)
    {
        if (ContLength <= _charIndex)
        {
            // _nextChar is read one character ahead, so the last character of the content is
            // waiting in it when _charIndex reaches the end. Hand it over before reporting
            // the end of the content, which the next call then does.
            _currChar = _nextChar;
            _nextChar = Chars.EOF;
            // Treat a single CR as LF, as the branch below does. Nothing is left to pair it
            // with, so it cannot be the CR of a CR LF.
            if (handleCrlf && _currChar == Chars.CR)
                _currChar = Chars.LF;
        }
        else
        {
            CharacterScanning.Advance(ref _currChar, ref _nextChar, handleCrlf, _readNextRawByte);
        }
        return _currChar;
    }

    /// <summary>
    /// Reads the next raw byte from the content and advances <see cref="_charIndex"/> past it, or
    /// returns <see cref="Chars.EOF"/> once the content is exhausted. Cached as
    /// <see cref="_readNextRawByte"/> rather than a method group conversion at each call site.
    /// </summary>
    private char ReadNextRawByte() => ContLength <= _charIndex ? Chars.EOF : (char)_content[_charIndex++];

    private char ScanNextCharFolding() => ScanNextChar();

    /// <summary>
    /// The character after <see cref="_nextChar"/>, without reading it: the byte
    /// <see cref="_charIndex"/> already points at, or <see cref="Chars.EOF"/> past the end.
    /// </summary>
    private char PeekAfterNextChar() => ContLength <= _charIndex ? Chars.EOF : (char)_content[_charIndex];

    /// <summary>The value of a character <see cref="IsHexChar"/> accepts.</summary>
    private static int HexValue(char ch) => ch <= '9' ? ch - '0' : (ch | 0x20) - 'a' + 10;

    /// <summary>
    /// Resets the current token to the empty string.
    /// </summary>
    private void ClearToken()
    {
        _token.Length = 0;
        _tokenAsLong = 0;
        _tokenAsReal = 0;
    }

    /// <summary>
    /// Appends current character to the token and reads next one.
    /// </summary>
    internal char AppendAndScanNextChar()
    {
        // The document lexer refuses rather than appends the end-of-content marker itself, so a
        // grammar rule that keeps calling this past the end - rather than stopping at the
        // character it was just handed - is stopped here instead of growing a token out of a
        // sentinel that is not content.
        if (_currChar == Chars.EOF)
            ContentReaderDiagnostics.ThrowContentReaderException("Undetected EOF reached.");

        _token.Append(_currChar);
        return ScanNextChar();
    }

    /// <summary>
    /// If the current character is not a white space, the function immediately returns it.
    /// Otherwise the PDF cursor is moved forward to the first non-white space or EOF. White
    /// spaces are NUL, HT, LF, FF, CR, SP, a vertical tab, and a soft hyphen.
    /// </summary>
    public char MoveToNonWhiteSpace() =>
        _currChar = CharacterScanning.SkipWhiteSpace(_currChar, _scanNextCharFolding);

    /// <summary>
    /// Gets or sets the current symbol.
    /// </summary>
    public CSymbol Symbol { get; set; } = CSymbol.None;

    /// <summary>
    /// Gets the current token.
    /// </summary>
    public string Token => _token.ToString();

    /// <summary>
    /// Interprets current token as integer literal.
    /// </summary>
    internal int TokenToInteger
    {
        get
        {
            Debug.Assert(_tokenAsLong == int.Parse(_token.ToString(), CultureInfo.InvariantCulture));
            return (int)_tokenAsLong;
        }
    }

    /// <summary>
    /// Interpret current token as real or integer literal.
    /// </summary>
    internal double TokenToReal
    {
        get
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            #pragma warning disable S1244 // Exact on purpose: asserts the same text parses to the same value both ways.
            Debug.Assert(_tokenAsReal == double.Parse(_token.ToString(), CultureInfo.InvariantCulture));
            #pragma warning restore S1244
            return _tokenAsReal;
        }
    }

    /// <summary>
    /// Indicates whether the specified character is a content stream white-space character.
    /// </summary>
    internal static bool IsWhiteSpace(char ch) => CharacterScanning.IsWhiteSpace(ch);

    /// <summary>
    /// Indicates whether the specified character is an content operator character.
    /// </summary>
    internal static bool IsOperatorChar(char ch)
    {
        if (char.IsLetter(ch))
            return true;
        return ch switch
        {
            Chars.Asterisk           // *
                or Chars.QuoteSingle // '
                or Chars.QuoteDbl    // "
                => true,
            _ => false
        };
    }

    /// <summary>
    /// Indicates whether the specified character is an octal digit. A literal string escapes a
    /// character code in octal, so only '0' to '7' count — '8' and '9' end the code rather than
    /// extending it, and a backslash before either is dropped and the digit kept as text.
    /// </summary>
    internal static bool IsOctalDigit(char ch) => CharacterScanning.IsOctalDigit(ch);

    /// <summary>
    /// Indicates whether the specified character is a hexadecimal digit.
    /// </summary>
    internal static bool IsHexChar(char ch) => CharacterScanning.IsHexChar(ch);

    /// <summary>
    /// Indicates whether the specified character is a PDF delimiter character.
    /// </summary>
    internal static bool IsDelimiter(char ch) => CharacterScanning.IsDelimiter(ch);

    /// <summary>
    /// Gets the length of the content.
    /// </summary>
    public int ContLength => _content.Length;

    // ad
    /// <summary>Gets or sets how far through the content the lexer has read, in bytes.</summary>
    public int Position
    {
        get => _charIndex;
        set
        {
            _charIndex = value;
            _currChar = (char)_content[_charIndex - 1];
            _nextChar = (char)_content[_charIndex - 1];
        }
    }

    private readonly byte[] _content;
    private int _charIndex;
    private char _currChar;
    private char _nextChar;

    // Cached rather than a method group conversion at each call site, since
    // CharacterScanning.Advance and .SkipWhiteSpace are called once per character scanned.
    private readonly Func<char> _readNextRawByte;
    private readonly Func<char> _scanNextCharFolding;

    private readonly StringBuilder _token = new();
    // Where in the content the token last scanned by ScanNextToken begins.
    private int _tokenStart;
    private long _tokenAsLong;
    private double _tokenAsReal;
}
