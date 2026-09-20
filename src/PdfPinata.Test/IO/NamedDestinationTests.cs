using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.NamedDestinationFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   A link can name where it goes instead of saying it outright, leaving the document catalog
///   to hold what the name stands for. Importing a page brings no catalog with it, so a name
///   used to arrive standing for nothing: the link kept a destination naming something that was
///   nowhere in the file, and clicking it did nothing. Merging a document whose cross references
///   worked gave one whose cross references did not, even with every page of it imported.
///   <para>
///   The name is resolved against the document the page came from while that is still at hand,
///   and what it stands for is written in its place, so the destination that arrives carries
///   itself. Documents that are merged do not have to agree about what their names mean, which
///   they would if the names were kept.
///   </para>
/// </summary>
public class NamedDestinationTests
{
    [Fact]
    public void MergingResolvesADestinationNamedByStringInTheNameTree()
    {
        using var output = Merge(InNameTree(LinkToName()));

        GoesToTheSecondPage(output);
    }

    [Fact]
    public void MergingResolvesADestinationHeldInADictionaryOfItsOwn()
    {
        using var output = Merge(InNameTreeUnderD(LinkToName()));

        GoesToTheSecondPage(output);
    }

    [Fact]
    public void MergingResolvesADestinationHeldDownTheNameTree()
    {
        using var output = Merge(InNameTreeWithKids(LinkToName()));

        GoesToTheSecondPage(output);
    }

    [Fact]
    public void MergingResolvesADestinationNamedByNameInTheDestsDictionary()
    {
        using var output = Merge(InDestsDictionary(LinkToNameObject()));

        GoesToTheSecondPage(output);
    }

    [Fact]
    public void MergingResolvesTheNamedDestinationOfAGoToAction()
    {
        using var output = Merge(InNameTree(LinkWithAction("/S/GoTo/D(" + Name + ")")));

        GoesToTheSecondPage(output);
    }

    /// <summary>
    ///   Where on the page to go is what the destination says beyond which page, and it has to
    ///   arrive along with it or the link lands somewhere else on the right page.
    /// </summary>
    [Fact]
    public void ResolvingADestinationKeepsWhereOnThePageItGoes()
    {
        using var output = Merge(InNameTree(LinkToName()));

        var destination = DestinationOf(output);
        destination.Elements.GetName(1).Should().Be("/XYZ");
        destination.Elements.GetInteger(2).Should().Be(11);
        destination.Elements.GetInteger(3).Should().Be(22);
    }

    /// <summary>
    ///   The counterpart of the split case for explicit destinations: the page the link goes to
    ///   is left behind, so the link is left without an aim rather than with one that stands for
    ///   nothing.
    /// </summary>
    [Fact]
    public void ImportingOnlyTheLinkingPageDropsTheDestination()
    {
        using var output = Import(InNameTree(LinkToName()), 1);

        var annotation = AnnotationsOf(output).Elements.GetDictionary(0);
        annotation.Elements.ContainsKey("/Dest").Should().BeFalse();

        // And the page it named was left behind rather than dragged in by the name.
        output.Length.Should().BeLessThan(2 * ImageLength);
    }

    [Fact]
    public void ImportingOnlyTheLinkingPageDropsTheActionThatHasNowhereToGo()
    {
        using var output = Import(InNameTree(LinkWithAction("/S/GoTo/D(" + Name + ")")), 1);

        var annotation = AnnotationsOf(output).Elements.GetDictionary(0);
        annotation.Elements.ContainsKey("/A").Should().BeFalse();
    }

    /// <summary>
    ///   A destination going into another file is for that file to resolve. Reading it as one of
    ///   this document would point the link at whatever this document happens to call that.
    /// </summary>
    [Fact]
    public void ARemoteGoToKeepsTheNameItWasWrittenWith()
    {
        using var output = Merge(InNameTree(
            LinkWithAction("/S/GoToR/F(other.pdf)/D(" + Name + ")")));

        var action = AnnotationsOf(output).Elements.GetDictionary(0).Elements.GetDictionary("/A");
        action.Elements.GetName("/S").Should().Be("/GoToR");
        action.Elements.GetString("/D").Should().Be(Name);
    }

    /// <summary>
    ///   A name the catalog does not hold cannot be resolved and cannot be shown to be wrong
    ///   either, so it is left as it was written.
    /// </summary>
    [Fact]
    public void ANameThatStandsForNothingIsLeftAlone()
    {
        using var output = Merge(WithNothingHeld(LinkToName()));

        AnnotationsOf(output).Elements.GetDictionary(0)
            .Elements.GetString("/Dest").Should().Be(Name);
    }

    /// <summary>
    ///   Nothing about a link that says where it goes changes, which is the behaviour the
    ///   destination tests of SplitTests and InsertRangeTests are about.
    /// </summary>
    [Fact]
    public void MergingStillResolvesADestinationThatSaysWhereItGoes()
    {
        using var output = Merge(WithNothingHeld(Link("/Dest[4 0 R/Fit]")));

        GoesToTheSecondPage(output);
    }

    [Fact]
    public void MergingResolvesEveryNamedDestinationOfThePage()
    {
        using var output = Merge(InNameTree(
            LinkToName(), LinkWithAction("/S/GoTo/D(" + Name + ")"), LinkToName()));

        var annotations = AnnotationsOf(output);
        annotations.Elements.Count.Should().Be(3);
        for (var idx = 0; idx < 3; idx++)
            PageOf(annotations.Elements.GetDictionary(idx)).Should().Be(SecondPageOf(output));
    }

    /// <summary>
    ///   The whole document, which is the case the name is meant to be resolvable in.
    /// </summary>
    private static MemoryStream Merge(byte[] document)
    {
        return Import(document, 2);
    }

    private static MemoryStream Import(byte[] document, int pageCount)
    {
        using var input = new MemoryStream(document);
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        for (var idx = 0; idx < pageCount; idx++)
            _ = target.AddPage(source.Pages[idx]);

        var output = new MemoryStream();
        target.Save(output, false);
        return output;
    }

    /// <summary>
    ///   The link of the first page goes to the second page of the document written out.
    /// </summary>
    private static void GoesToTheSecondPage(MemoryStream document)
    {
        PageOf(AnnotationsOf(document).Elements.GetDictionary(0))
            .Should().Be(SecondPageOf(document));
    }

    private static PdfArray AnnotationsOf(MemoryStream document)
    {
        document.Position = 0;
        var page = Pdf.IO.PdfReader.Open(document, PdfDocumentOpenMode.Modify).Pages[0];
        return page.Elements.GetArray("/Annots");
    }

    private static PdfObjectID SecondPageOf(MemoryStream document)
    {
        document.Position = 0;
        var reread = Pdf.IO.PdfReader.Open(document, PdfDocumentOpenMode.Modify);
        reread.PageCount.Should().Be(2);
        return PdfInternals.GetObjectID(reread.Pages[1]);
    }

    private static PdfObjectID PageOf(PdfDictionary annotation)
    {
        return ((PdfReference)DestinationOf(annotation).Elements[0]).ObjectID;
    }

    private static PdfArray DestinationOf(MemoryStream document)
    {
        return DestinationOf(AnnotationsOf(document).Elements.GetDictionary(0));
    }

    /// <summary>
    ///   The destination of a link, wherever in the annotation it is held.
    /// </summary>
    private static PdfArray DestinationOf(PdfDictionary annotation)
    {
        return annotation.Elements.GetArray("/Dest")
               ?? annotation.Elements.GetDictionary("/A").Elements.GetArray("/D");
    }
}
