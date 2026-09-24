#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using System.Globalization;
using System.Linq;
using PinataLayout.DocumentObjectModel.Resources;


/*
  ddl = <document> | <empty>

  table-element:
    \table «attributes»opt { «columns-element» «rows-element» }

  table-element:
    \table «attributes»opt { «columns-element» «rows-element» }
*/

namespace PinataLayout.DocumentObjectModel.IO;

/// <summary>
/// DdlScanner
/// </summary>
internal class DdlScanner
{
  /// <summary>
  /// Initializes a new instance of the DdlScanner class.
  /// </summary>
  internal DdlScanner(string documentFileName, string ddl)
  {
    Init(ddl, documentFileName);
  }

  /// <summary>
  /// Initializes a new instance of the DdlScanner class.
  /// </summary>
  internal DdlScanner(string ddl)
    : this("", ddl)
  {
  }

  /// <summary>
  /// Initializes all members and prepares the scanner.
  /// </summary>
  internal bool Init(string document, string documentFileName)
  {
    DocumentPath = documentFileName;
    m_strDocument = document;
    ddlLength = m_strDocument.Length;
    m_idx = 0;
    m_idxLine = 1;
    m_idxLinePos = 0;

    DocumentFileName = documentFileName;

    CurrentLine = m_idxLine;
    CurrentLinePos = m_idxLinePos;

    ScanNextChar();

    return true;
  }

  /// <summary>
  /// Reads to the next DDL token. Comments are ignored.
  /// </summary>
  /// <returns>
  /// Returns the current symbol.
  /// It is Symbol.Eof if the end of the DDL string is reached.
  /// </returns>
  internal Symbol ReadCode()
  {
    while (true)
    {
      symbol = Symbol.None;
      tokenType = TokenType.None;
      token = "";

      MoveToNonWhiteSpace();
      SaveCurDocumentPos();

      if (currChar == Chars.Null)
      {
        symbol = Symbol.Eof;
        return Symbol.Eof;
      }

      // Token is comment. In code comments are ignored. No other token starts with '/', so
      // testing for one first takes nothing from the tests in ScanCodeToken.
      if (currChar == '/' && nextChar == '/')
      {
        ScanSingleLineComment();
        continue;
      }

      ScanCodeToken();
      return symbol;
    }
  }

  /// <summary>
  /// Scans the token of code starting at the current character, which is neither the end of the
  /// document nor the start of a comment, and sets symbol and tokenType to what it is.
  /// </summary>
  private void ScanCodeToken()
  {
    if (IsIdentifierChar(currChar, true))
    {
      ScanIdentifierOrKeyword();
    }
    else if (currChar == '"')
    {
      // Token is string literal.
      token += ScanStringLiteral();
      symbol = Symbol.StringLiteral;
      tokenType = TokenType.StringLiteral;
    }
    else if (IsNumberStart)
    {
      // Token is number literal.
      symbol = ScanNumber(false);
      tokenType = NumberTokenType(symbol);
    }
    else if (IsPointBeforeDigit)
    {
      // Token is real literal.
      symbol = ScanNumber(true);
      tokenType = TokenType.RealLiteral;
    }
    else if (currChar == '\\')
    {
      // Token is keyword.
      token = "\\";
      symbol = ScanKeyword();
      tokenType = KeywordTokenType(symbol);
    }
    else if (IsVerbatimStringStart)
    {
      // Token is verbatim string literal.
      ScanNextChar();
      token += ScanVerbatimStringLiteral();
      symbol = Symbol.StringLiteral;
      tokenType = TokenType.StringLiteral;
    }
    else
    {
      // Punctuator or syntax error.
      symbol = ScanPunctuator();
    }
  }

  /// <summary>
  /// Whether the current character is a point that starts a real literal, such as «.5».
  /// </summary>
  private bool IsPointBeforeDigit => currChar == '.' && IsDigit(nextChar);

  /// <summary>
  /// Whether the current characters start a verbatim string literal, «@"».
  /// </summary>
  private bool IsVerbatimStringStart => currChar == '@' && nextChar == '"';

  private static TokenType NumberTokenType(Symbol symbol)
    => symbol == Symbol.RealLiteral ? TokenType.RealLiteral : TokenType.IntegerLiteral;

  private static TokenType KeywordTokenType(Symbol symbol)
    => symbol != Symbol.None ? TokenType.KeyWord : TokenType.None;

