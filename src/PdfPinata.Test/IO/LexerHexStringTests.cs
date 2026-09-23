using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A hexadecimal string that is never closed has no '>' to end it, and the document lexer
///   scanned on past the end of the file looking for one: neither '>' nor a hex digit, so it
///   stepped forward, and stepping forward at the end of a file stays where it is. Opening such
///   a document never returned. The content lexer already had the guard.
///   The timeouts turn a regression into a failure rather than into a wedged test host.
///   See https://github.com/empira/PDFsharp/issues/266.
/// </summary>
public class LexerHexStringTests
{
    /// <summary>The single byte 0x90, which is what the digit 9 alone spells.</summary>
    private static readonly string NinetyHex = ((char)0x90).ToString();

    [Theory(Timeout = 5000)]
    [InlineData("<48656C6C6F")]      // no closing '>' at all
    [InlineData("<48656C6C6F ")]     // and one that ends in white space
    public async Task AHexStringThatIsNeverClosedEndsWhereTheFileDoes(string pdf)
    {
        var scanned = await ScanFirstToken(pdf);

        scanned.Symbol.Should().Be(Symbol.HexString);
        scanned.Token.Should().Be("Hello");
    }

    [Fact(Timeout = 5000)]
    public async Task AHexStringThatIsClosedIsStillRead()
    {
        var scanned = await ScanFirstToken("<48656C6C6F>");

        scanned.Symbol.Should().Be(Symbol.HexString);
        scanned.Token.Should().Be("Hello");
    }

    [Theory(Timeout = 5000)]
    [InlineData("<9>")]              // closed, one digit short
    [InlineData("<9")]               // never closed, one digit short
    public async Task AHexStringWithAnOddNumberOfDigitsEndsInAZero(string pdf)
    {
        // The specification says the missing final digit is a zero, so this is 0x90 and not
        // 0x09. Reading it as the low digit put the byte out by a factor of sixteen.
        var scanned = await ScanFirstToken(pdf);

        scanned.Token.Should().Be(NinetyHex);
    }

    [Theory(Timeout = 5000)]
    [InlineData("<9 0>")]            // white space between the two digits of a byte
    [InlineData("<9\n0>")]
    [InlineData("<9*0>")]            // and something that is not a digit at all
    public async Task TheTwoDigitsOfAByteNeedNotBeSideBySide(string pdf)
    {
        var scanned = await ScanFirstToken(pdf);

        scanned.Token.Should().Be(NinetyHex);
    }

    [Fact(Timeout = 5000)]
    public async Task AUnicodeHexStringIsStillRecognisedByItsByteOrderMark()
    {
        var scanned = await ScanFirstToken("<FEFF00480049>");

        scanned.Symbol.Should().Be(Symbol.UnicodeHexString);
        scanned.Token.Should().Be("HI");
    }

    [Theory(Timeout = 5000)]
    [InlineData("<FEFF0>")]          // closed, but a byte order mark and then a single digit
    [InlineData("<FEFF0")]           // and the same never closed
    public async Task AUnicodeHexStringMissingHalfOfItsLastCharacterEndsInAZero(string pdf)
    {
        // The digit alone is the byte 0x00, and the character it begins is short of its low
        // byte, which is a zero as well. Reading on for that byte ran off the end of the string.
        var scanned = await ScanFirstToken(pdf);

        scanned.Symbol.Should().Be(Symbol.UnicodeHexString);
        scanned.Token.Should().Be("\0");
    }

    [Fact(Timeout = 5000)]
    public async Task ScanningStopsAtTheEndOfAnUnclosedHexStringRatherThanRunningOn()
    {
        // Every token of the file, not just the first: a scan that did not stop would never
        // reach the end of the file to report it.
        var symbols = await ScanAll("<48 65 6C");

        symbols.Should().Equal(Symbol.HexString, Symbol.Eof);
    }

    [Fact(Timeout = 20000)]
    public async Task ADocumentWhoseCrossReferenceOffsetIsAnUnclosedHexStringIsRefused()
    {
        // What the reported document amounts to: "startxref" followed by something that is not
        // a number and never ends. Refusing it is right; never returning is not.
        var document = Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj\n<</Type/Catalog>>\nendobj\n" +
                                                "trailer\n<</Size 2/Root 1 0 R>>\nstartxref\n<4865");

        // The reading itself on the other thread, and not merely the making of the delegate
        // that does it: what runs on the test thread cannot be interrupted by the Timeout.
        Func<Task> open = () => Interruptibly.Run(() =>
            Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify));

        await open.Should().ThrowAsync<PdfReaderException>();
    }

    private record Scanned(Symbol Symbol, string Token);

    private static Task<Scanned> ScanFirstToken(string pdf)
    {
        // On a thread of its own, so that the Timeout on these tests can interrupt a scan that
        // does not end. xUnit honours it only on an async test.
        return Interruptibly.Run(() =>
        {
            var lexer = new Lexer(new MemoryStream(Encoding.Latin1.GetBytes(pdf)));
            var symbol = lexer.ScanNextToken();
            return new Scanned(symbol, lexer.Token);
        });
    }

    private static Task<IReadOnlyList<Symbol>> ScanAll(string pdf)
    {
        return Interruptibly.Run<IReadOnlyList<Symbol>>(() =>
        {
            var lexer = new Lexer(new MemoryStream(Encoding.Latin1.GetBytes(pdf)));
            var symbols = new List<Symbol>();
            Symbol symbol;
            do
            {
                symbol = lexer.ScanNextToken();
                symbols.Add(symbol);
            }
            while (symbol != Symbol.Eof && symbols.Count < 100);

            return symbols;
        });
    }
}
