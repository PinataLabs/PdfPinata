using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Two members that had never been executed: the deep copy a <see cref="Table"/> makes of itself,
///   and the serializer that writes a <see cref="LineFormat"/> into DDL.
/// </summary>
/// <remarks>
///   <c>Table</c> declares no <c>DeepCopy</c> of its own: <c>DocumentObject.DeepCopy</c> clones and
///   reparents every non-simple [DV] member - columns, rows, format, borders and shading - driven
///   by the descriptor, the same move a hand-written override used to make for each of the five by
///   name. What is worth asserting is that the copy is <em>independent</em>, the way
///   <c>Chart.DeepCopy</c> was asserted in batch 5.2. A deep copy that comes back non-null proves
///   nothing at all; one that still shares its rows with the original proves the opposite of what
///   it looks like.
/// </remarks>
public class TableCloneAndLineFormatTests
{
    private static Table ATableWithEveryChildSet()
    {
        var table = new Table();
        table.AddColumn(Unit.FromCentimeter(3));
        table.AddColumn(Unit.FromCentimeter(4));

        var row = table.AddRow();
        row.Cells[0].AddParagraph("left");
        row.Cells[1].AddParagraph("right");

        table.Borders.Width = 2;
        table.Shading.Color = Colors.LightGray;
        table.Format.Alignment = ParagraphAlignment.Center;

        return table;
    }

    private static string TextOfCell(Table table, int row, int column) =>
        // ReSharper disable once PossibleNullReferenceException
        (table[row, column].Elements[0] as Paragraph).Elements[0] is Text text ? text.Content : null;

    // ----- Table.DeepCopy ---------------------------------------------------------------------------

    [Fact]
    public void ACopiedTableCarriesEverythingTheOriginalHad()
    {
        var copy = ATableWithEveryChildSet().Clone();

        copy.Columns.Count.Should().Be(2);
        copy.Rows.Count.Should().Be(1);
        TextOfCell(copy, 0, 0).Should().Be("left");
        TextOfCell(copy, 0, 1).Should().Be("right");
        copy.Borders.Width.Point.Should().BeApproximately(2, 0.001);
        copy.Shading.Color.Should().Be(Colors.LightGray);
        copy.Format.Alignment.Should().Be(ParagraphAlignment.Center);
    }

    [Fact]
    public void ChangingTheOriginalAfterCopyingDoesNotMoveTheCopy()
    {
        // The assertion that makes this a test of a *deep* copy. Each of these reaches one of the
        // five children DeepCopy clones and reparents, so a child left shared shows up here as the
        // copy following the original.
        var original = ATableWithEveryChildSet();
        var copy = original.Clone();

        original.Borders.Width = 9;
        original.Shading.Color = Colors.Red;
        original.Format.Alignment = ParagraphAlignment.Right;
        original.Columns[0].Width = Unit.FromCentimeter(8);
        // ReSharper disable once PossibleNullReferenceException
        (original[0, 0].Elements[0] as Paragraph).Elements.Clear();

        copy.Borders.Width.Point.Should().BeApproximately(2, 0.001);
        copy.Shading.Color.Should().Be(Colors.LightGray);
        copy.Format.Alignment.Should().Be(ParagraphAlignment.Center);
        copy.Columns[0].Width.Centimeter.Should().BeApproximately(3, 0.001);
        TextOfCell(copy, 0, 0).Should().Be("left");
    }

    [Fact]
    public void ChangingTheCopyDoesNotMoveTheOriginalEither()
    {
        var original = ATableWithEveryChildSet();
        var copy = original.Clone();

        copy.Borders.Width = 9;
        copy.AddRow();

        original.Borders.Width.Point.Should().BeApproximately(2, 0.001);
        original.Rows.Count.Should().Be(1);
    }

    [Fact]
    public void ACopiedTableBelongsToWhateverDocumentItIsPutIn()
    {
        // The reparenting half, which is not directly observable - parent is protected internal - so
        // it is checked the way batch 5.2 checked Chart.DeepCopy's: put the copy in a document of
        // its own and write it. A child still pointing at the original's tree breaks on the way out.
        var copy = ATableWithEveryChildSet().Clone();

        var document = new Document();
        document.AddSection().Add(copy);

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("\\table");
        ddl.Should().Contain("left").And.Contain("right");
    }

    // ----- LineFormat.Serialize ---------------------------------------------------------------------

    [Fact]
    public void EveryAttributeALineFormatHoldsIsWrittenOut()
    {
        // All five arms at once. The frame carries a size of its own as well, because the DOM treats
        // an object with nothing set as null and skips it - a shape holding only a line format would
        // be dropped along with the section around it, which batch 4 records.
        var document = new Document();
        var frame = document.AddSection().AddTextFrame();
        frame.Width = Unit.FromCentimeter(5);
        frame.Height = Unit.FromCentimeter(2);

        frame.LineFormat.Visible = true;
        frame.LineFormat.Style = LineStyle.Single;
        frame.LineFormat.DashStyle = DashStyle.DashDot;
        frame.LineFormat.Width = Unit.FromPoint(3);
        frame.LineFormat.Color = Colors.Blue;

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("LineFormat");
        ddl.Should().Contain("Visible");
        ddl.Should().Contain("Style");
        ddl.Should().Contain("DashStyle");
        ddl.Should().Contain("Width");
        ddl.Should().Contain("Color");
    }

    [Fact]
    public void ALineFormatSurvivesBeingWrittenAndReadAgain()
    {
        var document = new Document();
        var frame = document.AddSection().AddTextFrame();
        frame.Width = Unit.FromCentimeter(5);
        frame.Height = Unit.FromCentimeter(2);
        frame.LineFormat.Visible = true;
        frame.LineFormat.Style = LineStyle.Single;
        frame.LineFormat.DashStyle = DashStyle.SquareDot;
        frame.LineFormat.Width = Unit.FromPoint(3);
        frame.LineFormat.Color = Colors.Blue;

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));
        // ReSharper disable once PossibleNullReferenceException
        var format = (reread.LastSection.Elements[0] as TextFrame).LineFormat;

        format.Visible.Should().BeTrue();
        format.Style.Should().Be(LineStyle.Single);
        format.DashStyle.Should().Be(DashStyle.SquareDot);
        format.Width.Point.Should().BeApproximately(3, 0.001);
        format.Color.Should().Be(Colors.Blue);
    }

    [Fact]
    public void ALineFormatThatStatesNothingWritesNoAttributes()
    {
        // The other side of the five guards: each attribute is written only when it has been set,
        // so an untouched line format contributes nothing rather than a row of defaults.
        var document = new Document();
        var frame = document.AddSection().AddTextFrame();
        frame.Width = Unit.FromCentimeter(5);
        frame.Height = Unit.FromCentimeter(2);

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().NotContain("DashStyle");
        ddl.Should().NotContain("LineFormat\n");
    }
}
