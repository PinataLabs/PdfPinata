using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A literal string reads the same in a content stream as it does in the document body. Each
///   case here is scanned by both lexers, <see cref="Lexer"/> and <see cref="CLexer"/>, and the two
///   have to agree with each other as well as with the text expected.
/// </summary>
/// <remarks>
///   Each lexer used to carry its own table of escapes, and the two had drifted: the document
///   lexer listed <c>\ </c> (a backslash and a space, which AutoCAD writes) and the content lexer
///   did not. Both still read a space, because ISO 32000-1 7.3.4.2 has a backslash before any
///   character Table 3 does not list ignored, but the drift that did change what was read was in
///   the octal codes: the document lexer took any digit for an octal one, and threw on
///   <c>(\18)</c>, and it appended U+FFFF for a backslash the file ended on. The table now lives
///   in <c>CharacterScanning</c>, and these cases are what says whether the two still agree.
/// </remarks>
public class LiteralStringEscapeParityTests
{
    [Theory(Timeout = 5000)]
    // Table 3, each written as a backslash and a character.
    [InlineData(@"(a\nb)", "a\nb")]
    [InlineData(@"(a\rb)", "a\rb")]
    [InlineData(@"(a\tb)", "a\tb")]
    [InlineData(@"(a\bb)", "a\bb")]
    [InlineData(@"(a\fb)", "a\fb")]
    [InlineData(@"(a\(b)", "a(b")]
    [InlineData(@"(a\)b)", "a)b")]
    [InlineData(@"(a\\b)", @"a\b")]
    // Not in Table 3, so the backslash is ignored and the character stands for itself. A space is
    // read this way rather than by being listed.
    [InlineData(@"(a\ b)", "a b")]
    [InlineData(@"(\ )", " ")]
    [InlineData(@"(a\qb)", "aqb")]
    // Octal codes of one to three digits, and the scan stops at the third.
    [InlineData(@"(\101)", "A")]
    [InlineData(@"(\1)", "\u0001")]
    [InlineData(@"(\12)", "\n")]
    [InlineData(@"(\377)", "ÿ")]
    [InlineData(@"(\1012)", "A2")]
    // An '8' or a '9' is not an octal digit: it ends a code already begun, and otherwise loses
    // only its backslash.
    [InlineData(@"(\8)", "8")]
    [InlineData(@"(\9)", "9")]
    [InlineData(@"(\18)", "\u0001" + "8")]
    [InlineData(@"(\118)", "\t" + "8")]
    public async Task BothLexersReadAnEscapeAsTheSameCharacter(string literal, string expected)
    {
        var content = Encoding.ASCII.GetBytes(literal);

        var document = await ScanWithDocumentLexer(content);
        var contentStream = await ScanWithContentLexer(content);

        document.Should().Be(expected);
        contentStream.Should().Be(expected);
    }

    /// <summary>
    ///   A string the source ends inside, right after a backslash, keeps the characters it had and
    ///   gains nothing for the backslash - in particular not the end-of-file marker, U+FFFF, which
    ///   the document lexer used to append as if it had been escaped.
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ABackslashTheSourceEndsOnAddsNothingToTheString()
    {
        var content = Encoding.ASCII.GetBytes(@"(ab\");

        var document = await ScanWithDocumentLexer(content);
        var contentStream = await ScanWithContentLexer(content);

        document.Should().NotContain("￿");
        contentStream.Should().NotContain("￿");
        document.Should().Be(contentStream);
    }

    // Each on a thread of its own, so that the Timeout on these tests can interrupt a scan that
    // does not end. xUnit honours it only on an async test.

    private static Task<string> ScanWithDocumentLexer(byte[] content) =>
        Interruptibly.Run(() =>
        {
            var lexer = new Lexer(new MemoryStream(content));
            lexer.ScanNextToken().Should().Be(Symbol.String);
            return lexer.Token;
        });

    private static Task<string> ScanWithContentLexer(byte[] content) =>
        Interruptibly.Run(() =>
        {
            var lexer = new CLexer(content);
            lexer.ScanNextToken().Should().Be(CSymbol.String);
            return lexer.Token;
        });
}
