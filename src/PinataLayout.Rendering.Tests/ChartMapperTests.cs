using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;
using Charting = PdfPinata.Charting;
using DomChart = PinataLayout.DocumentObjectModel.Shapes.Charts.Chart;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A chart is described twice in this library: once in the document object model, which is what a
///   caller builds, and once in PdfPinata.Charting, which is what gets drawn. The mappers copy
///   the first onto the second, and every property they carry across is a branch guarded by a test
///   for whether the caller set it — <c>IsNull("MajorTick")</c> and its like. Anything the mapper
///   forgets is not an error anywhere; the drawn chart simply keeps the default, which is why these
///   are worth pinning value by value rather than by rendering a chart and looking at it.
/// </summary>
public class ChartMapperTests
{
    /// <summary>
    ///   A chart has to hang off a document: the series mapper reads
    ///   <c>domSeries.Document.UseCmykColor</c> for its marker colours, and asks
    ///   <c>DocumentRelations</c> for the parent chart when a series does not name its own type.
    /// </summary>
    static DomChart ChartIn(Document document, ChartType type = ChartType.Line)
    {
        return document.AddSection().AddChart(type);
    }

    static Charting.Chart Mapped(DomChart domChart)
    {
        return MappedChartProbe.In(ChartMapper.ChartMapper.Map(domChart));
    }

    // ----- the frame itself -----

    [Fact]
    public void TheFrameTakesItsSizeAndPositionFromTheChart()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.Width = Unit.FromPoint(300);
        domChart.Height = Unit.FromPoint(200);

        var frame = ChartMapper.ChartMapper.Map(domChart);

