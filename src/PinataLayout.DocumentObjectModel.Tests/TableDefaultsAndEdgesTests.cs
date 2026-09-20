using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The defaults a <see cref="Table"/> carries for the cells inside it — a style, a paragraph
///   format, four paddings, borders and a shading — and <see cref="Table.SetEdge(int,int,int,int,Edge,BorderStyle,Unit,Color)"/>,
///   which is how a caller draws a rule across a range of cells rather than cell by cell.
///   <para>
///   Every one of those defaults is written out only when it was set, so the DDL is what says
///   whether a property is actually being carried rather than merely readable.
///   </para>
/// </summary>
public class TableDefaultsAndEdgesTests
{
    static Table ATable(int columns = 2, int rows = 2)
    {
        var table = new Document().AddSection().AddTable();
        for (var index = 0; index < columns; index++)
            table.AddColumn("2cm");
        for (var index = 0; index < rows; index++)
            table.AddRow();
        return table;
    }

    [Fact]
    public void ATableIsEmptyUntilItHasBothColumnsAndRows()
    {
        var table = new Document().AddSection().AddTable();

        table.IsEmpty.Should().BeTrue();

        table.AddColumn("2cm");
        table.IsEmpty.Should().BeTrue("columns alone hold nothing");

        table.AddRow();
        table.IsEmpty.Should().BeFalse();
    }

    // ----- the defaults ---------------------------------------------------------------------------

    [Fact]
    public void EachDefaultReadsBackAsItWasSet()
    {
        var table = ATable();

        table.Style.Should().BeEmpty();
        table.Summary.Should().BeEmpty();
        table.Comment.Should().BeEmpty();

        table.Style = "Normal";
        table.Summary = "sales by quarter";
        table.Comment = "built by the quarterly job";
        table.TopPadding = "1mm";
        table.BottomPadding = "2mm";
        table.LeftPadding = "3mm";
        table.RightPadding = "4mm";

        table.Style.Should().Be("Normal");
        table.Summary.Should().Be("sales by quarter");
        table.Comment.Should().Be("built by the quarterly job");
        table.TopPadding.Millimeter.Should().BeApproximately(1, 1e-4);
        table.BottomPadding.Millimeter.Should().BeApproximately(2, 1e-4);
        table.LeftPadding.Millimeter.Should().BeApproximately(3, 1e-4);
        table.RightPadding.Millimeter.Should().BeApproximately(4, 1e-4);
    }