  /// <summary>
  /// Whether the current character starts a number: a digit, or a sign followed by one.
  /// </summary>
  private bool IsNumberStart =>
    IsDigit(currChar) || currChar is '-' or '+' && IsDigit(nextChar);

  /// <summary>
  /// Scans an identifier, which is a keyword instead when it is one of the keywords that do not
  /// start with a backslash.
  /// </summary>
  private void ScanIdentifierOrKeyword()
  {
    symbol = ScanIdentifier();
    tokenType = TokenType.Identifier;
    // Some keywords do not start with a backslash: true, false, and null.
    var sym = KeyWords.SymbolFromName(token);
    if (sym != Symbol.None)
    {
      symbol = sym;
      tokenType = TokenType.KeyWord;
    }
  }

  /// <summary>
  /// Gets the next keyword at the current position without touching the DDL cursor.
  /// </summary>
  internal Symbol PeekKeyword()
  {
    Debug.Assert(currChar == Chars.BackSlash);

    return PeekKeyword(m_idx);
  }

  /// <summary>
  /// Gets the next keyword without touching the DDL cursor.
  /// </summary>
  internal Symbol PeekKeyword(int index)
  {
    // Check special keywords
    switch (m_strDocument[index])
    {
      case '{':
      case '}':
      case '\\':
      case '-':
      case '(':
        return Symbol.Character;
    }

    var keyword = "\\";
    var idx = index;
    var length = ddlLength - idx;
    while (length > 0)
    {
      var ch = m_strDocument[idx++];
      if (IsLetter(ch))
      {
        keyword += ch;
        length--;
      }
      else
      {
        break;
      }
    }
    return KeyWords.SymbolFromName(keyword);
  }

  /// <summary>
  /// Gets the next punctuator terminal symbol without touching the DDL cursor.
  /// </summary>
  protected Symbol PeekPunctuator(int index)
  {
    var ch = m_strDocument[index];
    return ch switch
    {
      '+' => IsAssignAfter(index) ? Symbol.PlusAssign : Symbol.Plus,
      '-' => IsAssignAfter(index) ? Symbol.MinusAssign : Symbol.Minus,
      _ => SingleCharPunctuator(ch)
    };
  }

  /// <summary>
  /// Whether the character after the one at the given index is '='.
  /// </summary>
  /// <remarks>
  /// The bound is "there is a character after this one", so it is > rather than >=: with >=,
  /// a '+' or '-' as the last character of the document read one past the end. ScanPunctuator
  /// has the same two arms and gets this right because it looks at nextChar, which is null at
  /// the end of the buffer rather than out of it.
  /// </remarks>
  private bool IsAssignAfter(int index) =>
    ddlLength > index + 1 && m_strDocument[index + 1] == '=';

  /// <summary>
  /// The punctuator terminal symbol a character is on its own, or Symbol.None if it is none.
  /// '+' and '-' are left to the callers, because whether they are one depends on what follows.
  /// </summary>
  private static Symbol SingleCharPunctuator(char ch) =>
    ch switch
    {
      '{' => Symbol.BraceLeft,
      '}' => Symbol.BraceRight,
      '[' => Symbol.BracketLeft,
      ']' => Symbol.BracketRight,
      '(' => Symbol.ParenLeft,
      ')' => Symbol.ParenRight,
      ':' => Symbol.Colon,
      ';' => Symbol.Semicolon,
      '.' => Symbol.Dot,
      ',' => Symbol.Comma,
      '%' => Symbol.Percent,
      '$' => Symbol.Dollar,
      '@' => Symbol.At,
      '#' => Symbol.Hash,
      '¤' => Symbol.Currency, //??? used in DDL?
      '=' => Symbol.Assign,
      '/' => Symbol.Slash,
      '\\' => Symbol.BackSlash,
      Chars.CR => Symbol.CR,
      Chars.LF => Symbol.LF,
      Chars.Space => Symbol.Blank,
      Chars.Null => Symbol.Eof,
      _ => Symbol.None
    };

  /// <summary>
  /// Gets the next symbol without touching the DDL cursor.
  /// </summary>
  internal Symbol PeekSymbol()
  {
    var idx = m_idx - 1;
    var length = ddlLength - idx;

    // Move to first non whitespace
    var ch = char.MinValue;
    while (length > 0)
    {
      ch = m_strDocument[idx++];
      if (!IsWhiteSpace(ch))
        break;
      length--;
    }

    if (IsLetter(ch))
      return Symbol.Text;
    if (ch == '\\')
      return PeekKeyword(idx);
    return PeekPunctuator(idx - 1);
  }

