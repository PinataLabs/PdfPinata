using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.ImportedPageFixtures;
using static PdfPinata.Test.IO.SplitTests;

namespace PdfPinata.Test.IO;

/// <summary>
///   PdfPages.InsertRange used to copy the annotations of a page twice: once with the page and
///   once through a loop of its own that kept links alone, and of those only the ones whose
///   destination was written the one way it knew. The second copy threw when there was anything
///   for it to add.
/// </summary>
public class InsertRangeTests
{
    [Fact]
    public void InsertingARangeOfPagesThatLinkToEachOtherDoesNotThrow()
    {
        // A destination of five elements naming a page of the range is what it took to reach the
        // second copy of the annotations at all.
        var insert = () => InsertRange(LinkedPagesDocument(Link("/Dest[4 0 R/XYZ 0 0 0]")), 0, 3);

        insert.Should().NotThrow();
    }

    [Theory]
    [InlineData("/Dest[4 0 R/XYZ 0 0 0]")]  // The one shape the loop of its own knew.
    [InlineData("/Dest[4 0 R/Fit]")]
    [InlineData("/Dest[4 0 R/FitH 0]")]
    [InlineData("/Dest[4 0 R/FitR 0 0 10 10]")]
    [InlineData("/A<</S/GoTo/D[4 0 R/Fit]>>")]
    public void InsertingARangeKeepsALinkToAPageOfTheRange(string destination)
    {
        using var output = InsertRange(LinkedPagesDocument(Link(destination)), 0, 3);

        var reread = Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        reread.PageCount.Should().Be(3);

        var annotations = AnnotationsOf(output, 0);
        annotations.Elements.Count.Should().Be(1);
        DestinationOf(annotations.Elements.GetDictionary(0)).ObjectID
            .Should().Be(PdfInternals.GetObjectID(reread.Pages[1]));
    }

    [Fact]
    public void InsertingARangeDropsALinkToAPageLeftOutOfIt()
    {
        // The link goes to the third page, of which only the first two are inserted.
        using var output = InsertRange(LinkedPagesDocument(Link("/Dest[5 0 R/XYZ 0 0 0]")), 0, 2);

        Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Modify).PageCount.Should().Be(2);

        var annotation = AnnotationsOf(output, 0).Elements.GetDictionary(0);
        annotation.Elements.ContainsKey("/Dest").Should().BeFalse();

        // And the page it went to was left behind rather than copied along with the link.
        output.Length.Should().BeLessThan(3 * ImageLength);
    }

    [Fact]
    public void InsertingARangeKeepsAnAnnotationThatIsNotALink()
    {
        using var output = InsertRange(LinkedPagesDocument(Note()), 0, 3);

        var annotations = AnnotationsOf(output, 0);
        annotations.Elements.Count.Should().Be(1);
        annotations.Elements.GetDictionary(0).Elements.GetString("/Subtype").Should().Be("/Text");
    }

    [Fact]
    public void InsertingARangeKeepsEveryAnnotationOfThePage()
    {
        using var output = InsertRange(
            LinkedPagesDocument(Link("/Dest[4 0 R/Fit]"), Note()), 0, 3);

        AnnotationsOf(output, 0).Elements.Count.Should().Be(2);
    }

    private static MemoryStream InsertRange(byte[] document, int startIndex, int pageCount)
    {
        using var input = new MemoryStream(document);
        var inputDocument = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        target.Pages.InsertRange(0, inputDocument, startIndex, pageCount);

        var output = new MemoryStream();
        target.Save(output, false);
        return output;
    }
}
