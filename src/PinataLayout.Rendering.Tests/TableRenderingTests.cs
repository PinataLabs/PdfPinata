using System;
using System.Collections.Generic;
using System.Linq;
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
///   Promoted from MigraDoc 1.32's TestTable, which built one of each of these and saved it to a
///   file for a person to look at. Each arrangement is kept; what was a look is now an assertion
///   about the segments and the text positions in the content stream, which is exact and does not
///   need the page rasterized.
/// </remarks>
public class TableRenderingTests
{
    /// <summary>The width a table gives a border it was not told the width of.</summary>
    const double DefaultBorderWidth = 0.5;

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
    static Document Bordered()
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

    static Document OneRowOf(double height)
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

    static Document Aligned(VerticalAlignment alignment)
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

    /// <summary>A two by two table, with whatever the caller wants merged in it merged.</summary>
    static Document Merged(Action<(Cell TopLeft, Cell TopRight)> merge)
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
    static IReadOnlyList<StrokedLines.Line> SegmentsDownTheMiddleOfTheFirstRow(Document document)
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
    static IReadOnlyList<StrokedLines.Line> SegmentsAcrossTheMiddleOfTheSecondColumn(Document document)
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
    static IReadOnlyList<double> HorizontalRules(PdfPinata.Pdf.PdfPage page)
    {
        return StrokedLines.Of(page)
            .Where(line => line.IsHorizontal)
            .Select(line => Math.Round(line.Y1, 2))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    static double TextBaselineOf(Document document)
    {
        return TextBaselines.LinesOf(Rendered.FirstPageOf(document)).Single();
    }

    /// <summary>The middle one of three evenly spaced positions, to the nearest hundredth.</summary>
    static double Middle(IEnumerable<double> positions)
    {
        return positions.Select(position => Math.Round(position, 2)).Distinct().OrderBy(p => p).ElementAt(1);
    }

    static bool Near(double one, double other) => Math.Abs(one - other) < 0.01;
}
