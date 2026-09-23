using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Three parts of flattening that <see cref="FlatteningTests"/> does not reach: tab stops, which
///   inherit by position rather than by property; tables, which have four levels of format to
///   resolve between the table and a cell; and the RTF visitor, which is the other of the two and
///   differs from the PDF one in what it does with a run of formatted text.
/// </summary>
public class FlatteningTabStopsAndTablesTests
{
    /// <summary>The positions of a tab stop collection in centimetres, in order.</summary>
    private static double[] PositionsOf(TabStops tabStops) =>
        tabStops.Cast<TabStop>().Select(stop => stop.Position.Centimeter).ToArray();

    private static Document Flattened(Document document)
    {
        new PdfFlattenVisitor().Visit(document);
        return document;
    }

    // ----- tab stops ------------------------------------------------------------------------------

    private static Document ADocumentWithStyledTabStops(out Paragraph paragraph)
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Tabbed", "Normal");
        style.ParagraphFormat.TabStops.AddTabStop(Unit.FromCentimeter(2));
        style.ParagraphFormat.TabStops.AddTabStop(Unit.FromCentimeter(4));

        paragraph = document.AddSection().AddParagraph("t");
        paragraph.Style = "Tabbed";
        return document;
    }

    [Fact]
    public void AParagraphInheritsTheTabStopsOfItsStyle()
    {
        var document = ADocumentWithStyledTabStops(out var paragraph);

        Flattened(document);

        PositionsOf(paragraph.Format.TabStops).Should().Equal(2.0, 4.0);
    }

    [Fact]
    public void AParagraphKeepsItsOwnTabStopsAlongsideTheOnesItInherits()
    {
        var document = ADocumentWithStyledTabStops(out var paragraph);
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6));

        Flattened(document);

        PositionsOf(paragraph.Format.TabStops).Should().Equal(2.0, 4.0, 6.0);
    }

    [Fact]
    public void AParagraphCanCancelATabStopItWouldOtherwiseInherit()
    {
        // The tombstone: RemoveTabStop records a stop marked not to be added, and flattening is
        // where the mark is finally acted on. Without it the paragraph would inherit the stop
        // from its style, because there is nothing in the paragraph to inherit over.
        var document = ADocumentWithStyledTabStops(out var paragraph);
        paragraph.Format.TabStops.RemoveTabStop(Unit.FromCentimeter(2));

        Flattened(document);

        PositionsOf(paragraph.Format.TabStops).Should().Equal(new[] { 4.0 }, "the cancelled one is gone, not merely marked");
    }

    [Fact]
    public void AParagraphCanRefuseToInheritAnyTabStopAtAll()
    {
        var document = ADocumentWithStyledTabStops(out var paragraph);
        paragraph.Format.TabStops.ClearAll();

        Flattened(document);

        paragraph.Format.TabStops.Count.Should().Be(0);
    }

    [Fact]
    public void AParagraphThatClearsAllStillKeepsTheStopsItAddsItself()
    {
        var document = ADocumentWithStyledTabStops(out var paragraph);
        paragraph.Format.TabStops.ClearAll();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(9));

        Flattened(document);

        PositionsOf(paragraph.Format.TabStops).Should().Equal(9.0);
    }

    [Fact]
    public void AStopTheParagraphAlreadyHasIsNotInheritedOverTheTopOfIt()
    {
        // Inheritance is by position, so a stop at the same place is the same stop and the
        // paragraph's own alignment for it has to win.
        var document = ADocumentWithStyledTabStops(out var paragraph);
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(2), TabAlignment.Decimal);

        Flattened(document);

        paragraph.Format.TabStops.Count.Should().Be(2);
        paragraph.Format.TabStops.GetTabStopAt(Unit.FromCentimeter(2))
            .Alignment.Should().Be(TabAlignment.Decimal);
    }

    [Fact]
    public void AFlattenedParagraphInheritsNothingFurther()
    {
        // Flattening leaves the collection complete, which it says by marking it cleared - so
        // flattening the same document twice cannot pull the style's stops in a second time.
        var document = ADocumentWithStyledTabStops(out var paragraph);

        Flattened(document);
        Flattened(document);

        paragraph.Format.TabStops.Count.Should().Be(2);
        paragraph.Format.TabStops.TabsCleared.Should().BeTrue();
    }

    // ----- tables ---------------------------------------------------------------------------------

    private static Table ATable(Document document, int columns = 2, int rows = 2)
    {
        var table = document.LastSection.AddTable();
        for (var idx = 0; idx < columns; idx++)
            table.AddColumn(Unit.FromCentimeter(3));
        for (var idx = 0; idx < rows; idx++)
            table.AddRow();
        return table;
    }

    [Fact]
    public void ATableWithNoStyleIsGivenTheNormalOne()
    {
        var document = new Document();
        document.AddSection();
        var table = ATable(document);

        Flattened(document);

        table.Style.Should().Be("Normal");
        table.Format.Should().NotBeNull();
    }

    [Fact]
    public void ATableTakesOnWhatItsStyleSays()
    {
        var document = new Document();
        document.Styles.AddStyle("Gridded", "Normal").Font.Bold = true;
        document.AddSection();
        var table = ATable(document);
        table.Style = "Gridded";

        Flattened(document);

        table.Format.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void ATableIsGivenPaddingWhereItStatedNone()
    {
        var document = new Document();
        document.AddSection();
        var table = ATable(document);

        Flattened(document);

        table.LeftPadding.Millimeter.Should().BeApproximately(1.2, 1e-4);
        table.RightPadding.Millimeter.Should().BeApproximately(1.2, 1e-4);
    }

    [Fact]
    public void AColumnInheritsTheTablesPaddingAndFormat()
    {
        var document = new Document();
        document.AddSection();
        var table = ATable(document);
        table.Format.Font.Italic = true;
        table.LeftPadding = Unit.FromCentimeter(1);

        Flattened(document);

        table.Columns[0].LeftPadding.Centimeter.Should().BeApproximately(1, 1e-4);
        table.Columns[0].Format.Font.Italic.Should().BeTrue();
    }

    [Fact]
    public void ACellEndsUpWithTheFormatOfTheTableItIsIn()
    {
        var document = new Document();
        document.AddSection();
        var table = ATable(document);
        table.Format.Font.Size = 17;

        Flattened(document);

        table[0, 0].Format.Font.Size.Point.Should().BeApproximately(17, 1e-4);
    }

    [Fact]
    public void ACellKeepsItsOwnAnswerWhereItDisagreesWithTheTable()
    {
        var document = new Document();
        document.AddSection();
        var table = ATable(document);
        table.Format.Font.Size = 17;
        table[1, 1].Format.Font.Size = 8;

        Flattened(document);

        table[0, 0].Format.Font.Size.Point.Should().BeApproximately(17, 1e-4);
        table[1, 1].Format.Font.Size.Point.Should().BeApproximately(8, 1e-4);
    }

    [Fact]
    public void ATableWithNoRowsOrColumnsIsFlattenedWithoutComplaint()
    {
        var document = new Document();
        document.AddSection().AddTable();

        var flatten = () => Flattened(document);

        flatten.Should().NotThrow();
    }

    // ----- the RTF visitor ------------------------------------------------------------------------

    private static Paragraph RtfFlattened(Document document)
    {
        new RtfFlattenVisitor().Visit(document);
        return document.LastSection.Elements[0] as Paragraph;
    }

    private static FormattedText FormattedTextOf(Paragraph paragraph) =>
        paragraph.Elements.OfType<FormattedText>().Single();

    [Fact]
    public void AFormattedTextNamingAStyleTakesThatStylesFont()
    {
        var document = new Document();
        document.Styles.AddStyle("Loud", "Normal").Font.Bold = true;
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddFormattedText("shout", "Loud");

        FormattedTextOf(RtfFlattened(document)).Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void AFormattedTextKeepsWhatItSaysItselfOverWhatItsStyleSays()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Loud", "Normal");
        style.Font.Bold = true;
        style.Font.Size = 20;
        var paragraph = document.AddSection().AddParagraph();
        var formatted = paragraph.AddFormattedText("shout", "Loud");
        formatted.Font.Size = 8;

        var flattened = FormattedTextOf(RtfFlattened(document));

        flattened.Font.Bold.Should().BeTrue("this came from the style");
        flattened.Font.Size.Point.Should().BeApproximately(8, 1e-4, "and this was its own");
    }

    [Fact]
    public void AFormattedTextNamingNoStyleIsLeftAlone()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddFormattedText("plain").Font.Italic = true;

        var flattened = FormattedTextOf(RtfFlattened(document));

        flattened.Font.Italic.Should().BeTrue();
        flattened.Font.Bold.Should().BeFalse("there was no style to take a font from");
    }

    /// <summary>
    ///   A style name that names nothing falls back to the built-in InvalidStyleName style rather
    ///   than to no style at all, so a document with a typo in it still renders and looks wrong in
    ///   a way that is meant to be noticed.
    /// </summary>
    [Fact]
    public void AFormattedTextNamingAStyleThatDoesNotExistFallsBackToTheInvalidOne()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddFormattedText("mistyped", "NoSuchStyle");

        var flatten = () => RtfFlattened(document);

        flatten.Should().NotThrow();

        // Not merely that there is a font: FormattedText.Font makes one on being asked, so a
        // fallback that did nothing at all would still leave a font there. InvalidStyleName is
        // bold, dash-underlined and bright green precisely so that it cannot be mistaken for
        // anything a document meant, and that is what has to have arrived.
        var invalid = document.Styles[StyleNames.InvalidStyleName].Font;
        var font = FormattedTextOf(document.LastSection.Elements[0] as Paragraph).Font;
        font.Bold.Should().Be(invalid.Bold).And.Be(true);
        font.Underline.Should().Be(invalid.Underline);
        font.Color.Should().Be(invalid.Color);
    }

    [Fact]
    public void AHyperlinkTakesTheHyperlinkStylesFont()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddHyperlink("target", HyperlinkType.Local).AddText("go");

        new RtfFlattenVisitor().Visit(document);

        // ReSharper disable once PossibleNullReferenceException
        (document.LastSection.Elements[0] as Paragraph)
            .Elements.OfType<Hyperlink>().Single().Font.Underline
            .Should().Be(document.Styles["Hyperlink"].Font.Underline);
    }
}
