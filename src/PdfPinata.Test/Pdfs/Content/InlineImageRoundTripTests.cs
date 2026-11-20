using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.Content;

/// <summary>
///   An inline image - <c>BI</c>, the image's dictionary, <c>ID</c>, its data and <c>EI</c> - read
///   with <see cref="ContentReader"/> and written back out with <see cref="CSequence.ToContent"/>.
///   The parser used to step over the dictionary and the data, leaving a bare <c>BI</c> and a bare
///   <c>EI</c> in the sequence, so writing the content back lost every inline image in it. Now the
///   whole image is one <see cref="CInlineImage"/>.
/// </summary>
public class InlineImageRoundTripTests
{
    private const string Gray4x4 = "BI /W 4 /H 4 /CS /G /BPC 8 ID xxxxxxxxxxxxxxxx EI";

    [Test]
    public void AnInlineImageIsReadAsOneObjectHoldingItsDictionaryAndItsData()
    {
        var sequence = Read("q " + Gray4x4 + " Q");

        sequence.Select(item => ((COperator)item).Name).Should().Equal("q", "BI", "Q");
        var image = sequence[1].Should().BeOfType<CInlineImage>().Subject;
        image.OpCode.OpCodeName.Should().Be(OpCodeName.BI);
        image.ImageDictionary.Should().Be("/W 4 /H 4 /CS /G /BPC 8");
        Encoding.Latin1.GetString(image.Data).Should().Be("xxxxxxxxxxxxxxxx",
            "the blanks either side of the data separate it from ID and EI rather than belonging to it");
    }

    [Test]
    public void AnInlineImageIsWrittenBackOut()
    {
        RoundTripOf("q " + Gray4x4 + " Q").Should()
            .Be("q\nBI\n/W 4 /H 4 /CS /G /BPC 8\nID xxxxxxxxxxxxxxxx\nEI\nQ\n");
    }

    [Test]
    public void AnInlineImageSurvivesBeingReadAndWrittenTwice()
    {
        var once = RoundTripOf("q 4 0 0 4 10 10 cm " + Gray4x4 + " Q");

        RoundTripOf(once).Should().Be(once);
    }

    [Test]
    public void BinaryImageDataComesBackByteForByte()
    {
        // Every byte value but those of 'E' and 'I', which is what the end of the data is found by.
        var data = Enumerable.Range(0, 256).Where(b => b != 'E' && b != 'I').Select(b => (byte)b).ToArray();
        var content = Encoding.Latin1.GetBytes("BI /W 254 /H 1 /CS /G /BPC 8 ID ")
            .Concat(data)
            .Concat(Encoding.Latin1.GetBytes("\nEI Q"))
            .ToArray();

        var written = ContentReader.ReadContent(content).ToContent();
        var reread = ContentReader.ReadContent(written);

        var image = reread[0].Should().BeOfType<CInlineImage>().Subject;
        image.Data.Should().Equal(data);
        ((COperator)reread[1]).Name.Should().Be("Q");
    }

    [Test]
    public void Ascii85DataIsReadPastAnEIInsideIt()
    {
        var sequence = Read("BI /W 1 /H 1 /CS /G /BPC 8 /F /A85 ID abEIcd~> EI Q");

        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        Encoding.Latin1.GetString(image.Data).Should().Be("abEIcd~>");
        ((COperator)sequence[1]).Name.Should().Be("Q");
    }

    [Test]
    public void AKeywordInTheDictionaryIsKeptAsWritten()
    {
        // true, false and null have no type among the content objects, and a stencil mask says
        // /IM true, so the dictionary is kept as the text it was written as.
        RoundTripOf("BI /W 8 /H 1 /IM true /D [1 0] ID \u0055 EI")
            .Should().Be("BI\n/W 8 /H 1 /IM true /D [1 0]\nID \u0055\nEI\n");
    }

    [Test]
    public void TheDataIsAlwaysSeparatedFromEIByALineFeed()
    {
        var sequence = new CSequence { new CInlineImage("/W 1 /H 1 /CS /G /BPC 8", [0x45]) };

        Encoding.Latin1.GetString(sequence.ToContent()).Should().Be("BI\n/W 1 /H 1 /CS /G /BPC 8\nID E\nEI\n");
    }

    // A last byte that happens to be white space - a pixel of 0x20 or 0x00 - is data, and has to
    // stay data: it is not taken for the separator before EI, and no byte is gained or lost by
    // reading the content and writing it back again.
    [Test]
    [Arguments(new byte[] { 0x45, 0x20 })]
    [Arguments(new byte[] { 0x45, 0x00 })]
    [Arguments(new byte[] { 0x0A })]
    [Arguments(new byte[0])]
    public void DataEndingInWhiteSpaceOrHoldingNothingComesBackAsItWas(byte[] data)
    {
        var sequence = new CSequence { new CInlineImage("/W 1 /H 1", data) };

        var once = ContentReader.ReadContent(sequence.ToContent());
        var twice = ContentReader.ReadContent(once.ToContent());

        once[0].Should().BeOfType<CInlineImage>().Which.Data.Should().Equal(data);
        twice[0].Should().BeOfType<CInlineImage>().Which.Data.Should().Equal(data);
    }

