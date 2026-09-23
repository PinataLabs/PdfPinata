using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   A row of a table has as many cells as the table has columns. The cells used to be made only
///   as they were asked for by index, so a row reported however many of them something had
///   happened to touch. Enumerating a row reached no further than that, which made a loop over
///   every cell of every row quietly pass some of them by: the loop that colours a whole table
///   coloured a triangle of it, and left the rest of the table in the colour it started in.
/// </summary>
public class TableRowCellsTests
{
    private const int Columns = 4;

    [Fact]
    public void ARowHasACellForEveryColumnOfItsTable()
    {
        var table = ATableOf(Columns, rows: 3);

        foreach (Row row in table.Rows)
            row.Cells.Count.Should().Be(Columns);
    }

    [Fact]
    public void EnumeratingARowReachesEveryOneOfItsCells()
    {
        var table = ATableOf(Columns, rows: 3);

        foreach (Row row in table.Rows)
            ColumnIndicesOf(row).Should().Equal(Enumerable.Range(0, Columns));
    }

    [Fact]
    public void ARowStillHasEveryCellWhenSomethingHasAlreadyTouchedOne()
    {
        // Touching a cell by index is what used to decide how many the row had.
        var table = ATableOf(Columns, rows: 3);
        table.Rows[1][0].AddParagraph("Test");

        ColumnIndicesOf(table.Rows[1]).Should().Equal(Enumerable.Range(0, Columns));
    }

    [Fact]
    public void SettingSomethingOnEveryCellOfEveryRowReachesTheWholeTable()
    {
        // The loop from https://github.com/empira/PDFsharp/issues/228, which reached a triangle
        // of the table and left the rest of it uncoloured.
        var table = ATableOf(Columns, rows: Columns);
        var green = new Color(0, 255, 0);

        foreach (Row row in table.Rows)
            foreach (Cell cell in row.Cells)
                cell.Borders.Color = green;

        for (var row = 0; row < Columns; row++)
            for (var column = 0; column < Columns; column++)
                ColourOf(table[row, column]).Should().Be(green);
    }

    [Fact]
    public void ARowAddedRatherThanCreatedByTheTableHasItsCellsToo()
    {
        // Not every row arrives through AddRow.
        var table = ATableOf(Columns, rows: 0);
        table.Rows.Add(new Row());

        table.Rows[0].Cells.Count.Should().Be(Columns);
    }

    [Fact]
    public void ARowInsertedAmongTheOthersHasItsCellsToo()
    {
        var table = ATableOf(Columns, rows: 2);
        table.Rows.InsertObject(1, new Row());

        table.Rows[1].Cells.Count.Should().Be(Columns);
        table.Rows.Count.Should().Be(3);
    }

    [Fact]
    public void TheCellsOfARowAreTheCellsTheTableAddressesByIndex()
    {
        // Filling the row must not leave the table addressing different objects.
        var table = ATableOf(Columns, rows: 2);

        for (var row = 0; row < 2; row++)
            for (var column = 0; column < Columns; column++)
                table[row, column].Should().BeSameAs(table.Rows[row].Cells[column]);
    }

    [Fact]
    public void ARowWithNoTableYetHasNoCellsToGive()
    {
        // A row on its own knows no column count, and asking for one must not invent cells.
        var row = new Row();

        row.Cells.Count.Should().Be(0);
    }

    [Fact]
    public void ATableSurvivesAWriteAndAReadUnchanged()
    {
        // The cells now written out explicitly are read back as the same table.
        var document = new Document();
        var table = document.AddSection().AddTable();
        for (var column = 0; column < Columns; column++)
            table.AddColumn(Unit.FromCentimeter(2));
        table.AddRow()[0].AddParagraph("Test");

        var written = DdlWriter.WriteToString(document);
        var rewritten = DdlWriter.WriteToString(DdlReader.DocumentFromString(written));

        rewritten.Should().Be(written);
    }

    [Fact]
    public void ATableReadFromDdlHasACellForEveryColumnAsWell()
    {
        var document = DdlReader.DocumentFromString(
            @"\document{ \section{ \table{
                  \columns{ \column \column \column \column }
                  \rows{ \row{ \cell{ \paragraph{Test} } } }
              } } }");

        var table = (Table)document.Sections[0].Elements[0];

        table.Rows[0].Cells.Count.Should().Be(Columns);
    }

    private static IReadOnlyList<int> ColumnIndicesOf(Row row)
    {
        var indices = new List<int>();
        foreach (Cell cell in row.Cells)
            indices.Add(cell.Column.Index);
        return indices;
    }

    /// <summary>
    ///   Read without creating what is not there: a cell the loop never reached has no borders,
    ///   and asking for them the ordinary way would make some and hide that.
    /// </summary>
    private static Color ColourOf(Cell cell)
    {
        var borders = (Borders)cell.GetValue("Borders", GV.GetNull);
        borders.Should().NotBeNull(
            "the cell at row {0}, column {1} should have been reached", cell.Row.Index, cell.Column.Index);
        return borders.Color;
    }

    private static Table ATableOf(int columns, int rows)
    {
        var table = new Document().AddSection().AddTable();
        for (var column = 0; column < columns; column++)
            table.AddColumn(Unit.FromCentimeter(2));
        for (var row = 0; row < rows; row++)
            table.AddRow();
        return table;
    }
}
