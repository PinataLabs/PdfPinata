using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   An edge two cells share is stroked once, by the cell drawn second, and an edge only one of
///   them draws on this page is still drawn.
/// </summary>
/// <remarks>
///   Each cell used to stroke all four of its edges, so every edge inside a table was stroked
///   twice, in the same place with the same pen - which a viewer blends twice along each side, and
///   so draws heavier than the edges round the outside. <c>TableSharedBorderRenderingTests</c>
///   measures that on the page; these say which strokes are left, and that leaving one out never
///   leaves a gap. See empira/PDFsharp#408.
/// </remarks>
public class TableSharedEdgeTests
{
    private const int Columns = 3;

    [Test]
    public void EveryEdgeOfARuledGridIsStrokedOnce()
    {
        var page = Rendered.FirstPageOf(Grid(rows: 3));

        var segments = Segments(page);

        // Four rules across, three cells long, and four down, three cells tall.
        segments.Should().OnlyHaveUniqueItems();
        segments.Should().HaveCount(2 * (3 + 1) * Columns);
    }

    [Test]
    public void TheLastRowOnAPageKeepsItsFootWhenTheRowBelowItGoesOnToTheNextPage()
    {
        var document = Rendered.Of(Grid(rows: 80));
        document.PageCount.Should().BeGreaterThan(1);

        foreach (var page in document.Pages)
            ShouldBeFullyRuled(page);
    }

    [Test]
    public void AHeadingRowRepeatedOnTheNextPageSharesItsFootWithTheRowBelowItOnce()
    {
        var document = Rendered.Of(Grid(rows: 80, headingRows: 1));
        document.PageCount.Should().BeGreaterThan(1);

        // The second page starts with the heading row drawn again, and the row under it is not the
        // one under it in the table.
        ShouldBeFullyRuled(document.Pages[1]);
    }

    [Test]
    [Arguments(RoundedCorner.TopLeft)]
    [Arguments(RoundedCorner.TopRight)]
    public void ACellBelowWhoseTopIsRoundedLeavesTheEdgeToTheCellAboveIt(RoundedCorner corner)
    {
        // A rounded corner draws an arc in place of the two straight edges meeting there, so the
        // cell below draws no straight top for the cell above to leave its foot to.
        var document = Grid(rows: 2);
        var table = (Table)document.LastSection.Elements[0];
        table[1, 1].RoundedCorner = corner;

        var page = Rendered.FirstPageOf(document);
        var rules = Segments(page).Where(segment => segment.Horizontal).ToList();
        var between = rules.Select(rule => rule.Y).Distinct().OrderByDescending(y => y).ElementAt(1);

        rules.Where(rule => rule.Y == between).Should().HaveCount(Columns,
            "the cell above the rounded one still draws its foot");

        // And the arc takes the rounded cell's side as well, which the cell beside it keeps.
        Segments(page).Where(segment => !segment.Horizontal).GroupBy(segment => segment.X)
            .Should().AllSatisfy(rule => rule.Should().HaveCount(2, "every column edge is ruled down both rows"));
    }

    [Test]
    public void AnEdgeOnlyPartlySharedIsDrawnByBothCells()
    {
        // The first cell of the top row spans two columns, so its foot runs under two cells of the
        // row below, and neither of their tops is the same stroke as it.
        var document = Grid(rows: 2);
        var table = (Table)document.LastSection.Elements[0];
        table[0, 0].MergeRight = 1;

        var page = Rendered.FirstPageOf(document);
        var rules = Segments(page).Where(segment => segment.Horizontal).ToList();
        var between = rules.Select(rule => rule.Y).Distinct().OrderByDescending(y => y).ElementAt(1);

        // The spanning cell's foot, which matches no top below it, and the three tops. The foot of
        // the last cell above is the same stroke as the top of the one below it, and goes.
        rules.Where(rule => rule.Y == between).Should().HaveCount(1 + Columns);
    }

    /// <summary>
    ///   That the page strokes each edge it draws once and leaves none out: every rule across the
    ///   table is <see cref="Columns"/> segments long, with one more rule than there are rows.
    /// </summary>
    private static void ShouldBeFullyRuled(PdfPage page)
    {
        var segments = Segments(page);
        segments.Should().OnlyHaveUniqueItems();

        var rows = TextOperators.ShownStrings(page).Count / Columns;
        var rules = segments.Where(segment => segment.Horizontal).GroupBy(segment => segment.Y).ToList();

        rules.Should().HaveCount(rows + 1);
        rules.Should().AllSatisfy(rule => rule.Should().HaveCount(Columns));
    }

    /// <summary>A table ruled round every cell, one word in each, and the first rows headings if asked.</summary>
    private static Document Grid(int rows, int headingRows = 0)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;

        for (var column = 0; column < Columns; column++)
            table.AddColumn(Unit.FromCentimeter(3));

        for (var r = 0; r < rows; r++)
        {
            var row = table.AddRow();
            row.HeadingFormat = r < headingRows;
            for (var column = 0; column < Columns; column++)
                row[column].AddParagraph("r" + r + "c" + column);
        }

        return document;
    }

    /// <summary>
    ///   Every straight segment the page strokes, rounded to a hundredth of a point and with its
    ///   ends in a fixed order, so that two strokes of the one edge compare equal.
    /// </summary>
    private static List<Segment> Segments(PdfPage page) =>
    [
        ..StrokedLines.Of(page).Select(line => new Segment(
            line.IsHorizontal,
            Math.Round(Math.Min(line.X1, line.X2), 2),
            Math.Round(Math.Min(line.Y1, line.Y2), 2),
            Math.Round(Math.Max(line.X1, line.X2), 2),
            Math.Round(Math.Max(line.Y1, line.Y2), 2)))
    ];

    private readonly record struct Segment(bool Horizontal, double X, double Y, double Right, double Top);
}
