using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
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
