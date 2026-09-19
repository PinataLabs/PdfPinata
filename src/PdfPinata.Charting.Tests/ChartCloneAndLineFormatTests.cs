using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   Copying a chart, and the line format every renderer draws its strokes through.
///   <para>
///   <c>Chart.DeepCopy</c> clones nine children by hand and reparents each. A child left out is
///   shared between the copy and the original, which shows only when one of them is written to -
///   so every assertion here mutates the original after copying and looks at the copy.
///   </para>
///   <para>
///   <c>LineFormatRenderer</c> has three constructors, and the one taking an explicit width is
///   how a renderer draws a line at a width the format does not itself state. It is reached the
///   only way anything in this package is reached: by drawing a chart that needs one.
///   </para>
/// </summary>
public class ChartCloneAndLineFormatTests
{
    // ----- Chart.DeepCopy ------------------------------------------------------------------------

    static Chart AFullyPopulatedChart()
    {
        var chart = Charts.OfSeries(ChartType.Line, new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 });
        chart.XAxis.Title.Caption = "across";
        chart.YAxis.Title.Caption = "up";
        chart.ZAxis.Title.Caption = "through";
        chart.PlotArea.LineFormat.Width = 3;
        chart.DataLabel.Format = "0.0";
        return chart;
    }

    [Fact]
    public void ACopiedChartKeepsWhatTheOriginalSaid()
    {
        var copy = AFullyPopulatedChart().Clone();

        copy.XAxis.Title.Caption.Should().Be("across");
        copy.YAxis.Title.Caption.Should().Be("up");
        copy.ZAxis.Title.Caption.Should().Be("through");
        copy.SeriesCollection.Count.Should().Be(2);
        copy.XValues.Count.Should().Be(1);
        copy.PlotArea.LineFormat.Width.Point.Should().BeApproximately(3, 1e-4);
        copy.DataLabel.Format.Should().Be("0.0");
    }

    [Fact]
    public void WritingToTheOriginalAfterwardsDoesNotReachTheCopy()
    {
        var chart = AFullyPopulatedChart();
        var copy = chart.Clone();

        chart.XAxis.Title.Caption = "changed";
        chart.YAxis.Title.Caption = "changed";
        chart.ZAxis.Title.Caption = "changed";
        chart.PlotArea.LineFormat.Width = 9;
        chart.DataLabel.Format = "0.000";
        chart.SeriesCollection.AddSeries().Add(5.0);

        copy.XAxis.Title.Caption.Should().Be("across");
        copy.YAxis.Title.Caption.Should().Be("up");
        copy.ZAxis.Title.Caption.Should().Be("through");
        copy.PlotArea.LineFormat.Width.Point.Should().BeApproximately(3, 1e-4);
        copy.DataLabel.Format.Should().Be("0.0");
        copy.SeriesCollection.Count.Should().Be(2);
        copy.XValues.Count.Should().Be(1);
    }

    /// <summary>
    ///   The legend and the chart's font are children like the other seven, and were the two the
    ///   copy left out: docking the copy's legend moved the original's too.
    /// </summary>
    [Fact]
    public void ACopiedChartHasALegendAndAFontOfItsOwn()
    {
        var chart = AFullyPopulatedChart();
        chart.Legend.Docking = DockingType.Left;
        chart.Font.Size = 10;

        var copy = chart.Clone();
        copy.Legend.Docking = DockingType.Top;
        copy.Font.Size = 20;

        copy.Legend.Should().NotBeSameAs(chart.Legend);
        copy.Font.Should().NotBeSameAs(chart.Font);
        chart.Legend.Docking.Should().Be(DockingType.Left);
        chart.Font.Size.Point.Should().BeApproximately(10, 1e-4);
    }

    /// <summary>
    ///   Every child the copy has is the copy's: none of them still names the original as its
    ///   parent, which is what a renderer walks up through to find the chart a child belongs to.
    /// </summary>
    [Fact]
    public void EveryChildOfACopiedChartNamesTheCopyAsItsParent()
    {
        var chart = AFullyPopulatedChart();
        _ = chart.Legend;
        _ = chart.Font;

        var copy = chart.Clone();

        new DocumentObject[]
        {
            copy.XAxis, copy.YAxis, copy.ZAxis, copy.SeriesCollection, copy.XValues,
            copy.PlotArea, copy.DataLabel, copy.Legend, copy.Font
        }.Should().OnlyContain(child => child.Parent == copy);
    }

    [Fact]
    public void ACopyOfAnEmptyChartIsStillAChart()
    {
        // Every one of the nine clones is guarded by a null check, and a chart with nothing on
        // it takes none of them.
        var copy = Charts.Empty(ChartType.Pie2D).Clone();

        copy.Should().NotBeNull();
        copy.Type.Should().Be(ChartType.Pie2D);
    }

    [Fact]
    public void ACopiedChartDrawsWhatTheOriginalWouldHaveDrawn()
    {
        // The copy has to be a chart a frame can still draw, which is what reparenting is for:
        // a child still pointing at the original would resolve the wrong chart for its defaults.
        var chart = AFullyPopulatedChart();
        chart.HasDataLabel = true;

        var fromCopy = ShownText.On(Drawn.Page(chart.Clone()));
        var fromOriginal = ShownText.On(Drawn.Page(chart));

        fromCopy.Should().Equal(fromOriginal);
    }

    // ----- LineFormatRenderer ---------------------------------------------------------------------

    /// <summary>
    ///   The gridlines of an axis, which is where a line format is actually drawn from: every
    ///   call site in the package builds a <c>LineFormatRenderer</c> out of one of the four
    ///   gridline formats.
    /// </summary>
    static Chart AChartWithGridlines(double width, bool visible = true)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 2.0, 3.0);
        chart.YAxis.HasMajorGridlines = true;
        chart.YAxis.MajorGridlines.LineFormat.Visible = visible;
        chart.YAxis.MajorGridlines.LineFormat.Width = width;
        return chart;
    }

    static double[] StrokeWidthsOf(Chart chart) =>
        StrokedLines.Of(Drawn.Page(chart)).Select(line => line.Width).ToArray();

    [Fact]
    public void AGridlineIsStrokedAtTheWidthItsFormatStates()
    {
        StrokeWidthsOf(AChartWithGridlines(3)).Should()
            .Contain(width => System.Math.Abs(width - 3) < 1e-4);
    }

    [Fact]
    public void AGridlineFormatStatingOnlyAColourIsStillDrawn()
    {
        // Visible is not the only way to ask for a line: a format that names a colour and no
        // width is taken to want one, because naming a colour for a line nobody draws is not a
        // thing anyone means.
        var chart = Charts.Of(ChartType.Column2D, 1.0, 2.0, 3.0);
        chart.YAxis.HasMajorGridlines = true;
        chart.YAxis.MajorGridlines.LineFormat.Color = XColors.Red;

        StrokedLines.Of(Drawn.Page(chart)).Select(line => line.Colour)
            .Should().Contain("1,0,0");
    }

    /// <summary>
    ///   Asking for gridlines is what draws them; the format only says how. A format that states
    ///   nothing still gets a line, at the renderer's own default width rather than at nothing and
    ///   rather than at a width it was never given.
    /// </summary>
    [Fact]
    public void AGridlineFormatThatStatesNothingIsDrawnAtADefaultWidthRatherThanAStatedOne()
    {
        var stated = AChartWithGridlines(4);
        var silent = Charts.Of(ChartType.Column2D, 1.0, 2.0, 3.0);
        silent.YAxis.HasMajorGridlines = true;
        var none = Charts.Of(ChartType.Column2D, 1.0, 2.0, 3.0);

        StrokeWidthsOf(stated).Should().Contain(width => System.Math.Abs(width - 4) < 1e-4);
        StrokeWidthsOf(silent).Should().NotContain(width => System.Math.Abs(width - 4) < 1e-4,
            "the width belongs to the chart that stated it and to no other");
        StrokeWidthsOf(silent).Length.Should().BeGreaterThan(StrokeWidthsOf(none).Length,
            "a silent format is still a gridline - it is HasMajorGridlines that decides");
    }

    [Fact]
    public void AChartWithNoGridlinesAsksForNoneOfThem()
    {
        var withGridlines = AChartWithGridlines(5);
        var without = Charts.Of(ChartType.Column2D, 1.0, 2.0, 3.0);

        StrokedLines.Of(Drawn.Page(withGridlines)).Count
            .Should().BeGreaterThan(StrokedLines.Of(Drawn.Page(without)).Count);
    }
}