  /// <summary>
  /// Reads either text or \keyword from current position.
  /// </summary>
  internal Symbol ReadText(bool rootLevel)
  {
    // Previous call encountered an empty line.
    if (emptyLine)
    {
      emptyLine = false;
      symbol = Symbol.EmptyLine;
      tokenType = TokenType.None;
      token = "";
      return Symbol.EmptyLine;
    }

    // Init for scanning.
    prevSymbol = symbol;
    symbol = Symbol.None;
    tokenType = TokenType.None;
    token = "";

    // Save where we are
    SaveCurDocumentPos();

    // Check for EOF.
    if (currChar == Chars.Null)
    {
      symbol = Symbol.Eof;
      return Symbol.Eof;
    }

    // Check for keyword or escaped character.
    if (currChar == '\\')
      return ReadEscapeOrKeyword(rootLevel);

    // Check for reserved terminal symbols in text.
    switch (currChar)
    {
      case '{':
        AppendAndScanNextChar();
        symbol = Symbol.BraceLeft;
        tokenType = TokenType.OperatorOrPunctuator;
        return Symbol.BraceLeft;  // Syntax error in any case.

      case '}':
        AppendAndScanNextChar();
        symbol = Symbol.BraceRight;
        tokenType = TokenType.OperatorOrPunctuator;
        return Symbol.BraceRight;
    }

    // Check for end of line.
    if (currChar != Chars.LF)
      return ReadPlainText(rootLevel);

    return ReadLineEnd(rootLevel);
  }

  /// <summary>
  /// Reads from a backslash in text: an escaped character begins plain text, anything else is a
  /// keyword or a syntax error.
  /// </summary>
  private Symbol ReadEscapeOrKeyword(bool rootLevel)
  {
    switch (nextChar)
    {
      case '\\':
      case '{':
      case '}':
      case '/':
      case '-':
        return ReadPlainText(rootLevel);
    }
    // Either key word or syntax error.
    token = "\\";
    return ScanKeyword();
  }

  /// <summary>
  /// Reads from the end of a line of text: a blank when the paragraph continues on the next line,
  /// otherwise the empty line or closing brace that ends it.
  /// </summary>
  private Symbol ReadLineEnd(bool rootLevel)
  {
    // The line ends here. See if the paragraph continues in the next line.
    if (MoveToNextParagraphContentLine(rootLevel))
    {
      // Paragraph continues in next line. Simulate the read of a blank to separate words.
      token = " ";
      if (IgnoreLineBreak())
        token = "";
      this.symbol = Symbol.Text;
      return Symbol.Text;
    }

    // Paragraph ends here. Return NewLine or BraceRight.
    if (currChar != Chars.BraceRight)
    {
      symbol = Symbol.EmptyLine;
      tokenType = TokenType.None; //???
      return Symbol.EmptyLine;
    }

    AppendAndScanNextChar();
    symbol = Symbol.BraceRight;
    tokenType = TokenType.OperatorOrPunctuator;
    return Symbol.BraceRight;
  }

  /// <summary>
  /// Returns whether the linebreak should be ignored, because the previous symbol is already a whitespace.
  /// </summary>
  private bool IgnoreLineBreak()
  {
    return prevSymbol switch
    {
      Symbol.LineBreak or Symbol.Space or Symbol.Tab => true,
      _ => false
    };
  }

  /// <summary>
  /// Read text from current position until block ends or \keyword occurs.
  /// </summary>
  private Symbol ReadPlainText(bool rootLevel)
  {
    var foundSpace = false;
    while (currChar != Chars.Null)
    {
      if (!ReadPlainTextStep(rootLevel, ref foundSpace))
        break;
    }

    symbol = Symbol.Text;
    tokenType = TokenType.Text;
    return Symbol.Text;
  }

