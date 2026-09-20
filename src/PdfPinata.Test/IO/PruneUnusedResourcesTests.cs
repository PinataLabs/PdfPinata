using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.SharedResourceFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   A page carries the resources it was given, which is not the same as the resources it uses.
///   Pages of a document commonly share one dictionary naming every font and image in it, so
///   splitting the document gave every page a copy of all of them.
///   See https://github.com/ststeiger/PdfSharpCore/issues/461.
/// </summary>
public class PruneUnusedResourcesTests
{
    [Fact]
    public void EachPageKeepsTheImageItDraws()
    {
        var document = Open(PagesSharingOneResourceDictionary());

        document.PruneUnusedResources();

        // The dictionary is one object shared by all three pages, so pruning one page by
        // reaching into it would leave the other two without the image they draw.
        XObjectsOf(document.Pages[0]).Should().Equal("/Im0");
        XObjectsOf(document.Pages[1]).Should().Equal("/Im1");
        XObjectsOf(document.Pages[2]).Should().Equal("/Im2");
    }

    [Fact]
    public void SplittingAfterPruningLeavesEachFileWithOneImage()
    {
        var sizes = Split(PagesSharingOneResourceDictionary(), prune: true);

        sizes.Should().OnlyContain(size => size < 2 * ImageLength);
    }

    [Fact]
    public void SplittingWithoutPruningStillCopiesEverything()
    {
        // The state of affairs the issue reports, and what makes the test above worth having.
        var sizes = Split(PagesSharingOneResourceDictionary(), prune: false);

        sizes.Should().OnlyContain(size => size > 3 * ImageLength);
    }

    [Fact]
    public void AFormWithoutResourcesKeepsWhatItDrawsWithFromThePage()
    {
        var document = Open(PageDrawingThroughAFormWithoutResources());

        document.PruneUnusedResources();

        // The form draws F1 and Im1; nobody draws F2 or Im2.
        FontsOf(document.Pages[0]).Should().Equal("/F1");
        XObjectsOf(document.Pages[0]).Should().Equal("/Fm0", "/Im1");
    }

    [Fact]
    public void AFormWithItsOwnResourcesDoesNotKeepThePageEntryOfTheSameName()
    {
        var document = Open(PageDrawingThroughAFormWithItsOwnResources());

        document.PruneUnusedResources();

        // The Im1 the form draws is its own, named by its own resources, not the page's.
        XObjectsOf(document.Pages[0]).Should().Equal("/Fm0");
    }

    [Fact]
    public void ASoftMaskWithoutResourcesKeepsWhatItPaintsWithFromThePage()
    {
        var document = Open(PageDrawingThroughASoftMaskWithoutResources());

        document.PruneUnusedResources();

        // The page draws nothing but the graphics state; the image is painted by the mask.
        // Dropping Im1 would leave the page naming a mask it cannot paint.
        XObjectsOf(document.Pages[0]).Should().Equal("/Im1");
    }

    [Fact]
    public void ASoftMaskWithItsOwnResourcesDoesNotKeepThePageEntryOfTheSameName()
    {
        var document = Open(PageDrawingThroughASoftMaskWithItsOwnResources());

        document.PruneUnusedResources();

        // The Im1 the mask paints is its own, named by its own resources, not the page's.
        XObjectsOf(document.Pages[0]).Should().BeEmpty();
    }

    [Fact]
    public void AGraphicsStateTurningTheSoftMaskOffPaintsNothing()
    {
        var document = Open(PageTurningTheSoftMaskOff());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().BeEmpty();
    }

    [Fact]
    public void APageWhoseSoftMaskCannotBeReadIsLeftAlone()
    {
        var document = Open(PageWhoseSoftMaskCannotBeRead());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Im1", "/Im2");
    }

    [Fact]
    public void AFormDrawingItselfIsReadOnceAndPrunedAllTheSame()
    {
        var document = Open(PageWithAFormDrawingItself());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Fm0");
    }

    [Fact]
    public void APageHoldingAnInlineImageIsLeftAlone()
    {
        var document = Open(PageWithAnInlineImage());

        document.PruneUnusedResources();

        // Im0 is drawn and Im1 is not, but reading over an inline image is guesswork, so
        // nothing is dropped on the strength of what was read after one.
        XObjectsOf(document.Pages[0]).Should().Equal("/Im0", "/Im1");
    }

    [Fact]
    public void APageWhoseContentCannotBeReadIsLeftAlone()
    {
        var document = Open(PageWhoseContentCannotBeRead());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Im0", "/Im1");
    }

    [Fact]
    public void ThePageReadsThePrunedResourcesAfterwards()
    {
        var document = Open(PagesSharingOneResourceDictionary());

        // A page reads its resources but once and keeps them, so a page asked for them before
        // being pruned would go on answering with the ones it started with.
        document.Pages[0].Resources.Elements.GetDictionary("/XObject").Elements.Count.Should().Be(3);

        document.PruneUnusedResources();

        document.Pages[0].Resources.Elements.GetDictionary("/XObject").Elements.Count.Should().Be(1);
    }

    [Fact]
    public void PruningTwiceChangesNothingTheSecondTime()
    {
        var document = Open(PagesSharingOneResourceDictionary());

        document.PruneUnusedResources();
        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Im0");
    }

    private static PdfDocument Open(byte[] document)
    {
        return Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);
    }

    private static IEnumerable<string> XObjectsOf(PdfPage page)
    {
        return NamesOf(page, "/XObject");
    }

    private static IEnumerable<string> FontsOf(PdfPage page)
    {
        return NamesOf(page, "/Font");
    }

    private static IEnumerable<string> NamesOf(PdfPage page, string category)
    {
        var entries = page.Elements.GetDictionary("/Resources").Elements.GetDictionary(category);
        return entries == null
            ? Enumerable.Empty<string>()
            : entries.Elements.KeyNames.Select(name => name.Value).OrderBy(name => name);
    }

    private static List<long> Split(byte[] document, bool prune)
    {
        var inputDocument = Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Import);

        var sizes = new List<long>();
        for (var i = 0; i < inputDocument.PageCount; i++)
        {
            var outputDocument = new PdfDocument();
            _ = outputDocument.AddPage(inputDocument.Pages[i]);
            if (prune)
                outputDocument.PruneUnusedResources();

            using var output = new MemoryStream();
            outputDocument.Save(output, false);
            sizes.Add(output.Length);
        }

        return sizes;
    }
}
