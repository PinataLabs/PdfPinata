using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The children hanging off a <see cref="Chart"/>, from the other side to
///   <see cref="ChartModelTests"/>: that one reads each child where the chart creates it, and this
///   one writes to each child and hands the chart one of its own.
///   <para>
///   Every one of these classes is written the same way - a lazily created child behind a getter, a
///   setter that reparents whatever it is handed, and a <c>Serialize</c> that writes an attribute
///   only when the backing field was set. The getter is what a caller reaches for and so what gets
///   exercised; the setter and the written attribute are what nothing reaches, which is why a chart
///   assembled out of parts and a chart with every dial turned are what is pinned here. A setter
///   that forgot to reparent leaves a child whose <c>parent</c> is the object it came from, and the
///   DDL a chart writes is built by walking parents - so it writes the child under the wrong
///   heading, or under none.
///   </para>
/// </summary>
public class ChartChildObjectTests
{
    static Chart AChart() => new Document().AddSection().AddChart(ChartType.Column2D);

    static string DdlOf(DocumentObject documentObject) => DdlWriter.WriteToString(documentObject);

    // ----- built on their own -----------------------------------------------------------------

    /// <summary>
    ///   Each of these has a public parameterless constructor so that a caller can build one and
    ///   hand it to a chart rather than reaching through the chart for it. A parented constructor
    ///   is what the chart itself uses, and it is the only one anything here ever ran.
    /// </summary>
    [Fact]
    public void AChartChildCanBeBuiltWithNoParentAtAll()
    {
        new Axis().MajorTick.Should().Be(0);
        new AxisTitle().Caption.Should().BeEmpty();
        new TickLabels().Format.Should().BeEmpty();
        new Gridlines().LineFormat.Should().NotBeNull();
        new Legend().Style.Should().BeEmpty();
        new DataLabel().Format.Should().BeEmpty();
        new Series().Count.Should().Be(0);
        new XSeries().Clone().Should().BeOfType<XSeries>();
        new XValues().Count.Should().Be(0);
        new Point(2.5).Value.Should().Be(2.5);
    }

    /// <summary>
    ///   <c>Clone</c> on each of these is one line over <c>DeepCopy</c>, and the one line is the
    ///   cast that gives the caller back its own type rather than a <c>DocumentObject</c>. A copy
    ///   that shared its children with the original would be invisible until something wrote to
    ///   one of them, so the values are read back off the copy and then the original is changed.
    /// </summary>
    [Fact]
    public void ACopyOfAChartChildCarriesTheValuesAndNotTheObjects()
    {
        var axis = new Axis { MajorTick = 5 };
        axis.TickLabels.Format = "0.00";

        var copy = axis.Clone();

        copy.Should().NotBeSameAs(axis);
        copy.MajorTick.Should().Be(5);
        copy.TickLabels.Should().NotBeSameAs(axis.TickLabels);
        copy.TickLabels.Format.Should().Be("0.00");

        axis.TickLabels.Format = "0";
        copy.TickLabels.Format.Should().Be("0.00");
    }

    [Fact]
    public void EveryChartChildCopiesItselfIntoItsOwnType()
    {
        new AxisTitle { Caption = "across" }.Clone().Caption.Should().Be("across");
        new TickLabels { Style = "Normal" }.Clone().Style.Should().Be("Normal");
        new Gridlines().Clone().Should().BeOfType<Gridlines>();
        new Legend { Style = "Normal" }.Clone().Style.Should().Be("Normal");
        new DataLabel { Format = "0.0" }.Clone().Format.Should().Be("0.0");
        new Series { Name = "sales" }.Clone().Name.Should().Be("sales");
        new Point(4).Clone().Value.Should().Be(4);
        AChart().PlotArea.Clone().Should().BeOfType<PlotArea>();
    }

    // ----- handed to a chart ------------------------------------------------------------------

