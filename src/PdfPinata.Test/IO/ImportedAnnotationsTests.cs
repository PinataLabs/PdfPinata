using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.ImportedPageFixtures;
using static PdfPinata.Test.IO.SplitTests;

namespace PdfPinata.Test.IO;

/// <summary>
///   Importing a page copies its annotations only because that is what AnnotationCopyingType
///   defaults to. The default started out DoNotCopy, which dropped every hyperlink of every page
///   a document was combined from, and is what
///   https://github.com/ststeiger/PdfSharpCore/issues/307 reports. Nothing else in the suite
///   pins the default down: the tests around the destinations of links would go on passing with
///   the annotations gone, because a page with no annotations has no destination to get wrong.
///   <para>
///   The link here opens a web address, the shape of link the issue is about. It names no page,
///   so nothing has to be resolved for it to arrive intact and there is nothing between it and
///   the copy that the default decides on.
///   </para>
/// </summary>
public class ImportedAnnotationsTests
{
    private const string Url = "http://www.example.com/a?b=c";

    [Fact]
    public void AddingAPageKeepsItsWebLink()
    {
        using var output = Import(source =>
        {
            var target = new PdfDocument();
            target.AddPage(source.Pages[0]);
            return target;
        });

        UrlOf(output).Should().Be(Url);
    }

    [Fact]
    public void InsertingARangeKeepsTheWebLinkOfThePage()
    {
        using var output = Import(source =>
        {
            var target = new PdfDocument();
            target.Pages.InsertRange(0, source, 0, 1);
            return target;
        });

        UrlOf(output).Should().Be(Url);
    }

    [Fact]
    public void DeepCopyingAnnotationsKeepsTheWebLink()
    {
        using var output = Import(source =>
        {
            var target = new PdfDocument();
            target.Pages.Add(source.Pages[0], AnnotationCopyingType.DeepCopy);
            return target;
        });

        UrlOf(output).Should().Be(Url);
    }

    [Fact]
    public void AskingForNoAnnotationsLeavesThePageWithout()
    {
        using var output = Import(source =>
        {
            var target = new PdfDocument();
            target.Pages.Add(source.Pages[0], AnnotationCopyingType.DoNotCopy);
            return target;
        });

        AnnotationsOf(output, 0).Should().BeNull();
    }

    /// <summary>
    ///   A web link names no page of the document it came from, so importing one takes no page
    ///   along with it. The fixture pages weigh an image each, which is how one that came along
    ///   would show.
    /// </summary>
    [Fact]
    public void AWebLinkTakesNoPageOfTheDocumentWithIt()
    {
        using var output = Import(source =>
        {
            var target = new PdfDocument();
            target.AddPage(source.Pages[0]);
            return target;
        });

        UrlOf(output).Should().Be(Url);
        Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Modify).PageCount.Should().Be(1);
        output.Length.Should().BeLessThan(2 * ImageLength);
    }

    /// <summary>
    ///   Imports the page carrying the web link, by the means given, and writes the result out.
    /// </summary>
    private static MemoryStream Import(Func<PdfDocument, PdfDocument> import)
    {
        using var input = new MemoryStream(LinkedPagesDocument(UriLink(Url)));
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var output = new MemoryStream();
        import(source).Save(output, false);
        return output;
    }

    /// <summary>
    ///   The address the only annotation of the first page opens, or null if it opens none.
    /// </summary>
    private static string UrlOf(MemoryStream document)
    {
        var annotations = AnnotationsOf(document, 0);
        annotations.Elements.Count.Should().Be(1);

        var action = annotations.Elements.GetDictionary(0).Elements.GetDictionary("/A");
        return action?.Elements.GetString("/URI");
    }
}
