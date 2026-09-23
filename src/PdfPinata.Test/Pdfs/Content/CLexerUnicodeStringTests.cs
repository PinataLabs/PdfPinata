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
///   A literal string in a content stream that opens with a UTF-16 byte order mark has its escapes
///   resolved on the bytes, like any other, and is decoded from the bytes that leaves.
///   <see cref="CLexerTests"/> covers the escapes, brackets and line continuations of an 8-bit
///   string; this covers the same inside a wide one.
///   <para>
///   The escapes are where a wide string needs looking at on its own: the backslash and what
///   follows it are <em>single bytes</em> in the middle of a stream of pairs, so a named escape
///   inside a UTF-16 string is three bytes rather than two characters.
///   </para>
/// </summary>
public class CLexerUnicodeStringTests
{
    /// <summary>A big-endian UTF-16 literal string, with the bytes given written inside it.</summary>
    private static byte[] BigEndianString(params byte[] inside)
    {
        var bytes = new List<byte> { (byte)'(', 0xFE, 0xFF };
        bytes.AddRange(inside);
        bytes.Add((byte)')');
        return bytes.ToArray();
    }

    /// <summary>One character of a big-endian UTF-16 string.</summary>
    private static byte[] Wide(char ch) => [(byte)(ch >> 8), (byte)(ch & 0xFF)];

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static async Task<string> TheStringIn(byte[] content)
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
    ///   A line continuation is a backslash and an end of line, two or three bytes that stand for
    ///   nothing, written between two characters' byte pairs. The wide loop used to read the
    ///   string two bytes at a time and work the escape out on a code unit, so resuming after the
    ///   line ending left every pair after it straddling two characters, and the string ran on past
    ///   the parenthesis that closed it. The escapes are resolved on the bytes now, and the string
    ///   decoded afterwards.
    /// </summary>
    [Theory(Timeout = 5000)]
    [InlineData(new[] { (byte)'\n' })]
    [InlineData(new[] { (byte)'\r' })]
    public async Task AContinuationInsideAWideStringJoinsTheCharactersEitherSideOfIt(byte[] lineEnding)
    {
        var content = BigEndianString(Concat(Wide('a'), [(byte)'\\'], lineEnding, Wide('b')));

        var scanned = await TheStringIn(content);

        scanned.Should().Be("ab");
    }

    [Fact(Timeout = 5000)]
    public async Task AContinuationInsideALittleEndianStringJoinsTheCharactersEitherSideOfIt()
    {
        var content = new List<byte> { (byte)'(', 0xFF, 0xFE };
        content.AddRange([(byte)'a', 0x00]);
        content.AddRange([(byte)'\\', (byte)'\n']);
        content.AddRange([(byte)'b', 0x00]);
        content.Add((byte)')');

        var scanned = await TheStringIn(content.ToArray());

        scanned.Should().Be("ab");
    }

    /// <summary>
    ///   An escape is a matter of bytes, so a backslash is whichever byte is 0x5C, whether it is
    ///   the low byte of a pair or not. Here the wide backslash's high byte is a stray zero, and the
    ///   continuation after its low byte takes the next character's high byte into that zero's
    ///   place - which is what the bytes say, and what the document lexer reads too. What matters
    ///   is that the string still ends at its closing parenthesis, and the tokens after it are read.
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task AContinuationAfterAWideBackslashStillEndsTheStringAtItsParenthesis()
    {
        var content = Concat(
            BigEndianString(Concat(Wide('a'), Wide('\\'), [(byte)'\n'], Wide('b'))),
            " Tj"u8.ToArray());

        var tokens = await Interruptibly.Run(() =>
        {
            var lexer = new CLexer(content);
            var scanned = new List<(CSymbol, string)>();
            CSymbol symbol;
            while ((symbol = lexer.ScanNextToken()) != CSymbol.Eof)
                scanned.Add((symbol, lexer.Token));
            return scanned;
        });

        // 00 61 | 00 [5C 0A] 00 62 | 00 padded: 'a', U+0000 and U+6200.
        var expected = new string(['a', (char)0x0000, (char)0x6200]);
        tokens.Should().Equal((CSymbol.UnicodeString, expected), (CSymbol.Operator, "Tj"));
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

    // ----- the little-endian byte order ---------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task AnEscapeInALittleEndianStringStandsForTheByteItIsWrittenIn()
    {
        // An escape stands for one byte wherever it falls, so the line feed U+000A is written the
        // little-endian way round as the escaped byte 0A followed by its high byte 00. This test
        // used to write the backslash as the pair 5C 00 and the 'n' after it, which only a loop
        // working the escape out on a code unit read as a line feed: the byte after the backslash
        // there is the zero.
        var content = new List<byte> { (byte)'(', 0xFF, 0xFE };
        content.AddRange([(byte)'a', 0x00]);
        content.AddRange([0x5C, (byte)'n', 0x00]);
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
