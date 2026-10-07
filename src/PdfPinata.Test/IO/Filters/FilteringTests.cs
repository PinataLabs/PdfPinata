using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using TUnit.Core;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
///   <see cref="Filtering"/> is the switchboard: a stream's <c>/Filter</c> entry names one filter
///   or a chain of them, and this is what turns those names into objects and runs the data through
///   in order. Getting a name wrong is the difference between a stream that decodes and one that
///   comes back as raw deflate output, so what the names map to is worth stating.
/// </summary>
public class FilteringTests
{
    [Test]
    [Arguments("ASCIIHexDecode", typeof(AsciiHexDecode))]
    [Arguments("AHx", typeof(AsciiHexDecode))]
    [Arguments("ASCII85Decode", typeof(Ascii85Decode))]
    [Arguments("A85", typeof(Ascii85Decode))]
    [Arguments("LZWDecode", typeof(LzwDecode))]
    [Arguments("LZW", typeof(LzwDecode))]
    [Arguments("FlateDecode", typeof(FlateDecode))]
    [Arguments("Fl", typeof(FlateDecode))]
    [Arguments("RunLengthDecode", typeof(RunLengthDecode))]
    [Arguments("RL", typeof(RunLengthDecode))]
    public void EveryFilterNameAndItsAbbreviationReachTheSameFilter(string name, Type expected)
    {
        // The abbreviations are not in the reference. Some tools write them anyway, and a reader
        // that does not know them treats a perfectly good stream as undecodable.
        Filtering.GetFilter(name).Should().BeOfType(expected);
        Filtering.GetFilter("/" + name).Should().BeOfType(expected, "a name from a dictionary carries its slash");
    }

    [Test]
    public void AFilterIsTheSameObjectEveryTimeItIsAskedFor()
    {
        // They hold no per-stream state, so one of each is kept rather than made per call.
        Filtering.GetFilter("FlateDecode").Should().BeSameAs(Filtering.FlateDecode);
        Filtering.GetFilter("ASCII85Decode").Should().BeSameAs(Filtering.ASCII85Decode);
        Filtering.GetFilter("ASCIIHexDecode").Should().BeSameAs(Filtering.ASCIIHexDecode);
        Filtering.GetFilter("LZWDecode").Should().BeSameAs(Filtering.LzwDecode);
    }

    [Test]
    [Arguments("CCITTFaxDecode")]
    [Arguments("JBIG2Decode")]
    [Arguments("DCTDecode")]
    [Arguments("JPXDecode")]
    [Arguments("Crypt")]
    public void AFilterThatIsRealButUnimplementedComesBackAsNothing(string name)
    {
        // Named in the reference and not written here. The caller gets null rather than an
        // exception, and every Encode and Decode overload passes that null straight through - so a
        // JPEG stream comes back as null rather than as its own bytes.
        Filtering.GetFilter(name).Should().BeNull();

        Filtering.Decode([1, 2, 3], name).Should().BeNull();
        Filtering.Decode([1, 2, 3], name, null).Should().BeNull();
        Filtering.DecodeToString([1, 2, 3], name).Should().BeNull();
        Filtering.DecodeToString([1, 2, 3], name, null).Should().BeNull();
        Filtering.Encode([1, 2, 3], name).Should().BeNull();
        Filtering.Encode("abc", name).Should().BeNull();
    }

    [Test]
    public void AFilterNobodyHasHeardOfIsRefused()
    {
        var act = () => Filtering.GetFilter("MakeItSmallerDecode");

        act.Should().Throw<NotImplementedException>().WithMessage("*MakeItSmallerDecode*");
    }

    [Test]
    public void EncodingAndDecodingByNameAgreeWithTheFilterItself()
    {
        var data = "something to squeeze"u8.ToArray();

        var byName = Filtering.Encode(data, "FlateDecode");

        Filtering.Decode(byName, "FlateDecode").Should().Equal(data);
        Filtering.DecodeToString(byName, "FlateDecode", new FilterParms(null))
            .Should().Be("something to squeeze");
    }

