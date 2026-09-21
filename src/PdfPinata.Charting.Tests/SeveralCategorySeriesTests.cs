using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A chart given more than one <see cref="XSeries"/> - empira/PDFsharp#286.
/// </summary>
/// <remarks>
///   <see cref="Chart.XValues"/> is a collection of category series, but the category axis has
///   one row of labels and one slot per category. It is labelled from the first series: that is
///   the one the horizontal axis already measured when reserving its room, the one the pie
///   legend already reads its entries from, and the one Excel takes an axis's categories from
///   when every series names categories of its own. The axis used to draw every series one after
///   another without going back to the start, so the second series' labels carried on past the
///   last category - off the right of a column chart and below the bottom of a bar chart.
/// </remarks>
public class SeveralCategorySeriesTests
{
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    public void OnlyTheFirstCategorySeriesLabelsTheAxis(ChartType type)
    {
        var page = Drawn.Page(WithCategorySeries(type, new[] { "A", "B", "C" }, new[] { "X", "Y", "Z" }));

        var shown = ShownText.On(page);

        shown.Should().Contain(new[] { "A", "B", "C" });
        shown.Should().NotContain(new[] { "X", "Y", "Z" },
            "the axis has one slot per category, and the first series has filled them");
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    public void ASecondCategorySeriesLeavesTheChartAsTheFirstAloneDrewIt(ChartType type)
    {
        var alone = ShownText.RunsOn(Drawn.Page(WithCategorySeries(type, new[] { "A", "B", "C" })));
        var withSecond = ShownText.RunsOn(Drawn.Page(WithCategorySeries(type,
            new[] { "A", "B", "C" }, new[] { "A much longer name", "Another", "And a third" })));

        // Compared run for run, positions included, and with the second series' labels far wider
        // than the first's: the vertical axis used to measure every series when reserving its
        // width, so a second series that was no longer drawn would still have pushed the plot
        // area, and every label on both axes, to the right.
        withSecond.Select(Describe).Should().Equal(alone.Select(Describe));
    }

    private static Chart WithCategorySeries(ChartType type, params string[][] categorySeries)
    {
        var chart = Charts.Empty(type);
        foreach (var categories in categorySeries)
        {
            var xs = chart.XValues.AddXSeries();
            foreach (var category in categories)
                xs.Add(category);
        }
        chart.SeriesCollection.AddSeries().Add(1.0, 5.0, 3.0);
        return chart;
    }

    private static string Describe(ShownText.Run run) => $"{run.Text}@{run.X:F2},{run.Y:F2}";
}
