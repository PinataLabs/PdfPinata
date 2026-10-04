using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A value axis given the same minimum and maximum, which spans nothing.
/// </summary>
/// <remarks>
///   A range that comes from the data is widened when its two ends are equal, by
///   <c>YAxisRenderer.WidenEmptyOrFlatRange</c>. A range the caller gives used to skip that, so the
///   axis was scaled by dividing its length by a span of zero. The rule now is the same for both: a
///   scale that spans nothing is widened as a flat data range is - up to 0.9 from zero, up to zero
///   from below it, up by one from above it - and a tick the caller did not give is worked out from
///   the widened range rather than from the data.
/// </remarks>
public class EqualValueScaleTests
{
    public static TheoryData<ChartType, bool, double, double?> EveryValueAxisChart()
    {
        var data = new TheoryData<ChartType, bool, double, double?>();
        foreach (var (type, combination) in ValueAxisCharts)
            foreach (var scale in new[] { 0.0, 5.0, -5.0 })
                foreach (var majorTick in new double?[] { null, 1.0 })
                    data.Add(type, combination, scale, majorTick);
        return data;
    }

    /// <summary>
    ///   Every chart type with a value axis draws a scale of one value, with every part of the
    ///   axis and the plot area asked for - tick marks of both kinds, gridlines of both kinds and
    ///   data labels - and writes nothing that is not a number.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryValueAxisChart))]
    public void AScaleOfOneValueIsDrawnAndWritesNoNaN(ChartType type, bool combination, double scale, double? majorTick)
    {
        var chart = EqualScale(type, combination, scale, majorTick);

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        ShownText.On(page).Should().NotContain("NaN");
    }

    [Theory]
    // The three cases of the flat data range, with no tick given: the tick is worked out from the
    // range the scale is widened to.
    [InlineData(0.0, null, "0.0 0.1 0.2 0.3 0.4 0.5 0.6 0.7 0.8 0.9")]
    [InlineData(5.0, null, "5.0 5.2 5.4 5.6 5.8 6.0")]
    [InlineData(-5.0, null, "-5.0 -4.0 -3.0 -2.0 -1.0 0.0")]
    // A tick the caller gave is kept, whatever the range is widened to.
    [InlineData(5.0, 0.5, "5.0 5.5 6.0")]
    [InlineData(-5.0, 2.5, "-5.0 -2.5 0.0")]
    public void AScaleOfOneValueIsWidenedAsAFlatRangeOfDataIs(double scale, double? majorTick, string expected)
    {
        foreach (var (type, combination) in ValueAxisCharts)
        {
            var chart = Charts.Of(type, 1.0, 3.0);
            if (combination)
            {
                chart.SeriesCollection.AddSeries().Add(2.0, 1.0);
                chart.SeriesCollection[0].ChartType = ChartType.ColumnStacked2D;
            }
            chart.YAxis.MinimumScale = scale;
            chart.YAxis.MaximumScale = scale;
            if (majorTick is { } tick)
                chart.YAxis.MajorTick = tick;

            ShownText.NumericOn(Drawn.Page(chart)).Should().Equal(expected.Split(' '), $"a {type} chart{(combination ? " with stacked columns" : "")} is scaled so");
        }
    }

