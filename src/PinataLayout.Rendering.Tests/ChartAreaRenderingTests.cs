using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A chart in a PinataLayout document is drawn inside six text areas — a header and a footer across
///   the whole width, and a top, bottom, left and right around the plot itself. Each one is laid
///   out and placed by a method of its own in <c>ChartRenderer</c>, and each of those measures the
///   ones already placed to know where it starts.
///   <para>
///   The positions are worked out from one another, so an area missing from a chart changes where
///   the rest go: the left area starts below the header and stops above the footer, the top area
///   starts below the header and runs between the left and the right. A chart carrying all six is
///   the only arrangement that reaches every one of those.
///   </para>
/// </summary>
public class ChartAreaRenderingTests
{
    static Document ADocumentWithAChart(bool withAreas)
    {
        var document = new Document();
        var section = document.AddSection();
        var chart = section.AddChart(ChartType.Column2D);
        chart.Width = Unit.FromCentimeter(12);
        chart.Height = Unit.FromCentimeter(8);

        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0, 4.0, 2.0, 7.0);
        chart.XValues.AddXSeries().Add("one", "two", "three", "four");

        if (withAreas)
        {
            // One word each, and each one different: a run is cut at every space, so a phrase
            // would never appear as a run of its own.
            chart.HeaderArea.AddParagraph("Alpha");
            chart.FooterArea.AddParagraph("Bravo");
            chart.TopArea.AddParagraph("Charlie");
            chart.BottomArea.AddParagraph("Delta");
            chart.LeftArea.AddParagraph("Echo");
            chart.RightArea.AddParagraph("Foxtrot");
        }

        return document;
    }

    [Fact]
    public void AChartWithNoAreasAtAllStillRenders()
    {
        var page = Rendered.FirstPageOf(ADocumentWithAChart(withAreas: false));

        page.Should().NotBeNull();
    }

    [Fact]
    public void AChartCarryingAllSixAreasDrawsEveryOneOfThem()
    {
        var page = Rendered.FirstPageOf(ADocumentWithAChart(withAreas: true));

        var runs = Glyphs.RunsOn(page);

        foreach (var word in new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot" })
            runs.Should().ContainEquivalentOf(Glyphs.For(word), "the chart draws " + word);
    }

    /// <summary>
    ///   The areas take room from the plot, so a chart of the same size with all six drawn has less
    ///   of itself left for the columns. The plot is drawn as filled rectangles, so what says so is
    ///   how tall the tallest of them is.
    /// </summary>
    [Fact]
    public void TheAreasTakeTheirRoomFromThePlot()
    {
        var bare = Rendered.FirstPageOf(ADocumentWithAChart(withAreas: false));
        var full = Rendered.FirstPageOf(ADocumentWithAChart(withAreas: true));

        Glyphs.On(full).Count.Should().BeGreaterThan(Glyphs.On(bare).Count,
            "the six areas are drawn as well as the plot");
    }

    [Fact]
    public void AChartWithAHeaderAndNoFooterPlacesTheSideAreasBelowTheHeader()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        chart.Width = Unit.FromCentimeter(10);
        chart.Height = Unit.FromCentimeter(6);
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);
        chart.HeaderArea.AddParagraph("Golf");
        chart.LeftArea.AddParagraph("Hotel");
        chart.RightArea.AddParagraph("India");

        var page = Rendered.FirstPageOf(document);

        var runs = Glyphs.RunsOn(page);
        foreach (var word in new[] { "Golf", "Hotel", "India" })
            runs.Should().ContainEquivalentOf(Glyphs.For(word));
    }

    [Fact]
    public void AChartWithAFooterAndNoHeaderStillPlacesItsTopArea()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Bar2D);
        chart.Width = Unit.FromCentimeter(10);
        chart.Height = Unit.FromCentimeter(6);
        chart.SeriesCollection.AddSeries().Add(3.0, 1.0, 2.0);
        chart.FooterArea.AddParagraph("Juliett");
        chart.TopArea.AddParagraph("Kilo");
        chart.BottomArea.AddParagraph("Lima");

        var page = Rendered.FirstPageOf(document);

        var runs = Glyphs.RunsOn(page);
        foreach (var word in new[] { "Juliett", "Kilo", "Lima" })
            runs.Should().ContainEquivalentOf(Glyphs.For(word));
    }

    /// <summary>
    ///   A legend in the footer is docked below the chart, and one with more entries than fit
    ///   across the chart wraps them onto more rows inside it - where it used to run on in a single
    ///   row centred on the chart, off both sides of it and off the page (empira/PDFsharp#306).
    ///   A pie with no data labels draws no text but its legend, so every run on the page is one.
    /// </summary>
    [Fact]
    public void AFooterLegendTooWideForTheChartStaysInsideIt()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Pie2D);
        chart.Width = Unit.FromCentimeter(12);
        chart.Height = Unit.FromCentimeter(8);

        var series = chart.SeriesCollection.AddSeries();
        var categories = chart.XValues.AddXSeries();
        for (var n = 1; n <= 12; n++)
        {
            series.Add(1.0);
            categories.Add($"Region {n}");
        }
        chart.FooterArea.AddLegend();

        var placed = Glyphs.PlacedOn(Rendered.FirstPageOf(document));

        placed.Should().HaveCount(12);
        // The chart draws in a space of its own, so its positions are measured from its left edge.
        placed.Should().OnlyContain(run => run.X >= 0 && run.X < Unit.FromCentimeter(12).Point,
            "every entry starts inside the chart");
        placed.Select(run => System.Math.Round(run.Y, 2)).Distinct().Should().HaveCountGreaterThan(1,
            "twelve entries do not fit in one row across twelve centimetres");
    }
}
