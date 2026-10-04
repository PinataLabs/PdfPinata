using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A chart line format given a dash pattern of its own (#208).
/// </summary>
/// <remarks>
///   <c>LineFormat.DashStyle</c> could be set to <c>XDashStyle.Custom</c>, but the format had no
///   pattern to go with it and <c>Converter.ToXPen</c> built the pen from the style alone, so the
///   pen reached the content stream as <c>Custom</c> with no pattern and was written
///   <c>[]0 d</c> - a solid line - for every series, point, axis and gridline.
///   <c>LineFormat.DashPattern</c> is that pattern, in units of the line width as
///   <c>XPen.DashPattern</c> is, so what the <c>d</c> operator says is the pattern times the width.
///
///   Nothing else on these pages is stroked in red or blue, so a stroke in either is the line the
///   test is about.
/// </remarks>
public class CustomDashPatternTests
{
    private static readonly string Red = PaintedRectangles.ColourOf(XColors.Red);
    private static readonly string Blue = PaintedRectangles.ColourOf(XColors.Blue);

    private static void Dash(LineFormat lineFormat, XColor colour, double width, params double[] pattern)
    {
        lineFormat.Visible = true;
        lineFormat.Color = colour;
        lineFormat.Width = width;
        lineFormat.DashPattern = pattern;
    }

    private static IReadOnlyList<PaintedPaths.Path> RedOn(Chart chart) => PaintedPaths.StrokedIn(Drawn.Page(chart), Red);

    private static void ShouldBeDashed(IReadOnlyList<PaintedPaths.Path> paths, params double[] expected)
    {
        paths.Should().NotBeEmpty();
        foreach (var path in paths)
        {
            path.DashArray.Should().Equal(expected, (a, e) => Math.Abs(a - e) < 0.001, path.ToString());
            path.DashPhase.Should().Be(0, "a pattern starts at its beginning");
        }
    }

    [Fact]
    public void SettingADashPatternMakesTheDashStyleCustom()
    {
        var lineFormat = new LineFormat { DashPattern = [3, 1] };

        lineFormat.DashStyle.Should().Be(XDashStyle.Custom, "as setting XPen.DashPattern does");
        lineFormat.DashPattern.Should().Equal(3, 1);
    }

    [Fact]
    public void ALineFormatKeepsACopyOfThePatternItWasGiven()
    {
        var pattern = new double[] { 3, 1 };
        var lineFormat = new LineFormat { DashPattern = pattern };

        pattern[0] = 7;
        lineFormat.DashPattern[1] = 9;

        lineFormat.DashPattern.Should().Equal([3, 1],
            "changing the array afterwards would bypass the check that every length is positive");
    }

    [Fact]
    public void ALineFormatWithNoPatternAnswersAnEmptyOne()
    {
        new LineFormat().DashPattern.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void ADashOrGapThatIsNotPositiveIsRefused(double length)
    {
        var lineFormat = new LineFormat();

        var act = () => lineFormat.DashPattern = [3, length];

        act.Should().Throw<ArgumentException>();
        lineFormat.DashStyle.Should().Be(XDashStyle.Solid, "a refused pattern changes nothing");
    }

    [Fact]
    public void ANullPatternIsRefused()
    {
        var act = () => new LineFormat().DashPattern = null;

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    public void ASeriesLineIsDrawnInItsPatternScaledByItsWidth(ChartType type)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        Dash(chart.SeriesCollection[0].LineFormat, XColors.Red, 2, 3, 1);

        ShouldBeDashed(RedOn(chart), 6, 2);
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.Pie2D)]
    public void APointThatSaysNothingAboutDashesInheritsItsSeriesPattern(ChartType type)
    {
        // Since #192 a point that sets only its colour or its width keeps its series' dash style,
        // and a Custom without the pattern it came with was a solid line.
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        Dash(chart.SeriesCollection[0].LineFormat, XColors.Red, 2, 3, 1);
        var point = chart.SeriesCollection[0].Elements[1].LineFormat;
        point.Visible = true;
        point.Color = XColors.Blue;
        point.Width = 4;

        var page = Drawn.Page(chart);

        ShouldBeDashed(PaintedPaths.StrokedIn(page, Blue), 12, 4);
        PaintedPaths.StrokedIn(page, Blue).Should().ContainSingle();
        ShouldBeDashed(PaintedPaths.StrokedIn(page, Red), 6, 2);
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Pie2D)]
    public void APointWithAPatternOfItsOwnIsDrawnInIt(ChartType type)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        Dash(chart.SeriesCollection[0].LineFormat, XColors.Red, 2, 3, 1);
        Dash(chart.SeriesCollection[0].Elements[1].LineFormat, XColors.Blue, 2, 1, 2);

        ShouldBeDashed(PaintedPaths.StrokedIn(Drawn.Page(chart), Blue), 2, 4);
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Pie2D)]
    public void APointThatChoosesAnotherStyleIsNotDrawnInItsSeriesPattern(ChartType type)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        Dash(chart.SeriesCollection[0].LineFormat, XColors.Red, 2, 3, 1);
        var point = chart.SeriesCollection[0].Elements[1].LineFormat;
        point.Visible = true;
        point.Color = XColors.Blue;
        point.DashStyle = XDashStyle.Solid;

        PaintedPaths.StrokedIn(Drawn.Page(chart), Blue).Should().ContainSingle()
            .Which.Dashed.Should().BeFalse("the point asked for a solid line");
    }

    [Theory]
    [InlineData(ChartType.Column2D, true)]
    [InlineData(ChartType.Column2D, false)]
    [InlineData(ChartType.Bar2D, true)]
    [InlineData(ChartType.Bar2D, false)]
    public void AnAxisLineIsDrawnInItsPattern(ChartType type, bool xAxis)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        Dash((xAxis ? chart.XAxis : chart.YAxis).LineFormat, XColors.Red, 1.5, 4, 2);

        // Its tick marks are drawn from the same format, at their own widths, so the line itself
        // is the stroke 1.5 wide.
        ShouldBeDashed([.. RedOn(chart).Where(path => Math.Abs(path.LineWidth - 1.5) < 0.001)], 6, 3);
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Bar2D)]
    public void AGridlineIsDrawnInItsPattern(ChartType type)
    {
        var chart = Charts.Of(type, 1.0, 3.0, 2.0);
        chart.YAxis.HasMajorGridlines = true;
        Dash(chart.YAxis.MajorGridlines.LineFormat, XColors.Red, 0.5, 4, 2);

        ShouldBeDashed(RedOn(chart), 2, 1);
    }

    [Fact]
    public void CustomWithNoPatternIsDrawnSolidAsAnXPenIs()
    {
        // XPen draws a Custom with no pattern as a solid line, and so does a chart.
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0);
        var lineFormat = chart.SeriesCollection[0].LineFormat;
        lineFormat.Visible = true;
        lineFormat.Color = XColors.Red;
        lineFormat.DashStyle = XDashStyle.Custom;

        RedOn(chart).Should().HaveCount(3).And.OnlyContain(path => !path.Dashed);
    }

    [Fact]
    public void ACopiedChartIsDrawnInThePatternAndKeepsItsOwnCopyOfIt()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0);
        Dash(chart.SeriesCollection[0].LineFormat, XColors.Red, 2, 3, 1);
        var point = chart.SeriesCollection[0].Elements[1].LineFormat;
        point.Visible = true;
        point.Color = XColors.Blue;

        var copy = chart.Clone();
        chart.SeriesCollection[0].LineFormat.DashPattern = [1, 1];

        var page = Drawn.Page(copy);
        ShouldBeDashed(PaintedPaths.StrokedIn(page, Red), 6, 2);
        ShouldBeDashed(PaintedPaths.StrokedIn(page, Blue), 6, 2);
    }

    [Fact]
    public void ACopiedLineFormatDoesNotShareItsPatternWithTheOriginal()
    {
        var original = new LineFormat { DashPattern = [3, 1] };

        var copy = original.Clone();

        copy.DashStyle.Should().Be(XDashStyle.Custom);
        copy.DashPattern.Should().Equal(3, 1);
    }
}
