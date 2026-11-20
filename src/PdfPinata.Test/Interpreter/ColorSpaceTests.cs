using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfInt = PdfPinata.Pdf.PdfInteger;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// The colour spaces a page names, each turned to the RGB that reaches the device: Indexed, through
/// its base, and ICCBased, through its alternate or its component count. Both are built by hand and
/// drawn through the interpreter, because that is how content reaches them.
/// </summary>
public class ColorSpaceTests
{
    private static readonly byte[] Primaries = [255, 0, 0, 0, 255, 0, 0, 0, 255];

    private static Task<Drawn> Page(string content, Func<PdfDocument, PdfObject> space) =>
        Interruptibly.Run(() => Drawn.Page(content, (document, resources) =>
            Drawn.Add(document, resources, "/ColorSpace", "/Cs1", space(document))));

    private static PdfName Name(string name) => new(name);

    private static PdfArray Array(PdfDocument document, params PdfItem[] items)
    {
        var array = new PdfArray(document, items);
        document.Internals.AddObject(array);
        return array;
    }

    private static PdfString Bytes(byte[] bytes) =>
        new(new string(bytes.Select(b => (char)b).ToArray()), PdfStringEncoding.RawEncoding);

    private static PdfDictionary StreamOf(PdfDocument document, byte[] bytes)
    {
        var stream = new PdfDictionary(document);
        document.Internals.AddObject(stream);
        stream.CreateStream(bytes);
        return stream;
    }

    private static PdfDictionary Profile(PdfDocument document, int? n, PdfItem alternate = null)
    {
        var profile = StreamOf(document, [0, 1, 2]);
        if (n != null)
            profile.Elements.SetInteger("/N", n.Value);
        if (alternate != null)
            profile.Elements["/Alternate"] = alternate;
        return profile;
    }

    private static PdfArray Indexed(PdfDocument document, PdfItem baseSpace, int highValue, PdfItem lookup) =>
        Array(document, Name("/Indexed"), baseSpace, new PdfInt(highValue), lookup);

    private static PdfArray Icc(PdfDocument document, PdfDictionary profile) =>
        Array(document, Name("/ICCBased"), profile.Reference);

    private static PagePaint Filled(Drawn drawn) => drawn.Device.Fills.Single().Paint;

    private const string Fill = " 0 0 5 5 re f";