    /// <summary>
    ///   A given minimum that the calculated maximum happens to equal is the same flat scale, and
    ///   is widened the same way: the data here is scaled 0.0 to 6.0, so a minimum of six leaves
    ///   nothing between the two.
    /// </summary>
    [Fact]
    public void AGivenMinimumEqualToTheCalculatedMaximumIsWidenedToo()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.YAxis.MinimumScale = 6;

        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal("6.0", "6.2", "6.4", "6.6", "6.8", "7.0");
    }

    /// <summary>
    ///   A calculated end is a whole number of steps worked out in floating point, so the maximum
    ///   this data is scaled to, labelled 0.6, is six steps of 0.1 - which is 0.6000000000000001.
    ///   A minimum given as the 0.6 the label reads is the same value, and is widened as one.
    /// </summary>
    [Fact]
    public void AGivenEndEqualToACalculatedOneButForRoundingIsWidenedToo()
    {
        var chart = Charts.Of(ChartType.Column2D, 0.1, 0.5);
        chart.YAxis.MinimumScale = 0.6;

        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal("0.6", "0.8", "1.0", "1.2", "1.4", "1.6");
    }

    /// <summary>
    ///   A narrow scale far from zero is not flat, and is drawn as given: allowing for rounding
    ///   must not swallow a span a double holds without trouble.
    /// </summary>
    [Fact]
    public void ANarrowScaleFarFromZeroIsNotWidened()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MinimumScale = 1e12;
        chart.YAxis.MaximumScale = 1e12 + 100;
        chart.YAxis.MajorTick = 50;
        chart.YAxis.TickLabels.Format = "0";

        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal("1000000000000", "1000000000050", "1000000000100");
    }

    /// <summary>
    ///   Past 2^53 a double cannot hold a value one above another, so widening by one changes
    ///   nothing and the scale would still span zero. There the end is moved by a tenth of the
    ///   value instead, which a double can always tell apart, so the axis is still drawn.
    /// </summary>
    [Theory]
    [InlineData(Huge, null)]
    [InlineData(Huge, 1.0)]
    // Below zero the top goes to zero, which is always distinct: the rule that needs no tenth.
    [InlineData(-Huge, null)]
    public void AScaleOfOneHugeValueIsStillWidened(double scale, double? majorTick)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MinimumScale = scale;
        chart.YAxis.MaximumScale = scale;
        if (majorTick is { } tick)
            chart.YAxis.MajorTick = tick;

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        if (majorTick == null)
            ShownText.NumericOn(page).Distinct().Should().HaveCountGreaterThan(1, "the widened scale is labelled");
    }

    /// <summary>
    ///   The same for data all of one huge value, which reaches the widening through the
    ///   calculated range rather than through a given one.
    /// </summary>
    [Theory]
    [InlineData(Huge)]
    [InlineData(-Huge)]
    public void DataOfOneHugeValueIsStillGivenARange(double value)
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, value, value));

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        ShownText.NumericOn(page).Distinct().Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    ///   And a given maximum far below zero that the calculated minimum meets, which widens
    ///   downwards: data at -2^54 is scaled from -2e16, and one below that is the same double.
    /// </summary>
    [Fact]
    public void AHugeGivenMaximumMeetingTheCalculatedMinimumIsWidenedDownwards()
    {
        var chart = Charts.Of(ChartType.Column2D, -Huge, -Huge);
        chart.YAxis.MaximumScale = -2e16;

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        var labels = ShownText.NumericOn(page).Select(label => double.Parse(label, CultureInfo.InvariantCulture)).ToList();
        labels.Distinct().Should().HaveCountGreaterThan(1);
        labels.Max().Should().Be(-2e16, "the maximum given is not the end moved");
    }

    /// <summary>
    ///   Rounding is allowed for only where a calculated end meets a given one. Two ends the caller
    ///   gave are compared exactly, so a scale narrower than that allowance but given as such is
    ///   kept: two ticks across it, not the two thousand a scale widened by one would have.
    /// </summary>
    [Fact]
    public void TwoGivenEndsCloserThanRoundingAreKept()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MinimumScale = 1e12;
        chart.YAxis.MaximumScale = 1e12 + Math.Pow(2, -10);
        chart.YAxis.MajorTick = Math.Pow(2, -11);

        ShownText.NumericOn(Drawn.Page(chart)).Should().HaveCount(3);
    }

    /// <summary>
    ///   And a given minimum above a given maximum by no more than rounding is still a scale turned
    ///   upside down, which draws nothing in the plot area, rather than one widened into a scale
    ///   that can be drawn against.
    /// </summary>
    [Fact]
    public void AGivenMinimumAHairAboveAGivenMaximumStillDrawsNothing()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.5);
        chart.YAxis.MaximumScale = 5;
        chart.YAxis.MinimumScale = Math.BitIncrement(5.0);

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        PaintedRectangles.FilledOn(page).Should().BeEmpty();
    }

    /// <summary>2^54, past which a double has no room for a value one above another.</summary>
    private const double Huge = 18014398509481984;

    /// <summary>
    ///   The top of a long scale keeps its label. The step is a fifth here, and it used to be
    ///   worked out in single precision, a little over a fifth - an error that a hundred steps
    ///   added up into the top label going missing.
    /// </summary>
    [Fact]
    public void TheTopOfALongScaleKeepsItsLabel()
    {
        var chart = Charts.Of(ChartType.Column2D, 0.0, 1.0);
        chart.YAxis.MaximumScale = 20;

        var labels = ShownText.NumericOn(Drawn.Page(chart));

        labels.Should().HaveCount(101);
        labels[^1].Should().Be("20.0");
    }

    /// <summary>
    ///   A given maximum that the calculated minimum happens to equal is widened the other way: the
    ///   value the caller gave is never the one moved, so the bottom is lowered by one rather than
    ///   the top raised to zero. The data here is scaled from -3.5, so a maximum of -3.5 leaves
    ///   nothing between the two.
    /// </summary>
    [Fact]
    public void AGivenMaximumEqualToTheCalculatedMinimumIsWidenedDownwards()
    {
        var chart = Charts.Of(ChartType.Column2D, -3.0, -1.0);
        chart.YAxis.MaximumScale = -3.5;

        ShownText.NumericOn(Drawn.Page(chart)).Should().Equal("-4.5", "-4.3", "-4.1", "-3.9", "-3.7", "-3.5");
    }

    /// <summary>
    ///   Every label on a widened scale has a major gridline, and the minor gridlines fall between
    ///   them and not on either end. The top label used to go without its gridline, which was
    ///   stepped towards the maximum and compared with it exactly, and a single-precision step
    ///   overshoots it.
    /// </summary>
    [Fact]
    public void EveryTickLabelOnAWidenedScaleHasAGridline()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.5);
        chart.YAxis.MinimumScale = 5;
        chart.YAxis.MaximumScale = 5;
        Grid(chart.YAxis.MajorGridlines, XColors.Orange);
        Grid(chart.YAxis.MinorGridlines, XColors.Purple);

        var lines = StrokedLines.Of(Drawn.Page(chart));

        // Six labels, 5.0 to 6.0 in steps of 0.2, so six major gridlines. The minor tick is a fifth
        // of that, which is twenty-five steps across the scale and twenty-four gridlines inside it,
        // the four under the inner major ones included, as they always were.
        lines.Count(line => line.Colour == PaintedRectangles.ColourOf(XColors.Orange)).Should().Be(6);
        lines.Count(line => line.Colour == PaintedRectangles.ColourOf(XColors.Purple)).Should().Be(24);
    }

    /// <summary>
    ///   A major tick of zero is no tick at all: the axis draws no labels and no gridlines, where
    ///   every loop over the ticks used to step by nothing towards the maximum for ever.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task AMajorTickOfZeroDrawsNoTicksRatherThanNeverFinishing()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.MajorTick = 0;
        chart.YAxis.HasMajorGridlines = true;

        var page = await Task.Run(() => Drawn.Page(chart));

        ShownText.NumericOn(page).Should().BeEmpty();
    }

    /// <summary>
    ///   Widened, the scale is one that can be plotted against: a column whose value lies on it is
    ///   drawn, and the ones below it are left undrawn, as any value off the scale is.
    /// </summary>
    [Fact]
    public void AColumnOnTheWidenedScaleIsDrawn()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.5);
        chart.YAxis.MinimumScale = 5;
        chart.YAxis.MaximumScale = 5;

        PaintedRectangles.FilledOn(Drawn.Page(chart)).Should().ContainSingle();
    }

    private static void Grid(Gridlines gridlines, XColor colour)
    {
        gridlines.LineFormat.Visible = true;
        gridlines.LineFormat.Width = 0.25;
        gridlines.LineFormat.Color = colour;
    }

    private static Chart EqualScale(ChartType type, bool combination, double scale, double? majorTick)
    {
        var chart = Charts.OfSeries(type, [1.0, -2.0, 3.0], [2.0, 1.0, -1.0]);
        if (combination)
        {
            // A line chart whose first two series are stacked columns, which is what makes it a
            // combination.
            chart.SeriesCollection.AddSeries().Add(1.5, 0.5, 2.5);
            chart.SeriesCollection[0].ChartType = ChartType.ColumnStacked2D;
            chart.SeriesCollection[1].ChartType = ChartType.ColumnStacked2D;
        }

        chart.YAxis.MinimumScale = scale;
        chart.YAxis.MaximumScale = scale;
        if (majorTick is { } tick)
            chart.YAxis.MajorTick = tick;

        chart.YAxis.MajorTickMark = TickMarkType.Outside;
        chart.YAxis.MinorTickMark = TickMarkType.Outside;
        chart.YAxis.HasMajorGridlines = true;
        chart.YAxis.HasMinorGridlines = true;
        chart.YAxis.LineFormat.Visible = true;
        chart.XAxis.HasMajorGridlines = true;
        chart.HasDataLabel = true;
        return chart;
    }

    /// <summary>
    ///   Every chart type with a value axis, and a combination - a line chart some of whose series
    ///   are columns - as the last.
    /// </summary>
    private static readonly (ChartType Type, bool Combination)[] ValueAxisCharts =
    [
        (ChartType.Column2D, false), (ChartType.ColumnStacked2D, false), (ChartType.Bar2D, false),
        (ChartType.BarStacked2D, false), (ChartType.Line, false), (ChartType.Area2D, false),
        (ChartType.Line, true)
    ];
}
