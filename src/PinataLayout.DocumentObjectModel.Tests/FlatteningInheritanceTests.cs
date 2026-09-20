using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The parts of flattening that only run when both sides of an inheritance have something to
///   say: a paragraph with a shading of its own under a style that also has one, a border set on
///   the collection that holds it as well as on the edge itself, a page setup that names a format
///   <em>and</em> one of its two lengths, and a paragraph naming a style that does not exist.
///   <para>
///   <see cref="FlatteningTests"/> covers the ordinary case, where one side is empty and simply
///   takes what the other says. These are the cases where the two have to be merged, which is
///   where an arm of one of the near-identical per-edge blocks can be wrong without the arm beside
///   it noticing.
///   </para>
/// </summary>
public class FlatteningInheritanceTests
{
    static void Flattened(Document document) => new PdfFlattenVisitor().Visit(document);

    // ----- shading ----------------------------------------------------------------------------------

    [Fact]
    public void AParagraphWithNoShadingTakesTheOneItsStyleHas()
    {
        var document = new Document();
        document.Styles.AddStyle("Shaded", "Normal").ParagraphFormat.Shading.Color = Colors.LightGray;
        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Style = "Shaded";

        Flattened(document);

        paragraph.Format.Shading.Color.Should().Be(Colors.LightGray);
    }

    [Fact]
    public void AParagraphWithAShadingOfItsOwnKeepsItAndFillsInTheRest()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Shaded", "Normal");
        style.ParagraphFormat.Shading.Color = Colors.LightGray;
        style.ParagraphFormat.Shading.Visible = true;
        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Style = "Shaded";
        paragraph.Format.Shading.Color = Colors.Yellow;

        Flattened(document);

