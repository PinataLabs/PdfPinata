using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   An outline entry is in one list at a time. It has one <c>/Parent</c>, one <c>/Prev</c> and one
///   <c>/Next</c>, so an entry in two lists - or twice in one - is a tree the file cannot describe.
/// </summary>
/// <remarks>
///   <para>
///     <c>PdfOutlineCollection</c> used to accept any entry at all: added twice to one list it
///     wrote an entry whose <c>/Next</c> was itself, which a reader walks for ever; added to a second
///     parent it stayed in the first list with <c>/Parent</c> naming the second; added under itself
///     it overflowed the stack on save. Removal left the links of the entries beside it as they
///     were, so once a document had been saved or read, removing an entry left its old sibling's
///     <c>/Next</c> - or its parent's <c>/First</c> - pointing at it, and the save wrote it back.
///   </para>
///   <para>
///     Every assertion about the written tree reads the file back and walks the raw dictionaries,
///     checking each link against the walk, because the object model is what was wrong.
///   </para>
/// </remarks>
public class OutlineTreeMembershipTests
{
    // ── What is refused ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AnEntryAlreadyInTheListIsRefused()
    {
        var document = ADocumentOf(1);
        var entry = document.Outlines.Add("a", document.Pages[0]);

        var adding = () => document.Outlines.Add(entry);

        adding.Should().Throw<InvalidOperationException>().WithMessage("*already in an outline collection*");
        document.Outlines.Count.Should().Be(1);
        Tree(RoundTripped(document)).Should().Be("a");
    }

    [Fact]
    public void AnEntryUnderAnotherParentIsRefusedHoweverItIsPut()
    {
        var document = ADocumentOf(1);
        var a = document.Outlines.Add("a", document.Pages[0]);
        var b = document.Outlines.Add("b", document.Pages[0]);
        b.Outlines.Add("b1", document.Pages[0]);
        var x = a.Outlines.Add("x", document.Pages[0]);

        var adding = () => b.Outlines.Add(x);
        var inserting = () => b.Outlines.Insert(0, x);
        var replacing = () => b.Outlines[0] = x;

        adding.Should().Throw<InvalidOperationException>();
        inserting.Should().Throw<InvalidOperationException>();
        replacing.Should().Throw<InvalidOperationException>();
        x.Parent.Should().BeSameAs(a);
        Tree(RoundTripped(document)).Should().Be("a(x),b(b1)");
    }

    [Fact]
    public void AnEntryCannotBePutUnderItselfOrUnderAnythingBelowIt()
    {
        var document = ADocumentOf(1);
        var a = document.Outlines.Add("a", document.Pages[0]);
        var b = a.Outlines.Add("b", document.Pages[0]);
        document.Outlines.Remove(a);

        var underItself = () => a.Outlines.Add(a);
        var underItsChild = () => b.Outlines.Add(a);

        underItself.Should().Throw<InvalidOperationException>().WithMessage("*under itself*");
        underItsChild.Should().Throw<InvalidOperationException>().WithMessage("*under itself*");

        // The outline root is reachable as the Parent of any top-level entry, and is the ancestor
        // of everything.
        document.Outlines.Add(a);
        var rootUnderAnEntry = () => b.Outlines.Add(a.Parent);
        rootUnderAnEntry.Should().Throw<InvalidOperationException>().WithMessage("*under itself*");

        Tree(RoundTripped(document)).Should().Be("a(b)");
    }

    [Fact]
    public void EntriesUnderAnEntryNotYetInADocumentAreRefusedWithAReason()
    {
        // This used to be a NullReferenceException.
        var detached = new PdfOutline { Title = "detached" };

        var adding = () => detached.Outlines.Add(new PdfOutline { Title = "child" });

        adding.Should().Throw<InvalidOperationException>().WithMessage("*not in a document yet*");
    }

    [Fact]
    public void ACollectionAskedForBeforeItsEntryWasAddedFollowsTheEntryIntoTheDocument()
    {
        var document = ADocumentOf(1);
        var entry = new PdfOutline { Title = "a" };
        var children = entry.Outlines;

        document.Outlines.Add(entry);
        children.Add("a1", document.Pages[0]);

        Tree(RoundTripped(document)).Should().Be("a(a1)");
    }

    [Fact]
    public void AnEntryOfAnotherDocumentIsRefused()
    {
        var document = ADocumentOf(1);
        var elsewhere = ADocumentOf(1);
        var theirs = new PdfOutline { Title = "theirs" };
        elsewhere.Outlines.Add(theirs);
        elsewhere.Outlines.Remove(theirs);

        var adding = () => document.Outlines.Add(theirs);

        adding.Should().Throw<ArgumentException>().WithMessage("*another document*");
    }

    // ── What is allowed, and what the file then says ────────────────────────────────────────────

    [Fact]
    public void AnEntryRemovedFromOneParentCanBeAddedToAnother()
    {
        var document = ADocumentOf(1);
        var a = document.Outlines.Add("a", document.Pages[0]);
        var b = document.Outlines.Add("b", document.Pages[0]);
        var x = a.Outlines.Add("x", document.Pages[0]);
        Saved(document);

        a.Outlines.Remove(x);
        b.Outlines.Add(x);

        x.Parent.Should().BeSameAs(b);
        Tree(RoundTripped(document)).Should().Be("a,b(x)",
            "a was saved with x as its /First and /Last, and has none now");
    }