    /// <summary>
    ///   An axis built elsewhere and assigned takes the place of the one the chart would have made,
    ///   and the chart has to become its parent - <c>CheckAxis</c> answers by reference, and it is
    ///   how the axis learns whether to write itself as <c>\xaxis</c>, <c>\yaxis</c> or
    ///   <c>\zaxis</c>.
    /// </summary>
    [Fact]
    public void AnAxisHandedToAChartIsTheAxisTheChartThenWrites()
    {
        var chart = AChart();

        chart.XAxis = new Axis { MajorTick = 7 };
        chart.YAxis = new Axis { MinorTick = 3 };
        chart.ZAxis = new Axis { MaximumScale = 90 };

        chart.XAxis.MajorTick.Should().Be(7);
        chart.YAxis.MinorTick.Should().Be(3);
        chart.ZAxis.MaximumScale.Should().Be(90);

        var ddl = DdlOf(chart);
        ddl.Should().Contain("\\xaxis").And.Contain("\\yaxis").And.Contain("\\zaxis");
        ddl.Should().Contain("MajorTick = 7").And.Contain("MinorTick = 3");
    }

    /// <summary>
    ///   A child taken from another chart has to be copied on the way across. The DOM refuses to
    ///   reparent an object that already has a parent, because the one it came from is still
    ///   holding it - the two charts would share a child and neither would know.
    /// </summary>
    [Fact]
    public void AChildThatAlreadyBelongsToAnotherChartIsRefusedRatherThanTakenAway()
    {
        var chart = AChart();
        var elsewhere = AChart();

        var taking = () => chart.XAxis = elsewhere.XAxis;

        taking.Should().Throw<System.ArgumentException>()
            .WithMessage("*must be cloned before set*");
    }

    [Fact]
    public void EveryOtherChildHandedToAChartIsTheOneTheChartThenUses()
    {
        var chart = AChart();
        var elsewhere = AChart();
        elsewhere.PlotArea.LeftPadding = Unit.FromPoint(6);
        elsewhere.HeaderArea.AddParagraph("from elsewhere");
        elsewhere.SeriesCollection.AddSeries().Add(1.0, 2.0);
        elsewhere.XValues.AddXSeries().Add("one", "two");

        chart.Format = new ParagraphFormat { SpaceBefore = Unit.FromPoint(4) };
        chart.SeriesCollection = elsewhere.SeriesCollection.Clone();
        chart.XValues = elsewhere.XValues.Clone();
        chart.PlotArea = elsewhere.PlotArea.Clone();
        chart.DataLabel = new DataLabel { Format = "0.0" };
        chart.HeaderArea = elsewhere.HeaderArea.Clone();
        chart.FooterArea = elsewhere.FooterArea.Clone();
        chart.TopArea = elsewhere.TopArea.Clone();
        chart.BottomArea = elsewhere.BottomArea.Clone();
        chart.LeftArea = elsewhere.LeftArea.Clone();
        chart.RightArea = elsewhere.RightArea.Clone();

        chart.Format.SpaceBefore.Point.Should().Be(4);
        chart.SeriesCollection.Count.Should().Be(1);
        chart.XValues.Count.Should().Be(1);
        chart.PlotArea.LeftPadding.Point.Should().Be(6);
        chart.DataLabel.Format.Should().Be("0.0");
        chart.HeaderArea.Elements.Count.Should().Be(1);
        chart.HeaderArea.Should().NotBeSameAs(elsewhere.HeaderArea);
        chart.FooterArea.Should().NotBeSameAs(elsewhere.FooterArea);
        chart.TopArea.Should().NotBeSameAs(elsewhere.TopArea);
        chart.BottomArea.Should().NotBeSameAs(elsewhere.BottomArea);
        chart.LeftArea.Should().NotBeSameAs(elsewhere.LeftArea);
        chart.RightArea.Should().NotBeSameAs(elsewhere.RightArea);
    }

