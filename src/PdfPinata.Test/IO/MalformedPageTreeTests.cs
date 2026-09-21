using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   What the reader does with a page tree that does not hold what a page tree holds.
/// </summary>
/// <remarks>
///   <c>PdfPages.GetKids</c> walks the tree, and read the <c>/Kids</c> of each node as an array or
///   else as a reference to one - with no third case. A <c>/Kids</c> that was neither dereferenced
///   null and threw a bare <see cref="NullReferenceException"/> from inside the reader, naming
///   nothing; so did a reference whose target was not an array, one line further on; and an entry in
///   the array that was not a reference threw <see cref="InvalidCastException"/> out of the
///   <c>foreach</c>. All three are reachable from a file, none of them said which object was wrong.
///
///   The line between the two answers here is whether the file failed to say something or said
///   something impossible. A node with no <c>/Kids</c> at all is read as a node with no children,
///   which is the tolerant reading already taken a few lines above for a node with no <c>/Type</c>.
///   A <c>/Kids</c> that is there but is not a list of pages is a file this method cannot walk, and
///   it says so, naming the node.
/// </remarks>
public class MalformedPageTreeTests
{
    [Fact]
    public void ANodeWithNoKidsAtAllIsANodeWithNoChildren()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Count 0>>"
        ]));

        document.Pages.Count.Should().Be(0);
    }

    /// <summary>
    ///   And the pages a tree does list are still read when one of its nodes lists none.
    /// </summary>
    [Fact]
    public void AnEmptyNodeCostsOnlyItsOwnSubtree()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 1>>",
            "<</Type/Pages/Parent 2 0 R/Count 0>>",
            Page
        ]));

        document.Pages.Count.Should().Be(1);
    }

    /// <summary>
    ///   The supported shape that the null dereference sat behind: <c>/Kids</c> as an indirect
    ///   reference to the array rather than the array itself.
    /// </summary>
    [Fact]
    public void KidsMayBeAnIndirectReferenceToTheArray()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids 4 0 R/Count 1>>",
            Page,
            "[3 0 R]"
        ]));

        document.Pages.Count.Should().Be(1);
    }

    [Theory]
    [InlineData("/Kids 42", "PdfInteger")]
    [InlineData("/Kids<</Type/Pages>>", "PdfDictionary")]
    [InlineData("/Kids/Identity", "PdfName")]
    public void AKidsEntryThatIsNotAnArrayNamesTheNodeItIsOn(string kids, string found)
    {
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages" + kids + "/Count 1>>",
            Page
        ]));

        opening.Should().Throw<PdfReaderException>()
            .WithMessage("*/Kids*2 0*" + found + "*array*");
    }

    [Fact]
    public void AKidsReferenceToSomethingThatIsNotAnArrayNamesTheNodeToo()
    {
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids 4 0 R/Count 1>>",
            Page,
            "42"
        ]));

        opening.Should().Throw<PdfReaderException>().WithMessage("*/Kids*2 0*array*");
    }

    [Fact]
    public void AKidThatIsNotAReferenceNamesTheNodeItIsIn()
    {
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[<</Type/Page/MediaBox[0 0 200 100]>>]/Count 1>>"
        ]));

        opening.Should().Throw<PdfReaderException>()
            .WithMessage("*/Kids*2 0*indirect reference*");
    }

    [Fact]
    public void AKidThatRefersToSomethingOtherThanADictionaryNamesTheObject()
    {
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "42"
        ]));

        opening.Should().Throw<PdfReaderException>()
            .WithMessage("*3 0*page tree*dictionary*");
    }

    /// <summary>
    ///   A <c>/Kids</c> whose reference the file never defines. ISO 32000-1 7.3.9 makes such a
    ///   reference the null object and a null entry the same as no entry, which is what
    ///   <c>DanglingReferenceTests</c> covers for the rest of a page - so it is read here as a node
    ///   with no children rather than as a node that cannot be walked.
    /// </summary>
    [Fact]
    public void AKidsReferenceTheFileNeverDefinesIsNoKidsAtAll()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids 9 0 R/Count 1>>"
        ]));

        document.Pages.Count.Should().Be(0);
    }

    /// <summary>The same rule with the null spelled out rather than dangled into.</summary>
    [Fact]
    public void AKidsEntryWrittenAsNullIsNoKidsEither()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids null/Count 1>>"
        ]));

        document.Pages.Count.Should().Be(0);
    }

    /// <summary>
    ///   A well-formed tree, so that what these tests report as broken is broken and not merely
    ///   hand-written.
    /// </summary>
    [Fact]
    public void AWellFormedTreeIsStillWalked()
    {
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page,
            Page
        ]));

        document.Pages.Count.Should().Be(2);
    }

    // ----- a page tree that is not a tree -----
    //
    // ISO 32000-1 7.7.3.2 has the pages of a document in a tree, and this walk believed it. A file
    // whose /Kids lead back where they came from recursed until the stack ran out — and a stack
    // overflow cannot be caught, so the process went with it. Opening the file was enough: the
    // catalog asks for the pages while PdfReader.Open is still running. That is
    // https://github.com/empira/PDFsharp/issues/361, reported against a file from a corpus of
    // documents written to make readers loop.

    [Fact]
    public void ANodeThatListsTheNodeAboveItIsALoopRatherThanADeeperTree()
    {
        // The reported shape: 2 lists 3, and 3 lists 2 straight back.
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Pages/Kids[2 0 R]/Count 1>>"
        ]));

        opening.Should().Throw<PdfReaderException>().WithMessage("*2 0*loop*tree*");
    }

    [Fact]
    public void ANodeThatListsItselfIsTheSameAnswer()
    {
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[2 0 R]/Count 1>>"
        ]));

        opening.Should().Throw<PdfReaderException>().WithMessage("*2 0*loop*");
    }

    [Fact]
    public void ALoopIsFoundWithRealPagesAroundIt()
    {
        // The corpus has this one too, and it is the one that says the walk is watched all the way
        // down rather than at the root: the first branch is a page and reads fine, and the loop is
        // in the second.
        var opening = Opening(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page,
            "<</Type/Pages/Kids[5 0 R]/Count 1>>",
            "<</Type/Pages/Kids[4 0 R]/Count 1>>"
        ]));

        opening.Should().Throw<PdfReaderException>().WithMessage("*4 0*loop*");
    }

    [Fact]
    public void ATreeNestedDeeperThanTheStackCanHoldIsRefusedByName()
    {
        // No loop here and nothing repeats, so the ancestors alone would not have saved it. Two
        // thousand nodes one inside the next, which is past the eighteen hundred frames the walk
        // managed before the stack gave out - so this file killed the process too, without a
        // single object being listed twice.
        var objects = new List<string> { "<</Type/Catalog/Pages 2 0 R>>" };
        for (var id = 2; id <= 2000; id++)
            objects.Add("<</Type/Pages/Kids[" + (id + 1) + " 0 R]/Count 1>>");
        objects.Add(Page);

        var opening = Opening(RawPdf.Build(objects));

        opening.Should().Throw<PdfReaderException>().WithMessage("*levels deep*");
    }

    [Fact]
    public void ANodeThatTwoParentsListIsNotALoop()
    {
        // A subtree hanging in two places is a page counted twice: malformed, and something this
        // walk ends on. So it is read rather than refused, which is what makes the question asked
        // of each node "am I inside myself" rather than "have I been seen before".
        var document = Read(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            "<</Type/Pages/Kids[5 0 R]/Count 1>>",
            "<</Type/Pages/Kids[5 0 R]/Count 1>>",
            "<</Type/Pages/Kids[6 0 R]/Count 1>>",
            Page
        ]));

        document.Pages.Count.Should().Be(2);
    }

    [Fact(Timeout = 30_000)]
    public async Task ANodeListedTwiceAtEveryLevelIsRefusedRatherThanWalkedForever()
    {
        // No loop and nothing deep, just every node listed twice by its parent - which the test
        // above reads, once. Forty levels of it are two to the forty nodes to walk in a file of
        // forty-odd objects, which ends in principle and never in practice.
        var objects = new List<string> { "<</Type/Catalog/Pages 2 0 R>>" };
        for (var id = 2; id <= 41; id++)
            objects.Add("<</Type/Pages/Kids[" + (id + 1) + " 0 R " + (id + 1) + " 0 R]/Count 1>>");
        objects.Add(Page);

        var opening = Opening(RawPdf.Build(objects));

        await Task.Run(() => opening.Should().Throw<PdfReaderException>().WithMessage("*over and over*"));
    }

    const string Page = "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>";

    static PdfDocument Read(byte[] document) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);

    /// <summary>
    ///   Opening it and then reaching for the pages, because the tree is walked when the catalog is
    ///   first asked for them rather than while the file is being read.
    /// </summary>
    static Action Opening(byte[] document) => () => _ = Read(document).Pages.Count;
}
