using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Writing the elements of a paragraph and the shapes of a section back out as MDDDL, and
///   reading them in again. Each of these <c>Serialize</c> methods is a run of "if this property
///   was set, write it", so a property that stops being written is invisible until the document is
///   read back and found to have lost it - which is what the round trips here are for.
///   <para>
///   <c>Character</c> is the odd one and gets the most attention. It has no attributes at all: the
///   whole method chooses between six different spellings of the same object depending on which
///   symbol it holds and how many times it repeats, and the choice is made by masking the top
///   nibbles of the enum value.
///   </para>
/// </summary>
public class DdlElementSerializationTests
{
    static string Write(Document document) => DdlWriter.WriteToString(document);

    static Document RoundTrip(Document document) => DdlReader.DocumentFromString(Write(document));

    static Document DocumentWithAParagraph(out Paragraph paragraph)
    {
        var document = new Document();
        paragraph = document.AddSection().AddParagraph();
        return document;
    }

    static Paragraph FirstParagraphOf(Document document) =>
        document.LastSection.Elements[0] as Paragraph;

    // ----- Character.Serialize ---------------------------------------------------------------------

    [Theory]
    [InlineData(SymbolName.Euro)]
    [InlineData(SymbolName.Copyright)]
    [InlineData(SymbolName.Trademark)]
    [InlineData(SymbolName.RegisteredTrademark)]
    [InlineData(SymbolName.Bullet)]
    [InlineData(SymbolName.Not)]
    [InlineData(SymbolName.EmDash)]
    [InlineData(SymbolName.EnDash)]
    [InlineData(SymbolName.NonBreakableBlank)]
    public void EverySymbolIsWrittenAsOneAndReadBackAsTheSameOne(SymbolName symbol)
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(symbol);

        var reread = RoundTrip(document);