    /// <summary>
    ///   A chart offers each of its six areas to a visitor, and only the ones that exist - which is
    ///   the whole reason they are created lazily. Flattening is the visitor the renderer runs, and
    ///   it is what copies a style down into the paragraphs that name it, so an area a chart failed
    ///   to offer reaches the renderer with nothing filled in.
    /// </summary>
    [Fact]
    public void FlatteningReachesTheParagraphInEveryAreaAChartHas()
    {
        var document = new Document();
        var loud = document.Styles.AddStyle("Loud", "Normal");
        loud.Font.Bold = true;

        var chart = document.AddSection().AddChart(ChartType.Column2D);
        foreach (var area in new[]
                 {
                     chart.HeaderArea, chart.FooterArea, chart.TopArea,
                     chart.BottomArea, chart.LeftArea, chart.RightArea
                 })
            area.AddParagraph("here").Style = "Loud";

        new Visitors.PdfFlattenVisitor().Visit(document);

        foreach (var area in new[]
                 {
                     chart.HeaderArea, chart.FooterArea, chart.TopArea,
                     chart.BottomArea, chart.LeftArea, chart.RightArea
                 })
            ((Paragraph)area.Elements[0]).Format.Font.Bold.Should().BeTrue();
    }

    // ----- what each child writes -------------------------------------------------------------

    /// <summary>
    ///   Each of these attributes is written only when its backing field was set, so a chart left
    ///   alone writes none of them and nothing but setting every one reaches every branch. The
    ///   names are worth reading: the two gridline flags are written <c>HasMajorGridLines</c> with
    ///   a capital L, which is not how the property is spelled, and a reader that expected the
    ///   property's own spelling would drop them without a word.
    /// </summary>
    [Fact]
    public void AnAxisWritesEveryDialThatWasTurned()
    {
        var chart = AChart();
        var axis = chart.XAxis;

        axis.MinimumScale = 0;
        axis.MaximumScale = 100;
        axis.MajorTick = 25;
        axis.MinorTick = 5;
        axis.HasMajorGridlines = true;
        axis.HasMinorGridlines = true;
        axis.MajorTickMark = TickMarkType.Outside;
        axis.MinorTickMark = TickMarkType.Cross;
        axis.Title.Caption = "across";
        axis.LineFormat.Width = Unit.FromPoint(2);
        axis.MajorGridlines.LineFormat.Width = Unit.FromPoint(1);
        axis.MinorGridlines.LineFormat.Width = Unit.FromPoint(0.5);
        axis.TickLabels.Format = "0.0";

        var ddl = DdlOf(chart);

        ddl.Should().Contain("MinimumScale = 0")
            .And.Contain("MaximumScale = 100")
            .And.Contain("MajorTick = 25")
            .And.Contain("MinorTick = 5")
            .And.Contain("HasMajorGridLines = true")
            .And.Contain("HasMinorGridLines = true")
            .And.Contain("MajorTickMark = Outside")
            .And.Contain("MinorTickMark = Cross")
            .And.Contain("MajorGridlines")
            .And.Contain("MinorGridlines")
            .And.Contain("TickLabels");
    }

    /// <summary>
    ///   A <see cref="Gridlines"/> has no name of its own. It writes itself under whichever of the
    ///   two names its axis says it is, and an axis asked about a gridlines object that is neither
    ///   of its own answers with an empty string rather than guessing.
    /// </summary>
    [Fact]
    public void GridlinesAreNamedByTheAxisTheyBelongTo()
    {
        var chart = AChart();

        chart.XAxis.MajorGridlines.LineFormat.Width = Unit.FromPoint(1);
        chart.XAxis.MinorGridlines.LineFormat.Width = Unit.FromPoint(2);

        DdlOf(chart).Should().Contain("MajorGridlines").And.Contain("MinorGridlines");
    }

