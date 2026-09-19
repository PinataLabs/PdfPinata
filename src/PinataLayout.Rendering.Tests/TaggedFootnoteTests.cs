using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Footnotes were drawn but not tagged. Their body paragraphs went through the paragraph renderer
///   and so arrived in the tree as bare <c>P</c> elements hanging wherever the walk happened to be,
///   and the mark and the separator rule were drawn with no scope at all — which in a tagged document
///   is content sitting outside the structure tree, the first thing a validator objects to.
/// </summary>
/// <remarks>
///   <para>
///     Item 6a of <c>docs/specs/tagged-pdf-accessibility.md</c>, which that spec recorded as moot
///     because nothing drew a footnote. Something does now; see <c>docs/specs/PinataLayout-footnotes.md</c>.
///   </para>
///   <para>
///     The interesting assertion is <em>where</em> the <c>Note</c> sits. It is drawn at the foot of the
///     page and it belongs, in reading order, beside the sentence that cited it — so it is built at the
///     citation and filled in later, and these tests are what say the two renderers agree about which
///     element they are talking about.
///   </para>
/// </remarks>
public class TaggedFootnoteTests
{
    // ── The shape of it ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AFootnoteIsANote()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var tree = Structure.Of(document);

        tree.OfTag("Note").Should().HaveCount(1, "one note was cited, so there is one Note");
    }

    [Fact]
    public void TheMarkInTheSentenceIsAReferenceToIt()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var tree = Structure.Of(document);

        var reference = tree.Single("Reference");
        reference.MarkCount.Should().Be(1, "the raised mark is what the reference is drawn from");
    }

    [Fact]
    public void TheNoteReadsWhereItWasCitedRatherThanWhereItIsDrawn()
    {
        // The whole point of building the element at the citation. Drawing order would put every note
        // after all the body text of the page, severed from the sentence that cited it - which is
        // exactly the reading order a structure tree exists to correct.
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");
        section.AddParagraph("A later paragraph.");

        var tree = Structure.Of(document);
        var paragraphs = tree.Single("Sect").Children;

        paragraphs.Should().HaveCount(2);

        // The citation and the note it cites, in that order, inside the paragraph that cited them -
        // and not after the second paragraph, which is where they are drawn.
        paragraphs[0].ChildTags().Should().Equal(new[] { "Reference", "Note" });
    }

    [Fact]
    public void TheNoteHoldsTheParagraphsOfItsOwnContent()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var note = Structure.Of(document).Single("Note");

        note.ChildTags().Should().Contain("Lbl", "the note's own mark names the note it sits beside");
        note.ChildTags().Should().Contain("P", "and the note's text is a paragraph like any other");
    }

    [Fact]
    public void TheNoteItselfHoldsNoMarksDirectly()
    {
        // Entered rather than marked. A Note carrying marks of its own would claim that some of the
        // page belongs to the note rather than to the label and paragraphs inside it, which is a claim
        // about reading order and a false one.
        var note = Structure.Of(WithOneNote()).Single("Note");

        note.MarkCount.Should().Be(0);
    }

    [Fact]
    public void ANoteWithSeveralParagraphsKeepsThemAllInside()
    {
        var document = Document(out var section);
        var footnote = section.AddParagraph("A claim").AddFootnote();
        footnote.AddParagraph("The first half.");
        footnote.AddParagraph("The second half.");

        var note = Structure.Of(document).Single("Note");

        note.OfTag("P").Should().HaveCount(2);
    }

    // ── The identifier PDF/UA asks for ──────────────────────────────────────────────────────────

    [Fact]
    public void EveryNoteCanBePointedAt()
    {
        // ISO 14289-1 7.9. A note exists to be pointed at from the mark that cited it, and an element
        // with no identifier cannot be pointed at.
        var note = Structure.Of(WithOneNote()).Single("Note");

        note.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TwoNotesGetIdentifiersOfTheirOwn()
    {
        var document = Document(out var section);
        var paragraph = section.AddParagraph("Two claims");
        paragraph.AddFootnote("The first support.");
        paragraph.AddFootnote("The second support.");

        var notes = Structure.Of(document).OfTag("Note").ToList();

        notes.Should().HaveCount(2);
        notes.Select(note => note.Id).Should().OnlyHaveUniqueItems(
            "an identifier nothing can tell apart names neither of them");
    }

    [Fact]
    public void TheIdentifiersAreIndexedSoThatSomethingCanResolveThem()
    {
        // ISO 32000-1 requires the IDTree the moment any element carries an /ID: an identifier nothing
        // can be looked up by is a name with no directory.
        var rendered = Rendered.Of(WithOneNote());
        var note = Structure.Of(WithOneNote()).Single("Note");

        var tree = rendered.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")
            .Elements.GetDictionary("/IDTree");

        tree.Should().NotBeNull();
        var names = tree.Elements.GetArray("/Names");
        names.Should().NotBeNull();
        ((PdfString)names.Elements[0]).Value.Should().Be(note.Id);
    }

    [Fact]
    public void SavingTwiceIndexesTheIdentifiersOnceRatherThanTwice()
    {
        // Saving to a stream and then to a file is an ordinary thing to do, and the index is written
        // at save time. A fresh /IDTree each time would leave the earlier one in the cross-reference
        // table with nothing pointing at it - an orphan written into the file, and one more with
        // every save.
        var renderer = new PdfDocumentRenderer(true) { Document = WithOneNote() };
        renderer.RenderDocument();
        var pdf = renderer.PdfDocument;

        using var once = new System.IO.MemoryStream();
        pdf.Save(once, false);
        var first = pdf.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")
            .Elements.GetDictionary("/IDTree");

        using var twice = new System.IO.MemoryStream();
        pdf.Save(twice, false);
        var again = pdf.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")
            .Elements.GetDictionary("/IDTree");

        again.Should().BeSameAs(first, "the tree written last time is filled in again, not replaced");
        again.Elements.GetArray("/Names").Elements.Count.Should().Be(2,
            "one name and the element it names, not both twice over");
    }

    [Fact]
    public void ADocumentWithNoNotesInItHasNoIdentifierIndex()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim with nothing to support it.");

        var rendered = Rendered.Of(document);

        rendered.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")
            .Elements.GetDictionary("/IDTree")
            .Should().BeNull("an index of nothing says less than no index at all");
    }

    // ── The furniture ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheSeparatorRuleIsFurnitureRatherThanContent()
    {
        // Everything on a page is either content or an artifact, and a rule that is neither is what a
        // validator objects to first. The rule says where the body text stops; it carries nothing to
        // read out.
        var page = Rendered.FirstPageOf(WithOneNote());

        var content = PdfPinata.Test.Helpers.PageContent.Of(page);
        System.Text.Encoding.Latin1.GetString(content).Should().Contain("/Artifact BMC");
    }

    // ── What a PDF/UA claim makes of it ─────────────────────────────────────────────────────────

    [Fact]
    public void ADocumentWithFootnotesCanClaimAccessibility()
    {
        // The rule added here has teeth, so this is the test that says a footnote PinataLayout rendered
        // satisfies it rather than tripping over it.
        var renderer = new PdfDocumentRenderer(true)
        {
            Document = WithOneNote(),
            TagContent = true,
            Language = "en-GB"
        };

        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = "A claim and its support";
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;

        var saving = () =>
        {
            using var stream = new System.IO.MemoryStream();
            renderer.PdfDocument.Save(stream, false);
        };

        saving.Should().NotThrow();
    }

    [Fact]
    public void ANoteWithNoIdentifierIsRefused()
    {
        // Not reachable through MigraDoc, which always writes one - so built by hand, which is the
        // only way to get there and the reason the rule is checked rather than assumed.
        var document = new PdfDocument();
        var page = document.AddPage();
        document.Info.Title = "Hand built";
        document.Language = "en-GB";

        // Every earlier rule satisfied, so that the one under test is the one that fires. The page has
        // to be in the tree even though it draws nothing, which is what RegisterPage is for.
        document.Structure.RegisterPage(page);
        document.Structure.CreateElement(PdfPinata.Pdf.Structure.PdfTag.Note);

        // Set here because the validator is called directly. On the way to a save the conformance
        // writer settles this itself, before validating - it is a mechanical consequence of the claim
        // rather than something to refuse over - so a rule after it would never be reached otherwise.
        document.ViewerPreferences.DisplayDocTitle = true;

        var validating = () => PdfPinata.Pdf.Structure.PdfUaValidator.Validate(document);

        validating.Should().Throw<InvalidOperationException>().WithMessage("*note*no /ID*");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    static Document WithOneNote()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");
        return document;
    }

    /// <summary>
    ///   The pinned face, because a tagged document is still a laid-out one and the note has to fit on
    ///   the page for any of this to be observable.
    /// </summary>
    static Document Document(out Section section)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Sans";
        normal.Font.Size = 11;

        document.Styles[StyleNames.Footnote].Font.Size = 8;

        section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        return document;
    }
}
