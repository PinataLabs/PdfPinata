using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A series whose line format says <c>Visible = false</c>, which is empira/PDFsharp#287.
/// </summary>
/// <remarks>
///   <c>Converter.ToXPen</c> answers a hidden format with a pen of width 0, and the renderers'
///   own convention is that such a pen is no line at all: the column plot area, the plot-area
///   border and the value axis all test <c>Width &gt; 0</c> before they stroke. The line, area,
///   bar and pie plot areas did not, and handed the pen straight to <c>XGraphics</c> - where a
///   width of 0 is the thinnest line the device can draw rather than no line, so a hidden series
///   came out as a hairline. The column renderer is the twin that had the guard.
///
///   Each series here is given a colour of its own so that what is asked is exactly whether
///   anything was stroked in it; nothing else on the page is drawn in red.
/// </remarks>
public class HiddenSeriesLineTests
{
    private static readonly string Red = PaintedRectangles.ColourOf(XColors.Red);

    private static Chart WithARedLine(ChartType type, bool visible)
    {
        var chart = Charts.OfSeries(type, new[] { 1.0, 3.0, 2.0 }, new[] { 2.0, 1.0, 3.0 });
        var series = chart.SeriesCollection[0];
        series.LineFormat.Color = XColors.Red;
        series.LineFormat.Width = 2;
        series.LineFormat.Visible = visible;
        return chart;
    }

    private static int StrokedInRed(PdfPage page) => PaintedPaths.StrokedIn(page, Red).Count;

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void AVisibleLineFormatIsStroked(ChartType type)
    {
        // The other half of the question: the format is honoured when it asks for a line, so that
        // the test below is not passing on a chart that never strokes in red at all.
        StrokedInRed(Drawn.Page(WithARedLine(type, visible: true))).Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void AHiddenLineFormatIsNotStrokedAtAll(ChartType type)
    {
        StrokedInRed(Drawn.Page(WithARedLine(type, visible: false))).Should().Be(0,
            "a line format that says Visible = false is no line, not a hairline");
    }

    [Fact]
    public void AHiddenLineStillHasItsMarkersAndLeavesTheOtherSeriesAlone()
    {
        // Visible is about the line. The markers are the series' points and are drawn - filled in
        // the line's colour, as they are when the line is shown - and the second series still has
        // its line.
        var hidden = Drawn.Page(WithARedLine(ChartType.Line, visible: false));
        var shown = Drawn.Page(WithARedLine(ChartType.Line, visible: true));

        PaintedPaths.FilledIn(hidden, Red).Count.Should().Be(3);
        PaintedPaths.On(hidden).Count(path => path.Stroked && !path.Filled)
            .Should().Be(PaintedPaths.On(shown).Count(path => path.Stroked && !path.Filled) - 1,
                "exactly one line - the hidden one - is missing");
    }

    [Fact]
    public void AnAreaWithAHiddenOutlineIsStillFilled()
    {
        var hidden = Drawn.Page(WithARedLine(ChartType.Area2D, visible: false));
        var shown = Drawn.Page(WithARedLine(ChartType.Area2D, visible: true));

        PaintedPaths.On(hidden).Count(path => path.Filled)
            .Should().Be(PaintedPaths.On(shown).Count(path => path.Filled));
    }

    [Fact]
    public void AHiddenLineIsNotDrawnInTheLegendEither()
    {
        // A line chart's legend draws a stroke of the line with its marker on it. The stroke is
        // the line, so a hidden line has none; the marker and the name stay.
        var chart = WithARedLine(ChartType.Line, visible: false);
        chart.SeriesCollection[0].Name = "Hidden";
        chart.Legend.Docking = DockingType.Bottom;

        var page = Drawn.Page(chart);

        StrokedInRed(page).Should().Be(0);
        ShownText.On(page).Should().Contain("Hidden");
        PaintedPaths.FilledIn(page, Red).Count.Should().Be(4, "three points and the legend's marker");
    }

    [Theory]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.Pie2D)]
    public void AHiddenBorderIsNotDrawnRoundTheLegendSwatchEither(ChartType type)
    {
        // Every other chart's legend draws a swatch of the fill outlined with the series' line,
        // and that outline is hidden the same way as the one in the plot area.
        var chart = WithARedLine(type, visible: false);
        chart.Legend.Docking = DockingType.Bottom;

        StrokedInRed(Drawn.Page(chart)).Should().Be(0);
    }
}