    /// <summary>
    ///   <see cref="FlateDecode"/> and <see cref="LzwDecode"/> are the only two filters that read
    ///   the parameters they are handed, to find out whether a predictor was applied. They used to
    ///   read them without first asking whether they were given any, and null is not an unusual
    ///   thing to pass: it is what <see cref="Filter.DecodeToString(byte[])"/> and
    ///   <see cref="Filtering.DecodeToString(byte[], string)"/> pass, neither of which offers a way
    ///   to supply parameters. So the shortest correct-looking way to read a deflated stream as
    ///   text threw a NullReferenceException every time.
    /// </summary>
    [Test]
    public void TheTwoFiltersThatReadTheirParametersAcceptBeingGivenNoneAtAll()
    {
        const string text = "something to squeeze";
        var deflated = Filtering.FlateDecode.Encode(Encoding.ASCII.GetBytes(text));

        Filtering.FlateDecode.Decode(deflated, (FilterParms)null)
            .Should().Equal(Encoding.ASCII.GetBytes(text));
        Filtering.FlateDecode.DecodeToString(deflated).Should().Be(text);
        Filtering.DecodeToString(deflated, "FlateDecode").Should().Be(text);
        Filtering.LzwDecode.Decode(Packed(ClearTable, 'A', 'B', EndOfData), (FilterParms)null)
            .Should().Equal("AB"u8.ToArray());
    }

    [Test]
    public void AStringIsEncodedByItsRawBytes()
    {
        Filtering.Encode("abc", "ASCIIHexDecode").Should().Equal("616263"u8.ToArray());
    }

    // ----- a filter named by a dictionary entry --------------------------------------------------

    [Test]
    public void ASingleFilterCanBeNamedByAPdfName()
    {
        var data = "abc"u8.ToArray();
        var encoded = Filtering.ASCIIHexDecode.Encode(data);

        Filtering.Decode(encoded, new PdfName("/ASCIIHexDecode"), null).Should().Equal(data);
    }

    [Test]
    public void AChainOfFiltersIsUndoneInTheOrderItWasApplied()
    {
        // /Filter [/ASCII85Decode /FlateDecode] means the data was deflated and then made
        // printable, so it is read back the same way round: un-ASCII85 first, then inflate.
        var document = new PdfDocument();
        var data = "a stream worth compressing, compressing, compressing"u8.ToArray();
        var encoded = Filtering.ASCII85Decode.Encode(Filtering.FlateDecode.Encode(data));

        var chain = new PdfArray(document, new PdfName("/ASCII85Decode"), new PdfName("/FlateDecode"));

        Filtering.Decode(encoded, chain, null).Should().Equal(data);
    }

    // ----- parameters in a shape other than the one the filter entry asks for -----------------------

    // ISO 32000-1 Table 5 wants /DecodeParms in the shape of /Filter: a dictionary for a name, an
    // array of the same length for an array. Where the shape differs but there is still only one
    // way to read it, it is read that way. Where a dictionary could belong to more than one filter,
    // nothing is guessed - the decode comes back as nothing, as an unknown filter does, rather than
    // as the data it was given: TryUnfilter takes any answer as the decoded bytes and drops the
    // /Filter entry, so handing back the encoded data saved a page's content still deflated with
    // nothing left to say so.

    private const string Predicted = "0 0 m 100 100 l S\n";

    /// <summary>
    ///   <see cref="Predicted"/> run through PNG prediction (one row, filter type None) and
    ///   deflated, so it decodes only when the /Predictor in its parameters is actually read.
    /// </summary>
    private static byte[] DeflatedWithPrediction() =>
        Filtering.FlateDecode.Encode([0, ..Encoding.ASCII.GetBytes(Predicted)]);

