using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
/// A Flate or LZW stream a few kilobytes long can inflate to gigabytes, so a caller that knows how
/// much it expects hands the filter a <see cref="DecodeLimit"/> and the filter stops there.
/// </summary>
/// <remarks>
/// Each decode that could run away goes through <see cref="Interruptibly"/> under a timeout: a cap
/// that failed to stop the inflater would otherwise show as a test host out of memory.
/// </remarks>
public class DecodeLimitTests
{
    private const int Megabyte = 1024 * 1024;

    private static byte[] Deflated(byte[] data) => Deflated(data.Length, i => data[i]);

    private static byte[] Deflated(long length, Func<int, byte> at)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var chunk = new byte[Megabyte];
            for (long done = 0; done < length; done += chunk.Length)
            {
                var count = (int)Math.Min(chunk.Length, length - done);
                for (var i = 0; i < count; i++)
                    chunk[i] = at(i);
                zlib.Write(chunk, 0, count);
            }
        }

        return output.ToArray();
    }

    private static byte[] Counting(int length) =>
        Enumerable.Range(0, length).Select(i => (byte)(i * 31 % 251)).ToArray();

    [Test, Timeout(60_000)]
    public async Task AStreamThatInflatesPastTheCapStopsAtTheCapWithoutHoldingTheRest()
    {
        const long inflated = 256L * Megabyte;
        var bomb = Deflated(inflated, _ => (byte)'0');
        bomb.Length.Should().BeLessThan(2 * Megabyte, "the stream is small and what it expands to is not");
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var bytes = Filtering.FlateDecode.Decode(bomb, new FilterParms(null, limit));
            return (bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)'0');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "the output is held to the cap, not to what the stream says");
    }

    [Test, Timeout(60_000)]
    public async Task ACapEqualToTheLengthIsNotATruncationAndOneByteShortIs()
    {
        var data = Counting(5000);
        var deflated = Deflated(data);

        var exact = new DecodeLimit(data.Length);
        var shortOfIt = new DecodeLimit(data.Length - 1);
        var (whole, cut) = await Interruptibly.Run(() => (
            Filtering.FlateDecode.Decode(deflated, new FilterParms(null, exact)),
            Filtering.FlateDecode.Decode(deflated, new FilterParms(null, shortOfIt))));

        whole.Should().Equal(data);
        exact.Truncated.Should().BeFalse();
        cut.Should().Equal(data.Take(data.Length - 1));
        shortOfIt.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    public async Task ACapOfZeroGivesNothingAndSaysIfThereWasSomething()
    {
        var limit = new DecodeLimit(0);
        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(Deflated(Counting(10)), new FilterParms(null, limit)));

        decoded.Should().BeEmpty();
        limit.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    public async Task WithNoLimitAStreamInflatesWholeAsItAlwaysDid()
    {
        var data = Counting(100_000);
        var deflated = Deflated(data);

        var (noParms, noLimit, large) = await Interruptibly.Run(() => (
            Filtering.FlateDecode.Decode(deflated, (FilterParms)null),
            Filtering.FlateDecode.Decode(deflated, new FilterParms(null)),
            Filtering.FlateDecode.Decode(deflated, new FilterParms(null, new DecodeLimit(long.MaxValue)))));

        noParms.Should().Equal(data);
        noLimit.Should().Equal(data);
        large.Should().Equal(data);
    }

    [Test, Timeout(60_000)]
    public async Task TheCapReachesTheFilterThroughAStreamsFilterEntry()
    {
        var deflated = Deflated(256L * Megabyte / 4, _ => 0);
        var limit = new DecodeLimit(1000);

        var decoded = await Interruptibly.Run(() =>
            Filtering.Decode(deflated, new PdfName("/FlateDecode"), null, limit));

        decoded.Should().HaveCount(1000);
        limit.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    public async Task TheCapReachesTheFilterThroughAStreamsOwnDictionary()
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.CreateStream(Deflated(Counting(5000)));
        dictionary.Elements.SetName("/Filter", "/FlateDecode");
        var limit = new DecodeLimit(100);

        var (limited, whole) = await Interruptibly.Run(() =>
            (dictionary.Stream.GetUnfilteredValue(limit), dictionary.Stream.UnfilteredValue));

        limited.Should().Equal(Counting(5000).Take(100));
        limit.Truncated.Should().BeTrue();
        whole.Should().Equal(Counting(5000));
    }

    [Test, Timeout(60_000)]
    public async Task APredictorIsUndoneOnWhatTheCapLeft()
    {
        // Rows of three bytes behind a filter-type byte of 0 (None). The cap lands inside the
        // third row, and a part row is dropped by the predictor.
        var rows = new byte[] { 0, 1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9 };
        var document = new PdfDocument();
        var parms = new PdfDictionary(document);
        parms.Elements.SetInteger("/Predictor", 15);
        parms.Elements.SetInteger("/Columns", 3);
        var limit = new DecodeLimit(10);

        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(Deflated(rows), new FilterParms(parms, limit)));

        decoded.Should().Equal(1, 2, 3, 4, 5, 6);
        limit.Truncated.Should().BeTrue();
    }

    /// <summary>
    /// A PDF LZW stream at nine bits throughout: a clear code every two hundred literals keeps the
    /// table under the point where the width grows. It does not compress, which does not matter
    /// to what the cap counts.
    /// </summary>
    private static byte[] LiteralLzw(byte[] data)
    {
        var codes = new System.Collections.Generic.List<int> { 256 };
        for (var i = 0; i < data.Length; i++)
        {
            codes.Add(data[i]);
            if (i % 200 == 199)
                codes.Add(256);
        }

        codes.Add(257);

        var output = new System.Collections.Generic.List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var code in codes)
        {
            buffer = (buffer << 9) | code;
            bits += 9;
            while (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
                buffer &= (1 << bits) - 1;
            }
        }

        if (bits > 0)
            output.Add((byte)(buffer << (8 - bits)));
        return output.ToArray();
    }

    [Test, Timeout(60_000)]
    public async Task AnLzwStreamIsCutAtTheCapAndWholeWithoutOne()
    {
        var data = Counting(1000);
        var lzw = LiteralLzw(data);
        var limit = new DecodeLimit(450);

        var (whole, cut) = await Interruptibly.Run(() => (
            Filtering.LzwDecode.Decode(lzw, (FilterParms)null),
            Filtering.LzwDecode.Decode(lzw, new FilterParms(null, limit))));

        whole.Should().Equal(data);
        cut.Should().Equal(data.Take(450));
        limit.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    public async Task AnLzwStreamCutExactlyAtItsEndIsNotTruncated()
    {
        var data = Counting(1000);
        var limit = new DecodeLimit(data.Length);

        var decoded = await Interruptibly.Run(() =>
            Filtering.LzwDecode.Decode(LiteralLzw(data), new FilterParms(null, limit)));

        decoded.Should().Equal(data);
        limit.Truncated.Should().BeFalse();
    }
}
