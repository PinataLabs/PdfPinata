using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   A table's rows and columns as objects rather than as a grid: what each one remembers, what it
///   writes out, and the two directions it is reached from.
///   <para>
///   <see cref="CellTests"/> covers the cells and <see cref="BordersTests"/> the borders around
///   them. What is left is everything a row or a column carries on behalf of the cells in it -
///   padding, shading, a paragraph format, a style name, a keep-with count - all of which is
///   written into the DDL only when it was set, so a table left alone exercises none of it.
///   </para>
/// </summary>
public class TableStructureFormattingTests
{
    private static Table ATable(int columns = 2, int rows = 2)
    {
        var table = new Document().AddSection().AddTable();
        for (var idx = 0; idx < columns; idx++)
            table.AddColumn(Unit.FromCentimeter(2));
        for (var idx = 0; idx < rows; idx++)
            table.AddRow();
        return table;
    }

    private static string DdlOf(DocumentObject documentObject) => DdlWriter.WriteToString(documentObject);

    // ----- reaching a cell from either side ---------------------------------------------------

    /// <summary>
    ///   A cell is at a row and a column, and either can be asked for it. The column's indexer
    ///   walks back up to the table through its collection, which is the one route nothing else
    ///   takes - a column knows its table only by asking its parent's parent.
    /// </summary>
    [Fact]
    public void AColumnReachesTheSameCellTheRowDoes()
    {
        var table = ATable();
        table[1, 0].AddParagraph("bottom left");

        table.Columns[0][1].Should().BeSameAs(table.Rows[1][0]);
        table.Columns[0].Table.Should().BeSameAs(table);
        table.Rows[1].Table.Should().BeSameAs(table);
        table.Columns[0].Index.Should().Be(0);
        table.Rows[1].Index.Should().Be(1);
    }

    // ----- built on their own -----------------------------------------------------------------

    /// <summary>
    ///   Widths given to the collection's constructor become one column each, which is the shortest
    ///   way to describe a table's shape and the one route that builds a column without a table
    ///   above it.
    /// </summary>
    [Fact]
    public void AColumnCollectionCanBeBuiltFromNothingButWidths()
    {
        var columns = new Columns(Unit.FromCentimeter(1), Unit.FromCentimeter(2), Unit.FromCentimeter(3));

        columns.Count.Should().Be(3);
        columns[2].Width.Centimeter.Should().Be(3);
        new Columns().Count.Should().Be(0);
    }

    [Fact]
    public void ARowAndAColumnCopyThemselvesIntoTheirOwnType()
    {
        var table = ATable();
        table.Columns[0].Comment = "the first";
        table.Rows[0].Comment = "the top";

        table.Columns[0].Clone().Comment.Should().Be("the first");
        table.Rows[0].Clone().Comment.Should().Be("the top");
        table.Columns.Clone().Count.Should().Be(2);
    }

    /// <summary>
    ///   Columns describe the shape of the grid, so adding one after the rows exist would leave
    ///   every row a cell short. The collection refuses rather than growing the rows behind the
    ///   caller, because a cell added silently is a cell nothing has written into.
    /// </summary>
    [Fact]
    public void AColumnCannotBeAddedOnceThereAreRowsToBeShortOfIt()
    {
        var table = ATable(2, 1);

        var adding = () => table.AddColumn();

        adding.Should().Throw<InvalidOperationException>()
            .WithMessage("*rows collection is not empty*");
    }

    // ----- what a column writes ---------------------------------------------------------------

    [Fact]
    public void AColumnWritesEverythingItWasGivenAndNothingItWasNot()
    {
        var table = ATable(1, 0);
        var column = table.Columns[0];

        DdlOf(table).Should().NotContain("HeadingFormat");

        column.Style = "Normal";
        column.Format.SpaceBefore = Unit.FromPoint(3);
        column.HeadingFormat = true;
        column.LeftPadding = Unit.FromPoint(4);
        column.RightPadding = Unit.FromPoint(5);
        column.KeepWith = 2;
        column.Borders.Width = Unit.FromPoint(1);
        column.Shading.Color = Colors.Azure;
        column.Comment = "the only column";

        column.Style.Should().Be("Normal");
        column.KeepWith.Should().Be(2);
        column.HeadingFormat.Should().BeTrue();
        column.Comment.Should().Be("the only column");

        var ddl = DdlOf(table);
        ddl.Should().Contain("Style = \"Normal\"")
            .And.Contain("HeadingFormat = true")
            .And.Contain("LeftPadding = 4")
            .And.Contain("RightPadding = 5")
            .And.Contain("KeepWith = 2")
            .And.Contain("SpaceBefore = 3")
            .And.Contain("Shading")
            .And.Contain("the only column");
    }