    /// <summary>
    ///   An axis asked about a gridlines object that is no longer either of its own answers with an
    ///   empty name rather than guessing at one. Displacing a gridlines object is the only way to
    ///   get one whose parent is an axis that has stopped owning it, and a wrong guess here would
    ///   write it out under a heading that belongs to the object which replaced it.
    /// </summary>
    [Fact]
    public void GridlinesTheirAxisNoLongerOwnsAreWrittenUnderNoNameAtAll()
    {
        var axis = AChart().XAxis;
        var displaced = axis.MajorGridlines;
        displaced.LineFormat.Width = Unit.FromPoint(1);

        axis.MajorGridlines = new Gridlines();

        DdlOf(displaced).Should().NotContain("MajorGridlines").And.NotContain("MinorGridlines");
    }

    [Fact]
    public void GridlinesHandedALineFormatWriteThatOneRatherThanTheirOwn()
    {
        var chart = AChart();

        chart.XAxis.MajorGridlines.LineFormat = new LineFormat { Width = Unit.FromPoint(3) };

        chart.XAxis.MajorGridlines.LineFormat.Width.Point.Should().Be(3);
        DdlOf(chart).Should().Contain("MajorGridlines").And.Contain("Width = 3");
    }

    [Fact]
    public void TickLabelsWriteTheirStyleTheirFormatAndTheirFont()
    {
        var chart = AChart();
        var axis = chart.XAxis;

        axis.TickLabels.Style = "Normal";
        axis.TickLabels.Format = "0.00";
        axis.TickLabels.Font = new Font("Palatino") { Size = Unit.FromPoint(9) };

        axis.TickLabels.Font.Name.Should().Be("Palatino");

        var ddl = DdlOf(chart);
        ddl.Should().Contain("TickLabels")
            .And.Contain("Style = \"Normal\"")
            .And.Contain("Format = \"0.00\"")
            .And.Contain("Palatino");
    }

    [Fact]
    public void ADataLabelWritesItsStyleFormatPositionTypeAndFont()
    {
        var chart = AChart();

        chart.HasDataLabel = true;
        chart.DataLabel.Style = "Normal";
        chart.DataLabel.Format = "0.0";
        chart.DataLabel.Position = DataLabelPosition.OutsideEnd;
        chart.DataLabel.Type = DataLabelType.Percent;
        chart.DataLabel.Font = new Font("Palatino");

        chart.DataLabel.Font.Name.Should().Be("Palatino");

        var ddl = DdlOf(chart);
        ddl.Should().Contain("HasDataLabel = true")
            .And.Contain("Style = \"Normal\"")
            .And.Contain("Format = \"0.0\"")
            .And.Contain("Position = OutsideEnd")
            .And.Contain("Type = Percent")
            .And.Contain("Palatino");
    }

    /// <summary>
    ///   A legend says it is never null even when nothing was set on it, because a chart carrying
    ///   one is a chart that shows a legend - the object's presence is the setting. Everything else
    ///   in the DOM answers the opposite way, which is why this override is worth a test of its own.
    /// </summary>
    [Fact]
    public void ALegendIsNeverNullEvenWhenNothingWasSetOnIt()
    {
        new Legend().IsNull().Should().BeFalse();
    }

    [Fact]
    public void ALegendWritesItsStyleItsFormatAndItsBorder()
    {
        var chart = AChart();
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0, 2.0);

        var legend = chart.RightArea.AddLegend();
        legend.Style = "Normal";
        legend.Format = new ParagraphFormat { SpaceBefore = Unit.FromPoint(2) };
        legend.LineFormat = new LineFormat { Width = Unit.FromPoint(1) };

        legend.Format.SpaceBefore.Point.Should().Be(2);
        legend.LineFormat.Width.Point.Should().Be(1);

