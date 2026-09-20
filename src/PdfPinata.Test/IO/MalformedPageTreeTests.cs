using System;
using System.IO;
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

    const string Page = "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>";

    static PdfDocument Read(byte[] document) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);

    /// <summary>
    ///   Opening it and then reaching for the pages, because the tree is walked when the catalog is
    ///   first asked for them rather than while the file is being read.
    /// </summary>
    static Action Opening(byte[] document) => () => _ = Read(document).Pages.Count;
}
