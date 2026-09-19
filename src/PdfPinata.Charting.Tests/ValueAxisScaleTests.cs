using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   What scale the value axis comes out at, and therefore what its tick labels read.
/// </summary>
/// <remarks>
///   The scale is chosen by <c>YAxisRenderer.FineTuneYAxis</c>, which takes the smallest and
///   largest value plotted and answers with a minimum, a maximum and a major and minor tick. It is
///   the single most involved method in the charting package and none of its arithmetic is
///   reachable from outside: the renderer is internal and the numbers it produces live in an
///   internal renderer info.
///
///   They are legible on the page, though, because the tick labels are drawn from them - one per
///   major tick from the minimum to the maximum, formatted with the axis's format string. So the
///   labels a chart draws state the whole answer, and reading them back is how these tests ask.
///
///   The rules being pinned down here, in the order the method applies them:
///   an empty range is widened; a range whose ends differ by less than a fifth is dropped to zero,
///   which the method's own comment attributes to Excel; the step is the largest of 1, 0.5 and 0.2
///   scaled by a power of ten that fits the range; and the ends are rounded outwards by half a
///   step. None of it is documented anywhere else.
/// </remarks>
public class ValueAxisScaleTests
{
    [Theory]
    // A maximum a fifth again above the minimum takes the minimum down to zero, so a chart of
    // 1 to 5 is drawn from 0 rather than from 1.
    [InlineData(new[] { 1.0, 5.0, 3.0 }, "0.0 1.0 2.0 3.0 4.0 5.0 6.0")]
    // Every value the same and zero: there is no range at all, so one is invented.
    [InlineData(new[] { 0.0, 0.0 }, "0.0 0.1 0.2 0.3 0.4 0.5 0.6 0.7 0.8 0.9 1.0")]
    // Every value the same and positive: the top is lifted by one, and then the ratio rule takes
    // the bottom to zero, so four and four are plotted against a scale of nought to six.
    [InlineData(new[] { 4.0, 4.0 }, "0.0 1.0 2.0 3.0 4.0 5.0 6.0")]
    // Every value the same and negative: the top becomes zero and the axis runs up to it.
    [InlineData(new[] { -4.0, -4.0 }, "-4.5 -4.0 -3.5 -3.0 -2.5 -2.0 -1.5 -1.0 -0.5 0.0")]
    // Both ends negative and far enough apart: the ratio rule applies to the top instead, and
    // the axis is extended to zero rather than shortened to the data.
    [InlineData(new[] { -3.0, -8.0 }, "-9.0 -8.0 -7.0 -6.0 -5.0 -4.0 -3.0 -2.0 -1.0 0.0")]
    // Both ends negative and close together: the ratio rule does not fire, and the axis stays
    // around the data instead of running all the way to zero.
    [InlineData(new[] { -8.0, -9.0 }, "-9.2 -9.0 -8.8 -8.6 -8.4 -8.2 -8.0 -7.8 -7.6")]
    // A range spanning zero: neither end is moved, and the step widens to two.
    [InlineData(new[] { -4.0, 6.0, 2.0 }, "-6.0 -4.0 -2.0 0.0 2.0 4.0 6.0 8.0")]
    // A range under one: the step scales down with it, to 0.02 here - which the default format
    // of "0.0" then rounds, so three consecutive labels read 0.0 and two more read 0.1.
    [InlineData(new[] { 0.11, 0.14 }, "0.0 0.0 0.0 0.1 0.1 0.1 0.1 0.1 0.2")]
    // And a range in the thousands: the same three step widths, scaled up instead.
    [InlineData(new[] { 1200.0, 4000.0 }, "0.0 500.0 1000.0 1500.0 2000.0 2500.0 3000.0 3500.0 4000.0 4500.0")]
    // A narrow range high above zero: the ratio rule does not fire, so the axis does not start at
    // zero, and the narrowest of the three step widths is used.
    [InlineData(new[] { 10.0, 11.0 }, "9.6 9.8 10.0 10.2 10.4 10.6 10.8 11.0 11.2")]
    public void TheValueAxisIsScaledToTheDataItPlots(double[] values, string expected)
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, values));

        ShownText.NumericOn(page).Should().Equal(expected.Split(' '));
    }

    /// <summary>
    ///   A blank in a series takes no part in the scale.
    /// </summary>
    /// <remarks>
    ///   <see cref="Series.AddBlank"/> puts a null in the element collection, and the pass that
    ///   finds the smallest and largest value used to walk those elements testing each for NaN
    ///   without first testing it for null - so the one thing the method exists to permit was the
    ///   one thing the renderer could not survive.
    ///
    ///   A blank is now read as NaN, which is what a renderer already meant by a missing value,
    ///   so it falls out of the scale, out of the plot area and out of the data labels on its own.
    ///   That is <see cref="BlankType.NotPlotted"/>, which is what
    ///   <see cref="Chart.DisplayBlanksAs"/> defaults to - though nothing reads that property, so
    ///   the other two kinds are still unimplemented.
    /// </remarks>
    [Fact]
    public void ABlankInASeriesIsLeftOutOfTheScale()
    {
        var blank = Charts.Empty(ChartType.Column2D);
        blank.XValues.AddXSeries().Add("A", "B");
        var series = blank.SeriesCollection.AddSeries();
        series.Add(5.0);
        series.AddBlank();

        // Scaled as a series holding five and nothing else: one value, so the range is empty and
        // the top is lifted by one to make one, and then the ratio rule takes the bottom to zero.
        ShownText.NumericOn(Drawn.Page(blank)).Should().Equal(
            "0.0", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0", "7.0");

        // A zero in the same place is a different chart, which is the point: a blank is a value
        // that is not there rather than a value of nought. Here the range runs from nought to
        // five to begin with, so the top is rounded up to six rather than to seven.
        ShownText.NumericOn(Drawn.Page(Charts.Of(ChartType.Column2D, 5.0, 0.0))).Should().Equal(
            "0.0", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0");
    }

    [Fact]
    public void ASeriesOfNothingButBlanksIsGivenARangeToDrawAgainst()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B");
        var series = chart.SeriesCollection.AddSeries();
        series.AddBlank();
        series.AddBlank();

        // The case in FineTuneYAxis for no data at all: the smallest and largest are still the
        // sentinels they were seeded with, so a range of nought to nine tenths is invented. It is
        // the same range a series that is all zeroes is given, and until a blank stopped throwing
        // there was no way to reach it.
        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal(
            "0.0", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0");
    }

    [Fact]
    public void ABlankInASeriesIsNotDrawn()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B", "C");
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0);
        series.AddBlank();
        series.Add(3.0);
        chart.HasDataLabel = true;

        var page = Drawn.Page(chart);

        // Two columns for three categories, and a label on each column rather than a third
        // reading NaN.
        PaintedRectangles.FilledOn(page).Should().HaveCount(2);
        ShownText.On(page).Should().NotContain("NaN");
        ShownText.On(page).Take(2).Should().Equal("1", "3");
    }

    [Theory]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void EveryChartTypeSurvivesABlank(ChartType type)
    {
        var chart = Charts.Empty(type);
        chart.XValues.AddXSeries().Add("A", "B", "C");
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0);
        series.AddBlank();
        series.Add(3.0);
        chart.HasDataLabel = type is ChartType.Pie2D or ChartType.PieExploded2D;

        ShownText.On(Drawn.Page(chart)).Should().NotContain("NaN");
    }

    [Fact]
    public void AScaleGivenOnTheAxisIsUsedInsteadOfTheCalculatedOne()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MinimumScale = 0;
        chart.YAxis.MaximumScale = 10;
        chart.YAxis.MajorTick = 5;

        var page = Drawn.Page(chart);

        // Left to itself this data is scaled 0.0 to 3.5 in steps of half.
        ShownText.NumericOn(page).Should().Equal("0.0", "5.0", "10.0");
    }

    [Fact]
    public void AScaleGivenOnTheAxisStillDecidesHowTallTheColumnsAre()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MinimumScale = 0;
        chart.YAxis.MaximumScale = 10;
        chart.YAxis.MajorTick = 5;

        var columns = PaintedRectangles.FilledOn(Drawn.Page(chart));

        // Three times the value, three times the height - the scale moves both together.
        columns.Should().HaveCount(2);
        columns[1].Height.Should().BeApproximately(columns[0].Height * 3, Tolerance);
    }

    [Fact]
    public void ATickLabelFormatGivenOnTheAxisReplacesTheDefault()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.TickLabels.Format = "0.000";

        var page = Drawn.Page(chart);

        ShownText.NumericOn(page).Should().Equal(
            "0.000", "0.500", "1.000", "1.500", "2.000", "2.500", "3.000", "3.500");
    }

    /// <summary>
    ///   The scale of a bar chart's value axis is worked out by the same method, which is worth
    ///   saying because a bar chart's value axis is the horizontal one. The renderer that draws it
    ///   is not the renderer that draws a column chart's, but the arithmetic behind both is.
    /// </summary>
    [Fact]
    public void ABarChartsValueAxisIsScaledByTheSameRules()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Bar2D, 3.0, 6.0));

        ShownText.NumericOn(page).Should().Equal(
            "0.0", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0", "7.0");
    }

    /// <summary>
    ///   A chart with no value axis object of its own is scaled all the same, and every one of the
    ///   four numbers the method produces is calculated rather than read - which is the branch a
    ///   chart takes when it has an axis but has set nothing on it, too.
    /// </summary>
    /// <remarks>
    ///   Worth arranging by hand because of what it says about the other axis. The value axis
    ///   renderer has always worked out its scale before it asks whether the axis object exists,
    ///   with only the labels, title and gridlines inside that question - so a chart with no value
    ///   axis is drawn correctly and merely goes unlabelled. The category axis renderer used to
    ///   put the equivalent call inside the question instead, which is why a chart with no
    ///   category axis was not drawn at all but written to the page as NaN. One line apart, and
    ///   the two renderers disagreed about which side of it that line belonged on; they agree now.
    ///   See <c>ChartFrameTests.AChartWithNoXAxisIsStillDrawnAgainstItsData</c>.
    /// </remarks>
    [Fact]
    public void AChartWithNoValueAxisObjectIsScaledAllTheSameAndMerelyGoesUnlabelled()
    {
        var chart = new Chart(ChartType.Column2D);
        _ = chart.XAxis;
        chart.XValues.AddXSeries().Add("A", "B", "C");
        chart.SeriesCollection.AddSeries().Add(1.0, 5.0, 3.0);

        var page = Drawn.Page(chart);

        ShownText.NumericOn(page).Should().BeEmpty("there is no axis to hang tick labels on");

        // The columns are drawn against the scale the method worked out regardless, so they still
        // stand in proportion to the values they plot.
        var columns = PaintedRectangles.FilledOn(page);
        columns.Should().HaveCount(3);
        columns[1].Height.Should().BeApproximately(columns[0].Height * 5, Tolerance);
        columns[2].Height.Should().BeApproximately(columns[0].Height * 3, Tolerance);
    }

    /// <summary>
    ///   Given a minimum but no maximum, the axis keeps the one it was given and works the other
    ///   out - the four numbers are decided one at a time rather than all together.
    /// </summary>
    [Fact]
    public void AMinimumGivenWithoutAMaximumLeavesTheMaximumToBeCalculated()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.YAxis.MinimumScale = -2;

        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal(
            "-2.0", "-1.0", "0.0", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0");
    }

    [Fact]
    public void AMinorTickGivenWithoutAMajorOneLeavesTheMajorToBeCalculated()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.YAxis.MinorTick = 0.25;
        chart.YAxis.HasMinorGridlines = true;

        // The major tick is still the calculated one, so the labels are unchanged and only the
        // gridlines between them are closer together.
        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal(
            "0.0", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0");
    }

    private const double Tolerance = 0.01;
}
