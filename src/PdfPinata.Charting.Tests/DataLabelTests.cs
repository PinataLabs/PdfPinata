using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The numbers written on the data itself.
/// </summary>
/// <remarks>
///   <c>DataLabelRenderer.Init</c> decides, for each series, whether it has labels at all and what
///   they say. There are four ways to ask for them - a flag or a settings object, on the chart or
///   on one series - and the settings are inherited from the chart by a series that has none of
///   its own. Where nothing says otherwise the format is "0" and the type is the value, except on
///   a pie, where it is the percentage. Every one of those is a branch, and none of them shows
///   anywhere but in the text drawn on the page.
///
///   <c>PieDataLabelRenderer.CalcPositions</c> then decides where a pie's labels go, which is a
///   harder question than for a column: there is no rectangle to sit on top of, only a wedge and
///   an angle, and the answer depends on which of the three label positions was asked for.
/// </remarks>
public class DataLabelTests
{
    [Fact]
    public void AChartWithNoDataLabelsWritesNoneOfTheValues()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Column2D, 12.0, 34.0));

        ShownText.On(page).Should().NotContain("12").And.NotContain("34");
    }

    [Fact]
    public void AskingTheChartForDataLabelsLabelsEverySeries()
    {
        var chart = Charts.OfSeries(ChartType.Column2D, new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 });
        chart.HasDataLabel = true;

        var page = Drawn.Page(chart);

        ShownText.On(page).Take(4).Should().Equal("1", "2", "3", "4");
    }

    /// <summary>
    ///   Settings on the chart are enough on their own: a chart that was given a data label object
    ///   has labels whether or not anything set the flag, because the renderer treats the object's
    ///   existence as the request.
    /// </summary>
    [Fact]
    public void GivingTheChartADataLabelFormatIsItselfAskingForLabels()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.5, 2.5);
        chart.DataLabel.Format = "0.00";

        ShownText.On(Drawn.Page(chart)).Take(2).Should().Equal("1.50", "2.50");
    }

    [Fact]
    public void TheDefaultFormatRoundsToAWholeNumber()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.4, 2.5);
        chart.HasDataLabel = true;

        ShownText.On(Drawn.Page(chart)).Take(2).Should().Equal("1", "3");
    }

    [Fact]
    public void AskingOneSeriesForLabelsLeavesTheOthersUnlabelled()
    {
        var chart = Charts.OfSeries(ChartType.Column2D, new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 });
        chart.SeriesCollection[1].HasDataLabel = true;

        var shown = ShownText.On(Drawn.Page(chart));

        shown.Take(2).Should().Equal("3", "4");
        shown.Should().NotContain("1").And.NotContain("2");
    }

    [Fact]
    public void ASeriesWithItsOwnFormatUsesItRatherThanTheCharts()
    {
        var chart = Charts.OfSeries(ChartType.Column2D, new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 });
        chart.DataLabel.Format = "0.0";
        chart.SeriesCollection[1].DataLabel.Format = "0.000";

        var shown = ShownText.On(Drawn.Page(chart));

        shown.Take(4).Should().Equal("1.0", "2.0", "3.000", "4.000");
    }

    [Fact]
    public void ADataLabelIsDrawnOverTheColumnItBelongsTo()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        chart.HasDataLabel = true;

        var page = Drawn.Page(chart);
        var labels = ShownText.RunsOn(page).Take(3).ToList();
        var columns = PaintedRectangles.FilledOn(page);

        for (var idx = 0; idx < columns.Count; idx++)
        {
            labels[idx].X.Should().BeInRange(columns[idx].X, columns[idx].Right);
            labels[idx].Y.Should().BeInRange(columns[idx].Y, columns[idx].Top);
        }
    }

    /// <summary>
    ///   A pie is labelled with percentages rather than values unless it is told otherwise - the
    ///   one place where the default type depends on the kind of chart being drawn.
    /// </summary>
    [Fact]
    public void APieIsLabelledWithPercentagesByDefault()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 2.0, 1.0);
        chart.HasDataLabel = true;

        ShownText.On(Drawn.Page(chart)).Should().Equal("25%", "50%", "25%");
    }

    [Fact]
    public void APieAskedForValuesIsLabelledWithThem()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 2.0, 1.0);
        chart.DataLabel.Type = DataLabelType.Value;

        ShownText.On(Drawn.Page(chart)).Should().Equal("1", "2", "1");
    }

    /// <summary>
    ///   Percentages are a pie's answer and no other chart's. A column asked for them says so
    ///   rather than drawing something wrong, which is worth pinning down because the type is
    ///   accepted by the object model without complaint and only refused at the point of drawing.
    /// </summary>
    [Fact]
    public void AColumnChartRefusesToLabelItselfWithPercentages()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.DataLabel.Type = DataLabelType.Percent;

        var drawing = () => Drawn.Page(chart);

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be set to 'Percent'*");
    }

    [Fact]
    public void AnExplodedPieIsLabelledLikeAClosedOne()
    {
        var chart = Charts.Of(ChartType.PieExploded2D, 1.0, 1.0, 1.0, 1.0);
        chart.HasDataLabel = true;

        ShownText.On(Drawn.Page(chart)).Should().Equal("25%", "25%", "25%", "25%");
    }

    /// <summary>
    ///   Where a pie's labels sit depends on the position asked for, and the three answers are
    ///   distinct: outside the wedge is further from the middle of the pie than inside its end,
    ///   which is further than the centre.
    /// </summary>
    [Fact]
    public void APiesLabelPositionDecidesHowFarFromTheMiddleTheLabelsSit()
    {
        var centre = SpreadOfLabels(DataLabelPosition.Center);
        var insideEnd = SpreadOfLabels(DataLabelPosition.InsideEnd);
        var outsideEnd = SpreadOfLabels(DataLabelPosition.OutsideEnd);

        insideEnd.Should().BeGreaterThan(centre);
        outsideEnd.Should().BeGreaterThan(insideEnd);
    }

    /// <summary>
    ///   The fourth position a pie label can be given puts each label at the base of its own
    ///   wedge, which for a pie is the centre of the circle - each laid out away from that point
    ///   along the wedge it belongs to, so that the four do not land on top of one another.
    /// </summary>
    /// <remarks>
    ///   They used to. The case set the label's corner to the centre of the pie and then tested
    ///   whether that corner was left of, or above, the centre of the pie - which it could not be,
    ///   being the same point - so neither adjustment ever ran. The tests are now on the direction
    ///   the wedge runs in, which is what they were reaching for.
    /// </remarks>
    [Fact]
    public void APieLabelledAtItsBaseGivesEachWedgeItsOwnCorner()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 1.0, 1.0, 1.0);
        chart.HasDataLabel = true;
        chart.DataLabel.Position = DataLabelPosition.InsideBase;

        var labels = ShownText.RunsOn(Drawn.Page(chart)).ToList();

        // Four equal wedges, one to a quadrant, so each label takes a different corner of the
        // centre and no two are drawn at the same point.
        labels.Should().HaveCount(4);
        labels.Select(label => (label.X, label.Y)).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void APieLabelledAtItsBaseKeepsItsLabelsNearerTheMiddleThanAnyOtherPosition()
    {
        SpreadOfLabels(DataLabelPosition.InsideBase)
            .Should().BeLessThan(SpreadOfLabels(DataLabelPosition.Center));
    }

    [Fact]
    public void APieTooSmallForItsLabelsStillSpreadsThemRoundIt()
    {
        // The gap comes off the radius, so a small pie asked for large labels could otherwise have
        // its inside-end labels pulled past the middle and out through the far side of the wedge.
        // They stop at the half-radius instead, and four wedges still get four different places.
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 1.0, 1.0, 1.0);
        chart.HasDataLabel = true;
        chart.DataLabel.Position = DataLabelPosition.InsideEnd;
        chart.DataLabel.Font.Size = 20;

        var labels = ShownText.RunsOn(Drawn.Page(chart, 90, 90)).ToList();

        labels.Should().HaveCount(4);
        labels.Select(label => (label.X, label.Y)).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void APieAskedForNoLabelTypeIsLeftUnlabelled()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 2.0, 1.0);
        chart.DataLabel.Type = DataLabelType.None;

        ShownText.On(Drawn.Page(chart)).Should().BeEmpty();
    }

    [Fact]
    public void EveryWedgeOfAPieIsLabelledSomewhereDifferent()
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 1.0, 1.0, 1.0);
        chart.HasDataLabel = true;

        var labels = ShownText.RunsOn(Drawn.Page(chart)).ToList();

        labels.Should().HaveCount(4);
        labels.Select(label => (label.X, label.Y)).Distinct().Should().HaveCount(4);
    }

    /// <summary>
    ///   How far apart the four labels of an equal four-wedge pie are drawn, measured as the width
    ///   of the box they all fall inside. The pie itself does not move, so a position that puts
    ///   the labels further out puts them further apart.
    /// </summary>
    private static double SpreadOfLabels(DataLabelPosition position)
    {
        var chart = Charts.Of(ChartType.Pie2D, 1.0, 1.0, 1.0, 1.0);
        chart.HasDataLabel = true;
        chart.DataLabel.Position = position;

        var labels = ShownText.RunsOn(Drawn.Page(chart)).ToList();

        return labels.Max(label => label.X) - labels.Min(label => label.X);
    }
    // ----- a bar chart's labels ---------------------------------------------------------------

    /// <summary>
    ///   A bar chart's labels are laid out by <c>BarDataLabelRenderer</c>, which is the pie
    ///   renderer's opposite number and the column renderer's mirror image: a bar runs across the
    ///   page, so the position chooses an x within the bar and the y is always its middle.
    /// </summary>
    /// <remarks>
    ///   Read a blank point's value through <c>PointRendererInfo.Value</c>, which answers NaN,
    ///   rather than through <c>point.value</c>, which throws - the shape
    ///   <c>charting-renderer-findings.md</c> records as C7 on the pie.
    /// </remarks>
    private static IReadOnlyList<ShownText.Run> BarLabelsAt(DataLabelPosition position)
    {
        var chart = Charts.Of(ChartType.Bar2D, 10.0, 20.0, 30.0);
        chart.HasDataLabel = true;
        chart.DataLabel.Position = position;

        return ShownText.RunsOn(Drawn.Page(chart));
    }

    private static IReadOnlyList<ShownText.Run> ValueLabelsOf(IReadOnlyList<ShownText.Run> runs) =>
        runs.Where(run => run.Text is "10" or "20" or "30").ToList();

    [Fact]
    public void EveryBarIsLabelledWithItsValue()
    {
        var labels = ValueLabelsOf(BarLabelsAt(DataLabelPosition.Center));

        labels.Select(label => label.Text).Should().BeEquivalentTo(new[] { "10", "20", "30" });
    }

    [Theory]
    [InlineData(DataLabelPosition.Center)]
    [InlineData(DataLabelPosition.InsideBase)]
    [InlineData(DataLabelPosition.InsideEnd)]
    [InlineData(DataLabelPosition.OutsideEnd)]
    public void EveryLabelPositionPutsALabelOnEveryBar(DataLabelPosition position)
    {
        ValueLabelsOf(BarLabelsAt(position)).Should().HaveCount(3);
    }

    [Fact]
    public void EachLabelPositionPutsTheLabelSomewhereDifferentAlongTheBar()
    {
        // The four arms of the switch differ only in the x they choose, so the y is expected to
        // be the same and the x is expected not to be.
        var atBase = ValueLabelsOf(BarLabelsAt(DataLabelPosition.InsideBase));
        var atEnd = ValueLabelsOf(BarLabelsAt(DataLabelPosition.InsideEnd));
        var outside = ValueLabelsOf(BarLabelsAt(DataLabelPosition.OutsideEnd));

        atBase[0].X.Should().BeLessThan(atEnd[0].X, "the base of a bar is to the left of its end");
        atEnd[0].X.Should().BeLessThan(outside[0].X, "and outside the end is further right still");
        atEnd[0].Y.Should().BeApproximately(atBase[0].Y, 0.01, "every position sits mid-bar");
    }

    [Fact]
    public void ALongerBarIsLabelledFurtherAlongThanAShorterOne()
    {
        // The position is worked out from the bar's own rectangle, so the three labels of three
        // different values cannot all land in the same place.
        var labels = ValueLabelsOf(BarLabelsAt(DataLabelPosition.InsideEnd));

        var byValue = labels.OrderBy(label => int.Parse(label.Text)).ToList();
        byValue[0].X.Should().BeLessThan(byValue[1].X);
        byValue[1].X.Should().BeLessThan(byValue[2].X);
    }

    [Fact]
    public void ABarChartWithABlankInItIsStillLabelled()
    {
        // A blank is a null, and reading it as a number is what throws. The renderer has to reach
        // the value through PointRendererInfo.Value, which answers NaN for one.
        var chart = Charts.Of(ChartType.Bar2D, 10.0, 20.0);
        chart.SeriesCollection[0].Add(30.0);
        chart.SeriesCollection[0].AddBlank();
        chart.HasDataLabel = true;

        var draw = () => ShownText.RunsOn(Drawn.Page(chart));

        // Drawing without throwing is half of it. The other half is that the points either side
        // of the blank are still labelled: a renderer that gave up at the blank would also not
        // throw, and would leave the chart with fewer labels than it has values.
        var labels = draw.Should().NotThrow().Subject;
        labels.Select(run => run.Text).Should().Contain(new[] { "10", "20", "30" },
            "the blank is skipped, not the points around it");
    }
}
