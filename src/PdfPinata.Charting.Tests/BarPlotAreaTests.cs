using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The bar chart: the same picture as a column chart turned on its side.
/// </summary>
/// <remarks>
///   A bar chart is drawn by the column chart's own renderers - <c>ColumnPlotAreaRenderer</c> and
///   <c>ColumnLikeGridlinesRenderer</c> - given the orientation of a category axis running up the
///   chart rather than across it: a zero line forced when the data crosses zero, every bar filled
///   and then all outlined, and the category gridlines drawn across the plot area instead of up it.
///   They used to be separate copies, and were separately wrong when they were wrong. They are
///   covered here as a bar chart draws them, and <c>ColumnBarParityTests</c> holds them to the column
///   chart's picture turned on its side.
/// </remarks>
public class BarPlotAreaTests
{
    [Fact]
    public void ABarIsAsLongAsTheValueItPlots()
    {
        var bars = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Bar2D, 3.0, 6.0)));

        bars.Should().HaveCount(2);
        bars[1].Width.Should().BeApproximately(bars[0].Width * 2, Tolerance);
    }

    [Fact]
    public void EveryBarStartsAtTheSameEdge()
    {
        var bars = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0)));

        bars.Select(bar => bar.X).Distinct().Should().ContainSingle();
        bars.Select(bar => bar.Height).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void TheFirstCategoryIsTheBottomBar()
    {
        var bars = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0)));

        bars.Should().BeInAscendingOrder(bar => bar.Y);
    }

    [Fact]
    public void ClusteredSeriesShareACategorysBand()
    {
        var oneSeries = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Bar2D, 1.0, 5.0)));

        var page = Drawn.Page(Charts.OfSeries(ChartType.Bar2D,
            [1.0, 5.0], [2.0, 4.0]));
        var clustered = PaintedRectangles.FilledOn(page);

        clustered.Should().HaveCount(4);
        clustered[0].Height.Should().BeApproximately(oneSeries[0].Height / 2, Tolerance);
        clustered[2].Height.Should().BeApproximately(clustered[0].Height, Tolerance);
        clustered[2].Y.Should().BeApproximately(clustered[0].Top, Tolerance);
    }

    [Fact]
    public void StackedBarsContinueFromOneAnother()
    {
        var page = Drawn.Page(Charts.OfSeries(ChartType.BarStacked2D,
            [1.0, 2.0], [3.0, 1.0]));

        var bars = PaintedRectangles.FilledOn(page);

        bars.Should().HaveCount(4);

        // Full height each, as a stacked chart gives a category one band rather than dividing it,
        // and the second series begins where the first leaves off.
        bars[2].Height.Should().BeApproximately(bars[0].Height, Tolerance);
        bars[2].Y.Should().BeApproximately(bars[0].Y, Tolerance);
        bars[2].X.Should().BeApproximately(bars[0].Right, Tolerance);
        bars[2].Width.Should().BeApproximately(bars[0].Width * 3, Tolerance);
    }

    [Fact]
    public void BarBordersAreStrokedAfterEveryBarHasBeenFilled()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Bar2D, 3.0, 6.0));

        var rectangles = PaintedRectangles.On(page)
            .Where(rectangle => rectangle.Filled || rectangle.Stroked)
            .ToList();

        rectangles.Should().HaveCount(4);
        rectangles.Take(2).Should().OnlyContain(rectangle => rectangle.Filled);
        rectangles.Skip(2).Should().OnlyContain(rectangle => rectangle.Stroked && !rectangle.Filled);
    }

    [Fact]
    public void AValueOutsideTheGivenScaleIsNotDrawnAtAll()
    {
        var chart = Charts.Of(ChartType.Bar2D, 1.0, 40.0);
        chart.YAxis.MinimumScale = 0;
        chart.YAxis.MaximumScale = 5;
        chart.YAxis.MajorTick = 1;

        PaintedRectangles.FilledOn(Drawn.Page(chart)).Should().ContainSingle();
    }

    /// <summary>
    ///   The gridlines of a bar chart's category axis run across the plot area, one to a category
    ///   boundary, which is one more than there are categories.
    /// </summary>
    [Fact]
    public void CategoryGridlinesRunAcrossTheBarChart()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        chart.XAxis.HasMajorGridlines = true;
        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        var lines = StrokedLines.Of(Drawn.Page(chart));

        lines.Should().OnlyContain(line => line.IsHorizontal);
        lines.Should().HaveCount(4, "three categories have four boundaries between and around them");
    }

    [Fact]
    public void CategoryGridlinesAreEvenlySpaced()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        chart.XAxis.HasMajorGridlines = true;
        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        var heights = StrokedLines.Of(Drawn.Page(chart))
            .Select(line => line.Y1)
            .OrderBy(y => y)
            .ToList();

        var firstGap = heights[1] - heights[0];
        (heights[2] - heights[1]).Should().BeApproximately(firstGap, Tolerance);
        (heights[3] - heights[2]).Should().BeApproximately(firstGap, Tolerance);
    }

    /// <summary>
    ///   Four kinds of gridline, drawn by four blocks of the same shape: the category axis's
    ///   major and minor, running across, and the value axis's major and minor, running up. Asking
    ///   for all four is the only way to reach all four.
    /// </summary>
    [Fact]
    public void BothAxesDrawBothTheirGridlines()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;
        chart.XAxis.HasMajorGridlines = true;
        chart.XAxis.HasMinorGridlines = true;
        chart.YAxis.HasMajorGridlines = true;
        chart.YAxis.HasMinorGridlines = true;

        var lines = StrokedLines.Of(Drawn.Page(chart));

        // The category axis runs up the side, so its gridlines are the horizontal ones; the value
        // axis runs across, so its are vertical.
        lines.Should().Contain(line => line.IsHorizontal);
        lines.Should().Contain(line => line.IsVertical);
    }

    [Fact]
    public void MinorGridlinesAreDrawnBetweenTheMajorOnes()
    {
        var majorOnly = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        majorOnly.YAxis.MajorTickMark = TickMarkType.None;
        majorOnly.YAxis.MinorTickMark = TickMarkType.None;
        majorOnly.YAxis.HasMajorGridlines = true;

        var withMinor = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        withMinor.YAxis.MajorTickMark = TickMarkType.None;
        withMinor.YAxis.MinorTickMark = TickMarkType.None;
        withMinor.YAxis.HasMajorGridlines = true;
        withMinor.YAxis.HasMinorGridlines = true;

        StrokedLines.Of(Drawn.Page(withMinor)).Count
            .Should().BeGreaterThan(StrokedLines.Of(Drawn.Page(majorOnly)).Count);
    }

    /// <summary>
    ///   The bar chart's own copy of the forced zero line, which runs up the plot area where a
    ///   column chart's runs across it.
    /// </summary>
    [Fact]
    public void DataOnBothSidesOfZeroGetsAZeroLineOfItsOwn()
    {
        var straddling = Charts.Of(ChartType.Bar2D, -4.0, 6.0);
        straddling.YAxis.HasMajorGridlines = true;

        var oneSided = Charts.Of(ChartType.Bar2D, 4.0, 6.0);
        oneSided.YAxis.HasMajorGridlines = true;

        StrokedLines.Of(Drawn.Page(straddling)).Count
            .Should().Be(StrokedLines.Of(Drawn.Page(oneSided)).Count + 1);
    }

    /// <summary>
    ///   With only minor gridlines asked for, the forced zero line is drawn in their format
    ///   instead - the renderer prefers the minor format when it has one, which is the other side
    ///   of the same branch.
    /// </summary>
    [Fact]
    public void TheZeroLineIsDrawnEvenWhenOnlyMinorGridlinesWereAskedFor()
    {
        var straddling = Charts.Of(ChartType.Bar2D, -4.0, 6.0);
        straddling.YAxis.HasMinorGridlines = true;

        var oneSided = Charts.Of(ChartType.Bar2D, 4.0, 6.0);
        oneSided.YAxis.HasMinorGridlines = true;

        StrokedLines.Of(Drawn.Page(straddling)).Count
            .Should().Be(StrokedLines.Of(Drawn.Page(oneSided)).Count + 1);
    }

    [Fact]
    public void ABarBelowZeroReachesBackFromTheZeroLine()
    {
        var bars = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Bar2D, -4.0, 6.0, 2.0)));

        // The negative bar ends where the positive ones begin.
        bars[0].Right.Should().BeApproximately(bars[1].X, Tolerance);
        bars[2].X.Should().BeApproximately(bars[1].X, Tolerance);
    }

    [Fact]
    public void ABarChartWithNoGridlinesAsksForNoneOfThem()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0, 1.0);
        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        StrokedLines.Of(Drawn.Page(chart)).Should().BeEmpty();
    }

    private const double Tolerance = 0.01;
}
