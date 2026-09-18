using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The chart half of the object model: a <see cref="Chart"/> and the axes, areas, series and
///   points hanging off it. Nothing here draws anything - a chart in the DOM is a description of
///   one, and turning it into marks on a page is <c>PdfPinata.Charting</c>'s job.
///   <para>
///   What is worth pinning is that the description reads back the way it was written. Almost every
///   member of these classes is a lazily created child object or a value-model property, both of
///   which are generated rather than written by hand, so the risk is not that one of them is
///   subtly wrong - it is that a whole class is wired up to the wrong meta and nobody notices,
///   because a chart that renders as nothing looks much like a chart with nothing in it.
///   </para>
/// </summary>
public class ChartModelTests
{
    // ----- the chart itself ------------------------------------------------------------------------

    [Fact]
    public void AChartRemembersTheKindOfChartItIs()
    {
        new Chart(ChartType.Line).Type.Should().Be(ChartType.Line);
        new Chart(ChartType.Pie2D).Type.Should().Be(ChartType.Pie2D);
    }

    [Fact]
    public void AChartMadeWithNoTypeCanBeToldOneAfterwards()
    {
        var chart = new Chart { Type = ChartType.Bar2D };

        chart.Type.Should().Be(ChartType.Bar2D);
    }

    [Fact]
    public void AChartOffersAnAreaOnEverySideAndOneInTheMiddle()
    {
        // Six text areas and a plot area, each created on first mention rather than up front. A
        // property that returned null here would be a chart that cannot be given a heading.
        var chart = new Chart(ChartType.Column2D);

        chart.HeaderArea.Should().NotBeNull();
        chart.FooterArea.Should().NotBeNull();
        chart.TopArea.Should().NotBeNull();
        chart.BottomArea.Should().NotBeNull();
        chart.LeftArea.Should().NotBeNull();
        chart.RightArea.Should().NotBeNull();
        chart.PlotArea.Should().NotBeNull();
    }

    [Fact]
    public void EachAreaIsTheSameOneEveryTimeItIsAskedFor()
    {
        // Lazily created, but created once: an area handed a paragraph and then asked for again
        // has to be the area that has the paragraph in it.
        var chart = new Chart(ChartType.Column2D);

        chart.HeaderArea.AddParagraph("Sales");

        chart.HeaderArea.Elements.Count.Should().Be(1);
        chart.HeaderArea.Should().BeSameAs(chart.HeaderArea);
        chart.PlotArea.Should().BeSameAs(chart.PlotArea);
    }

    [Fact]
    public void AChartHasThreeAxesAndTheyAreNotEachOther()
    {
        var chart = new Chart(ChartType.Line);

        chart.XAxis.MajorTick = 5;
        chart.YAxis.MajorTick = 10;

        chart.XAxis.MajorTick.Should().Be(5);
        chart.YAxis.MajorTick.Should().Be(10, "the axes are separate objects");
        chart.ZAxis.Should().NotBeNull();
    }

    /// <summary>
    ///   <c>HasDataLabel</c> reads like "is there a data label on this chart" and is not that: it
    ///   is a flag of its own, and filling in <c>DataLabel</c> does not raise it. A caller who
    ///   describes the label and never sets the flag has described a label that will not be drawn.
    /// </summary>
    [Fact]
    public void ADataLabelIsDescribedInOnePlaceAndSwitchedOnInAnother()
    {
        var chart = new Chart(ChartType.Pie2D);

        chart.HasDataLabel.Should().BeFalse();

        chart.DataLabel.Position = DataLabelPosition.Center;

        chart.HasDataLabel.Should().BeFalse("describing the label does not ask for it");

        chart.HasDataLabel = true;

        chart.HasDataLabel.Should().BeTrue();
        chart.DataLabel.Position.Should().Be(DataLabelPosition.Center, "and the description stands");
    }

    [Fact]
    public void AClonedChartCarriesItsContentsAndNotItsIdentity()
    {
        var chart = new Chart(ChartType.Line);
        chart.HeaderArea.AddParagraph("Before");
        chart.XAxis.MajorTick = 4;

        var copy = chart.Clone();
        copy.XAxis.MajorTick = 9;

        copy.Type.Should().Be(ChartType.Line);
        copy.HeaderArea.Elements.Count.Should().Be(1);
        copy.HeaderArea.Should().NotBeSameAs(chart.HeaderArea, "a copy is a copy all the way down");
        chart.XAxis.MajorTick.Should().Be(4, "changing the copy does not change the original");
    }

