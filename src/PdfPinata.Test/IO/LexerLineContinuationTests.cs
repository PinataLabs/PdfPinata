using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A backslash at the end of a line in a literal string continues the string onto the next
///   line, and neither the backslash nor the line ending is part of it (ISO 32000-1 7.3.4.2).
/// </summary>
/// <remarks>
///   A line ends in CR, LF or CR LF, and a CR LF is one line ending. The document lexer read it as
///   a CR ending the line and kept the LF as the first character of the next, so a string written
///   on two lines by a producer ending its lines in CR LF came back with a line feed in the middle.
///   The content-stream lexer did the same, and <c>CLexerTests</c> pins it there.
/// </remarks>
public class LexerLineContinuationTests
{
    [Theory(Timeout = 5000)]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task ABackslashBeforeALineEndingJoinsTheLines(string lineEnding)
    {
        var token = await ScanLiteralString("a\\" + lineEnding + "b");

        token.Should().Be("ab");
    }

    [Theory(Timeout = 5000)]
    [InlineData("\r\n\n", "a\nb")]
    [InlineData("\n\n", "a\nb")]
    [InlineData("\r\r", "a\rb")]
    public async Task OnlyOneLineEndingIsSwallowed(string lineEndings, string expected)
    {
        var token = await ScanLiteralString("a\\" + lineEndings + "b");

        token.Should().Be(expected);
    }

    [Fact(Timeout = 5000)]
    public async Task ACrLfContinuationAtTheEndOfTheStringLeavesNothingBehind()
    {
        var token = await ScanLiteralString("ab\\\r\n");

        token.Should().Be("ab");
    }

    /// <summary>Scans the contents wrapped in the parentheses that make them a literal string.</summary>
    private static Task<string> ScanLiteralString(string contents)
    {
        var pdf = Encoding.ASCII.GetBytes("(" + contents + ")");

        // On a thread of its own, so that the Timeout on these tests can interrupt a scan that
        // does not end. xUnit honours it only on an async test.
        return Interruptibly.Run(() =>
        {
            var lexer = new Lexer(new MemoryStream(pdf));
            lexer.ScanNextToken().Should().Be(Symbol.String);
            return lexer.Token;
        });
    }
}
