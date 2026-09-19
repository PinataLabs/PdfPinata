using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A literal string beginning with a byte order mark is UTF-16, two bytes to the character. One
///   byte short of that is a string whose last character is missing half of itself, and the
///   document lexer reads the missing byte as a zero.
/// </summary>
/// <remarks>
///   The reading was there already and had no test. What it also had was a
///   <c>DebugBreak.Break()</c> beside it, left over from the "TODO What does the PDF Reference say
///   about this case?" written above it - so a caller opening a slightly corrupt document under a
///   debugger was stopped inside the library at a breakpoint nobody had set. The break is gone and
///   the reading is pinned here instead.
///
///   The break itself cannot be asserted against: it fired only when a debugger was attached, and
///   nothing is attached to a test host. What these assert is the reading it was standing beside.
///
///   The missing byte is read as a zero because that is the reading the reference does specify for
///   the same shortfall in a hexadecimal string, which <see cref="LexerHexStringTests"/> covers.
///   It did not use to be. The padding was written <c>temp.Append(0)</c>, which binds to
///   <c>StringBuilder.Append(int)</c> and appends the *digit* zero, 0x30, rather than the
///   character. Big endian ended a byte-short string in '0'; little endian was worse, because
///   there the stray digit lands in the high half and turned a nearly complete "I" into U+3049.
///   Nothing caught it, because nothing read a byte-short Unicode string.
///
///   Written as byte arrays rather than as strings. What the lexer scans here is a byte sequence
///   and not text, half of it unprintable, and a zero byte written into a string literal is an
///   invisible character in the source that the next person to touch this file cannot see.
/// </remarks>
public class LexerUnicodeStringTests
{
    const byte Bom0 = 0xFE;
    const byte Bom1 = 0xFF;
    const byte Nul = 0x00;
    const byte H = (byte)'H';
    const byte I = (byte)'I';

    [Fact(Timeout = 5000)]
    public async Task AUnicodeStringIsRecognisedByItsByteOrderMark()
    {
        var scanned = await ScanLiteralString(Bom0, Bom1, Nul, H, Nul, I);

        scanned.Symbol.Should().Be(Symbol.UnicodeString);
        scanned.Codes.Should().Equal(H, I);
    }

    [Fact(Timeout = 5000)]
    public async Task AUnicodeStringMissingHalfOfItsLastCharacterEndsInAZero()
    {
        // Five bytes: the mark, one whole character, and the high half of a second with nothing
        // after it. The low half is taken to be zero, so the string ends in U+0000 rather than
        // losing that character or running off the end of itself looking for the byte.
        var scanned = await ScanLiteralString(Bom0, Bom1, Nul, H, Nul);

        scanned.Symbol.Should().Be(Symbol.UnicodeString);
        scanned.Codes.Should().Equal(H, Nul);
    }

    [Fact(Timeout = 5000)]
    public async Task ALittleEndianUnicodeStringIsReadTheOtherWayRound()
    {
        var scanned = await ScanLiteralString(Bom1, Bom0, H, Nul, I, Nul);

        scanned.Symbol.Should().Be(Symbol.UnicodeString);
        scanned.Codes.Should().Equal(H, I);
    }

    [Fact(Timeout = 5000)]
    public async Task ALittleEndianUnicodeStringMissingHalfOfItsLastCharacterKeepsThatCharacter()
    {
        // The same shortfall the other way round loses the *high* half, which is a zero anyway for
        // anything in the Latin range - so that last character survives whole, where the big
        // endian case above gains a U+0000. The two are not symmetrical, and neither drops a byte.
        var scanned = await ScanLiteralString(Bom1, Bom0, H, Nul, I);

        scanned.Symbol.Should().Be(Symbol.UnicodeString);
        scanned.Codes.Should().Equal(H, I);
    }

    [Fact(Timeout = 5000)]
    public async Task AStringWithNoByteOrderMarkIsReadAByteToTheCharacter()
    {
        // The mark is what decides it, so the same bytes without one are not UTF-16 at all and an
        // odd number of them is not short of anything.
        var scanned = await ScanLiteralString(H, I, H);

        scanned.Symbol.Should().Be(Symbol.String);
        scanned.Codes.Should().Equal(H, I, H);
    }

    [Fact(Timeout = 5000)]
    public async Task AByteOrderMarkWithNothingAfterItIsAnEmptyUnicodeString()
    {
        var scanned = await ScanLiteralString(Bom0, Bom1);

        scanned.Symbol.Should().Be(Symbol.UnicodeString);
        scanned.Codes.Should().BeEmpty();
    }

    /// <summary>
    ///   The token as the code of each of its characters. Asserted this way because half of what
    ///   comes back is unprintable, and a failure comparing two strings holding a U+0000 prints
    ///   two things that look identical.
    /// </summary>
    record Scanned(Symbol Symbol, string Token)
    {
        public IReadOnlyList<int> Codes => Token.Select(character => (int)character).ToList();
    }

    /// <summary>Scans those bytes wrapped in the parentheses that make them a literal string.</summary>
    static Task<Scanned> ScanLiteralString(params byte[] contents)
    {
        var pdf = new List<byte> { (byte)'(' };
        pdf.AddRange(contents);
        pdf.Add((byte)')');

        // On a thread of its own, so that the Timeout on these tests can interrupt a scan that
        // does not end. xUnit honours it only on an async test.
        return Interruptibly.Run(() =>
        {
            var lexer = new Lexer(new MemoryStream(pdf.ToArray()));
            var symbol = lexer.ScanNextToken();
            return new Scanned(symbol, lexer.Token);
        });
    }
}
