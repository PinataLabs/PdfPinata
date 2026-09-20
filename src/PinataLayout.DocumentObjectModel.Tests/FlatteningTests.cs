using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Flattening is what turns a document that says as little as possible into one that says
///   everything: styles are copied down into the paragraphs that name them, a section's unset page
///   setup is filled in from the document's default, and a table's merged cells are worked out.
///   The renderer runs it first and then reads only the flattened model, so a value flattening
///   fails to fill in is a value the renderer will find empty.
///   <para>
///   <see cref="PdfFlattenVisitor"/> is the public way in and <c>VisitorBase</c> does most of the
///   work, so everything here goes through <c>Visit</c> on a whole document rather than reaching
///   for the internals. That is also how the renderer calls it.
///   </para>
/// </summary>
public class FlatteningTests
{
    static void Flattened(Document document)
    {
        new PdfFlattenVisitor().Visit(document);
    }

    // ----- styles into paragraphs ---------------------------------------------------------------------

    [Fact]
    public void AParagraphTakesOnWhatItsStyleSaysWithoutLosingWhatItSaysItself()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Loud", "Normal");
        style.Font.Bold = true;
        style.Font.Size = 20;
        style.ParagraphFormat.SpaceBefore = "1cm";
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Loud";
        paragraph.Format.Font.Italic = true;

        paragraph.Format.Font.Bold.Should().BeFalse("nothing is copied down until it is flattened");

        Flattened(document);

