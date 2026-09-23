using System;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Filters;
using Xunit;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
///   The two filters that make a stream of bytes printable: ASCIIHexDecode, which spends two
///   characters per byte, and ASCII85Decode, which spends five per four and so costs a quarter
///   rather than double. Both are decoders by name and encoders as well in practice, because
///   PDFsharp writes streams as well as reading them, and the property that matters for both is
///   that the two directions agree.
///   <para>
///   ASCII85 is the one worth testing exhaustively. Its encoder has a separate arm for a trailing
///   group of one, two and three bytes, its decoder has a matching arm for each, and the decoder's
///   arms carry a correction its own author could not derive - "in some rare cases the value must
///   not be increased by one, but I cannot found [sic] a general formula or a proof". Round-tripping
///   every length settles all of it at once.
///   </para>
/// </summary>
public class AsciiFilterTests
{
    /// <summary>
    ///   Bytes that are the same on every run and every machine, and that avoid being accidentally
    ///   well-behaved: they span the whole byte range and are not periodic in four.
    /// </summary>
    private static byte[] Bytes(int count)
    {
        var data = new byte[count];
        for (var idx = 0; idx < count; idx++)
            data[idx] = (byte)((idx * 37 + 11) % 256);
        return data;
    }

    public static TheoryData<int> EveryLengthUpTo(int count)
    {
        var data = new TheoryData<int>();
        for (var length = 0; length <= count; length++)
            data.Add(length);
        return data;
    }

    public static TheoryData<int> Lengths => EveryLengthUpTo(24);

