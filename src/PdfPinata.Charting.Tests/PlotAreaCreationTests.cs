using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   That a chart is drawn whether or not anything asked it for a plot area, and that a plot area
///   somebody did configure is the one the renderers draw from.
/// </summary>
/// <remarks>
///   <see cref="Chart.PlotArea"/> creates the plot area the first time it is read, so a chart that
///   was never asked has none. <c>PlotAreaRenderer.Init</c> read the field instead, and worked only
///   because each of the six chart renderers' <c>Init</c> read the property first, on a line that
///   assigned the result to nothing and so read as dead code. Deleting that line - which is what an
///   unused-value warning asks for - took every chart with it, as a
///   <see cref="System.NullReferenceException"/> from <c>InitLineFormat</c>.
///
///   The renderer reads the property now and the six lines are gone, so the invariant is pinned
///   here rather than kept alive by six files that look as though they could lose it.
/// </remarks>
public class PlotAreaCreationTests
{
    /// <summary>
    ///   One chart of every shape the factory can pick, none of them asked for a plot area.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void AChartWhosePlotAreaWasNeverAskedForIsStillDrawn(ChartType type)
    {
        var chart = Charts.Of(type, 1.0, 5.0, 3.0);

        var draw = () => Drawn.Page(chart);

        draw.Should().NotThrow();
    }

    /// <summary>
    ///   A series plotting as something other than the chart does is what picks the combination
    ///   renderer, which has an <c>Init</c> of its own and so needed the same line.
    /// </summary>
    [Fact]
    public void ACombinationChartWhosePlotAreaWasNeverAskedForIsStillDrawn()
    {
        var chart = Charts.OfSeries(ChartType.Column2D, [1.0, 5.0, 3.0], [2.0, 4.0, 1.0]);
        chart.SeriesCollection[1].ChartType = ChartType.Line;

        var draw = () => Drawn.Page(chart);

        draw.Should().NotThrow();
    }

    /// <summary>
    ///   Reading the property rather than the field must still answer the plot area the caller
    ///   configured, not a fresh one - so the background it was given is the background drawn.
    /// </summary>
    [Fact]
    public void ThePlotAreaTheCallerConfiguredIsTheOneDrawn()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.PlotArea.FillFormat.Color = XColors.LightGray;

        var page = Drawn.Page(chart);

        PaintedRectangles.FilledOn(page)
            .Should().Contain(rectangle => rectangle.Colour == PaintedRectangles.ColourOf(XColors.LightGray));
    }

    /// <summary>
    ///   And the border it was given is the border drawn. <c>PlotAreaBorderRenderer</c> strokes it
    ///   rather than filling it, so it is the stroked rectangle on the page.
    /// </summary>
    [Fact]
    public void ThePlotAreaBorderTheCallerConfiguredIsTheOneDrawn()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.PlotArea.LineFormat.Visible = true;
        chart.PlotArea.LineFormat.Color = XColors.Red;
        chart.PlotArea.LineFormat.Width = 2;

        var page = Drawn.Page(chart);

        PaintedRectangles.On(page)
            .Should().Contain(rectangle =>
                rectangle.Stroked && rectangle.Colour == PaintedRectangles.ColourOf(XColors.Red));
    }
}