    // ----- series and the numbers in them ------------------------------------------------------------

    [Fact]
    public void ASeriesHoldsTheNumbersItIsGiven()
    {
        var chart = new Chart(ChartType.Column2D);

        var series = chart.SeriesCollection.AddSeries();
        series.Add(3.0, 4.0, 5.0);

        series.Count.Should().Be(3);
        PointAt(series, 0).Value.Should().Be(3);
        PointAt(series, 2).Value.Should().Be(5);
    }

    // The elements of a series are DocumentObjects, because a blank is one of the things a series
    // can hold and a blank is no point at all.
    static Point PointAt(Series series, int index) => (Point)series.Elements[index];

    [Fact]
    public void AddingOneNumberHandsBackThePointItBecame()
    {
        var series = new Chart(ChartType.Line).SeriesCollection.AddSeries();

        var point = series.Add(7.5);

        point.Should().NotBeNull();
        point.Value.Should().Be(7.5);
        series.Count.Should().Be(1);
    }

    [Fact]
    public void ABlankIsAGapInTheSeriesRatherThanAZero()
    {
        // A blank counts towards the length of the series - the points after it keep their
        // positions - but stands for no value, which is why DisplayBlanksAs exists.
        var series = new Chart(ChartType.Line).SeriesCollection.AddSeries();

        series.Add(1.0);
        series.AddBlank();
        series.Add(3.0);

        series.Count.Should().Be(3);
        series.Elements[1].Should().BeNull("a blank is the absence of a point");
        PointAt(series, 2).Value.Should().Be(3);
    }

    [Fact]
    public void EverySeriesAddedIsItsOwn()
    {
        var chart = new Chart(ChartType.Column2D);

        var first = chart.SeriesCollection.AddSeries();
        var second = chart.SeriesCollection.AddSeries();
        first.Name = "Actual";
        second.Name = "Forecast";

        chart.SeriesCollection.Count.Should().Be(2);
        chart.SeriesCollection[0].Name.Should().Be("Actual");
        chart.SeriesCollection[1].Name.Should().Be("Forecast");
    }

    [Fact]
    public void ASeriesCanBeDrawnDifferentlyFromTheChartAroundIt()
    {
        // Which is what makes a combination chart: a line drawn over columns is one series saying
        // it is a line while the chart says columns.
        var chart = new Chart(ChartType.Column2D);
        var series = chart.SeriesCollection.AddSeries();

        series.ChartType = ChartType.Line;
        series.MarkerStyle = MarkerStyle.Diamond;
        series.MarkerSize = Unit.FromPoint(4);

        series.ChartType.Should().Be(ChartType.Line);
        series.MarkerStyle.Should().Be(MarkerStyle.Diamond);
        series.MarkerSize.Point.Should().Be(4);
        chart.Type.Should().Be(ChartType.Column2D);
    }

    [Fact]
    public void ASeriesDataLabelWorksTheSameTwoStepWay()
    {
        var series = new Chart(ChartType.Line).SeriesCollection.AddSeries();

        series.DataLabel.Format = "0.0";

        series.HasDataLabel.Should().BeFalse("as on the chart, the flag is separate");

        series.HasDataLabel = true;

        series.HasDataLabel.Should().BeTrue();
        series.DataLabel.Format.Should().Be("0.0");
    }

    // ----- the labels along the bottom -----------------------------------------------------------------

    [Fact]
    public void TheNamesAlongTheAxisAreASeriesOfTheirOwn()
    {
        // An XSeries holds its values privately - it is not a collection a caller can index - so
        // what it holds is read back the way the file reads it, through the serializer.
        var chart = new Chart(ChartType.Column2D);

        var names = chart.XValues.AddXSeries();
        names.Add("Jan", "Feb", "Mar");

        var ddl = DdlWriter.WriteToString(chart);
        ddl.Should().Contain("Jan").And.Contain("Feb").And.Contain("Mar");
    }

    [Fact]
    public void AddingOneNameHandsBackTheValueItBecame()
    {
        var names = new Chart(ChartType.Column2D).XValues.AddXSeries();

        var value = names.Add("Q1");

        value.Should().NotBeNull();
        DdlWriter.WriteToString(value).Should().Contain("Q1");
    }