        var ddl = DdlOf(chart);
        ddl.Should().Contain("\\legend")
            .And.Contain("Style = \"Normal\"")
            .And.Contain("SpaceBefore = 2");
    }

    [Fact]
    public void APlotAreaWritesEveryPaddingAndBothOfItsFormats()
    {
        var chart = AChart();

        chart.PlotArea.TopPadding = Unit.FromPoint(1);
        chart.PlotArea.LeftPadding = Unit.FromPoint(2);
        chart.PlotArea.RightPadding = Unit.FromPoint(3);
        chart.PlotArea.BottomPadding = Unit.FromPoint(4);
        chart.PlotArea.LineFormat = new LineFormat { Width = Unit.FromPoint(5) };
        chart.PlotArea.FillFormat = new FillFormat { Color = Colors.Azure };

        chart.PlotArea.LineFormat.Width.Point.Should().Be(5);
        chart.PlotArea.FillFormat.Color.Should().Be(Colors.Azure);

        var ddl = DdlOf(chart);
        ddl.Should().Contain("TopPadding = 1")
            .And.Contain("LeftPadding = 2")
            .And.Contain("RightPadding = 3")
            .And.Contain("BottomPadding = 4");
    }

    /// <summary>
    ///   A point is written as a bare number when it has no formatting of its own, and that is the
    ///   path every existing series takes. Give it a line or a fill and it becomes a <c>\point</c>
    ///   block with the number inside - a different shape entirely, and the one nothing reached.
    /// </summary>
    [Fact]
    public void AFormattedPointIsWrittenAsABlockRatherThanABareNumber()
    {
        var chart = AChart();
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0, 2.0);

        DdlOf(chart).Should().NotContain("\\point");

        ((Point)series.Elements[0]).LineFormat = new LineFormat { Width = Unit.FromPoint(2) };
        ((Point)series.Elements[1]).FillFormat = new FillFormat { Color = Colors.Azure };

        DdlOf(chart).Should().Contain("\\point");
    }

    [Fact]
    public void ASeriesWritesItsNameItsMarkersAndItsFormats()
    {
        var chart = AChart();
        var series = chart.SeriesCollection.AddSeries();

        series.Add(1.0, 2.0);
        series.Name = "sales";
        series.MarkerSize = Unit.FromPoint(3);
        series.MarkerStyle = MarkerStyle.Diamond;
        series.MarkerForegroundColor = Colors.Azure;
        series.MarkerBackgroundColor = Colors.Beige;
        series.ChartType = ChartType.Line;
        series.LineFormat = new LineFormat { Width = Unit.FromPoint(2) };
        series.FillFormat = new FillFormat { Color = Colors.Coral };
        series.HasDataLabel = true;
        series.DataLabel = new DataLabel { Format = "0.0" };

        series.LineFormat.Width.Point.Should().Be(2);
        series.FillFormat.Color.Should().Be(Colors.Coral);
        series.DataLabel.Format.Should().Be("0.0");

        var ddl = DdlOf(chart);
        ddl.Should().Contain("Name = \"sales\"")
            .And.Contain("MarkerSize = 3")
            .And.Contain("MarkerStyle = Diamond")
            .And.Contain("ChartType = Line")
            .And.Contain("HasDataLabel = true");
    }

    /// <summary>
    ///   A series handed a whole element collection keeps the values that collection already holds,
    ///   which is the one way to build the numbers before the chart that shows them.
    /// </summary>
    [Fact]
    public void ASeriesHandedAnElementCollectionKeepsTheValuesInIt()
    {
        var built = new Series();
        built.Add(1.0, 2.0, 3.0);

        var chart = AChart();
        var series = chart.SeriesCollection.AddSeries();
        series.Elements = built.Elements.Clone();

        series.Count.Should().Be(3);
        ((Point)series.Elements[2]).Value.Should().Be(3);
    }

    [Fact]
    public void AChartWritesTheFourSettingsThatAreNotAboutOneChildOrAnother()
    {
        var chart = AChart();

        chart.DisplayBlanksAs = BlankType.Interpolated;
        chart.PivotChart = true;
        chart.HasDataLabel = true;
        chart.Style = "Normal";

        chart.DisplayBlanksAs.Should().Be(BlankType.Interpolated);
        chart.PivotChart.Should().BeTrue();

        var ddl = DdlOf(chart);
        ddl.Should().Contain("DisplayBlanksAs = Interpolated")
            .And.Contain("PivotChart = true")
            .And.Contain("HasDataLabel = true")
            .And.Contain("Style = \"Normal\"");
    }
}
