using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   Whether an outline entry arrives expanded, which is <c>/Count</c> and nothing else.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="PdfOutline.Opened"/> used to be inert. A reader takes an entry's state from
///     <c>/Count</c> — positive for open, negative for closed, absent when there is nothing to
///     expand — and <c>PrepareForSave</c> wrote that key only when an <c>OpenCount</c> field was
///     positive. The one thing that assigned it was <c>PdfOutlineCollection.Add</c>, walking the
///     new entry's ancestors, so it recorded the value an entry was constructed with, never saw an
///     <c>Opened</c> set afterwards, and was never undone by a removal. No entry with children
///     carried <c>/Count</c> at all, and every tree arrived collapsed however it had been built.
///   </para>
///   <para>
///     The count itself was also the wrong quantity: open <em>descendants</em>, where the
///     specification asks for descendants that would be <em>visible</em> — which stops at a closed
///     child rather than counting through it.
///   </para>
/// </remarks>
public class OutlineOpenStateTests
{
    [Fact]
    public void AnOpenedEntryWithChildrenCountsThemUp()
    {
        var document = ThreePages();
        var chapter = document.Outlines.Add("Chapter", document.Pages[0], opened: true);
        chapter.Outlines.Add("1.1", document.Pages[1]);
        chapter.Outlines.Add("1.2", document.Pages[2]);

        Save(document);

        // Two children, both leaves, and the entry is open: two rows appear under it.
        CountOf(chapter).Should().Be(2);
    }

    [Fact]
    public void AClosedEntryCountsTheSameNumberDownwards()
    {
        var document = ThreePages();
        var chapter = document.Outlines.Add("Chapter", document.Pages[0], opened: false);
        chapter.Outlines.Add("1.1", document.Pages[1]);
        chapter.Outlines.Add("1.2", document.Pages[2]);

        Save(document);

        // The magnitude says how many would appear if it were reopened; the sign says it is shut.
        CountOf(chapter).Should().Be(-2);
    }

    [Fact]
    public void OpenedSetAfterTheEntryWasAddedIsStillWritten()
    {
        var document = ThreePages();
        var chapter = document.Outlines.Add("Chapter", document.Pages[0], opened: false);
        chapter.Outlines.Add("1.1", document.Pages[1]);

        // The assignment the old bookkeeping could not see: it ran once, inside Add.
        chapter.Opened = true;

        Save(document);

        CountOf(chapter).Should().Be(1);
    }

    [Fact]
    public void AnEntryWithNoChildrenCarriesNoCount()
    {
        var document = ThreePages();
        var leaf = document.Outlines.Add("Leaf", document.Pages[0], opened: true);

        Save(document);

        leaf.Elements.ContainsKey("/Count").Should().BeFalse();
    }

    [Fact]
    public void AClosedChildHidesItsOwnDescendantsFromTheCountAbove()
    {
        var document = ThreePages();
        var chapter = document.Outlines.Add("Chapter", document.Pages[0], opened: true);

        var shut = chapter.Outlines.Add("1.1", document.Pages[1], opened: false);
        shut.Outlines.Add("1.1.1", document.Pages[2]);
        shut.Outlines.Add("1.1.2", document.Pages[2]);

        var open = chapter.Outlines.Add("1.2", document.Pages[1], opened: true);
        open.Outlines.Add("1.2.1", document.Pages[2]);

        Save(document);

        // 1.1 and 1.2 are visible; 1.2.1 is visible because 1.2 is open; the two under 1.1 are
        // not, because it is shut. A count of open descendants would have said five.
        CountOf(chapter).Should().Be(3);
    }

    [Fact]
    public void TheOutlineDictionaryCountsEveryRowAReaderWouldShow()
    {
        var document = ThreePages();

        var first = document.Outlines.Add("One", document.Pages[0], opened: true);
        first.Outlines.Add("1.1", document.Pages[1]);
        first.Outlines.Add("1.2", document.Pages[1]);

        var second = document.Outlines.Add("Two", document.Pages[1], opened: false);
        second.Outlines.Add("2.1", document.Pages[2]);

        using var reopened = SaveAndOpen(document);

        // Two top-level entries, plus the two under the open one. The closed one's child does
        // not show, and the root's count is never negative.
        var root = (PdfDictionary)reopened.Internals.Catalog.Elements.GetObject("/Outlines");
        root.Elements.GetInteger("/Count").Should().Be(4);
    }