        FirstParagraphOf(reread).Elements.OfType<Character>().Single()
            .SymbolName.Should().Be(symbol);
    }

    [Theory]
    [InlineData(SymbolName.Tab, "\\tab")]
    [InlineData(SymbolName.LineBreak, "\\linebreak")]
    public void TheTwoSymbolsWithAKeywordOfTheirOwnAreWrittenAsThatKeyword(
        SymbolName symbol, string keyword)
    {
        // Not \symbol(Tab): these two have their own spelling, and only when they stand alone.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(symbol);

        Write(document).Should().Contain(keyword);
        FirstParagraphOf(RoundTrip(document)).Elements.OfType<Character>().Single()
            .SymbolName.Should().Be(symbol);
    }

    [Fact]
    public void ABlankIsWrittenWithItsCountEvenWhenThereIsOnlyOneOfIt()
    {
        // The source explains why: a bare \space followed by text beginning with '(' would read
        // as \space(…), so the braces are never left off.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(SymbolName.Blank);

        Write(document).Should().Contain("\\space(1)");
    }

    [Fact]
    public void ARepeatedBlankKeepsItsCount()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(SymbolName.Blank, 7);

        FirstParagraphOf(RoundTrip(document)).Elements.OfType<Character>().Single()
            .Count.Should().Be(7);
    }

    [Theory]
    [InlineData(SymbolName.En)]
    [InlineData(SymbolName.Em)]
    [InlineData(SymbolName.EmQuarter)]
    public void TheOtherSpacesAreWrittenByNameAndReadBack(SymbolName space)
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(space);

        Write(document).Should().Contain("\\space(" + space + ")");
        FirstParagraphOf(RoundTrip(document)).Elements.OfType<Character>().Single()
            .SymbolName.Should().Be(space);
    }

    [Fact]
    public void ARepeatedSpaceWritesItsNameAndItsCount()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter(SymbolName.Em, 3);

        Write(document).Should().Contain("\\space(Em, 3)");

        var reread = FirstParagraphOf(RoundTrip(document)).Elements.OfType<Character>().Single();
        reread.SymbolName.Should().Be(SymbolName.Em);
        reread.Count.Should().Be(3);
    }

    [Fact]
    public void ACharacterThatIsNotASymbolIsWrittenAsItsNumberInHex()
    {
        // The last arm: anything without one of the reserved top nibbles is a plain character,
        // and it goes out as \chr(0x…) rather than as itself.
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddCharacter('A');

        Write(document).Should().Contain("\\chr(0x41)");
        FirstParagraphOf(RoundTrip(document)).Elements.OfType<Character>().Single()
            .Char.Should().Be('A');
    }

    // ----- Footnote.Serialize ----------------------------------------------------------------------

    [Fact]
    public void AFootnoteIsWrittenWithItsTextAndReadBackWithIt()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddText("see below");
        paragraph.AddFootnote("the note itself");

        var footnote = FirstParagraphOf(RoundTrip(document)).Elements.OfType<Footnote>().Single();

        // ReSharper disable PossibleNullReferenceException
        string.Concat((footnote.Elements[0] as Paragraph).Elements.OfType<Text>()
            .Select(t => t.Content)).Should().Be("the note itself");
        // ReSharper restore PossibleNullReferenceException
    }

    [Fact]
    public void AFootnoteKeepsItsReferenceMarkAndItsStyle()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var footnote = paragraph.AddFootnote("note");
        footnote.Reference = "*";
        footnote.Style = "Heading1";

        var reread = FirstParagraphOf(RoundTrip(document)).Elements.OfType<Footnote>().Single();

        reread.Reference.Should().Be("*");
        reread.Style.Should().Be("Heading1");
    }

    [Fact]
    public void AFootnoteKeepsItsOwnFormat()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var footnote = paragraph.AddFootnote("note");
        footnote.Format.Alignment = ParagraphAlignment.Right;
        footnote.Format.SpaceBefore = Unit.FromCentimeter(2);

        var reread = FirstParagraphOf(RoundTrip(document)).Elements.OfType<Footnote>().Single();

        reread.Format.Alignment.Should().Be(ParagraphAlignment.Right);
        reread.Format.SpaceBefore.Centimeter.Should().BeApproximately(2, 1e-4);
    }

    [Fact]
    public void AFootnoteWithNothingSetIsStillWrittenAndStillRead()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddText("x");
        paragraph.AddFootnote();

        FirstParagraphOf(RoundTrip(document)).Elements.OfType<Footnote>()
            .Should().ContainSingle();
    }

    /// <summary>
    ///   A paragraph holding nothing but an empty footnote is not written at all, and neither is
    ///   the section around it. Nothing is wrong: the DOM treats an object with no value set as
    ///   null and skips it, and an empty footnote has nothing set. Recorded because the
    ///   alternative reading - that the footnote was lost - is the one that looks likely.
    /// </summary>
    [Fact]
    public void AParagraphOfNothingButAnEmptyFootnoteIsNotWrittenAtAll()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        paragraph.AddFootnote();

        Write(document).Should().NotContain(@"\section");
    }

    // ----- Barcode.Serialize -----------------------------------------------------------------------

    [Fact]
    public void ABarcodeIsWrittenWithItsCodeAndReadBackWithIt()
    {
        var document = new Document();
        var barcode = document.AddSection().Elements.AddBarcode();
        barcode.Code = "9781234567897";

        var reread = RoundTrip(document).LastSection.Elements.OfType<Barcode>().Single();

        reread.Code.Should().Be("9781234567897");
    }

    [Fact]
    public void EverythingABarcodeCanSayIsWrittenAndReadBack()
    {
        var document = new Document();
        var barcode = document.AddSection().Elements.AddBarcode();
        barcode.Code = "12345";
        barcode.Orientation = TextOrientation.Vertical;
        barcode.BearerBars = true;
        barcode.Text = true;
        barcode.Type = BarcodeType.Barcode39;
        barcode.LineRatio = 3;
        barcode.LineHeight = 2;
        barcode.NarrowLineWidth = 0.1;

        var reread = RoundTrip(document).LastSection.Elements.OfType<Barcode>().Single();

        reread.Orientation.Should().Be(TextOrientation.Vertical);
        reread.BearerBars.Should().BeTrue();
        reread.Text.Should().BeTrue();
        reread.Type.Should().Be(BarcodeType.Barcode39);
        reread.LineRatio.Should().Be(3);
        reread.LineHeight.Should().BeApproximately(2, 1e-4);
        reread.NarrowLineWidth.Should().BeApproximately(0.1, 1e-4);
    }

    /// <summary>
    ///   A barcode with no code cannot be written, and says so rather than writing an empty one.
    ///   The message names BookmarkField rather than Barcode, which is a copy-and-paste slip in
    ///   the source; pinned as it is so that correcting it is a deliberate change.
    /// </summary>
    [Fact]
    public void ABarcodeWithNoCodeRefusesToBeWritten()
    {
        // Something has to be set on it or the DOM calls the whole barcode null and skips it
        // without ever reaching the guard - which is what happens to a barcode with nothing on it
        // at all, section and all.
        var document = new Document();
        document.AddSection().Elements.AddBarcode().Text = true;

        var write = () => Write(document);

        write.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ABarcodeWithNothingSetAtAllIsSkippedRatherThanRefused()
    {
        var document = new Document();
        document.AddSection().Elements.AddBarcode();

        Write(document).Should().NotContain(@"\barcode");
    }

    // ----- AxisTitle.Serialize ---------------------------------------------------------------------

    [Fact]
    public void EverythingAnAxisTitleCanSayIsWrittenAndReadBack()
    {
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Line);
        var title = chart.XAxis.Title;
        title.Caption = "Quarter";
        title.Style = "Heading1";
        title.Orientation = 90;
        title.Alignment = HorizontalAlignment.Right;
        title.VerticalAlignment = VerticalAlignment.Bottom;
        title.Font.Bold = true;

        var reread = RoundTrip(document).LastSection.Elements.OfType<Chart>().Single().XAxis.Title;

        reread.Caption.Should().Be("Quarter");
        reread.Style.Should().Be("Heading1");
        reread.Orientation.Point.Should().BeApproximately(title.Orientation.Point, 1e-4);
        reread.Alignment.Should().Be(HorizontalAlignment.Right);
        reread.VerticalAlignment.Should().Be(VerticalAlignment.Bottom);
        reread.Font.Bold.Should().BeTrue();
    }

    // ----- Serializer.WriteComment -----------------------------------------------------------------

    [Fact]
    public void ACommentIsWrittenAsOne()
    {
        var document = new Document();
        document.AddSection().AddParagraph("t");
        document.Comment = "who wrote this and why";

        Write(document).Should().Contain("// who wrote this and why");
    }

    [Fact]
    public void ACommentCarryingANewLineIsWrittenAsTwoComments()
    {
        // The recursive arm: the writer splits on CR/LF rather than emitting a comment with a
        // line break in the middle of it, which would end the comment and leave the rest as code.
        var document = new Document();
        document.AddSection().AddParagraph("t");
        document.Comment = "first line\r\nsecond line";

        var written = Write(document);

        written.Should().Contain("// first line");
        written.Should().Contain("// second line");
    }

    [Fact]
    public void ACommentTooLongForALineIsWrappedAtASpace()
    {
        // The wrapping arm: long comments are chopped at the last space before the limit, so no
        // line runs past it and no word is cut in half.
        var document = new Document();
        document.AddSection().AddParagraph("t");
        document.Comment = string.Join(" ", Enumerable.Repeat("word", 60));

        var commentLines = Write(document).Split('\n')
            .Where(line => line.TrimStart().StartsWith("//"))
            .Where(line => line.Contains("word"))
            .ToList();

        commentLines.Count.Should().BeGreaterThan(1, "sixty words do not fit on one line");
        commentLines.Should().AllSatisfy(line =>
            line.TrimEnd().Length.Should().BeLessThanOrEqualTo(200, "which is where the writer wraps"));
        string.Join(" ", commentLines.Select(line => line.Trim()[3..].Trim()))
            .Should().Be(document.Comment, "and no word is lost or cut in half");
    }

    [Fact]
    public void ACommentOfOneWordTooLongForALineIsWrittenAnyway()
    {
        // There is no space to chop at, so the alternative to writing it over the limit is not
        // writing it.
        var document = new Document();
        document.AddSection().AddParagraph("t");
        document.Comment = new string('x', 200);

        Write(document).Should().Contain(new string('x', 200));
    }

    [Fact]
    public void AnEmptyCommentIsNotWrittenAtAll()
    {
        var document = new Document();
        document.AddSection().AddParagraph("t");
        document.Comment = "";

        Write(document).Should().NotContain("// \n");
    }

    // ----- Members MDG007 found missing from Serialize ---------------------------------------------

    [Fact]
    public void ATableKeepsItsRowsTogetherAcrossARoundTrip()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn();
        table.AddRow();
        table.KeepTogether = true;

        Write(document).Should().Contain("KeepTogether = true");
        RoundTrip(document).LastSection.Elements.OfType<Table>().Single()
            .KeepTogether.Should().BeTrue();
    }

    [Fact]
    public void AHeaderKeepsItsStyleAcrossARoundTrip()
    {
        var document = new Document();
        document.Styles.AddStyle("Running", StyleNames.Normal);
        var section = document.AddSection();
        section.Headers.Primary.AddParagraph("A header");
        section.Headers.Primary.Style = "Running";

        RoundTrip(document).LastSection.Headers.Primary.Style.Should().Be("Running");
    }

    [Fact]
    public void AHyperlinkKeepsItsFontAcrossARoundTrip()
    {
        var document = DocumentWithAParagraph(out var paragraph);
        var hyperlink = paragraph.AddHyperlink("target");
        hyperlink.AddText("click");
        hyperlink.Font.Bold = true;
        hyperlink.Font.Size = 14;
        paragraph.AddText(" after");

        var paragraphRead = FirstParagraphOf(RoundTrip(document));
        var reread = paragraphRead.Elements.OfType<Hyperlink>().Single();

        reread.Font.Bold.Should().BeTrue();
        reread.Font.Size.Point.Should().Be(14);
        reread.Name.Should().Be("target");
        reread.Elements.OfType<Text>().Single().Content.Should().Be("click");
        string.Concat(paragraphRead.Elements.OfType<Text>().Select(t => t.Content))
            .Should().Be(" after", "the font block ends inside the brackets, not in the text");
    }
}