    [Test]
    public void ACloneHasDataOfItsOwn()
    {
        var image = new CInlineImage("/W 1 /H 1", [1, 2, 3]);

        var clone = image.Clone();
        clone.Data[0] = 9;

        image.Data.Should().Equal(1, 2, 3);
        clone.ImageDictionary.Should().Be("/W 1 /H 1");
    }

    [Test, Timeout(5000)]
    // No EI: the data runs to the end of the content.
    [Arguments("BI /W 1 /H 1 ID abc", "/W 1 /H 1", "abc")]
    // No ID either. The scan for ID used to call ScanNextToken until it said ID, and at the end of
    // the content it says Eof for ever.
    [Arguments("BI /W 1 /H 1", "/W 1 /H 1", "")]
    [Arguments("BI", "", "")]
    public async Task AnInlineImageTheContentCutsOffEndsWithTheContent(string content, string dictionary, string data)
    {
        var sequence = await Interruptibly.Run(() => Read(content));

        var image = sequence.Should().ContainSingle().Subject.Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be(dictionary);
        Encoding.Latin1.GetString(image.Data).Should().Be(data);
    }

    [Test, Timeout(5000)]
    [Arguments("(unclosed")]
    [Arguments("<<")]
    [Arguments("<</Predictor 1")]
    [Arguments("<00ff")]
    public async Task AnUnclosedStringOrDictionaryEndsAtTheIDAndWhatFollowsTheImageIsRead(string unclosed)
    {
        var sequence = await Interruptibly.Run(() =>
            Read($"BI /W 1 /H 1 /BPC 8 /CS /G /X {unclosed} ID ÿ EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be($"/W 1 /H 1 /BPC 8 /CS /G /X {unclosed}");
        image.Data.Should().Equal(0xff);
    }

    [Test, Timeout(5000)]
    public async Task AnUnclosedStringDoesNotReachIntoTheNextInlineImage()
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 /X (unclosed ID a EI BI /W 1 /H 1 ID b EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "BI", "re", "f");
        sequence.OfType<CInlineImage>().Select(image => Encoding.Latin1.GetString(image.Data))
            .Should().Equal("a", "b");
    }

    [Test, Timeout(5000)]
    public async Task AnIDInsideAStringThatClosesDoesNotEndTheEntriesBeforeAnUnclosedOne()
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 /X ( ID EI ) /Y (unclosed ID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /X ( ID EI ) /Y (unclosed");
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    [Test, Timeout(5000)]
    public async Task IDsInsideACommentDoNotEndTheEntries()
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 /BPC 8 /CS /G % ID ID ID ID ID EI\nID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /BPC 8 /CS /G % ID ID ID ID ID EI");
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    // A dictionary left open used to read on to the end of the stream, so every image after the
    // first was lost. The limit is generous because coverage instrumentation slows reading.
    [Test, Timeout(30_000)]
    public async Task ManyInlineImagesWithUnclosedStringsAreEachReadOnlyAsFarAsTheirOwnEnd()
    {
        const int images = 4_000;
        var content = string.Concat(Enumerable.Repeat("BI /W 1 /H 1 /X (unclosed ID a EI ", images)) + "0 0 10 10 re f";

        var sequence = await Interruptibly.Run(() => Read(content));

        sequence.OfType<CInlineImage>().Should().HaveCount(images);
        sequence.Select(item => ((COperator)item).Name).TakeLast(2).Should().Equal("re", "f");
    }

    [Test, Timeout(5000)]
    [Arguments("/F /A85 ID!!~>EI", "/F /A85", "!!~>")]
    [Arguments("/F /A85 ID!!~>\nEI", "/F /A85", "!!~>")]
    [Arguments("/F /AHx ID00>EI", "/F /AHx", "00>")]
    [Arguments("/F /AHx ID<00>\nEI", "/F /AHx", "<00>")]
    [Arguments("/F [/A85 /Fl] ID<~!!~>\nEI", "/F [/A85 /Fl]", "<~!!~>")]
    [Arguments("ID\u0080\u0081\nEI", "", "\u0080\u0081")]
    [Arguments("ID0\nEI", "", "0")]
    [Arguments("ID(\nEI", "", "(")]
    [Arguments("ID/\nEI", "", "/")]
    [Arguments("ID\u000ba\nEI", "", "\u000ba")]
    [Arguments("ID\u00ada\nEI", "", "\u00ada")]
    public async Task AnIDWithNoWhiteSpaceAfterItEndsTheEntries(string image, string entries, string data)
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 " + image + " 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var read = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        read.ImageDictionary.Should().Be(("/W 1 /H 1 " + entries).TrimEnd());
        Encoding.Latin1.GetString(read.Data).Should().Be(data);
    }

    [Test, Timeout(5000)]
    [Arguments("8ID a\nEI", "8")]
    [Arguments("8.0ID a\nEI", "8.0")]
    [Arguments("8\u000bID a\nEI", "8\u000b")]
    [Arguments("8\u00adID a\nEI", "8\u00ad")]
    public async Task AnIDWithNoWhiteSpaceBeforeItEndsTheEntries(string image, string bitsPerComponent)
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 /BPC " + image + " 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var read = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        read.ImageDictionary.Should().Be("/W 1 /H 1 /BPC " + bitsPerComponent);
        Encoding.Latin1.GetString(read.Data).Should().Be("a");
    }

    [Test, Timeout(5000)]
    public async Task AnIDWithNoWhiteSpaceAfterItIsFoundPastAStringHoldingAnotherID()
    {
        var sequence = await Interruptibly.Run(() =>
            Read("BI /W 1 /H 1 /X (a ID b) /F /A85 ID!!~>EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /X (a ID b) /F /A85");
        Encoding.Latin1.GetString(image.Data).Should().Be("!!~>");
    }

    [Test, Timeout(5000)]
    public async Task ThreeIDsInsideAStringThatClosesAreReadAsText()
    {
        var sequence = await Interruptibly.Run(() => Read("BI /W 1 /H 1 /X ( ID ID ID ) ID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /X ( ID ID ID )");
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    // Each ID a string holds costs one more read of the string, so the reading gives up at the
    // fourth and takes the string to be left open. The image is misread, and what follows it is not.
    [Test, Timeout(5000)]
    public async Task FourIDsInsideAStringThatClosesEndTheEntriesAtTheFirst()
    {
        var sequence = await Interruptibly.Run(() => Read("BI /W 1 /H 1 /X ( ID ID ID ID ) ID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /X (");
        Encoding.Latin1.GetString(image.Data).Should().Be("ID ID ID ) ID a");
    }

    [Test, Timeout(5000)]
    public async Task FourStringsThatEachCloseOverAnIDAreReadAsText()
    {
        const string entries = "/A ( ID ) /B ( ID ) /C ( ID ) /D ( ID ) /E 1";

        var sequence = await Interruptibly.Run(() => Read("BI " + entries + " ID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be(entries);
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    // An ID standing hard against its data is no candidate, so the next candidate may lie at the
    // end of the content. Searching to it, or copying the content up to it, for every such image
    // made the time grow with the square of the images. Doubling the images must about double it.
    [Test, Timeout(60_000), NotInParallel]
    [Arguments("BI /W 1 /H 1 /F /AHx ID00>EI ")]
    [Arguments("BI /W 1 /H 1 /F /A85 ID!!~>EI ")]
    [Arguments("BI /W 1 /H 1 /BPC 8ID a EI ")]
    public async Task ImagesWhoseIDIsNoCandidateAreReadInTimeProportionalToTheirNumber(string image)
    {
        const int images = 4_000;
        var once = Encoding.Latin1.GetBytes(string.Concat(Enumerable.Repeat(image, images)) + "BI /W 1 ID a EI");
        var twice = Encoding.Latin1.GetBytes(string.Concat(Enumerable.Repeat(image, 2 * images)) + "BI /W 1 ID a EI");

        var ratio = await Interruptibly.Run(() =>
        {
            ContentReader.ReadContent(once);
            var fastestOnce = long.MaxValue;
            var fastestTwice = long.MaxValue;
            for (var run = 0; run < 7; run++)
            {
                fastestOnce = Math.Min(fastestOnce, TicksToRead(once));
                fastestTwice = Math.Min(fastestTwice, TicksToRead(twice));
            }
            return (double)fastestTwice / fastestOnce;
        });

        ratio.Should().BeLessThan(3, "a linear reading doubles and a quadratic one quadruples");
    }

    private static long TicksToRead(byte[] content)
    {
        var clock = Stopwatch.StartNew();
        ContentReader.ReadContent(content);
        return clock.ElapsedTicks;
    }

    [Test, Timeout(5000)]
    public async Task ManyCommentLinesEachHoldingAnIDDoNotEndTheEntries()
    {
        var comments = string.Concat(Enumerable.Repeat("% ID\n", 100));

        var sequence = await Interruptibly.Run(() => Read("BI /W 1 /H 1 " + comments + "/BPC 8 ID a EI 0 0 10 10 re f"));

        sequence.Select(item => ((COperator)item).Name).Should().Equal("BI", "re", "f");
        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 " + comments + "/BPC 8");
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    [Test]
    public void AStringThatClosesKeepsAnIDInsideItAsText()
    {
        var sequence = Read("BI /W 1 /H 1 /X (an ID inside) ID a EI");

        var image = sequence.Should().ContainSingle().Subject.Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be("/W 1 /H 1 /X (an ID inside)");
        Encoding.Latin1.GetString(image.Data).Should().Be("a");
    }

    private static CSequence Read(string content) => ContentReader.ReadContent(Encoding.Latin1.GetBytes(content));

    private static string RoundTripOf(string content) => Encoding.Latin1.GetString(Read(content).ToContent());
}