    private static PdfDictionary Prediction(PdfDocument document)
    {
        var parms = new PdfDictionary(document);
        parms.Elements.SetInteger("/Predictor", 12);
        parms.Elements.SetInteger("/Columns", Predicted.Length);
        return parms;
    }

    [Test]
    public void ASingleFilterFindsItsParametersInAnArrayOfOne()
    {
        var document = new PdfDocument();
        var parms = new PdfArray(document, Prediction(document));

        var decoded = Filtering.Decode(DeflatedWithPrediction(), new PdfName("/FlateDecode"), parms);

        Encoding.ASCII.GetString(decoded).Should().Be(Predicted);
    }

    [Test]
    public void AChainOfOneFindsItsParametersInADictionaryOnItsOwn()
    {
        var document = new PdfDocument();
        var chain = new PdfArray(document, new PdfName("/FlateDecode"));

        var decoded = Filtering.Decode(DeflatedWithPrediction(), chain, Prediction(document));

        Encoding.ASCII.GetString(decoded).Should().Be(Predicted);
    }

    [Test]
    public void ParametersThatSayNothingLeaveEveryFilterToItsDefaultsWhateverTheirCount()
    {
        var document = new PdfDocument();
        var data = "a stream worth compressing, compressing, compressing"u8.ToArray();
        var encoded = Filtering.ASCII85Decode.Encode(Filtering.FlateDecode.Encode(data));
        var chain = new PdfArray(document, new PdfName("/ASCII85Decode"), new PdfName("/FlateDecode"));

        Filtering.Decode(encoded, chain, new PdfArray(document, PdfNull.Value)).Should().Equal(data);
        Filtering.Decode(encoded, chain, new PdfArray(document)).Should().Equal(data);
        Filtering.Decode(Filtering.FlateDecode.Encode(data), new PdfName("/FlateDecode"), new PdfArray(document))
            .Should().Equal(data);
    }

    [Test]
    public void AChainWhoseParametersCannotBeMatchedToItsFiltersDecodesToNothing()
    {
        // Is the predictor ASCII85's or Flate's? Only one of them reads it, but a reader that
        // guessed by that would be guessing about a file that is already wrong.
        var document = new PdfDocument();
        var data = Filtering.ASCII85Decode.Encode(DeflatedWithPrediction());
        var chain = new PdfArray(document, new PdfName("/ASCII85Decode"), new PdfName("/FlateDecode"));

        Filtering.Decode(data, chain, Prediction(document)).Should().BeNull();
        Filtering.Decode(data, chain, new PdfArray(document, Prediction(document))).Should().BeNull();
        Filtering.Decode(data, chain, new PdfArray(document, PdfNull.Value, PdfNull.Value, Prediction(document)))
            .Should().BeNull();
    }

    [Test]
    public void ASingleFilterGivenSeveralSetsOfParametersDecodesToNothing()
    {
        var document = new PdfDocument();
        var parms = new PdfArray(document, Prediction(document), Prediction(document));

        Filtering.Decode(DeflatedWithPrediction(), new PdfName("/FlateDecode"), parms).Should().BeNull();
    }

    [Test]
    public void AStreamWhoseParametersCannotBeMatchedKeepsItsFilter()
    {
        // The consequence that matters: the stream is left as it was found, still saying how it
        // is encoded, rather than stripped of /Filter with its bytes still deflated.
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        var encoded = Filtering.ASCII85Decode.Encode(DeflatedWithPrediction());
        dictionary.CreateStream(encoded);
        dictionary.Elements["/Filter"] = new PdfArray(document, new PdfName("/ASCII85Decode"), new PdfName("/FlateDecode"));
        dictionary.Elements["/DecodeParms"] = new PdfArray(document, Prediction(document));

        dictionary.Stream.TryUnfilter().Should().BeFalse();

        dictionary.Elements.ContainsKey("/Filter").Should().BeTrue();
        dictionary.Stream.Value.Should().Equal(encoded);
    }

