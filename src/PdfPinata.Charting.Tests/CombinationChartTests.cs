using System;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A chart whose series do not all plot as the chart does, which is what picks the combination
///   renderer.
/// </summary>
/// <remarks>
///   The combination renderer used to take clustered columns and nothing else, and threw for a
///   series asking to be stacked. It now takes either kind, but not both at once: one set of
///   columns shares one slot per category, and that slot is either divided between the series or
///   stacked up in it.
/// </remarks>
public class CombinationChartTests
{
    [Fact]
    public void StackedColumnsInACombinationSitOnTopOfOneAnother()
    {
        var chart = StackedColumnsWithALine([1.0, 2.0], [3.0, 1.0], [0.5, 0.5]);

        var columns = PaintedRectangles.FilledOn(Drawn.Page(chart));

        columns.Should().HaveCount(4);

        // Each category has one slot the full width of the column, and the second series stands
        // on the first - the same geometry a chart of stacked columns alone draws.
        columns[2].X.Should().BeApproximately(columns[0].X, Tolerance);
        columns[2].Width.Should().BeApproximately(columns[0].Width, Tolerance);
        columns[2].Y.Should().BeApproximately(columns[0].Top, Tolerance);
        columns[2].Height.Should().BeApproximately(columns[0].Height * 3, Tolerance);
    }

    [Fact]
    public void TheValueAxisOfACombinationReachesTheTallestStack()
    {
        // No single value passes 3, but the first category stacks to 4.
        var chart = StackedColumnsWithALine([1.0, 2.0], [3.0, 1.0], [0.5, 0.5]);

        var top = AxisMaximum(chart);

        top.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void TheValueAxisOfACombinationStillReachesALineAboveTheStacks()
    {
        var chart = StackedColumnsWithALine([1.0, 2.0], [3.0, 1.0], [1.0, 9.0]);

        AxisMaximum(chart).Should().BeGreaterThanOrEqualTo(9);
    }

    [Fact]
    public void TheValueAxisOfACombinationReachesTheLowestNegativeStack()
    {
        var chart = StackedColumnsWithALine([-1.0, 2.0], [-3.0, 1.0], [0.5, 0.5]);

        var labels = ShownText.NumericOn(Drawn.Page(chart))
            .Select(label => double.Parse(label, CultureInfo.InvariantCulture));

        labels.Min().Should().BeLessThanOrEqualTo(-4);
    }

    [Fact]
    public void ClusteredColumnsInACombinationAreStillSideBySide()
    {
        var chart = Charts.OfSeries(ChartType.Line, [1.0, 2.0], [3.0, 1.0], [0.5, 0.5]);
        chart.SeriesCollection[0].ChartType = ChartType.Column2D;
        chart.SeriesCollection[1].ChartType = ChartType.Column2D;

        var columns = PaintedRectangles.FilledOn(Drawn.Page(chart));

        columns.Should().HaveCount(4);
        columns[2].X.Should().BeGreaterThan(columns[0].X, "a clustered column stands beside the other series'");
        AxisMaximum(chart).Should().BeLessThan(4, "nothing is stacked, so nothing is summed");
    }

    [Fact]
    public void MixingClusteredAndStackedColumnsIsRefused()
    {
        var chart = Charts.OfSeries(ChartType.Line, [1.0, 2.0], [3.0, 1.0], [0.5, 0.5]);
        chart.SeriesCollection[0].ChartType = ChartType.Column2D;
        chart.SeriesCollection[1].ChartType = ChartType.ColumnStacked2D;

        var draw = () => Drawn.Page(chart);

        draw.Should().Throw<InvalidOperationException>()
            .WithMessage("*Column2D*ColumnStacked2D*");
    }

    [Fact]
    public void ASeriesTheCombinationCannotDrawIsStillRefused()
    {
        var chart = Charts.OfSeries(ChartType.Line, [1.0, 2.0], [3.0, 1.0]);
        chart.SeriesCollection[0].ChartType = ChartType.BarStacked2D;

        var draw = () => Drawn.Page(chart);

        draw.Should().Throw<InvalidOperationException>().WithMessage("*BarStacked2D*");
    }

    /// <summary>
    ///   A line chart whose first two series are stacked columns, which is what makes it a
    ///   combination.
    /// </summary>
    private static Chart StackedColumnsWithALine(double[] first, double[] second, double[] line)
    {
        var chart = Charts.OfSeries(ChartType.Line, first, second, line);
        chart.SeriesCollection[0].ChartType = ChartType.ColumnStacked2D;
        chart.SeriesCollection[1].ChartType = ChartType.ColumnStacked2D;
        return chart;
    }

    /// <summary>The largest tick label on the value axis.</summary>
    private static double AxisMaximum(Chart chart) =>
        ShownText.NumericOn(Drawn.Page(chart))
            .Select(label => double.Parse(label, CultureInfo.InvariantCulture))
            .Max();

    private const double Tolerance = 0.01;
}
