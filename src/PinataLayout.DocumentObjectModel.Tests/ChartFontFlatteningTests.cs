using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   An axis title and its tick labels each have a style and a font of their own, and flattening
///   is what merges the two: the font keeps what it sets and takes the rest from the style it names
///   or, naming none, from the chart. Before it did, the renderer mapped the style and then the font
///   over it, and the font's unset bold, italic and colour overwrote the style's.
/// </summary>
public class ChartFontFlatteningTests
{
    static Chart AChart(out Document document)
    {
        document = new Document();
        var loud = document.Styles.AddStyle("Loud", "Normal");
        loud.Font.Bold = true;
        loud.Font.Italic = true;
        loud.Font.Color = Colors.Red;
        return document.AddSection().AddChart(ChartType.Column2D);
    }

    static void Flattened(Document document) => new PdfFlattenVisitor().Visit(document);

    [Fact]
    public void AnAxisTitleWithAStyleAndAFontOfItsOwnKeepsBoth()
    {
        var chart = AChart(out var document);
        var title = chart.YAxis.Title;
        title.Caption = "Revenue";
        title.Style = "Loud";
        title.Font.Size = 14;

        Flattened(document);

        title.Font.Size.Point.Should().BeApproximately(14, 1e-4, "the title's own size");
        title.Font.Bold.Should().BeTrue("the style's bold, which the title did not set");
        title.Font.Italic.Should().BeTrue("the style's italic, which the title did not set");
        title.Font.Color.Should().Be(Colors.Red, "the style's colour, which the title did not set");
    }

    [Fact]
    public void TickLabelsWithAStyleAndAFontOfTheirOwnKeepBoth()
    {
        var chart = AChart(out var document);
        var tickLabels = chart.XAxis.TickLabels;
        tickLabels.Style = "Loud";
        tickLabels.Font.Size = 7;

        Flattened(document);

        tickLabels.Font.Size.Point.Should().BeApproximately(7, 1e-4);
        tickLabels.Font.Bold.Should().BeTrue();
        tickLabels.Font.Color.Should().Be(Colors.Red);
    }

    [Fact]
    public void AnAxisTitleNamingNoStyleTakesTheChartsFont()
    {
        var chart = AChart(out var document);
        chart.Format.Font.Color = Colors.Blue;
        chart.Format.Font.Bold = true;
        var title = chart.XAxis.Title;
        title.Caption = "Quarter";
        title.Font.Italic = true;

        Flattened(document);

        title.Font.Color.Should().Be(Colors.Blue);
        title.Font.Bold.Should().BeTrue();
        title.Font.Italic.Should().BeTrue();
    }

    [Fact]
    public void WhatTheTitleSaysItselfWinsOverItsStyle()
    {
        var chart = AChart(out var document);
        var title = chart.YAxis.Title;
        title.Style = "Loud";
        title.Font.Bold = false;
        title.Font.Color = Colors.Green;

        Flattened(document);

        title.Font.Bold.Should().BeFalse("an explicit false is an answer, not a gap");
        title.Font.Color.Should().Be(Colors.Green);
        title.Font.Italic.Should().BeTrue("and what it left unset still comes from the style");
    }

    [Fact]
    public void AStyleThatDoesNotExistFallsBackToTheChart()
    {
        var chart = AChart(out var document);
        chart.Format.Font.Color = Colors.Blue;
        var title = chart.YAxis.Title;
        title.Style = "NoSuchStyle";
        title.Font.Size = 9;

        Flattened(document);

        title.Font.Color.Should().Be(Colors.Blue);
    }
}
