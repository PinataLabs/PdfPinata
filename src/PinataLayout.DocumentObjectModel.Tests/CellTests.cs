using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   A table cell: what its <c>Add</c> methods put in it, how it finds the row, column and table it
///   sits in, and what a writer says about it.
/// </summary>
public class CellTests
{
    sealed class UndrawnImage : IImageSource
    {
        public int Width => 1;
        public int Height => 1;
        public string Name => "undrawn";
        public bool Transparent => false;
        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();
        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }

    static Table TwoByTwo(Section section)
    {
        var table = section.AddTable();
        table.AddColumn();
        table.AddColumn();
        table.AddRow();
        table.AddRow();
        return table;
    }

    [Fact]
    public void EveryAddMethodOnACellAddsWhatItReturns()
    {
        var cell = new Cell();

        object[] added =
        [
            cell.AddParagraph(),
            cell.AddParagraph("text"),
            cell.AddChart(ChartType.Pie2D),
            cell.AddChart(),
            cell.AddImage(new UndrawnImage()),
            cell.AddTextFrame(),
        ];

        cell.Elements.Cast<object>().Should().Equal(added);
    }

    [Fact]
    public void EveryAddOverloadOnACellAddsTheElementItIsGiven()
    {
        var cell = new Cell();
        var paragraph = new Paragraph();
        var chart = new Chart();
        var image = new Image();
        var frame = new TextFrame();

        cell.Add(paragraph);
        cell.Add(chart);
        cell.Add(image);
        cell.Add(frame);

        cell.Elements.Cast<object>().Should().Equal(paragraph, chart, image, frame);
    }

    [Fact]
    public void ACellKnowsItsTableRowAndColumn()
    {
        var table = TwoByTwo(new Document().AddSection());

        var cell = table[1, 1];

        cell.Table.Should().BeSameAs(table);
        cell.Row.Should().BeSameAs(table.Rows[1]);
        cell.Column.Should().BeSameAs(table.Columns[1]);
        table[0, 0].Column.Should().BeSameAs(table.Columns[0]);
    }

    [Fact]
    public void ACellInNoTableHasNoTable()
    {
        new Cell().Table.Should().BeNull();
    }

    [Fact]
    public void ACellKeepsWhatItIsGiven()
    {
        var cell = new Cell();
        var format = new ParagraphFormat();
        var borders = new Borders();
        var shading = new Shading();
        var elements = new DocumentElements();

        cell.Format = format;
        cell.Borders = borders;
        cell.Shading = shading;
        cell.Elements = elements;
        cell.Comment = "a comment";

        cell.Format.Should().BeSameAs(format);
        cell.Borders.Should().BeSameAs(borders);
        cell.Shading.Should().BeSameAs(shading);
        cell.Elements.Should().BeSameAs(elements);
        cell.Comment.Should().Be("a comment");
        new Cell().Comment.Should().BeEmpty();
        new Cell().Style.Should().BeEmpty();
    }

    [Fact]
    public void ACloneIsDeep()
    {
        var table = TwoByTwo(new Document().AddSection());
        var cell = table[0, 0];
        cell.MergeRight = 1;
        cell.AddParagraph("text");

        var clone = cell.Clone();

        clone.MergeRight.Should().Be(1);
        clone.Elements.Should().NotBeSameAs(cell.Elements);
        clone.Elements.Count.Should().Be(1);
    }

    [Fact]
    public void EverythingACellCanSayIsWrittenAndReadBack()
    {
        var document = new Document();
        var table = TwoByTwo(document.AddSection());
        var cell = table[0, 0];
        cell.Style = StyleNames.Heading4;
        cell.Format.Alignment = ParagraphAlignment.Center;
        cell.MergeDown = 1;
        cell.MergeRight = 1;
        cell.VerticalAlignment = VerticalAlignment.Bottom;
        cell.Borders.Width = 2;
        cell.Shading.Color = Colors.Yellow;
        cell.RoundedCorner = RoundedCorner.TopLeft;
        cell.AddParagraph("text");

        var again = ((Table)DdlReader.DocumentFromString(DdlWriter.WriteToString(document))
            .LastSection.Elements[0])[0, 0];

        again.Style.Should().Be(StyleNames.Heading4);
        again.Format.Alignment.Should().Be(ParagraphAlignment.Center);
        again.MergeDown.Should().Be(1);
        again.MergeRight.Should().Be(1);
        again.VerticalAlignment.Should().Be(VerticalAlignment.Bottom);
        again.Borders.Width.Point.Should().Be(2);
        again.Shading.Color.Should().Be(Colors.Yellow);
        again.RoundedCorner.Should().Be(RoundedCorner.TopLeft);
        again.Elements.Count.Should().Be(1);
    }
}
