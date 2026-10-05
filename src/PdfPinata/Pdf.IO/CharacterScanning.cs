using System;
using System.Text;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// The character-level reading <see cref="Lexer"/> and <see cref="Content.CLexer"/> share: the
/// current-and-next character pair, the carriage-return-then-line-feed fold, the white-space
/// skip built on it, the character-class predicates a token grammar is built from, the value of
/// a hexadecimal digit, the escapes a literal string writes as a backslash and one character,
/// and the UTF-16 byte order marks a string's bytes may open with. What
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
        return ch switch
        {
            Chars.NUL        // 0 Null
                or Chars.HT  // 9 Horizontal Tab
                or Chars.LF  // 10 Line Feed
                or Chars.FF  // 12 Form Feed
                or Chars.CR  // 13 Carriage Return
                or Chars.SP  // 32 Space
                => true,
            _ => false
        };
    }

    /// <summary>Indicates whether the specified character is a PDF delimiter character.</summary>
    public static bool IsDelimiter(char ch)
    {
        return ch switch
        {
            '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%' => true,
            _ => false
        };
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

    /// <summary>The value of a character <see cref="IsHexChar"/> accepts.</summary>
    public static int HexValue(char ch) => ch <= '9' ? ch - '0' : (ch | 0x20) - 'a' + 10;

    /// <summary>
    /// Resolves the escapes a literal string writes as a backslash and one character - exactly
    /// those of ISO 32000-1 7.3.4.2 Table 3. Returns false for any other character, which the
    /// caller then reads as the first digit of an octal code or, failing that, as itself: "If the
    /// character following the REVERSE SOLIDUS is not one of those shown in Table 3, the REVERSE
    /// SOLIDUS shall be ignored."
    /// </summary>
    /// <remarks>
    /// That rule is what reads <c>(\ )</c> - which AutoCAD writes - as a single space, without the
    /// space being listed here. The document lexer used to list it and the content lexer did not,
    /// which looked like the two reading the same string differently when both read a space; with
    /// one table there is nothing left to drift.
    /// </remarks>
    public static bool TryResolveSimpleEscape(char ch, out char resolved)
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
    /// Indicates whether bytes held one per character open with FE FF, the UTF-16BE byte order
    /// mark ISO 32000-1 7.9.2.2 opens a Unicode text string with.
    /// </summary>
    public static bool StartsWithUtf16BigEndianMark(StringBuilder bytes) =>
        bytes.Length >= 2 && bytes[0] == '\xFE' && bytes[1] == '\xFF';

    /// <inheritdoc cref="StartsWithUtf16BigEndianMark(StringBuilder)"/>
    public static bool StartsWithUtf16BigEndianMark(ReadOnlySpan<char> bytes) =>
        bytes.Length >= 2 && bytes[0] == '\xFE' && bytes[1] == '\xFF';

    /// <summary>
    /// Indicates whether the bytes open with FE FF, the UTF-16BE byte order mark ISO 32000-1
    /// 7.9.2.2 opens a Unicode text string with.
    /// </summary>
    public static bool StartsWithUtf16BigEndianMark(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF;

    /// <summary>
    /// Indicates whether bytes held one per character open with FF FE, the UTF-16LE byte order
    /// mark. The reference names only the big-endian one, but Adobe Reader accepts this one too,
    /// and so does a literal string in either lexer.
    /// </summary>
    public static bool StartsWithUtf16LittleEndianMark(StringBuilder bytes) =>
        bytes.Length >= 2 && bytes[0] == '\xFF' && bytes[1] == '\xFE';
}