    [Fact]
    public void AnEntryRemovedAndAddedAgainGoesAtTheEnd()
    {
        var document = ADocumentOf(1);
        document.Outlines.Add("a", document.Pages[0]);
        var b = document.Outlines.Add("b", document.Pages[0]);
        document.Outlines.Add("c", document.Pages[0]);

        document.Outlines.Remove(b);
        document.Outlines.Add(b);

        Tree(RoundTripped(document)).Should().Be("a,c,b");
    }

    [Fact]
    public void AnEntryRemovedAcrossASaveComesBackWithEverythingUnderIt()
    {
        // The save between the two drops the entry and its child from the object table and
        // numbers the rest from one again, so both come back to numbers that are now taken.
        var document = ADocumentOf(1);
        document.Outlines.Add("a", document.Pages[0]);
        var b = document.Outlines.Add("b", document.Pages[0]);
        b.Outlines.Add("b1", document.Pages[0]);
        document.Outlines.Add("c", document.Pages[0]);

        document.Outlines.Remove(b);
        Saved(document);
        document.Outlines.Add(b);

        Tree(RoundTripped(document)).Should().Be("a,c,b(b1)");
    }

    [Fact]
    public void RemovingTheLastEntryOfASavedTreeLeavesNoLinkToIt()
    {
        var document = ADocumentOf(1);
        document.Outlines.Add("a", document.Pages[0]);
        document.Outlines.Add("b", document.Pages[0]);
        var c = document.Outlines.Add("c", document.Pages[0]);
        Saved(document);

        document.Outlines.Remove(c);

        Tree(RoundTripped(document)).Should().Be("a,b", "b was saved with a /Next to c");
    }

    [Fact]
    public void RemovingEntriesOfADocumentReadFromAFileTakesThemOutOfTheFile()
    {
        var original = ADocumentOf(1);
        original.Outlines.Add("a", original.Pages[0]);
        original.Outlines.Add("b", original.Pages[0]);
        original.Outlines.Add("c", original.Pages[0]);

        var first = RoundTripped(original);
        first.Outlines.RemoveAt(0);
        first.Outlines.RemoveAt(1);
        Tree(RoundTripped(first)).Should().Be("b");

        var second = RoundTripped(original);
        second.Outlines.Clear();
        Tree(RoundTripped(second)).Should().Be("<none>",
            "the outline root was read with a /First and a /Last naming entries now gone");
    }

    [Fact]
    public void ADocumentWhoseOutlinesWereOnlyLookedAtIsWellFormed()
    {
        // Asking document.Outlines anything creates the root. With no entries it was never
        // written, and the catalog named it anyway.
        var document = ADocumentOf(1);
        document.Outlines.Count.Should().Be(0);

        var reopened = RoundTripped(document);

        Tree(reopened).Should().Be("<none>");
        reopened.Outlines.Add("added later", reopened.Pages[0]);
        Tree(RoundTripped(reopened)).Should().Be("added later");
    }

    [Fact]
    public void AnEntryReplacedThroughTheIndexerIsFreeToBeAddedElsewhere()
    {
        var document = ADocumentOf(1);
        var a = document.Outlines.Add("a", document.Pages[0]);
        document.Outlines.Add("b", document.Pages[0]);
        var y = new PdfOutline("y", document.Pages[0]);

        document.Outlines[0] = y;
        document.Outlines[0] = y;
        a.Parent.Should().BeNull();
        y.Outlines.Add(a);

        Tree(RoundTripped(document)).Should().Be("y(a),b");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    private static PdfDocument ADocumentOf(int pages)
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

    private static byte[] Saved(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static PdfDocument RoundTripped(PdfDocument document) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(Saved(document)), PdfDocumentOpenMode.Modify);

    /// <summary>
    ///   The outline tree of a document read back, as titles: <c>a(a1,a2),b</c>, or <c>&lt;none&gt;</c>
    ///   when the catalog names no outline or the outline has no entries. Read from the raw
    ///   dictionaries, and every link is checked against the walk: an entry whose <c>/Parent</c> or
    ///   <c>/Prev</c> is not the one it was reached through is marked, as is a list whose
    ///   <c>/Last</c> is not where the <c>/Next</c> chain ended.
    /// </summary>
    private static string Tree(PdfDocument document)
    {
        var root = document.Internals.Catalog.Elements.GetDictionary("/Outlines");
        if (root == null || root.Elements.GetDictionary("/First") == null)
            return "<none>";
        return Entries(root);
    }

    private static string Entries(PdfDictionary parent)
    {
        var text = new StringBuilder();
        PdfDictionary previous = null;
        var current = parent.Elements.GetDictionary("/First");

        // Bounded, because a list linked back into itself is one of the things being looked for.
        for (var seen = 0; current != null && seen < 100; seen++)
        {
            if (text.Length > 0)
                text.Append(',');
            AppendEntry(text, current, parent, previous);

            previous = current;
            current = current.Elements.GetDictionary("/Next");
        }

        if (current != null)
            text.Append("!loop");
        if (!ReferenceEquals(parent.Elements.GetDictionary("/Last"), previous))
            text.Append("!last");
        return text.ToString();
    }

    /// <summary>
    ///   One entry's title, a mark for each back-link that is not the one it was reached through,
    ///   and its own children in parentheses.
    /// </summary>
    private static void AppendEntry(StringBuilder text, PdfDictionary current, PdfDictionary parent, PdfDictionary previous)
    {
        text.Append(current.Elements.GetString("/Title"));

        if (!ReferenceEquals(current.Elements.GetDictionary("/Parent"), parent))
            text.Append("!parent");
        if (!ReferenceEquals(current.Elements.GetDictionary("/Prev"), previous))
            text.Append("!prev");
        if (current.Elements.GetDictionary("/First") != null)
            text.Append('(').Append(Entries(current)).Append(')');
    }
}
