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
///   A chart's text areas from the object-model side: what each <c>Add</c> puts in one, and what
///   a writer says about one. <see cref="DdlChartAreaTests"/> covers the reading side.
/// </summary>
public class ChartTextAreaTests
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

    static Chart ChartIn(Document document) => document.AddSection().AddChart(ChartType.Line);

    static Chart RoundTrip(Document document) =>
        (Chart)DdlReader.DocumentFromString(DdlWriter.WriteToString(document)).LastSection.Elements[0];

    [Fact]
    public void EveryAddMethodOnATextAreaAddsWhatItReturns()
    {
        var area = new Chart(ChartType.Line).TopArea;

        object[] added =
        [
            area.AddParagraph(),
            area.AddParagraph("text"),
            area.AddTable(),
            area.AddImage(new UndrawnImage()),
            area.AddLegend(),
        ];

        area.Elements.Cast<object>().Should().Equal(added);
    }

    [Fact]
    public void EveryAddOverloadOnATextAreaAddsTheElementItIsGiven()
    {
        var area = new Chart(ChartType.Line).BottomArea;
        var paragraph = new Paragraph();
        var table = new Table();
        var image = new Image();
        var legend = new Chart(ChartType.Line).RightArea.AddLegend().Clone();

        area.Add(paragraph);
        area.Add(table);
        area.Add(image);
        area.Add(legend);

        area.Elements.Cast<object>().Should().Equal(paragraph, table, image, legend);
    }

    [Fact]
    public void ATextAreaKeepsWhatItIsGiven()
    {
        var area = new Chart(ChartType.Line).LeftArea;
        var format = new ParagraphFormat();
        var lineFormat = new LineFormat();
        var fillFormat = new FillFormat();
        var elements = new DocumentElements();

        area.Format = format;
        area.LineFormat = lineFormat;
        area.FillFormat = fillFormat;
        area.Elements = elements;

        area.Format.Should().BeSameAs(format);
        area.LineFormat.Should().BeSameAs(lineFormat);
        area.FillFormat.Should().BeSameAs(fillFormat);
        area.Elements.Should().BeSameAs(elements);
        area.Style.Should().BeEmpty();
    }

    [Fact]
    public void ACloneIsDeep()
    {
        var area = new Chart(ChartType.Line).HeaderArea;
        area.Width = 50;
        area.AddParagraph("title");

        var clone = area.Clone();

        clone.Width.Point.Should().Be(50);
        clone.Elements.Should().NotBeSameAs(area.Elements);
        clone.Elements.Count.Should().Be(1);
    }

    [Fact]
    public void EverythingATextAreaCanSayIsWrittenAndReadBack()
    {
        var document = new Document();
        var area = ChartIn(document).TopArea;
        area.Style = StyleNames.Heading3;
        area.Format.Alignment = ParagraphAlignment.Right;
        area.TopPadding = 1;
        area.LeftPadding = 2;
        area.RightPadding = 3;
        area.BottomPadding = 4;
        area.Width = 100;
        area.Height = 20;
        area.VerticalAlignment = VerticalAlignment.Center;
        area.LineFormat.Width = 0.5;
        area.FillFormat.Color = Colors.LightGray;
        area.AddParagraph("title");

        var again = RoundTrip(document).TopArea;

        again.Style.Should().Be(StyleNames.Heading3);
        again.Format.Alignment.Should().Be(ParagraphAlignment.Right);
        (again.TopPadding.Point, again.LeftPadding.Point, again.RightPadding.Point, again.BottomPadding.Point)
            .Should().Be((1.0, 2.0, 3.0, 4.0));
        again.Width.Point.Should().Be(100);
        again.Height.Point.Should().Be(20);
        again.VerticalAlignment.Should().Be(VerticalAlignment.Center);
        again.LineFormat.Width.Point.Should().Be(0.5);
        again.FillFormat.Color.Should().Be(Colors.LightGray);
        again.Elements.Count.Should().Be(1);
    }

    [Theory]
    [InlineData("headerarea")]
    [InlineData("footerarea")]
    [InlineData("toparea")]
    [InlineData("bottomarea")]
    [InlineData("leftarea")]
    [InlineData("rightarea")]
    public void EachAreaIsWrittenUnderItsOwnKeyword(string keyword)
    {
        var document = new Document();
        var chart = ChartIn(document);
        var area = keyword switch
        {
            "headerarea" => chart.HeaderArea,
            "footerarea" => chart.FooterArea,
            "toparea" => chart.TopArea,
            "bottomarea" => chart.BottomArea,
            "leftarea" => chart.LeftArea,
            _ => chart.RightArea,
        };
        area.AddParagraph("here");

        DdlWriter.WriteToString(document).Should().Contain("\\" + keyword);
    }
}
