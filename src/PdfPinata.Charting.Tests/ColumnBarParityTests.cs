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

    private static double LengthOfLast(ChartType type, params double[] values)
    {
        var chart = Charts.Of(type, values);
        chart.YAxis.MinimumScale = -10;
        chart.YAxis.MaximumScale = -1;
        chart.YAxis.MajorTick = 1;

        var last = PaintedRectangles.FilledOn(Drawn.Page(chart))[^1];
        return type == ChartType.Bar2D ? last.Width : last.Height;
    }

    private const double Tolerance = 0.01;
}