    [Fact]
    public void ABlankNameIsAGapAlongTheAxis()
    {
        var chart = new Chart(ChartType.Column2D);
        var names = chart.XValues.AddXSeries();

        names.Add("Jan");
        names.AddBlank();
        names.Add("Mar");

        // Two names and the gap between them, which the serializer writes as an empty slot rather
        // than closing up.
        var ddl = DdlWriter.WriteToString(chart);
        ddl.Should().Contain("\"Jan\", null, \"Mar\"",
            "the blank keeps its place between the two names rather than closing up");
    }

    // ----- axes ------------------------------------------------------------------------------------------

    [Fact]
    public void AnAxisRemembersTheScaleAndTheTicksItWasGiven()
    {
        var axis = new Chart(ChartType.Line).YAxis;

        axis.MinimumScale = 0;
        axis.MaximumScale = 100;
        axis.MajorTick = 25;
        axis.MinorTick = 5;
        axis.MajorTickMark = TickMarkType.Outside;
        axis.MinorTickMark = TickMarkType.None;

        axis.MinimumScale.Should().Be(0);
        axis.MaximumScale.Should().Be(100);
        axis.MajorTick.Should().Be(25);
        axis.MinorTick.Should().Be(5);
        axis.MajorTickMark.Should().Be(TickMarkType.Outside);
        axis.MinorTickMark.Should().Be(TickMarkType.None);
    }

    [Fact]
    public void GridlinesAreDescribedInOnePlaceAndSwitchedOnInAnotherToo()
    {
        var axis = new Chart(ChartType.Line).YAxis;

        axis.MajorGridlines.LineFormat.Width = Unit.FromPoint(0.5);

        axis.HasMajorGridlines.Should().BeFalse("describing them does not ask for them");

        axis.HasMajorGridlines = true;

        axis.HasMajorGridlines.Should().BeTrue();
        axis.HasMinorGridlines.Should().BeFalse("one does not bring the other");
        axis.MajorGridlines.LineFormat.Width.Point.Should().Be(0.5);
    }

    [Fact]
    public void AnAxisCarriesATitleAndTheLabelsAlongIt()
    {
        var axis = new Chart(ChartType.Line).XAxis;

        axis.Title.Caption = "Month";
        axis.TickLabels.Format = "0";

        axis.Title.Caption.Should().Be("Month");
        axis.TickLabels.Format.Should().Be("0");
    }

    // ----- the areas -------------------------------------------------------------------------------------

    [Fact]
    public void ATextAreaTakesTheSameThingsAParagraphContainerDoes()
    {
        var area = new Chart(ChartType.Column2D).HeaderArea;

        area.AddParagraph("Quarterly sales");
        area.AddTable();
        area.AddLegend();

        area.Elements.Count.Should().Be(3);
    }

    [Fact]
    public void ATextAreaRemembersItsSizeAndItsPadding()
    {
        var area = new Chart(ChartType.Column2D).FooterArea;

        area.Height = "2cm";
        area.Width = "5cm";
        area.LeftPadding = "3mm";
        area.RightPadding = "3mm";

        area.Height.Centimeter.Should().BeApproximately(2, 1e-6);
        area.Width.Centimeter.Should().BeApproximately(5, 1e-6);
        area.LeftPadding.Millimeter.Should().BeApproximately(3, 1e-6);
        area.RightPadding.Millimeter.Should().BeApproximately(3, 1e-6);
    }

    [Fact]
    public void ThePlotAreaIsPaddedOnEverySideIndependently()
    {
        var plot = new Chart(ChartType.Column2D).PlotArea;

        plot.LeftPadding = "1cm";
        plot.RightPadding = "2cm";
        plot.TopPadding = "3cm";
        plot.BottomPadding = "4cm";

        plot.LeftPadding.Centimeter.Should().BeApproximately(1, 1e-6);
        plot.RightPadding.Centimeter.Should().BeApproximately(2, 1e-6);
        plot.TopPadding.Centimeter.Should().BeApproximately(3, 1e-6);
        plot.BottomPadding.Centimeter.Should().BeApproximately(4, 1e-6);
    }

    [Fact]
    public void APointCarriesItsOwnLineAndFillRatherThanTheSeriesOne()
    {
        // Which is how one column in a bar chart is picked out in a different colour.
        var series = new Chart(ChartType.Bar2D).SeriesCollection.AddSeries();
        series.Add(1.0, 2.0);

        PointAt(series, 1).FillFormat.Color = Colors.Red;

        PointAt(series, 1).FillFormat.Color.Should().Be(Colors.Red);
        PointAt(series, 0).FillFormat.Color.Should().Be(Color.Empty, "only the one asked for");
    }
}
