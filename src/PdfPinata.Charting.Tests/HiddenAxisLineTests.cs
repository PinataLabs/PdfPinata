using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   An axis, gridline or legend line format that gives a width but not <c>Visible = true</c>.
/// </summary>
/// <remarks>
///   <c>LineFormat.Visible</c> defaults to false, and <c>Converter.ToXPen</c> turns a hidden format
///   into a pen of width 0. The renderers' convention since C12 is that such a pen is no line, but
///   the axes predated it and disagreed among themselves: a column chart's value axis tested the
///   width before drawing its line, while the category axis and a bar chart's value axis tested
///   only that there was a line format, and stroked the width-0 pen - which PDF draws as the
///   thinnest line the device can, not as nothing. The tick marks, gridlines, zero baseline and
///   legend border tested nothing. <c>LineFormatRenderer</c> now treats a pen of width 0 as no pen,
///   and everything below is drawn through it.
///
///   Everything asked about is given a colour nothing else on the page is drawn in, so the question
///   is exactly whether anything was stroked in it.
/// </remarks>
public class HiddenAxisLineTests
{
    private static readonly string Red = PaintedRectangles.ColourOf(XColors.Red);

    public enum Which
    {
        XAxis,
        YAxis
    }

    private static Chart WithARedAxis(ChartType type, Which which, bool visible)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        var axis = which == Which.XAxis ? chart.XAxis : chart.YAxis;
        axis.LineFormat.Color = XColors.Red;
        axis.LineFormat.Width = 1;
        axis.LineFormat.Visible = visible;
        return chart;
    }

    private static int StrokedInRed(PdfPage page) => StrokedLines.Of(page).Count(line => line.Colour == Red);

    [Theory]
    [InlineData(ChartType.Column2D, Which.XAxis)]
    [InlineData(ChartType.Column2D, Which.YAxis)]
    [InlineData(ChartType.Line, Which.XAxis)]
    [InlineData(ChartType.Line, Which.YAxis)]
    [InlineData(ChartType.Bar2D, Which.XAxis)]
    [InlineData(ChartType.Bar2D, Which.YAxis)]
    [InlineData(ChartType.BarStacked2D, Which.YAxis)]
    public void AVisibleAxisLineIsStroked(ChartType type, Which which)
    {
        // The other half, so the test below is not passing on an axis that is never drawn in red.
        StrokedInRed(Drawn.Page(WithARedAxis(type, which, visible: true))).Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(ChartType.Column2D, Which.XAxis)]
    [InlineData(ChartType.Column2D, Which.YAxis)]
    [InlineData(ChartType.Line, Which.XAxis)]
    [InlineData(ChartType.Line, Which.YAxis)]
    [InlineData(ChartType.Bar2D, Which.XAxis)]
    [InlineData(ChartType.Bar2D, Which.YAxis)]
    [InlineData(ChartType.BarStacked2D, Which.YAxis)]
    public void AnAxisLineThatIsNotVisibleIsNotStrokedOnAnyAxis(ChartType type, Which which)
    {
        // Its tick marks are drawn from the same format, and are hidden with it.
        StrokedInRed(Drawn.Page(WithARedAxis(type, which, visible: false))).Should().Be(0,
            "a line format that is not visible is no line on every axis, not a hairline on some");
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void GridlinesThatAreNotVisibleAreNotStroked(ChartType type)
    {
        // A negative value puts zero inside the scale, so the zero baseline is drawn from the
        // gridlines' pen as well.
        var chart = Charts.Of(type, -1.0, 3.0, 2.0);
        chart.YAxis.MajorGridlines.LineFormat.Color = XColors.Red;
        chart.YAxis.MajorGridlines.LineFormat.Width = 1;
        chart.YAxis.HasMajorGridlines = true;

        StrokedInRed(Drawn.Page(chart)).Should().Be(0);

        chart.YAxis.MajorGridlines.LineFormat.Visible = true;
        StrokedInRed(Drawn.Page(chart)).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ALegendBorderThatIsNotVisibleIsNotStroked()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0);
        chart.SeriesCollection[0].Name = "Series";
        chart.Legend.Docking = DockingType.Bottom;
        chart.Legend.LineFormat.Color = XColors.Red;
        chart.Legend.LineFormat.Width = 1;

        PaintedPaths.StrokedIn(Drawn.Page(chart), Red).Should().BeEmpty();

        chart.Legend.LineFormat.Visible = true;
        PaintedPaths.StrokedIn(Drawn.Page(chart), Red).Should().ContainSingle();
    }
}
