using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Two things a <see cref="Chart"/> has to do for itself, because a chart holds more child
///   objects than anything else in the DOM and the generated machinery does not cover either.
///   <para>
///   <c>DeepCopy</c> clones fourteen children by hand and reparents each one, so a child left out
///   is shared between the copy and the original rather than copied - which is invisible until
///   something writes to one of them. <c>CheckTextArea</c> answers which of the six areas a given
///   one is, by reference, and it is how each area knows the keyword to write itself as.
///   </para>
/// </summary>
public class ChartDomTests
{
    /// <summary>A chart with every child object it can have brought into being.</summary>
    static Chart AFullyPopulatedChart()
    {
        var chart = new Document().AddSection().AddChart(ChartType.Line);

        chart.Format.Font.Name = "Palatino";
        chart.XAxis.Title.Caption = "across";
        chart.YAxis.Title.Caption = "up";
        chart.ZAxis.Title.Caption = "through";
        chart.XValues.AddXSeries().Add("one", "two");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0);
        chart.HeaderArea.AddParagraph("header");
        chart.FooterArea.AddParagraph("footer");
        chart.TopArea.AddParagraph("top");
        chart.BottomArea.AddParagraph("bottom");
        chart.LeftArea.AddParagraph("left");
        chart.RightArea.AddParagraph("right");
        chart.PlotArea.LeftPadding = Unit.FromCentimeter(1);
        chart.DataLabel.Format = "0.0";

