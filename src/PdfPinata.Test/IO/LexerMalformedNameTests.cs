using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   In a name, <c>#</c> followed by two hexadecimal digits is the byte they spell (ISO 32000-1
///   7.3.5). The document lexer took the two characters after every <c>#</c> as digits whatever
///   they were, so <c>/A#ZZ</c>, or a <c>#</c> at the end of a name, ended the read with a
///   <see cref="System.FormatException"/>. A <c>#</c> that does not begin such an escape is now
///   kept as the character it is, which is what readers do and what the content lexer does too.
///   The timeouts turn a scan that no longer ends into a failure rather than a wedged test host.
/// </summary>
public class LexerMalformedNameTests
{
    [Theory(Timeout = 5000)]
    [InlineData("/A#41 ", "/AA")]            // an escape, as before
    [InlineData("/A#e9 ", "/Aé")]      // in lower case too
    [InlineData("/A##41 ", "/A#A")]          // a '#' that is not an escape, then one that is
    [InlineData("/A#ZZ ", "/A#ZZ")]          // no hexadecimal digit at all
    [InlineData("/A#4G ", "/A#4G")]          // only the first is one
    [InlineData("/A#4 ", "/A#4")]            // one digit and then the end of the name
    [InlineData("/A#4", "/A#4")]             // one digit and then the end of the file
    [InlineData("/A# ", "/A#")]              // none, and the end of the name
    [InlineData("/A#", "/A#")]               // none, and the end of the file
    public async Task AHashIsAnEscapeOnlyWhenTwoHexadecimalDigitsFollowIt(string pdf, string name)
    {
        var scanned = await ScanFirstToken(pdf);

        scanned.Symbol.Should().Be(Symbol.Name);
        scanned.Token.Should().Be(name);
    }

    [Fact(Timeout = 5000)]
    public async Task AHashWithOneDigitEndsTheNameWhereADelimiterSaysSo()
    {
        var scanned = await Interruptibly.Run(() =>
        {
            var lexer = LexerOver("/A#4/B");
            var first = (lexer.ScanNextToken(), lexer.Token);
            var second = (lexer.ScanNextToken(), lexer.Token);
            return (first, second);
        });

        scanned.first.Should().Be((Symbol.Name, "/A#4"));
        scanned.second.Should().Be((Symbol.Name, "/B"));
    }

    // ----- The end-of-file marker ------------------------------------------------------------------

    [Fact]
    public void TheEndOfFileMarkerIsScannedAsTheEndOfTheFileAndRememberedAsSuch()
    {
        var lexer = LexerOver("%%EOF\n");

        lexer.ScanComment().Should().Be(Symbol.Eof);
        lexer.Symbol.Should().Be(Symbol.Eof, "every other token the lexer scans is remembered too");
    }

    [Fact]
    public void ScanningTokensPassesOverTheEndOfFileMarker()
    {
        // An incrementally updated file has a %%EOF at the end of every revision but the last, and
        // what follows it is the next revision, so the body ends only where the file does.
        var lexer = LexerOver("%%EOF\n42");

        lexer.ScanNextToken().Should().Be(Symbol.Integer);
        lexer.ScanNextToken().Should().Be(Symbol.Eof);
    }

    private record Scanned(Symbol Symbol, string Token);

    private static Task<Scanned> ScanFirstToken(string pdf)
    {
        return Interruptibly.Run(() =>
        {
            var lexer = LexerOver(pdf);
            var symbol = lexer.ScanNextToken();
            return new Scanned(symbol, lexer.Token);
        });
    }

    private static Lexer LexerOver(string pdf) => new(new MemoryStream(ParserProbe.Bytes(pdf)));
}
