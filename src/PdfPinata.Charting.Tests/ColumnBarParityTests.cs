using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using AwesomeAssertions.Equivalency;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
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
///
///   The tests from <see cref="EveryColumnIsABarTurnedOnItsSide"/> on hold the two to more than
///   agreement: to being geometric transposes of one another. Each measures what it reads against
///   the plot area - a column's place across the chart as a fraction of the plot area's width, a
///   bar's as a fraction of its height - so that the two charts, laid out in plot areas of
///   different shapes, can be compared number for number with the axes swapped. The gridlines, the
///   zero line and the data labels are held to the same, so the plot area, its gridlines and its
///   labels can be drawn from one orientation-aware renderer each.
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
    ///   The data every transposition test below draws both ways: a series each, a null being a
    ///   blank, and an explicit scale where one is given. Between them they cover values above and
    ///   below zero, several series, blanks, values off an explicit scale, a scale wholly below
    ///   zero, and a scale worked out from the data.
    /// </summary>
    public static TheoryData<bool, double?[][], double?, double?> Plots => new()
    {
        { false, [[1.0, 5.0, 3.0]], null, null },
        { false, [[2.0, -3.0, 4.0]], null, null },
        { false, [[1.0, 2.0], [3.0, -1.0], [2.0, 2.0]], null, null },
        { false, [[1.0, null, 3.0], [2.0, 2.0, null]], null, null },
        { false, [[1.0, 5.0, 3.0]], 0.0, 4.0 },
        { false, [[2.0, -3.0, 4.0]], -5.0, 5.0 },
        { false, [[-2.0, -5.0, -8.0], [-1.0, -12.0, -3.0]], -10.0, -1.0 },
        { false, [[0.5, 1.5], [1.0, 0.25]], 0.0, 2.0 },
        { true, [[1.0, 5.0, 3.0]], null, null },
        { true, [[2.0, -3.0, 4.0], [1.0, -1.0, 2.0]], null, null },
        { true, [[1.0, 2.0], [3.0, -1.0], [2.0, 2.0]], null, null },
        { true, [[1.0, null, 3.0], [2.0, 2.0, null]], null, null },
        { true, [[1.0, 2.0, 3.0], [2.0, 2.0, 2.0]], 0.0, 4.0 },
        { true, [[2.0, -3.0, 4.0], [1.0, -1.0, 2.0]], -5.0, 7.0 },
        { true, [[0.5, 1.5], [1.0, 0.25]], 0.0, 2.0 }
    };

    /// <summary>
    ///   Every rectangle a column chart paints - each column's body, and the border around it - is
    ///   the rectangle the bar chart paints for the same point, with the axes swapped: a column's
    ///   place along the category axis, measured from the left of the plot area, is the bar's
    ///   measured from the foot of it, and likewise for the value axis. Both charts paint them in
    ///   the same order, leave out the same ones - a blank, a value off the scale - and outline the
    ///   same ones, including a point with a border of its own and leaving out one whose border is
    ///   hidden.
    /// </summary>
    [Theory]
    [MemberData(nameof(Plots))]
    public void EveryColumnIsABarTurnedOnItsSide(bool stacked, double?[][] series, double? minimum, double? maximum)
    {
        var column = Plotted(Build(stacked ? ChartType.ColumnStacked2D : ChartType.Column2D, series, minimum, maximum), isBar: false);
        var bar = Plotted(Build(stacked ? ChartType.BarStacked2D : ChartType.Bar2D, series, minimum, maximum), isBar: true);

        column.Should().NotBeEmpty();
        bar.Should().BeEquivalentTo(column, Transposed);
    }

    /// <summary>
    ///   The gridlines of both axes, major and minor, and the zero line the plot area draws when
    ///   its scale spans zero, are the same lines on a column chart and a bar chart with the axes
    ///   swapped. Each kind is stroked in a colour of its own, so a line of one kind cannot stand in
    ///   for a line of another; the zero line is stroked with the value axis's gridlines, and
    ///   counted among them.
    /// </summary>
    [Theory]
    [InlineData(false, null, null)]
    [InlineData(false, -6.0, 6.0)]
    [InlineData(false, 0.0, 4.5)]
    [InlineData(true, null, null)]
    [InlineData(true, -6.0, 8.0)]
    public void EveryGridlineOfAColumnChartIsAGridlineOfABarChartTurnedOnItsSide(bool stacked, double? minimum, double? maximum)
    {
        double?[][] series = [[2.0, -3.0, 4.0], [1.0, -1.0, 2.0]];
        var column = Gridlines(Build(stacked ? ChartType.ColumnStacked2D : ChartType.Column2D, series, minimum, maximum), isBar: false);
        var bar = Gridlines(Build(stacked ? ChartType.BarStacked2D : ChartType.Bar2D, series, minimum, maximum), isBar: true);

        column.Select(line => line.Colour).Distinct().Should().HaveCount(GridColours.Length, "every kind of gridline is drawn");
        bar.Should().BeEquivalentTo(column, Transposed);
    }

    /// <summary>
    ///   Where each position puts a data label, stated once for both orientations: centred on its
    ///   column or bar across the value axis, and along it flush with the end the value reaches
    ///   (<c>InsideEnd</c>), with the base (<c>InsideBase</c>), in the middle (<c>Center</c>) or
    ///   just beyond the end (<c>OutsideEnd</c>). The end a value reaches is its top on a column
    ///   and its right on a bar when the value is positive, and the other one when it is negative.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D, DataLabelPosition.Center)]
    [InlineData(ChartType.Column2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.Column2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.Column2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.Center)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.Center)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.Center)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.OutsideEnd)]
    public void EachPositionPutsADataLabelInTheSamePlaceWhicheverWayTheChartRuns(ChartType type, DataLabelPosition position)
    {
        foreach (var scale in new (double?, double?)[] { (null, null), (-5.0, 5.0) })
        {
            var chart = Build(type, [[2.25, -1.75, 3.5], [1.5, -0.5, 1.25]], scale.Item1, scale.Item2);
            LabelledAt(chart, position);

            foreach (var (label, value, rect) in LabelsAndTheirColumns(chart))
                ShouldBePlaced(label, rect, position, positive: value >= 0, IsBar(type));
        }
    }

    /// <summary>
    ///   A zero is a column or a bar of no length, so which side of it the label goes is all a
    ///   position has left to say, and it goes where a positive value's would: a zero is not
    ///   negative, which is how the value axis and the plot area already count it. A bar chart used
    ///   to agree only at the base, and put the label of a zero at its end, inside or out, where a
    ///   negative value's would go - below the line on a column chart's terms, and to the left of
    ///   the axis on its own.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.Column2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.Column2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.ColumnStacked2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.InsideBase)]
    [InlineData(ChartType.Bar2D, DataLabelPosition.OutsideEnd)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.InsideEnd)]
    [InlineData(ChartType.BarStacked2D, DataLabelPosition.OutsideEnd)]
    public void AZerosLabelGoesWhereAPositiveValuesWould(ChartType type, DataLabelPosition position)
    {
        var chart = Build(type, [[0.0, 2.25]], -4.0, 4.0);
        LabelledAt(chart, position);

        var (label, _, rect) = LabelsAndTheirColumns(chart)[0];
        ShouldBePlaced(label, rect, position, positive: true, IsBar(type));
    }

    /// <summary>
    ///   A clustered column or bar whose value is above zero and below a minimum the caller set
    ///   runs, from the minimum, backwards: today both orientations make a rectangle of negative
    ///   size of it, and <c>XRect</c> refuses one, so the chart cannot be drawn at all.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AClusteredValueBetweenZeroAndAPositiveMinimumCannotBeDrawn(ChartType type)
    {
        var chart = Build(type, [[1.0, 3.0]], 2.0, 5.0);

        var draw = () => Drawn.Page(chart);

        draw.Should().Throw<ArgumentException>();
    }

    /// <summary>
    ///   A scale whose minimum is above its maximum leaves nothing to plot against. A bar chart
    ///   works nothing out and draws nothing; a stacked column chart works its columns out anyway,
    ///   against a matrix that was never scaled, and throws for a rectangle of negative size.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D, false)]
    [InlineData(ChartType.Bar2D, false)]
    [InlineData(ChartType.ColumnStacked2D, true)]
    [InlineData(ChartType.BarStacked2D, false)]
    public void AScaleTurnedUpsideDownDrawsNothing(ChartType type, bool throws)
    {
        var chart = Build(type, [[3.0, 5.0]], 6.0, 2.0);

        var draw = () => Drawn.Page(chart);

        if (throws)
            draw.Should().Throw<ArgumentException>();
        else
            Plotted(chart, IsBar(type)).Should().BeEmpty();
    }

    /// <summary>
    ///   A combination chart draws its columns through the column chart's own plot area, gridline
    ///   and data label renderers, so its columns, gridlines and labels are those of a chart of the
    ///   same columns alone: measured against the plot area, the same rectangles and the same
    ///   lines, and each label placed on its column as the column chart places it. The line
    ///   series beside them is drawn as a path, which none of these readers sees.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    public void ACombinationDrawsItsColumnsAsAColumnChartDoes(ChartType type)
    {
        double?[][] columns = [[2.25, -1.75, 3.5], [1.5, -0.5, 1.25]];

        var alone = Build(type, columns, -4.0, 6.0);
        LabelledAt(alone, DataLabelPosition.OutsideEnd);

        // The line is added after Build has given the columns their borders, so that the same
        // columns carry the same borders on both charts.
        var combined = Build(ChartType.Line, columns, -4.0, 6.0);
        combined.SeriesCollection.AddSeries().Add(1.0, 2.0, 1.0);
        combined.SeriesCollection[0].ChartType = type;
        combined.SeriesCollection[1].ChartType = type;
        LabelledAt(combined, DataLabelPosition.OutsideEnd);

        Plotted(combined, isBar: false).Should().BeEquivalentTo(Plotted(alone, isBar: false), Transposed);
        Gridlines(combined, isBar: false).Should().BeEquivalentTo(Gridlines(alone, isBar: false), Transposed);

        var labelsAlone = LabelsAndTheirColumns(alone);
        var labelsCombined = LabelsAndTheirColumns(combined);
        labelsCombined.Should().HaveCount(labelsAlone.Count);
        for (var idx = 0; idx < labelsAlone.Count; idx++)
        {
            var (a, _, aRect) = labelsAlone[idx];
            var (c, _, cRect) = labelsCombined[idx];
            c.Text.Should().Be(a.Text);
            (c.X - cRect.X).Should().BeApproximately(a.X - aRect.X, Tolerance);
            (c.Y - cRect.Y).Should().BeApproximately(a.Y - aRect.Y, Tolerance);
        }
    }

    /// <summary>
    ///   A chart of the given type plotting each array as a series, a null as a blank, with one
    ///   category per value of the first series. The plot area is filled, so that its rectangle can
    ///   be read back as the first thing filled; the first point that is not a blank is given a
    ///   border of its own, and the last one a border that is hidden; and each kind of gridline is
    ///   asked for, in a colour of its own.
    /// </summary>
    private static Chart Build(ChartType type, double?[][] series, double? minimum, double? maximum)
    {
        var chart = Charts.Empty(type);

        var categories = chart.XValues.AddXSeries();
        for (var idx = 0; idx < series[0].Length; idx++)
            categories.Add(((char)('A' + idx)).ToString());

        foreach (var values in series)
        {
            var added = chart.SeriesCollection.AddSeries();
            foreach (var value in values)
            {
                if (value is { } plotted)
                    added.Add(plotted);
                else
                    added.AddBlank();
            }
        }

        var points = chart.SeriesCollection.Cast<Series>()
            .SelectMany(s => s.Elements.Cast<Point>())
            .Where(point => point != null)
            .ToList();
        points[0].LineFormat.Visible = true;
        points[0].LineFormat.Width = 2;
        points[0].LineFormat.Color = XColors.Blue;
        points[^1].LineFormat.Visible = false;
        points[^1].LineFormat.Width = 3;

        if (minimum is { } min)
            chart.YAxis.MinimumScale = min;
        if (maximum is { } max)
            chart.YAxis.MaximumScale = max;

        chart.PlotArea.FillFormat.Color = WallColour;

        Grid(chart.XAxis.MajorGridlines, GridColours[0]);
        Grid(chart.XAxis.MinorGridlines, GridColours[1]);
        Grid(chart.YAxis.MajorGridlines, GridColours[2]);
        Grid(chart.YAxis.MinorGridlines, GridColours[3]);
        return chart;

        static void Grid(Gridlines gridlines, XColor colour)
        {
            gridlines.LineFormat.Visible = true;
            gridlines.LineFormat.Width = 0.25;
            gridlines.LineFormat.Color = colour;
        }
    }

    /// <summary>
    ///   Compares two lists of placed rectangles or lines member by member, in order, each
    ///   fraction to within <see cref="Fraction"/>.
    /// </summary>
    private static EquivalencyOptions<T> Transposed<T>(EquivalencyOptions<T> options) => options
        .ComparingByMembers<T>()
        .WithStrictOrdering()
        .Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, Fraction))
        .WhenTypeIs<double>();

    private static bool IsBar(ChartType type) => type is ChartType.Bar2D or ChartType.BarStacked2D;

    /// <summary>
    ///   A rectangle measured against the plot area and turned so that the category axis is always
    ///   first: where along the category axis it starts and how far it reaches, as fractions of the
    ///   plot area's extent that way, and the same along the value axis.
    /// </summary>
    internal readonly record struct Placed(
        double Category, double CategoryExtent, double Value, double ValueExtent, bool Filled, bool Stroked, string Colour);

    /// <summary>
    ///   A gridline measured and turned as <see cref="Placed"/> is, its two ends in order.
    /// </summary>
    internal readonly record struct GridLine(double Category1, double Value1, double Category2, double Value2, string Colour);

    /// <summary>
    ///   The plot area as the wall behind it was filled: the first rectangle filled in its colour.
    /// </summary>
    private static PaintedRectangles.Rectangle Wall(PdfPage page) =>
        PaintedRectangles.FilledOn(page).First(rectangle => rectangle.Colour == PaintedRectangles.ColourOf(WallColour));

    /// <summary>
    ///   Every rectangle the chart paints but the wall, in the order it paints them, placed against
    ///   the plot area.
    /// </summary>
    private static Placed[] Plotted(Chart chart, bool isBar)
    {
        var page = Drawn.Page(chart);
        var painted = PaintedRectangles.On(page);
        var wallColour = PaintedRectangles.ColourOf(WallColour);
        var wall = painted.FirstOrDefault(rectangle => rectangle.Filled && rectangle.Colour == wallColour);

        return [.. painted.Where(rectangle => !(rectangle.Filled && rectangle.Colour == wallColour)).Select(r => isBar
            ? new Placed((r.Y - wall.Y) / wall.Height, r.Height / wall.Height, (r.X - wall.X) / wall.Width, r.Width / wall.Width, r.Filled, r.Stroked, r.Colour)
            : new Placed((r.X - wall.X) / wall.Width, r.Width / wall.Width, (r.Y - wall.Y) / wall.Height, r.Height / wall.Height, r.Filled, r.Stroked, r.Colour))];
    }

    /// <summary>
    ///   Every gridline the chart strokes, and the zero line with them, placed against the plot
    ///   area and sorted, so that two charts drawing the same lines in another order still agree.
    /// </summary>
    private static GridLine[] Gridlines(Chart chart, bool isBar)
    {
        var page = Drawn.Page(chart);
        var wall = Wall(page);
        var colours = GridColours.Select(PaintedRectangles.ColourOf).ToHashSet();

        return [.. StrokedLines.Of(page)
            .Where(line => colours.Contains(line.Colour))
            .Select(line =>
            {
                var (c1, v1) = Place(line.X1, line.Y1);
                var (c2, v2) = Place(line.X2, line.Y2);
                return (c1, v1).CompareTo((c2, v2)) <= 0
                    ? new GridLine(c1, v1, c2, v2, line.Colour)
                    : new GridLine(c2, v2, c1, v1, line.Colour);
            })
            .OrderBy(line => line.Colour, StringComparer.Ordinal)
            .ThenBy(line => Math.Round(line.Category1, 4))
            .ThenBy(line => Math.Round(line.Value1, 4))];

        (double Category, double Value) Place(double x, double y)
        {
            var across = (x - wall.X) / wall.Width;
            var up = (y - wall.Y) / wall.Height;
            return isBar ? (up, across) : (across, up);
        }
    }

    /// <summary>
    ///   Labels every point with its value, at the position given, in a font whose size this class
    ///   knows so that it can measure the labels the renderer measured.
    /// </summary>
    private static void LabelledAt(Chart chart, DataLabelPosition position)
    {
        chart.HasDataLabel = true;
        chart.DataLabel.Type = DataLabelType.Value;
        chart.DataLabel.Format = LabelFormat;
        chart.DataLabel.Position = position;
        chart.DataLabel.Font.Name = LabelFontName;
        chart.DataLabel.Font.Size = LabelFontSize;
    }

    /// <summary>
    ///   Each data label the chart writes, with the value it is for and the column or bar it
    ///   labels: the labels in the order they are written, which is series by series as the
    ///   columns are filled. Every point here is drawn, so the two lists pair off.
    /// </summary>
    private static List<(ShownText.Run Label, double Value, PaintedRectangles.Rectangle Column)> LabelsAndTheirColumns(Chart chart)
    {
        var page = Drawn.Page(chart);
        var wallColour = PaintedRectangles.ColourOf(WallColour);
        var columns = PaintedRectangles.FilledOn(page).Where(rectangle => rectangle.Colour != wallColour).ToList();
        var labels = ShownText.RunsOn(page)
            .Where(run => Regex.IsMatch(run.Text, @"^-?\d+\.\d\d$"))
            .ToList();

        labels.Should().HaveCount(columns.Count, "every point drawn is labelled");
        return [.. labels.Select((label, idx) => (label, double.Parse(label.Text, CultureInfo.InvariantCulture), columns[idx]))];
    }

    /// <summary>
    ///   Asserts that a label lies where its position puts it against its column or bar. The
    ///   label's box is recovered from where its text was shown: it is exactly as wide as its text,
    ///   so the text starts at its left edge, and it is centred on the line the renderer centres it
    ///   on, three eighths of the ascent above the baseline (<c>TextOrigin</c>).
    /// </summary>
    private static void ShouldBePlaced(ShownText.Run label, PaintedRectangles.Rectangle rect, DataLabelPosition position,
        bool positive, bool isBar)
    {
        var size = Measure.MeasureString(label.Text, LabelFont);
        var ascent = LabelFont.GetHeight() * LabelFont.CellAscent / LabelFont.CellSpace;
        var bottom = label.Y + ascent * 3 / 8 - size.Height / 2;

        // Across the value axis, then along it in the direction the values grow.
        var (across, acrossSize, along, alongSize) = isBar
            ? (bottom, size.Height, label.X, size.Width)
            : (label.X, size.Width, bottom, size.Height);
        var (from, extent, fromAlong, extentAlong) = isBar
            ? (rect.Y, rect.Height, rect.X, rect.Width)
            : (rect.X, rect.Width, rect.Y, rect.Height);

        (across + acrossSize / 2).Should().BeApproximately(from + extent / 2, Tolerance,
            $"{label.Text} is centred on its column across the value axis");

        var expected = position switch
        {
            DataLabelPosition.Center => fromAlong + extentAlong / 2 - alongSize / 2,
            DataLabelPosition.InsideEnd => positive ? fromAlong + extentAlong - alongSize : fromAlong,
            DataLabelPosition.InsideBase => positive ? fromAlong : fromAlong + extentAlong - alongSize,
            DataLabelPosition.OutsideEnd => positive ? fromAlong + extentAlong : fromAlong - alongSize,
            _ => double.NaN
        };
        along.Should().BeApproximately(expected, Tolerance, $"{label.Text} is placed {position}");
    }

    private const string LabelFontName = "Arial";

    private const double LabelFontSize = 10;

    private static readonly XFont LabelFont = new(LabelFontName, LabelFontSize);

    private static readonly XGraphics Measure =
        XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards);

    private static readonly XColor WallColour = XColors.LightYellow;

    private static readonly XColor[] GridColours = [XColors.Orange, XColors.Purple, XColors.Teal, XColors.Olive];

    /// <summary>
    ///   How near two fractions of the plot area must be. The content stream rounds what it writes,
    ///   and a hundred-thousandth of a plot area a few hundred points across is well below that.
    /// </summary>
    private const double Fraction = 1e-4;

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