  /// <summary>
  /// Reads the next piece of plain text: an escape, a comment, a line end or a character.
  /// Returns false where the text ends instead.
  /// </summary>
  private bool ReadPlainTextStep(bool rootLevel, ref bool foundSpace)
  {
    // Check for escaped character or keyword.
    if (currChar == '\\')
      return ScanTextEscape(); // false: Keyword

    // Check for reserved terminal symbols in text: '{' is a syntax error any way, '}' is the
    // block end.
    if (currChar is '{' or '}')
      return false;

    // A comment runs to the end of the line, which is then handled as any other. A comment
    // that runs to the end of the document leaves the end of the document as the current
    // character, and that is appended like any other character below.
    if (currChar == '/' && nextChar == '/')
      ScanToEol();

    // Check for end of line.
    if (currChar == Chars.LF)
      return ContinuePlainTextOnNextLine(rootLevel);

    foundSpace = AppendTextChar(foundSpace);
    return true;
  }

  /// <summary>
  /// At the end of a line of plain text, moves on to the next line when the paragraph continues
  /// there, and answers whether it did.
  /// </summary>
  private bool ContinuePlainTextOnNextLine(bool rootLevel)
  {
    // The line ends here. See if the paragraph continues in the next line.
    if (MoveToNextParagraphContentLine(rootLevel))
    {
      // Paragraph continues in next line. Add a blank to separate words.
      if (!token.EndsWith(' '))
        token += ' ';
      return true;
    }

    // Paragraph ends here. Remember that for next call except the reason
    // for end is '}'
    emptyLine = currChar != Chars.BraceRight;
    return false;
  }

  /// <summary>
  /// Reads the escape a backslash in text starts, if it is one: an escaped '\', '{', '}' or
  /// '/' is appended to the token, and \- becomes the soft hyphen that is then the current
  /// character. Returns false, having read nothing, when the backslash starts a keyword instead.
  /// </summary>
  private bool ScanTextEscape()
  {
    switch (nextChar)
    {
      case '\\':
      case '{':
      case '}':
      case '/':
        ScanNextChar();
        AppendAndScanNextChar();
        return true;

      case '-':
        // Treat \- as soft hyphen.
        ScanNextChar();
        // Fake soft hyphen and go on as usual.
        currChar = Chars.SoftHyphen;
        return true;

      default:
        return false;
    }
  }

  /// <summary>
  /// Appends the current character of text to the token, compressing multiple blanks to one:
  /// a blank following a blank is skipped. Returns whether the character was a blank.
  /// </summary>
  private bool AppendTextChar(bool afterSpace)
  {
    var isSpace = currChar == ' ';
    if (isSpace && afterSpace)
      ScanNextChar();
    else
      AppendAndScanNextChar();
    return isSpace;
  }

  /// <summary>
  /// Moves to the next DDL token if Symbol is not set to a valid position.
  /// </summary>
  internal Symbol MoveToCode()
  {
    if (symbol is Symbol.None or Symbol.CR /*|| this.symbol == Symbol.comment*/)
      ReadCode();
    return symbol;
  }

  /// <summary>
  /// Moves to the first character the content of a paragraph starts with. Empty lines
  /// and comments are skipped. Returns true if such a character exists, and false if the
  /// paragraph ends without content.
  /// </summary>
  internal bool MoveToParagraphContent()
  {
    Again:
    MoveToNonWhiteSpace();
    if (currChar == Chars.Slash && nextChar == Chars.Slash)
    {
      MoveBeyondEol();
      goto Again;
    }
    return currChar != Chars.BraceRight;
  }

  /// <summary>
  /// Moves to the first character of the content of a paragraph beyond an EOL.
  /// Returns true if such a character exists and belongs to the current paragraph.
  /// Returns false if a new line (at root level) or '}' occurs. If a new line caused
  /// the end of the paragraph, the DDL cursor is moved to the next valid content
  /// character or '}' respectively.
  /// </summary>
  internal bool MoveToNextParagraphContentLine(bool rootLevel)
  {
    Debug.Assert(currChar == Chars.LF);
    ScanNextChar();
    while (true)
    {
      // Scan to next EOL and ignore any white space.
      MoveToNonWhiteSpaceOrEol();
      switch (currChar)
      {
        case Chars.Null:
          return false;

        case Chars.LF:
          ScanNextChar(); // read beyond EOL
          if (EndsParagraphAtEmptyLine(rootLevel))
            return false;

          // An empty line inside nested content is skipped, and scanning goes on with the
          // line after it.
          break;

        case Chars.Slash:
          // Current character is a slash.
          if (nextChar != Chars.Slash)
            return true;

          // A line with comment is not treated as empty.
          // Skip this line.
          MoveBeyondEol();
          break;

        case Chars.BraceRight:
          return false;

        default:
          return true;
      }
    }
  }

