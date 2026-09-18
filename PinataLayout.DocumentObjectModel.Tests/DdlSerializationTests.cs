using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using PdfPinata.Text;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Writing the model back out as MDDDL. Every <c>Serialize</c> in the DOM is a long method with
///   an arm per property, and the four longest - <c>Font</c>, <c>ParagraphFormat</c>,
///   <c>Borders</c> and <c>Chart</c> - are where a property quietly stops being written.
///   <para>
///   A round trip is what catches that. Reading the text back and comparing the model against the
///   one it came from tests the writer and the reader together and needs no expected string to be
///   kept in step by hand; the few assertions on the text itself are for the shape of the output
///   rather than its contents, where a round trip cannot tell the difference.
///   </para>
/// </summary>
public class DdlSerializationTests
{
    static Document RoundTrip(Document document) =>
        DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

    static Document DocumentWithAParagraph(out Paragraph paragraph)
    {
        var document = new Document();
        paragraph = document.AddSection().AddParagraph();
        return document;
    }

    // ----- Font.Serialize ----------------------------------------------------------------------------

    [Fact]
    public void EveryThingAFontCanSayIsWrittenAndReadBack()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var font = paragraph.Format.Font;
        font.Name = "Palatino";
        font.Size = 13.5;
        font.Bold = true;
        font.Italic = true;
        font.Underline = Underline.Dotted;
        font.Color = Colors.DarkGreen;
        font.Superscript = true;

        var again = RoundTrip(document).LastSection.Elements[0] as Paragraph;

