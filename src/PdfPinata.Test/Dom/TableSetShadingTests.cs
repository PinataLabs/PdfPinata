using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Colouring a block of cells. The call takes a corner and a size rather than two corners, and
///   four of its five arguments can be wrong in two directions each, so nearly all of it is the
///   range check.
///   <para>
///   Every assertion here says what happened to the cells outside the block as well as inside it.
///   A range that runs one cell too far is not visible in the cells it was asked to colour - they
///   are all correct - and there is no other way to see it.
///   </para>
/// </summary>
public class TableSetShadingTests
{
    private const int Columns = 4;
    private const int Rows = 3;

    private static Table ATable() => ATableOf(Columns, Rows);

    private static Table ATableOf(int columns, int rows)
    {
        var table = new Document().AddSection().AddTable();
        for (var column = 0; column < columns; column++)
            table.AddColumn(Unit.FromCentimeter(2));
        for (var row = 0; row < rows; row++)
            table.AddRow();
        return table;
    }

    /// <summary>
    ///   The table as a picture: one character per cell, '#' where the shading is the colour
    ///   asked for and '.' where it is not. Reading a whole table at once is the only way an
    ///   assertion can say the block stopped where it was meant to.
    /// </summary>
    private static string Shaded(Table table, Color colour)
    {
        var picture = new System.Text.StringBuilder();
        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Columns.Count; column++)
                picture.Append(table[row, column].Shading.Color == colour ? '#' : '.');
            if (row < table.Rows.Count - 1)
                picture.Append('/');
        }
        return picture.ToString();
    }

    // ----- the block it colours ---------------------------------------------------------------

    [Fact]
    public void ABlockIsColouredAndNothingAroundItIs()
    {
        var table = ATable();

        table.SetShading(clm: 1, row: 0, clms: 2, rows: 2, clr: Colors.Red);

        Shaded(table, Colors.Red).Should().Be(".##./.##./....");
    }

    [Fact]
    public void ASingleCellIsABlockOfOne()
    {
        var table = ATable();

        table.SetShading(clm: 2, row: 1, clms: 1, rows: 1, clr: Colors.Red);

        Shaded(table, Colors.Red).Should().Be("..../..#./....");
    }

    [Fact]
    public void TheWholeTableIsABlockToo()
    {
        var table = ATable();

        table.SetShading(0, 0, Columns, Rows, Colors.Red);

        Shaded(table, Colors.Red).Should().Be("####/####/####");
    }

    [Fact]
    public void ABlockThatEndsOnTheLastCellIsAllowed()
    {
        // The off-by-one that the count checks exist for, from the inside: clm + clms is exactly
        // the column count here, which is the largest it may be.
        var table = ATable();

        table.SetShading(clm: 2, row: 1, clms: 2, rows: 2, clr: Colors.Red);

        Shaded(table, Colors.Red).Should().Be("..../..##/..##");
    }

    [Fact]
    public void ColouringOneBlockAndThenAnotherLeavesBoth()
    {
        var table = ATable();

        table.SetShading(0, 0, 1, 1, Colors.Red);
        table.SetShading(3, 2, 1, 1, Colors.Red);

        Shaded(table, Colors.Red).Should().Be("#.../..../...#");
    }

    // ----- the ranges it refuses --------------------------------------------------------------

    [Theory]
    [InlineData(0, -1, 1, 1, "row")]
    [InlineData(0, Rows, 1, 1, "row")]
    [InlineData(-1, 0, 1, 1, "clm")]
    [InlineData(Columns, 0, 1, 1, "clm")]
    [InlineData(0, 0, 1, 0, "rows")]
    [InlineData(0, 0, 1, -1, "rows")]
    [InlineData(0, 1, 1, Rows, "rows")]
    [InlineData(0, 0, 0, 1, "clms")]
    [InlineData(0, 0, -1, 1, "clms")]
    [InlineData(1, 0, Columns, 1, "clms")]
    public void ARangeOutsideTheTableIsRefusedAndSaysWhichArgumentIsWrong(
        int clm, int row, int clms, int rows, string offendingArgument)
    {
        var table = ATable();

        var shade = () => table.SetShading(clm, row, clms, rows, Colors.Red);

        shade.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be(offendingArgument);
    }

    [Fact]
    public void ARefusedRangeColoursNothingAtAll()
    {
        // A range check that ran cell by cell would colour the part of the block that fits and
        // then throw, which is worse than refusing outright.
        var table = ATable();

        table.Invoking(t => t.SetShading(clm: 2, row: 0, clms: 3, rows: 1, clr: Colors.Red))
            .Should().Throw<ArgumentOutOfRangeException>();

        Shaded(table, Colors.Red).Should().Be("..../..../....");
    }

    /// <summary>
    ///   A table that has had nothing added to it has no cell to colour, and says so. It used to
    ///   throw NullReferenceException instead: the range checks read the backing fields, which are
    ///   null until something has asked for the collection, so the first line of the method failed
    ///   before any of the checks below it could run.
    /// </summary>
    [Fact]
    public void AnEmptyTableHasNoRangeToColour()
    {
        var empty = new Document().AddSection().AddTable();

        var shade = () => empty.SetShading(0, 0, 1, 1, Colors.Red);

        shade.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("row");
    }

    [Fact]
    public void ATableWithColumnsButNoRowsHasNoRangeToColourEither()
    {
        var noRows = new Document().AddSection().AddTable();
        noRows.AddColumn(Unit.FromCentimeter(2));

        var shade = () => noRows.SetShading(0, 0, 1, 1, Colors.Red);

        shade.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("row");
    }
}
