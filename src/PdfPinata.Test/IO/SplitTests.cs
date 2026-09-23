using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.ImportedPageFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   A link annotation names the page it goes to by an indirect reference, so importing one used
///   to copy that page, and with it every page reachable through the page tree. Splitting a
///   document therefore produced files as large as the document they came from.
///   See https://github.com/ststeiger/PdfSharpCore/issues/461.
/// </summary>
public class SplitTests
{
    /// <summary>
    ///   The destination of the link on the first page. It names the second page in all of them,
    ///   the difference being where in the annotation the destination sits.
    /// </summary>
    public static TheoryData<string> Destinations =>
    [
        "/Dest[4 0 R/Fit]",                 // A destination on the annotation.
        "/A<</S/GoTo/D[4 0 R/Fit]>>",       // A go-to action.
        "/P 3 0 R/Dest[4 0 R/Fit]" // Both, and a page back reference.
    ];

    [Theory]
    [MemberData(nameof(Destinations))]
    public void SplittingAPageThatLinksToAnotherOneLeavesThatPageBehind(string destination)
    {
        var pages = Split(LinkedPagesDocument(Link(destination)));

        // Every page draws one image, so a page that took another one with it is twice the size.
        pages.Should().OnlyContain(page => page.Length < 2 * ImageLength);
    }

    [Theory]
    [MemberData(nameof(Destinations))]
    public void SplittingAPageDropsTheLinkThatHasNowhereToGo(string destination)
    {
        var page = Split(LinkedPagesDocument(Link(destination)))[0];

        var annotation = AnnotationsOf(page, 0).Elements.GetDictionary(0);
        annotation.Elements.ContainsKey("/Dest").Should().BeFalse();
        annotation.Elements.ContainsKey("/A").Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Destinations))]
    public void MergingKeepsALinkPointingAtThePageItGoesTo(string destination)
    {
        using var input = new MemoryStream(LinkedPagesDocument(Link(destination)));
        var inputDocument = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var merged = new PdfDocument();
        foreach (var page in inputDocument.Pages)
            _ = merged.AddPage(page);

        using var output = new MemoryStream();
        merged.Save(output, false);

        // The link goes forward, to a page that was not imported when the link itself was.
        var reread = Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        reread.PageCount.Should().Be(3);
        DestinationOf(AnnotationsOf(output, 0).Elements.GetDictionary(0)).ObjectID
            .Should().Be(PdfInternals.GetObjectID(reread.Pages[1]));

        // And the page it goes to is the imported page, not a second copy of it.
        output.Length.Should().BeLessThan(4 * ImageLength);
    }

    /// <summary>
    ///   The annotations of a page of a document that has been written out.
    /// </summary>
    internal static PdfArray AnnotationsOf(MemoryStream document, int pageIndex)
    {
        document.Position = 0;
        var page = Pdf.IO.PdfReader.Open(document, PdfDocumentOpenMode.Modify).Pages[pageIndex];
        return page.Elements.GetArray("/Annots");
    }

    /// <summary>
    ///   The page a link goes to, wherever in the annotation the destination is held.
    /// </summary>
    internal static PdfReference DestinationOf(PdfDictionary annotation)
    {
        var array = annotation.Elements.GetArray("/Dest")
                    ?? annotation.Elements.GetDictionary("/A").Elements.GetArray("/D");
        return (PdfReference)array.Elements[0];
    }

    /// <summary>
    ///   Writes every page of the document to a file of its own, the way the issue does it.
    /// </summary>
    private static List<MemoryStream> Split(byte[] document)
    {
        using var input = new MemoryStream(document);
        var inputDocument = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var pages = new List<MemoryStream>();
        for (var i = 0; i < inputDocument.PageCount; i++)
        {
            var outputDocument = new PdfDocument();
            _ = outputDocument.AddPage(inputDocument.Pages[i]);

            var output = new MemoryStream();
            outputDocument.Save(output, false);
            pages.Add(output);
        }

        return pages;
    }
}