    // ----- ASCII85 -------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Lengths))]
    public void Ascii85CarriesAnyNumberOfBytesThereAndBack(int length)
    {
        // Four bytes become five characters, and a trailing group of one, two or three becomes two,
        // three or four - so every length mod 4 takes a different arm through both directions.
        var original = Bytes(length);

        var encoded = Filtering.ASCII85Decode.Encode(original);
        var decoded = Filtering.ASCII85Decode.Decode(encoded, (FilterParms)null);

        decoded.Should().Equal(original);
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void Ascii85CarriesRunsOfZeroBytesThereAndBack(int length)
    {
        // Four zero bytes are written as the single character z rather than as five exclamation
        // marks, which is a branch of its own in both directions and the only one where the
        // character count does not follow from the byte count.
        var original = new byte[length];

        var encoded = Filtering.ASCII85Decode.Encode(original);
        var decoded = Filtering.ASCII85Decode.Decode(encoded, (FilterParms)null);

        decoded.Should().Equal(original);
    }

    /// <summary>
    ///   The lengths at which the decoder's main loop used to stop one group early.
    /// </summary>
    /// <remarks>
    ///   The loop ran <c>while (idx + 4 &lt; length)</c>, which assumes every turn of it consumes
    ///   five characters, and a <c>z</c> consumes one. A stream using the shortcut therefore left
    ///   the loop with characters still to read, and the code that reads the trailing group read
    ///   the <c>z</c> as part of it. It bit at 4n+1 and 4n+2 bytes, where the tail is a group of
    ///   two or three; a group of four happened to leave the loop in the right place. The encoder
    ///   in this same class writes those streams, so PDFsharp could not read back what PDFsharp
    ///   wrote, and zero runs are what image and embedded-file data is full of.
    /// </remarks>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(14)]
    public void Ascii85CarriesARunOfZeroBytesThatEndsInAPartialGroup(int length)
    {
        var original = new byte[length];

        var encoded = Filtering.ASCII85Decode.Encode(original);
        var decoded = Filtering.ASCII85Decode.Decode(encoded, (FilterParms)null);

        decoded.Should().Equal(original);
    }

    [Fact]
    public void Ascii85ReadsAZeroRunWhereverItFalls()
    {
        // The shortcut is taken wherever four zero bytes land on a group boundary, not only at the
        // start, so every combination of run length, run position and total length has to come
        // back with the bytes on either side of the run where they belong.
        for (var length = 0; length <= 24; length++)
        {
            for (var start = 0; start < length; start++)
            {
                for (var run = 1; start + run <= length; run++)
                {
                    var original = Bytes(length);
                    Array.Clear(original, start, run);

                    var encoded = Filtering.ASCII85Decode.Encode(original);
                    var decoded = Filtering.ASCII85Decode.Decode(encoded, (FilterParms)null);

                    decoded.Should().Equal(original,
                        "{0} zero bytes from {1} of {2} should survive", run, start, length);
                }
            }
        }
    }

    [Fact]
    public void Ascii85WritesFourZeroBytesAsOneCharacter()
    {
        var encoded = Filtering.ASCII85Decode.Encode(new byte[8]);

        Encoding.ASCII.GetString(encoded).Should().Be("zz~>");
    }

    [Fact]
    public void Ascii85WritesNothingAsTheEndMarkerAlone()
    {
        Encoding.ASCII.GetString(Filtering.ASCII85Decode.Encode([])).Should().Be("~>");
        Filtering.ASCII85Decode.Decode("~>"u8.ToArray(), (FilterParms)null).Should().BeEmpty();
    }

    [Fact]
    public void Ascii85SpendsFiveCharactersOnFourBytesAndTwoMoreOnTheEndMarker()
    {
        Filtering.ASCII85Decode.Encode(Bytes(4)).Should().HaveCount(5 + 2);
        Filtering.ASCII85Decode.Encode(Bytes(8)).Should().HaveCount(10 + 2);
        Filtering.ASCII85Decode.Encode(Bytes(5)).Should().HaveCount(5 + 2 + 2);
        Filtering.ASCII85Decode.Encode(Bytes(6)).Should().HaveCount(5 + 3 + 2);
        Filtering.ASCII85Decode.Encode(Bytes(7)).Should().HaveCount(5 + 4 + 2);
    }

    [Fact]
    public void Ascii85SkipsCharactersThatAreNotPartOfTheEncoding()
    {
        // A writer is free to break the stream into lines, so the decoder ignores anything outside
        // its own alphabet rather than refusing the stream.
        var original = Bytes(12);
        var encoded = Encoding.ASCII.GetString(Filtering.ASCII85Decode.Encode(original));
        var broken = Encoding.ASCII.GetBytes(
            encoded[..5] + "\r\n" + encoded.Substring(5, 5) + "\n" + encoded[10..]);

        Filtering.ASCII85Decode.Decode(broken, (FilterParms)null).Should().Equal(original);
    }

    [Fact]
    public void Ascii85RefusesAStreamThatNeverEnds()
    {
        var act = () => Filtering.ASCII85Decode.Decode("<+oue"u8.ToArray(), (FilterParms)null);

        act.Should().Throw<ArgumentException>("the end-of-data marker is what says the stream is whole");
    }

    [Fact]
    public void Ascii85RefusesAnEndMarkerThatIsNotOne()
    {
        var act = () => Filtering.ASCII85Decode.Decode("<+oue~x"u8.ToArray(), (FilterParms)null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ascii85RefusesATrailingGroupOfOneCharacter()
    {
        // A group of one carries no whole byte, so a stream ending in one was mis-encoded.
        var act = () => Filtering.ASCII85Decode.Decode("<+oue!~>"u8.ToArray(), (FilterParms)null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ascii85RefusesAGroupThatWouldNotFitInFourBytes()
    {
        // "uuuuu" is 85^4 x 84 + ... which overflows the four bytes a group stands for.
        var act = () => Filtering.ASCII85Decode.Decode("uuuuu~>"u8.ToArray(), (FilterParms)null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("!!z!!~>")]
    [InlineData("<+oue!z~>")]
    public void Ascii85RefusesAZeroGroupInsideAnotherGroup(string encoded)
    {
        // A z stands for a whole group of zeros, so it can only begin one. Inside a group it used
        // to be read as a digit worth 89 and the rest of the stream decoded out of step.
        var act = () => Filtering.ASCII85Decode.Decode(Encoding.ASCII.GetBytes(encoded), (FilterParms)null);

        act.Should().Throw<ArgumentException>().WithMessage("*'z'*");
    }

    [Fact]
    public void Ascii85ReadsAZeroGroupBetweenWholeGroups()
    {
        var original = new byte[] { 1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8 };
        var encoded = Filtering.ASCII85Decode.Encode(original);

        Encoding.ASCII.GetString(encoded).Should().Contain("z");
        Filtering.ASCII85Decode.Decode(encoded, (FilterParms)null).Should().Equal(original);
    }

    [Fact]
    public void Ascii85DecodingCompactsTheBufferItWasGivenRatherThanACopyOfIt()
    {
        // The decoder squeezes the characters it keeps down to the front of the caller's array
        // before decoding them, so a stream that had anything to skip comes back changed. Worth
        // knowing before handing it a buffer somebody else still holds, or decoding one twice.
        var encoded = Filtering.ASCII85Decode.Encode(Bytes(8));
        var withLineBreaks = Encoding.ASCII.GetBytes(
            Encoding.ASCII.GetString(encoded).Insert(5, "\r\n"));
        var copy = (byte[])withLineBreaks.Clone();

        Filtering.ASCII85Decode.Decode(withLineBreaks, (FilterParms)null).Should().Equal(Bytes(8));

        withLineBreaks.Should().NotEqual(copy);
    }

    // ----- ASCIIHex ------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Lengths))]
    public void AsciiHexCarriesAnyNumberOfBytesThereAndBack(int length)
    {
        var original = Bytes(length);

        var encoded = Filtering.ASCIIHexDecode.Encode(original);
        var decoded = Filtering.ASCIIHexDecode.Decode(encoded, (FilterParms)null);

        encoded.Should().HaveCount(2 * length, "hex spends two characters on every byte");
        decoded.Should().Equal(original);
    }

    [Fact]
    public void AsciiHexWritesTheDigitsInUpperCase()
    {
        Encoding.ASCII.GetString(Filtering.ASCIIHexDecode.Encode([0x00, 0x0F, 0xA5, 0xFF]))
            .Should().Be("000FA5FF");
    }

    [Fact]
    public void AsciiHexReadsLowerCaseDigitsToo()
    {
        Filtering.ASCIIHexDecode.Decode("00afA5ff"u8.ToArray(), (FilterParms)null)
            .Should().Equal(new byte[] { 0x00, 0xAF, 0xA5, 0xFF });
    }

    [Fact]
    public void AsciiHexIgnoresTheWhiteSpaceAWriterBreaksTheStreamWith()
    {
        // Null, tab, line feed, form feed, carriage return and space are all white space to a
        // content stream, and the filter drops the lot before reading digits.
        var withSpace = "41 42\t43\r\n44\f45"u8.ToArray() .Concat(new byte[] { 0 }).ToArray();

        Filtering.ASCIIHexDecode.Decode(withSpace, (FilterParms)null)
            .Should().Equal("ABCDE"u8.ToArray());
    }

    [Fact]
    public void AsciiHexStopsAtTheEndOfDataMarker()
    {
        Filtering.ASCIIHexDecode.Decode("414243>"u8.ToArray(), (FilterParms)null)
            .Should().Equal("ABC"u8.ToArray());
    }

    [Fact]
    public void AsciiHexDecodesNothingFromNothing()
    {
        Filtering.ASCIIHexDecode.Decode([], (FilterParms)null).Should().BeEmpty();
        Filtering.ASCIIHexDecode.Decode(">"u8.ToArray(), (FilterParms)null).Should().BeEmpty();
    }

    /// <summary>
    ///   The reference is explicit: if the filter reaches the end of data having read an odd number
    ///   of hexadecimal digits, it must behave as though a <c>0</c> digit followed the last one, so
    ///   "4" is 0x40. Lengthening the byte array alone pads with the byte 0x00 rather than the
    ///   character '0', and 0x00 goes through the digit arithmetic as -48, which used to leave
    ///   every odd-length stream ending in a byte 48 too small.
    /// </summary>
    [Theory]
    [InlineData("4", 0x40)]
    [InlineData("41424", 0x40)]
    [InlineData("F", 0xF0)]
    [InlineData("414243F>", 0xF0)]
    public void AsciiHexTreatsAMissingLastDigitAsZero(string hex, int expected)
    {
        var decoded = Filtering.ASCIIHexDecode.Decode(Encoding.ASCII.GetBytes(hex), (FilterParms)null);

        decoded.Last().Should().Be((byte)expected);
    }

    [Fact]
    public void AsciiHexReadsNothingAfterTheEndOfDataMarkerWhereverItComes()
    {
        // The marker ends the data wherever it is, so what follows it - even what is not hex at
        // all - is not read. It used to be recognised only as the very last character.
        Filtering.ASCIIHexDecode.Decode("4142>4344 not hex"u8.ToArray(), (FilterParms)null)
            .Should().Equal("AB"u8.ToArray());
    }

    [Fact]
    public void AsciiHexTreatsAMissingLastDigitAsZeroWhenTheMarkerComesMidStream()
    {
        Filtering.ASCIIHexDecode.Decode("41 4>43"u8.ToArray(), (FilterParms)null)
            .Should().Equal(0x41, 0x40);
    }

    [Theory]
    [InlineData("41G2")]
    [InlineData("41-42")]
    [InlineData("<4142>")]
    [InlineData("41\u000B42")]   // vertical tab is not white space to PDF
    public void AsciiHexRefusesACharacterThatIsNeitherADigitNorWhiteSpace(string hex)
    {
        // "Any other characters shall cause an error." They used to go through the digit
        // arithmetic as though they were digits and come out as whatever byte that made.
        var act = () => Filtering.ASCIIHexDecode.Decode(Encoding.ASCII.GetBytes(hex), (FilterParms)null);

        act.Should().Throw<ArgumentException>().WithMessage("*Illegal character*");
    }

    [Fact]
    public void AsciiHexLeavesTheBufferItWasGivenAsItWas()
    {
        var withSpace = "41 42"u8.ToArray();
        var copy = (byte[])withSpace.Clone();

        Filtering.ASCIIHexDecode.Decode(withSpace, (FilterParms)null);

        withSpace.Should().Equal(copy);
    }

    // ----- what every filter has in common -------------------------------------------------------

    [Fact]
    public void AFilterEncodesAStringByItsBytesAndDecodesBackToTheSameString()
    {
        const string text = "Hello, filter.";

        var encoded = Filtering.ASCIIHexDecode.Encode(text);

        Filtering.ASCIIHexDecode.DecodeToString(encoded).Should().Be(text);
        Filtering.ASCIIHexDecode.DecodeToString(encoded, null).Should().Be(text);
    }

    [Fact]
    public void EveryFilterRefusesToWorkOnNothingAtAll()
    {
        var hexEncode = () => Filtering.ASCIIHexDecode.Encode((byte[])null);
        var hexDecode = () => Filtering.ASCIIHexDecode.Decode(null, (FilterParms)null);
        var a85Encode = () => Filtering.ASCII85Decode.Encode((byte[])null);
        var a85Decode = () => Filtering.ASCII85Decode.Decode(null, (FilterParms)null);

        hexEncode.Should().Throw<ArgumentNullException>();
        hexDecode.Should().Throw<ArgumentNullException>();
        a85Encode.Should().Throw<ArgumentNullException>();
        a85Decode.Should().Throw<ArgumentNullException>();
    }
}
