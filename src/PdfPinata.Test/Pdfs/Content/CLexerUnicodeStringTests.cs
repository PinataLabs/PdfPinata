using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Pdfs.Content;

/// <summary>
///   A literal string in a content stream is scanned by one of two loops, chosen by the byte order
///   mark: one reading a byte at a time and one reading two. <see cref="CLexerTests"/> covers the
///   8-bit loop's escapes, brackets and line continuations; this covers the 16-bit loop's, which
///   are a second copy of the same switch and can drift from the first without anything noticing.
///   <para>
///   The escapes are the part where the two loops are genuinely different, not merely duplicated:
///   the backslash and what follows it are read as <em>single bytes</em> in the middle of a stream
///   of pairs, so a named escape inside a UTF-16 string is three bytes rather than two characters.
///   </para>
/// </summary>
public class CLexerUnicodeStringTests
{
    /// <summary>A big-endian UTF-16 literal string, with the bytes given written inside it.</summary>
    static byte[] BigEndianString(params byte[] inside)
    {
        var bytes = new List<byte> { (byte)'(', 0xFE, 0xFF };
        bytes.AddRange(inside);
        bytes.Add((byte)')');
        return bytes.ToArray();
    }

    /// <summary>One character of a big-endian UTF-16 string.</summary>
    static byte[] Wide(char ch) => [(byte)(ch >> 8), (byte)(ch & 0xFF)];

    static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    static async Task<string> TheStringIn(byte[] content)
    {
        var tokens = await Interruptibly.Run(() =>
        {
            var lexer = new CLexer(content);
            var scanned = new List<(CSymbol, string)>();
            CSymbol symbol;
            while ((symbol = lexer.ScanNextToken()) != CSymbol.Eof)
                scanned.Add((symbol, lexer.Token));
            return scanned;
        });

        return tokens.Where(token => token.Item1 == CSymbol.UnicodeString)
            .Select(token => token.Item2)
            .Single();
    }

    // ----- the named escapes ------------------------------------------------------------------------

    [Theory(Timeout = 5000)]
    [InlineData('n', '\n')]
    [InlineData('r', '\r')]
    [InlineData('t', '\t')]
    [InlineData('b', '\b')]
    [InlineData('f', '\f')]
    [InlineData('(', '(')]
    [InlineData(')', ')')]
    [InlineData('\\', '\\')]
    public async Task EveryNamedEscapeIsReadInsideAWideStringToo(char escape, char expected)
    {
        var content = BigEndianString(Concat(Wide('a'), Wide('\\'), [(byte)escape], Wide('b')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("a" + expected + "b");
    }

    [Theory(Timeout = 5000)]
    [InlineData("101", "A")]
    [InlineData("10", "\b")]
    [InlineData("7", "\a")]
    public async Task AnOctalCodeOfOneTwoOrThreeDigitsIsReadInsideAWideString(string digits, string expected)
    {
        var content = BigEndianString(Concat(
            Wide('a'), Wide('\\'), Encoding.ASCII.GetBytes(digits), Wide('b')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("a" + expected + "b");
    }

    [Fact(Timeout = 5000)]
    public async Task AnEscapeTheWideLoopDoesNotKnowKeepsTheCharacterAfterIt()
    {
        var content = BigEndianString(Concat(Wide('a'), Wide('\\'), [(byte)'q'], Wide('b')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("aqb");
    }

    // ----- the line continuation --------------------------------------------------------------------

    /// <summary>
    ///   A line continuation inside a wide string loses the loop its alignment, and this pins that
    ///   rather than claiming otherwise. The 8-bit loop reads one character per byte, so resuming
    ///   after the line ending with a single read is right there; the wide loop reads two bytes per
    ///   character, and resuming with one leaves every pair after it straddling two characters.
    ///   Here the wide 'b' and the closing bracket are paired into U+6229, and the string runs on
    ///   past the terminator that should have ended it.
    ///   <para>
    ///   The source says as much beside the switch — "TODO: not sure that this is correct". Nothing
    ///   in this library writes a continuation into a UTF-16 string, so no document built here meets
    ///   it, but a file from elsewhere could.
    ///   </para>
    /// </summary>
    [Theory(Timeout = 5000)]
    [InlineData((byte)'\n')]
    [InlineData((byte)'\r')]
    public async Task AContinuationInsideAWideStringLosesTheLoopItsAlignment(byte lineEnding)
    {
        var content = BigEndianString(Concat(Wide('a'), Wide('\\'), [lineEnding], Wide('b')));

        var scanned = await TheStringIn(content);

        scanned.Should().HaveLength(3);
        scanned[0].Should().Be('a');
        ((int)scanned[1]).Should().Be(0,
            "the high byte of the wide 'b' was taken for a whole character");
        ((int)scanned[2]).Should().Be(0x6229,
            "and its low byte was paired with the closing bracket, terminator and all");
    }

    // ----- brackets -----------------------------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task ABracketedRunInsideAWideStringIsPartOfIt()
    {
        var content = BigEndianString(Concat(
            Wide('a'), Wide('('), Wide('b'), Wide(')'), Wide('c')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("a(b)c");
    }

    [Fact(Timeout = 5000)]
    public async Task NestedBracketsInsideAWideStringAreCountedOffAgainstEachOther()
    {
        var content = BigEndianString(Concat(
            Wide('a'), Wide('('), Wide('('), Wide('b'), Wide(')'), Wide(')'), Wide('c')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("a((b))c");
    }

    // ----- the little-endian loop ---------------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task AnEscapeIsReadTheSameWayRoundInALittleEndianString()
    {
        // The switch sees the wide character the pair spells rather than the bytes it was written
        // as, so the backslash has to be spelled the little-endian way round like everything else.
        var content = new List<byte> { (byte)'(', 0xFF, 0xFE };
        content.AddRange([(byte)'a', 0x00]);
        content.AddRange([0x5C, 0x00]);
        content.Add((byte)'n');
        content.AddRange([(byte)'b', 0x00]);
        content.Add((byte)')');

        var scanned = await TheStringIn(content.ToArray());

        scanned.Should().Be("a\nb");
    }

    // ----- running out of content ----------------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task AWideStringThatEndsAfterABackslashEndsThereRatherThanScanningOn()
    {
        var content = Concat([(byte)'(', 0xFE, 0xFF], Wide('a'), Wide('\\'));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("a");
    }
}