  /// <summary>
  /// Whether the empty line just read beyond ends the paragraph, having moved to what follows
  /// the paragraph when it does.
  /// </summary>
  private bool EndsParagraphAtEmptyLine(bool rootLevel)
  {
    if (rootLevel)
    {
      // At nesting level 0 (root level) a new line ends the paragraph content.
      // Move to next content block or '}' respectively.
      MoveToParagraphContent();
      return true;
    }

    // Skip new lines at the end of the paragraph.
    if (PeekSymbol() == Symbol.BraceRight)
    {
      MoveToNonWhiteSpace();
      return true;
    }

    return false;
  }

  /// <summary>
  /// If the current character is not a white space, the function immediately returns it.
  /// Otherwise the DDL cursor is moved forward to the first non-white space or EOF.
  /// White spaces are SPACE, HT, VT, CR, and LF.???
  /// </summary>
  internal char MoveToNonWhiteSpaceOrEol()
  {
    while (currChar != Chars.Null)
    {
      switch (currChar)
      {
        case Chars.Space:
        case Chars.HT:
        case Chars.VT:
          ScanNextChar();
          break;

        default:
          return currChar;
      }
    }
    return currChar;
  }

  /// <summary>
  /// If the current character is not a white space, the function immediately returns it.
  /// Otherwise the DDL cursor is moved forward to the first non-white space or EOF.
  /// White spaces are SPACE, HT, VT, CR, and LF.
  /// </summary>
  internal char MoveToNonWhiteSpace()
  {
    while (currChar != Chars.Null)
    {
      switch (currChar)
      {
        case Chars.Space:
        case Chars.HT:
        case Chars.VT:
        case Chars.CR:
        case Chars.LF:
          ScanNextChar();
          break;

        default:
          return currChar;
      }
    }
    return currChar;
  }

  /// <summary>
  /// Moves to the first character beyond the next EOL.
  /// </summary>
  internal void MoveBeyondEol()
  {
    // Similar to ScanSingleLineComment but do not scan the token.
    ScanNextChar();
    while (currChar != Chars.Null && currChar != Chars.LF)
      ScanNextChar();
    ScanNextChar(); // read beyond EOL
  }

  /// <summary>
  /// Reads a single line comment.
  /// </summary>
  internal Symbol ScanSingleLineComment()
  {
    var ch = ScanNextChar();
    while (ch != Chars.Null && ch != Chars.LF)
    {
      token += currChar;
      ch = ScanNextChar();
    }
    ScanNextChar(); // read beyond EOL
    return Symbol.Comment;
  }


  /// <summary>
  /// Gets the current symbol.
  /// </summary>
  internal Symbol Symbol => symbol;

  /// <summary>
  /// Gets the current token type.
  /// </summary>
  internal TokenType TokenType => tokenType;

  /// <summary>
  /// Gets the current token.
  /// </summary>
  internal string Token => token;

