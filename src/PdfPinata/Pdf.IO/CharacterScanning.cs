using System;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// The character-level reading <see cref="Lexer"/> and <see cref="Content.CLexer"/> share: the
/// current-and-next character pair, the carriage-return-then-line-feed fold, the white-space
/// skip built on it, and the character-class predicates a token grammar is built from. What
/// differs between the two lexers is the token grammar above this - the source each reads from,
/// how each tracks its own position in it, and what a span of characters means - and that stays
/// with each lexer rather than moving here.
/// </summary>
internal static class CharacterScanning
{
    /// <summary>
    /// Shifts <paramref name="nextChar"/> into <paramref name="currChar"/> and reads a fresh
    /// <paramref name="nextChar"/> from <paramref name="readNextByte"/>, folding a carriage
    /// return into a line feed when <paramref name="handleCRLF"/> is set - a CR LF pair becomes
    /// one LF, and a lone CR becomes LF as well. A grammar decoding raw bytes character by
    /// character - inside a literal string's escape handling, for instance - passes false so it
    /// can tell a carriage return from a line feed itself. Returns the new current character.
    /// </summary>
    public static char Advance(ref char currChar, ref char nextChar, bool handleCRLF, Func<char> readNextByte)
    {
        currChar = nextChar;
        nextChar = readNextByte();
        if (handleCRLF && currChar == Chars.CR)
        {
            if (nextChar == Chars.LF)
            {
                currChar = nextChar;
                nextChar = readNextByte();
            }
            else
            {
                currChar = Chars.LF;
            }
        }
        return currChar;
    }

    /// <summary>
    /// If <paramref name="currChar"/> is not white space, returns it unchanged. Otherwise calls
    /// <paramref name="scanNextChar"/> until the first non-white-space character or the end of
    /// the source. White space here is wider than <see cref="IsWhiteSpace"/>: NUL, HT, LF, FF,
    /// CR, SP, a vertical tab, and a soft hyphen.
    /// </summary>
    public static char SkipWhiteSpace(char currChar, Func<char> scanNextChar)
    {
        while (currChar != Chars.EOF)
        {
            switch (currChar)
            {
                case Chars.NUL:
                case Chars.HT:
                case Chars.LF:
                case Chars.FF:
                case Chars.CR:
                case Chars.SP:
                case Chars.VT:
                case Chars.SoftHyphen:
                    currChar = scanNextChar();
                    break;

                default:
                    return currChar;
            }
        }
        return currChar;
    }

    /// <summary>Indicates whether the specified character is a PDF white-space character.</summary>
    public static bool IsWhiteSpace(char ch)
    {
        switch (ch)
        {
            case Chars.NUL:  // 0 Null
            case Chars.HT:   // 9 Horizontal Tab
            case Chars.LF:   // 10 Line Feed
            case Chars.FF:   // 12 Form Feed
            case Chars.CR:   // 13 Carriage Return
            case Chars.SP:   // 32 Space
                return true;
        }
        return false;
    }

    /// <summary>Indicates whether the specified character is a PDF delimiter character.</summary>
    public static bool IsDelimiter(char ch)
    {
        switch (ch)
        {
            case '(':
            case ')':
            case '<':
            case '>':
            case '[':
            case ']':
            case '{':
            case '}':
            case '/':
            case '%':
                return true;
        }
        return false;
    }

    /// <summary>Indicates whether the specified character is a hexadecimal digit.</summary>
    public static bool IsHexChar(char ch) =>
        char.IsDigit(ch) || ch is >= 'A' and <= 'F' or >= 'a' and <= 'f';

    /// <summary>
    /// Indicates whether the specified character is an octal digit. A literal string escapes a
    /// character code in octal, so only '0' to '7' count - '8' and '9' end the code rather than
    /// extending it, and a backslash before either is dropped and the digit kept as text.
    /// </summary>
    public static bool IsOctalDigit(char ch) => ch is >= '0' and <= '7';
}