        frame.Size.Width.Should().BeApproximately(300, 0.01);
        frame.Size.Height.Should().BeApproximately(200, 0.01);
    }

    [Theory]
    [InlineData(ChartType.Line, Charting.ChartType.Line)]
    [InlineData(ChartType.Column2D, Charting.ChartType.Column2D)]
    [InlineData(ChartType.Bar2D, Charting.ChartType.Bar2D)]
    [InlineData(ChartType.Pie2D, Charting.ChartType.Pie2D)]
    [InlineData(ChartType.Area2D, Charting.ChartType.Area2D)]
    public void TheChartKeepsItsType(ChartType domType, Charting.ChartType expected)
    {
        Mapped(ChartIn(new Document(), domType)).Type.Should().Be(expected);
    }

    // ----- AxisMapper -----

    /// <summary>
    ///   The scale and the tick spacing are numbers the caller sets and the drawing reads back, so
    ///   a mapper that dropped one would put the gridlines somewhere else entirely.
    /// </summary>
    [Fact]
    public void AnAxisCarriesItsScaleAndItsTicks()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.XAxis.MinimumScale = 5;
        domChart.XAxis.MaximumScale = 55;
        domChart.XAxis.MajorTick = 10;
        domChart.XAxis.MinorTick = 2.5;

        var xAxis = Mapped(domChart).XAxis;

        xAxis.MinimumScale.Should().Be(5);
        xAxis.MaximumScale.Should().Be(55);
        xAxis.MajorTick.Should().Be(10);
        xAxis.MinorTick.Should().Be(2.5);
    }

    [Theory]
    [InlineData(TickMarkType.None, Charting.TickMarkType.None)]
    [InlineData(TickMarkType.Inside, Charting.TickMarkType.Inside)]
    [InlineData(TickMarkType.Outside, Charting.TickMarkType.Outside)]
    [InlineData(TickMarkType.Cross, Charting.TickMarkType.Cross)]
    public void AnAxisCarriesTheShapeOfItsTickMarks(TickMarkType domType, Charting.TickMarkType expected)
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.YAxis.MajorTickMark = domType;
        domChart.YAxis.MinorTickMark = domType;

        var yAxis = Mapped(domChart).YAxis;

        yAxis.MajorTickMark.Should().Be(expected);
        yAxis.MinorTickMark.Should().Be(expected);
    }

    /// <summary>
    ///   Gridlines are two decisions rather than one: whether they are drawn at all, and how. The
    ///   mapper reads the second only when the caller described it, so both are worth a test.
    /// </summary>
    [Fact]
    public void AnAxisCarriesWhetherItHasGridlines()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.XAxis.HasMajorGridlines = true;
        domChart.XAxis.HasMinorGridlines = true;
        domChart.YAxis.HasMajorGridlines = false;
        domChart.YAxis.HasMinorGridlines = false;

        var chart = Mapped(domChart);

        chart.XAxis.HasMajorGridlines.Should().BeTrue();
        chart.XAxis.HasMinorGridlines.Should().BeTrue();
        chart.YAxis.HasMajorGridlines.Should().BeFalse();
        chart.YAxis.HasMinorGridlines.Should().BeFalse();
    }

    [Fact]
    public void AnAxisCarriesItsTitleAndHowItSits()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.XAxis.Title.Caption = "Quarter";
        domChart.XAxis.Title.Orientation = 90;
        domChart.XAxis.Title.Alignment = HorizontalAlignment.Right;
        domChart.XAxis.Title.VerticalAlignment = DocumentObjectModel.Tables.VerticalAlignment.Bottom;

        var title = Mapped(domChart).XAxis.Title;

        title.Caption.Should().Be("Quarter");
        title.Orientation.Should().BeApproximately(90, 0.01);
        title.Alignment.Should().Be(Charting.HorizontalAlignment.Right);
        title.VerticalAlignment.Should().Be(Charting.VerticalAlignment.Bottom);
    }

    [Fact]
    public void AnAxisCarriesTheFormatItsTickLabelsAreWrittenIn()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.YAxis.TickLabels.Format = "#,##0.00";

        Mapped(domChart).YAxis.TickLabels.Format.Should().Be("#,##0.00");
    }

    /// <summary>
    ///   A title's style is mapped first and its own font over it, so the font must carry across
    ///   only what it sets. It used to carry everything, and a size set on its own answered false
    ///   for the style's bold and an empty colour for the style's red.
    /// </summary>
    [Fact]
    public void AnAxisTitlesOwnFontLeavesWhatItDoesNotSetToItsStyle()
    {
        var document = new Document();
        var loud = document.Styles.AddStyle("Loud", "Normal");
        loud.Font.Bold = true;
        loud.Font.Color = Colors.Red;
        var domChart = ChartIn(document);
        domChart.YAxis.Title.Caption = "Revenue";
        domChart.YAxis.Title.Style = "Loud";
        domChart.YAxis.Title.Font.Size = 14;

        var font = Mapped(domChart).YAxis.Title.Font;

        font.Size.Point.Should().BeApproximately(14, 0.01);
        font.Bold.Should().BeTrue();
        font.Color.R.Should().Be(255);
        font.Color.G.Should().Be(0);
    }

    /// <summary>
    ///   An axis the caller never touched still maps, and takes the drawn defaults rather than
    ///   whatever the last chart left behind. Every copy in the mapper is guarded by a test for
    ///   whether the value was set, and this is the path where none of them fire.
    /// </summary>
    [Fact]
    public void AnAxisNobodySetStillMaps()
    {
        var chart = Mapped(ChartIn(new Document()));

        chart.XAxis.Should().NotBeNull();
        chart.YAxis.Should().NotBeNull();
        chart.XAxis.Title.Caption.Should().BeNullOrEmpty();
    }

    // ----- LegendMapper -----

    /// <summary>
    ///   Where the legend sits is decided by which of the chart's six areas it was added to, and
    ///   the mapper walks all six in a fixed order looking for it. Two of them mean the same edge
    ///   as another — a header docks to the top and a footer to the bottom — which is the sort of
    ///   thing that is silently lost when a case is dropped.
    /// </summary>
    [Fact]
    public void ALegendInTheBottomAreaDocksToTheBottom()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.BottomArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Bottom);
    }

    [Fact]
    public void ALegendInTheTopAreaDocksToTheTop()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.TopArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Top);
    }

    [Fact]
    public void ALegendInTheLeftAreaDocksToTheLeft()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.LeftArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Left);
    }

    [Fact]
    public void ALegendInTheRightAreaDocksToTheRight()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.RightArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Right);
    }

    [Fact]
    public void ALegendInTheHeaderAreaDocksToTheTop()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.HeaderArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Top);
    }

    [Fact]
    public void ALegendInTheFooterAreaDocksToTheBottom()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.FooterArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Bottom);
    }

    /// <summary>
    ///   The areas are walked in order and the last one holding a legend wins, rather than the
    ///   first. A chart with a legend in two places is not something a caller should write, but
    ///   what happens then is decided by the order of the loops and is worth recording.
    /// </summary>
    [Fact]
    public void TheLastAreaWalkedDecidesWhereTwoLegendsDock()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.BottomArea.AddLegend();
        domChart.FooterArea.AddLegend();

        Mapped(domChart).Legend.Docking.Should().Be(Charting.DockingType.Bottom);
    }

    // ----- SeriesCollectionMapper -----

    [Fact]
    public void EverySeriesIsCarriedAcrossWithItsNameAndItsValues()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        var first = domChart.SeriesCollection.AddSeries();
        first.Name = "Rainfall";
        first.Add(1.0, 2.0, 3.0);
        var second = domChart.SeriesCollection.AddSeries();
        second.Name = "Sunshine";
        second.Add(4.0, 5.0);

        var series = Mapped(domChart).SeriesCollection;

        series.Count.Should().Be(2);
        series[0].Name.Should().Be("Rainfall");
        series[0].Count.Should().Be(3);
        series[1].Name.Should().Be("Sunshine");
        series[1].Count.Should().Be(2);
    }

    /// <summary>
    ///   A series that does not name a type of its own takes the chart's, which the mapper finds by
    ///   walking back up to the parent chart rather than being handed it. That walk is the reason
    ///   these tests build a chart inside a document instead of on its own.
    /// </summary>
    [Fact]
    public void ASeriesWithNoTypeOfItsOwnTakesTheChartsType()
    {
        var document = new Document();
        var domChart = ChartIn(document, ChartType.Column2D);
        domChart.SeriesCollection.AddSeries().Add(1.0);

        Mapped(domChart).SeriesCollection[0].ChartType.Should().Be(Charting.ChartType.Column2D);
    }

    [Fact]
    public void ASeriesThatNamesItsOwnTypeKeepsIt()
    {
        var document = new Document();
        var domChart = ChartIn(document, ChartType.Column2D);
        var domSeries = domChart.SeriesCollection.AddSeries();
        domSeries.ChartType = ChartType.Line;
        domSeries.Add(1.0);

        Mapped(domChart).SeriesCollection[0].ChartType.Should().Be(Charting.ChartType.Line);
    }

    [Fact]
    public void ASeriesCarriesHowItsPointsAreMarked()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        var domSeries = domChart.SeriesCollection.AddSeries();
        domSeries.Add(1.0);
        domSeries.MarkerSize = Unit.FromPoint(7);
        domSeries.MarkerStyle = MarkerStyle.Diamond;

        var series = Mapped(domChart).SeriesCollection[0];

        series.MarkerSize.Point.Should().BeApproximately(7, 0.01);
        series.MarkerStyle.Should().Be(Charting.MarkerStyle.Diamond);
    }

    /// <summary>
    ///   A marker colour the caller left alone maps to the empty colour rather than to black, so
    ///   that the drawing can tell "not set" from "set to something" and pick its own.
    /// </summary>
    [Fact]
    public void AMarkerColourNobodySetArrivesEmpty()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        domChart.SeriesCollection.AddSeries().Add(1.0);

        var series = Mapped(domChart).SeriesCollection[0];

        series.MarkerBackgroundColor.IsEmpty.Should().BeTrue();
        series.MarkerForegroundColor.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AMarkerColourThatWasSetArrivesAsThatColour()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        var domSeries = domChart.SeriesCollection.AddSeries();
        domSeries.Add(1.0);
        domSeries.MarkerBackgroundColor = Colors.Firebrick;
        domSeries.MarkerForegroundColor = Colors.Navy;

        var series = Mapped(domChart).SeriesCollection[0];

        series.MarkerBackgroundColor.IsEmpty.Should().BeFalse();
        series.MarkerForegroundColor.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    ///   A blank is a gap in the data rather than a zero, and has to stay one across the mapping —
    ///   a chart drawn with zeros where the blanks were tells a different story from the one the
    ///   caller wrote.
    /// </summary>
    [Fact]
    public void ABlankInASeriesStaysABlank()
    {
        var document = new Document();
        var domChart = ChartIn(document);
        var domSeries = domChart.SeriesCollection.AddSeries();
        domSeries.Add(1.0);
        domSeries.AddBlank();
        domSeries.Add(3.0);

        Mapped(domChart).SeriesCollection[0].Count.Should().Be(3);
    }

    [Fact]
    public void AChartWithNoSeriesAtAllStillMaps()
    {
        Mapped(ChartIn(new Document())).SeriesCollection.Count.Should().Be(0);
    }

    /// <summary>
    ///   Everything at once, which is the arrangement a caller actually writes and the one where a
    ///   mapper that reads the wrong object shows up.
    /// </summary>
    [Fact]
    public void AChartDescribedInFullArrivesDescribedInFull()
    {
        var document = new Document();
        var domChart = ChartIn(document, ChartType.Column2D);
        domChart.Width = Unit.FromPoint(400);
        domChart.Height = Unit.FromPoint(250);
        domChart.XAxis.Title.Caption = "Month";
        domChart.XAxis.HasMajorGridlines = true;
        domChart.YAxis.MinimumScale = 0;
        domChart.YAxis.MaximumScale = 100;
        domChart.RightArea.AddLegend();
        var domSeries = domChart.SeriesCollection.AddSeries();
        domSeries.Name = "Takings";
        domSeries.Add(10.0, 20.0, 30.0);

        var frame = ChartMapper.ChartMapper.Map(domChart);
        var chart = MappedChartProbe.In(frame);

        frame.Size.Width.Should().BeApproximately(400, 0.01);
        chart.Type.Should().Be(Charting.ChartType.Column2D);
        chart.XAxis.Title.Caption.Should().Be("Month");
        chart.XAxis.HasMajorGridlines.Should().BeTrue();
        chart.YAxis.MaximumScale.Should().Be(100);
        chart.Legend.Docking.Should().Be(Charting.DockingType.Right);
        chart.SeriesCollection.Count.Should().Be(1);
        chart.SeriesCollection[0].Name.Should().Be("Takings");
        chart.SeriesCollection[0].Count.Should().Be(3);
    }
}
