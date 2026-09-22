using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

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
    const string Gray4x4 = "BI /W 4 /H 4 /CS /G /BPC 8 ID xxxxxxxxxxxxxxxx EI";

    [Fact]
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

    [Fact]
    public void AnInlineImageIsWrittenBackOut()
    {
        RoundTripOf("q " + Gray4x4 + " Q").Should()
            .Be("q\nBI\n/W 4 /H 4 /CS /G /BPC 8\nID xxxxxxxxxxxxxxxx\nEI\nQ\n");
    }

    [Fact]
    public void AnInlineImageSurvivesBeingReadAndWrittenTwice()
    {
        var once = RoundTripOf("q 4 0 0 4 10 10 cm " + Gray4x4 + " Q");

        RoundTripOf(once).Should().Be(once);
    }

    [Fact]
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

    [Fact]
    public void Ascii85DataIsReadPastAnEIInsideIt()
    {
        var sequence = Read("BI /W 1 /H 1 /CS /G /BPC 8 /F /A85 ID abEIcd~> EI Q");

        var image = sequence[0].Should().BeOfType<CInlineImage>().Subject;
        Encoding.Latin1.GetString(image.Data).Should().Be("abEIcd~>");
        ((COperator)sequence[1]).Name.Should().Be("Q");
    }

    [Fact]
    public void AKeywordInTheDictionaryIsKeptAsWritten()
    {
        // true, false and null have no type among the content objects, and a stencil mask says
        // /IM true, so the dictionary is kept as the text it was written as.
        RoundTripOf("BI /W 8 /H 1 /IM true /D [1 0] ID \u0055 EI")
            .Should().Be("BI\n/W 8 /H 1 /IM true /D [1 0]\nID \u0055\nEI\n");
    }

    [Fact]
    public void TheDataIsAlwaysSeparatedFromEIByALineFeed()
    {
        var sequence = new CSequence { new CInlineImage("/W 1 /H 1 /CS /G /BPC 8", [0x45]) };

        Encoding.Latin1.GetString(sequence.ToContent()).Should().Be("BI\n/W 1 /H 1 /CS /G /BPC 8\nID E\nEI\n");
    }

    // A last byte that happens to be white space - a pixel of 0x20 or 0x00 - is data, and has to
    // stay data: it is not taken for the separator before EI, and no byte is gained or lost by
    // reading the content and writing it back again.
    [Theory]
    [InlineData(new byte[] { 0x45, 0x20 })]
    [InlineData(new byte[] { 0x45, 0x00 })]
    [InlineData(new byte[] { 0x0A })]
    [InlineData(new byte[0])]
    public void DataEndingInWhiteSpaceOrHoldingNothingComesBackAsItWas(byte[] data)
    {
        var sequence = new CSequence { new CInlineImage("/W 1 /H 1", data) };

        var once = ContentReader.ReadContent(sequence.ToContent());
        var twice = ContentReader.ReadContent(once.ToContent());

        once[0].Should().BeOfType<CInlineImage>().Which.Data.Should().Equal(data);
        twice[0].Should().BeOfType<CInlineImage>().Which.Data.Should().Equal(data);
    }

    [Fact]
    public void ACloneHasDataOfItsOwn()
    {
        var image = new CInlineImage("/W 1 /H 1", [1, 2, 3]);

        var clone = image.Clone();
        clone.Data[0] = 9;

        image.Data.Should().Equal(1, 2, 3);
        clone.ImageDictionary.Should().Be("/W 1 /H 1");
    }

    [Theory(Timeout = 5000)]
    // No EI: the data runs to the end of the content.
    [InlineData("BI /W 1 /H 1 ID abc", "/W 1 /H 1", "abc")]
    // No ID either. The scan for ID used to call ScanNextToken until it said ID, and at the end of
    // the content it says Eof for ever.
    [InlineData("BI /W 1 /H 1", "/W 1 /H 1", "")]
    [InlineData("BI", "", "")]
    public async Task AnInlineImageTheContentCutsOffEndsWithTheContent(string content, string dictionary, string data)
    {
        var sequence = await Interruptibly.Run(() => Read(content));

        var image = sequence.Should().ContainSingle().Subject.Should().BeOfType<CInlineImage>().Subject;
        image.ImageDictionary.Should().Be(dictionary);
        Encoding.Latin1.GetString(image.Data).Should().Be(data);
    }

    static CSequence Read(string content) => ContentReader.ReadContent(Encoding.Latin1.GetBytes(content));

    static string RoundTripOf(string content) => Encoding.Latin1.GetString(Read(content).ToContent());
}