  /// <summary>
  /// Interpret current token as integer literal.
  /// </summary>
  /// <remarks>
  /// A literal that is not a number, or is one too large for an integer, is a fault in the text
  /// and is thrown as a DdlParserException, which the parser reports and recovers from; a
  /// FormatException or OverflowException would escape the reader altogether.
  /// </remarks>
  internal int GetTokenValueAsInt()
  {
    int value;
    if (symbol == Symbol.IntegerLiteral)
    {
      if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        return value;

      // ScanNumber lets nothing but a sign and digits into a decimal literal, so one that does
      // not parse is too large rather than malformed.
      throw IntegerOutOfRange();
    }
    if (symbol == Symbol.HexIntegerLiteral)
    {
      var number = token[2..];
      if (int.TryParse(number, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
        return value;

      // ReadHexNumber takes in any identifier character, so a hex literal can be malformed as
      // well as too large.
      // ReSharper disable once PossibleNullReferenceException
      if (number.Length > 0 && number.All(IsHexDigit))
        throw IntegerOutOfRange();
    }
    throw ParserException(DomMsgID.IntegerExpected, token);
  }

  private DdlParserException IntegerOutOfRange() =>
    ParserException(DomMsgID.OutOfRange,
      string.Format(CultureInfo.InvariantCulture, "{0} - {1}", int.MinValue, int.MaxValue));

  private DdlParserException UnsignedIntegerOutOfRange() =>
    ParserException(DomMsgID.OutOfRange,
      string.Format(CultureInfo.InvariantCulture, "{0} - {1}", uint.MinValue, uint.MaxValue));

  /// <summary>
  /// A DdlParserException carrying the given message and the position of the current token.
  /// </summary>
  private DdlParserException ParserException(DomMsgID errorCode, params object[] args) =>
    new(new DdlReaderError(DdlErrorLevel.Error, DomSR.FormatMessage(errorCode, args),
      (int)errorCode, DocumentFileName, CurrentLine, CurrentLinePos));

  /// <summary>
  /// Interpret current token as unsigned integer literal.
  /// </summary>
  /// <remarks>
  /// Refuses what GetTokenValueAsInt refuses, and a negative literal as well, in the same way:
  /// as a DdlParserException the parser reports, never as a FormatException or OverflowException.
  /// </remarks>
  internal uint GetTokenValueAsUInt()
  {
    uint value;
    if (symbol == Symbol.IntegerLiteral)
    {
      if (uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        return value;

      // A sign and digits only, as for GetTokenValueAsInt, so one that does not parse is too
      // large or negative rather than malformed.
      throw UnsignedIntegerOutOfRange();
    }
    if (symbol == Symbol.HexIntegerLiteral)
    {
      var number = token[2..];
      if (uint.TryParse(number, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
        return value;

      // ReSharper disable once PossibleNullReferenceException
      if (number.Length > 0 && number.All(IsHexDigit))
        throw UnsignedIntegerOutOfRange();
    }
    throw ParserException(DomMsgID.IntegerExpected, token);
  }

  /// <summary>
  /// Interpret current token as real literal.
  /// </summary>
  /// <returns></returns>
  internal double GetTokenValueAsReal()
  {
    return double.Parse(token, CultureInfo.InvariantCulture);
  }

  /// <summary>
  /// Gets the current character or EOF.
  /// </summary>
  internal char Char => currChar;

  /// <summary>
  /// Gets the character after the current character or EOF.
  /// </summary>
  internal char NextChar => nextChar;

  /// <summary>
  /// Move DDL cursor one character further.
  /// </summary>
  internal char ScanNextChar()
  {
    if (ddlLength <= m_idx)
    {
      currChar = Chars.Null;
      nextChar = Chars.Null;
      return currChar;
    }

    // A CR before an LF is skipped, and the LF ends the line. A CR on its own ends the line
    // too, as it does in a classic Mac OS file, and is read as an LF so that everything
    // looking for the end of a line finds it.
    ReadDocumentChar();
    if (currChar == Chars.CR && nextChar == Chars.LF)
      ReadDocumentChar();

    switch (currChar)
    {
      case Chars.Null:  //???
      case Chars.LF:
        EndLine();
        break;

      case Chars.CR:
        currChar = Chars.LF;
        EndLine();
        break;
    }
    return currChar;
  }

  /// <summary>
  /// Makes the document's next character current, and the one after it the next.
  /// </summary>
  private void ReadDocumentChar()
  {
    currChar = m_strDocument[m_idx++];
    nextChar = ddlLength <= m_idx ? Chars.Null : m_strDocument[m_idx];
    ++m_idxLinePos;
  }

  /// <summary>
  /// Counts a line ended at the current character.
  /// </summary>
  private void EndLine()
  {
    m_idxLine++;
    m_idxLinePos = 0;
  }

  /// <summary>
  /// Move DDL cursor to the next EOL (or EOF).
  /// </summary>
  internal void ScanToEol()
  {
    while (!IsEof(currChar) && currChar != Chars.LF)
      ScanNextChar();
  }

  /// <summary>
  /// Appends current character to the token and reads next character.
  /// </summary>
  internal char AppendAndScanNextChar()
  {
    token += currChar;
    return ScanNextChar();
  }

  /// <summary>
  /// Appends all next characters to current token until end of line or end of file is reached.
  /// CR/LF or EOF is not part of the token.
  /// </summary>
  internal void AppendAndScanToEol()
  {
    var ch = ScanNextChar();
    while (ch != Chars.Null && ch != Chars.CR && ch != Chars.LF)  //BUG Chars.Null == CharLF
    {
      token += currChar;
      ch = ScanNextChar();
    }
  }

  /// <summary>
  /// Is character in '0' ... '9'.
  /// </summary>
  internal static bool IsDigit(char ch)
  {
    return char.IsDigit(ch);
  }

  /// <summary>
  /// Is character a hexadecimal digit.
  /// </summary>
  internal static bool IsHexDigit(char ch)
  {
    return char.IsDigit(ch) || ch is >= 'A' and <= 'F' or >= 'a' and <= 'f';
  }

  /// <summary>
  /// Is character an octal digit.
  /// </summary>
  internal static bool IsOctDigit(char ch)
  {
    return char.IsDigit(ch) && ch < '8';
  }

  /// <summary>
  /// Is character an alphabetic letter.
  /// </summary>
  internal static bool IsLetter(char ch)
  {
    return char.IsLetter(ch);
  }

  /// <summary>
  /// Is character a white space.
  /// </summary>
  internal static bool IsWhiteSpace(char ch)
  {
    return char.IsWhiteSpace(ch);
  }

  /// <summary>
  /// Is character an identifier character. First character can be letter or underscore, following
  /// letters, digits or underscores.
  /// </summary>
  internal static bool IsIdentifierChar(char ch, bool firstChar) //IsId..Char
  {
    if (firstChar)
      return char.IsLetter(ch) || ch == '_';
    return char.IsLetterOrDigit(ch) || ch == '_';
  }

  /// <summary>
  /// Is character the end of file character.
  /// </summary>
  internal static bool IsEof(char ch)
  {
    return ch == Chars.Null;
  }

  /// <summary>
  /// Gets the current filename of the document.
  /// </summary>
  internal string DocumentFileName { get; private set; }

  /// <summary>
  /// Gets the current path of the document.
  /// </summary>
  internal string DocumentPath { get; private set; }

  /// <summary>
  /// Gets the current scanner line in the document.
  /// </summary>
  internal int CurrentLine { get; private set; }

  /// <summary>
  /// Gets the current scanner column in the document.
  /// </summary>
  internal int CurrentLinePos { get; private set; }

  /// <summary>
  /// Scans an identifier.
  /// </summary>
  protected Symbol ScanIdentifier()
  {
    var ch = AppendAndScanNextChar();
    while (IsIdentifierChar(ch, false))
      ch = AppendAndScanNextChar();

    return Symbol.Identifier;
  }

  /// <summary>
  /// Scans an integer or real literal.
  /// </summary>
  protected Symbol ScanNumber(bool mantissa)
  {
    var ch = currChar;
    token += currChar;

    ScanNextChar();
    if (!mantissa && IsHexPrefix(ch))
      return ReadHexNumber();

    // The end of the document is no digit, so this stops there too.
    while (IsDigit(currChar))
      AppendAndScanNextChar();

    if (!mantissa && currChar == Chars.Period)
      return ScanNumber(true);

    return mantissa ? Symbol.RealLiteral : Symbol.IntegerLiteral;
  }

  /// <summary>
  /// Whether the character just scanned and the current one are «0x» or «0X».
  /// </summary>
  private bool IsHexPrefix(char first) => first == '0' && (currChar is 'x' or 'X');

  /// <summary>
  /// Scans an hexadecimal literal.
  /// </summary>
  protected Symbol ReadHexNumber()
  {
    token = "0x";
    ScanNextChar();
    while (currChar != Chars.Null)
    {
      if (IsHexDigit(currChar))
        AppendAndScanNextChar();
      else if (!IsIdentifierChar(currChar, false)) //???
        break;
      else
        AppendAndScanNextChar();
    }
    return Symbol.HexIntegerLiteral;
  }

  /// <summary>
  /// Scans a DDL keyword that starts with a backslash.
  /// </summary>
  private Symbol ScanKeyword()
  {
    var ch = ScanNextChar();

    // \- is a soft hyphen == char(173).
    if (ch == '-')
    {
      token += "-";
      ScanNextChar();
      return Symbol.SoftHyphen;
    }

    // \( is a short cut for symbol.
    if (ch == '(')
    {
      token += "(";
      symbol = Symbol.Chr;
      return Symbol.Chr; // Short cut for \chr(
    }

    while (!IsEof(ch) && IsIdentifierChar(ch, false))
      ch = AppendAndScanNextChar();

    symbol = KeyWords.SymbolFromName(token);
    return symbol;
  }

  /// <summary>
  /// Scans punctuator terminal symbols.
  /// </summary>
  protected Symbol ScanPunctuator()
  {
    // The end of the document is not appended to the token.
    if (currChar == Chars.Null)
      return Symbol.Eof;

    var sym = currChar switch
    {
      '+' => ScanCompoundAssign(Symbol.Plus, Symbol.PlusAssign),
      '-' => ScanCompoundAssign(Symbol.Minus, Symbol.MinusAssign),
      _ => SingleCharPunctuator(currChar)
    };
    token += currChar;
    ScanNextChar();
    return sym;
  }

  /// <summary>
  /// For a '+' or '-': when '=' follows, appends the operator to the token and moves on to the
  /// '=', answering the compound assignment; otherwise answers the operator alone.
  /// </summary>
  private Symbol ScanCompoundAssign(Symbol alone, Symbol withAssign)
  {
    if (nextChar != '=')
      return alone;

    token += currChar;
    ScanNextChar();
    return withAssign;
  }

  /// <summary>
  /// Scans verbatim strings like «@"String with ""quoted"" text"».
  /// </summary>
  protected string ScanVerbatimStringLiteral()
  {
    var str = "";
    var ch = ScanNextChar();
    while (!IsEof(ch))
    {
      if (ch == Chars.QuoteDbl)
      {
        if (nextChar == Chars.QuoteDbl)
          ch = ScanNextChar();
        else
          break;
      }

      str += ch;
      ch = ScanNextChar();
    }

    ScanNextChar();
    return str;
  }

  /// <summary>
  /// Scans regular string literals like «"String with \"escaped\" text"».
  /// </summary>
  protected string ScanStringLiteral()
  {
    Debug.Assert(Char == '\"');
    var str = "";
    ScanNextChar();
    while (currChar != Chars.QuoteDbl && !IsEof(currChar))
    {
      if (currChar == '\\')
      {
        ScanNextChar(); // read escaped characters
        if (currChar == 'x')
        {
          // Reading the hex digits leaves the scanner on the character after the last one, which
          // is the next character of the string and must not be stepped over by the ScanNextChar
          // at the bottom of the loop - so this continues rather than falls through. Stepping
          // over it lost that character, and when it was the closing quote the string ran on
          // into whatever followed it.
          str += ScanHexEscape();
          continue;
        }
        str += SimpleEscape(currChar);
      }
      else if (currChar is Chars.Null or Chars.CR or Chars.LF)
      {
        throw new DdlParserException(DdlErrorLevel.Error,
          DomSR.GetString(DomMsgID.NewlineInString), DomMsgID.NewlineInString);
      }
      else
      {
        str += currChar;
      }

      ScanNextChar();
    }
    ScanNextChar();  // read '"'
    return str;
  }

  /// <summary>
  /// The character a single-letter escape in a string literal names, the letter being the one
  /// after the backslash. Throws for a letter that names none.
  /// </summary>
  private static char SimpleEscape(char ch) =>
    ch switch
    {
      'a' => '\a',
      'b' => '\b',
      'f' => '\f',
      'n' => '\n',
      'r' => '\r',
      't' => '\t',
      'v' => '\v',
      '\'' => '\'',
      '\"' => '\"',
      '\\' => '\\',
      _ => throw EscapeSequenceNotAllowed()
    };

  /// <summary>
  /// Reads the \x escape in a string literal, the scanner being on the 'x': one or two hex digits
  /// name the character. Leaves the scanner on the character after the last digit.
  /// </summary>
  private char ScanHexEscape()
  {
    ScanNextChar();
    var hexDigits = "";
    while (IsHexDigit(currChar))
    {
      hexDigits += currChar;
      ScanNextChar();
    }
    if (hexDigits.Length is 0 or > 2)
      throw EscapeSequenceNotAllowed();
    return (char)Convert.ToInt32(hexDigits, 16);
  }

  private static DdlParserException EscapeSequenceNotAllowed() =>
    new(DdlErrorLevel.Error,
      DomSR.GetString(DomMsgID.EscapeSequenceNotAllowed), DomMsgID.EscapeSequenceNotAllowed);

  /// <summary>
  /// Save the current scanner location in the document for error handling.
  /// </summary>
  private void SaveCurDocumentPos()
  {
    CurrentLine = m_idxLine;
    CurrentLinePos = m_idxLinePos;
  }

  private string m_strDocument;
  private int ddlLength;
  private int m_idx;
  private int m_idxLine;
  private int m_idxLinePos;

  private char currChar;
  private char nextChar;
  private string token = "";
  private Symbol symbol = Symbol.None;
  private Symbol prevSymbol = Symbol.None;
  private TokenType tokenType = TokenType.None;
  private bool emptyLine;
}
