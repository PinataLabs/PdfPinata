using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   docs/specs/generated-serialization.md §4 calls byte-for-byte comparison the constraint that
///   makes touching <c>Serialize</c> safe: the generated member order is base-class-then-source-
///   position, the hand-written order is whatever the author typed, and the two agree only by
///   coincidence. A test that compares strings notices a reordering; this harness exists so that
///   noticing is automatic rather than something a future change has to remember to check for.
///   <para>
///   Six documents, each covering a corner the existing round-trip tests were written to pin -
///   nullable values left set and unset, a formatted-text font, cleared borders and shading, a
///   table, a chart and a styled paragraph - serialized once and compared against the DDL a
///   correct <c>Serialize</c> produces today. Until something deliberately changes what gets
///   written, every one of these must come back unchanged; a change that is not byte-identical
///   either reverts, or is a deliberate reordering the commit says so about.
///   </para>
/// </summary>
public class DdlByteComparisonHarnessTests
{
    static string Ddl(Document document) => DdlWriter.WriteToString(document);

    static Document NullableValuesSetAndUnset()
    {
        var document = new Document();
        document.Info.Title = "A title";
        document.FootnoteStartingNumber = 7;

        var section = document.AddSection();
        section.PageSetup.StartingNumber = 3;
        section.PageSetup.MirrorMargins = true;

        var paragraph = section.AddParagraph("Hello");
        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Name = "Arial";

        return document;
    }

    static Document FormattedTextFont()
    {
        var document = new Document();
        var formatted = document.AddSection().AddParagraph().AddFormattedText("text");
        formatted.Font.Name = "Times New Roman";
        formatted.Font.Size = Unit.FromPoint(14);
        formatted.Font.Bold = true;
        formatted.Font.Italic = true;
        formatted.Font.Underline = Underline.Dash;
        formatted.Font.Strikethrough = Strikethrough.Single;
        formatted.Font.Superscript = true;
        formatted.Font.Color = Colors.Firebrick;
        return document;
    }

    static Document ClearedBordersAndShading()
    {
        var document = new Document();
        var format = document.AddSection().AddParagraph("Hello").Format;
        format.Borders.Top.Width = 1;
        format.Borders.ClearAll();
        format.Shading.Clear();
        return document;
    }

    static Document ATable()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(3));
        table.AddColumn(Unit.FromCentimeter(4));
        var row = table.AddRow();
        row.Cells[0].AddParagraph("left");
        row.Cells[1].AddParagraph("right");
        table.Borders.Width = 2;
        table.Shading.Color = Colors.LightGray;
        table.Format.Alignment = ParagraphAlignment.Center;
        return document;
    }

    static Document AChart()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        chart.XValues.AddXSeries().Add("one", "two");
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0);
        return document;
    }

    static Document AStyledParagraph()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Loud", "Normal");
        style.Font.Bold = true;
        style.Font.Size = 20;
        style.ParagraphFormat.SpaceBefore = "1cm";
        var paragraph = document.AddSection().AddParagraph("styled");
        paragraph.Style = "Loud";
        paragraph.Format.Font.Italic = true;
        return document;
    }

    [Fact]
    public void NullableValuesSetAndUnsetSerializesToTheDdlItAlwaysHas() =>
        Ddl(NullableValuesSetAndUnset()).Should().Be(Golden.NullableValuesSetAndUnset);

    [Fact]
    public void FormattedTextFontSerializesToTheDdlItAlwaysHas() =>
        Ddl(FormattedTextFont()).Should().Be(Golden.FormattedTextFont);

    [Fact]
    public void ClearedBordersAndShadingSerializeToTheDdlTheyAlwaysHave() =>
        Ddl(ClearedBordersAndShading()).Should().Be(Golden.ClearedBordersAndShading);

    [Fact]
    public void ATableSerializesToTheDdlItAlwaysHas() =>
        Ddl(ATable()).Should().Be(Golden.Table);

    [Fact]
    public void AChartSerializesToTheDdlItAlwaysHas() =>
        Ddl(AChart()).Should().Be(Golden.Chart);

    [Fact]
    public void AStyledParagraphSerializesToTheDdlItAlwaysHas() =>
        Ddl(AStyledParagraph()).Should().Be(Golden.StyledParagraph);
}