    [Fact]
    public void AColumnHandedAFormatBordersOrShadingUsesTheOneItWasHanded()
    {
        var table = ATable(1, 0);
        var column = table.Columns[0];

        column.Format = new ParagraphFormat { SpaceAfter = Unit.FromPoint(7) };
        column.Borders = new Borders { Width = Unit.FromPoint(2) };
        column.Shading = new Shading { Color = Colors.Beige };

        column.Format.SpaceAfter.Point.Should().Be(7);
        column.Borders.Width.Point.Should().Be(2);
        column.Shading.Color.Should().Be(Colors.Beige);
    }

    /// <summary>
    ///   A width on the collection is the default every column without one of its own is drawn at,
    ///   and it is written at the collection rather than repeated on each column.
    /// </summary>
    [Fact]
    public void AWidthOnTheCollectionIsWrittenOnceForEveryColumn()
    {
        var table = new Document().AddSection().AddTable();
        table.AddColumn();
        table.Columns.Width = Unit.FromCentimeter(4);
        table.Columns.Comment = "all the same";

        table.Columns.Width.Centimeter.Should().Be(4);
        table.Columns.Comment.Should().Be("all the same");
        DdlOf(table).Should().Contain("Width = \"4cm\"").And.Contain("all the same");
    }

    /// <summary>
    ///   A table with no columns cannot be drawn at all, and the writer says so in the file rather
    ///   than leaving a reader to work out why nothing appeared.
    /// </summary>
    [Fact]
    public void ATableWithNoColumnsIsWrittenWithAComplaintInIt()
    {
        var table = new Document().AddSection().AddTable();

        DdlOf(table).Should().Contain("Table will not render");
    }

    // ----- what a row writes ------------------------------------------------------------------

    [Fact]
    public void ARowWritesEverythingItWasGivenAndNothingItWasNot()
    {
        var table = ATable(1, 1);
        var row = table.Rows[0];

        DdlOf(table).Should().NotContain("HeightRule");

        row.Style = "Normal";
        row.Format.SpaceBefore = Unit.FromPoint(3);
        row.VerticalAlignment = VerticalAlignment.Center;
        row.Height = Unit.FromPoint(30);
        row.HeightRule = RowHeightRule.Exactly;
        row.TopPadding = Unit.FromPoint(4);
        row.BottomPadding = Unit.FromPoint(5);
        row.HeadingFormat = true;
        row.KeepWith = 1;
        row.Borders.Width = Unit.FromPoint(1);
        row.Shading.Color = Colors.Azure;
        row.Comment = "the only row";

        row.VerticalAlignment.Should().Be(VerticalAlignment.Center);
        row.Style.Should().Be("Normal");
        row.Comment.Should().Be("the only row");

        var ddl = DdlOf(table);
        ddl.Should().Contain("Style = \"Normal\"")
            .And.Contain("VerticalAlignment = Center")
            .And.Contain("Height = 30")
            .And.Contain("HeightRule = Exactly")
            .And.Contain("TopPadding = 4")
            .And.Contain("BottomPadding = 5")
            .And.Contain("HeadingFormat = true")
            .And.Contain("KeepWith = 1")
            .And.Contain("the only row");
    }

    [Fact]
    public void ARowHandedAFormatBordersOrShadingUsesTheOneItWasHanded()
    {
        var table = ATable(1, 1);
        var row = table.Rows[0];

        row.Format = new ParagraphFormat { SpaceAfter = Unit.FromPoint(7) };
        row.Borders = new Borders { Width = Unit.FromPoint(2) };
        row.Shading = new Shading { Color = Colors.Beige };

        row.Format.SpaceAfter.Point.Should().Be(7);
        row.Borders.Width.Point.Should().Be(2);
        row.Shading.Color.Should().Be(Colors.Beige);
    }
}
