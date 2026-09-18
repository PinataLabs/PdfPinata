using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The frame a chart is drawn into, and the renderer it picks to draw it.
/// </summary>
/// <remarks>
///   <c>ChartFrame</c> is the whole public surface of the charting package's drawing side: it
///   chooses a renderer from the chart's type, or a combination renderer if the series disagree
///   about what type they are, and runs Init, Format and Draw over it. Everything else the package
///   does is behind that choice.
/// </remarks>
public class ChartFrameTests
{
    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void EveryChartTypeDrawsSomething(ChartType type)
    {
        var page = Drawn.Page(Charts.Of(type, 1.0, 5.0, 3.0));

        PageContent.Of(page).Should().NotBeEmpty();
        var drewSomething = StrokedLines.Of(page).Any()
            || PaintedRectangles.On(page).Any()
            || ShownText.On(page).Any();
        drewSomething.Should().BeTrue();
    }

    /// <summary>
    ///   A chart whose series do not all agree with it about their type is drawn by the
    ///   combination renderer instead - which is chosen from the series rather than from the
    ///   chart, so a chart of type Column2D holding one line series is not a column chart.
    /// </summary>
    [Fact]
    public void SeriesDisagreeingAboutTheirTypeAreDrawnTogether()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0);
        var line = chart.SeriesCollection.AddSeries();
        line.ChartType = ChartType.Line;
        line.Add(3.0, 1.0);

        var page = Drawn.Page(chart);

        // The column series is filled and the line series is stroked, so the page carries both -
        // which a chart drawn by either renderer alone would not.
        PaintedRectangles.FilledOn(page).Should().HaveCount(2);
        StrokedLines.Of(page).Should().Contain(segment => !segment.IsHorizontal && !segment.IsVertical);
    }

    /// <summary>
    ///   Drawing the frame rather than the chart alone puts a rounded, shadowed, gradient-filled
    ///   border around it first. Nothing else in the package draws a rounded rectangle, so the
    ///   curves are the signature.
    /// </summary>
    [Fact]
    public void DrawingTheFrameAddsABorderThatDrawingTheChartAloneDoesNot()
    {
        var chartOnly = Content(frame => frame.DrawChart);
        var framed = Content(frame => frame.Draw);

        Curves(framed).Should().BeGreaterThan(0);
        Curves(chartOnly).Should().Be(0);
    }

    [Fact]
    public void AFrameHoldingTwoChartsDrawsThemOneAboveTheOther()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = 400;
        page.Height = 600;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var frame = new ChartFrame(new XRect(0, 0, 400, 600));
            frame.Add(Charts.Of(ChartType.Column2D, 1.0, 2.0));
            frame.Add(Charts.Of(ChartType.Column2D, 3.0, 4.0));
            frame.Draw(gfx);
        }

        var columns = PaintedRectangles.FilledOn(Reopened(document));

        columns.Should().HaveCount(4);

        // Two charts, each with two columns standing on its own baseline, and the two baselines
        // are not the same one.
        columns.Select(column => column.Y).Distinct().Should().HaveCount(2);
    }

    /// <summary>
    ///   A chart whose X axis was never asked for is drawn all the same.
    /// </summary>
    /// <remarks>
    ///   <see cref="Chart.XAxis"/> creates the axis the first time it is read, so a chart nothing
    ///   configured has none at all. It used to be drawn at NaN: the category axis renderer
    ///   calculated its scale only when the chart had an axis, leaving the maximum at zero, and
    ///   the plot area builds its matrix by dividing its own width by that scale. The page was
    ///   written with <c>NaN NaN NaN 200 re</c> and would not parse.
    ///
    ///   The scale is now worked out either way, as the value axis renderer has always worked out
    ///   its own, so what an absent axis costs is the labelling and nothing else.
    /// </remarks>
    [Fact]
    public void AChartWithNoXAxisIsStillDrawnAgainstItsData()
    {
        var chart = new Chart(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B", "C");
        chart.SeriesCollection.AddSeries().Add(1.0, 5.0, 3.0);

        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = 440;
        page.Height = 340;
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var frame = new ChartFrame(new XRect(20, 20, 400, 300));
            frame.Add(chart);
            frame.DrawChart(gfx);
        }

        var drawn = Reopened(document);

        Encoding.ASCII.GetString(PageContent.Of(drawn)).Should().NotContain("NaN");

        var columns = PaintedRectangles.FilledOn(drawn);
        columns.Should().HaveCount(3);
        columns[1].Height.Should().BeApproximately(columns[0].Height * 5, 0.01);
        columns[2].Height.Should().BeApproximately(columns[0].Height * 3, 0.01);
    }

    /// <summary>
    ///   What the absent axis does cost: no categories along the bottom. Everything inside the
    ///   plot area is unaffected, which is the whole of the difference.
    /// </summary>
    [Fact]
    public void AChartWithNoXAxisGoesUnlabelled()
    {
        var chart = new Chart(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0);

        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var frame = new ChartFrame(new XRect(0, 0, 400, 300));
            frame.Add(chart);
            frame.DrawChart(gfx);
        }

        ShownText.On(Reopened(document)).Should().NotContain("A").And.NotContain("B");
    }

    /// <summary>
    ///   A chart with nothing plotted draws its axes and nothing inside them.
    /// </summary>
    /// <remarks>
    ///   Three lazily-created collections stood between here and that, all the same shape as
    ///   <see cref="Chart.XAxis"/>: <see cref="Chart.SeriesCollection"/>, read as a field by
    ///   <c>ChartFrame.GetChartRenderer</c>, and <see cref="Series.Elements"/>, read as a field at
    ///   thirteen sites. A chart nothing was added to has none of them, so it threw
    ///   <see cref="NullReferenceException"/> before any renderer ran.
    ///
    ///   Behind them was the arithmetic: with nothing plotted the category scale is zero, because
    ///   it counts the points in the longest series, and the plot area divides its own width by
    ///   that - the same infinity that made a chart with no X axis draw at NaN. The renderers now
    ///   leave the matrix alone when there is nothing to plot against it.
    /// </remarks>
    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    [InlineData(ChartType.PieExploded2D)]
    public void AChartWithNoSeriesAtAllIsDrawnEmpty(ChartType type)
    {
        var page = Drawn.Page(Charts.Empty(type));

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        PaintedRectangles.FilledOn(page).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area2D)]
    [InlineData(ChartType.Pie2D)]
    public void AChartWhoseSeriesHasNoPointsIsDrawnEmpty(ChartType type)
    {
        var chart = Charts.Empty(type);
        chart.SeriesCollection.AddSeries();

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        PaintedRectangles.FilledOn(page).Should().BeEmpty();
    }

    [Fact]
    public void AChartWithCategoriesButNoSeriesIsDrawnEmpty()
    {
        var chart = Charts.Empty(ChartType.Column2D);
        chart.XValues.AddXSeries().Add("A", "B");

        var page = Drawn.Page(chart);

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
        PaintedRectangles.FilledOn(page).Should().BeEmpty();
    }

    /// <summary>
    ///   A line needs two points to be a line, and one series with one point is not enough to
    ///   draw one - which the drawing surface answers by throwing rather than by drawing nothing.
    /// </summary>
    [Fact]
    public void ALineChartWithASinglePointDrawsNoLine()
    {
        var page = Drawn.Page(Charts.Of(ChartType.Line, 3.0));

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().NotContain("NaN");
    }

    private static byte[] Content(Func<ChartFrame, Action<XGraphics>> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var frame = new ChartFrame(new XRect(20, 20, 400, 300));
            frame.Add(Charts.Of(ChartType.Column2D, 1.0, 5.0, 3.0));
            draw(frame)(gfx);
        }

        return PageContent.Of(Reopened(document));
    }

    /// <summary>How many Bézier segments the page draws, which for a chart means rounded corners.</summary>
    private static int Curves(byte[] content) =>
        Encoding.ASCII.GetString(content)
            .Split('\n')
            .Count(line => line.EndsWith(" c") || line == "c");

    private static PdfPage Reopened(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }
}
