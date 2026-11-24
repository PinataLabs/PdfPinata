using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

    private static PdfArray Chain(PdfDocument document, params string[] filters)
    {
        var chain = new PdfArray(document);
        foreach (var filter in filters)
            chain.Elements.Add(new PdfName(filter));
        return chain;
    }

    private static PdfDictionary Predictor(PdfDocument document, int predictor, int columns)
    {
        var parms = new PdfDictionary(document);
        parms.Elements.SetInteger("/Predictor", predictor);
        parms.Elements.SetInteger("/Columns", columns);
        return parms;
    }

    [Test, Timeout(60_000)]
    public async Task ARunLengthStreamBehindFlateIsHeldToTheCapWhileItIsBuilt()
    {
        // Pairs of 0x81 and a space, 128 spaces each: 31 MiB of them is two gigabytes of spaces.
        var bomb = Deflated(31L * Megabyte, i => i % 2 == 0 ? (byte)0x81 : (byte)' ');
        bomb.Length.Should().BeLessThan(Megabyte);
        var limit = new DecodeLimit(Megabyte);
        var chain = Chain(new PdfDocument(), "/FlateDecode", "/RunLengthDecode");

        var (decoded, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var bytes = Filtering.Decode(bomb, chain, null, limit);
            return (bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)' ');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "RunLength builds no more than the cap");
    }

    /// <summary>
    /// What a chain of filters decodes to under the limit, and what decoding it allocated on its
    /// own thread: the input is built before, so only the decode is counted.
    /// </summary>
    private static Task<(byte[] Decoded, long Allocated)> DecodedUnder(DecodeLimit limit, byte[] encoded, params string[] filters)
    {
        PdfItem chain = filters.Length == 1 ? new PdfName(filters[0]) : Chain(new PdfDocument(), filters);
        return Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var bytes = Filtering.Decode(encoded, chain, null, limit);
            return (bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        });
    }

    private static byte[] Repeated(string text, long count)
    {
        var bytes = new byte[text.Length * count];
        for (long at = 0; at < bytes.Length; at++)
            bytes[at] = (byte)text[(int)(at % text.Length)];
        return bytes;
    }

    /// <summary>RunLength pairs of 0x81 and a space, each 128 spaces: 64 times what it is written in.</summary>
    private static byte[] SpaceRuns(long pairs) => Repeated("\u0081 ", pairs);

    /// <summary>
    /// PDF LZW with the default <c>/EarlyChange</c> of 1, growing its codes from 9 to 12 bits and
    /// clearing the table before it fills, so that a repetitive stream compresses as a writer's would.
    /// </summary>
    private static byte[] Lzw(byte[] data)
    {
        var output = new MemoryStream();
        long buffer = 0;
        var bits = 0;
        var width = 9;

        void Emit(int code)
        {
            buffer = (buffer << width) | (uint)code;
            bits += width;
            while (bits >= 8)
            {
                output.WriteByte((byte)(buffer >> (bits - 8)));
                bits -= 8;
                buffer &= (1L << bits) - 1;
            }
        }

        var table = new System.Collections.Generic.Dictionary<int, int>();
        var next = 258;
        Emit(256);
        var prefix = -1;
        foreach (var symbol in data)
        {
            if (prefix < 0)
            {
                prefix = symbol;
                continue;
            }

            if (table.TryGetValue(prefix << 8 | symbol, out var found))
            {
                prefix = found;
                continue;
            }

            Emit(prefix);
            table[prefix << 8 | symbol] = next++;
            width = next switch { 512 => 10, 1024 => 11, 2048 => 12, _ => width };
            if (next >= 4094)
            {
                Emit(256);
                table.Clear();
                next = 258;
                width = 9;
            }

            prefix = symbol;
        }

        if (prefix >= 0)
            Emit(prefix);
        Emit(257);
        if (bits > 0)
            output.WriteByte((byte)(buffer << (8 - bits)));
        return output.ToArray();
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task AnAscii85StreamOfZsIsHeldToTheCapWhileItIsBuilt()
    {
        // Each z is four zeros: 256 MiB of them.
        var bomb = Encoding.ASCII.GetBytes(new string('z', 64 * Megabyte) + "~>");
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/ASCII85Decode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == 0);
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "ASCII85 builds no more than the cap");
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task AnAsciiHexStreamIsHeldToTheCapWhileItIsBuilt()
    {
        var bomb = Repeated("41", 32L * Megabyte);
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/ASCIIHexDecode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)'A');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "ASCIIHex builds no more than the cap");
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task ARunLengthStreamAloneIsHeldToTheCapWhileItIsBuilt()
    {
        // 512 MiB of spaces.
        var bomb = SpaceRuns(4L * Megabyte);
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/RunLengthDecode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)' ');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "RunLength builds no more than the cap");
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task ARunLengthStreamBehindLzwIsHeldToTheCapWhileItIsBuilt()
    {
        // 16 MiB of runs, two gigabytes of spaces, in well under a megabyte of LZW.
        var bomb = Lzw(SpaceRuns(8L * Megabyte));
        bomb.Length.Should().BeLessThan(Megabyte);
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/LZWDecode", "/RunLengthDecode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)' ');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "neither filter builds more than the cap");
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task ARunLengthStreamBehindFlateBehindAscii85IsHeldToTheCapWhileItIsBuilt()
    {
        var bomb = Filtering.ASCII85Decode.Encode(Deflated(31L * Megabyte, i => i % 2 == 0 ? (byte)0x81 : (byte)' '));
        bomb.Length.Should().BeLessThan(Megabyte);
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/ASCII85Decode", "/FlateDecode", "/RunLengthDecode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == (byte)' ');
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "no filter of the three builds more than the cap");
    }

    [Test, Timeout(60_000), NotInParallel]
    public async Task AnAscii85StreamBehindFlateIsHeldToTheCapWhileItIsBuilt()
    {
        // 256 MiB of z, which is a gigabyte of zeros.
        var bomb = Deflated(256L * Megabyte, _ => (byte)'z');
        bomb.Length.Should().BeLessThan(Megabyte);
        var limit = new DecodeLimit(Megabyte);

        var (decoded, allocated) = await DecodedUnder(limit, bomb, "/FlateDecode", "/ASCII85Decode");

        decoded.Should().HaveCount(Megabyte);
        decoded.Should().OnlyContain(b => b == 0);
        limit.Truncated.Should().BeTrue();
        allocated.Should().BeLessThan(16L * Megabyte, "neither filter builds more than the cap");
    }

    [Test]
    public async Task ARunLengthStreamIsCutAtTheCapInsideARunAndNotWhenItEndsThere()
    {
        byte[] Stream() => [0x81, (byte)'a', 1, (byte)'x', (byte)'y', 128];
        var cut = new DecodeLimit(129);
        var exact = new DecodeLimit(130);

        var (partial, whole) = await Interruptibly.Run(() => (
            Filtering.RunLengthDecode.Decode(Stream(), new FilterParms(null, cut)),
            Filtering.RunLengthDecode.Decode(Stream(), new FilterParms(null, exact))));

        partial.Should().Equal(Enumerable.Repeat((byte)'a', 128).Append((byte)'x'));
        cut.Truncated.Should().BeTrue();
        whole.Should().HaveCount(130);
        exact.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task AnAscii85StreamOfZsIsCutAtTheCapAndAWholeOneIsNot()
    {
        byte[] Zeros() => Encoding.ASCII.GetBytes(new string('z', 1000) + "~>");
        byte[] HelloWorld() => Encoding.ASCII.GetBytes("87cURD]i,\"Ebo7~>");
        var zeros = new DecodeLimit(98);
        var hello = new DecodeLimit(5);
        var whole = new DecodeLimit(11);

        var (zeroed, five, all) = await Interruptibly.Run(() => (
            Filtering.ASCII85Decode.Decode(Zeros(), new FilterParms(null, zeros)),
            Filtering.ASCII85Decode.Decode(HelloWorld(), new FilterParms(null, hello)),
            Filtering.ASCII85Decode.Decode(HelloWorld(), new FilterParms(null, whole))));

        zeroed.Should().Equal(new byte[98]);
        zeros.Truncated.Should().BeTrue();
        Encoding.ASCII.GetString(five).Should().Be("Hello");
        hello.Truncated.Should().BeTrue();
        Encoding.ASCII.GetString(all).Should().Be("Hello World");
        whole.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task AnAsciiHexStreamIsCutAtTheCapAndAWholeOneIsNot()
    {
        byte[] Hex(string text) => Encoding.ASCII.GetBytes(text);
        var three = new DecodeLimit(3);
        var five = new DecodeLimit(5);
        var oddTail = new DecodeLimit(2);

        var (cut, whole, tail) = await Interruptibly.Run(() => (
            Filtering.ASCIIHexDecode.Decode(Hex("48656C6C6F>"), new FilterParms(null, three)),
            Filtering.ASCIIHexDecode.Decode(Hex("48656C6C6F>"), new FilterParms(null, five)),
            Filtering.ASCIIHexDecode.Decode(Hex("4865 6>"), new FilterParms(null, oddTail))));

        Encoding.ASCII.GetString(cut).Should().Be("Hel");
        three.Truncated.Should().BeTrue();
        Encoding.ASCII.GetString(whole).Should().Be("Hello");
        five.Truncated.Should().BeFalse();
        tail.Should().Equal(0x48, 0x65);
        oddTail.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    public async Task ATiffPredictorDropsAPartRowWhereTheCapCutTheStream()
    {
        // Three rows of three samples as differences; the cap lands inside the third.
        var rows = new byte[] { 1, 1, 1, 2, 2, 2, 3, 3, 3 };
        var parms = Predictor(new PdfDocument(), 2, 3);
        var limit = new DecodeLimit(7);

        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(Deflated(rows), new FilterParms(parms, limit)));

        decoded.Should().Equal(1, 2, 3, 2, 4, 6);
        limit.Truncated.Should().BeTrue();
    }

    [Test, Timeout(60_000)]
    [Arguments(2, new byte[] { 1, 1, 1, 2, 2, 2, 3, 3 })]
    [Arguments(15, new byte[] { 1, 1, 1, 1, 2, 1, 2, 3, 2, 1, 1 })]
    public async Task APredictorDropsAPartRowWhereDamageCutTheStream(int predictor, byte[] rows)
    {
        // Two whole rows of three samples and the start of a third, the stream ending in a wrong
        // checksum.
        var parms = Predictor(new PdfDocument(), predictor, 3);
        var limit = new DecodeLimit(1_000) { TolerateDamage = true };
        var damaged = Deflated(rows);
        damaged[^1] ^= 0xFF;

        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(damaged, new FilterParms(parms, limit)));

        decoded.Should().Equal([1, 2, 3, 2, 4, 6], "a part row's differences cannot be undone, and would be drawn as colours");
        limit.Damaged.Should().BeTrue();
        limit.Truncated.Should().BeFalse("damage is not the cap");
    }

    [Test, Timeout(60_000)]
    public async Task ATiffPredictorWithNoLimitStillLeavesAPartRowAsItWas()
    {
        var parms = Predictor(new PdfDocument(), 2, 3);

        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(Deflated(new byte[] { 1, 1, 1, 2, 2 }), new FilterParms(parms)));

        decoded.Should().Equal(1, 2, 3, 2, 2);
    }

    [Test, Timeout(60_000)]
    public async Task APngPredictorDropsAPartRowWithNoLimit()
    {
        var parms = Predictor(new PdfDocument(), 15, 3);
        var rows = new byte[] { 0, 1, 2, 3, 0, 4, 5 };

        var decoded = await Interruptibly.Run(() =>
            Filtering.FlateDecode.Decode(Deflated(rows), new FilterParms(parms)));

        decoded.Should().Equal(1, 2, 3);
    }

    [Test, Timeout(60_000)]
    public async Task AFlateStreamInsideACutFlateStreamReturnsWhatItInflatedAndSaysItWasCut()
    {
        var random = new Random(3);
        var payload = new byte[200_000];
        random.NextBytes(payload);
        var inner = Deflated(payload);
        var twice = Deflated(inner);
        var limit = new DecodeLimit(50_000);
        var chain = Chain(new PdfDocument(), "/FlateDecode", "/FlateDecode");

        var decoded = await Interruptibly.Run(() => Filtering.Decode(twice, chain, null, limit));

        limit.Truncated.Should().BeTrue();
        decoded.Should().NotBeEmpty();
        decoded.Length.Should().BeLessThanOrEqualTo(50_000);
        decoded.Should().Equal(payload.Take(decoded.Length));
    }

    [Test, Timeout(60_000)]
    public async Task AnAscii85StreamCutByAnEarlierFilterGivesTheWholeGroupsBeforeTheCut()
    {
        var payload = Counting(1000);
        var encoded = Deflated(Filtering.ASCII85Decode.Encode(payload));
        // 503 characters of ASCII85: a hundred groups, and three of the next one.
        var limit = new DecodeLimit(503);

        var (decoded, _) = await DecodedUnder(limit, encoded, "/FlateDecode", "/ASCII85Decode");

        limit.Truncated.Should().BeTrue();
        decoded.Should().Equal(payload.Take(400), "a group cut part way is left out, not read as the last one");
    }

    // 1002 bytes are 250 groups and three characters standing for the last two bytes, then the
    // end marker at 1253 and 1254, and a comment after it for the cap to fall in. A cut after the
    // marker or between its two characters has the whole stream, last part group included
    // (ISO 32000-1 7.4.3).
    [Test, Timeout(60_000)]
    [Arguments(1255 + 4)]
    [Arguments(1255 - 1)]
    public async Task AnAscii85StreamCutAtOrAfterItsEndMarkerGivesItsLastPartGroup(int cap)
    {
        var payload = Counting(1002);
        var ascii85 = Filtering.ASCII85Decode.Encode(payload);
        ascii85.Length.Should().Be(1255);
        var encoded = Deflated([.. ascii85, .. Encoding.ASCII.GetBytes(" % a comment past the end marker ")]);
        var limit = new DecodeLimit(cap);

        var (decoded, _) = await DecodedUnder(limit, encoded, "/FlateDecode", "/ASCII85Decode");

        limit.Truncated.Should().BeTrue();
        decoded.Should().Equal(payload);
    }

    [Test]
    public void AFlateStreamThatIsCutShortStillThrowsWhenNoEarlierFilterWasCapped()
    {
        var random = new Random(3);
        var payload = new byte[20_000];
        random.NextBytes(payload);
        var deflated = Deflated(payload);
        var cutShort = deflated[..(deflated.Length / 2)];

        var plain = () => Filtering.FlateDecode.Decode(cutShort, (FilterParms)null);
        var limited = () => Filtering.FlateDecode.Decode(cutShort, new FilterParms(null, new DecodeLimit(1_000_000)));

        plain.Should().Throw<Exception>();
        limited.Should().Throw<Exception>("a limit alone does not make a damaged stream readable");
    }

    /// <summary>The payload deflated, its Adler-32 checksum cut off or made wrong.</summary>
    private static byte[] WithChecksumDamaged(byte[] payload, bool cut)
    {
        var deflated = Deflated(payload);
        if (cut)
            return deflated[..^4];
        deflated[^1] ^= 0xFF;
        return deflated;
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void AFlateStreamWithItsChecksumMissingOrWrongStillThrowsUnlessDamageIsTolerated(bool cut)
    {
        var damaged = WithChecksumDamaged(Counting(100 * 100), cut);

        var plain = () => Filtering.FlateDecode.Decode(damaged, (FilterParms)null);
        var limited = () => Filtering.FlateDecode.Decode(damaged, new FilterParms(null, new DecodeLimit(1_000_000)));

        plain.Should().Throw<Exception>();
        limited.Should().Throw<Exception>("only a caller that asks for tolerance is given a damaged stream's bytes");
    }

    [Test]
    [Arguments(true, 100 * 100)]
    [Arguments(false, 100 * 100)]
    [Arguments(true, 1_000_000)]
    [Arguments(false, 1_000_000)]
    public void AFlateStreamWithItsChecksumMissingOrWrongGivesEveryByteAndSaysItWasDamagedWhenTolerated(bool cut, long cap)
    {
        var payload = Counting(100 * 100);
        var limit = new DecodeLimit(cap) { TolerateDamage = true };

        var decoded = Filtering.FlateDecode.Decode(WithChecksumDamaged(payload, cut), new FilterParms(null, limit));

        decoded.Should().Equal(payload);
        limit.Damaged.Should().BeTrue();
        limit.Truncated.Should().BeFalse();
    }

    [Test]
    public void AFlateStreamCutPartWayGivesWhatInflatedWhenTolerated()
    {
        var random = new Random(3);
        var payload = new byte[20_000];
        random.NextBytes(payload);
        var deflated = Deflated(payload);
        var limit = new DecodeLimit(1_000_000) { TolerateDamage = true };

        var decoded = Filtering.FlateDecode.Decode(deflated[..(deflated.Length / 2)], new FilterParms(null, limit));

        decoded.Should().NotBeEmpty();
        decoded.Should().Equal(payload.Take(decoded.Length));
        limit.Damaged.Should().BeTrue();
    }

    [Test]
    public void AWholeFlateStreamIsNotDamagedWhenDamageIsTolerated()
    {
        var payload = Counting(100 * 100);
        var limit = new DecodeLimit(1_000_000) { TolerateDamage = true };

        var decoded = Filtering.FlateDecode.Decode(Deflated(payload), new FilterParms(null, limit));

        decoded.Should().Equal(payload);
        limit.Damaged.Should().BeFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void AFlateStreamDamagedAfterMoreThanOneReadGivesEveryByteWhenTolerated(bool cut)
    {
        // Longer than the 32 KiB FlateDecode reads at a time, so the read that fails is not the
        // first and its buffer still holds the bytes of the one before.
        var payload = Counting(100_000);
        var limit = new DecodeLimit(1_000_000) { TolerateDamage = true };

        var decoded = Filtering.FlateDecode.Decode(WithChecksumDamaged(payload, cut), new FilterParms(null, limit));

        decoded.Should().Equal(payload);
        limit.Damaged.Should().BeTrue();
    }

    [Test]
    public void DataThatIsNotFlateStillThrowsWhenDamageIsTolerated()
    {
        var limit = new DecodeLimit(1_000_000) { TolerateDamage = true };

        var decode = () => Filtering.FlateDecode.Decode([1, 2, 3, 4, 5, 6], new FilterParms(null, limit));

        decode.Should().Throw<Exception>("nothing inflated, so there is nothing to give");
    }
}
