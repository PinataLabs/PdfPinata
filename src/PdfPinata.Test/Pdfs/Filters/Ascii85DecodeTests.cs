using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Filters;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.Filters;

/// <summary>
/// ASCII85 packs four bytes into five printable characters, so everything interesting happens at
/// the end of the data, where a group is short and the decoder has to work out how many bytes a
/// partial group stands for. The comment above that code says the author found no general formula
/// and tested all the cases programmatically; the tests he ran are not in the repository, so the
/// round trip below is what is left to hold the arithmetic in place.
/// </summary>
public class Ascii85DecodeTests
{
    private static readonly Ascii85Decode Filter = new();

    private static byte[] Decode(byte[] encoded)
    {
        return Filter.Decode(encoded, (FilterParms)null);
    }

    /// <summary>
    /// Encoding and decoding every length from nothing to two full groups and a bit covers all
    /// four endings — an exact multiple of four bytes, and one, two or three left over — with
    /// data that is not all the same byte.
    /// </summary>
    [Test]
    public void DataOfAnyLengthComesBackAsItself()
    {
        var wrong = new List<string>();
        var random = new Random(20260815);

        for (var length = 0; length <= 40; length++)
        {
            var original = new byte[length];
            random.NextBytes(original);

            var decoded = Decode(Filter.Encode(original));

            if (!decoded.SequenceEqual(original))
                wrong.Add($"{length} bytes came back as {decoded.Length}");
        }

        wrong.Should().BeEmpty("every length survives the round trip");
    }

    /// <summary>
    /// The high bytes are where the "increase by one" correction in the decoder applies, so a
    /// partial group of large values is worth running separately from random data.
    /// </summary>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    public void APartialGroupOfHighBytesComesBackAsItself(int length)
    {
        var original = Enumerable.Repeat((byte)0xFF, length).ToArray();

        Decode(Filter.Encode(original)).Should().Equal(original);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(8)]
    public void ARunOfZerosComesBackAsItself(int length)
    {
        var original = new byte[length];

        Decode(Filter.Encode(original)).Should().Equal(original);
    }

    /// <summary>
    /// A group of four zero bytes is written as a single 'z' rather than as five '!'s, and the
    /// decoder has to expand it again. This is the one place where one input character stands for
    /// four output bytes, and the byte count is worked out from a separate count of them.
    /// </summary>
    [Test]
    public void AZeroGroupIsWrittenAsZAndExpandedAgain()
    {
        var fourZeros = new byte[4];

        var encoded = Filter.Encode(fourZeros);

        Encoding.ASCII.GetString(encoded).Should().Be("z~>");
        Decode(encoded).Should().Equal(fourZeros);
    }

    [Test]
    public void ZeroGroupsMixedWithDataComeBackInTheRightPlaces()
    {
        byte[] original = [1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8];

        var encoded = Filter.Encode(original);

        Encoding.ASCII.GetString(encoded).Should().Contain("z");
        Decode(encoded).Should().Equal(original);
    }

    [Test]
    public void NothingEncodesToTheEndMarkerAloneAndDecodesBackToNothing()
    {
        var encoded = Filter.Encode([]);

        Encoding.ASCII.GetString(encoded).Should().Be("~>");
        Decode(encoded).Should().BeEmpty();
    }

    /// <summary>
    /// Real PDFs wrap the encoded text at some line length, so anything that is not a valid digit
    /// has to be stepped over rather than decoded. The decoder ignores it silently — there is no
    /// validity check beyond the end marker.
    /// </summary>
    private const string Text = "Hello, World!";

