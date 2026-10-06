using System;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using TUnit.Core;

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
    private const char Eof = (char)65535;

    private static readonly Type ScannerType =
        typeof(Lexer).Assembly.GetType("PdfPinata.Pdf.IO.CharacterScanning", throwOnError: true);

    // ----- Advance: the current-and-next character pair, with the CR/LF fold --------------------

    [Test]
    public void Advance_shiftsNextCharIntoCurrCharAndReadsAFreshNextChar()
    {
        var (curr, next) = InvokeAdvance('a', handleCrlf: false, queue: "b");

        curr.Should().Be('a');
        next.Should().Be('b');
    }

    [Test]
    public void Advance_foldsALoneCarriageReturnIntoALineFeedWhenAsked()
    {
        var (curr, next) = InvokeAdvance('\r', handleCrlf: true, queue: "b");

        curr.Should().Be('\n');
        next.Should().Be('b');
    }

    [Test]
    public void Advance_foldsACarriageReturnLineFeedPairIntoOneLineFeedWhenAsked()
    {
        // Both bytes of the pair are consumed - the fold does not leave the LF behind for the
        // next character to see.
        var (curr, next) = InvokeAdvance('\r', handleCrlf: true, queue: "\nb");

        curr.Should().Be('\n');
        next.Should().Be('b');
    }

    [Test]
    public void Advance_keepsARawCarriageReturnWhenFoldingIsOff()
    {
        // A grammar decoding raw bytes character by character - a literal string's escape
        // handling - passes false so it can tell a carriage return from a line feed itself.
        var (curr, next) = InvokeAdvance('\r', handleCrlf: false, queue: "b");

        curr.Should().Be('\r');
        next.Should().Be('b');
    }

    [Test]
    public void Advance_readsTheEndOfSourceAsEOF()
    {
        var (curr, next) = InvokeAdvance('z', handleCrlf: true, queue: "");

        curr.Should().Be('z');
        next.Should().Be(Eof);
    }

    // ----- SkipWhiteSpace -------------------------------------------------------------------------

    [Test]
    [Arguments('\0')] // NUL
    [Arguments('\t')] // HT
    [Arguments('\n')] // LF
    [Arguments('\f')] // FF
    [Arguments('\r')] // CR
    [Arguments(' ')]  // SP
    [Arguments((char)11)]  // vertical tab
    [Arguments((char)173)] // soft hyphen
    public void SkipWhiteSpace_skipsEveryWhiteSpaceCharacterUntilAnOrdinaryOneIsReached(char whiteSpace)
    {
        var queue = new string(whiteSpace, 3) + "x";
        var index = 0;
        var next = () => index < queue.Length ? queue[index++] : Eof;

        var result = InvokeSkipWhiteSpace(next(), next);

        result.Should().Be('x');
    }

    [Test]
    public void SkipWhiteSpace_stopsAtTheEndOfSourceWhenEverythingWasWhiteSpace()
    {
        var queue = "   ";
        var index = 0;
        var next = () => index < queue.Length ? queue[index++] : Eof;

        var result = InvokeSkipWhiteSpace(next(), next);

        result.Should().Be(Eof);
    }

    [Test]
    public void SkipWhiteSpace_returnsAnOrdinaryCharacterUnchanged()
    {
        var result = InvokeSkipWhiteSpace('x', () => 'y');

        result.Should().Be('x');
    }

    // ----- character-class predicates ---------------------------------------------------------

    [Test]
    [Arguments('\0', true)]
    [Arguments('\t', true)]
    [Arguments('\n', true)]
    [Arguments('\f', true)]
    [Arguments('\r', true)]
    [Arguments(' ', true)]
    // Narrower than SkipWhiteSpace: a vertical tab and a soft hyphen are not white space by this
    // predicate, only by the wider skip built on top of it - the same asymmetry Lexer's own
    // IsWhiteSpace and MoveToNonWhiteSpace have always had.
    [Arguments((char)11, false)]
    [Arguments((char)173, false)]
    [Arguments('a', false)]
    public void IsWhiteSpace_matchesPdfsNarrowerWhiteSpaceList(char ch, bool expected)
    {
        InvokeStatic<bool>("IsWhiteSpace", ch).Should().Be(expected);
    }

    [Test]
    [Arguments('(', true)]
    [Arguments(')', true)]
    [Arguments('<', true)]
    [Arguments('>', true)]
    [Arguments('[', true)]
    [Arguments(']', true)]
    [Arguments('{', true)]
    [Arguments('}', true)]
    [Arguments('/', true)]
    [Arguments('%', true)]
    [Arguments('a', false)]
    public void IsDelimiter_matchesTheNineDelimiterCharacters(char ch, bool expected)
    {
        InvokeStatic<bool>("IsDelimiter", ch).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments('a', true)]
    [Arguments('f', true)]
    [Arguments('A', true)]
    [Arguments('F', true)]
    [Arguments('g', false)]
    [Arguments('G', false)]
    public void IsHexChar_acceptsBothCasesOfAThroughF(char ch, bool expected)
    {
        InvokeStatic<bool>("IsHexChar", ch).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('7', true)]
    [Arguments('8', false)]
    [Arguments('9', false)]
    [Arguments('a', false)]
    public void IsOctalDigit_stopsAtSevenRatherThanNine(char ch, bool expected)
    {
        InvokeStatic<bool>("IsOctalDigit", ch).Should().Be(expected);
    }

    // ----- reflection plumbing --------------------------------------------------------------------

    /// <summary>Invokes Advance once, seeding nextChar and reading the rest of the queue from it.</summary>
    private static (char curr, char next) InvokeAdvance(char initialNextChar, bool handleCrlf, string queue)
    {
        var index = 0;
        var readNextByte = () => index < queue.Length ? queue[index++] : Eof;

        var method = ScannerType.GetMethod("Advance", BindingFlags.Public | BindingFlags.Static);
        object[] args = ['\0', initialNextChar, handleCrlf, readNextByte];
        // ReSharper disable once PossibleNullReferenceException
        method.Invoke(null, args);
        return ((char)args[0], (char)args[1]);
    }

    private static char InvokeSkipWhiteSpace(char currChar, Func<char> scanNextChar)
    {
        var method = ScannerType.GetMethod("SkipWhiteSpace", BindingFlags.Public | BindingFlags.Static);
        // ReSharper disable once PossibleNullReferenceException
        return (char)method.Invoke(null, [currChar, scanNextChar]);
    }

    private static T InvokeStatic<T>(string methodName, params object[] args)
    {
        var method = ScannerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        // ReSharper disable once PossibleNullReferenceException
        return (T)method.Invoke(null, args);
    }
}
