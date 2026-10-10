using System;
using System.Collections.Generic;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.Filters;

/// <summary>
/// <c>/FlateDecode</c> data is a zlib stream (ISO 32000-1 7.4.4.1 refers to RFC 1950), and a zlib
/// stream ends with the Adler-32 checksum of what was compressed. SharpZipLib's <c>Deflater</c>
/// writes it when asked for the zlib framing, which is what <see cref="FlateDecode.Encode(byte[])"/>
/// asks for. Upstream replaced it with <c>DeflateStream</c>, which writes raw deflate and no
/// checksum, so every stream it wrote was four bytes short - a lenient reader stops at the last
/// deflate block and never notices, and a strict one rejects the stream, which is how a validator
/// came to find no XML in a Factur-X invoice (empira/PDFsharp#397). Nothing here exercised the
/// trailer, so these tests are what would notice the same swap being made in this fork.
/// </summary>
public class FlateEncodeTests
{
    private static readonly FlateDecode Filter = new();

    /// <summary>
    /// The example the issue gives, which is the one Wikipedia's article on Adler-32 works through.
    /// </summary>
    [Test]
    public void TheEncodedDataEndsWithTheAdler32OfWhatWentIn()
    {
        var encoded = Filter.Encode(Encoding.ASCII.GetBytes("Wikipedia"));

        Convert.ToHexString(encoded[^4..]).Should().Be("11E60398",
            "the last four bytes of a zlib stream are the Adler-32 of the data, most significant first");
    }

    [Test]
    public void TheEncodedDataStartsWithAZlibHeader()
    {
        var encoded = Filter.Encode(Encoding.ASCII.GetBytes("Wikipedia"));

        (encoded[0] & 0x0F).Should().Be(8, "the compression method is deflate");
        (((encoded[0] << 8) | encoded[1]) % 31).Should().Be(0,
            "the two header bytes read as a big-endian number are a multiple of 31");
        (encoded[1] & 0x20).Should().Be(0, "no preset dictionary is used");
    }

    /// <summary>
    /// Every length and every mode, against a checksum worked out here rather than read from the
    /// encoder. 5552 bytes is the longest run Adler-32 sums before reducing modulo 65521, so the
    /// lengths either side of it are where an implementation that reduces too late goes wrong.
    /// </summary>
    [Test]
    [Arguments(PdfFlateEncodeMode.Default)]
    [Arguments(PdfFlateEncodeMode.BestCompression)]
    [Arguments(PdfFlateEncodeMode.BestSpeed)]
    public void EveryLengthAndModeEndsWithItsOwnChecksum(PdfFlateEncodeMode mode)
    {
        var wrong = new List<string>();
        var random = new Random(20261009);

        foreach (var length in new[] { 0, 1, 2, 100, 5551, 5552, 5553, 65536, 200_000 })
        {
            var data = new byte[length];
            random.NextBytes(data);

            var encoded = Filter.Encode(data, mode);
            var expected = Adler32(data);
            var written = ((uint)encoded[^4] << 24) | ((uint)encoded[^3] << 16)
                | ((uint)encoded[^2] << 8) | encoded[^1];

            if (written != expected)
                wrong.Add($"{length} bytes: expected {expected:X8}, written {written:X8}");
        }

        wrong.Should().BeEmpty("every stream ends with the Adler-32 of its own data");
    }

    /// <summary>
    /// RFC 1950 section 9, reduced after every byte: slow, and too plain to be wrong.
    /// </summary>
    private static uint Adler32(byte[] data)
    {
        const uint modulus = 65521;
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % modulus;
            b = (b + a) % modulus;
        }

        return (b << 16) | a;
    }
}
