using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;
using static PdfPinata.Test.IO.ImportedPageFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   Importing a page copies what the page reaches, and stops at any other page. An annotation's
///   <c>/P</c>, a link's <c>/Dest</c> or a go-to action's <c>/D</c> names a page, whose
///   <c>/Parent</c> is the page tree, and following those copied every page of the document into
///   the target only for the save to throw the copies away again (#366).
///   <para>
///   The assertions are on the target before it is saved, because the save already dropped what
///   nothing reached. Every fixture page draws an image of its own, so the number of images in
///   the target says how many pages' worth of objects came along.
///   </para>
/// </summary>
public class ImportClosureBoundaryTests
{
    [Test]
    [MethodDataSource(typeof(SplitTests), nameof(SplitTests.Destinations))]
    public void ImportingAPageThatLinksToAnotherOneCopiesNoOtherPage(string destination)
    {
        var target = ImportFirstPage(LinkedPagesDocument(Link(destination)));

        ImagesIn(target).Should().Be(1);
        PagesIn(target).Should().Be(1);
    }

    [Test]
    public void APageWithoutATypeIsRecognisedByItsPlaceInThePageTree()
    {
        var target = ImportFirstPage(UntypedPagesDocument(Link("/P 4 0 R")));

        ImagesIn(target).Should().Be(1);
    }

    [Test]
    public void AnAnnotationStillNamesTheImportedPage()
    {
        var target = ImportFirstPage(LinkedPagesDocument(Link("/P 4 0 R/Dest[4 0 R/Fit]")));

        var page = target.Pages[0];
        page.Annotations.Count.Should().Be(1);
        page.Annotations.Elements.GetDictionary(0)!.Elements["/P"].Should().BeSameAs(page.Reference);
    }

    private static PdfDocument ImportFirstPage(byte[] document)
    {
        using var input = new MemoryStream(document);
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        _ = target.AddPage(source.Pages[0]);
        return target;
    }

    private static int ImagesIn(PdfDocument document)
    {
        return document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Count(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image");
    }

    private static int PagesIn(PdfDocument document)
    {
        return document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Count(dictionary => dictionary.Elements.GetName("/Type") == "/Page");
    }
}
