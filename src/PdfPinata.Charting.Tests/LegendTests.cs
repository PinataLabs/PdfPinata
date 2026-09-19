using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The key beside the chart that says which series is which.
/// </summary>
/// <remarks>
///   <c>LegendRenderer.Format</c> measures one entry per series - a marker, a gap and the series
///   name - and adds them up: side by side for a legend docked above or below the chart, one under
///   another for a legend docked beside it, with padding round the outside that is doubled when the
///   legend has a border to keep clear of. <c>ChartRenderer.LayoutLegend</c> then takes that much
///   room off the side it is docked to, and <c>LegendRenderer.Draw</c> hands each entry its
///   rectangle for <c>LegendEntryRenderer</c> to draw into: a swatch of the series' fill for a
///   column, a stroke of the line with its marker on it for a line.
///
///   None of that leaves a trace but the page, so every assertion here is about the page: the
///   series names read back through <see cref="ShownText"/>, the swatches and the border through
///   <see cref="PaintedRectangles"/>, and the line keys through <see cref="StrokedLines"/> and
///   <see cref="PaintedPaths"/>. Positions are in the space the chart was drawn in, which is the
///   frame's own - so the legend's padding and docking can be stated against the frame's size.
/// </remarks>
public class LegendTests
{
    /// <summary>The padding between the edge of the legend's room and its border.</summary>
    private const double Padding = 6;

    /// <summary>The gap between one legend entry and the next.</summary>
    private const double EntrySpacing = 5;

    /// <summary>The gap between an entry's marker and its text.</summary>
    private const double MarkerToText = 4.3;

    /// <summary>The swatch drawn for a column series, square.</summary>
    private const double Swatch = 7;

    private static readonly XColor NorthColour = XColors.Orange;
    private static readonly XColor SouthColour = XColors.Teal;
    private static readonly XColor BorderColour = XColors.Green;

    // ----- whether there is a legend at all -----

    [Fact]
    public void AChartThatNeverAskedForALegendNamesNoSeries()
    {
        var page = Drawn.Page(TwoNamedSeries(ChartType.Column2D));

        ShownText.On(page).Should().NotContain("North").And.NotContain("South");
    }

    /// <summary>
    ///   Reading <see cref="Chart.Legend"/> is itself the request: the property creates the legend
    ///   the first time it is asked for, and the renderer draws one whenever it exists.
    /// </summary>
    [Fact]
    public void AskingForTheLegendIsEnoughToHaveOneDrawn()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        _ = chart.Legend;