        paragraph.Format.Font.Bold.Should().BeTrue("this came from the style");
        paragraph.Format.Font.Size.Point.Should().BeApproximately(20, 1e-4);
        paragraph.Format.SpaceBefore.Centimeter.Should().BeApproximately(1, 1e-4);
        paragraph.Format.Font.Italic.Should().BeTrue("and this was the paragraph's own");
    }

    [Fact]
    public void AParagraphKeepsItsOwnAnswerWhereItDisagreesWithItsStyle()
    {
        // The whole point of setting a property on a paragraph that already has a style.
        var document = new Document();
        document.Styles.AddStyle("Loud", "Normal").Font.Size = 20;
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Loud";
        paragraph.Format.Font.Size = 8;

        Flattened(document);

        paragraph.Format.Font.Size.Point.Should().BeApproximately(8, 1e-4);
    }

    [Fact]
    public void AStyleBuiltOnAnotherBringsWhatBothOfThemSay()
    {
        var document = new Document();
        var middle = document.Styles.AddStyle("Middle", "Normal");
        middle.Font.Bold = true;
        var leaf = document.Styles.AddStyle("Leaf", "Middle");
        leaf.Font.Color = Colors.Purple;
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Leaf";

        Flattened(document);

        paragraph.Format.Font.Bold.Should().BeTrue("from the style it is based on");
        paragraph.Format.Font.Color.Should().Be(Colors.Purple, "from the style itself");
    }

    [Theory]
    [MemberData(nameof(FontMemberCases.All), MemberType = typeof(FontMemberCases))]
    public void AStyleSettingAnyFontMemberPassesItDownToAParagraph(string member, Action<Font> set, Func<Font, object> read)
    {
        // strikethrough was the one member FlattenFont forgot - docs/specs/dom-property-seams.md.
        // This runs the same check for all nine so a tenth member added later is the tenth case.
        var document = new Document();
        var style = document.Styles.AddStyle("Loud", "Normal");
        set(style.Font);
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Loud";

        Flattened(document);

        read(paragraph.Format.Font).Should().Be(read(style.Font), $"{member} should have been inherited from the style");
    }

    [Fact]
    public void AParagraphInsideACellIsFlattenedToo()
    {
        // The visitor has to reach every collection of elements, not only a section's own. A cell,
        // a header and a text frame each hold one.
        var document = new Document();
        document.Styles.AddStyle("Loud", "Normal").Font.Bold = true;
        var section = document.AddSection();
        var table = section.AddTable();
        table.AddColumn("3cm");
        var inCell = table.AddRow()[0].AddParagraph("in a cell");
        inCell.Style = "Loud";
        var inHeader = section.Headers.Primary.AddParagraph("in a header");
        inHeader.Style = "Loud";

        Flattened(document);

        inCell.Format.Font.Bold.Should().BeTrue();
        inHeader.Format.Font.Bold.Should().BeTrue();
    }

    // ----- page setup ------------------------------------------------------------------------------------

    [Fact]
    public void ASectionsUnsetPageSetupIsFilledInFromTheDocumentDefault()
    {
        var document = new Document();
        var setup = document.AddSection().PageSetup;
        setup.LeftMargin = "3cm";

        setup.RightMargin.IsEmpty.Should().BeTrue("only the left margin was given");
        setup.PageWidth.IsEmpty.Should().BeTrue();

        Flattened(document);

        setup.LeftMargin.Centimeter.Should().BeApproximately(3, 1e-4, "what was given is kept");
        setup.RightMargin.IsEmpty.Should().BeFalse("and what was not is filled in");
        setup.PageWidth.Centimeter.Should().BeApproximately(21, 0.1,
            "the default format is A4, and flattening turns the name into a size");
    }

    [Fact]
    public void EachSectionIsFilledInFromWhatItSaysRatherThanFromTheOneBeforeIt()
    {
        var document = new Document();
        var first = document.AddSection().PageSetup;
        first.PageFormat = PageFormat.A5;
        var second = document.AddSection().PageSetup;
        second.PageFormat = PageFormat.A6;

        Flattened(document);

        PageSetup.GetPageSize(PageFormat.A5, out var a5Width, out _);
        PageSetup.GetPageSize(PageFormat.A6, out var a6Width, out _);
        first.PageWidth.Point.Should().BeApproximately(a5Width.Point, 1);
        second.PageWidth.Point.Should().BeApproximately(a6Width.Point, 1);
    }

    [Fact]
    public void FlatteningTwiceLeavesTheSameDocument()
    {
        // The renderer prepares a document before every render, and a document rendered twice must
        // not drift - so flattening has to be something that can be done to an already flat model.
        var document = new Document();
        document.Styles.AddStyle("Loud", "Normal").Font.Size = 20;
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Loud";

        Flattened(document);
        var afterOnce = paragraph.Format.Font.Size.Point;
        var marginAfterOnce = document.LastSection.PageSetup.RightMargin.Point;
        Flattened(document);

        paragraph.Format.Font.Size.Point.Should().Be(afterOnce);
        document.LastSection.PageSetup.RightMargin.Point.Should().Be(marginAfterOnce);
    }

    // ----- tables and the cells merged away ------------------------------------------------------------------

    static Table ThreeByThree()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        for (var column = 0; column < 3; column++)
            table.AddColumn("2cm");
        for (var row = 0; row < 3; row++)
            table.AddRow();
        return table;
    }

    [Fact]
    public void AMergedCellCoversTheOnesItSwallowedAndTheyAreNotInTheList()
    {
        var table = ThreeByThree();
        table[0, 0].MergeRight = 1;
        table[1, 1].MergeDown = 1;

        var merged = new MergedCellList(table);

        merged.Count.Should().Be(7, "nine cells, less the two that were merged away");
        merged.GetCoveringCell(table[0, 1]).Should().BeSameAs(table[0, 0]);
        merged.GetCoveringCell(table[2, 1]).Should().BeSameAs(table[1, 1]);
        merged.GetCoveringCell(table[2, 2]).Should().BeSameAs(table[2, 2],
            "a cell that was not merged covers itself");
    }

    [Fact]
    public void ACellThatWasMergedAwayHasNoBordersToAskFor()
    {
        // It is not drawn, so asking how to draw it is the caller's mistake rather than a case to
        // answer - and the exception says which cell.
        var table = ThreeByThree();
        table[0, 0].MergeRight = 1;
        var merged = new MergedCellList(table);

        var act = () => merged.GetEffectiveBorders(table[0, 1]);

        act.Should().Throw<ArgumentException>().WithParameterName("cell");
    }

    [Fact]
    public void ACellKeepsItsOwnBorderWhereItHasOne()
    {
        var table = ThreeByThree();
        table[0, 0].Borders.Bottom.Width = "5pt";

        var borders = new MergedCellList(table).GetEffectiveBorders(table[0, 0]);

        borders.Bottom.Width.Point.Should().BeApproximately(5, 1e-4);
    }

    /// <summary>
    ///   Two cells side by side share the line between them, and only one of them can draw it. The
    ///   rule is that the heavier border wins, so that a table with one emphasised row does not end
    ///   up with the line either side of it drawn differently depending on which cell was asked.
    /// </summary>
    [Fact]
    public void TheHeavierOfTwoNeighbouringBordersIsTheOneBothCellsGet()
    {
        var table = ThreeByThree();
        table[0, 0].Borders.Right.Width = "4pt";
        table[0, 1].Borders.Left.Width = "1pt";

        var merged = new MergedCellList(table);

        merged.GetEffectiveBorders(table[0, 1]).Left.Width.Point.Should().BeApproximately(4, 1e-4,
            "the neighbour's heavier right border is what is drawn between them");
    }

    [Fact]
    public void TheBorderOfACellMergedRightComesFromTheCellAtTheFarEnd()
    {
        // The right-hand edge of a merged run is the right-hand edge of the last cell in it, which
        // is a cell that is not itself in the list.
        var table = ThreeByThree();
        table[0, 0].MergeRight = 1;
        table[0, 1].Borders.Right.Width = "6pt";

        var borders = new MergedCellList(table).GetEffectiveBorders(table[0, 0]);

        borders.Right.Width.Point.Should().BeApproximately(6, 1e-4);
    }

    [Fact]
    public void TheBorderOfACellMergedDownComesFromTheCellAtTheBottom()
    {
        var table = ThreeByThree();
        table[0, 0].MergeDown = 1;
        table[1, 0].Borders.Bottom.Width = "7pt";

        var borders = new MergedCellList(table).GetEffectiveBorders(table[0, 0]);

        borders.Bottom.Width.Point.Should().BeApproximately(7, 1e-4);
    }

    [Fact]
    public void ATableWithNothingMergedListsEveryCellItHas()
    {
        var table = ThreeByThree();

        new MergedCellList(table).Count.Should().Be(9);
    }

    [Fact]
    public void TheCellsAreListedRowByRowFromTheTopLeft()
    {
        // The list is binary-searched by position, so the order is not cosmetic.
        var table = ThreeByThree();
        var merged = new MergedCellList(table);

        for (var index = 1; index < merged.Count; index++)
        {
            var previous = merged[index - 1];
            var current = merged[index];
            var goesForward = current.Row.Index > previous.Row.Index
                || (current.Row.Index == previous.Row.Index && current.Column.Index > previous.Column.Index);
            goesForward.Should().BeTrue($"cell {index} should come after cell {index - 1}");
        }
    }
}
