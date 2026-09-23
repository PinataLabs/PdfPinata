using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   What a table draws: the borders each cell asks for, the height a row is held to, the edges a
///   merge takes away, and where a cell puts its text when it has more room than it needs.
/// </summary>
/// <remarks>
///   Promoted from PinataLayout 1.32's TestTable, which built one of each of these and saved it to a
///   file for a person to look at. Each arrangement is kept; what was a look is now an assertion
///   about the segments and the text positions in the content stream, which is exact and does not
///   need the page rasterized.
/// </remarks>
public class TableRenderingTests
{
    /// <summary>The width a table gives a border it was not told the width of.</summary>
    private const double DefaultBorderWidth = 0.5;

    [Fact]
    public void ACellBorderIsDrawnAtTheWidthTheCellAsksForRatherThanTheTables()
    {
        var page = Rendered.FirstPageOf(Bordered());

        // The table's own borders are visible and half a point wide; the first cell overrides
        // three of them individually. All four widths have to reach the page, or a cell asking
        // for a heavier rule than the table's silently gets the table's.
        StrokedLines.Of(page).Select(line => Math.Round(line.Width, 2)).Distinct()
            .Should().BeEquivalentTo(new[] { DefaultBorderWidth, 2.0, 8.0, 15.0 });
    }

    [Theory]
    [InlineData(14)]
    [InlineData(40)]
    public void ARowHeldToAnExactHeightIsDrawnAtThatHeight(double height)
    {
        var page = Rendered.FirstPageOf(OneRowOf(height));

        // The rules are drawn down the middle of the border, so the outermost pair stand half a
        // border apart from the row's own edges - once at the top and once at the foot, which
        // cancels out and leaves the span one whole border width over.
        var rules = HorizontalRules(page);
        (rules[0] - rules[^1]).Should().BeApproximately(height + DefaultBorderWidth, 0.01);
    }

    [Fact]
    public void MergingACellToTheRightTakesAwayTheEdgeBetweenTheTwo()
    {
        // The edge is still drawn where the cells below it describe it, so what changes is not
        // whether the column is ruled but whether it is ruled across this row.
        SegmentsDownTheMiddleOfTheFirstRow(Merged(cells => cells.TopLeft.MergeRight = 1))
            .Should().BeEmpty();

        SegmentsDownTheMiddleOfTheFirstRow(Merged(_ => { }))
            .Should().NotBeEmpty("an unmerged row is ruled between its two cells");
    }

    [Fact]
    public void MergingACellDownwardsTakesAwayTheEdgeBetweenTheTwo()
    {
        SegmentsAcrossTheMiddleOfTheSecondColumn(Merged(cells => cells.TopRight.MergeDown = 1))
            .Should().BeEmpty();

        SegmentsAcrossTheMiddleOfTheSecondColumn(Merged(_ => { }))
            .Should().NotBeEmpty("an unmerged column is ruled between its two cells");
    }

    [Fact]
    public void ACentredCellPutsItsTextHalfwayBetweenWhereTheTopAndTheBottomWouldPutIt()
    {
        // A row taller than its text has to place that text somewhere, and the three alignments
        // are only distinguishable from one another by where. Centring is asserted against the
        // other two rather than against a number, so the assertion holds whatever the height of
        // the line the text is set on.
        var top = TextBaselineOf(Aligned(VerticalAlignment.Top));
        var centre = TextBaselineOf(Aligned(VerticalAlignment.Center));
        var bottom = TextBaselineOf(Aligned(VerticalAlignment.Bottom));

        centre.Should().BeApproximately((top + bottom) / 2, 0.1);
        top.Should().BeGreaterThan(centre);
        centre.Should().BeGreaterThan(bottom);
    }

    /// <summary>
    ///   The arrangement of the original harness: a table whose first cell overrides three of the
    ///   borders it inherits, with a paragraph either side of it.
    /// </summary>
    private static Document Bordered()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("A paragraph before.");

        var table = section.AddTable();
        table.Borders.Visible = true;
        table.AddColumn();
        table.AddColumn();
        table.Rows.HeightRule = RowHeightRule.Exactly;
        table.Rows.Height = 14;

