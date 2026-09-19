using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The axis the categories are written along - across the bottom of a column chart, up the side
///   of a bar chart.
/// </summary>
/// <remarks>
///   Two renderers, <c>HorizontalXAxisRenderer</c> and <c>VerticalXAxisRenderer</c>, each with a
///   Draw of its own. They are near-copies of one another, and the interesting part of these tests
///   is where the copies have drifted: the horizontal one skips a category with no value and stops
///   at the end of the series, and the vertical one does neither. Both facts are pinned down
///   below, the second as the exception it currently throws.
/// </remarks>
public class CategoryAxisTests
{
    [Fact]
    public void AColumnChartWritesItsCategoriesLeftToRightInOrder()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0));

        var categories = ShownText.RunsOn(page).Where(run => run.Text.Length == 1).ToList();

        categories.Select(run => run.Text).Should().Equal("A", "B", "C");
        categories.Should().BeInAscendingOrder(run => run.X);
    }

    [Fact]
    public void AColumnChartPutsEachCategoryUnderItsOwnColumn()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0));

        var categories = ShownText.RunsOn(page).Where(run => run.Text.Length == 1).ToList();
        var columns = PaintedRectangles.FilledOn(page);

        categories.Should().HaveCount(columns.Count);
        for (var idx = 0; idx < columns.Count; idx++)
            categories[idx].X.Should().BeApproximately(columns[idx].CentreX, LabelTolerance);
    }

    [Fact]
    public void ABarChartWritesItsCategoriesUpTheSideInReverse()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0));

        var categories = ShownText.RunsOn(page).Where(run => run.Text.Length == 1).ToList();

        // The last category is drawn first because it belongs at the top: a bar chart's first
        // category is the bottom bar.
        categories.Select(run => run.Text).Should().Equal("C", "B", "A");
        categories.Should().BeInDescendingOrder(run => run.Y);
    }

    [Fact]
    public void ABarChartPutsEachCategoryBesideItsOwnBar()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0));

        var categories = ShownText.RunsOn(page).Where(run => run.Text.Length == 1).ToList();
        var bars = PaintedRectangles.FilledOn(page);

        // Bars come out in category order and labels in reverse, so the first label belongs to
        // the last bar. A label is placed by its baseline rather than by the middle of its
        // glyphs, so what is asserted is that it falls within the bar's own band rather than at
        // any particular point in it.
        categories.Should().HaveCount(bars.Count);
        for (var idx = 0; idx < bars.Count; idx++)
        {
            categories[bars.Count - 1 - idx].Y
                .Should().BeInRange(bars[idx].Y, bars[idx].Top);
        }
    }

    /// <summary>
    ///   A chart given no categories of its own is labelled 1, 2, 3 - the axis renderer fills them
    ///   in from the scale rather than leaving the axis blank.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AChartWithNoCategoriesIsLabelledByPosition(ChartType type)
    {
        var chart = Charts.Empty(type);
        chart.SeriesCollection.AddSeries().Add(1.0, 5.0, 3.0);

        var shown = ShownText.On(Drawn.Page(chart));

        shown.Should().Contain("1").And.Contain("2").And.Contain("3");
    }

    [Fact]
    public void TurningOffEveryTickMarkLeavesNothingStrokedOnTheAxes()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);

        StrokedLines.Of(Drawn.Page(chart)).Should().NotBeEmpty("the default axes draw tick marks");

        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        // No gridlines, no axis line format and now no tick marks: an axis with nothing to stroke
        // strokes nothing, rather than drawing a zero-length segment for each tick.
        StrokedLines.Of(Drawn.Page(chart)).Should().BeEmpty();
    }

    /// <summary>
    ///   The tick-mark pens repair: every axis's tick marks are stroked with the pens
    ///   <c>AxisRenderer.InitAxisLineFormat</c> already computes for it, which are never null, so
    ///   the category axis's tick marks are visible without asking for anything - exactly as the
    ///   value axis's always were. Before the axis renderers were merged, the category axis
    ///   stroked its ticks with a pen built from <see cref="Axis.LineFormat"/> instead, which holds
    ///   no pen until a caller sets one, so a default chart had tick marks up the side and none
    ///   along the bottom.
    /// </summary>
    [Fact]
    public void TheCategoryAxisDrawsTickMarksByDefault()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0, 4.0);

        StrokedLines.Of(Drawn.Page(chart)).Should().Contain(line => line.IsVertical,
            "the category axis's tick marks run up and down, and need no line format to be drawn");
    }

    /// <summary>
    ///   The category axis's own line - the single segment running along it, separate from its
    ///   tick marks - still waits on <see cref="Axis.LineFormat"/>, which holds no pen until a
    ///   caller sets one.
    /// </summary>
    [Fact]
    public void TheCategoryAxissOwnLineStillWaitsForALineFormat()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0, 4.0);
        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;

        var before = StrokedLines.Of(Drawn.Page(chart));
        before.Should().OnlyContain(line => line.IsHorizontal,
            "only the value axis's tick marks, which stick out sideways from it, have drawn anything");

        chart.XAxis.LineFormat.Visible = true;

        // A column chart's category axis runs left to right along the bottom, so its own line is
        // one more horizontal segment - one this chart had no line to draw before.
        var after = StrokedLines.Of(Drawn.Page(chart));
        after.Should().OnlyContain(line => line.IsHorizontal);
        after.Count.Should().BeGreaterThan(before.Count, "the category axis now draws its own line");
    }

    /// <summary>
    ///   A chart with nothing plotted scales to a maximum of zero, so its minor tick count is also
    ///   zero. Dividing the axis length by that turned every minor tick position into NaN - the
    ///   same defect class the major ticks were already guarded against, just for a pen that stays
    ///   null until a caller asks for minor ticks at all.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void AnEmptyChartsMinorTickMarksStillWriteNoNaN(ChartType type)
    {
        var chart = Charts.Empty(type);
        chart.XAxis.MinorTickMark = TickMarkType.Outside;

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
    }

    [Fact]
    public void AskingForMinorTickMarksDrawsMoreOfThemThanMajorOnesAlone()
    {
        var majorOnly = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0, 4.0);
        majorOnly.XAxis.LineFormat.Visible = true;
        majorOnly.XAxis.MinorTickMark = TickMarkType.None;

        var withMinor = Charts.Of(ChartType.Column2D, 1.0, 3.0, 2.0, 4.0);
        withMinor.XAxis.LineFormat.Visible = true;
        withMinor.XAxis.MinorTickMark = TickMarkType.Outside;

        // A minor tick every half category against a major tick every whole one, so asking for
        // them roughly doubles what the axis strokes.
        StrokedLines.Of(Drawn.Page(withMinor)).Count
            .Should().BeGreaterThan(StrokedLines.Of(Drawn.Page(majorOnly)).Count);
    }

    /// <summary>
    ///   The bar chart's category axis is the same story as the column chart's: its tick marks -
    ///   here running across rather than up - are stroked by default, and only its own line still
    ///   waits on a line format.
    /// </summary>
    [Fact]
    public void ABarChartsCategoryAxisDrawsTickMarksByDefaultButNotItsOwnLine()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0);
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        var beforeLineFormat = StrokedLines.Of(Drawn.Page(chart));
        beforeLineFormat.Should().NotBeEmpty("the category axis's tick marks need no line format");
        beforeLineFormat.Should().OnlyContain(line => line.IsHorizontal,
            "a bar chart's category tick marks stick out sideways, and its own line is not drawn yet");

        chart.XAxis.LineFormat.Visible = true;
        chart.XAxis.MinorTickMark = TickMarkType.Outside;

        var lines = StrokedLines.Of(Drawn.Page(chart));

        // A bar chart's categories run up the side, so the axis itself is the one vertical line
        // and every tick mark sticks out sideways from it.
        lines.Should().ContainSingle(line => line.IsVertical);
        lines.Should().Contain(line => line.IsHorizontal);
    }

    [Fact]
    public void ABarChartsCategoryAxisDrawsAnAxisLineWhenItHasALineFormat()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0);
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;
        chart.XAxis.MajorTickMark = TickMarkType.None;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.XAxis.LineFormat.Visible = true;

        // Every tick mark is off, so what is left is the axis itself - one line, up the side.
        var lines = StrokedLines.Of(Drawn.Page(chart));

        lines.Should().ContainSingle();
        lines[0].IsVertical.Should().BeTrue();
    }

    [Theory]
    [InlineData(TickMarkType.Inside)]
    [InlineData(TickMarkType.Outside)]
    [InlineData(TickMarkType.Cross)]
    public void EachKindOfTickMarkIsDrawnSomewhereOfItsOwn(TickMarkType kind)
    {
        var lines = TickMarks(kind);
        var others = new[] { TickMarkType.Inside, TickMarkType.Outside, TickMarkType.Cross }
            .Where(other => other != kind)
            .Select(TickMarks);

        others.Should().NotContain(lines);
    }

    /// <summary>
    ///   A category with no value is skipped rather than drawn: the horizontal renderer tests each
    ///   X value for null before it measures it.
    /// </summary>
    [Fact]
    public void AColumnChartSkipsACategoryWithNoValue()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        var categories = chart.XValues.AddXSeries();
        categories.Add("A");
        categories.AddBlank();
        categories.Add("C");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);

        var shown = ShownText.On(Drawn.Page(chart));

        shown.Should().Contain("A").And.Contain("C");
    }

    /// <summary>
    ///   And so does the same chart drawn as bars. The vertical renderer used to read the X value
    ///   without testing it first - the two are otherwise the same method, and it was a guard one
    ///   of them was missing rather than a difference either was designed to have.
    /// </summary>
    [Fact]
    public void ABarChartSkipsACategoryWithNoValue()
    {
        var chart = Charts.Empty(ChartType.Bar2D);
        var categories = chart.XValues.AddXSeries();
        categories.Add("A");
        categories.AddBlank();
        categories.Add("C");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);

        var shown = ShownText.On(Drawn.Page(chart));

        shown.Should().Contain("A").And.Contain("C");
    }

    /// <summary>
    ///   The blank keeps its place: the categories around it are still drawn beside their own
    ///   bars, rather than closing up over the gap.
    /// </summary>
    [Fact]
    public void ABlankCategoryStillTakesItsPlaceOnTheAxis()
    {
        var chart = Charts.Empty(ChartType.Bar2D);
        var categories = chart.XValues.AddXSeries();
        categories.Add("A");
        categories.AddBlank();
        categories.Add("C");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);

        var page = Drawn.Page(chart);
        var labels = ShownText.RunsOn(page).Where(run => run.Text.Length == 1).ToList();
        var bars = PaintedRectangles.FilledOn(page);

        labels.Select(label => label.Text).Should().Equal("C", "A");
        labels[0].Y.Should().BeInRange(bars[2].Y, bars[2].Top);
        labels[1].Y.Should().BeInRange(bars[0].Y, bars[0].Top);
    }

    /// <summary>
    ///   Fewer categories than there are values is the same story from the other side. The scale
    ///   runs to the longest series, so the horizontal renderer's second condition - stop at the
    ///   end of the category list - is what keeps it in bounds, and the vertical renderer has no
    ///   such condition.
    /// </summary>
    [Fact]
    public void AColumnChartDrawsTheCategoriesItHasWhenThereAreFewerThanValues()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);

        ShownText.On(Drawn.Page(chart)).Should().Contain("A").And.Contain("B");
    }

    [Fact]
    public void ABarChartDrawsTheCategoriesItHasWhenThereAreFewerThanValues()
    {
        var chart = Charts.Empty(ChartType.Bar2D);
        chart.XValues.AddXSeries().Add("A", "B");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);

        var page = Drawn.Page(chart);

        ShownText.On(page).Should().Contain("A").And.Contain("B");
        PaintedRectangles.FilledOn(page).Should().HaveCount(3,
            "the third value is still plotted, it simply has no name");
    }

    /// <summary>
    ///   Where a column chart's category tick marks are drawn, as text, so that two kinds can be
    ///   compared for having come out anywhere different.
    /// </summary>
    private static string TickMarks(TickMarkType kind)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.XAxis.LineFormat.Visible = true;
        chart.XAxis.MajorTickMark = kind;
        chart.XAxis.MinorTickMark = TickMarkType.None;
        chart.YAxis.MajorTickMark = TickMarkType.None;
        chart.YAxis.MinorTickMark = TickMarkType.None;

        return string.Join("; ", StrokedLines.Of(Drawn.Page(chart)).Select(line => line.ToString()));
    }

    /// <summary>
    ///   A label is centred on its column by its measured width, so its left edge lands half a
    ///   character to the left of the centre it is aligned to.
    /// </summary>
    private const double LabelTolerance = 5;
}
