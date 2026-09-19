using System;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The character-level reading Lexer and CLexer share: advancing the current-and-next
///   character pair with the carriage-return-then-line-feed fold, the white-space skip built on
///   it, and the character-class predicates a token grammar is built from. CharacterScanning is
///   internal and this repository carries no InternalsVisibleTo, so it is reached by reflection.
///   A good test here feeds bytes in and asserts on characters out, the way the spec asks for -
///   no lexer, parser or PdfDocument needed to reach the scanner directly.
/// </summary>
public class CharacterScanningTests
{
    const char Eof = (char)65535;

    static readonly Type ScannerType =
        typeof(Lexer).Assembly.GetType("PdfPinata.Pdf.IO.CharacterScanning", throwOnError: true);

    // ----- Advance: the current-and-next character pair, with the CR/LF fold --------------------

    [Fact]
    public void Advance_shiftsNextCharIntoCurrCharAndReadsAFreshNextChar()
    {
        var (curr, next) = InvokeAdvance('a', handleCRLF: false, queue: "b");

        curr.Should().Be('a');
        next.Should().Be('b');
    }

    [Fact]
    public void Advance_foldsALoneCarriageReturnIntoALineFeedWhenAsked()
    {
        var (curr, next) = InvokeAdvance('\r', handleCRLF: true, queue: "b");

        curr.Should().Be('\n');
        next.Should().Be('b');
    }

    [Fact]
    public void Advance_foldsACarriageReturnLineFeedPairIntoOneLineFeedWhenAsked()
    {
        // Both bytes of the pair are consumed - the fold does not leave the LF behind for the
        // next character to see.
        var (curr, next) = InvokeAdvance('\r', handleCRLF: true, queue: "\nb");

        curr.Should().Be('\n');
        next.Should().Be('b');
    }

    [Fact]
    public void Advance_keepsARawCarriageReturnWhenFoldingIsOff()
    {
        // A grammar decoding raw bytes character by character - a literal string's escape
        // handling - passes false so it can tell a carriage return from a line feed itself.
        var (curr, next) = InvokeAdvance('\r', handleCRLF: false, queue: "b");

        curr.Should().Be('\r');
        next.Should().Be('b');
    }

    [Fact]
    public void Advance_readsTheEndOfSourceAsEOF()
    {
        var (curr, next) = InvokeAdvance('z', handleCRLF: true, queue: "");

        curr.Should().Be('z');
        next.Should().Be(Eof);
    }

    // ----- SkipWhiteSpace -------------------------------------------------------------------------

    [Theory]
    [InlineData('\0')] // NUL
    [InlineData('\t')] // HT
    [InlineData('\n')] // LF
    [InlineData('\f')] // FF
    [InlineData('\r')] // CR
    [InlineData(' ')]  // SP
    [InlineData((char)11)]  // vertical tab
    [InlineData((char)173)] // soft hyphen
    public void SkipWhiteSpace_skipsEveryWhiteSpaceCharacterUntilAnOrdinaryOneIsReached(char whiteSpace)
    {
        var queue = new string(whiteSpace, 3) + "x";
        var index = 0;
        var next = () => index < queue.Length ? queue[index++] : Eof;

        var result = InvokeSkipWhiteSpace(next(), next);

        result.Should().Be('x');
    }

    [Fact]
    public void SkipWhiteSpace_stopsAtTheEndOfSourceWhenEverythingWasWhiteSpace()
    {
        var queue = "   ";
        var index = 0;
        var next = () => index < queue.Length ? queue[index++] : Eof;

        var result = InvokeSkipWhiteSpace(next(), next);

        result.Should().Be(Eof);
    }

    [Fact]
    public void SkipWhiteSpace_returnsAnOrdinaryCharacterUnchanged()
    {
        var result = InvokeSkipWhiteSpace('x', () => 'y');

        result.Should().Be('x');
    }

    // ----- character-class predicates ---------------------------------------------------------

    [Theory]
    [InlineData('\0', true)]
    [InlineData('\t', true)]
    [InlineData('\n', true)]
    [InlineData('\f', true)]
    [InlineData('\r', true)]
    [InlineData(' ', true)]
    // Narrower than SkipWhiteSpace: a vertical tab and a soft hyphen are not white space by this
    // predicate, only by the wider skip built on top of it - the same asymmetry Lexer's own
    // IsWhiteSpace and MoveToNonWhiteSpace have always had.
    [InlineData((char)11, false)]
    [InlineData((char)173, false)]
    [InlineData('a', false)]
    public void IsWhiteSpace_matchesPdfsNarrowerWhiteSpaceList(char ch, bool expected)
    {
        InvokeStatic<bool>("IsWhiteSpace", ch).Should().Be(expected);
    }

    [Theory]
    [InlineData('(', true)]
    [InlineData(')', true)]
    [InlineData('<', true)]
    [InlineData('>', true)]
    [InlineData('[', true)]
    [InlineData(']', true)]
    [InlineData('{', true)]
    [InlineData('}', true)]
    [InlineData('/', true)]
    [InlineData('%', true)]
    [InlineData('a', false)]
    public void IsDelimiter_matchesTheNineDelimiterCharacters(char ch, bool expected)
    {
        InvokeStatic<bool>("IsDelimiter", ch).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('a', true)]
    [InlineData('f', true)]
    [InlineData('A', true)]
    [InlineData('F', true)]
    [InlineData('g', false)]
    [InlineData('G', false)]
    public void IsHexChar_acceptsBothCasesOfAThroughF(char ch, bool expected)
    {
        InvokeStatic<bool>("IsHexChar", ch).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('7', true)]
    [InlineData('8', false)]
    [InlineData('9', false)]
    [InlineData('a', false)]
    public void IsOctalDigit_stopsAtSevenRatherThanNine(char ch, bool expected)
    {
        InvokeStatic<bool>("IsOctalDigit", ch).Should().Be(expected);
    }

    // ----- reflection plumbing --------------------------------------------------------------------

    /// <summary>Invokes Advance once, seeding nextChar and reading the rest of the queue from it.</summary>
    static (char curr, char next) InvokeAdvance(char initialNextChar, bool handleCRLF, string queue)
    {
        var index = 0;
        var readNextByte = () => index < queue.Length ? queue[index++] : Eof;

        var method = ScannerType.GetMethod("Advance", BindingFlags.Public | BindingFlags.Static);
        object[] args = { '\0', initialNextChar, handleCRLF, readNextByte };
        method.Invoke(null, args);
        return ((char)args[0], (char)args[1]);
    }

    static char InvokeSkipWhiteSpace(char currChar, Func<char> scanNextChar)
    {
        var method = ScannerType.GetMethod("SkipWhiteSpace", BindingFlags.Public | BindingFlags.Static);
        return (char)method.Invoke(null, new object[] { currChar, scanNextChar });
    }

    static T InvokeStatic<T>(string methodName, params object[] args)
    {
        var method = ScannerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        return (T)method.Invoke(null, args);
    }
}
