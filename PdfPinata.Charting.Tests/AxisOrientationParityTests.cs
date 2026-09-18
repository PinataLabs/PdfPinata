using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   A column chart and a bar chart draw the same data turned on its side: a column chart's
///   category axis runs left to right and its value axis runs up the side, a bar chart's the
///   other way round. Both are drawn by the same merged renderers now,
///   <see cref="PdfPinata.Charting.Renderers.XAxisRenderer"/> and
///   <see cref="PdfPinata.Charting.Renderers.YAxisRenderer"/>, with the orientation injected as
///   data rather than as a second copy of the geometry.
/// </summary>
/// <remarks>
///   Everything asserted here should not depend on which way an axis runs, and before the two
///   renderers of each pair were merged, none of it was pinned down anywhere: a future change
///   reaching one orientation and not its twin - the defect class that produced the tick-mark
///   pens bug this merge repairs, and the two defects before it in
///   docs/specs/charting-renderer-findings.md - would have had nothing here to catch it. See
///   docs/specs/axis-renderer-duplication.md.
/// </remarks>
public class AxisOrientationParityTests
{
    /// <summary>
    ///   The tick-mark pens repair, pinned for every axis kind in turn. Before the axis renderers
    ///   were merged, this was true only of the value axis of a column or line chart - the one
    ///   renderer of the four whose tick marks were stroked with a pen that was never null; the
    ///   other three stroked theirs with a pen built from <c>Axis.LineFormat</c>, which is null
    ///   until a caller sets one, so their tick marks did not appear until then. This is the
    ///   repair item 2 of docs/specs/axis-renderer-duplication.md asks for, and it fails on three
    ///   of these four cases against the renderers this merge replaces.
    /// </summary>
    [Theory]
    [InlineData(ChartType.Column2D, true)]  // The category axis, horizontal for a column chart.
    [InlineData(ChartType.Column2D, false)] // The value axis, vertical for a column chart.
    [InlineData(ChartType.Bar2D, true)]     // The category axis, vertical for a bar chart.
    [InlineData(ChartType.Bar2D, false)]    // The value axis, horizontal for a bar chart.
    public void EveryAxisDrawsTickMarksByDefaultWithNoLineFormatSet(ChartType type, bool testingCategoryAxis)
    {
        var chart = Charts.Of(type, 1.0, 5.0, 3.0);

        // Turn off the other axis's tick marks, so what is left to strike can only be the one
        // under test.
        if (testingCategoryAxis)
        {
            chart.YAxis.MajorTickMark = TickMarkType.None;
            chart.YAxis.MinorTickMark = TickMarkType.None;
        }
        else
        {
            chart.XAxis.MajorTickMark = TickMarkType.None;
            chart.XAxis.MinorTickMark = TickMarkType.None;
        }

        StrokedLines.Of(Drawn.Page(chart)).Should().NotBeEmpty(
            "no axis should need a line format before its own tick marks are drawn");
    }

    /// <summary>
    ///   The same categories, drawn along the bottom or up the side, are the same set of labels.
    /// </summary>
    [Fact]
    public void BothOrientationsLabelTheSameCategories()
    {
        var column = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        var bar = Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0);

        var columnLabels = ShownText.RunsOn(Drawn.Page(column))
            .Where(run => run.Text.Length == 1).Select(run => run.Text);
        var barLabels = ShownText.RunsOn(Drawn.Page(bar))
            .Where(run => run.Text.Length == 1).Select(run => run.Text);

        columnLabels.Should().BeEquivalentTo(barLabels);
    }

    /// <summary>
    ///   The value axis is scaled by the same arithmetic - <c>YAxisRenderer.FineTuneYAxis</c> -
    ///   whichever way it runs, so the same data produces the same tick labels.
    /// </summary>
    [Fact]
    public void BothOrientationsShowTheSameValueAxisLabels()
    {
        var column = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        var bar = Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0);

        ShownText.NumericOn(Drawn.Page(column)).Should().Equal(ShownText.NumericOn(Drawn.Page(bar)));
    }

    /// <summary>
    ///   The number of lines an axis strokes - its major and minor tick marks - does not depend on
    ///   which way it runs. A column chart and a bar chart plotting the same data stroke the same
    ///   total between their category axis and their value axis, whichever axis is which.
    /// </summary>
    [Fact]
    public void BothOrientationsStrokeTheSameNumberOfLines()
    {
        var column = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        var bar = Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0);

        StrokedLines.Of(Drawn.Page(column)).Count.Should().Be(StrokedLines.Of(Drawn.Page(bar)).Count);
    }

    /// <summary>
    ///   The widths an axis strokes its lines at - the default tick-mark widths
    ///   <c>AxisRenderer.DefaultMajorTickMarkLineWidth</c> and
    ///   <c>DefaultMinorTickMarkLineWidth</c> - are read from the same pens whichever way the axis
    ///   runs, so the same data produces the same multiset of stroke widths.
    /// </summary>
    [Fact]
    public void BothOrientationsUseTheSameTickMarkWidths()
    {
        var column = Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0);
        var bar = Charts.Of(ChartType.Bar2D, 1.0, 5.0, 3.0);

        var columnWidths = StrokedLines.Of(Drawn.Page(column)).Select(line => line.Width).OrderBy(w => w);
        var barWidths = StrokedLines.Of(Drawn.Page(bar)).Select(line => line.Width).OrderBy(w => w);

        columnWidths.Should().Equal(barWidths);
    }
}
