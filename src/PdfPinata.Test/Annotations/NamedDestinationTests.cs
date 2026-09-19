using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfIO = PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   Named destinations, and links that follow them. A name outlives the page it stands for, which
///   a page number does not: insert a page in front of page 7 and every link to page 7 is wrong,
///   while every link to "chapter-3" is still right.
///   <para>
///   The library could read these - <see cref="PdfNamedDestinations"/> resolves one when a page
///   carrying it is imported - and had no way at all to write one.
///   </para>
/// </summary>
public class NamedDestinationTests
{
    static PdfDocument TwoPageDocument()
    {
        var document = new PdfDocument();
        for (var i = 0; i < 2; i++)
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString("page " + (i + 1), new XFont("Arial", 12), XBrushes.Black, 20, 40);
        }
        return document;
    }

    /// <summary>Saves and reads back, which is the only way to see what was written.</summary>
    static PdfDocument RoundTrip(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfIO.PdfReader.Open(stream, PdfIO.PdfDocumentOpenMode.Modify);
    }

    static PdfArray LookupOn(PdfDocument document, string name)
    {
        // The reader the library already had, pointed at what the writer just produced.
        return document.NamedDestinations.Resolve(name);
    }

    // ----- E2, naming a destination --------------------------------------------------------------

    [Fact]
    public void ANameSurvivesBeingWrittenAndReadBack()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("chapter-2", document.Pages[1]);

        LookupOn(RoundTrip(document), "chapter-2").Should().NotBeNull();
    }

    [Fact]
    public void ANameStandsForThePageItWasGiven()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("first", document.Pages[0]);
        document.NamedDestinations.Add("second", document.Pages[1]);

        var reopened = RoundTrip(document);

        // The first element of a destination array is the page it points at.
        var first = LookupOn(reopened, "first");
        var second = LookupOn(reopened, "second");
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first.Elements[0].Should().NotBe(second.Elements[0]);
    }

    [Fact]
    public void APlaceOnAPageIsKeptAlongWithThePage()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("halfway", document.Pages[1], 400);

        var destination = LookupOn(RoundTrip(document), "halfway");

        // page, /XYZ, left, top, zoom - and the top is the one that was asked for.
        destination.Elements.Count.Should().Be(5);
        destination.Elements.GetReal(3).Should().BeApproximately(400, 0.01);
    }

    [Fact]
    public void ANameWithNoPlaceLeavesTheReaderWhereItIs()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("somewhere", document.Pages[1]);

        var destination = LookupOn(RoundTrip(document), "somewhere");

        // A null top is what says "wherever the page is already scrolled to".
        destination.Elements[3].Should().BeOfType<PdfNull>();
    }

    [Fact]
    public void NamesAreWrittenInTheOrderANameTreeHasToBeIn()
    {
        var document = TwoPageDocument();
        foreach (var name in new[] { "zebra", "apple", "mango" })
            document.NamedDestinations.Add(name, document.Pages[0]);

        document.NamedDestinations.Names.Should().Equal("apple", "mango", "zebra");

        // And all three are still findable once written, which sorting is what makes possible.
        var reopened = RoundTrip(document);
        foreach (var name in new[] { "apple", "mango", "zebra" })
            LookupOn(reopened, name).Should().NotBeNull(name + " should be findable");
    }

    [Fact]
    public void NamingTheSamePlaceTwiceReplacesTheFirst()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("here", document.Pages[0]);
        document.NamedDestinations.Add("here", document.Pages[1], 100);

        document.NamedDestinations.Count.Should().Be(1);
        LookupOn(RoundTrip(document), "here").Elements.GetReal(3).Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public void ANameCanBeTakenBack()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("here", document.Pages[0]);

        document.NamedDestinations.Remove("here").Should().BeTrue();
        document.NamedDestinations.Remove("here").Should().BeFalse();
        document.NamedDestinations.Contains("here").Should().BeFalse();
    }

    [Fact]
    public void ADocumentThatNamesNothingWritesNoNameTree()
    {
        var reopened = RoundTrip(TwoPageDocument());

        // Nothing was asked for, so nothing is added to the catalog.
        var names = reopened.Internals.Catalog.Elements.GetDictionary("/Names");
        (names == null || names.Elements.GetDictionary("/Dests") == null).Should().BeTrue();
    }

    [Fact]
    public void ADestinationMustBeNamedSomething()
    {
        var document = TwoPageDocument();

        document.Invoking(d => d.NamedDestinations.Add("", d.Pages[0]))
            .Should().Throw<ArgumentException>();
        document.Invoking(d => d.NamedDestinations.Add("here", null))
            .Should().Throw<ArgumentNullException>();
    }

    // ----- what a second save must not lose ------------------------------------------------------

    [Fact]
    public void NamingOneMoreDestinationKeepsTheOnesTheDocumentCameWith()
    {
        var first = TwoPageDocument();
        first.NamedDestinations.Add("chapter-1", first.Pages[0]);
        first.NamedDestinations.Add("chapter-2", first.Pages[1], 300);

        // Open what was written, name one more, and write it again.
        var second = RoundTrip(first);
        second.NamedDestinations.Add("chapter-3", second.Pages[1], 100);
        var third = RoundTrip(second);

        // The table writes the tree whole, so the names it was not told about have to be taken in
        // before it does - or every one of them is dropped, silently, on the second save.
        LookupOn(third, "chapter-1").Should().NotBeNull();
        LookupOn(third, "chapter-2").Should().NotBeNull();
        LookupOn(third, "chapter-3").Should().NotBeNull();
    }

    [Fact]
    public void ADestinationTheDocumentCameWithKeepsItsPlace()
    {
        var first = TwoPageDocument();
        first.NamedDestinations.Add("chapter-2", first.Pages[1], 300);

        var second = RoundTrip(first);
        second.NamedDestinations.Add("chapter-3", second.Pages[0]);

        // Written back as it was found, rather than rebuilt from what this library can express.
        LookupOn(RoundTrip(second), "chapter-2").Elements.GetReal(3).Should().BeApproximately(300, 0.01);
    }

    [Fact]
    public void ANameTakenBackIsGoneFromTheDocumentToo()
    {
        var first = TwoPageDocument();
        first.NamedDestinations.Add("temporary", first.Pages[0]);
        first.NamedDestinations.Add("permanent", first.Pages[1]);

        var second = RoundTrip(first);
        second.NamedDestinations.Remove("temporary").Should().BeTrue();
        var third = RoundTrip(second);

        LookupOn(third, "temporary").Should().BeNull();
        LookupOn(third, "permanent").Should().NotBeNull();
    }

    [Fact]
    public void TakingBackEveryNameLeavesNoTreeBehind()
    {
        var first = TwoPageDocument();
        first.NamedDestinations.Add("only", first.Pages[0]);

        var second = RoundTrip(first);
        second.NamedDestinations.Remove("only").Should().BeTrue();

        LookupOn(RoundTrip(second), "only").Should().BeNull();
    }

    [Fact]
    public void ContainsAnswersForTheNamesTheDocumentCameWith()
    {
        var first = TwoPageDocument();
        first.NamedDestinations.Add("chapter-1", first.Pages[0]);

        RoundTrip(first).NamedDestinations.Contains("chapter-1").Should().BeTrue();
    }

    [Fact]
    public void APageOfAnotherDocumentIsRejected()
    {
        var document = TwoPageDocument();
        var other = TwoPageDocument();

        // Its object number belongs to the other document's table, so a reference to it here would
        // point at whatever this document holds under that number, or at nothing.
        document.Invoking(d => d.NamedDestinations.Add("elsewhere", other.Pages[0]))
            .Should().Throw<ArgumentException>();
    }

    // ----- E3, linking to a name -----------------------------------------------------------------

    [Fact]
    public void ALinkToANameIsWrittenAsAStringDestination()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("chapter-2", document.Pages[1]);
        document.Pages[0].AddNamedLink(new PdfRectangle(new XRect(20, 20, 100, 20)), "chapter-2");

        var reopened = RoundTrip(document);
        var annotation = reopened.Pages[0].Annotations[0];

        // A string sends the reader to the /Names /Dests tree; a name would send it to the /Dests
        // dictionary of PDF 1.1, which is not where this was written.
        annotation.Elements["/Dest"].Should().BeOfType<PdfString>();
        ((PdfString)annotation.Elements["/Dest"]).Value.Should().Be("chapter-2");
    }

    [Fact]
    public void ALinkFindsTheDestinationItNames()
    {
        var document = TwoPageDocument();
        document.NamedDestinations.Add("chapter-2", document.Pages[1], 500);
        document.Pages[0].AddNamedLink(new PdfRectangle(new XRect(20, 20, 100, 20)), "chapter-2");

        var reopened = RoundTrip(document);
        var name = ((PdfString)reopened.Pages[0].Annotations[0].Elements["/Dest"]).Value;

        // The two halves meet: what the link says, the name tree answers.
        LookupOn(reopened, name).Should().NotBeNull();
    }

    [Fact]
    public void ANameOutsideWinAnsiSurvivesBothHalves()
    {
        const string name = "kapitel-über-2";

        var document = TwoPageDocument();
        document.NamedDestinations.Add(name, document.Pages[1]);
        document.Pages[0].AddNamedLink(new PdfRectangle(new XRect(20, 20, 100, 20)), name);

        var reopened = RoundTrip(document);

        // The tree and the link have to encode the name the same way. Pinning one end to WinAnsi
        // turned every character outside it into a question mark, and the link then pointed at a
        // name the document did not hold.
        var linked = ((PdfString)reopened.Pages[0].Annotations[0].Elements["/Dest"]).Value;
        linked.Should().Be(name);
        LookupOn(reopened, linked).Should().NotBeNull();
    }

    [Fact]
    public void ALinkMustNameSomething()
    {
        var rect = new PdfRectangle(new XRect(20, 20, 100, 20));

        ((Action)(() => PdfLinkAnnotation.CreateNamedLink(rect, null)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => PdfLinkAnnotation.CreateNamedLink(rect, "")))
            .Should().Throw<ArgumentException>();
    }

    // ----- E1, linking what was drawn ------------------------------------------------------------

    [Fact]
    public void AnAreaOfTheDrawingCanBeLinkedToTheWeb()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 12);
            gfx.DrawString("Anthropic", font, XBrushes.Blue, 20, 40);
            var width = gfx.MeasureString("Anthropic", font).Width;

            // The rectangle is given in the coordinates the text was drawn in, which is the whole
            // of what was missing - an annotation is placed from the bottom of the page instead.
            gfx.AddWebLink(new XRect(20, 40 - font.GetHeight(), width, font.GetHeight()),
                "https://www.anthropic.com");
        }

        page.Annotations.Count.Should().Be(1);

        var reopened = RoundTrip(document);
        var annotation = reopened.Pages[0].Annotations[0];
        annotation.Elements.GetDictionary("/A").Elements["/URI"].ToString().Should().Contain("anthropic.com");
    }

    [Fact]
    public void ALinkedAreaIsPlacedFromTheBottomOfThePage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.AddWebLink(new XRect(20, 40, 100, 20), "https://example.com");

        var rect = page.Annotations[0].Elements.GetRectangle("/Rect");

        // Drawn 40 points down from the top, so the annotation sits that far up from the bottom.
        rect.Y2.Should().BeApproximately(page.Height.Point - 40, 0.01);
        rect.X1.Should().BeApproximately(20, 0.01);
    }

    [Fact]
    public void APlaceInTheDrawingCanBeNamedAndLinkedTo()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(second))
            gfx.AddNamedDestination("chapter-2", new XPoint(0, 200));

        using (var gfx = XGraphics.FromPdfPage(first))
            gfx.AddNamedLink(new XRect(20, 40, 100, 20), "chapter-2");

        var reopened = RoundTrip(document);
        var destination = LookupOn(reopened, "chapter-2");

        destination.Should().NotBeNull();
        // 200 points down the page from the top is that far up from the bottom.
        destination.Elements.GetReal(3).Should().BeApproximately(second.Height.Point - 200, 0.01);
    }

    [Fact]
    public void LinkingNeedsAPageToLinkOn()
    {
        // An XGraphics that draws somewhere other than a PDF page has nowhere to put an annotation.
        var document = new PdfDocument();
        document.AddPage();
        var form = new PdfPinata.Drawing.XForm(document, XUnit.FromPoint(100), XUnit.FromPoint(100));
        using var gfx = XGraphics.FromForm(form);

        gfx.Invoking(g => g.AddWebLink(new XRect(0, 0, 10, 10), "https://example.com"))
            .Should().Throw<InvalidOperationException>();
    }
}
