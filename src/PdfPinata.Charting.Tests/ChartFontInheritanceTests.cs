using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The text a chart draws takes what its own font leaves unset from its chart.
/// </summary>
/// <remarks>
///   An axis title, the tick labels, the legend and the data labels each have a font of their own,
///   and each resolves what it does not set through <c>Font.ParentFont</c>: the nearest ancestor
///   with a font, which is the chart - except for a series' data label, which asks the chart's
///   data label first. Before, name and size came from the chart, bold and italic were added to
///   the chart's so that an explicit false did nothing, and colour did not come at all: every
///   renderer drew in black whatever the chart's font said.
/// </remarks>
public class ChartFontInheritanceTests
{
    private static readonly string Blue = PaintedRectangles.ColourOf(XColors.Blue);
    private static readonly string Red = PaintedRectangles.ColourOf(XColors.Red);

    private static Chart ALabelledChart()
    {
        var chart = Charts.Of(ChartType.Column2D, 10, 20, 30);
        chart.SeriesCollection[0].Name = "Sales";
        chart.XAxis.Title.Caption = "Quarter";
        chart.HasDataLabel = true;
        chart.Legend.Docking = DockingType.Bottom;
        return chart;
    }

    [Fact]
    public void TheChartsColourColoursEveryPieceOfTextItDraws()
    {
        var chart = ALabelledChart();
        chart.Font.Color = XColors.Blue;

        var runs = ShownText.RunsOn(Drawn.Page(chart));

        runs.Single(run => run.Text == "Quarter").Colour.Should().Be(Blue, "the axis title");
        runs.Single(run => run.Text == "Sales").Colour.Should().Be(Blue, "the legend");
        runs.Single(run => run.Text == "30").Colour.Should().Be(Blue, "a data label");
        runs.Single(run => run.Text == "30.0").Colour.Should().Be(Blue, "a tick label");
    }

    [Fact]
    public void TextOnAChartThatSaysNoColourIsStillBlack()
    {
        var runs = ShownText.RunsOn(Drawn.Page(ALabelledChart()));

        runs.Should().OnlyContain(run => run.Colour == PaintedRectangles.Grey(0));
    }

    [Fact]
    public void AColourOfItsOwnWinsOverTheCharts()
    {
        var chart = ALabelledChart();
        chart.Font.Color = XColors.Blue;
        chart.XAxis.Title.Font.Color = XColors.Red;

        var runs = ShownText.RunsOn(Drawn.Page(chart));

        runs.Single(run => run.Text == "Quarter").Colour.Should().Be(Red);
        runs.Single(run => run.Text == "Sales").Colour.Should().Be(Blue);
    }

    [Fact]
    public void ABoldChartMakesItsTitleBold()
    {
        var chart = ALabelledChart();
        chart.Font.Bold = true;

        var title = ShownText.RunsOn(Drawn.Page(chart)).Single(run => run.Text == "Quarter");

        title.Face.Should().Contain("Bold");
    }

    [Fact]
    public void ATitleSayingNotBoldIsDrawnRegularUnderABoldChart()
    {
        var chart = ALabelledChart();
        chart.Font.Bold = true;
        chart.XAxis.Title.Font.Bold = false;

        var runs = ShownText.RunsOn(Drawn.Page(chart));

        runs.Single(run => run.Text == "Quarter").Face.Should().NotContain("Bold",
            "an explicit false is an answer, where it used to be added to the chart's bold and lost");
        runs.Single(run => run.Text == "Sales").Face.Should().Contain("Bold", "the legend said nothing");
    }

    [Fact]
    public void ATitleSayingNotItalicIsDrawnUprightUnderAnItalicChart()
    {
        var chart = ALabelledChart();
        chart.Font.Italic = true;
        chart.XAxis.Title.Font.Italic = false;

        var runs = ShownText.RunsOn(Drawn.Page(chart));

        runs.Single(run => run.Text == "Quarter").Face.Should().NotContain("Italic");
        runs.Single(run => run.Text == "Sales").Face.Should().Contain("Italic");
    }

    [Fact]
    public void ASeriesDataLabelTakesTheChartDataLabelsFontForWhatItDoesNotSet()
    {
        var chart = Charts.Of(ChartType.Column2D, 10, 20, 30);
        chart.DataLabel.Font.Size = 15;
        chart.DataLabel.Font.Color = XColors.Red;
        chart.SeriesCollection[0].DataLabel.Format = "0.00";

        var labels = ShownText.RunsOn(Drawn.Page(chart)).Where(run => run.Text.EndsWith(".00")).ToList();

        labels.Should().HaveCount(3);
        labels.Should().OnlyContain(run => run.Size == 15 && run.Colour == Red);
    }

    [Fact]
    public void ASeriesDataLabelTakesTheChartDataLabelsPositionWhenItSetsNone()
    {
        // The chart's data label puts them at the foot of each column; the series' says only how
        // to write the number. It used to replace the chart's outright, and so drew them outside
        // the end - its own default for a position nobody set.
        var chart = Charts.Of(ChartType.Column2D, 10, 20, 30);
        chart.DataLabel.Position = DataLabelPosition.InsideBase;
        chart.SeriesCollection[0].DataLabel.Format = "0.00";

        var page = Drawn.Page(chart);
        var labels = ShownText.RunsOn(page).Where(run => run.Text.EndsWith(".00")).ToList();
        var columns = PaintedRectangles.FilledOn(page);

        labels.Should().HaveCount(columns.Count);
        for (var idx = 0; idx < columns.Count; idx++)
            labels[idx].Y.Should().BeLessThan(columns[idx].Y + columns[idx].Height / 2, "at the foot of the column");
    }

    [Fact]
    public void ASeriesDataLabelTakesTheChartDataLabelsTypeWhenItSetsNone()
    {
        // A pie labels its wedges with percentages unless told otherwise.
        var chart = Charts.Of(ChartType.Pie2D, 1, 3);
        chart.DataLabel.Type = DataLabelType.Value;
        chart.SeriesCollection[0].DataLabel.Format = "0.00";

        ShownText.On(Drawn.Page(chart)).Should().Contain(["1.00", "3.00"]);
    }

    [Fact]
    public void TheGettersStillAnswerOnlyWhatWasSetOnTheFontItself()
    {
        var chart = ALabelledChart();
        chart.Font.Color = XColors.Blue;
        chart.Font.Bold = true;

        Drawn.Page(chart);

        chart.XAxis.Title.Font.Color.IsEmpty.Should().BeTrue();
        chart.XAxis.Title.Font.Bold.Should().BeFalse();
    }
}