        return chart;
    }

    // ----- DeepCopy ------------------------------------------------------------------------------

    [Fact]
    public void ACopiedChartKeepsWhatTheOriginalSaid()
    {
        var chart = AFullyPopulatedChart();

        var copy = chart.Clone();

        copy.Format.Font.Name.Should().Be("Palatino");
        copy.XAxis.Title.Caption.Should().Be("across");
        copy.YAxis.Title.Caption.Should().Be("up");
        copy.ZAxis.Title.Caption.Should().Be("through");
        copy.SeriesCollection.Count.Should().Be(1);
        copy.XValues.Count.Should().Be(1);
        copy.PlotArea.LeftPadding.Centimeter.Should().BeApproximately(1, 1e-4);
        copy.DataLabel.Format.Should().Be("0.0");
    }

    /// <summary>
    ///   The assertion that matters for a deep copy: not that a copy came back, but that writing
    ///   to the original afterwards leaves the copy where it was. A child that was referenced
    ///   rather than cloned passes every other test and fails this one.
    /// </summary>
    [Fact]
    public void WritingToTheOriginalAfterwardsDoesNotReachTheCopy()
    {
        var chart = AFullyPopulatedChart();
        var copy = chart.Clone();

        chart.Format.Font.Name = "Courier New";
        chart.XAxis.Title.Caption = "changed";
        chart.YAxis.Title.Caption = "changed";
        chart.ZAxis.Title.Caption = "changed";
        chart.PlotArea.LeftPadding = Unit.FromCentimeter(9);
        chart.DataLabel.Format = "0.000";
        chart.SeriesCollection.AddSeries().Add(3.0);
        chart.XValues.AddXSeries().Add("three");

        copy.Format.Font.Name.Should().Be("Palatino");
        copy.XAxis.Title.Caption.Should().Be("across");
        copy.YAxis.Title.Caption.Should().Be("up");
        copy.ZAxis.Title.Caption.Should().Be("through");
        copy.PlotArea.LeftPadding.Centimeter.Should().BeApproximately(1, 1e-4);
        copy.DataLabel.Format.Should().Be("0.0");
        copy.SeriesCollection.Count.Should().Be(1);
        copy.XValues.Count.Should().Be(1);
    }

    [Theory]
    [InlineData("HeaderArea")]
    [InlineData("FooterArea")]
    [InlineData("TopArea")]
    [InlineData("BottomArea")]
    [InlineData("LeftArea")]
    [InlineData("RightArea")]
    public void EveryTextAreaIsCopiedRatherThanShared(string areaName)
    {
        var chart = AFullyPopulatedChart();
        var copy = chart.Clone();

        AreaOf(chart, areaName).AddParagraph("added after copying");

        AreaOf(copy, areaName).Elements.Count.Should()
            .Be(1, "the copy's {0} is its own", areaName);
    }

    [Fact]
    public void ACopiedChartCanBePutInAnotherDocumentAndWrittenThere()
    {
        // The other half of a deep copy is reparenting each clone onto the copy. A child still
        // pointing at the original's chart would resolve the wrong document for its styles, and
        // an area would ask the wrong chart which of the six it is - so it would be written under
        // the wrong keyword, or none.
        var original = AFullyPopulatedChart();
        var elsewhere = new Document();
        elsewhere.AddSection().Elements.Add(original.Clone());

        var written = DdlWriter.WriteToString(elsewhere);

        foreach (var keyword in new[]
                 { "headerarea", "footerarea", "toparea", "bottomarea", "leftarea", "rightarea" })
            written.Should().Contain("\\" + keyword, "the copied {0} knows what it is", keyword);
    }

    [Fact]
    public void ACopyOfAnEmptyChartIsStillAChart()
    {
        // Every one of the fourteen clones is guarded by a null check, and a chart with nothing
        // on it takes none of them.
        var copy = new Document().AddSection().AddChart(ChartType.Pie2D).Clone();

        copy.Should().NotBeNull();
        copy.Type.Should().Be(ChartType.Pie2D);
    }

    static TextArea AreaOf(Chart chart, string areaName) => areaName switch
    {
        "HeaderArea" => chart.HeaderArea,
        "FooterArea" => chart.FooterArea,
        "TopArea" => chart.TopArea,
        "BottomArea" => chart.BottomArea,
        "LeftArea" => chart.LeftArea,
        "RightArea" => chart.RightArea,
        _ => throw new System.ArgumentOutOfRangeException(nameof(areaName)),
    };

    // ----- CheckTextArea -------------------------------------------------------------------------

    /// <summary>
    ///   An area has no name of its own; it finds out which one it is by asking its chart, which
    ///   compares it against each of the six by reference. The answer is the keyword the area is
    ///   written as, so serializing a chart is what asks the question.
    /// </summary>
    [Theory]
    [InlineData("HeaderArea", "headerarea")]
    [InlineData("FooterArea", "footerarea")]
    [InlineData("TopArea", "toparea")]
    [InlineData("BottomArea", "bottomarea")]
    [InlineData("LeftArea", "leftarea")]
    [InlineData("RightArea", "rightarea")]
    public void EachAreaIsWrittenUnderTheKeywordThatNamesIt(string areaName, string keyword)
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        AreaOf(chart, areaName).AddParagraph("here");

        DdlWriter.WriteToString(document).Should().Contain("\\" + keyword);
    }

    [Fact]
    public void EachOfTheSixIsToldApartFromTheOtherFive()
    {
        // All six populated at once, which is the case where getting the comparison wrong would
        // write one area's contents under another's keyword.
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        foreach (var (areaName, keyword) in new[]
        {
            ("HeaderArea", "headerarea"), ("FooterArea", "footerarea"),
            ("TopArea", "toparea"), ("BottomArea", "bottomarea"),
            ("LeftArea", "leftarea"), ("RightArea", "rightarea"),
        })
            AreaOf(chart, areaName).AddParagraph(keyword + " content");

        var written = DdlWriter.WriteToString(document);

        var reread = DdlReader.DocumentFromString(written)
            .LastSection.Elements.OfType<Chart>().Single();

        foreach (var (areaName, keyword) in new[]
        {
            ("HeaderArea", "headerarea"), ("FooterArea", "footerarea"),
            ("TopArea", "toparea"), ("BottomArea", "bottomarea"),
            ("LeftArea", "leftarea"), ("RightArea", "rightarea"),
        })
        {
            var paragraph = AreaOf(reread, areaName).Elements[0] as Paragraph;
            string.Concat(paragraph.Elements.OfType<Text>().Select(t => t.Content))
                .Should().Be(keyword + " content", "{0} kept its own contents", areaName);
        }
    }
}