    [Test]
    public void AChainCanCarryOneSetOfParametersPerFilter()
    {
        var document = new PdfDocument();
        var data = "abc"u8.ToArray();
        var encoded = Filtering.ASCII85Decode.Encode(Filtering.ASCIIHexDecode.Encode(data));
        var chain = new PdfArray(document, new PdfName("/ASCII85Decode"), new PdfName("/ASCIIHexDecode"));
        var parms = new PdfArray(document, new PdfDictionary(document), new PdfDictionary(document));

        Filtering.Decode(encoded, chain, parms).Should().Equal(data);
    }

    [Test]
    public void SomethingThatNamesNoFilterAtAllDecodesToNothing()
    {
        var data = "abc"u8.ToArray();

        // Fully qualified: this test assembly has a PdfInteger of its own.
        Filtering.Decode(data, new PdfPinata.Pdf.PdfInteger(7), null).Should().BeNull();
        Filtering.Decode(data, (PdfItem)null, null).Should().BeNull();
    }

    // ----- Flate ---------------------------------------------------------------------------------

    [Test]
    [Arguments(PdfFlateEncodeMode.Default)]
    [Arguments(PdfFlateEncodeMode.BestSpeed)]
    [Arguments(PdfFlateEncodeMode.BestCompression)]
    public void EveryCompressionSettingProducesSomethingThatInflatesBackAgain(PdfFlateEncodeMode mode)
    {
        var data = Encoding.ASCII.GetBytes(new string('a', 500) + new string('b', 500));

        var encoded = Filtering.FlateDecode.Encode(data, mode);

        encoded.Length.Should().BeLessThan(data.Length, "a thousand bytes of two characters must compress");
        Filtering.FlateDecode.Decode(encoded, new FilterParms(null)).Should().Equal(data);
    }

    [Test]
    public void InflatingNothingGivesNothingBack()
    {
        Filtering.FlateDecode.Decode([], (FilterParms)null).Should().BeEmpty();
    }

    // ----- LZW -----------------------------------------------------------------------------------

    /// <summary>
    ///   Packs a run of LZW codes into bytes the way the reference asks for them: nine bits each,
    ///   most significant bit first, run together with no padding between one code and the next.
    /// </summary>
    /// <remarks>
    ///   Streams are built here rather than pasted in as hex because nothing in PdfPinata can
    ///   encode LZW - <see cref="LzwDecode.Encode(byte[])"/> throws - so there is nothing to round-trip
    ///   against, and a stream written out by hand is one nobody can check by reading it. Nine
    ///   bits is the whole story for these tests: the width only grows once the table passes 511
    ///   entries, which takes a stream far longer than any of them.
    /// </remarks>
    private static byte[] Packed(params int[] codes)
    {
        var bits = new List<bool>();
        foreach (var code in codes)
            for (var bit = 8; bit >= 0; bit--)
                bits.Add(((code >> bit) & 1) == 1);
        while (bits.Count % 8 != 0)
            bits.Add(false);

        var bytes = new byte[bits.Count / 8];
        for (var idx = 0; idx < bits.Count; idx++)
            if (bits[idx])
                bytes[idx / 8] |= (byte)(1 << (7 - idx % 8));
        return bytes;
    }

    private const int ClearTable = 256;
    private const int EndOfData = 257;
    private const int FirstFreeCode = 258;

    [Test]
    public void LzwReadsLiteralCodesAsThemselves()
    {
        var decoded = Filtering.LzwDecode.Decode(
            Packed(ClearTable, 'A', 'B', 'C', EndOfData), new FilterParms(null));

        decoded.Should().Equal("ABC"u8.ToArray());
    }

    [Test]
    public void LzwDoesNotInsistOnBeingToldToClearTheTableFirst()
    {
        var decoded = Filtering.LzwDecode.Decode(Packed('A', 'B', EndOfData), new FilterParms(null));

        decoded.Should().Equal("AB"u8.ToArray());
    }