        ShownText.On(Drawn.Page(chart)).Should().ContainInOrder("North", "South");
    }

    [Theory]
    [InlineData(DockingType.Top)]
    [InlineData(DockingType.Bottom)]
    [InlineData(DockingType.Left)]
    [InlineData(DockingType.Right)]
    public void EveryDockingPositionNamesEverySeriesInOrder(DockingType docking)
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = docking;

        ShownText.On(Drawn.Page(chart)).Should().ContainInOrder("North", "South");
    }

    // ----- where the legend goes -----

    [Fact]
    public void ALegendDockedRightIsDrawnAgainstTheRightEdgeAndClearOfEveryColumn()
    {
        var page = Drawn.Page(Bordered(DockingType.Right));
        var border = BorderOn(page);

        border.Right.Should().BeApproximately(Drawn.DefaultWidth - Padding, 0.01);
        border.CentreY.Should().BeApproximately(Drawn.DefaultHeight / 2, 0.01,
            "a legend docked beside the chart is centred down its side");
        Columns(page).Should().NotBeEmpty().And.OnlyContain(column => column.Right <= border.X);
    }

    [Fact]
    public void ALegendDockedLeftIsDrawnAgainstTheLeftEdgeAndClearOfTheValueAxis()
    {
        var page = Drawn.Page(Bordered(DockingType.Left));
        var border = BorderOn(page);

        border.X.Should().BeApproximately(Padding, 0.01);
        border.CentreY.Should().BeApproximately(Drawn.DefaultHeight / 2, 0.01);
        Columns(page).Should().NotBeEmpty().And.OnlyContain(column => column.X >= border.Right);
        TickLabels(page).Should().NotBeEmpty().And.OnlyContain(label => label.X >= border.Right,
            "the value axis is pushed right to make room, labels and all");
    }

    [Fact]
    public void ALegendDockedTopIsDrawnAgainstTheTopEdgeAndAboveEveryColumn()
    {
        var page = Drawn.Page(Bordered(DockingType.Top));
        var border = BorderOn(page);

        border.Top.Should().BeApproximately(Drawn.DefaultHeight - Padding, 0.01);
        border.CentreX.Should().BeApproximately(Drawn.DefaultWidth / 2, 0.01,
            "a legend docked above or below the chart is centred across it");
        Columns(page).Should().NotBeEmpty().And.OnlyContain(column => column.Top <= border.Y);
        TickLabels(page).Should().NotBeEmpty().And.OnlyContain(label => label.Y <= border.Y);
    }

    [Fact]
    public void ALegendDockedBottomIsDrawnAgainstTheBottomEdgeAndBelowTheCategoryLabels()
    {
        var page = Drawn.Page(Bordered(DockingType.Bottom));
        var border = BorderOn(page);

        border.Y.Should().BeApproximately(Padding, 0.01);
        border.CentreX.Should().BeApproximately(Drawn.DefaultWidth / 2, 0.01);
        Columns(page).Should().NotBeEmpty().And.OnlyContain(column => column.Y >= border.Top);
        CategoryLabels(page).Should().NotBeEmpty().And.OnlyContain(label => label.Y >= border.Top);
    }

    /// <summary>
    ///   The legend's room comes out of the plot area's rather than being drawn over it, so the
    ///   same columns end further left once a legend has been docked to their right.
    /// </summary>
    [Fact]
    public void ALegendTakesItsRoomFromThePlotArea()
    {
        var without = Drawn.Page(TwoNamedSeries(ChartType.Column2D));
        var with = Drawn.Page(Bordered(DockingType.Right));

        Columns(with).Max(column => column.Right).Should().BeLessThan(Columns(without).Max(column => column.Right));
    }

    // ----- how the entries are laid out -----

    /// <summary>
    ///   Beside the chart the entries are stacked, each one a line of text below the last with the
    ///   entry spacing between them - which depends on the legend's own font, not the chart's.
    /// </summary>
    [Theory]
    [InlineData(DockingType.Left, 0)]
    [InlineData(DockingType.Right, 0)]
    [InlineData(DockingType.Right, 20)]
    public void ALegendBesideTheChartStacksItsEntries(DockingType docking, double fontSize)
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = docking;
        if (fontSize > 0)
            chart.Legend.Font.Size = fontSize;

        var page = Drawn.Page(chart);
        var north = RunReading(page, "North");
        var south = RunReading(page, "South");

        south.X.Should().BeApproximately(north.X, 0.01);
        (north.Y - south.Y).Should().BeApproximately(
            HeightOf(fontSize > 0 ? fontSize : DefaultFontSize) + EntrySpacing, 0.01);
    }

    /// <summary>
    ///   Above or below the chart the entries run side by side, each one starting the entry
    ///   spacing after the last one's text ends.
    /// </summary>
    [Theory]
    [InlineData(DockingType.Top)]
    [InlineData(DockingType.Bottom)]
    public void ALegendAboveOrBelowTheChartSetsItsEntriesSideBySide(DockingType docking)
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = docking;

        var page = Drawn.Page(chart);
        var north = RunReading(page, "North");
        var south = RunReading(page, "South");

        south.Y.Should().BeApproximately(north.Y, 0.01);
        (south.X - north.X).Should().BeApproximately(
            WidthOf("North", DefaultFontSize) + Swatch + MarkerToText + EntrySpacing, 0.01);
    }

    /// <summary>
    ///   A column series is keyed with a square swatch of its own fill, set just before its name.
    /// </summary>
    [Fact]
    public void AColumnSeriesIsKeyedWithASwatchOfItsFill()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);

        foreach (var (name, colour) in new[] { ("North", NorthColour), ("South", SouthColour) })
        {
            var text = RunReading(page, name);
            var swatch = Swatches(page).Single(s => s.Colour == PaintedRectangles.ColourOf(colour));

            swatch.Width.Should().BeApproximately(Swatch, 0.01);
            swatch.Height.Should().BeApproximately(Swatch, 0.01);
            swatch.Right.Should().BeApproximately(text.X - MarkerToText, 0.01);
            swatch.Y.Should().BeInRange(text.Y - Swatch, text.Y + Swatch,
                "the swatch sits on the line of the text it keys");
        }
    }

    /// <summary>
    ///   A stacked column is drawn bottom up, first series lowest, so its legend lists the series
    ///   the other way round - the order a reader sees them in the stack.
    /// </summary>
    [Fact]
    public void AStackedColumnLegendListsItsSeriesInStackOrder()
    {
        var chart = TwoNamedSeries(ChartType.ColumnStacked2D);
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);

        RunReading(page, "South").Y.Should().BeGreaterThan(RunReading(page, "North").Y);
    }

    /// <summary>
    ///   A series with no name still has its entry, and the entry is only its swatch.
    /// </summary>
    [Fact]
    public void AnUnnamedSeriesIsKeyedWithItsSwatchAlone()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.SeriesCollection[0].Name = "";
        chart.SeriesCollection[1].Name = "";
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var swatches = Swatches(page);
        var columns = Columns(page);

        swatches.Should().HaveCount(2);
        swatches.Should().OnlyContain(s => s.X > columns.Max(column => column.Right));
        swatches[0].Y.Should().BeGreaterThan(swatches[1].Y, "the entries are still stacked");
        ShownText.On(page).Should().OnlyContain(text => IsAxisLabel(text));
    }

    // ----- the border -----

    [Fact]
    public void ALegendWithNoLineFormatHasNoBorder()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var north = RunReading(page, "North");

        PaintedRectangles.On(page).Should().NotContain(r =>
            r.Stroked && r.X <= north.X && r.Right >= north.X && r.Y <= north.Y && r.Top >= north.Y);
    }

    /// <summary>
    ///   A visible line format draws the border in its colour and at its width, round every entry.
    /// </summary>
    [Fact]
    public void ALegendBorderIsStrokedInItsLineFormatAroundEveryEntry()
    {
        var chart = Bordered(DockingType.Right);
        chart.Legend.LineFormat.Width = 2;

        var page = Drawn.Page(chart);
        var border = PaintedPaths.StrokedIn(page, PaintedRectangles.ColourOf(BorderColour)).Single();

        border.LineWidth.Should().BeApproximately(2, 0.01);
        foreach (var name in new[] { "North", "South" })
        {
            var run = RunReading(page, name);
            run.X.Should().BeInRange(border.Left, border.Right);
            run.Y.Should().BeInRange(border.Bottom, border.Top);
        }
    }

    /// <summary>
    ///   A visible line format that states no width is drawn at the legend's own default - a
    ///   twentieth of a millimetre - rather than at nothing.
    /// </summary>
    [Fact]
    public void ALegendBorderWithNoWidthIsDrawnAtTheDefaultWidth()
    {
        var page = Drawn.Page(Bordered(DockingType.Right));

        PaintedPaths.StrokedIn(page, PaintedRectangles.ColourOf(BorderColour)).Single()
            .LineWidth.Should().BeApproximately(0.14, 0.001);
    }

    /// <summary>
    ///   A bordered legend is padded twice over: once outside the border and once inside it, so
    ///   that the entries do not touch the line. The first swatch starts one padding inside the
    ///   border, and the whole legend is one padding wider on each side than it would be unbordered.
    /// </summary>
    [Fact]
    public void ABorderedLegendIsPaddedInsideItsBorderAsWellAsOutside()
    {
        var bordered = Drawn.Page(Bordered(DockingType.Right));
        var border = BorderOn(bordered);
        var firstSwatch = Swatches(bordered).First();

        (firstSwatch.X - border.X).Should().BeApproximately(Padding, 0.01);
        (border.Top - firstSwatch.Top).Should().BeGreaterThanOrEqualTo(Padding);

        var plain = TwoNamedSeries(ChartType.Column2D);
        plain.Legend.Docking = DockingType.Right;
        var unbordered = Drawn.Page(plain);

        (RunReading(unbordered, "North").X - RunReading(bordered, "North").X).Should().BeApproximately(Padding, 0.01,
            "the legend is docked by its right edge and grows leftwards by the extra padding");
    }

    // ----- a line series -----

    /// <summary>
    ///   A line series is keyed with a stroke of the line in its colour, three markers wide, and
    ///   the marker itself drawn on the middle of it - but never shorter than three swatches, so a
    ///   small marker still has a line to sit on.
    /// </summary>
    [Theory]
    [InlineData(10, 30)]
    [InlineData(4, 21)]
    public void ALineSeriesIsKeyedWithAStrokeOfTheLineCarryingItsMarker(double markerSize, double keyLength)
    {
        var chart = Charts.Of(ChartType.Line, 1.0, 3.0, 2.0);
        var series = chart.SeriesCollection[0];
        series.Name = "North";
        series.MarkerStyle = MarkerStyle.Square;
        series.MarkerSize = markerSize;
        series.MarkerForegroundColor = XColors.Red;
        series.MarkerBackgroundColor = XColors.Blue;
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var text = RunReading(page, "North");
        var blue = PaintedRectangles.ColourOf(XColors.Blue);

        // The plot area strokes its line far thinner; the key is drawn with a pen of its own.
        var key = StrokedLines.Of(page).Single(line => line.Colour == blue && line.Width > 0.5);
        key.IsHorizontal.Should().BeTrue();
        Length(key).Should().BeApproximately(keyLength, 0.01);
        Math.Max(key.X1, key.X2).Should().BeApproximately(text.X - MarkerToText, 0.01);

        var marker = PaintedPaths.FilledIn(page, blue).First();
        marker.CentreX.Should().BeApproximately((key.X1 + key.X2) / 2, 0.01);
        marker.CentreY.Should().BeApproximately(key.Y1, 0.01);
        marker.Width.Should().BeApproximately(markerSize, 0.01);
    }

    /// <summary>
    ///   A marker size is a length, and the key is measured from it in points whatever unit it was
    ///   given in - a centimetre is 28.35 points, so its key is three times that rather than
    ///   three times one.
    /// </summary>
    [Fact]
    public void ALineKeyIsMeasuredFromAMarkerSizeInPointsWhateverItsUnit()
    {
        var chart = Charts.Of(ChartType.Line, 1.0, 3.0, 2.0);
        var series = chart.SeriesCollection[0];
        series.Name = "North";
        series.MarkerStyle = MarkerStyle.Square;
        series.MarkerSize = XUnit.FromCentimeter(1);
        series.MarkerBackgroundColor = XColors.Blue;
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var text = RunReading(page, "North");
        var blue = PaintedRectangles.ColourOf(XColors.Blue);
        var markerSize = XUnit.FromCentimeter(1).Point;

        var key = StrokedLines.Of(page).Single(line => line.Colour == blue && line.Width > 0.5);
        Length(key).Should().BeApproximately(3 * markerSize, 0.01);
        Math.Max(key.X1, key.X2).Should().BeApproximately(text.X - MarkerToText, 0.01);

        var marker = PaintedPaths.FilledIn(page, blue).First();
        marker.Width.Should().BeApproximately(markerSize, 0.01);
        marker.Left.Should().BeGreaterThanOrEqualTo(Math.Min(key.X1, key.X2),
            "the marker is drawn inside the room its entry reserved for it");
        marker.Right.Should().BeLessThanOrEqualTo(text.X - MarkerToText);
    }

    /// <summary>
    ///   A legend entry for a line series is wider than one for a column, and entries are widened
    ///   to the widest marker: a column series beside a line series is keyed with a swatch as wide
    ///   as the line's key, which is what keeps the two lined up.
    /// </summary>
    [Fact]
    public void AColumnSwatchBesideALineKeyIsWidenedToMatchIt()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.SeriesCollection[1].ChartType = ChartType.Line;
        chart.SeriesCollection[1].MarkerSize = 4;
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var swatch = Swatches(page, maxWidth: 3 * Swatch + 0.01).Single(s => s.Colour == PaintedRectangles.ColourOf(NorthColour));

        swatch.Width.Should().BeApproximately(3 * Swatch, 0.01);
        RunReading(page, "South").X.Should().BeApproximately(RunReading(page, "North").X, 0.01);
    }

    // ----- a pie -----

    /// <summary>
    ///   A pie has one series and keys its wedges rather than its series, so its legend names the
    ///   categories - and is the only text a pie draws when it has no data labels.
    /// </summary>
    [Fact]
    public void APieLegendNamesItsCategories()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 2.0, 3.0);
        chart.Legend.Docking = DockingType.Left;

        var page = Drawn.Page(chart);

        ShownText.On(page).Should().Equal("A", "B", "C");
        var runs = ShownText.RunsOn(page);
        runs.Select(run => run.X).Distinct().Should().HaveCount(1, "a legend docked to the side is one column");
        runs.Select(run => run.Y).Should().BeInDescendingOrder();
    }

    // ----- helpers -----

    /// <summary>The size the chart's default font is set at, which a legend inherits.</summary>
    private const double DefaultFontSize = 12;

    /// <summary>
    ///   A column chart of two series with names, and fills of their own so that each one's swatch
    ///   can be told apart. Tall enough columns that no column is mistaken for a swatch.
    /// </summary>
    private static Chart TwoNamedSeries(ChartType type)
    {
        var chart = Charts.OfSeries(type, new[] { 20.0, 40.0, 30.0 }, new[] { 25.0, 35.0, 45.0 });
        chart.SeriesCollection[0].Name = "North";
        chart.SeriesCollection[0].FillFormat.Color = NorthColour;
        chart.SeriesCollection[1].Name = "South";
        chart.SeriesCollection[1].FillFormat.Color = SouthColour;
        return chart;
    }

    private static Chart Bordered(DockingType docking)
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.Legend.Docking = docking;
        chart.Legend.LineFormat.Visible = true;
        chart.Legend.LineFormat.Color = BorderColour;
        return chart;
    }

    private static PaintedRectangles.Rectangle BorderOn(PdfPage page)
    {
        var border = PaintedRectangles.On(page)
            .Single(r => r.Stroked && r.Colour == PaintedRectangles.ColourOf(BorderColour));

        // The border encloses the whole legend, so it has to be the right one.
        var north = RunReading(page, "North");
        north.X.Should().BeInRange(border.X, border.Right);
        north.Y.Should().BeInRange(border.Y, border.Top);
        return border;
    }

    /// <summary>The columns themselves: filled, and far taller than any swatch.</summary>
    private static IReadOnlyList<PaintedRectangles.Rectangle> Columns(PdfPage page) =>
        PaintedRectangles.FilledOn(page).Where(r => r.Height > 3 * Swatch).ToList();

    /// <summary>The legend's swatches: filled, and no bigger than the marker area can make them.</summary>
    private static IReadOnlyList<PaintedRectangles.Rectangle> Swatches(PdfPage page, double maxWidth = Swatch + 0.01) =>
        PaintedRectangles.FilledOn(page).Where(r => r.Width <= maxWidth && r.Height <= Swatch + 0.01).ToList();

    private static IEnumerable<ShownText.Run> TickLabels(PdfPage page) =>
        ShownText.RunsOn(page).Where(run => IsNumber(run.Text));

    private static IEnumerable<ShownText.Run> CategoryLabels(PdfPage page) =>
        ShownText.RunsOn(page).Where(run => run.Text is "A" or "B" or "C");

    private static bool IsAxisLabel(string text) => text is "A" or "B" or "C" || IsNumber(text);

    private static bool IsNumber(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static ShownText.Run RunReading(PdfPage page, string text) =>
        ShownText.RunsOn(page).Single(run => run.Text == text);

    private static double Length(StrokedLines.Line line) =>
        Math.Sqrt((line.X2 - line.X1) * (line.X2 - line.X1) + (line.Y2 - line.Y1) * (line.Y2 - line.Y1));

    /// <summary>
    ///   How wide the text is in the legend's font, measured on a page of its own - measuring on
    ///   the page under test would add a content stream to it. Arial is what the charting defaults
    ///   ask for, and the pinned resolver answers it with Liberation Sans, as it does for the chart.
    /// </summary>
    private static double WidthOf(string text, double size) => Measure(text, size).Width;

    private static double HeightOf(double size) => Measure("North", size).Height;

    private static XSize Measure(string text, double size)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(text, new XFont("Arial", size));
    }
}
