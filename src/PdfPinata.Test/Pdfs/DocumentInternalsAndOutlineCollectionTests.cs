using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfInternals"/> is the door into a document's object graph - the numbers, the
///   references and the objects themselves - and <see cref="PdfOutlineCollection"/> is the list of
///   bookmarks, which is a list and a tree at the same time.
///   <para>
///   Both are public API that the library itself barely uses, which is exactly why they are worth
///   pinning: a caller reaching for either is doing something the rest of the library does not do
///   for them, and the only thing standing between them and a malformed file is these methods
///   agreeing with the document about what belongs to it. Every one of them that takes an object
///   checks that, and every check has an error worth reading.
///   </para>
/// </summary>
public class DocumentInternalsAndOutlineCollectionTests
{
    private static PdfDocument ADocumentOf(int pages = 2)
    {
        var document = new PdfDocument();
        for (var idx = 0; idx < pages; idx++)
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 20, 20);
        }
        return document;
    }

    // ----- the objects a document is made of --------------------------------------------------

    [Fact]
    public void EveryIndirectObjectCanBeFoundByItsNumberAndAnsweredBackAsItself()
    {
        var document = ADocumentOf();
        var internals = document.Internals;
        var page = document.Pages[0];

        var reference = PdfInternals.GetReference(page);
        var objectId = PdfInternals.GetObjectID(page);

        reference.Should().NotBeNull();
        PdfInternals.GetObjectNumber(page).Should().Be(objectId.ObjectNumber);
        PdfInternals.GenerationNumber(page).Should().Be(objectId.GenerationNumber);
        internals.GetObject(objectId).Should().BeSameAs(page);
        internals.GetAllObjects().Should().Contain(page);
        internals.Catalog.Should().NotBeNull();
        internals.ExtGStateTable.Should().NotBeNull();
    }

    [Fact]
    public void AskingForTheNumbersOfNothingSaysSoRatherThanAnsweringZero()
    {
        var takingAReference = () => PdfInternals.GetReference(null);
        var takingAnId = () => PdfInternals.GetObjectID(null);
        var takingANumber = () => PdfInternals.GetObjectNumber(null);
        var takingAGeneration = () => PdfInternals.GenerationNumber(null);

        takingAReference.Should().Throw<ArgumentNullException>();
        takingAnId.Should().Throw<ArgumentNullException>();
        takingANumber.Should().Throw<ArgumentNullException>();
        takingAGeneration.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    ///   The document identifier is a sixteen-byte string in the trailer, and reading it as a
    ///   <see cref="Guid"/> is a convenience over exactly those bytes. Anything that is not sixteen
    ///   bytes long is not one, and answers the empty guid rather than throwing part-way through.
    /// </summary>
    [Fact]
    public void TheDocumentIdentifierIsReadableAsBytesOrAsAGuid()
    {
        var document = ADocumentOf(1);

        document.Internals.FirstDocumentID.Length.Should().Be(16);
        document.Internals.FirstDocumentGuid.Should().NotBe(Guid.Empty);
        document.Internals.SecondDocumentGuid.Should().Be(document.Internals.FirstDocumentGuid);

        document.Internals.SecondDocumentID = "not sixteen bytes at all, this one";
        document.Internals.SecondDocumentID.Should().Be("not sixteen bytes at all, this one");
    }

    /// <summary>
    ///   Creating an object through the document is how a caller gets one that is already numbered
    ///   and already in the table. It goes through the constructor taking a document, and a type
    ///   without one is refused with the reason rather than with a reflection failure.
    /// </summary>
    [Fact]
    public void AnObjectCreatedThroughTheDocumentIsAlreadyPartOfIt()
    {
        var document = ADocumentOf(1);

        var dictionary = document.Internals.CreateIndirectObject<PdfDictionary>();

        dictionary.Owner.Should().BeSameAs(document);
        dictionary.Reference.Should().NotBeNull();
        document.Internals.GetAllObjects().Should().Contain(dictionary);
    }

    [Fact]
    public void AnObjectCanBeAddedToADocumentAndTakenOutOfItAgain()
    {
        var document = ADocumentOf(1);
        var dictionary = new PdfDictionary();

        document.Internals.AddObject(dictionary);

        dictionary.Reference.Should().NotBeNull();
        document.Internals.GetAllObjects().Should().Contain(dictionary);

        document.Internals.RemoveObject(dictionary);

        document.Internals.GetAllObjects().Should().NotContain(dictionary);
    }

    [Fact]
    public void AnObjectFromAnotherDocumentIsRefusedRatherThanRenumbered()
    {
        var document = ADocumentOf(1);
        var elsewhere = ADocumentOf(1);
        var theirs = elsewhere.Pages[0];

        var adding = () => document.Internals.AddObject(theirs);
        var removing = () => document.Internals.RemoveObject(theirs);

        adding.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not belong to this document*");
        removing.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not belong to this document*");
    }

    [Fact]
    public void ADirectObjectCannotBeRemovedBecauseNothingIsHoldingItByNumber()
    {
        var document = ADocumentOf(1);

        var removing = () => document.Internals.RemoveObject(new PdfDictionary());

        removing.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only indirect objects*");
    }

    /// <summary>
    ///   The closure of an object is everything reachable from it, which is what has to travel with
    ///   it when it is copied into another document. A page's closure holds its own resources.
    /// </summary>
    [Fact]
    public void TheClosureOfAPageHoldsEverythingThePageReachesDownTo()
    {
        var document = ADocumentOf(1);
        var page = document.Pages[0];

        var closure = document.Internals.GetClosure(page);
        var shallow = document.Internals.GetClosure(page, 1);

        closure.Should().Contain(page);
        closure.Length.Should().BeGreaterThan(1);
        shallow.Should().Contain(page);
        shallow.Length.Should().BeLessThanOrEqualTo(closure.Length);
    }

    [Fact]
    public void AnObjectCanBeWrittenOutOnItsOwn()
    {
        var document = ADocumentOf(1);
        using var stream = new MemoryStream();

        document.Internals.WriteObject(stream, document.Pages[0]);

        stream.Length.Should().BeGreaterThan(0);
    }

    // ----- the bookmarks ----------------------------------------------------------------------

    [Fact]
    public void TheOutlineCollectionBehavesAsTheListItSaysItIs()
    {
        var document = ADocumentOf(3);
        var outlines = document.Outlines;

        var first = outlines.Add("first", document.Pages[0]);
        var second = outlines.Add("second", document.Pages[1], true);
        var third = outlines.Add("third", document.Pages[2], true, PdfOutlineStyle.Bold);

        outlines.Count.Should().Be(3);
        outlines.IsReadOnly.Should().BeFalse();
        outlines.Contains(second).Should().BeTrue();
        outlines.IndexOf(third).Should().Be(2);
        outlines[0].Should().BeSameAs(first);
        outlines.Select(outline => outline.Title).Should().Equal("first", "second", "third");

        var copied = new PdfOutline[3];
        outlines.CopyTo(copied, 0);
        copied.Should().Equal(first, second, third);
    }

    [Fact]
    public void AnOutlineCanBePutAtAGivenPlaceTakenOutOrReplaced()
    {
        var document = ADocumentOf();
        var outlines = document.Outlines;
        outlines.Add("first", document.Pages[0]);
        outlines.Add("second", document.Pages[1]);

        outlines.Insert(1, new PdfOutline("inserted", document.Pages[0]));
        outlines.Select(outline => outline.Title).Should().Equal("first", "inserted", "second");

        outlines.RemoveAt(1);
        outlines.Select(outline => outline.Title).Should().Equal("first", "second");

        outlines[1] = new PdfOutline("replaced", document.Pages[1]);
        outlines.Select(outline => outline.Title).Should().Equal("first", "replaced");

        outlines.Remove(outlines[1]).Should().BeTrue();
        outlines.Remove(new PdfOutline("never added", document.Pages[0])).Should().BeFalse();
        outlines.Count.Should().Be(1);

        outlines.Clear();
        outlines.Count.Should().Be(0);
    }

    [Fact]
    public void AnOutlineIndexOutsideTheListIsRefusedRatherThanReadPastTheEnd()
    {
        var document = ADocumentOf(1);
        var outlines = document.Outlines;
        outlines.Add("only", document.Pages[0]);

        var reading = () => outlines[1];
        var writing = () => outlines[1] = new PdfOutline("elsewhere", document.Pages[0]);
        var nulling = () => outlines[0] = null;
        var inserting = () => outlines.Insert(5, new PdfOutline("elsewhere", document.Pages[0]));
        var insertingNothing = () => outlines.Insert(0, null);

        reading.Should().Throw<ArgumentOutOfRangeException>();
        writing.Should().Throw<ArgumentOutOfRangeException>();
        nulling.Should().Throw<ArgumentOutOfRangeException>();
        inserting.Should().Throw<ArgumentOutOfRangeException>();
        insertingNothing.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    ///   A bookmark points at a page by reference, so one pointing at a page of another document
    ///   would be written as a reference into a file that has no such object. It is refused where
    ///   it is added rather than where it is written, which is the only place the two documents are
    ///   both in view.
    /// </summary>
    [Fact]
    public void AnOutlineOntoAnotherDocumentsPageIsRefused()
    {
        var document = ADocumentOf(1);
        var elsewhere = ADocumentOf(1);

        var adding = () => document.Outlines.Add("theirs", elsewhere.Pages[0]);

        adding.Should().Throw<ArgumentException>()
            .WithMessage("*must belong to this document*");
    }

    // ----- how a reader is asked to show the document -----------------------------------------

    /// <summary>
    ///   The viewer preferences say how a reader should present the document - which parts of its
    ///   own interface to hide, whether to fit the window to the page, and which way the pages
    ///   read. All of them are optional, and a reader that finds none does whatever it likes.
    /// </summary>
    [Fact]
    public void EveryViewerPreferenceSurvivesBeingWrittenAndReadBack()
    {
        var document = ADocumentOf(1);
        var preferences = document.ViewerPreferences;

        preferences.Direction.Should().BeNull();

        preferences.HideToolbar = true;
        preferences.HideMenubar = true;
        preferences.HideWindowUI = true;
        preferences.FitWindow = true;
        preferences.CenterWindow = true;
        preferences.DisplayDocTitle = true;
        preferences.Direction = PdfReadingDirection.RightToLeft;

        preferences.HideToolbar.Should().BeTrue();
        preferences.HideMenubar.Should().BeTrue();
        preferences.HideWindowUI.Should().BeTrue();
        preferences.FitWindow.Should().BeTrue();
        preferences.CenterWindow.Should().BeTrue();
        preferences.DisplayDocTitle.Should().BeTrue();

        var reopened = RoundTripped(document).ViewerPreferences;

        reopened.HideToolbar.Should().BeTrue();
        reopened.FitWindow.Should().BeTrue();
        reopened.DisplayDocTitle.Should().BeTrue();
    }

    /// <summary>
    ///   The reading direction reads back as what it was set to. It did not: a PDF name carries a
    ///   slash and the getter matched against the two names without one, so this was a property a
    ///   caller could set and never read - in the same document, or out of the file it wrote.
    /// </summary>
    [Theory]
    [InlineData(PdfReadingDirection.RightToLeft)]
    [InlineData(PdfReadingDirection.LeftToRight)]
    public void TheReadingDirectionIsReadBackAsWhateverItWasSetTo(PdfReadingDirection direction)
    {
        var document = ADocumentOf(1);

        document.ViewerPreferences.Direction = direction;

        document.ViewerPreferences.Direction.Should().Be(direction);
        RoundTripped(document).ViewerPreferences.Direction.Should().Be(direction);
    }

    [Fact]
    public void AReadingDirectionSetBackToNothingIsTakenOutOfTheDocument()
    {
        var document = ADocumentOf(1);
        document.ViewerPreferences.Direction = PdfReadingDirection.RightToLeft;

        document.ViewerPreferences.Direction = null;

        document.ViewerPreferences.Direction.Should().BeNull();
        RoundTripped(document).ViewerPreferences.Direction.Should().BeNull();
    }

    private static PdfDocument RoundTripped(PdfDocument document)
    {
        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }
}
