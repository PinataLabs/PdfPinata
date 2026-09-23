using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
// The content-stream readers are linked in from the other test project and keep their namespace.
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   PinataLayout knows what it is drawing — a <c>Paragraph</c> whose style is <c>Heading1</c>, a
///   <c>Row</c> flagged as a heading, an <c>Image</c> — and used to throw all of it away on the way
///   to the page, where a heading and a caption are both a <c>Tj</c>. These are the assertions that
///   it now keeps it.
/// </summary>
/// <remarks>
///   Stage A gave the library a hand-driven tagging API, which is plumbing rather than a feature:
///   almost nobody tags a document by hand, so almost no document was accessible. This is Stage B,
///   where the renderer does it, and Stage C, where a document may claim PDF/UA and be held to it.
///   <para>
///   The tree rather than the content stream, because the tree is what a screen reader walks.
///   </para>
/// </remarks>
public class TaggedOutputTests
{
    /// <summary>
    /// A list item's own body — its direct child, not <c>Single("LBody")</c>, because a nested item
    /// has a body of its own too and the search that finds one in the whole subtree finds several.
    /// </summary>
    private static StructureNode Body(StructureNode listItem) => listItem.Children.First(child => child.Tag == "LBody");


    [Fact]
    public void AParagraphIsAParagraphAndAHeadingIsAHeading()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Invoice", "Heading1");
        section.AddParagraph("Amounts are in pounds sterling.");

        var tree = Structure.Of(document);