        var cell = table.AddRow().Cells[0];
        cell.Borders.Visible = true;
        cell.Borders.Left.Width = 8;
        cell.Borders.Right.Width = 2;
        cell.AddParagraph("First Cell");

        cell = table.AddRow().Cells[1];
        cell.AddParagraph("Last Cell within this table");
        cell.Borders.Bottom.Width = 15;
        cell.Shading.Color = Colors.LightBlue;

        section.AddParagraph("A Paragraph afterwards");
        return document;
    }

    private static Document OneRowOf(double height)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(3));
        table.Rows.HeightRule = RowHeightRule.Exactly;
        table.Rows.Height = height;
        table.AddRow()[0].AddParagraph("x");
        return document;
    }

    private static Document Aligned(VerticalAlignment alignment)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        table.AddColumn();
        table.AddColumn();

        var row = table.AddRow();
        row.HeightRule = RowHeightRule.Exactly;
        row.Height = 70;
        row.VerticalAlignment = alignment;
        row[0].AddParagraph("First Cell");
        row[1].AddParagraph("Second Cell");
        return document;
    }

    // ----- a cell over every column at once -----
    //
    // The shape in https://github.com/empira/PDFsharp/issues/358, where upstream's table renderer
    // throws "GetMinMergedCell: Unexpected problem #1" while laying it out. It is not a defect
    // here: that method still looks for the cell whose merge ends soonest at or below the row it
    // is asked about, where upstream's rewrite of it asks each cell of the row whether its merge
    // ends on that row - which is a way of asking whether it is merged downwards at all, and no
    // cell of such a row is not. Grafting upstream's version into this fork fails all three of
    // these with that message, so they are a guard rather than a formality.

    [Theory]
    [InlineData(2, 1)]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    public void ACellOverEveryColumnAndMoreThanOneRowIsDrawnWithTheRowsBelowIt(int columns, int mergeDown)
    {
        var page = Rendered.FirstPageOf(SpanningEveryColumn(columns, mergeDown));

        // One run of text for the cell that spans the table, and one for each cell of the ordinary
        // row underneath it - so the table was laid out to the end rather than abandoned part way.
        TextOperators.ShownStrings(page).Count.Should().Be(1 + columns);
    }

    /// <summary>
    ///   A table whose first cell covers every column and more than one row, with an ordinary row
    ///   under it.
    /// </summary>
    private static Document SpanningEveryColumn(int columns, int mergeDown)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;

        for (var column = 0; column < columns; column++)
            table.AddColumn(Unit.FromCentimeter(2));

        var first = table.AddRow();
        first[0].MergeRight = columns - 1;
        first[0].MergeDown = mergeDown;
        first[0].AddParagraph("over");

        // The rows the merge reaches into hold no cells of their own.
        for (var row = 0; row < mergeDown; row++)
            table.AddRow();

        var last = table.AddRow();
        for (var column = 0; column < columns; column++)
            last[column].AddParagraph("c" + column);

        return document;
    }

    // ----- a merge that runs off the edge of the table -----
    //
    // MergeRight and MergeDown are set on a cell while the table is still being built - the rows
    // and columns they speak of may be added afterwards, or never - so neither can be checked
    // against a table at the moment it is written, and a cell can end up claiming more of one than
    // there ever is. Every place that read a merge as a position then indexed past the end: the
    // formatter threw ArgumentOutOfRangeException naming nothing but "index", out of a document
    // the object model had accepted without a word. It is read as reaching the edge now, which is
    // the reading the renderer's own KeepWith arithmetic has always taken.

    [Fact]
    public void AMergeRunningPastTheLastRowDrawsWhatOneStoppingAtItDraws()
    {
        PageDrawnBy(Overmerged(right: 1, down: 5))
            .Should().Be(PageDrawnBy(Overmerged(right: 1, down: 2)));
    }

    [Fact]
    public void AMergeRunningPastTheLastColumnDrawsWhatOneStoppingAtItDraws()
    {
        PageDrawnBy(Overmerged(right: 9, down: 1))
            .Should().Be(PageDrawnBy(Overmerged(right: 2, down: 1)));
    }

    [Fact]
    public void AMergeRunningPastBothEdgesIsStillJustTheTable()
    {
        PageDrawnBy(Overmerged(right: 9, down: 5))
            .Should().Be(PageDrawnBy(Overmerged(right: 2, down: 2)));
    }

    [Fact]
    public void TheCellsTheMergeDoesNotReachAreStillDrawn()
    {
        // So that the comparisons above are between two pages with something on them rather than
        // between two tables that came out empty in the same way.
        var page = Rendered.FirstPageOf(Overmerged(right: 1, down: 5));

        // The merged cell plus the three cells of the column it does not cover.
        TextOperators.ShownStrings(page).Count.Should().Be(4);
    }

    /// <summary>
    ///   A three by three table, every cell with a word in it, whose first cell is merged as far
    ///   as the caller says - which may be further than the table goes.
    /// </summary>
    private static Document Overmerged(int right, int down)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;

        for (var column = 0; column < 3; column++)
            table.AddColumn(Unit.FromCentimeter(3));

        var rows = new[] { table.AddRow(), table.AddRow(), table.AddRow() };
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
                rows[row][column].AddParagraph("r" + row + "c" + column);

        rows[0][0].MergeRight = right;
        rows[0][0].MergeDown = down;

        return document;
    }

    /// <summary>The content stream of the first page, as the bytes it was written as.</summary>
    private static string PageDrawnBy(Document document) =>
        Encoding.Latin1.GetString(PageContent.Of(Rendered.FirstPageOf(document)));

    /// <summary>A two by two table, with whatever the caller wants merged in it merged.</summary>
    private static Document Merged(Action<(Cell TopLeft, Cell TopRight)> merge)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(3));
        table.AddColumn(Unit.FromCentimeter(3));

        var top = table.AddRow();
        var bottom = table.AddRow();
        top[0].AddParagraph("a");
        top[1].AddParagraph("b");
        bottom[0].AddParagraph("c");
        bottom[1].AddParagraph("d");

        merge((top[0], top[1]));
        return document;
    }

    /// <summary>
    ///   The vertical segments standing on the column edge between the two cells of the first row,
    ///   which is the edge a merge to the right does away with.
    /// </summary>
    private static IReadOnlyList<StrokedLines.Line> SegmentsDownTheMiddleOfTheFirstRow(Document document)
    {
        var page = Rendered.FirstPageOf(document);
        var lines = StrokedLines.Of(page);

        var middle = Middle(lines.Where(line => line.IsVertical).Select(line => line.X1));
        var firstRowFoot = HorizontalRules(page)[1];

        return lines
            .Where(line => line.IsVertical && Near(line.X1, middle) && line.Bottom > firstRowFoot - 0.5)
            .ToList();
    }

    /// <summary>
    ///   The horizontal segments lying on the row edge between the two cells of the second column,
    ///   which is the edge a merge downwards does away with.
    /// </summary>
    private static IReadOnlyList<StrokedLines.Line> SegmentsAcrossTheMiddleOfTheSecondColumn(Document document)
    {
        var page = Rendered.FirstPageOf(document);
        var lines = StrokedLines.Of(page);

        var middle = Middle(lines.Where(line => line.IsHorizontal).Select(line => line.Y1));
        var secondColumnLeft = Middle(lines.Where(line => line.IsVertical).Select(line => line.X1));

        return lines
            .Where(line => line.IsHorizontal && Near(line.Y1, middle)
                           && Math.Max(line.X1, line.X2) > secondColumnLeft + 0.5)
            .ToList();
    }

    /// <summary>The distinct heights the page rules at, from the top of the page downwards.</summary>
    private static IReadOnlyList<double> HorizontalRules(PdfPinata.Pdf.PdfPage page)
    {
        return StrokedLines.Of(page)
            .Where(line => line.IsHorizontal)
            .Select(line => Math.Round(line.Y1, 2))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    private static double TextBaselineOf(Document document)
    {
        return TextBaselines.LinesOf(Rendered.FirstPageOf(document)).Single();
    }

    /// <summary>The middle one of three evenly spaced positions, to the nearest hundredth.</summary>
    private static double Middle(IEnumerable<double> positions)
    {
        return positions.Select(position => Math.Round(position, 2)).Distinct().OrderBy(p => p).ElementAt(1);
    }

    private static bool Near(double one, double other) => Math.Abs(one - other) < 0.01;
}
