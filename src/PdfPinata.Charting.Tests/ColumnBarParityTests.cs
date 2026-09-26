using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A column chart and a bar chart plot the same data turned on its side, and must agree on which
///   of it they draw and from where.
/// </summary>
/// <remarks>
///   The column and bar plot area renderers are separate copies of one another -
///   <c>ColumnClusteredPlotAreaRenderer</c> and <c>BarClusteredPlotAreaRenderer</c>,
///   <c>ColumnStackedPlotAreaRenderer</c> and <c>BarStackedPlotAreaRenderer</c> - and each pair had
///   drifted apart: the stacked pair on which segments lie outside the scale, the clustered pair on
///   where a bar starts after one that did. Every test here draws the same data both ways and
///   holds them to the same answer, so a change reaching one twin and not the other fails.
/// </remarks>
public class ColumnBarParityTests
{
    /// <summary>
    ///   A stacked segment is drawn only when the whole of it lies on the scale: from where it
    ///   starts on its pile to where the pile has reached with it. That is the clustered charts'
    ///   rule - a value off the scale is left undrawn rather than clipped - taken to the stretch a
    ///   stacked segment actually covers rather than to its own value, which on a stacked chart is
    ///   a length and not a position. A blank has no segment, and is skipped.
    /// </summary>
    [Theory]
    // The third segment's top, at 6, is past the end of the scale at 5.
    [InlineData(new[] { 0, 1 }, 0, 5, new[] { 2.0, 2.0, 2.0 })]
    // The first segment starts at zero, below a scale starting at 2; the second, 3 to 4, is on it.
    [InlineData(new[] { 1 }, 2, 10, new[] { 3.0, 1.0 })]
    // The negative pile reaches -2, below a scale starting at -1.
    [InlineData(new[] { 0 }, -1, 5, new[] { 3.0, -2.0 })]
    // A blank is not a segment, and the one above it stacks as if it were not there.
    [InlineData(new[] { 0, 2 }, 0, 5, new[] { 2.0, double.NaN, 2.0 })]
    [InlineData(new[] { 0, 1, 2 }, -5, 5, new[] { 2.0, -2.0, 2.0 })]
    // A scale ending at the total: 0.1 + 0.2 sums to a hair over 0.3, and is still on it.
    [InlineData(new[] { 0, 1 }, 0, 0.3, new[] { 0.1, 0.2 })]
    [InlineData(new[] { 0, 1 }, -0.3, 0, new[] { -0.1, -0.2 })]
    public void StackedColumnsAndBarsDrawTheSameSegments(int[] drawn, double minimum, double maximum, double[] stack)
    {
        var expected = drawn.Select(series => Colours[series]);

        StackedSegmentColours(ChartType.ColumnStacked2D, minimum, maximum, stack).Should().Equal(expected);
        StackedSegmentColours(ChartType.BarStacked2D, minimum, maximum, stack).Should().Equal(expected);
    }

    /// <summary>
    ///   A column or bar off the scale changes nothing about the next one. On a scale wholly below
    ///   zero there is no zero line to draw from, so each starts at the scale's minimum - and a bar
    ///   used to start instead wherever the bar before it had ended, because the start was carried
    ///   from one bar to the next and a negative value is swapped into it.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AValueOffTheScaleLeavesTheNextOneWhereItWouldHaveBeen(ChartType type)
    {
        var afterOneOnTheScale = LengthOfLast(type, -2.0, -5.0);
        var afterOneOffTheScale = LengthOfLast(type, -12.0, -5.0);

        afterOneOffTheScale.Should().BeApproximately(afterOneOnTheScale, Tolerance,
            "the bar for -5 runs from the scale's minimum at -10 whatever came before it");
    }

    /// <summary>
    ///   A segment left out for lying off the scale takes its data label with it, or the number
    ///   would be written with nothing under it - outside the plot area, over the axis or the
    ///   legend. The third segment here reaches 5.75 on a scale ending at 5.
    /// </summary>
    [Theory]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.BarStacked2D)]
    public void AStackedSegmentLeftOffTheScaleIsNotLabelled(ChartType type)
    {
        var chart = Charts.OfSeries(type, [1.25], [1.5], [3.0]);
        Labelled(chart, 0, 5);

        ShownText.On(Drawn.Page(chart)).Should()
            .Contain(Label(1.25)).And.Contain(Label(1.5)).And.NotContain(Label(3.0));
    }

    /// <summary>
    ///   The same of a clustered column or bar whose value is off the scale: it is not drawn, and
    ///   neither is its label, while the label of the one on the scale is.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AClusteredValueOffTheScaleIsNotLabelled(ChartType type)
    {
        var chart = Charts.Of(type, 1.25, 40.0);
        Labelled(chart, 0, 5);

        ShownText.On(Drawn.Page(chart)).Should()
            .Contain(Label(1.25)).And.NotContain(Label(40.0));
    }

    /// <summary>
    ///   Labels every point with its value, to two places so that no label can be mistaken for a
    ///   tick label of a scale in whole numbers.
    /// </summary>
    private static void Labelled(Chart chart, double minimum, double maximum)
    {
        chart.HasDataLabel = true;
        chart.DataLabel.Type = DataLabelType.Value;
        chart.DataLabel.Format = LabelFormat;
        chart.YAxis.MinimumScale = minimum;
        chart.YAxis.MaximumScale = maximum;
        chart.YAxis.MajorTick = 1;
    }

    private static string Label(double value) => value.ToString(LabelFormat);

    private const string LabelFormat = "0.00";

    private static double LengthOfLast(ChartType type, params double[] values)
    {
        var chart = Charts.Of(type, values);
        chart.YAxis.MinimumScale = -10;
        chart.YAxis.MaximumScale = -1;
        chart.YAxis.MajorTick = 1;

        var last = PaintedRectangles.FilledOn(Drawn.Page(chart))[^1];
        return type == ChartType.Bar2D ? last.Width : last.Height;
    }

    /// <summary>
    ///   The colours of the segments a stacked chart draws at its one category, in the order it
    ///   draws them: a series each, one point each, each series filled in a colour of its own.
    /// </summary>
    private static string[] StackedSegmentColours(ChartType type, double minimum, double maximum, double[] stack)
    {
        var chart = Charts.OfSeries(type, [.. stack.Select(value => new[] { value })]);
        for (var idx = 0; idx < stack.Length; idx++)
            chart.SeriesCollection[idx].FillFormat.Color = SeriesColours[idx];

        chart.YAxis.MinimumScale = minimum;
        chart.YAxis.MaximumScale = maximum;
        chart.YAxis.MajorTick = (maximum - minimum) / 5;

        return [.. PaintedRectangles.FilledOn(Drawn.Page(chart)).Select(segment => segment.Colour)];
    }

    private static readonly XColor[] SeriesColours = [XColors.Red, XColors.Lime, XColors.Blue];

    private static readonly string[] Colours = [.. SeriesColours.Select(PaintedRectangles.ColourOf)];

    private const double Tolerance = 0.01;
}
