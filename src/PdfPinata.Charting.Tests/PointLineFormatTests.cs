using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A data point that carries a line format of its own, which each chart type used to turn into a
///   pen its own way.
/// </summary>
/// <remarks>
///   The column chart took any line format on the point and resolved it against the series' pen
///   through <c>Converter.ToXPen</c>. The bar chart did the same, but only when the point named a
///   colour, so a point that set a width and nothing else was drawn with the series' pen. The pie
///   chart also asked for a colour and then made a pen of that colour alone - width 1, whatever the
///   point said, and stroked even when the point said <c>Visible = false</c>, which is defect C12
///   in <c>docs/specs/charting-renderer-findings.md</c> for one sector. All of them now follow the
///   column chart.
///
///   Nothing else on these pages is drawn at a width of 3 or in red, so a stroke of either is the
///   point's own.
/// </remarks>
public class PointLineFormatTests
{
    private static readonly string Red = PaintedRectangles.ColourOf(XColors.Red);

    private static Chart WithAPointLine(ChartType type, bool visible, XColor colour)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        var point = chart.SeriesCollection[0].Elements[1];
        point.LineFormat.Visible = visible;
        point.LineFormat.Width = 3;
        if (!colour.IsEmpty)
            point.LineFormat.Color = colour;
        return chart;
    }

    private static int StrokedAtWidth3(PdfPage page) =>
        PaintedPaths.On(page).Count(path => path.Stroked && System.Math.Abs(path.LineWidth - 3) < 0.001);

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void APointThatSetsAWidthAndNoColourIsStrokedAtThatWidth(ChartType type)
    {
        // A bar chart and a pie chart looked for a colour before they looked at the point at all,
        // so a width on its own was dropped in favour of the series' pen.
        var page = Drawn.Page(WithAPointLine(type, visible: true, XColor.Empty));

        StrokedAtWidth3(page).Should().Be(1, "exactly one point asked for a line 3 wide");
    }

    [Theory]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void APiePointIsStrokedAtItsOwnWidthAndColour(ChartType type)
    {
        // It used to be drawn with a pen of the colour and nothing else, so 1 wide.
        var page = Drawn.Page(WithAPointLine(type, visible: true, XColors.Red));

        var red = PaintedPaths.StrokedIn(page, Red);
        red.Should().ContainSingle();
        red[0].LineWidth.Should().BeApproximately(3, 0.001);
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void AHiddenPointLineIsNotStrokedEvenWhenItNamesAColour(ChartType type)
    {
        // Visible = false is no line, not a line of width 1 in the colour given (C12).
        var page = Drawn.Page(WithAPointLine(type, visible: false, XColors.Red));

        PaintedPaths.StrokedIn(page, Red).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.Bar2D)]
    public void APointWithNoLineFormatKeepsTheSeriesPen(ChartType type)
    {
        // The other points of every chart above: a point that never asked for a line format is
        // drawn with its series' line, as it always was.
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        var series = chart.SeriesCollection[0];
        series.LineFormat.Visible = true;
        series.LineFormat.Color = XColors.Red;
        series.LineFormat.Width = 2;

        PaintedPaths.StrokedIn(Drawn.Page(chart), Red).Should().HaveCount(3)
            .And.OnlyContain(path => System.Math.Abs(path.LineWidth - 2) < 0.001);
    }
}