    [Fact]
    public void EveryDefaultThatWasSetIsWrittenOutAndTheRestAreNot()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn("2cm");
        table.AddRow();
        table.Style = "Normal";
        table.Summary = "sales by quarter";
        table.Comment = "built by the quarterly job";
        table.TopPadding = "1mm";
        table.BottomPadding = "2mm";
        table.LeftPadding = "3mm";
        table.RightPadding = "4mm";
        table.KeepTogether = true;
        table.Format.SpaceBefore = "5mm";
        table.Borders.Width = "1pt";
        table.Shading.Color = Colors.LightGray;

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("Style = \"Normal\"");
        ddl.Should().Contain("Summary = \"sales by quarter\"");
        ddl.Should().Contain("built by the quarterly job");
        ddl.Should().Contain("TopPadding = \"1mm\"");
        ddl.Should().Contain("BottomPadding = \"2mm\"");
        ddl.Should().Contain("LeftPadding = \"3mm\"");
        ddl.Should().Contain("RightPadding = \"4mm\"");
        ddl.Should().Contain("KeepTogether = true");
        ddl.Should().Contain("Borders");
        ddl.Should().Contain("Shading");
    }

    [Fact]
    public void ATableWithNoDefaultsSetWritesNoneOfThem()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn("2cm");
        table.AddRow();

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().NotContain("TopPadding");
        ddl.Should().NotContain("Summary");
        ddl.Should().NotContain("Style =");
    }

    [Fact]
    public void EachCompositePartOfATableCanBeAssignedWholesale()
    {
        var table = ATable();
        var other = ATable();
        other.Format.SpaceBefore = "5mm";
        other.Borders.Width = "1pt";
        other.Shading.Color = Colors.LightGray;

        table.Format = other.Format.Clone();
        table.Borders = other.Borders.Clone();
        table.Shading = other.Shading.Clone();
        table.Columns = other.Columns.Clone();
        table.Rows = other.Rows.Clone();

        table.Format.SpaceBefore.Millimeter.Should().BeApproximately(5, 1e-4);
        table.Borders.Width.Point.Should().BeApproximately(1, 1e-4);
        table.Shading.Color.Should().Be(Colors.LightGray);
        table.Columns.Count.Should().Be(2);
        table.Rows.Count.Should().Be(2);
    }

    [Fact]
    public void ATableClonesItselfWithItsRowsAndColumns()
    {
        var table = ATable();
        table[0, 0].AddParagraph("first");

        var clone = table.Clone();
        clone[0, 0].AddParagraph("second");

        clone.Rows.Count.Should().Be(2);
        clone.Columns.Count.Should().Be(2);
        table[0, 0].Elements.Count.Should().Be(1);
        clone[0, 0].Elements.Count.Should().Be(2);
    }

    // ----- the edges ------------------------------------------------------------------------------

    [Fact]
    public void SettingTheBoxEdgeDrawsOnlyTheOutsideOfTheRange()
    {
        var table = ATable(3, 3);

        table.SetEdge(0, 0, 2, 2, Edge.Box, BorderStyle.Single, "1pt", Colors.Red);

        table[0, 0].Borders.Top.Width.Point.Should().BeApproximately(1, 1e-4);
        table[0, 0].Borders.Left.Color.Should().Be(Colors.Red);
        table[1, 1].Borders.Bottom.Width.Point.Should().BeApproximately(1, 1e-4);
        table[1, 1].Borders.Right.Width.Point.Should().BeApproximately(1, 1e-4);
        table[2, 2].Borders.Top.Width.Should().Be(Unit.Empty, "the range stopped short of this cell");
    }

    [Fact]
    public void SettingTheInteriorEdgesDrawsBetweenTheCellsAndNotAround()
    {
        var table = ATable(2, 2);

        table.SetEdge(0, 0, 2, 2, Edge.Interior, BorderStyle.Single, "1pt", Colors.Blue);

        table[0, 0].Borders.Bottom.Width.Point.Should().BeApproximately(1, 1e-4);
        table[1, 0].Borders.Top.Width.Point.Should().BeApproximately(1, 1e-4);
        table[0, 0].Borders.Right.Width.Point.Should().BeApproximately(1, 1e-4);
        table[0, 1].Borders.Left.Width.Point.Should().BeApproximately(1, 1e-4);
        table[0, 0].Borders.Top.Width.Should().Be(Unit.Empty);
    }

    [Fact]
    public void BothDiagonalsCanBeDrawnAcrossARange()
    {
        var table = ATable();

        table.SetEdge(0, 0, 2, 2, Edge.DiagonalDown | Edge.DiagonalUp, BorderStyle.Single, "1pt", Colors.Green);

        table[0, 0].Borders.DiagonalDown.Width.Point.Should().BeApproximately(1, 1e-4);
        table[0, 0].Borders.DiagonalUp.Color.Should().Be(Colors.Green);
        table[1, 1].Borders.DiagonalDown.Width.Point.Should().BeApproximately(1, 1e-4);
    }

    [Fact]
    public void AnEdgeSetWithNoColourLeavesTheColourAlone()
    {
        var table = ATable();
        table[0, 0].Borders.Top.Color = Colors.Red;

        table.SetEdge(0, 0, 1, 1, Edge.Top, BorderStyle.Single, "2pt");

        table[0, 0].Borders.Top.Width.Point.Should().BeApproximately(2, 1e-4);
        table[0, 0].Borders.Top.Color.Should().Be(Colors.Red);
    }
}