        tree.Tag.Should().Be("Document");
        tree.Single("Sect").ChildTags().Should().Equal("H1", "P");
    }

    [Theory]
    [InlineData("Heading1", "H1")]
    [InlineData("Heading2", "H2")]
    [InlineData("Heading3", "H3")]
    [InlineData("Heading4", "H4")]
    [InlineData("Heading5", "H5")]
    // PinataLayout has nine heading levels and PDF has six, so the last four land on the deepest one
    // PDF has. A heading too deep to name exactly is still a heading, and calling it a paragraph
    // would lose more than calling it an H6 does.
    [InlineData("Heading6", "H6")]
    [InlineData("Heading7", "H6")]
    [InlineData("Heading9", "H6")]
    public void AHeadingIsTaggedAtItsOwnLevel(string style, string expected)
    {
        var document = new Document();
        document.AddSection().AddParagraph("Heading", style);

        Structure.Of(document).Single("Sect").ChildTags().Should().Equal(expected);
    }

    [Fact]
    public void ATableIsRowsAndCellsAndItsHeadingRowSaysWhichWayItReaches()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Summary = "Charges for the period, by service.";
        table.AddColumn("6cm");
        table.AddColumn("3cm");

        var heading = table.AddRow();
        heading.HeadingFormat = true;
        heading.Cells[0].AddParagraph("Service");
        heading.Cells[1].AddParagraph("Amount");

        var row = table.AddRow();
        row.Cells[0].AddParagraph("Support");
        row.Cells[1].AddParagraph("49.20");

        var tagged = Structure.Of(document).Single("Table");

        tagged.Summary.Should().Be("Charges for the period, by service.");
        tagged.ChildTags().Should().Equal("TR", "TR");
        tagged.Children[0].ChildTags().Should().Equal("TH", "TH");
        tagged.Children[1].ChildTags().Should().Equal("TD", "TD");

        // Without the scope a reader cannot say which heading a cell answers to, and a table reads
        // as a stream of values with nothing to attach them to.
        tagged.Children[0].Children.Should().AllSatisfy(cell => cell.Scope.Should().Be("Column"));
    }

    [Fact]
    public void AMergedCellSaysHowFarItReaches()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn("4cm");
        table.AddColumn("4cm");
        table.AddColumn("4cm");

        var row = table.AddRow();
        row.Cells[0].MergeRight = 2;
        row.Cells[0].AddParagraph("Everything");

        table.AddRow().Cells[0].AddParagraph("One");

        // Three columns and one cell. Without the span a reader would put "Everything" in the first
        // column and expect two more that are not there.
        var merged = Structure.Of(document).Single("Table").Children[0].Children[0];
        merged.ColumnSpan.Should().Be(3);
    }

    [Fact]
    public void AMergeThatRunsPastTheTableSaysOnlyHowFarItReaches()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn("4cm");
        table.AddColumn("4cm");

        var row = table.AddRow();
        row.Cells[0].MergeRight = 9;
        row.Cells[0].MergeDown = 5;
        row.Cells[0].AddParagraph("Everything");
        table.AddRow();

        // A span is how a reader knows which columns and rows the cell occupies, so one wider or
        // taller than the table describes a grid the reader cannot lay out — and the caller wrote
        // it into a table that has no such columns or rows to give.
        var merged = Structure.Of(document).Single("Table").Children[0].Children[0];

        merged.ColumnSpan.Should().Be(2);
        merged.RowSpan.Should().Be(2);
    }

    [Fact]
    public void AListIsAListAndItsBulletIsALabel()
    {
        var document = new Document();
        var section = document.AddSection();
        foreach (var item in new[] { "First", "Second", "Third" })
            section.AddParagraph(item).Format.ListInfo.ListType = ListType.BulletList1;

        var list = Structure.Of(document).Single("L");

        list.ChildTags().Should().Equal("LI", "LI", "LI");
        list.Children[0].ChildTags().Should().Equal("Lbl", "LBody");

        // The bullet is a label and not part of the sentence. Read as one, every item begins with
        // the word the bullet is pronounced as.
        list.Children[0].Single("Lbl").MarkCount.Should().Be(1);
    }

    [Fact]
    public void ARunOfListItemsIsOneListAndAChangeOfKindStartsAnother()
    {
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("First").Format.ListInfo.ListType = ListType.BulletList1;
        section.AddParagraph("Second").Format.ListInfo.ListType = ListType.BulletList1;
        section.AddParagraph("Interrupted.");
        section.AddParagraph("Third").Format.ListInfo.ListType = ListType.NumberList1;

        var section1 = Structure.Of(document).Single("Sect");

        section1.ChildTags().Should().Equal("L", "P", "L");
        section1.Children[0].Children.Should().HaveCount(2);
        section1.Children[2].Children.Should().HaveCount(1);
    }

    [Fact]
    public void ANestedListIsAListInsideAnItemsBody()
    {
        // docs/specs/nested-lists.md. Two outer items with two inner ones between them: the tree
        // should nest and then unnest, rather than coming out as a run of four siblings.
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Outer 1").Format.ListInfo.ListType = ListType.BulletList1;

        var inner1 = section.AddParagraph("Inner 1");
        inner1.Format.ListInfo.ListType = ListType.BulletList2;
        inner1.Format.ListInfo.NestingLevel = 2;

        var inner2 = section.AddParagraph("Inner 2");
        inner2.Format.ListInfo.ListType = ListType.BulletList2;
        inner2.Format.ListInfo.NestingLevel = 2;

        section.AddParagraph("Outer 2").Format.ListInfo.ListType = ListType.BulletList1;

        var sect = Structure.Of(document).Single("Sect");
        sect.ChildTags().Should().Equal("L"); // the whole run is one outer list

        var outerList = sect.Children[0];
        outerList.ChildTags().Should().Equal("LI", "LI");

        var firstItem = outerList.Children[0];
        firstItem.ChildTags().Should().Equal("Lbl", "LBody");

        var firstBody = Body(firstItem);
        firstBody.ChildTags().Should().Equal("L"); // the nested list lives in the item's body
        firstBody.Children[0].ChildTags().Should().Equal("LI", "LI");

        var secondItem = outerList.Children[1];
        secondItem.OfTag("L").Should().BeEmpty("returning to the outer level closes the inner list");
    }

    [Fact]
    public void ThreeLevelsNestJustAsTwoDo()
    {
        // Nothing about the tagger's rule is special-cased for the second level - a third has to
        // work exactly the same way, one item's body at a time.
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Level 1").Format.ListInfo.ListType = ListType.BulletList1;

        var level2 = section.AddParagraph("Level 2");
        level2.Format.ListInfo.ListType = ListType.BulletList2;
        level2.Format.ListInfo.NestingLevel = 2;

        var level3 = section.AddParagraph("Level 3");
        level3.Format.ListInfo.ListType = ListType.BulletList3;
        level3.Format.ListInfo.NestingLevel = 3;

        var outerList = Structure.Of(document).Single("Sect").Children[0];
        var middleList = Body(outerList.Children[0]).Children[0];
        middleList.Tag.Should().Be("L");

        var innermostList = Body(middleList.Children[0]).Children[0];
        innermostList.Tag.Should().Be("L");
        innermostList.Children[0].Single("Lbl").MarkCount.Should().Be(1);
    }

    [Fact]
    public void ASkippedLevelOpensOneNestedListRatherThanTwoOrAnError()
    {
        // An item at level three straight after one at level one is one level deeper, not two -
        // refusing it would fail a document generated from real data over a cosmetic inconsistency.
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Outer").Format.ListInfo.ListType = ListType.BulletList1;

        var deep = section.AddParagraph("Deep");
        deep.Format.ListInfo.ListType = ListType.BulletList2;
        deep.Format.ListInfo.NestingLevel = 3;

        var outerList = Structure.Of(document).Single("Sect").Children[0];
        var body = Body(outerList.Children[0]);

        body.ChildTags().Should().Equal("L"); // one nested list, not two, and no error
        body.Children[0].ChildTags().Should().Equal("LI");
    }

    [Fact]
    public void MixedTypesNestNumbersOutsideAndBulletsInside()
    {
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Section 1").Format.ListInfo.ListType = ListType.NumberList1;

        var detail = section.AddParagraph("Detail");
        detail.Format.ListInfo.ListType = ListType.BulletList1;
        detail.Format.ListInfo.NestingLevel = 2;

        var outerList = Structure.Of(document).Single("Sect").Children[0];
        var innerList = Body(outerList.Children[0]).Children[0];

        // Both are /L in the tree; what a reader is told apart by is the number formatting, decided
        // by rendering rather than by structure, so there is nothing here to distinguish them by
        // beyond the nesting itself.
        outerList.Tag.Should().Be("L");
        innerList.Tag.Should().Be("L");
        innerList.Children.Should().HaveCount(1);
    }

    [Fact]
    public void ADocumentThatNeverSetsALevelProducesExactlyTodaysTreeItemForItem()
    {
        // User story 8: adopting the new property changes nothing for a document that never sets it.
        var document = new Document();
        var section = document.AddSection();
        foreach (var item in new[] { "First", "Second", "Third" })
            section.AddParagraph(item).Format.ListInfo.ListType = ListType.BulletList1;

        var list = Structure.Of(document).Single("L");

        list.ChildTags().Should().Equal("LI", "LI", "LI");
        foreach (var listItem in list.Children)
            listItem.ChildTags().Should().Equal("Lbl", "LBody");
    }

    [Fact]
    public void EveryListItemHasABodyNestedOrNot()
    {
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Outer").Format.ListInfo.ListType = ListType.BulletList1;
        var inner = section.AddParagraph("Inner");
        inner.Format.ListInfo.ListType = ListType.BulletList2;
        inner.Format.ListInfo.NestingLevel = 2;

        var tree = Structure.Of(document);

        // A direct child, not merely one somewhere in the subtree - a nested item's LBody living
        // inside the outer item's own LBody must not count as the outer item's.
        tree.OfTag("LI").Should().OnlyContain(li => li.Children.Any(child => child.Tag == "LBody"));
    }

    [Fact]
    public void NestingSurvivesAPageBreakInTheMiddleOfAnInnerList()
    {
        var document = new Document();
        var section = document.AddSection();
        // Both dimensions: FlattenPageSetup overwrites whichever one is set on its own with the
        // document default, so a page sized by height alone is silently still A4.
        section.PageSetup.PageWidth = "21cm";
        section.PageSetup.PageHeight = "4cm";
        section.PageSetup.TopMargin = "0.5cm";
        section.PageSetup.BottomMargin = "0.5cm";

        section.AddParagraph("Outer").Format.ListInfo.ListType = ListType.BulletList1;
        for (var index = 0; index < 8; index++)
        {
            var inner = section.AddParagraph($"Inner {index}");
            inner.Format.ListInfo.ListType = ListType.BulletList2;
            inner.Format.ListInfo.NestingLevel = 2;
        }

        var rendered = Rendered.Of(document);
        rendered.PageCount.Should().BeGreaterThan(1, "the arrangement is only interesting if it splits");

        var outerList = Structure.RootOf(rendered).Single("Sect").Children[0];
        var innerList = Body(outerList.Children[0]).Children[0];

        innerList.Tag.Should().Be("L");
        innerList.ChildTags().Should().Equal("LI", "LI", "LI", "LI", "LI", "LI", "LI", "LI");
    }

    [Fact]
    public void NumberingContinuesAcrossANestedList()
    {
        // Depth does not touch numbering: an outer numbered list resuming after a nested list keeps
        // counting, exactly as it does today for a plain change of list type.
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("One").Format.ListInfo.ListType = ListType.NumberList1;

        var nested = section.AddParagraph("Nested");
        nested.Format.ListInfo.ListType = ListType.BulletList1;
        nested.Format.ListInfo.NestingLevel = 2;

        section.AddParagraph("Two").Format.ListInfo.ListType = ListType.NumberList1;

        var pdf = Rendered.Of(document);
        var shown = TextOperators.ShownStrings(pdf.Pages[0]);

        shown.Should().Contain("1.");
        shown.Should().Contain("2.");
    }

    [Fact]
    public void AnImageWithAlternativeTextIsAFigureThatSaysWhatItShows()
    {
        var document = new Document();
        var image = document.AddSection().AddImage(AnImage());
        image.Width = "3cm";
        image.AlternativeText = "The company logo.";

        var figure = Structure.Of(document).Single("Figure");
        figure.AlternateText.Should().Be("The company logo.");
    }

    [Fact]
    public void AnImageWithNothingToSayIsDrawnAsDecorationRatherThanAsAFigureWithNothingToSay()
    {
        var document = new Document();
        document.AddSection().AddImage(AnImage()).Width = "3cm";

        // Not a Figure with an empty /Alt, which announces to a reader that something is there and
        // then cannot say what — leaving them knowing only that they have missed something. An
        // artifact is passed over in silence, which for an undescribed image is the honest answer.
        Structure.Of(document).OfTag("Figure").Should().BeEmpty();
    }

    [Fact]
    public void AChartIsAFigureToo()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Column2D);
        chart.Width = "8cm";
        chart.Height = "5cm";
        chart.SeriesCollection.AddSeries().Add(3.0, 5.0, 4.0);
        chart.AlternativeText = "Sales by quarter, rising through the year.";

        // For the same reason an image is. Axis labels and data labels read out in drawing order say
        // nothing about the shape they describe, so the description is the whole of what a reader
        // who cannot see it gets.
        Structure.Of(document).Single("Figure").AlternateText
            .Should().Be("Sales by quarter, rising through the year.");
    }

    [Fact]
    public void AHyperlinkIsALinkThatReachesItsAnnotation()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("See ");
        paragraph.AddHyperlink("https://example.org/terms", HyperlinkType.Web)
            .AddText("the terms");
        paragraph.AddText(".");

        var link = Structure.Of(document).Single("Link");

        // Both halves. The marks are the words a reader announces as the link; the object reference
        // is the annotation, which is content as much as anything drawn is and is otherwise reachable
        // only by hit-testing rectangles.
        link.MarkCount.Should().BeGreaterThan(0);
        link.AnnotationCount.Should().Be(1);
    }

    [Fact]
    public void AWordBrokenAtAHyphenSaysWhatItReallyIs()
    {
        var tree = Structure.Of(Hyphenated());
        var span = tree.Single("Span");

        // The page says "demon-" and "strate". The word is neither, and /ActualText is the only
        // thing that can say so — without it a reader announces two fragments, a search for the
        // word fails, and copying the paragraph out pastes the hyphen the typesetter added.
        span.ActualText.Should().Be("demonstrate");

        // Two runs of marks, one per line, on the one element. Not one sequence spanning the break:
        // a marked-content sequence cannot cross a page boundary, and this has to work when the
        // break happens to be one.
        span.MarkCount.Should().Be(2);
    }

    [Fact]
    public void AWordBrokenAtAHyphenExtractsWhole()
    {
        // docs/specs/tagged-text-extraction.md gap G7: the extractor is page-scoped and never
        // consults the structure tree, so the only way it can read what the test above just proved
        // is on the element - "demonstrate" - is for the BDC that wraps each fragment to say the
        // same thing inline, beside its /MCID.
        var page = Rendered.FirstPageOf(Hyphenated());

        // Exact rather than Contain: the word is drawn as two content items of the same element,
        // one per line, and each used to repeat the whole word inline - "In demonstrate\ndemonstrate"
        // passed a Contain assertion just as easily as the single, correct occurrence does.
        PdfTextExtractor.ExtractText(page).Should().Be("In demonstrate");
    }

    [Fact]
    public void AHyphenThatBreaksNothingIsNotTaggedAtAll()
    {
        var document = new Document();
        var section = document.AddSection();

        // The same word, with room for it. PinataLayout draws no hyphen when it does not need one, so
        // there is no word to put back together and nothing to say about it.
        section.AddParagraph("A demon­strate of it.");

        Structure.Of(document).OfTag("Span").Should().BeEmpty();
    }

    [Fact]
    public void AWordBrokenAcrossAPageIsStillOneWord()
    {
        var document = new Document();
        var section = document.AddSection();

        // Both dimensions, because FlattenPageSetup overwrites whichever one is set on its own with
        // the document default — so a page sized by width alone is silently still A4. Room for one
        // line and no more, so the break at the hyphen is also the break between pages.
        section.PageSetup.PageWidth = "21cm";
        section.PageSetup.PageHeight = "2.5cm";
        section.PageSetup.TopMargin = "1cm";
        section.PageSetup.BottomMargin = "1cm";

        var paragraph = section.AddParagraph("In demon­strate");
        paragraph.Format.RightIndent = "14.2cm";
        paragraph.Format.WidowControl = false;

        var rendered = Rendered.Of(document);
        rendered.PageCount.Should().Be(2, "the arrangement is only interesting if it splits");

        // The hard case, and the reason the element is keyed by the soft hyphen rather than by
        // anything belonging to a renderer: the two halves are drawn by two different renderers, on
        // two different pages, into two different content streams. One element, two runs of marks,
        // one word.
        var span = Structure.RootOf(rendered).Single("Span");
        span.ActualText.Should().Be("demonstrate");
        span.MarkCount.Should().Be(2);
    }

    [Fact]
    public void SayingWhatTheWordIsDoesNotChangeWhatIsDrawn()
    {
        var tagged = Glyphs.On(Rendered.FirstPageOf(Hyphenated()));
        var plain = Glyphs.On(Untagged(Hyphenated()).Pages[0]);

        // /ActualText changes what the word is read as. The page still shows "demon-" and "strate",
        // hyphen and all, because that is what the typesetter decided and none of this is about
        // typesetting.
        tagged.Should().Equal(plain);
    }

    [Fact]
    public void EveryMarkedContentSequenceOnAPageIsClosed()
    {
        var document = new Document();
        var section = document.AddSection();
        section.Headers.Primary.AddParagraph("Running head");
        section.AddParagraph("Statement", "Heading1");

        // Everything that opens a scope, on one page and interleaved: a heading, a list, a link
        // inside a paragraph, a word broken at a hyphen, and a table whose cells hold paragraphs.
        foreach (var item in new[] { "First", "Second" })
            section.AddParagraph(item).Format.ListInfo.ListType = ListType.NumberList1;

        var paragraph = section.AddParagraph("See ");
        paragraph.AddHyperlink("https://example.org", HyperlinkType.Web).AddText("the terms");
        paragraph.AddText(" for demon­strate of it");
        paragraph.Format.RightIndent = "14.2cm";

        var table = section.AddTable();
        table.AddColumn("4cm");
        table.AddRow().Cells[0].AddParagraph("Cell");

        var content = Encoding.ASCII.GetString(PageContent.Of(Rendered.Of(document).Pages[0]));

        // An unbalanced BDC does not fail loudly — it corrupts every mark after it on the page, and
        // the page goes on drawing perfectly while the tree describes the wrong things.
        var opened = Occurrences(content, "BDC") + Occurrences(content, "BMC");
        opened.Should().BeGreaterThan(0, "the arrangement is only interesting if it marks anything");
        opened.Should().Be(Occurrences(content, "EMC"));
    }

    private static int Occurrences(string content, string token)
    {
        var found = 0;
        for (var at = content.IndexOf(token, StringComparison.Ordinal); at >= 0;
             at = content.IndexOf(token, at + token.Length, StringComparison.Ordinal))
            found++;

        return found;
    }

    [Fact]
    public void ARunningHeadIsFurnitureAndNotSomethingToReadOut()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Body text.");
        section.Headers.Primary.AddParagraph("Quarterly report");
        section.Footers.Primary.AddParagraph("Page 1");

        var tree = Structure.Of(document);

        // Nothing from the header or the footer is anywhere in the tree — they are drawn inside an
        // artifact scope instead. A page number read out between every paragraph is worse than no
        // page number.
        tree.Descendants().Should().HaveCount(3, "Document, Sect and the one paragraph");
    }

    [Fact]
    public void ADescribedImageInARunningHeadDoesNotDescribeSomethingElseInstead()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Body text.");

        var logo = section.Headers.Primary.AddParagraph().AddImage(AnImage());
        logo.Width = "3cm";
        logo.AlternativeText = "The company logo.";

        var tree = Structure.Of(document);

        // The image is inside an artifact, so no Figure is opened for it — and it must not describe
        // anything else either. The renderer used to ask the tagger what was current straight after
        // opening its scope, which is a different question: a refused scope pushes nothing, so what
        // came back was the enclosing element and the logo's alternate text was written onto that.
        // A reader would then have announced the body of the page as "The company logo."
        tree.OfTag("Figure").Should().BeEmpty();
        tree.Descendants().Should().OnlyContain(node => string.IsNullOrEmpty(node.AlternateText),
            "nothing in the tree is described by an image that is not in the tree");
    }

    [Fact]
    public void OneObjectCanBeDrawnOnAPageThatWasNeverBegun()
    {
        // RenderObject draws a single object onto a surface the caller owns, and nothing about that
        // path begins a page. Tagging still looked possible from inside it — it is switched on, and
        // there is a real page to write marks onto — so a list paragraph went to build its /L
        // against a document the tagger had not been given yet and threw NullReferenceException at
        // the caller. The condition it was missing is "no page has been begun", which is now part
        // of the one test the tagger asks rather than absent from three of its methods.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("A list item");
        paragraph.Format.ListInfo.ListType = ListType.BulletList1;

        var renderer = new DocumentRenderer(document);
        renderer.PrepareDocument();

        using var pdf = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(pdf.AddPage());

        // ReSharper disable once AccessToDisposedClosure
        var act = () => renderer.RenderObject(gfx, XUnit.FromCentimeter(1), XUnit.FromCentimeter(1),
            XUnit.FromCentimeter(10), paragraph);

        act.Should().NotThrow();
    }

    [Fact]
    public void ADescribedTableInARunningHeadDoesNotSummariseSomethingElseInstead()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Body text.");

        var table = section.Headers.Primary.AddTable();
        table.AddColumn("4cm");
        table.Summary = "Two columns of nothing in particular.";
        table.AddRow().Cells[0].AddParagraph("Cell");

        var tree = Structure.Of(document);

        tree.OfTag("Table").Should().BeEmpty();
        tree.Descendants().Should().OnlyContain(node => string.IsNullOrEmpty(node.Summary));
    }

    [Fact]
    public void APageThatDrawsNothingIsStillInTheTree()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("First page.");
        section.AddPageBreak();
        section.AddPageBreak();
        section.AddParagraph("Third page.");

        var rendered = Rendered.Of(document);

        // Every page, including the blank one. A page with no /StructParents cannot be told apart
        // from one imported out of an untagged document, and a validator is right to refuse both.
        for (var page = 0; page < rendered.PageCount; page++)
        {
            rendered.Pages[page].Elements.ContainsKey("/StructParents")
                .Should().BeTrue("page " + (page + 1) + " has to be in the tree");
        }
    }

    [Fact]
    public void EveryTaggedPageOrdersItsTabsByStructure()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Body text.");

        var rendered = Rendered.Of(document);
        rendered.Pages[0].Elements.GetName("/Tabs").Should().Be("/S");
    }

    [Fact]
    public void ADocumentThatAsksNotToBeTaggedIsWrittenAsItAlwaysWas()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Body text.");

        var renderer = new PdfDocumentRenderer(true) { Document = document, TagContent = false };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        var saved = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        saved.Internals.Catalog.Elements.ContainsKey("/StructTreeRoot").Should().BeFalse();
        saved.Internals.Catalog.Elements.ContainsKey("/MarkInfo").Should().BeFalse();
        saved.Pages[0].Elements.ContainsKey("/StructParents").Should().BeFalse();
    }

    [Fact]
    public void TheDocumentSaysWhatLanguageItIsIn()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Body text.");

        var renderer = new PdfDocumentRenderer(true) { Document = document, Language = "en-GB" };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        PdfReader.Open(stream, PdfDocumentOpenMode.Modify)
            .Internals.Catalog.Elements.GetString("/Lang").Should().Be("en-GB");
    }

    /// <summary>
    ///   A document narrow enough that "demonstrate" cannot finish the line it starts on, so
    ///   PinataLayout breaks it at the soft hyphen in the middle of it.
    /// </summary>
    /// <remarks>
    ///   Narrowed with an indent rather than with a page size, which is both what the other
    ///   soft-hyphen tests do and the only one of the two that works: <c>PageSetup.PageWidth</c> on
    ///   its own leaves the page A4 and the measure unchanged, so a document set up that way breaks
    ///   nothing and the test passes for the wrong reason.
    /// </remarks>
    private static Document Hyphenated()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("In demon­strate");

        // 16cm of measure less 14.2cm leaves 1.8cm: room for "In demon-" and not for
        // "In demonstrate".
        paragraph.Format.RightIndent = "14.2cm";
        return document;
    }

    /// <summary>The same document rendered with tagging off.</summary>
    private static PdfDocument Untagged(Document document)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document, TagContent = false };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;
        return PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    /// <summary>
    ///   An image the backend will really decode, because an image that fails to load is drawn as a
    ///   grey placeholder and would answer these tests without ever having been an image.
    /// </summary>
    private static ImageSource.IImageSource AnImage() =>
        ImageSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "lenna.png"));
}
