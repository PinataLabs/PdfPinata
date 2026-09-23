using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   Where the columns of a column chart land, and which of them are drawn at all.
/// </summary>
/// <remarks>
///   Two methods between them decide that. <c>ColumnClusteredPlotAreaRenderer.CalcColumns</c>
///   divides each category's slot between the series plotting in it and works out the rectangle
///   for every point; <c>ColumnPlotAreaRenderer.Draw</c> fills them, outlines them afterwards so
///   that a border cannot be overdrawn by the next column along, and drops any point lying outside
///   the scale rather than clipping it.
///
///   Neither is reachable except by drawing, and neither produces anything but geometry - so what
///   these tests read back is the geometry, as the <c>re</c> operators the page was written with.
/// </remarks>
public class ColumnPlotAreaTests
{
    [Fact]
    public void AColumnIsAsTallAsTheValueItPlots()
    {
        var columns = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0)));

        columns.Should().HaveCount(3);
        columns[1].Height.Should().BeApproximately(columns[0].Height * 5, Tolerance);
        columns[2].Height.Should().BeApproximately(columns[0].Height * 3, Tolerance);
    }

    [Fact]
    public void EveryColumnStandsOnTheSameBaseline()
    {
        var columns = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0)));

        columns.Select(column => column.Y).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void ColumnsAreEvenlySpacedAcrossThePlotArea()
    {
        var columns = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0)));

        columns.Select(column => column.Width).Distinct().Should().ContainSingle();

        var firstGap = columns[1].X - columns[0].X;
        (columns[2].X - columns[1].X).Should().BeApproximately(firstGap, Tolerance);
    }

    /// <summary>
    ///   Two series in a category stand side by side and share the slot between them: half the
    ///   width each, the second beginning exactly where the first ends.
    /// </summary>
    [Fact]
    public void ClusteredSeriesStandSideBySideWithinACategory()
    {
        var oneSeries = PaintedRectangles.FilledOn(
            Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0)));

        var page = Drawn.Page(Charts.OfSeries(ChartType.Column2D,
            [1.0, 5.0, 3.0], [2.0, 4.0, 1.0]));
        var clustered = PaintedRectangles.FilledOn(page);

        clustered.Should().HaveCount(6);

        // The columns come out a series at a time, so the fourth is the second series' first.
        clustered[0].Width.Should().BeApproximately(oneSeries[0].Width / 2, Tolerance);
        clustered[3].Width.Should().BeApproximately(clustered[0].Width, Tolerance);
        clustered[3].X.Should().BeApproximately(clustered[0].Right, Tolerance);
    }

    [Fact]
    public void EachSeriesTakesTheNextColourFromThePalette()
    {
        var page = Drawn.Page(Charts.OfSeries(ChartType.Column2D,
            [1.0, 5.0], [2.0, 4.0], [3.0, 3.0]));

        var columns = PaintedRectangles.FilledOn(page);

        // The first three of the column palette the charting package took from Excel.
        columns.Select(column => column.Colour).Should().Equal(
            Palette(0x9999FF), Palette(0x9999FF),
            Palette(0x993366), Palette(0x993366),
            Palette(0xFFFFCC), Palette(0xFFFFCC));
    }

    /// <summary>
    ///   A column outside the scale is left undrawn rather than clipped, which the renderer's own
    ///   comment gives as the reason: half a column cut off at the top of the plot area would say
    ///   something the data does not.
    /// </summary>
    [Fact]
    public void AValueAboveTheGivenScaleIsNotDrawnAtAll()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 40.0);
        chart.YAxis.MinimumScale = 0;
        chart.YAxis.MaximumScale = 5;
        chart.YAxis.MajorTick = 1;

        var columns = PaintedRectangles.FilledOn(Drawn.Page(chart));

        columns.Should().ContainSingle("the column for 40 is off the top of a scale ending at 5");
    }

    /// <summary>
    ///   Borders are stroked after every column has been filled, so that a border is never
    ///   overdrawn by the neighbour filled next. The renderer says so in a comment and this is
    ///   what the content stream shows.
    /// </summary>
    [Fact]
    public void ColumnBordersAreStrokedAfterEveryColumnHasBeenFilled()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0));

        var rectangles = PaintedRectangles.On(page)
            .Where(rectangle => rectangle.Filled || rectangle.Stroked)
            .ToList();

        rectangles.Should().HaveCount(6);
        rectangles.Take(3).Should().OnlyContain(rectangle => rectangle.Filled);
        rectangles.Skip(3).Should().OnlyContain(rectangle => rectangle.Stroked && !rectangle.Filled);
    }

    /// <summary>
    ///   With gridlines on and data on both sides of zero, the renderer forces a line along zero.
    ///   It exists because the calculated scale need not put a major tick there - as here, where
    ///   the axis runs from -6 to 8 in steps of two and so does have one, but the line is drawn
    ///   from the plot area rather than from the axis and is drawn regardless.
    /// </summary>
    [Fact]
    public void DataOnBothSidesOfZeroGetsAZeroLineOfItsOwn()
    {
        var straddling = Charts.Of(ChartType.Column2D, -4.0, 6.0);
        straddling.YAxis.HasMajorGridlines = true;

        var oneSided = Charts.Of(ChartType.Column2D, 4.0, 6.0);
        oneSided.YAxis.HasMajorGridlines = true;

        var acrossZero = StrokedLines.Of(Drawn.Page(straddling));
        var aboveZero = StrokedLines.Of(Drawn.Page(oneSided));

        // Both charts are drawn with eight gridlines and eight tick marks; only the one whose data
        // crosses zero gets a seventeenth line.
        acrossZero.Should().HaveCount(aboveZero.Count + 1);
    }

    [Fact]
    public void AColumnBelowZeroHangsUnderTheColumnsAboveIt()
    {
        var columns = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Column2D, -4.0, 6.0, 2.0)));

        // The negative column ends where the positive ones begin: they share the zero line, one
        // hanging from it and the others standing on it.
        columns[0].Top.Should().BeApproximately(columns[1].Y, Tolerance);
        columns[2].Y.Should().BeApproximately(columns[1].Y, Tolerance);
    }

    [Fact]
    public void StackedColumnsSitOnTopOfOneAnother()
    {
        var page = Drawn.Page(Charts.OfSeries(ChartType.ColumnStacked2D,
            [1.0, 2.0], [3.0, 1.0]));

        var columns = PaintedRectangles.FilledOn(page);

        columns.Should().HaveCount(4);

        // A stacked chart gives each category one slot rather than dividing it, so both series
        // are the full width, and the second stands on the first.
        columns[2].Width.Should().BeApproximately(columns[0].Width, Tolerance);
        columns[2].X.Should().BeApproximately(columns[0].X, Tolerance);
        columns[2].Y.Should().BeApproximately(columns[0].Top, Tolerance);
        columns[2].Height.Should().BeApproximately(columns[0].Height * 3, Tolerance);
    }

    [Fact]
    public void AColumnForZeroIsDrawnWithNoHeight()
    {
        var columns = PaintedRectangles.FilledOn(Drawn.Page(Charts.Of(ChartType.Column2D, 0.0, 0.0)));

        columns.Should().HaveCount(2);
        columns.Should().OnlyContain(column => column.Height == 0);
    }

    /// <summary>
    ///   A frame too small for its own axes draws no columns rather than throwing.
    /// </summary>
    /// <remarks>
    ///   Both plot area renderers open by returning if the plot area is empty, which reads as a
    ///   decision already taken that a chart with no room to draw in should draw nothing. The
    ///   guard used to be unreachable: the layout subtracts the axes from the frame and assigns
    ///   the remainder as a width, and <see cref="PdfPinata.Drawing.XRect"/> answers a negative
    ///   one by throwing, so what a caller got was an <see cref="ArgumentException"/> from three
    ///   frames down naming a rectangle rather than the chart. An extent below zero is now taken
    ///   as no extent, which is what makes the plot area empty rather than impossible.
    /// </remarks>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    public void AFrameTooSmallForItsAxesDrawsNothingInThePlotArea(ChartType type)
    {
        var page = Drawn.Page(Charts.Of(type, 1.0, 5.0, 3.0), 20, 15);

        PaintedRectangles.FilledOn(page).Should().BeEmpty();
    }

    [Fact]
    public void AFrameTooSmallForItsAxesStillWritesNoNaN()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0), 20, 15);

        System.Text.Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
    }

    private static string Palette(int rgb) =>
        PaintedRectangles.ColourOf(XColor.FromArgb(rgb >> 16 & 0xFF, rgb >> 8 & 0xFF, rgb & 0xFF));

    private const double Tolerance = 0.01;
}