    [Test]
    public void LzwReadsAStreamThatSaysNothingAsNothing()
    {
        Filtering.LzwDecode.Decode(Packed(ClearTable, EndOfData), new FilterParms(null))
            .Should().BeEmpty();
    }

    [Test]
    public void LzwClearingTheTableInTheMiddleStartsTheCodesOverAgain()
    {
        var decoded = Filtering.LzwDecode.Decode(
            Packed(ClearTable, 'A', 'B', ClearTable, 'C', 'D', EndOfData), new FilterParms(null));

        decoded.Should().Equal("ABCD"u8.ToArray());
    }

    /// <summary>
    ///   Every LZW decoder has to handle one code that is not in its table yet: the encoder is
    ///   allowed to emit the entry it is in the middle of defining, which it does whenever the
    ///   input repeats a run. Such a code stands for the previous entry followed by that entry's
    ///   own first byte - the "KwKwK" case, in the usual telling. This decoder used to write the
    ///   previous entry and stop, dropping the repeated byte. It added the right entry to the
    ///   table, so the stream stayed in step and nothing threw: the output was simply one byte
    ///   short at each occurrence, silently, and a run of the same byte is exactly what an encoder
    ///   emits this code for.
    /// </summary>
    [Test]
    public void LzwReadsTheCodeForTheEntryItIsStillDefining()
    {
        // Code 258 is the entry being defined by this very code, and stands for "AA", so the
        // whole stream is "A" + "AA".
        var decoded = Filtering.LzwDecode.Decode(
            Packed(ClearTable, 'A', FirstFreeCode, EndOfData), new FilterParms(null));

        decoded.Should().Equal("AAA"u8.ToArray());
    }

    [Test]
    public void LzwReadsALongerRunThroughTheSameCase()
    {
        // "ABABABA": after ClearTable the codes are A, B, then 258 ("AB") which is already in the
        // table, then 260 - the entry this code is itself defining, "ABA".
        var decoded = Filtering.LzwDecode.Decode(
            Packed(ClearTable, 'A', 'B', FirstFreeCode, FirstFreeCode + 2, EndOfData),
            new FilterParms(null));

        decoded.Should().Equal("ABABABA"u8.ToArray());
    }

    /// <summary>
    ///   <see cref="Filtering.LzwDecode"/> is one instance for the process, and it used to keep its
    ///   string table and read position in fields - so two decodes at once read each other's table
    ///   and came back short or empty. The tests above, running alongside one another, were what
    ///   showed it.
    /// </summary>
    [Test]
    public void LzwDecodesOnSeveralThreadsAtOnce()
    {
        var packed = Packed(ClearTable, 'A', 'B', FirstFreeCode, FirstFreeCode + 2, EndOfData);
        var wrong = 0;

        Parallel.For(0, 2000, _ =>
        {
            if (!Filtering.LzwDecode.Decode(packed, new FilterParms(null)).AsSpan().SequenceEqual("ABABABA"u8))
                Interlocked.Increment(ref wrong);
        });

        wrong.Should().Be(0);
    }

    [Test]
    public void LzwTreatsAStreamThatRunsOutAsOneThatEnded()
    {
        // Reading past the end is caught and reported as the end-of-data code, so a truncated
        // stream gives back what was read rather than throwing.
        var decoded = Filtering.LzwDecode.Decode(Packed(ClearTable, 'A', 'B'), new FilterParms(null));

        decoded.Should().Equal("AB"u8.ToArray());
    }

    [Test]
    public void LzwRefusesTheFlavourItCannotRead()
    {
        var act = () => Filtering.LzwDecode.Decode([0x00, 0x01, 0x02], new FilterParms(null));

        act.Should().Throw<Exception>().WithMessage("*flavour*");
    }

    [Test]
    public void LzwEncodingIsNotSupportedAndSaysSo()
    {
        var act = () => Filtering.LzwDecode.Encode([1, 2, 3]);

        act.Should().Throw<NotImplementedException>().WithMessage("*LZW*");
    }
}