    [Test, Timeout(30_000)]
    public async Task AnIndexedColourIsTheEntryOfTheBaseSpaceItNames()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill + " 2 sc" + Fill,
            d => Indexed(d, Name("/DeviceRGB"), 2, Bytes(Primaries)));

        drawn.Device.Fills.Select(f => f.Paint).Should().Equal(new PagePaint(0, 1, 0), new PagePaint(0, 0, 1));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task SelectingAnIndexedSpaceSetsTheFirstEntryAndStrokingUsesTheSameTable()
    {
        var drawn = await Page("/Cs1 cs /Cs1 CS 2 SC" + Fill + " 0 0 m 5 5 l S",
            d => Indexed(d, Name("/DeviceRGB"), 2, Bytes(Primaries)));

        drawn.Device.Strokes.Single().Paint.Should().Be(new PagePaint(0, 0, 1));
        Filled(drawn).Should().Be(new PagePaint(1, 0, 0));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedLookupCanBeAStream()
    {
        var drawn = await Page("/Cs1 cs 1 scn" + Fill,
            d => Indexed(d, Name("/DeviceGray"), 1, StreamOf(d, [0, 255]).Reference));

        Filled(drawn).Should().Be(new PagePaint(1, 1, 1));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedLookupCanBeAnIndirectString()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill, d =>
        {
            var table = new PdfStringObject(d, Bytes([0, 0, 0, 255, 0, 255]).Value);
            d.Internals.AddObject(table);
            return Indexed(d, Name("/DeviceRGB"), 1, table.Reference);
        });

        Filled(drawn).Should().Be(new PagePaint(1, 0, 1));
    }

    [Test, Timeout(30_000)]
    public async Task ATableThatBeginsWithAByteOrderMarkIsStillReadAsBytes()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d => Indexed(d, Name("/DeviceRGB"), 0, Bytes([0xFE, 0xFF, 0x00])));

        Filled(drawn).Should().Be(new PagePaint(254 / 255.0, 1, 0));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedColourOverCmykIsConvertedThroughTheBase()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill + " 1 sc" + Fill,
            d => Indexed(d, Name("/DeviceCMYK"), 1, Bytes([255, 0, 0, 0, 0, 0, 0, 255])));

        drawn.Device.Fills.Select(f => f.Paint).Should().Equal(new PagePaint(0, 1, 1), new PagePaint(0, 0, 0));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedBaseCanBeAnIccBasedSpaceDefinedInline()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill, d =>
            Indexed(d, Icc(d, Profile(d, 3)), 1, Bytes([0, 0, 0, 255, 128, 0])));

        Filled(drawn).Should().Be(new PagePaint(1, 128 / 255.0, 0));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexIsRoundedToTheNearestEntry()
    {
        var drawn = await Page("/Cs1 cs 0.4 sc" + Fill + " 0.6 sc" + Fill,
            d => Indexed(d, Name("/DeviceGray"), 1, Bytes([0, 255])));

        drawn.Device.Fills.Select(f => f.Paint.Red).Should().Equal(0, 1);
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexPastEitherEndOfTheTableIsHeldToTheEnd()
    {
        var drawn = await Page("/Cs1 cs 9 sc" + Fill + " -3 sc" + Fill,
            d => Indexed(d, Name("/DeviceGray"), 1, Bytes([51, 255])));

        drawn.Device.Fills.Select(f => f.Paint.Red).Should().Equal(1, 51 / 255.0);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task ATopIndexAbove255IsHeldTo255()
    {
        var table = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var drawn = await Page("/Cs1 cs 300 sc" + Fill + " 255 sc" + Fill,
            d => Indexed(d, Name("/DeviceGray"), 1000, Bytes(table)));

        drawn.Device.Fills.Select(f => f.Paint).Should().Equal(new PagePaint(1, 1, 1), new PagePaint(1, 1, 1));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task ANegativeTopIndexLeavesTheFirstEntry()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill, d => Indexed(d, Name("/DeviceGray"), -4, Bytes([255, 0])));

        Filled(drawn).Should().Be(new PagePaint(1, 1, 1));
    }

    [Test, Timeout(30_000)]
    public async Task AShortTableIsReportedAndWhatItLacksIsZero()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill + " 1 sc" + Fill + " 2 sc" + Fill,
            d => Indexed(d, Name("/DeviceRGB"), 2, Bytes([255, 255, 255, 255, 255])));

        drawn.Device.Fills.Select(f => f.Paint).Should().Equal(
            new PagePaint(1, 1, 1), new PagePaint(1, 1, 0), new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task ALookupThatIsNeitherAStringNorAStreamIsReportedAndTakenAsEmpty()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d => Indexed(d, Name("/DeviceRGB"), 1, new PdfInt(5)));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Contain("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task AHighestIndexThatIsNoNumberIsTakenFromTheTable()
    {
        var drawn = await Page("/Cs1 cs 5 sc" + Fill,
            d => Array(d, Name("/Indexed"), Name("/DeviceGray"), Name("/Oops"), Bytes([10, 200])));

        Filled(drawn).Should().Be(new PagePaint(200 / 255.0, 200 / 255.0, 200 / 255.0));
        drawn.Problems.Should().Equal("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedSpaceWithTooFewEntriesStillTakesOneComponent()
    {
        var drawn = await Page("/Cs1 cs 1 sc 0 0 5 5 re f",
            d => Array(d, Name("/Indexed"), Name("/DeviceRGB")));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedSpaceBasedOnAnIndexedSpaceIsRefused()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
            Indexed(d, Indexed(d, Name("/DeviceGray"), 0, Bytes([255])), 0, Bytes([0])));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedSpaceThatIsItsOwnBaseIsReportedAndDrawnInBlack()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
        {
            var space = Indexed(d, Name("/DeviceGray"), 0, Bytes([255]));
            space.Elements[1] = space.Reference;
            return space;
        });

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task TwoIndexedSpacesThatNameEachOtherEndAsWell()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
        {
            var first = Indexed(d, Name("/DeviceGray"), 0, Bytes([255]));
            var second = Indexed(d, first.Reference, 0, Bytes([255]));
            first.Elements[1] = second.Reference;
            return first;
        });

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Contain("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedSpaceBasedOnAnUnsupportedSpaceIsReportedOnceAndDrawnInBlack()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
            Indexed(d, Array(d, Name("/Separation"), Name("/Spot"), Name("/DeviceRGB"), new PdfDictionary(d)), 0,
                Bytes([255])));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("UnsupportedColorSpace /Separation");
    }

    [Test, Timeout(30_000)]
    [Arguments(1, "0.5 sc", 0.5, 0.5, 0.5)]
    [Arguments(3, "1 0.5 0 sc", 1, 0.5, 0)]
    [Arguments(4, "1 0 0 0 sc", 0, 1, 1)]
    public async Task AnIccBasedSpaceWithNoAlternateIsTheDeviceSpaceItsComponentCountNames(
        int n, string colour, double red, double green, double blue)
    {
        var drawn = await Page("/Cs1 cs " + colour + Fill, d => Icc(d, Profile(d, n)));

        Filled(drawn).Should().Be(new PagePaint(red, green, blue));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task EveryComponentOfAnIccBasedColourStartsAtZeroSoCmykStartsWhite()
    {
        var drawn = await Page("/Cs1 cs" + Fill, d => Icc(d, Profile(d, 4)));

        Filled(drawn).Should().Be(new PagePaint(1, 1, 1));
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedAlternateIsUsedInPreferenceToTheComponentCount()
    {
        var drawn = await Page("/Cs1 cs 0.2 0.4 0.6 sc" + Fill,
            d => Icc(d, Profile(d, 3, Name("/DeviceRGB"))));

        Filled(drawn).Should().Be(new PagePaint(0.2, 0.4, 0.6));
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedAlternateCanStandWhereTheProfileHasNoComponentCount()
    {
        var drawn = await Page("/Cs1 cs 1 0 0 0 sc" + Fill,
            d => Icc(d, Profile(d, null, Name("/DeviceCMYK"))));

        Filled(drawn).Should().Be(new PagePaint(0, 1, 1));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedAlternateThatIsAnArrayIsResolvedLikeAnyDefinition()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
            Icc(d, Profile(d, 1, Indexed(d, Name("/DeviceGray"), 0, Bytes([255])))));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnAlternateWithTheWrongNumberOfComponentsIsReportedAndTheComponentCountIsUsed()
    {
        var drawn = await Page("/Cs1 cs 1 0 0 sc" + Fill, d => Icc(d, Profile(d, 3, Name("/DeviceGray"))));

        Filled(drawn).Should().Be(new PagePaint(1, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnUnsupportedAlternateIsReportedAndTheComponentCountIsUsed()
    {
        var drawn = await Page("/Cs1 cs 0 1 0 sc" + Fill, d =>
            Icc(d, Profile(d, 3, Array(d, Name("/Lab"), new PdfDictionary(d)))));

        Filled(drawn).Should().Be(new PagePaint(0, 1, 0));
        drawn.Problems.Should().Equal("UnsupportedColorSpace /Lab");
    }

    [Test, Timeout(30_000)]
    [Arguments(5)]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task AnIccBasedSpaceWithABadComponentCountAndNoAlternateIsReportedAndDrawnInBlack(int n)
    {
        var drawn = await Page("/Cs1 cs 1 0 0 sc" + Fill, d => Icc(d, Profile(d, n)));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedSpaceWithNeitherComponentCountNorAlternateIsReportedAndDrawnInBlack()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill, d => Icc(d, Profile(d, null)));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedSpaceWithNoProfileIsReportedAndDrawnInBlack()
    {
        var drawn = await Page("/Cs1 cs 1 sc" + Fill, d => Array(d, Name("/ICCBased")));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnIccBasedAlternateThatNamesTheSpaceItIsInIsReportedAndTheComponentCountIsUsed()
    {
        var drawn = await Page("/Cs1 cs 0 0 1 sc" + Fill, d =>
        {
            var space = Array(d, Name("/ICCBased"));
            var profile = Profile(d, 3, space.Reference);
            space.Elements.Add(profile.Reference);
            return space;
        });

        Filled(drawn).Should().Be(new PagePaint(0, 0, 1));
        drawn.Problems.Should().Equal("InvalidValue /ICCBased");
    }

    [Test, Timeout(30_000)]
    public async Task AnIndexedSpaceBasedOnAnIccBasedSpaceThatNamesItIsReportedAndDrawnInBlack()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
        {
            var space = Indexed(d, Name("/DeviceGray"), 0, Bytes([255]));
            var profile = Profile(d, null, space.Reference);
            space.Elements[1] = Icc(d, profile).Reference;
            return space;
        });

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Contain("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task ADefinitionNestedBeyondTheLimitEndsInsteadOfFollowingItsLength()
    {
        var drawn = await Page("/Cs1 cs 0 sc" + Fill, d =>
        {
            var space = Indexed(d, Name("/DeviceGray"), 0, Bytes([255]));
            for (var depth = 0; depth < 200; depth++)
                space = Indexed(d, space.Reference, 0, Bytes([255]));
            return space;
        });

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Contain("InvalidValue /Indexed");
    }

    [Test, Timeout(30_000)]
    public async Task CalGrayAndCalRgbAreDrawnAsTheDeviceSpacesAndNothingIsReported()
    {
        var gray = await Page("/Cs1 cs 0.25 sc" + Fill, d => Array(d, Name("/CalGray"), new PdfDictionary(d)));
        var rgb = await Page("/Cs1 cs 0.1 0.2 0.3 sc" + Fill, d => Array(d, Name("/CalRGB"), new PdfDictionary(d)));

        Filled(gray).Should().Be(new PagePaint(0.25, 0.25, 0.25));
        Filled(rgb).Should().Be(new PagePaint(0.1, 0.2, 0.3));
        gray.Result.IsComplete.Should().BeTrue();
        rgb.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    [Arguments("/Separation", "0.5")]
    [Arguments("/DeviceN", "0.5")]
    [Arguments("/Lab", "50 0 0")]
    public async Task ASpaceThatIsNotResolvedIsReportedAndDrawnInTheFallbackColour(string family, string colour)
    {
        var drawn = await Page("1 0 0 rg /Cs1 cs " + colour + " scn" + Fill, d => Array(d, Name(family), new PdfDictionary(d)));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("UnsupportedColorSpace " + family);
    }

    [Test, Timeout(30_000)]
    public async Task APatternSpaceIsReportedAndDrawnInTheFallbackColour()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("/Pattern cs /P1 scn" + Fill));

        Filled(drawn).Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Contain("UnsupportedColorSpace /Pattern");
    }
}
