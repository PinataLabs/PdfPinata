using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
/// PDF names are byte strings, and both lexers scan them one byte per char without ever
/// re-decoding them. These tests pin that invariant down for byte sequences that are not
/// valid UTF-8 - Shift-JIS (CP932) here - because a lexer that decoded names as UTF-8
/// would replace those bytes with U+FFFD and lose them.
/// </summary>
public class LexerNameEncodingTests
{
    // Shift-JIS TE (U+30C6).
    static readonly byte[] ShiftJisTe = { 0x83, 0x65 };

    // Shift-JIS NIHONGO (U+65E5 U+672C U+8A9E).
    static readonly byte[] ShiftJisNihongo = { 0x93, 0xFA, 0x96, 0xD1, 0x8C, 0xEA };

    // The same three characters in UTF-8.
    static readonly byte[] Utf8Nihongo = { 0xE6, 0x97, 0xA5, 0xE6, 0x9C, 0xAC, 0xE8, 0xAA, 0x9E };

    const char ReplacementCharacter = (char)0xFFFD;

    [Fact]
    public void ScanName_keepsAShiftJisLeadByteInTheRange0x80To0xBF()
    {
        // 0x83 is a Shift-JIS lead byte, and (0x83 & 0xC0) == 0x80 rather than 0xC0, so a
        // lexer that sniffed for UTF-8 lead bytes that way would not even see it.
        var lexer = CreateLexer(Name(ShiftJisTe));

        lexer.ScanNextToken().Should().Be(Symbol.Name);

        lexer.Token.Should().Be("/" + (char)0x83 + (char)0x65);
        RawBytesOf(lexer.Token).Skip(1).Should().Equal(ShiftJisTe);
    }

    [Fact]
    public void ScanName_doesNotReplaceShiftJisBytesWithTheReplacementCharacter()
    {
        // 0x93 and 0x96 do satisfy (b & 0xC0) == 0xC0, so a UTF-8 re-decode would fire here
        // and mangle all six bytes - they are not a valid UTF-8 sequence.
        var lexer = CreateLexer(Name(ShiftJisNihongo));

        lexer.ScanNextToken().Should().Be(Symbol.Name);

        lexer.Token.Should().NotContain(ReplacementCharacter.ToString());
        RawBytesOf(lexer.Token).Skip(1).Should().Equal(ShiftJisNihongo);
    }

    [Fact]
    public void ScanName_leavesUtf8NamesAsRawBytesToo()
    {
        // The lexer does not decode the names it *could* decode either - a UTF-8 name stays
        // nine chars of one byte each rather than becoming three Unicode characters.
        var lexer = CreateLexer(Name(Utf8Nihongo));

        lexer.ScanNextToken().Should().Be(Symbol.Name);

        lexer.Token.Length.Should().Be(1 + Utf8Nihongo.Length);
        RawBytesOf(lexer.Token).Skip(1).Should().Equal(Utf8Nihongo);
    }

    [Fact]
    public void ScanName_stillDecodesHashEscapes()
    {
        // #xx is the spec's way of writing these bytes, and that path is unaffected.
        var lexer = CreateLexer(Encoding.ASCII.GetBytes("/#93#FA#96#D1#8C#EA "));

        lexer.ScanNextToken().Should().Be(Symbol.Name);

        RawBytesOf(lexer.Token).Skip(1).Should().Equal(ShiftJisNihongo);
    }

    [Fact]
    public void Save_writesAScannedShiftJisNameBackByteForByte()
    {
        var lexer = CreateLexer(Name(ShiftJisNihongo));
        lexer.ScanNextToken().Should().Be(Symbol.Name);

        var document = new PdfDocument();
        document.AddPage().Elements.SetName("/PdfPinataTestName", lexer.Token);

        using var ms = new MemoryStream();
        document.Save(ms, false);

        // Bytes outside '!'..'~' are written as #xx (ISO 32000-1 7.3.5), each escape naming the
        // byte itself rather than anything it was decoded into.
        RawStringOf(ms.ToArray()).Should().Contain("/#93#FA#96#D1#8C#EA");

        ms.Position = 0;
        var reread = PdfPinata.Pdf.IO.PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        RawBytesOf(reread.Pages[0].Elements.GetName("/PdfPinataTestName")).Skip(1).Should().Equal(ShiftJisNihongo);
    }

    [Fact]
    public void CLexer_ScanName_preservesShiftJisBytesInContentStreams()
    {
        var lexer = new CLexer(Name(ShiftJisNihongo));

        lexer.ScanNextToken().Should().Be(CSymbol.Name);

        lexer.Token.Should().NotContain(ReplacementCharacter.ToString());
        RawBytesOf(lexer.Token).Skip(1).Should().Equal(ShiftJisNihongo);
    }

    static Lexer CreateLexer(byte[] bytes)
    {
        return new Lexer(new MemoryStream(bytes));
    }

    /// <summary>
    /// Wraps the given bytes in a name token: a leading slash and a trailing delimiter.
    /// </summary>
    static byte[] Name(byte[] bytes)
    {
        var name = new byte[bytes.Length + 2];
        name[0] = (byte)'/';
        bytes.CopyTo(name, 1);
        name[^1] = (byte)' ';
        return name;
    }

    /// <summary>
    /// Undoes the lexer's one-byte-per-char scanning, the way PdfEncoders.RawEncoding does.
    /// </summary>
    static byte[] RawBytesOf(string token)
    {
        return token.Select(ch => (byte)ch).ToArray();
    }

    /// <summary>
    /// The inverse of <see cref="RawBytesOf"/>, so that a saved document can be searched
    /// for a byte sequence without decoding it.
    /// </summary>
    static string RawStringOf(byte[] bytes)
    {
        return new string(bytes.Select(b => (char)b).ToArray());
    }
}
