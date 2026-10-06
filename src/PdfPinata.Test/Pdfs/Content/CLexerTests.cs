using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.Content;

/// <summary>
/// A content stream can be truncated part way through a token, or hold a character the
/// token it is in the middle of has no use for, and every scan loop has to keep advancing
/// through both. The timeouts turn a regression into a failure rather than into an
/// exhausted heap.
/// </summary>
public class CLexerTests
{
    // A name at the end of a content stream has no delimiter to end it, whether or not one
    // is written: _nextChar is read one character ahead, so a trailing blank is still in it
    // when the content runs out.
    [Test, Timeout(5000)]
    [Arguments("/Foo")]
    [Arguments("/Foo ")]
    [Arguments("BT /Foo")]
    public async Task ScanNextToken_terminatesForANameThatEndsTheContentStream(string content)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Name).Should().Equal("/Foo");
    }

    // The document lexer treats a vertical tab and a soft hyphen as white space between tokens,
    // wider than PDF's own list of NUL, HT, LF, FF, CR and SP. A content stream a document lexer
    // reads should read the same way, rather than folding the separator into one of the operators
    // either side of it or refusing the byte outright.
    [Test, Timeout(5000)]
    [Arguments((byte)11)]  // vertical tab
    [Arguments((byte)173)] // soft hyphen
    public async Task ScanNextToken_treatsAVerticalTabAndASoftHyphenAsWhiteSpace(byte separator)
    {
        var content = new[] { (byte)'B', (byte)'T', separator, (byte)'Q' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.Operator).Should().Equal("BT", "Q");
    }

    // The document lexer treats '{' and '}' as delimiters; CLexer's copy of the list had both
    // commented out, so a name written hard against one - with no white space to end it instead -
    // swallowed the brace as if it were one more character of the name. Neither lexer's grammar
    // has a token that starts with a bare brace, so this scans the one name token rather than
    // scanning on into what follows it.
    [Test, Timeout(5000)]
    [Arguments("/Foo{", "/Foo")]
    [Arguments("/Foo}", "/Foo")]
    public async Task ScanName_endsAtABraceWithNoWhiteSpaceNeeded(string content, string expected)
    {
        var scanned = await Interruptibly.Run(() =>
        {
            var lexer = new CLexer(Encoding.ASCII.GetBytes(content));
            var symbol = lexer.ScanNextToken();
            return (symbol, lexer.Token);
        });

        scanned.symbol.Should().Be(CSymbol.Name);
        scanned.Token.Should().Be(expected);
    }

    // A '#' in a name stands for the byte its two hexadecimal digits spell - including a
    // delimiter or a blank, which the name then keeps rather than ending at.
    [Test, Timeout(5000)]
    [Arguments("/A#41 ", "/AA")]
    [Arguments("/A#4a ", "/AJ")]
    [Arguments("/A#20B ", "/A B")]
    [Arguments("/A#2F", "/A/")]
    public async Task ScanName_readsTwoHexDigitsAfterANumberSignAsTheByteTheySpell(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Name).Should().Equal(expected);
    }

    // Anything other than two hexadecimal digits after a '#' leaves it as a character of the
    // name. int.Parse was handed whatever two characters came next, so each of these threw a
    // FormatException and the content stream could not be read at all.
    [Test, Timeout(5000)]
    [Arguments("/A#ZZ Q", "/A#ZZ")]
    [Arguments("/A#4Z Q", "/A#4Z")]
    [Arguments("/A#4 Q", "/A#4")]
    [Arguments("/A# Q", "/A#")]
    [Arguments("/A#4", "/A#4")]
    [Arguments("/A#", "/A#")]
    [Arguments("/A##41 Q", "/A#A")]
    public async Task ScanName_keepsANumberSignNotFollowedByTwoHexDigits(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Name).Should().Equal(expected);
    }

    [Test, Timeout(5000)]
    [Arguments("<< /W 16", CSymbol.Dictionary)]
    [Arguments("(unterminated", CSymbol.String)]
    [Arguments("<48656C6C6F", CSymbol.HexString)]
    public async Task ScanNextToken_terminatesForATruncatedToken(string content, CSymbol expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        tokens.Should().Contain(token => token.Symbol == expected);
    }

    /// <summary>
    ///   A dictionary ends at the <c>&gt;&gt;</c> that matches, which is not the same as the first
    ///   <c>&gt;</c> that comes along. Ending at the first one left the rest of the dictionary to be
    ///   read as operators and the stray <c>&gt;</c> then stopped the whole content stream — which is
    ///   what <c>/Span &lt;&lt;/ActualText &lt;FEFF0066&gt;&gt;&gt; BDC</c> did, the sequence that says
    ///   a ligature stands for several characters.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments("<</ActualText <FEFF00660069>>> BDC", "<</ActualText <FEFF00660069>>>")]
    [Arguments("<</Outer <</Inner 1>> >> BDC", "<</Outer <</Inner 1>> >>")]
    [Arguments("<</Desc (a > b)>> BDC", "<</Desc (a > b)>>")]
    [Arguments("<</Desc (escaped \\) and > )>> BDC", "<</Desc (escaped \\) and > )>>")]
    [Arguments("<</Desc (nested (pair) > )>> BDC", "<</Desc (nested (pair) > )>>")]
    // A comment is legal wherever whitespace is, which includes between a dictionary's keys, and
    // what it says is not syntax - so neither the '>>' nor the '(' in one closes or opens anything.
    [Arguments("<</A 1 % see >> below\n/B 2>> BDC", "<</A 1 % see >> below\n/B 2>>")]
    [Arguments("<</A 1 % unclosed ( paren\n/B 2>> BDC", "<</A 1 % unclosed ( paren\n/B 2>>")]
    public async Task ScanNextToken_readsADictionaryToTheAngleBracketsThatMatch(
        string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Dictionary).Should().Equal(expected);

        // And the operator after it is still found, which is what says the reader was left in the
        // right place rather than somewhere inside what it had just read.
        TokensOf(tokens, CSymbol.Operator).Should().Equal("BDC");
    }

    [Test, Timeout(5000)]
    public async Task ScanNextToken_readsAPlainDictionaryExactlyAsItAlwaysDid()
    {
        // The shape every tagged page is full of. Whatever else changes about scanning a dictionary,
        // this one has to come back the same or every marked-content sequence stops being readable.
        var tokens = await ScanAll(new CLexer("/P <</MCID 0>> BDC"u8.ToArray()));

        TokensOf(tokens, CSymbol.Dictionary).Should().Equal("<</MCID 0>>");
        TokensOf(tokens, CSymbol.Name).Should().Equal("/P");
        TokensOf(tokens, CSymbol.Operator).Should().Equal("BDC");
    }

    [Test, Timeout(5000)]
    public async Task ScanNextToken_terminatesForATruncatedUnicodeString()
    {
        // An opening parenthesis, the UTF-16BE byte order mark that puts ScanLiteralString
        // on its 16-bit path, one character, and then nothing more.
        var content = new byte[] { (byte)'(', 0xFE, 0xFF, 0x00, (byte)'A' };

        var tokens = await ScanAll(new CLexer(content));

        tokens.Should().Contain(token => token.Symbol == CSymbol.UnicodeString);
    }

    [Test, Timeout(5000)]
    [Arguments("<48656C6C6F>", "48,65,6C,6C,6F")]
    // The two digits of a byte may be separated by white space, which is ignored.
    [Arguments("<4 8 65>", "48,65")]
    // A hex string with an odd number of digits ends in a zero, so the digit left over is
    // the high one: '<A>' is 0xA0 rather than 0x0A.
    [Arguments("<48A>", "48,A0")]
    [Arguments("<A>", "A0")]
    // A character that is neither '>' nor a hex digit is stepped over. Before, '*' matched
    // no branch and the scan never advanced, and 'G' was taken for a digit and threw.
    [Arguments("<48*65>", "48,65")]
    [Arguments("<48G65>", "48,65")]
    [Arguments("<48 * 65>", "48,65")]
    // Including when it falls between the two digits of one byte, which is where white
    // space is passed over as well.
    [Arguments("<4*8>", "48")]
    // Truncated, so the closing '>' never arrives and the last digit is the last byte of
    // the content.
    [Arguments("<48656C6C6F", "48,65,6C,6C,6F")]
    [Arguments("<48A", "48,A0")]
    public async Task ScanHexadecimalString_readsTheBytesTheDigitsSpell(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.HexString).Should().Equal(BytesSpelling(expected));
    }

    // A hex string carrying the UTF-16BE byte order mark decodes to text either way, but used to
    // come back as CSymbol.HexString regardless - CParser treats the two symbols alike, so nothing
    // downstream noticed, but the symbol itself said less than the scanner already knew.
    [Test, Timeout(5000)]
    public async Task ScanHexadecimalString_isRecognisedByItsByteOrderMark()
    {
        var tokens = await ScanAll(new CLexer("<FEFF00480049>"u8.ToArray()));

        TokensOf(tokens, CSymbol.UnicodeHexString).Should().Equal("HI");
    }

    // A Unicode hex string short of the low byte of its last character used to be caught only by
    // a Debug.Assert, which does nothing in a Release build - where the decode loop then read one
    // character past the end of the string. The missing byte is a zero, the same reading a plain
    // hex string missing its final digit gets, just above.
    [Test, Timeout(5000)]
    [Arguments("<FEFF0>")]
    [Arguments("<FEFF0")]
    public async Task ScanHexadecimalString_padsAUnicodeHexStringShortOfItsLastByte(string content)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.UnicodeHexString).Should().Equal("\0");
    }

    [Test, Timeout(5000)]
    [Arguments("BT", CSymbol.Operator, "BT")]
    [Arguments("q Q", CSymbol.Operator, "Q")]
    [Arguments("1 0 0 1 20 30 cm", CSymbol.Operator, "cm")]
    [Arguments("/F1 12", CSymbol.Integer, "12")]
    [Arguments("0.5", CSymbol.Real, "0.5")]
    [Arguments("/Foo", CSymbol.Name, "/Foo")]
    public async Task ScanNextToken_readsTheLastCharacterOfTheContent(string content, CSymbol expected, string token)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        tokens.Last().Should().Be((expected, token));
    }

    [Test, Timeout(5000)]
    public async Task ScanNextToken_readsALastCharacterThatEndsALine()
    {
        // A lone CR is a line feed, and the one that ends the content has nothing to pair
        // with. It ends the operator rather than being scanned as part of it.
        var tokens = await ScanAll(new CLexer("BT\r"u8.ToArray()));

        tokens.Last().Should().Be((CSymbol.Operator, "BT"));
    }

    // The escape sequences a literal string may carry. Each is written into the content as a
    // backslash and a character, and comes out of the scanner as the one character it stands
    // for, so a token the same length as the text that spelled it means an escape was missed.
    [Test, Timeout(5000)]
    [Arguments(@"(a\nb)", "a\nb")]
    [Arguments(@"(a\rb)", "a\rb")]
    [Arguments(@"(a\tb)", "a\tb")]
    [Arguments(@"(a\bb)", "a\bb")]
    [Arguments(@"(a\fb)", "a\fb")]
    [Arguments(@"(a\(b)", "a(b")]
    [Arguments(@"(a\)b)", "a)b")]
    [Arguments(@"(a\\b)", @"a\b")]
    public async Task ScanLiteralString_readsAnEscapedCharacterAsTheOneItStandsFor(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    // A backslash and up to three octal digits are one character. The scan stops at the third
    // digit whether or not a fourth follows, so the digits after it are text.
    [Test, Timeout(5000)]
    [Arguments(@"(\101)", "A")]
    [Arguments(@"(\1)", "\u0001")]
    [Arguments(@"(\12)", "\n")]
    [Arguments(@"(\0)", "\0")]
    [Arguments(@"(\377)", "ÿ")]
    [Arguments(@"(\1012)", "A2")]
    [Arguments(@"(\101\102)", "AB")]
    // Octal runs to '7'. An '8' or a '9' cannot belong to a code, so it ends one already begun
    // and otherwise loses only its backslash, like any escape the scanner does not know. The
    // test for a digit used to be char.IsDigit, which let both in: '\8' came out as a backspace
    // rather than as the digit it is, and '\18' as a tab rather than as two characters.
    [Arguments(@"(\8)", "8")]
    [Arguments(@"(\9)", "9")]
    [Arguments(@"(\18)", "\u0001" + "8")]
    [Arguments(@"(\118)", "\t" + "8")]
    public async Task ScanLiteralString_readsAnOctalCodeAsOneCharacter(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    /// <summary>
    /// A backslash at the end of a line continues the string onto the next one, and neither the
    /// backslash nor the line feed is part of it.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_joinsTheLinesABackslashContinues()
    {
        var tokens = await ScanAll(new CLexer("(a\\\nb)"u8.ToArray()));

        TokensOf(tokens, CSymbol.String).Should().Equal("ab");
    }

    /// <summary>
    /// Only one line ending is swallowed: a second one after it is part of the string. A CR LF is
    /// one line ending, so the LF of a second pair survives but not the LF of the first.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments("\r\n\n", "a\nb")]
    [Arguments("\n\n", "a\nb")]
    [Arguments("\r\r", "a\rb")]
    public async Task ScanLiteralString_keepsALineEndingAfterTheOneABackslashContinues(string lineEndings, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes("(a\\" + lineEndings + "b)")));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    /// <summary>
    /// A backslash before anything else is dropped and the character is kept, which is how a
    /// string carrying an escape the specification does not define still scans.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments(@"(a\qb)", "aqb")]
    [Arguments(@"(a\ b)", "a b")]
    public async Task ScanLiteralString_keepsTheCharacterAfterAnEscapeItDoesNotKnow(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    // Parentheses nest, so an inner pair is part of the string and only the one that closes the
    // outermost level ends it.
    [Test, Timeout(5000)]
    [Arguments("(a(b)c)", "a(b)c")]
    [Arguments("((nested))", "(nested)")]
    [Arguments("(a(b(c)d)e)", "a(b(c)d)e")]
    [Arguments("()", "")]
    public async Task ScanLiteralString_readsNestedParenthesesAsPartOfTheString(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    /// <summary>
    /// A byte order mark of FE FF puts the scan on its 16-bit path, where every character is two
    /// bytes rather than one.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_readsAUnicodeStringTwoBytesAtATime()
    {
        var content = new byte[] { (byte)'(', 0xFE, 0xFF, 0x00, (byte)'H', 0x00, (byte)'i', (byte)')' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.UnicodeString).Should().Equal("Hi");
    }

    /// <summary>
    /// Adobe Reader also accepts the little-endian byte order mark, FF FE, and the document lexer
    /// decodes it too - CLexer's copy checked for FE FF alone, so the same bytes read as raw pairs
    /// rather than as the text they spell.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_readsALittleEndianUnicodeStringTheOtherWayRound()
    {
        var content = new byte[] { (byte)'(', 0xFF, 0xFE, (byte)'H', 0x00, (byte)'i', 0x00, (byte)')' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.UnicodeString).Should().Equal("Hi");
    }

    /// <summary>
    /// Characters whose high byte is zero would read the same whether the two bytes were combined
    /// or the high one simply dropped, so a string of them cannot tell the two apart. These are
    /// above the Latin block and fail if the high byte is not carried.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_carriesTheHighByteOfAUnicodeCharacter()
    {
        // U+03A9 GREEK CAPITAL LETTER OMEGA and U+20AC EURO SIGN.
        var content = new byte[] { (byte)'(', 0xFE, 0xFF, 0x03, 0xA9, 0x20, 0xAC, (byte)')' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.UnicodeString).Should().Equal("Ω€");
    }

    [Test, Timeout(5000)]
    public async Task ScanLiteralString_readsAUnicodeStringWithNothingInIt()
    {
        var content = new byte[] { (byte)'(', 0xFE, 0xFF, (byte)')' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.UnicodeString).Should().Equal("");
    }

    /// <summary>
    /// Builds the token a run of bytes scans to, one char per byte, from a comma separated
    /// list of hexadecimal byte values.
    /// </summary>
    private static string BytesSpelling(string byteValues)
    {
        return new string(byteValues.Split(',')
            .Select(value => (char)Convert.ToInt32(value, 16))
            .ToArray());
    }

    // ----- literal strings ------------------------------------------------------------------------

    [Test, Timeout(5000)]
    [Arguments("(plain)", "plain")]
    [Arguments("()", "")]
    [Arguments("(a(nested)b)", "a(nested)b", "a bracketed run is part of the string")]
    [Arguments("(a((two deep))b)", "a((two deep))b")]
    [Arguments("(a\\(b)", "a(b", "and an escaped bracket needs no partner")]
    [Arguments("(a\\)b)", "a)b")]
    [Arguments("(a\\\\b)", "a\\b")]
    public async Task ScanLiteralString_readsBracketsWhetherBalancedOrEscaped(
        string content, string expected, string because = "")
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal([expected], because);
    }

    [Test, Timeout(5000)]
    [Arguments("(a\\nb)", "a\nb")]
    [Arguments("(a\\rb)", "a\rb")]
    [Arguments("(a\\tb)", "a\tb")]
    [Arguments("(a\\bb)", "a\bb")]
    [Arguments("(a\\fb)", "a\fb")]
    public async Task ScanLiteralString_readsEveryNamedEscape(string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    [Test, Timeout(5000)]
    [Arguments("(\\101)", "A", "three digits")]
    [Arguments("(\\10)", "\b", "two")]
    [Arguments("(\\7)", "\a", "and one")]
    [Arguments("(\\1011)", "A1", "a fourth digit is text, not part of the code")]
    [Arguments("(\\0)", "\0", "and nought is a character like any other")]
    public async Task ScanLiteralString_readsAnOctalCodeOfOneTwoOrThreeDigits(
        string content, string expected, string because)
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal([expected], because);
    }

    [Test, Timeout(5000)]
    [Arguments("(a\\8b)", "a8b")]
    [Arguments("(a\\9b)", "a9b")]
    public async Task ScanLiteralString_keepsTheDigitWhenItIsNotAnOctalOne(
        string content, string expected)
    {
        // Eight and nine end an octal code rather than extending one, so the backslash is
        // dropped and the digit kept as the text it is.
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    [Test, Timeout(5000)]
    [Arguments("(a\\\nb)", "ab")]
    [Arguments("(a\\\rb)", "ab")]
    // A CR LF is one end-of-line marker (ISO 32000-1 7.2.3), and a backslash before one is
    // ignored along with all of it (7.3.4.2). Both lexers used to swallow the CR alone and keep
    // the LF as the first character of the next line, and this case pinned that; the document
    // lexer's side is LexerLineContinuationTests.
    [Arguments("(a\\\r\nb)", "ab")]
    public async Task ScanLiteralString_treatsABackslashBeforeAnEndOfLineAsAContinuation(
        string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    /// <summary>
    ///   A carriage return that is not escaped is not a line continuation and is not folded into
    ///   a line feed either - the document lexer reads a literal string's characters with folding
    ///   off throughout, so a bare CR is kept exactly as written. CLexer used to fold every CR to
    ///   LF unconditionally, escaped or not, which is what made the guard above look unnecessary:
    ///   the fold did its job by accident. Closing it here is what makes an explicit case for CR
    ///   in the escape switch load-bearing rather than redundant.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_keepsABareCarriageReturnRatherThanFoldingIt()
    {
        var content = "(a\rb)"u8.ToArray();

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.String).Should().Equal("a\rb");
    }

    /// <summary>
    ///   Content that stops in the middle of a string. The scanner gives up at the end rather
    ///   than scanning for ever, and what it has read so far is the string - but a backslash
    ///   immediately before the end used to put the end-of-file marker itself into the text,
    ///   because the escape read past the guard that watches for it. See the backlog spec's
    ///   finding F16.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments("(unterminated", "unterminated")]
    [Arguments("(a\\", "a")]
    [Arguments("(", "")]
    [Arguments("(a(unclosed inner", "a(unclosed inner")]
    public async Task ScanLiteralString_endsAtTheEndOfTheContentWithoutInventingCharacters(
        string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes(content)));

        TokensOf(tokens, CSymbol.String).Should().Equal(expected);
    }

    /// <summary>
    ///   A literal string that opens with the UTF-16 byte order mark is read two bytes at a time,
    ///   by a branch that is a near-copy of the 8-bit one beside it. The copy had the same fault
    ///   and did not get the same fix: content ending in a backslash put the end-of-file marker
    ///   into the text. See the backlog spec's finding F18.
    /// </summary>
    [Test, Timeout(5000)]
    public async Task ScanLiteralString_endsAUnicodeStringAtTheEndOfTheContentWithoutInventingCharacters()
    {
        // "(" BOM "A" then a lone backslash and nothing after it.
        var content = new byte[] { (byte)'(', 0xFE, 0xFF, 0x00, (byte)'A', 0x00, (byte)'\\' };

        var tokens = await ScanAll(new CLexer(content));

        TokensOf(tokens, CSymbol.UnicodeString).Should().Equal("A");
    }

    [Test, Timeout(5000)]
    public async Task ScanLiteralString_readsAStringThatIsNothingButEscapes()
    {
        var tokens = await ScanAll(new CLexer(Encoding.Latin1.GetBytes("(\\n\\r\\t\\\\\\(\\))")));

        TokensOf(tokens, CSymbol.String).Should().Equal("\n\r\t\\()");
    }

    /// <summary>
    ///   <c>d0</c> and <c>d1</c> are the only content operators with a digit in them, and a
    ///   Type 3 glyph description has to begin with one of the two. The scanner ended an operator
    ///   at the first character that was not a letter, so it read the setdash operator <c>d</c>
    ///   and left the digit to become an operand of whatever came next - which meant every
    ///   operator in every Type 3 glyph was handed one operand too many, and the first of them
    ///   was a number where a name should be. See the backlog spec's finding F15.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments("1000 0 d0 /Im1 Do", "d0")]
    [Arguments("1000 0 0 0 200 200 d1 /Im1 Do", "d1")]
    public async Task ScanNextToken_readsTheGlyphMetricOperatorsAsOneTokenEach(
        string content, string expected)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Operator).Should().Equal(expected, "Do");
    }

    [Test, Timeout(5000)]
    public async Task ScanNextToken_stillReadsSetdashAsItself()
    {
        // The operator the two are told apart from. A digit only joins a 'd' when it follows it
        // with nothing in between, which is never how setdash and its next operand are written.
        var tokens = await ScanAll(new CLexer("[3 3] 0 d 0 0 m"u8.ToArray()));

        TokensOf(tokens, CSymbol.Operator).Should().Equal("d", "m");
    }

    [Test, Timeout(5000)]
    [Arguments("d2")]
    [Arguments("d9")]
    public async Task ScanNextToken_joinsNoOtherDigitToAnOperator(string content)
    {
        // There is no d2, so the digit is an operand of whatever follows rather than part of the
        // operator - which is what the scanner did for d0 and d1 too, and should not have.
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Operator).Should().Equal("d");
    }

    /// <summary>
    ///   An integer too large for CSymbol.Integer used to be refused outright. The document lexer
    ///   degrades the same value to a real rather than throw - CSymbol has no separate "long
    ///   integer" symbol to reach for instead, so a real is the fallback here too.
    /// </summary>
    [Test, Timeout(5000)]
    [Arguments("5000000000")]  // past Int32.MaxValue
    [Arguments("-5000000000")] // and its negative counterpart
    public async Task ScanNumber_degradesAnIntegerOutOfRangeToAReal(string content)
    {
        var tokens = await ScanAll(new CLexer(Encoding.ASCII.GetBytes(content)));

        TokensOf(tokens, CSymbol.Real).Should().Equal(content);
    }

    /// <summary>
    ///   A token with more digits than Int64 itself holds - not merely more than
    ///   CSymbol.Integer's Int32 range - overflows the long accumulator ScanNumber builds it in.
    ///   Unchecked arithmetic wraps rather than throws, and the wrapped value used to be trusted
    ///   anyway: <c>Debug.Assert(Int64.Parse(...) == value)</c> then called Int64.Parse on a
    ///   token Int64.Parse itself cannot represent, which throws OverflowException rather than
    ///   returning false - the assert never got the chance to fail cleanly, and TokenToReal's own
    ///   assert against a freshly-parsed double would have caught the wrong value even if it had.
    ///   ScanNumber now stops trusting the accumulator once it can no longer add a digit without
    ///   overflowing, and reads the real from the token text instead - the same source a real
    ///   with more than ten decimal digits already used. TokenToReal is internal and this
    ///   repository carries no InternalsVisibleTo, so it is reached by reflection.
    /// </summary>
    [Test]
    [Arguments("99999999999999999999999999999")]  // 29 nines - past Int64.MaxValue's own width
    [Arguments("-99999999999999999999999999999")] // and its negative counterpart
    public void ScanNumber_degradesAnIntegerBeyondInt64ToARealWithoutOverflowing(string content)
    {
        var lexer = new CLexer(Encoding.ASCII.GetBytes(content));

        var symbol = lexer.ScanNextToken();

        symbol.Should().Be(CSymbol.Real);
        lexer.Token.Should().Be(content);
        var tokenToReal = typeof(CLexer)
            .GetProperty("TokenToReal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((double)tokenToReal.GetValue(lexer)!).Should()
            .Be(double.Parse(content, CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///   The document lexer refuses to append the end-of-content marker to a token rather than
    ///   grow one out of it, and CLexer now carries the same guard. No grammar rule reaches it
    ///   through the public surface - each of ScanComment, ScanName and ScanOperator checks the
    ///   character this method returns for the end of content before calling it again - which is
    ///   exactly why the guard exists: it is what stops a rule that someday does not make that
    ///   check from reading past the token buffer instead. AppendAndScanNextChar is internal and
    ///   this repository carries no InternalsVisibleTo, so it is reached by reflection.
    /// </summary>
    [Test]
    public void AppendAndScanNextChar_refusesToAppendTheEndOfContentMarker()
    {
        var lexer = new CLexer([]);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CLexer).GetField("_currChar", flags)!.SetValue(lexer, (char)0xFFFF);
        var method = typeof(CLexer).GetMethod("AppendAndScanNextChar", flags)!;

        Action invoke = () => method.Invoke(lexer, null);

        invoke.Should().Throw<TargetInvocationException>()
            .WithInnerException<ContentReaderException>();
    }

    private static IEnumerable<string> TokensOf(IEnumerable<(CSymbol Symbol, string Token)> tokens, CSymbol symbol)
    {
        return tokens.Where(token => token.Symbol == symbol).Select(token => token.Token);
    }

    /// <summary>
    /// Scans the whole content on a thread of its own, so that the Timeout on these tests can
    /// interrupt a scan that never ends. Each token is taken while it is still current,
    /// since the next scan clears it.
    /// </summary>
    private static Task<List<(CSymbol Symbol, string Token)>> ScanAll(CLexer lexer)
    {
        return Interruptibly.Run(() =>
        {
            var tokens = new List<(CSymbol, string)>();
            CSymbol symbol;
            while ((symbol = lexer.ScanNextToken()) != CSymbol.Eof)
                tokens.Add((symbol, lexer.Token));
            return tokens;
        });
    }
}