        paragraph.Format.Shading.Color.Should().Be(Colors.Yellow, "its own answer wins");
        paragraph.Format.Shading.Visible.Should().BeTrue("and the rest came from the style");
    }

    // ----- borders ----------------------------------------------------------------------------------

    [Fact]
    public void EachEdgeOfABorderIsMergedWithTheSameEdgeOfTheStyle()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Boxed", "Normal");
        style.ParagraphFormat.Borders.Left.Width = "1pt";
        style.ParagraphFormat.Borders.Right.Width = "2pt";
        style.ParagraphFormat.Borders.Top.Width = "3pt";
        style.ParagraphFormat.Borders.Bottom.Width = "4pt";
        style.ParagraphFormat.Borders.Color = Colors.Red;

        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Style = "Boxed";
        paragraph.Format.Borders.Left.Color = Colors.Blue;

        Flattened(document);

        var borders = paragraph.Format.Borders;
        borders.Left.Width.Point.Should().BeApproximately(1, 1e-4);
        borders.Right.Width.Point.Should().BeApproximately(2, 1e-4);
        borders.Top.Width.Point.Should().BeApproximately(3, 1e-4);
        borders.Bottom.Width.Point.Should().BeApproximately(4, 1e-4);
        borders.Left.Color.Should().Be(Colors.Blue, "the edge's own colour wins");
        borders.Right.Color.Should().Be(Colors.Red, "and the rest take the collection's");
    }

    /// <summary>
    ///   A value set on the collection rather than on one of its four edges reaches every edge that
    ///   has not answered for itself. That fill-in runs per edge, after the style's own edge has
    ///   been merged in.
    /// </summary>
    [Fact]
    public void AValueSetOnTheCollectionReachesTheEdgesThatSaidNothing()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Boxed", "Normal");
        style.ParagraphFormat.Borders.Left.Visible = true;
        style.ParagraphFormat.Borders.Right.Visible = true;
        style.ParagraphFormat.Borders.Top.Visible = true;
        style.ParagraphFormat.Borders.Bottom.Visible = true;

        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Style = "Boxed";
        paragraph.Format.Borders.Width = "5pt";

        Flattened(document);

        var borders = paragraph.Format.Borders;
        borders.Left.Width.Point.Should().BeApproximately(5, 1e-4);
        borders.Right.Width.Point.Should().BeApproximately(5, 1e-4);
        borders.Top.Width.Point.Should().BeApproximately(5, 1e-4);
        borders.Bottom.Width.Point.Should().BeApproximately(5, 1e-4);
    }

    // ----- page setup -------------------------------------------------------------------------------

    /// <summary>
    ///   A section that names a page format and one of the two lengths keeps the length it set and
    ///   takes the other from the format. The two arms of that decision used to be swapped, so the
    ///   length the caller did set was the one thrown away and the other was left unset — a page of
    ///   no width at all.
    /// </summary>
    [Fact]
    public void ASectionThatNamesAPageFormatAndOneLengthKeepsTheLengthAndTakesTheOther()
    {
        var byWidth = new Document();
        var wide = byWidth.AddSection();
        wide.PageSetup.PageFormat = PageFormat.A5;
        wide.PageSetup.PageWidth = "10cm";

        var byHeight = new Document();
        var tall = byHeight.AddSection();
        tall.PageSetup.PageFormat = PageFormat.A5;
        tall.PageSetup.PageHeight = "10cm";

        Flattened(byWidth);
        Flattened(byHeight);

        PageSetup.GetPageSize(PageFormat.A5, out var a5Width, out var a5Height);

        wide.PageSetup.PageWidth.Centimeter.Should().BeApproximately(10, 1e-3, "its own answer stands");
        wide.PageSetup.PageHeight.Point.Should().BeApproximately(a5Height.Point, 1e-3);

        tall.PageSetup.PageHeight.Centimeter.Should().BeApproximately(10, 1e-3);
        tall.PageSetup.PageWidth.Point.Should().BeApproximately(a5Width.Point, 1e-3);
    }

    [Fact]
    public void ASectionThatNamesOnlyAPageFormatTakesBothLengthsFromIt()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A6;

        Flattened(document);

        PageSetup.GetPageSize(PageFormat.A6, out var width, out var height);
        section.PageSetup.PageWidth.Point.Should().BeApproximately(width.Point, 1e-3);
        section.PageSetup.PageHeight.Point.Should().BeApproximately(height.Point, 1e-3);
    }

    // ----- styles that are not there ------------------------------------------------------------------

    /// <summary>
    ///   A paragraph naming a style the document does not have is not an error. It is given the
    ///   name <c>InvalidStyleName</c>, which is a built-in style, so the document still renders and
    ///   the mistake is visible in the output rather than fatal at render time.
    /// </summary>
    [Fact]
    public void AParagraphNamingAStyleThatDoesNotExistIsGivenTheInvalidOne()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Style = "NoSuchStyle";

        Flattened(document);

        paragraph.Style.Should().Be("InvalidStyleName");
    }

    [Fact]
    public void AParagraphNamingNoStyleAtAllIsGivenNormal()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("x");

        Flattened(document);

        paragraph.Style.Should().Be("Normal");
    }

    // ----- footnotes ----------------------------------------------------------------------------------

    [Fact]
    public void AFootnoteNamingAStyleTakesThatStylesFormat()
    {
        var document = new Document();
        document.Styles.AddStyle("Tiny", "Normal").ParagraphFormat.SpaceBefore = "7mm";
        var footnote = document.AddSection().AddParagraph("x").AddFootnote("note");
        footnote.Style = "Tiny";

        Flattened(document);

        footnote.Format.SpaceBefore.Millimeter.Should().BeApproximately(7, 1e-3);
    }

    [Fact]
    public void AFootnoteWithAFormatOfItsOwnKeepsItAndFillsInTheRest()
    {
        var document = new Document();
        document.Styles.AddStyle("Tiny", "Normal").ParagraphFormat.SpaceBefore = "7mm";
        var footnote = document.AddSection().AddParagraph("x").AddFootnote("note");
        footnote.Style = "Tiny";
        footnote.Format.SpaceAfter = "3mm";

        Flattened(document);

        footnote.Format.SpaceAfter.Millimeter.Should().BeApproximately(3, 1e-3);
        footnote.Format.SpaceBefore.Millimeter.Should().BeApproximately(7, 1e-3);
    }

    [Fact]
    public void AFootnoteNamingNoStyleIsGivenTheFootnoteOne()
    {
        var document = new Document();
        var footnote = document.AddSection().AddParagraph("x").AddFootnote("note");

        Flattened(document);

        footnote.Style.Should().Be("Footnote");
    }

    // ----- charts -------------------------------------------------------------------------------------

    /// <summary>
    ///   An axis takes its line widths from fixed defaults rather than from the chart: a hairline
    ///   of 0.15 for gridlines and 0.4 for the axis itself. The axis's own line format is only
    ///   flattened if it exists, so asking for it is what brings it into being.
    /// </summary>
    [Fact]
    public void EachAxisOfAChartIsGivenTheDefaultLineWidths()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        chart.XAxis.HasMajorGridlines = true;
        chart.XAxis.HasMinorGridlines = true;
        chart.YAxis.HasMajorGridlines = true;
        chart.XAxis.LineFormat.Visible = true;
        // Asking for the gridlines is what brings them into being; an axis that was never asked
        // has none to flatten.
        chart.XAxis.MajorGridlines.LineFormat.Visible = true;
        chart.XAxis.MinorGridlines.LineFormat.Visible = true;
        chart.YAxis.MajorGridlines.LineFormat.Visible = true;

        Flattened(document);

        chart.XAxis.MajorGridlines.LineFormat.Width.Point.Should().BeApproximately(0.15, 1e-4);
        chart.XAxis.MinorGridlines.LineFormat.Width.Point.Should().BeApproximately(0.15, 1e-4);
        chart.YAxis.MajorGridlines.LineFormat.Width.Point.Should().BeApproximately(0.15, 1e-4);
        chart.XAxis.LineFormat.Width.Point.Should().BeApproximately(0.4, 1e-4);
    }

    [Fact]
    public void AnAxisThatSetsItsOwnLineWidthKeepsIt()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        chart.XAxis.LineFormat.Width = "3pt";

        Flattened(document);

        chart.XAxis.LineFormat.Width.Point.Should().BeApproximately(3, 1e-4);
    }

    [Fact]
    public void AChartWithAFormatOfItsOwnKeepsItAndFillsInTheRestFromItsStyle()
    {
        var document = new Document();
        document.Styles.AddStyle("Charted", "Normal").ParagraphFormat.SpaceBefore = "9mm";
        var chart = document.AddSection().AddChart(ChartType.Column2D);
        chart.Style = "Charted";
        chart.Format.SpaceAfter = "4mm";

        Flattened(document);

        chart.Format.SpaceAfter.Millimeter.Should().BeApproximately(4, 1e-3);
        chart.Format.SpaceBefore.Millimeter.Should().BeApproximately(9, 1e-3);
    }
}
