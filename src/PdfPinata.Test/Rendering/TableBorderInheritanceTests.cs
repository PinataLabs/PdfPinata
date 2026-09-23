using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   A cell that sets no borders of its own used to be handed the row's own Borders object
///   rather than a copy of it, so every such cell of the row held the one object between them.
///   Flattening the column's borders onto the first of those cells therefore wrote into the
///   row's borders, and every other cell of the row picked the result up: a right border given
///   to the first columns alone appeared on the last cell of the row as well.
///   See https://github.com/ststeiger/PdfSharpCore/issues/153.
/// </summary>
public class TableBorderInheritanceTests
{
    private const int Columns = 3;

    [Fact]
    public void TheLastCellOfAHeaderRowIsNotGivenTheBorderOfTheColumnsBeforeIt()
    {
        var page = Render(InnerColumnRulesWithABandedHeader);

        // Three columns ruled between them and nowhere else, so two lines, both inside the
        // table. The third, at the right edge of the last header cell, is the reported bug.
        var (left, right) = TableWidthOf(page);
        var edges = VerticalEdgesOf(page);

        edges.Should().HaveCount(2);
        edges.Should().AllSatisfy(x => x.Should().BeInRange(left + 1, right - 1));
    }

    [Fact]
    public void AHeaderRowIsRuledTheSameWayTheRowsBelowItAre()
    {
        var page = Render(InnerColumnRulesWithABandedHeader);

        // The header row is the top band of the table, the data row the one below it.
        var lines = StrokedLines.Of(page).Where(line => line.IsVertical).ToList();
        var middle = lines.Average(line => (line.Top + line.Bottom) / 2);

        var header = Distinct(lines.Where(line => (line.Top + line.Bottom) / 2 > middle));
        var data = Distinct(lines.Where(line => (line.Top + line.Bottom) / 2 < middle));

        // Only the header row carries row level borders, and that is what used to give it an
        // extra rule the data row never got.
        header.Should().Equal(data);
    }

    [Fact]
    public void ACellDoesNotShareItsBordersWithAnotherCell()
    {
        var document = new Document();
        var table = InnerColumnRulesWithABandedHeader(document);
        Render(document);

        var borders = AllCells(table).Select(cell => cell.Borders).ToList();
        borders.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ACellKeepsOwnershipOfItsBordersAcrossRendering()
    {
        var document = new Document();
        var table = InnerColumnRulesWithABandedHeader(document);
        Render(document);

        // Asking for the effective borders of a cell used to carry its neighbours' Border
        // objects off into a throwaway collection, which left Borders.Right of the cell next
        // door reporting itself as the "Left" of somewhere else. This is what the report was
        // written around.
        foreach (var cell in AllCells(table))
        {
            foreach (var slot in new[] { "Left", "Right", "Top", "Bottom" })
            {
                var border = cell.Borders.GetValue(slot, GV.GetNull) as Border;
                border?.Name.Should().Be(slot);
            }
        }
    }

    [Fact]
    public void ACellStillInheritsTheBordersOfItsRow()
    {
        var page = Render(InnerColumnRulesWithABandedHeader);

        // The band above and below the header row is set on the row, so it has to reach every
        // cell of that row: one rule at the top of the header and one at its foot, each of
        // them running past the outermost rule between the columns at either end.
        var rules = StrokedLines.Of(page)
            .Where(line => line.IsHorizontal)
            .GroupBy(line => System.Math.Round(line.Y1, 2))
            .ToList();

        var edges = VerticalEdgesOf(page);
        rules.Should().HaveCount(2);

        foreach (var rule in rules)
        {
            rule.Min(line => System.Math.Min(line.X1, line.X2)).Should().BeLessThan(edges[0]);
            rule.Max(line => System.Math.Max(line.X1, line.X2)).Should().BeGreaterThan(edges[^1]);
        }
    }

    /// <summary>
    ///   The table from the report: a rule down the inside of the table, set on every column
    ///   but the last, and a band above and below the header row, set on the row itself.
    /// </summary>
    private static Table InnerColumnRulesWithABandedHeader(Document document)
    {
        var table = document.AddSection().AddTable();

        for (var column = 0; column < Columns; column++)
        {
            table.AddColumn(Unit.FromCentimeter(4));

            if (column < Columns - 1)
            {
                table.Columns[column].Borders.Right.Color = Colors.Red;
                table.Columns[column].Borders.Right.Width = 1;
            }
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Borders.Top.Color = Colors.Green;
        header.Borders.Bottom.Color = Colors.Green;

        var data = table.AddRow();
        for (var column = 0; column < Columns; column++)
        {
            header.Cells[column].AddParagraph($"Header {column}");
            data.Cells[column].AddParagraph($"Data {column}");
        }

        return table;
    }

    private static IEnumerable<Cell> AllCells(Table table)
    {
        for (var row = 0; row < table.Rows.Count; row++)
        for (var column = 0; column < table.Columns.Count; column++)
            yield return table[row, column];
    }

    /// <summary>The distinct positions the page rules a vertical line at.</summary>
    private static IReadOnlyList<double> VerticalEdgesOf(PdfPage page)
    {
        return Distinct(StrokedLines.Of(page).Where(line => line.IsVertical));
    }

    private static IReadOnlyList<double> Distinct(IEnumerable<StrokedLines.Line> lines)
    {
        return [..lines.Select(line => System.Math.Round(line.X1, 2)).Distinct().OrderBy(x => x)];
    }

    /// <summary>
    ///   Where the table begins and ends across the page, taken from the rules the header row
    ///   draws over its whole width rather than worked out from margins and column widths.
    /// </summary>
    private static (double Left, double Right) TableWidthOf(PdfPage page)
    {
        var rules = StrokedLines.Of(page).Where(line => line.IsHorizontal).ToList();
        rules.Should().NotBeEmpty();

        return (rules.Min(line => System.Math.Min(line.X1, line.X2)),
            rules.Max(line => System.Math.Max(line.X1, line.X2)));
    }

    private static PdfPage Render(System.Func<Document, Table> build)
    {
        var document = new Document();
        build(document);
        return Render(document);
    }

    private static PdfPage Render(Document document)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }
}