        again.Format.Font.Name.Should().Be("Palatino");
        again.Format.Font.Size.Point.Should().BeApproximately(13.5, 1e-4);
        again.Format.Font.Bold.Should().BeTrue();
        again.Format.Font.Italic.Should().BeTrue();
        again.Format.Font.Underline.Should().Be(Underline.Dotted);
        again.Format.Font.Color.Should().Be(Colors.DarkGreen);
        again.Format.Font.Superscript.Should().BeTrue();
    }

    [Fact]
    public void SubscriptAndSuperscriptAreWrittenSeparatelyRatherThanAsOneChoice()
    {
        // They are two properties in the model and one thing in a document, so the serializer has
        // to write whichever was set without inventing the other.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.Format.Font.Subscript = true;

        var again = RoundTrip(document).LastSection.Elements[0] as Paragraph;

        again.Format.Font.Subscript.Should().BeTrue();
        again.Format.Font.Superscript.Should().BeFalse();
    }

    [Fact]
    public void AFontThatSaysNothingWritesNothing()
    {
        // The paragraph on its own rather than the whole document: a document carries the built-in
        // styles, and some of those do describe a font, so "no font anywhere" was never the
        // property - it is that a paragraph nobody has formatted writes no format block.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddText("text");

        var plain = DdlWriter.WriteToString(paragraph);
        paragraph.Format.Font.Bold = true;
        var formatted = DdlWriter.WriteToString(paragraph);

        plain.Should().NotContain("Font", "an empty font block would be noise in every file");
        plain.Should().NotContain("Format", "nor an empty format block around it");
        formatted.Should().Contain("Font", "and one that says something is written");
    }

    // ----- ParagraphFormat.Serialize -------------------------------------------------------------------

    [Fact]
    public void EveryThingAParagraphFormatCanSayIsWrittenAndReadBack()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var format = paragraph.Format;
        format.Alignment = ParagraphAlignment.Justify;
        format.FirstLineIndent = "1cm";
        format.LeftIndent = "2cm";
        format.RightIndent = "3cm";
        format.SpaceBefore = "4pt";
        format.SpaceAfter = "5pt";
        format.LineSpacing = "13pt";
        format.LineSpacingRule = LineSpacingRule.Exactly;
        format.KeepTogether = true;
        format.KeepWithNext = true;
        format.PageBreakBefore = true;
        format.OutlineLevel = OutlineLevel.Level3;
        format.TextDirection = BidiParagraphDirection.RightToLeft;

        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format;

        again.Alignment.Should().Be(ParagraphAlignment.Justify);
        again.FirstLineIndent.Centimeter.Should().BeApproximately(1, 1e-4);
        again.LeftIndent.Centimeter.Should().BeApproximately(2, 1e-4);
        again.RightIndent.Centimeter.Should().BeApproximately(3, 1e-4);
        again.SpaceBefore.Point.Should().BeApproximately(4, 1e-4);
        again.SpaceAfter.Point.Should().BeApproximately(5, 1e-4);
        again.LineSpacing.Point.Should().BeApproximately(13, 1e-4);
        again.LineSpacingRule.Should().Be(LineSpacingRule.Exactly);
        again.KeepTogether.Should().BeTrue();
        again.KeepWithNext.Should().BeTrue();
        again.PageBreakBefore.Should().BeTrue();
        again.OutlineLevel.Should().Be(OutlineLevel.Level3);
        again.TextDirection.Should().Be(BidiParagraphDirection.RightToLeft);
    }

    [Fact]
    public void TheTabStopsOfAParagraphSurviveWithTheirLeadersAndAlignments()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.Format.TabStops.AddTabStop("4cm", TabAlignment.Right, TabLeader.Dots);
        paragraph.Format.TabStops.AddTabStop("8cm", TabAlignment.Decimal);

        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format;

        again.TabStops.Count.Should().Be(2);
        again.TabStops[0].Position.Centimeter.Should().BeApproximately(4, 1e-4);
        again.TabStops[0].Alignment.Should().Be(TabAlignment.Right);
        again.TabStops[0].Leader.Should().Be(TabLeader.Dots);
        again.TabStops[1].Alignment.Should().Be(TabAlignment.Decimal);
    }

    [Fact]
    public void TheShadingBehindAParagraphSurvives()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.Format.Shading.Color = Colors.LightYellow;

        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format;

        again.Shading.Color.Should().Be(Colors.LightYellow);
    }

    // ----- Borders.Serialize ---------------------------------------------------------------------------

    [Fact]
    public void ABorderSetOnEverySideAtOnceIsWrittenOnce()
    {
        // Setting the collection rather than a side is the shorthand, and the serializer is
        // expected to write it back as the shorthand rather than four times over.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.Format.Borders.Width = "2pt";
        paragraph.Format.Borders.Color = Colors.Navy;
        paragraph.Format.Borders.Style = BorderStyle.DashLargeGap;

        // The paragraph alone, not the whole document: an assertion that some word appears nowhere
        // in a document is hostage to everything else the document happens to contain, including
        // the built-in styles.
        var ddl = DdlWriter.WriteToString(paragraph);
        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format.Borders;

        again.Width.Point.Should().BeApproximately(2, 1e-4);
        again.Color.Should().Be(Colors.Navy);
        again.Style.Should().Be(BorderStyle.DashLargeGap);
        ddl.Should().NotContain("Top", "the shorthand covers every side");
    }

    [Fact]
    public void EachSideOfABorderKeepsWhatWasSaidAboutItAlone()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var borders = paragraph.Format.Borders;
        borders.Left.Width = "1pt";
        borders.Right.Width = "2pt";
        borders.Top.Color = Colors.Red;
        borders.Bottom.Style = BorderStyle.DashDot;

        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format.Borders;

        again.Left.Width.Point.Should().BeApproximately(1, 1e-4);
        again.Right.Width.Point.Should().BeApproximately(2, 1e-4);
        again.Top.Color.Should().Be(Colors.Red);
        again.Bottom.Style.Should().Be(BorderStyle.DashDot);
    }

    [Fact]
    public void TheDistanceFromTheTextToTheBorderSurvivesOnEverySide()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var borders = paragraph.Format.Borders;
        borders.DistanceFromLeft = "1mm";
        borders.DistanceFromRight = "2mm";
        borders.DistanceFromTop = "3mm";
        borders.DistanceFromBottom = "4mm";

        var again = (RoundTrip(document).LastSection.Elements[0] as Paragraph).Format.Borders;

        again.DistanceFromLeft.Millimeter.Should().BeApproximately(1, 1e-4);
        again.DistanceFromRight.Millimeter.Should().BeApproximately(2, 1e-4);
        again.DistanceFromTop.Millimeter.Should().BeApproximately(3, 1e-4);
        again.DistanceFromBottom.Millimeter.Should().BeApproximately(4, 1e-4);
    }

    [Fact]
    public void ACellsOwnBordersSurviveInsideATable()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn("3cm");
        table.AddColumn("3cm");
        var row = table.AddRow();
        row[0].Borders.Bottom.Width = "3pt";
        row[1].Shading.Color = Colors.WhiteSmoke;

        var again = RoundTrip(document).LastSection.Elements[0] as Table;

        again.Rows[0][0].Borders.Bottom.Width.Point.Should().BeApproximately(3, 1e-4);
        again.Rows[0][1].Shading.Color.Should().Be(Colors.WhiteSmoke);
    }

    // ----- Chart.Serialize -------------------------------------------------------------------------------

    [Fact]
    public void AChartIsWrittenWithItsTypeAndReadBackAsTheSameKind()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0, 3.0);
        chart.XValues.AddXSeries().Add("a", "b", "c");

        var again = RoundTrip(document).LastSection.Elements[0] as Chart;

        again.Should().NotBeNull();
        again.Type.Should().Be(ChartType.Line);
    }

    [Fact]
    public void AChartsAxesAndAreasSurviveTheRoundTrip()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Column2D);
        chart.SeriesCollection.AddSeries().Add(4.0);
        chart.YAxis.MinimumScale = 0;
        chart.YAxis.MaximumScale = 10;
        chart.YAxis.HasMajorGridlines = true;
        chart.HeaderArea.AddParagraph("Heading");
        chart.PlotArea.LeftPadding = "5mm";

        var again = RoundTrip(document).LastSection.Elements[0] as Chart;

        again.YAxis.MinimumScale.Should().Be(0);
        again.YAxis.MaximumScale.Should().Be(10);
        again.YAxis.HasMajorGridlines.Should().BeTrue();
        again.PlotArea.LeftPadding.Millimeter.Should().BeApproximately(5, 1e-4);
        DdlWriter.WriteToString(again.HeaderArea).Should().Contain("Heading");
    }

    [Fact]
    public void AChartKeepsTheNumbersInItsSeriesAndTheGapsBetweenThem()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0);
        series.AddBlank();
        series.Add(3.0);

        var again = RoundTrip(document).LastSection.Elements[0] as Chart;
        var againSeries = again.SeriesCollection[0];

        againSeries.Count.Should().Be(3);
        ((Shapes.Charts.Point)againSeries.Elements[0]).Value.Should().Be(1);
        againSeries.Elements[1].Should().BeNull("the gap is still a gap");
        ((Shapes.Charts.Point)againSeries.Elements[2]).Value.Should().Be(3);
    }

    // ----- the whole document ----------------------------------------------------------------------------

    [Fact]
    public void ADocumentWithSomethingOfEveryKindInItSurvives()
    {
        var document = new Document();
        document.Info.Title = "Everything";
        document.Info.Author = "Nobody";
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A5;
        section.PageSetup.LeftMargin = "2cm";
        section.AddParagraph("A paragraph");
        section.AddPageBreak();
        var table = section.AddTable();
        table.AddColumn("4cm");
        table.AddRow()[0].AddParagraph("In a cell");
        section.Headers.Primary.AddParagraph("A header");
        section.Footers.Primary.AddParagraph("A footer");

        var again = RoundTrip(document);

        again.Info.Title.Should().Be("Everything");
        again.Info.Author.Should().Be("Nobody");
        again.LastSection.PageSetup.PageFormat.Should().Be(PageFormat.A5);
        again.LastSection.PageSetup.LeftMargin.Centimeter.Should().BeApproximately(2, 1e-4);
        again.LastSection.Elements.Count.Should().Be(3);
        again.LastSection.Headers.Primary.Elements.Count.Should().Be(1);
        again.LastSection.Footers.Primary.Elements.Count.Should().Be(1);
    }

    // ----- Section.Serialize -----------------------------------------------------------------------------

    // A lone plain paragraph is written without its \paragraph keyword, and the parser takes bare
    // text only straight after a section's opening brace - so after a header or footer the section
    // has to write the keyword out, or it closes early and the body is read as trailing garbage.

    static string TextOf(DocumentObject element) =>
        string.Concat(((Paragraph)element).Elements.OfType<Text>().Select(t => t.Content));

    [Fact]
    public void ALonePlainParagraphAfterAPrimaryHeaderSurvives()
    {
        var document = new Document();
        var section = document.AddSection();
        section.Headers.Primary.AddParagraph("H");
        section.AddParagraph("Body");

        var again = RoundTrip(document).LastSection;

        TextOf(again.Headers.Primary.Elements[0]!).Should().Be("H");
        again.Elements.Count.Should().Be(1);
        TextOf(again.Elements[0]!).Should().Be("Body");
    }

    [Fact]
    public void ALonePlainParagraphAfterAPrimaryFooterSurvives()
    {
        var document = new Document();
        var section = document.AddSection();
        section.Footers.Primary.AddParagraph("F");
        section.AddParagraph("Body");

        var again = RoundTrip(document).LastSection;

        TextOf(again.Footers.Primary.Elements[0]!).Should().Be("F");
        again.Elements.Count.Should().Be(1);
        TextOf(again.Elements[0]!).Should().Be("Body");
    }

    [Fact]
    public void ALonePlainParagraphAfterAnEvenPageHeaderSurvives()
    {
        var document = new Document();
        var section = document.AddSection();
        section.Headers.EvenPage.AddParagraph("E");
        section.AddParagraph("Body");

        var again = RoundTrip(document).LastSection;

        TextOf(again.Headers.EvenPage.Elements[0]!).Should().Be("E");
        again.Elements.Count.Should().Be(1);
        TextOf(again.Elements[0]!).Should().Be("Body");
    }

    [Fact]
    public void ALonePlainParagraphInASectionWithNoHeaderIsStillWrittenAsBareText()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Body");

        DdlWriter.WriteToString(document).Should().NotContain("\\paragraph");
    }
}
