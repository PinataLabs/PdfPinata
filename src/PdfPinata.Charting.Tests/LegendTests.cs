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
///   name - and adds them up: side by side for a legend docked above or below the chart, in as
///   many rows as it takes to fit across it, and one under another for a legend docked beside it,
///   with padding round the outside that is doubled when the legend has a border to keep clear of.
///   <c>ChartRenderer.LayoutLegend</c> then takes that much
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
    ///   The same in a combination chart, for the stacked columns among themselves: the series
    ///   drawn beside the stack keeps its place in the list.
    /// </summary>
    [Fact]
    public void ACombinationLegendListsItsStackedColumnsInStackOrder()
    {
        var chart = Charts.OfSeries(ChartType.Line,
            [20.0, 40.0, 30.0], [25.0, 35.0, 45.0], [10.0, 10.0, 10.0]);
        chart.SeriesCollection[0].Name = "North";
        chart.SeriesCollection[0].ChartType = ChartType.ColumnStacked2D;
        chart.SeriesCollection[1].Name = "South";
        chart.SeriesCollection[1].ChartType = ChartType.ColumnStacked2D;
        chart.SeriesCollection[2].Name = "Target";
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);

        RunReading(page, "South").Y.Should().BeGreaterThan(RunReading(page, "North").Y);
        RunReading(page, "North").Y.Should().BeGreaterThan(RunReading(page, "Target").Y);
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
        var firstSwatch = Swatches(bordered)[0];

        (firstSwatch.X - border.X).Should().BeApproximately(Padding, 0.01);
        (border.Top - firstSwatch.Top).Should().BeGreaterThanOrEqualTo(Padding);

        var plain = TwoNamedSeries(ChartType.Column2D);
        plain.Legend.Docking = DockingType.Right;
        var unbordered = Drawn.Page(plain);

        (RunReading(unbordered, "North").X - RunReading(bordered, "North").X).Should().BeApproximately(Padding, 0.01,
            "the legend is docked by its right edge and grows leftwards by the extra padding");
    }

    /// <summary>
    ///   A border that is not visible is no border, so it takes no room either: the legend is laid
    ///   out exactly as one with no line format at all, rather than padded for a line nobody sees.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AHiddenLegendBorderIsNotPaddedFor(ChartType type)
    {
        var plain = TwoNamedSeries(type);
        plain.Legend.Docking = DockingType.Right;

        var hidden = TwoNamedSeries(type);
        hidden.Legend.Docking = DockingType.Right;
        hidden.Legend.LineFormat.Color = BorderColour;
        hidden.Legend.LineFormat.Width = 2;

        var hiddenPage = Drawn.Page(hidden);
        PaintedPaths.StrokedIn(hiddenPage, PaintedRectangles.ColourOf(BorderColour)).Should().BeEmpty();
        RunReading(hiddenPage, "North").X.Should().BeApproximately(RunReading(Drawn.Page(plain), "North").X, 0.01);
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

        var marker = PaintedPaths.FilledIn(page, blue)[0];
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

        var marker = PaintedPaths.FilledIn(page, blue)[0];
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

    /// <summary>
    ///   A blank category is an entry with no text, as it is a tick label with no text on a column
    ///   chart's axis - the wedge is still keyed with its swatch, and the entries either side keep
    ///   their names. <see cref="XSeries.AddBlank"/> stores a null, whose value the legend used to
    ///   read.
    /// </summary>
    [Fact]
    public void APieLegendKeysABlankCategoryWithNoText()
    {
        var chart = Charts.Empty(ChartType.Pie2D);
        var categories = chart.XValues.AddXSeries();
        categories.Add("A");
        categories.AddBlank();
        categories.Add("C");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);
        chart.Legend.Docking = DockingType.Left;

        var page = Drawn.Page(chart);

        ShownText.On(page).Should().Equal("A", "C");
        Swatches(page).Should().HaveCount(3, "every wedge is keyed, the blank one too");
    }

    /// <summary>
    ///   A chart whose category collection holds no series has no categories, and a pie legend
    ///   numbers its entries then, as it does for a chart that never asked for the collection at all.
    ///   It used to read the first series without asking whether there was one.
    /// </summary>
    [Fact]
    public void APieLegendWithAnEmptyCategoryCollectionNumbersItsEntries()
    {
        var chart = Charts.Empty(ChartType.Pie2D);
        _ = chart.XValues;
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);
        chart.Legend.Docking = DockingType.Left;

        ShownText.On(Drawn.Page(chart)).Should().Equal("1", "2", "3");
    }

    // ----- a legend too wide for the chart (empira/PDFsharp#306) -----

    /// <summary>
    ///   Above or below the chart the entries run side by side until the next one would not fit
    ///   across the chart, and then start a new row under the last - where they used to run on in
    ///   a single row centred on the chart, off both edges of it and of the page it was on.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Pie2D, DockingType.Bottom)]
    [InlineData(ChartType.Pie2D, DockingType.Top)]
    [InlineData(ChartType.Column2D, DockingType.Bottom)]
    [InlineData(ChartType.Bar2D, DockingType.Bottom)]
    public void ALegendTooWideForTheChartWrapsItsEntriesOntoMoreRows(ChartType type, DockingType docking)
    {
        var chart = NamedEntries(type, TwelveRegions);
        chart.Legend.Docking = docking;

        var page = Drawn.Page(chart);
        var runs = ShownText.RunsOn(page).Where(run => TwelveRegions.Contains(run.Text)).ToList();

        runs.Select(run => run.Text).Should().BeEquivalentTo(TwelveRegions);
        runs.Should().OnlyContain(run => run.X >= 0 && run.X + WidthOf(run.Text, DefaultFontSize) <= Drawn.DefaultWidth,
            "every entry is drawn inside the chart");
        runs.Select(run => Math.Round(run.Y, 2)).Distinct().Should().HaveCountGreaterThan(1,
            "twelve entries this wide do not fit in one row across the chart");

        // Read top to bottom and left to right, the entries come in the order they were given.
        runs.OrderByDescending(run => Math.Round(run.Y, 2)).ThenBy(run => run.X).Select(run => run.Text)
            .Should().Equal(TwelveRegions);
    }

    /// <summary>
    ///   The rows of a wrapped legend are each centred across the chart, as a single row always was,
    ///   and they stack with the entry spacing between one row and the next.
    /// </summary>
    [Fact]
    public void TheRowsOfAWrappedLegendAreCentredAndSpacedAsEntriesAre()
    {
        var chart = NamedEntries(ChartType.Pie2D, TwelveRegions);
        chart.Legend.Docking = DockingType.Bottom;

        var page = Drawn.Page(chart);
        var rows = ShownText.RunsOn(page).Where(run => TwelveRegions.Contains(run.Text))
            .GroupBy(run => Math.Round(run.Y, 2)).OrderByDescending(row => row.Key).ToList();

        rows.Should().HaveCountGreaterThan(1);
        for (var idx = 1; idx < rows.Count; idx++)
            (rows[idx - 1].Key - rows[idx].Key).Should().BeApproximately(HeightOf(DefaultFontSize) + EntrySpacing, 0.01);

        foreach (var row in rows)
        {
            var left = row.Min(run => run.X) - Swatch - MarkerToText;
            var last = row.OrderBy(run => run.X).Last();
            var right = last.X + WidthOf(last.Text, DefaultFontSize);
            ((left + right) / 2).Should().BeApproximately(Drawn.DefaultWidth / 2, 0.01);
        }
    }

    /// <summary>
    ///   An entry that would be wider than the chart on its own is word wrapped inside its entry,
    ///   with its swatch against the first line, rather than pushing the legend off both sides.
    /// </summary>
    [Theory]
    [InlineData(DockingType.Bottom)]
    [InlineData(DockingType.Right)]
    public void AnEntryWiderThanTheChartIsWordWrapped(DockingType docking)
    {
        const string longName = "The quarterly revenue of every northern region taken together, before tax";
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.SeriesCollection[0].Name = longName;
        chart.Legend.Docking = docking;

        var page = Drawn.Page(chart);
        var lines = ShownText.RunsOn(page).Where(run => run.Text.Length > 1 && longName.Contains(run.Text)).ToList();

        lines.Should().HaveCountGreaterThan(1);
        string.Join(" ", lines.Select(line => line.Text)).Should().Be(longName);
        lines.Should().OnlyContain(line => line.X >= 0 && line.X + WidthOf(line.Text, DefaultFontSize) <= Drawn.DefaultWidth);
        lines.Select(line => Math.Round(line.X, 2)).Distinct().Should().HaveCount(1, "the lines of one entry start together");
        for (var idx = 1; idx < lines.Count; idx++)
            (lines[idx - 1].Y - lines[idx].Y).Should().BeApproximately(HeightOf(DefaultFontSize), 0.01);

        var swatch = Swatches(page).Single(s => s.Colour == PaintedRectangles.ColourOf(NorthColour));
        swatch.Right.Should().BeApproximately(lines[0].X - MarkerToText, 0.01);
        swatch.CentreY.Should().BeInRange(lines[0].Y, lines[0].Y + HeightOf(DefaultFontSize) / 2,
            "the swatch keys the first line, not the middle of the entry");
    }

    /// <summary>
    ///   A line break in a series name starts a new line of its entry, where it used to be dropped
    ///   and the two halves run together.
    /// </summary>
    [Fact]
    public void ALineBreakInASeriesNameStartsANewLineOfItsEntry()
    {
        var chart = TwoNamedSeries(ChartType.Column2D);
        chart.SeriesCollection[0].Name = "North\nand East";
        chart.Legend.Docking = DockingType.Right;

        var page = Drawn.Page(chart);
        var first = RunReading(page, "North");
        var second = RunReading(page, "and East");

        second.X.Should().BeApproximately(first.X, 0.01);
        (first.Y - second.Y).Should().BeApproximately(HeightOf(DefaultFontSize), 0.01);
    }

    // ----- helpers -----

    private static readonly string[] TwelveRegions = [..Enumerable.Range(1, 12).Select(n => $"Region {n}")];

    /// <summary>
    ///   A chart whose legend has one entry per name: the categories of a pie, the series of
    ///   anything else.
    /// </summary>
    private static Chart NamedEntries(ChartType type, IReadOnlyList<string> names)
    {
        var chart = Charts.Empty(type);
        var categories = chart.XValues.AddXSeries();
        if (type == ChartType.Pie2D)
        {
            var series = chart.SeriesCollection.AddSeries();
            foreach (var name in names)
            {
                categories.Add(name);
                series.Add(1.0);
            }
        }
        else
        {
            categories.Add("A");
            foreach (var name in names)
            {
                var series = chart.SeriesCollection.AddSeries();
                series.Name = name;
                series.Add(10.0);
            }
        }
        return chart;
    }

    /// <summary>The size the chart's default font is set at, which a legend inherits.</summary>
    private const double DefaultFontSize = 12;

    /// <summary>
    ///   A column chart of two series with names, and fills of their own so that each one's swatch
    ///   can be told apart. Tall enough columns that no column is mistaken for a swatch.
    /// </summary>
    private static Chart TwoNamedSeries(ChartType type)
    {
        var chart = Charts.OfSeries(type, [20.0, 40.0, 30.0], [25.0, 35.0, 45.0]);
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
        [..PaintedRectangles.FilledOn(page).Where(r => r.Height > 3 * Swatch)];

    /// <summary>The legend's swatches: filled, and no bigger than the marker area can make them.</summary>
    private static IReadOnlyList<PaintedRectangles.Rectangle> Swatches(PdfPage page, double maxWidth = Swatch + 0.01) =>
        [..PaintedRectangles.FilledOn(page).Where(r => r.Width <= maxWidth && r.Height <= Swatch + 0.01)];

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
