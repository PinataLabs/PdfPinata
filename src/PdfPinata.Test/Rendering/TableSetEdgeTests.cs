using System;
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
///   An interior edge of a table lies between two cells and both of them describe it, so the
///   renderer draws whichever of the two facing borders is the thicker. SetEdge only ever wrote
///   the upper and the left of each pair, which left the other side of every interior edge
///   inheriting the table's own borders. Clearing the interior of a table whose borders are
///   visible therefore drew every one of them anyway, at the inherited width and, where the
///   fabricated border carried no colour of its own, in the wrong colour.
///   See https://github.com/empira/PDFsharp/issues/228.
/// </summary>
public class TableSetEdgeTests
{
    private const int Size = 4;
    private static readonly Color Green = new Color(0, 255, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearingTheInteriorOfATableLeavesOnlyItsOutline(bool colourTheBorders)
    {
        var page = Render(table =>
        {
            if (colourTheBorders)
                ColourEveryCell(table);
            ClearTheInterior(table);
        });

        // The four sides of the table and nothing between them. Colouring the cells used to
        // decide how many of the interior rules survived; now none of them do either way.
        var lines = StrokedLines.Of(page);
        Distinct(lines.Where(line => line.IsVertical), line => line.X1).Should().HaveCount(2);
        Distinct(lines.Where(line => line.IsHorizontal), line => line.Y1).Should().HaveCount(2);
    }

    [Fact]
    public void AnOutlineLeftBehindIsDrawnInTheColourTheCellsWereGiven()
    {
        var page = Render(table =>
        {
            ColourEveryCell(table);
            ClearTheInterior(table);
        });

        // The reported document drew some of what was left in black, because the borders that
        // stood in for the ones never written carried no colour.
        StrokedLines.Of(page).Select(line => line.Colour).Distinct().Should().Equal("0,1,0");
    }

    [Fact]
    public void SettingTheInteriorOfATableRulesBetweenEveryCell()
    {
        var page = Render(table => table.SetEdge(0, 0, Size, Size, Edge.Interior, BorderStyle.Single, 1));

        // Three rules between four columns, three between four rows, and the outline the table
        // carries in its own right.
        var lines = StrokedLines.Of(page);
        Distinct(lines.Where(line => line.IsVertical), line => line.X1).Should().HaveCount(Size + 1);
        Distinct(lines.Where(line => line.IsHorizontal), line => line.Y1).Should().HaveCount(Size + 1);
    }

    [Fact]
    public void ClearingTheInteriorOfAPartOfATableLeavesTheRestOfItRuled()
    {
        var page = Render(table => table.SetEdge(0, 0, 2, 2, Edge.Interior, BorderStyle.None, 0));

        // The rule between the first two columns is cleared where it crosses the block and drawn
        // where it does not, so it survives below the block and nowhere above it.
        var lines = StrokedLines.Of(page);
        var betweenTheFirstTwoColumns = Distinct(lines.Where(line => line.IsVertical), line => line.X1)
            .OrderBy(x => x).ElementAt(1);
        var middle = MiddleOf(lines);

        var rule = lines
            .Where(line => line.IsVertical && Math.Abs(line.X1 - betweenTheFirstTwoColumns) < 0.01)
            .ToList();

        rule.Should().NotBeEmpty();
        rule.Should().AllSatisfy(line => line.Top.Should().BeLessThan(middle));
    }

    [Fact]
    public void ClearingTheInteriorClearsBothSidesOfEveryEdgeItCrosses()
    {
        var table = Build();
        ClearTheInterior(table);

        // The cell to the right of an interior edge describes it as much as the cell to its
        // left does, and used to be left saying nothing at all.
        BorderOf(table[0, 0], "Right").Should().NotBeNull();
        BorderOf(table[0, 1], "Left").Should().NotBeNull();
        BorderOf(table[0, 0], "Bottom").Should().NotBeNull();
        BorderOf(table[1, 0], "Top").Should().NotBeNull();
    }

    [Fact]
    public void ClearingTheInteriorLeavesTheEdgesOfTheTableAlone()
    {
        var table = Build();
        ClearTheInterior(table);

        // Nothing outside the range of interior edges is touched, so the table keeps whatever
        // outline it inherits.
        BorderOf(table[0, 0], "Left").Should().BeNull();
        BorderOf(table[0, 0], "Top").Should().BeNull();
        BorderOf(table[Size - 1, Size - 1], "Right").Should().BeNull();
        BorderOf(table[Size - 1, Size - 1], "Bottom").Should().BeNull();
    }

    private static void ClearTheInterior(Table table)
    {
        table.SetEdge(0, 0, table.Columns.Count, table.Rows.Count, Edge.Interior, BorderStyle.None, 0);
    }

    /// <summary>
    ///   Addressed by index rather than by enumerating the rows, because a row holds only the
    ///   cells something has already asked it for and enumerating one reaches no further.
    /// </summary>
    private static void ColourEveryCell(Table table)
    {
        for (var row = 0; row < table.Rows.Count; row++)
            for (var column = 0; column < table.Columns.Count; column++)
                table[row, column].Borders.Color = Green;
    }

    private static Border BorderOf(Cell cell, string side)
    {
        return (cell.GetValue("Borders", GV.GetNull) as Borders)?.GetValue(side, GV.GetNull) as Border;
    }

    /// <summary>Halfway up the table, which for four rows is the foot of the second one.</summary>
    private static double MiddleOf(IReadOnlyList<StrokedLines.Line> lines)
    {
        var horizontal = lines.Where(line => line.IsHorizontal).ToList();
        return (horizontal.Max(line => line.Y1) + horizontal.Min(line => line.Y1)) / 2;
    }

    /// <summary>The distinct positions, to the nearest hundredth of a point.</summary>
    private static IReadOnlyList<double> Distinct(
        IEnumerable<StrokedLines.Line> lines, Func<StrokedLines.Line, double> position)
    {
        return lines.Select(line => Math.Round(position(line), 2)).Distinct().ToList();
    }

    private static Table Build()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        for (var column = 0; column < Size; column++)
            table.AddColumn(Unit.FromCentimeter(2));
        for (var row = 0; row < Size; row++)
            table.AddRow()[row].AddParagraph("Test");
        return table;
    }

    private static PdfPage Render(Action<Table> arrange)
    {
        var table = Build();
        arrange(table);

        var renderer = new PdfDocumentRenderer(true) { Document = table.Document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }
}
