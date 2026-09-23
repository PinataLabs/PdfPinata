using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The six text areas around a chart - header, footer, top, bottom, left and right. One method
///   reads all six, and it has two quite different jobs depending on what it finds after the
///   brace: either the whole area is one run of paragraph content, written without saying so, or
///   it is a list of blocks each of which has to be dispatched by keyword.
///   <para>
///   The plot area in the middle is not one of them. It is a ChartObject rather than a TextArea
///   and has an overload of its own, which takes attributes and then discards whatever is between
///   the braces - the source marks that with an unanswered "ignore everything? warn?".
///   </para>
/// </summary>
public class DdlChartAreaTests
{
    private static Chart ChartFrom(string chartBody) =>
        DdlReader.DocumentFromString(
                "\\document{\\section{\\chart(Line){" + chartBody + "}}}")
            .LastSection.Elements[0] as Chart;

    private static string TextOf(Paragraph paragraph) =>
        string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content));

    private static IReadOnlyList<string> ComplaintsAbout(string ddl) =>
        ReaderDiagnostics.ComplaintsAbout(ddl);

    // ----- an area written as plain content ---------------------------------------------------

    [Fact]
    public void AnAreaCanBeWrittenAsNothingButItsText()
    {
        // The shorthand: no \paragraph, just the words. The area has to notice that what follows
        // the brace is paragraph content rather than a list of blocks.
        var chart = ChartFrom("\\headerarea{Sales by quarter}");

        TextOf(chart.HeaderArea.Elements[0] as Paragraph).Should().Be("Sales by quarter");
    }

    [Theory]
    [InlineData("headerarea")]
    [InlineData("footerarea")]
    [InlineData("toparea")]
    [InlineData("bottomarea")]
    [InlineData("leftarea")]
    [InlineData("rightarea")]
    public void EveryOneOfTheSixTextAreasIsReadOntoItself(string areaKeyword)
    {
        var chart = ChartFrom("\\" + areaKeyword + "{here}");

        var area = AreaNamed(chart, areaKeyword);
        area.Elements.Count.Should().Be(1);
        TextOf(area.Elements[0] as Paragraph).Should().Be("here");
    }

    private static TextArea AreaNamed(Chart chart, string areaKeyword) => areaKeyword switch
    {
        "headerarea" => chart.HeaderArea,
        "footerarea" => chart.FooterArea,
        "toparea" => chart.TopArea,
        "bottomarea" => chart.BottomArea,
        "leftarea" => chart.LeftArea,
        "rightarea" => chart.RightArea,
        _ => throw new ArgumentOutOfRangeException(nameof(areaKeyword))
    };

    // ----- an area written as a list of blocks -------------------------------------------------

    [Fact]
    public void AnAreaCanHoldParagraphsWrittenOutInFull()
    {
        var chart = ChartFrom("\\toparea{\\paragraph{first}\\paragraph{second}}");

        chart.TopArea.Elements.Count.Should().Be(2);
        TextOf(chart.TopArea.Elements[1] as Paragraph).Should().Be("second");
    }

    [Fact]
    public void AnAreaCanHoldALegend()
    {
        var chart = ChartFrom("\\rightarea{\\legend[Style = \"Normal\"]}");

        chart.RightArea.Elements.OfType<Legend>().Should().ContainSingle();
    }

    [Fact]
    public void AnAreaCanHoldATable()
    {
        var chart = ChartFrom(
            "\\bottomarea{\\table{\\columns{\\column[Width = \"2cm\"]}\\rows{\\row{\\cell{n}}}}}");

        var table = chart.BottomArea.Elements.OfType<Table>().Single();
        table.Columns.Count.Should().Be(1);
        TextOf(table[0, 0].Elements[0] as Paragraph).Should().Be("n");
    }

    [Fact]
    public void AnAreaCanHoldATextFrame()
    {
        var chart = ChartFrom("\\leftarea{\\textframe[Width = \"3cm\"]{\\paragraph{inside}}}");

        chart.LeftArea.Elements.OfType<Shapes.TextFrame>().Should().ContainSingle();
    }

    [Fact]
    public void AnAreaCanHoldMoreThanOneKindOfThingAtOnce()
    {
        // The loop's real job: it keeps dispatching until the closing brace, so the blocks need
        // not be of one kind.
        var chart = ChartFrom("\\toparea{\\paragraph{words}\\legend[Style = \"Normal\"]}");

        chart.TopArea.Elements.OfType<Paragraph>().Should().ContainSingle();
        chart.TopArea.Elements.OfType<Legend>().Should().ContainSingle();
    }

    // ----- the attributes and the empty cases ----------------------------------------------------

    [Fact]
    public void AnAreaCanCarryAttributesBeforeItsContent()
    {
        var chart = ChartFrom("\\headerarea[Style = \"Heading1\"]{titled}");

        chart.HeaderArea.Style.Should().Be("Heading1");
    }

    [Fact]
    public void AnAreaCanCarryAttributesAndNoContentAtAll()
    {
        // The early return: attributes, then no brace. Nothing follows and the area is still read.
        var chart = ChartFrom("\\headerarea[Style = \"Heading1\"]");

        chart.HeaderArea.Style.Should().Be("Heading1");
        chart.HeaderArea.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AnAreaCanBeEmpty()
    {
        ChartFrom("\\toparea{}").TopArea.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AKeywordThatIsNotAnAreaBlockIsComplainedAbout()
    {
        ComplaintsAbout("\\document{\\section{\\chart(Line){\\toparea{\\cell{stray}}}}}")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void AChartTypeThatIsNotOneIsNamedInTheComplaint()
    {
        ComplaintsAbout("\\document{\\section{\\chart(NoSuchType){\\toparea{x}}}}")
            .Should().Contain(complaint => complaint.Contains("NoSuchType"));
    }
}