    [Fact]
    public void OpenedSurvivesAReadAndAnotherSave()
    {
        var document = ThreePages();
        var open = document.Outlines.Add("Open", document.Pages[0], opened: true);
        open.Outlines.Add("1.1", document.Pages[1]);
        var shut = document.Outlines.Add("Shut", document.Pages[1], opened: false);
        shut.Outlines.Add("2.1", document.Pages[2]);

        using var once = SaveAndOpen(document);

        // Reading did not used to set Opened at all, so a document opened and saved again lost
        // every expanded branch it had.
        once.Outlines[0].Opened.Should().BeTrue();
        once.Outlines[1].Opened.Should().BeFalse();

        using var twice = SaveAndOpen(once);

        twice.Outlines[0].Opened.Should().BeTrue();
        twice.Outlines[1].Opened.Should().BeFalse();
        twice.Outlines[0].Elements.GetInteger("/Count").Should().Be(1);
        twice.Outlines[1].Elements.GetInteger("/Count").Should().Be(-1);
    }

    [Fact]
    public void ADeepChainCountsEveryLevelBeneathIt()
    {
        var document = ThreePages();

        // A chapter per page and a heading per section is the shape that used to cost the most:
        // the counts are now taken in one post-order pass rather than each level re-walking
        // everything below it, so this also pins the arithmetic that pass has to get right.
        const int Depth = 40;
        var chain = new List<PdfOutline>();
        var current = document.Outlines.Add("0", document.Pages[0], opened: true);
        chain.Add(current);

        for (var level = 1; level < Depth; level++)
        {
            current = current.Outlines.Add(level.ToString(), document.Pages[level % 3], opened: true);
            chain.Add(current);
        }

        Save(document);

        // Every entry is open, so each one shows everything beneath it: the deepest is a leaf
        // with no key at all, its parent shows one row, and so on up to the first.
        chain[Depth - 1].Elements.ContainsKey("/Count").Should().BeFalse();
        for (var level = 0; level < Depth - 1; level++)
            CountOf(chain[level]).Should().Be(Depth - 1 - level);
    }

    [Fact]
    public void ClosingOneLinkOfADeepChainHidesEverythingUnderIt()
    {
        var document = ThreePages();

        var top = document.Outlines.Add("top", document.Pages[0], opened: true);
        var middle = top.Outlines.Add("middle", document.Pages[1], opened: false);
        var bottom = middle.Outlines.Add("bottom", document.Pages[2], opened: true);
        bottom.Outlines.Add("leaf", document.Pages[0]);

        Save(document);

        // The shut link still carries its own subtree, because it has to know what to show when
        // it is opened - but the level above it counts only the shut entry itself.
        CountOf(top).Should().Be(1);
        CountOf(middle).Should().Be(-2);
        CountOf(bottom).Should().Be(1);
    }

    /// <summary>
    ///   Saves the document, which is what fills in <c>/Count</c>, and throws the bytes away. The
    ///   entries of the original are what get asserted against, because <c>PrepareForSave</c>
    ///   writes into the live dictionaries - so nothing has to be reopened to read them back.
    /// </summary>
    static void Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
    }

    static PdfDocument SaveAndOpen(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        // Fully qualified: PdfPinata.Test carries a PdfReader of its own, which wins here.
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    static int CountOf(PdfOutline outline)
    {
        return outline.Elements.GetInteger("/Count");
    }

    static PdfDocument ThreePages()
    {
        var document = new PdfDocument();
        var font = new XFont("Liberation Sans", 12);

        for (var page = 1; page <= 3; page++)
        {
            var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawString($"Page {page}", font, XBrushes.Black, 20, 50, XStringFormats.Default);
        }

        return document;
    }
}