    /// <summary>
    /// The encoding of <see cref="Text"/>, taken from the encoder rather than written out here,
    /// so that these tests say what the decoder tolerates rather than restating the digits.
    /// </summary>
    private static string Encoded()
    {
        return Encoding.ASCII.GetString(Filter.Encode(Encoding.ASCII.GetBytes(Text)));
    }

    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    [Arguments(" ")]
    [Arguments("\t")]
    [Arguments("\0")]
    public void WhiteSpaceInsideTheDataIsSteppedOver(string inserted)
    {
        var encoded = Encoded();
        var broken = encoded.Insert(encoded.Length / 2, inserted);

        var decoded = Decode(Encoding.ASCII.GetBytes(broken));

        Encoding.ASCII.GetString(decoded).Should().Be(Text);
    }

    [Test]
    public void TheSameTextDecodesWithNoWhiteSpaceAtAll()
    {
        var decoded = Decode(Encoding.ASCII.GetBytes(Encoded()));

        Encoding.ASCII.GetString(decoded).Should().Be(Text);
    }

    /// <summary>
    /// The end marker is the only thing that stops the scan, so data without one is rejected
    /// rather than decoded as far as it goes.
    /// </summary>
    [Test]
    public void DataWithNoEndMarkerIsRejected()
    {
        var withoutMarker = Encoded().Replace("~>", "");

        Action decode = () => Decode(Encoding.ASCII.GetBytes(withoutMarker));

        decode.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ATildeNotFollowedByAngleBracketIsRejected()
    {
        var wrongMarker = Encoded().Replace("~>", "~x");

        Action decode = () => Decode(Encoding.ASCII.GetBytes(wrongMarker));

        decode.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Data that stops between the two characters of the end marker is malformed in the same way
    /// as data that spells them wrongly, and has to be refused the same way. The second character
    /// was read without checking there was one, so a stream truncated here came back as an index
    /// out of range rather than as a complaint about the data.
    /// </summary>
    [Test]
    public void ATildeThatEndsTheDataIsRejected()
    {
        var truncated = Encoded().Replace("~>", "~");

        Action decode = () => Decode(Encoding.ASCII.GetBytes(truncated));

        decode.Should().Throw<ArgumentException>();
    }

    [Test]
    public void NothingButATildeIsRejected()
    {
        Action decode = () => Decode("~"u8.ToArray());

        decode.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// A single character left over encodes nothing — one character carries under seven bits, and
    /// a group of two is the shortest that can stand for a byte.
    /// </summary>
    [Test]
    public void AGroupOfOneCharacterIsRejected()
    {
        Action decode = () => Decode("87cUR!~>"u8.ToArray());

        decode.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Five characters can address more than four bytes can hold, and the excess is caught rather
    /// than truncated into a wrong value.
    /// </summary>
    [Test]
    public void AGroupTooLargeForFourBytesIsRejected()
    {
        Action decode = () => Decode("uuuuu~>"u8.ToArray());

        decode.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// The largest value four bytes can hold is right at that boundary and has to be accepted.
    /// </summary>
    [Test]
    public void TheLargestGroupThatFitsIsAccepted()
    {
        byte[] original = [0xFF, 0xFF, 0xFF, 0xFF];

        var encoded = Filter.Encode(original);

        Encoding.ASCII.GetString(encoded).Should().Be("s8W-!~>");
        Decode(encoded).Should().Equal(original);
    }

    [Test]
    public void EncodingNothingIsNotTheSameAsEncodingNull()
    {
        Action encode = () => Filter.Encode((byte[])null);
        Action decode = () => Filter.Decode(null, (FilterParms)null);

        encode.Should().Throw<ArgumentNullException>();
        decode.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// The decoder compacts the data in place before decoding it, so the array handed in comes
    /// back changed and cannot be decoded twice. Callers that keep the encoded bytes for anything
    /// else have to copy them first.
    /// </summary>
    [Test]
    public void DecodingWritesOverTheArrayItWasGiven()
    {
        var withSpace = Encoded().Insert(3, " ");
        var encoded = Encoding.ASCII.GetBytes(withSpace);
        var asHandedIn = encoded.ToArray();

        Decode(encoded);

        encoded.Should().NotEqual(asHandedIn, "the white space is squeezed out of the array in place");
    }
}
