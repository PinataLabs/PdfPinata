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
///   The parts of MDDDL that a document written by the serializer rarely or never contains, read
///   from text written by hand: a piece of a document read on its own rather than inside
///   <c>\document</c>, the header and footer keywords that name one page each, style definitions
///   that redefine or base themselves on nothing, tables and charts written with every optional
///   piece, and the short forms a paragraph allows.
///   <para>
///   Everything here is well formed. <c>DdlMalformedInputTests</c> has the same keywords written
///   wrongly.
///   </para>
/// </summary>
public class DdlStructureReadingTests
{
    private static Document Read(string ddl) => DdlReader.DocumentFromString(ddl);

    private static Section SectionOf(string sectionBody) =>
        Read("\\document{\\section{" + sectionBody + "}}").LastSection;

    private static Paragraph FirstParagraphOf(string paragraphBody) =>
        SectionOf("\\paragraph{" + paragraphBody + "}").Elements[0] as Paragraph;

    private static Chart ChartFrom(string chartBody) =>
        SectionOf("\\chart(Line){" + chartBody + "}").Elements[0] as Chart;

    private static string TextOf(Paragraph paragraph) =>
        string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content));

    private static DocumentObject ReadOnItsOwn(string ddl)
    {
        var errors = new DdlReaderErrors();
        var read = DdlReader.ObjectFromString(ddl, errors);
        ReaderDiagnostics.Reported(errors).Should().BeEmpty("the DDL is well formed");
        return read;
    }

    // ----- a piece of a document read on its own ----------------------------------------------------

    [Fact]
    public void ASectionCanBeReadWithoutADocumentAroundIt()
    {
        var section = ReadOnItsOwn("\\section[PageSetup{PageFormat = A5}]{\\paragraph{alone}}")
            .Should().BeOfType<Section>().Subject;

        section.PageSetup.PageFormat.Should().Be(PageFormat.A5);
        TextOf(section.Elements[0] as Paragraph).Should().Be("alone");
    }

    [Fact]
    public void AStylesBlockCanBeReadWithoutADocumentAroundIt()
    {
        var styles = ReadOnItsOwn("\\styles{Quiet : Normal{Font{Size = 8}}}")
            .Should().BeOfType<Styles>().Subject;

        styles["Quiet"].BaseStyle.Should().Be("Normal");
        styles["Quiet"].Font.Size.Point.Should().BeApproximately(8, 1e-4);
    }

    [Fact]
    public void ATableCanBeReadWithoutADocumentAroundIt()
    {
        var table = ReadOnItsOwn(
                "\\table{\\columns{\\column[Width = \"2cm\"]\\column}\\rows{\\row{\\cell{a}\\cell{b}}}}")
            .Should().BeOfType<Table>().Subject;

        table.Columns.Count.Should().Be(2);
        table.Columns[0].Width.Centimeter.Should().BeApproximately(2, 1e-4);
        TextOf(table[0, 1].Elements[0] as Paragraph).Should().Be("b");
    }

    [Fact]
    public void ATextFrameCanBeReadWithoutADocumentAroundIt()
    {
        var frame = ReadOnItsOwn("\\textframe[Width = \"5cm\"]{inside}")
            .Should().BeOfType<TextFrame>().Subject;

        frame.Width.Centimeter.Should().BeApproximately(5, 1e-4);
        TextOf(frame.Elements[0] as Paragraph).Should().Be("inside");
    }

    [Fact]
    public void AParagraphReadOnItsOwnComesBackInTheListItWasAddedTo()
    {
        // Which is the one kind the reader does not hand back as itself: the parser only knows how
        // to add a paragraph to a list of elements, so what is returned is that list.
        var elements = ReadOnItsOwn("\\paragraph[Style = \"Heading1\"]{hello}")
            .Should().BeOfType<DocumentElements>().Subject;

        var paragraph = elements.OfType<Paragraph>().Single();
        paragraph.Style.Should().Be("Heading1");
        TextOf(paragraph).Should().Be("hello");
    }

    /// <summary>
    ///   Pinned as the gap it is: the dispatch has an arm for <c>\chart</c> and the arm throws
    ///   rather than parsing, so a chart is readable inside a section and not on its own.
    /// </summary>
    [Fact]
    public void AChartCannotBeReadOnItsOwn()
    {
        var act = () => DdlReader.ObjectFromString("\\chart(Line){}", new DdlReaderErrors());

        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void AKeywordThatCannotStandAloneIsRefusedWhenReadOnItsOwn()
    {
        // A cell only means something inside a row, so it is not one of the things the reader
        // accepts at the top.
        var act = () => DdlReader.ObjectFromString("\\cell{stray}", new DdlReaderErrors());

        act.Should().Throw<Exception>().WithMessage("Unexpected symbol '\\cell'.");
    }

    // ----- styles -------------------------------------------------------------------------------

    [Fact]
    public void AStyleThatAlreadyExistsCanBeRedefinedAndRebased()
    {
        var document = Read(
            "\\document{\\styles{Heading1 : Normal{Font{Size = 30}}}\\section{\\paragraph{t}}}");

        document.Styles["Heading1"].BaseStyle.Should().Be("Normal");
        document.Styles["Heading1"].Font.Size.Point.Should().BeApproximately(30, 1e-4);
    }

    [Fact]
    public void AStyleNameCanBeAQuotedString()
    {
        // Which is how a name with a space in it is written, since an identifier cannot have one.
        var document = Read(
            "\\document{\\styles{\"Quiet Note\" : Normal{Font{Size = 8}}}\\section{\\paragraph{t}}}");

        document.Styles["Quiet Note"].Should().NotBeNull();
        document.Styles["Quiet Note"].Font.Size.Point.Should().BeApproximately(8, 1e-4);
    }

    [Fact]
    public void AStyleNeedNotSayAnythingAboutItself()
    {
        var document = Read("\\document{\\styles{Plain : Normal}\\section{\\paragraph{t}}}");

        document.Styles["Plain"].BaseStyle.Should().Be("Normal");
    }

    [Fact]
    public void AStylesBlockCanBeEmpty()
    {
        var document = Read("\\document{\\styles{}\\section{\\paragraph{t}}}");

        document.Styles["Normal"].Should().NotBeNull("the standard styles are there regardless");
        document.Sections.Count.Should().Be(1);
    }

    [Fact]
    public void ANewStyleWithNoBaseIsWarnedAboutAndStillDefined()
    {
        var errors = new DdlReaderErrors();

        var document = (Document)DdlReader.ObjectFromString(
            "\\document{\\styles{Mine{Font{Size = 8}}}\\section{\\paragraph{t}}}", errors);

        document.Styles["Mine"].Font.Size.Point.Should().BeApproximately(8, 1e-4);
        var warning = errors.Cast<DdlReaderError>().Should().ContainSingle().Subject;
        warning.ErrorLevel.Should().Be(DdlErrorLevel.Warning);
        warning.ErrorMessage.Should().Be("Use of undefined style 'Mine'.");
        errors.ErrorCount.Should().Be(0, "a warning is not an error");
    }

    [Fact]
    public void AStyleBasedOnOneThatDoesNotExistIsWarnedAboutAndStillDefined()
    {
        var errors = new DdlReaderErrors();

        var document = (Document)DdlReader.ObjectFromString(
            "\\document{\\styles{Mine : NoSuchBase{Font{Size = 8}}}\\section{\\paragraph{t}}}", errors);

        document.Styles["Mine"].Font.Size.Point.Should().BeApproximately(8, 1e-4);
        document.Styles["Mine"].BaseStyle.Should().NotBe("NoSuchBase",
            "a base that is not there is replaced rather than kept");
        var warning = errors.Cast<DdlReaderError>().Should().ContainSingle().Subject;
        warning.ErrorLevel.Should().Be(DdlErrorLevel.Warning);
        warning.ErrorMessage.Should().Be("Use of undefined base style 'NoSuchBase'.");
        warning.SourceLine.Should().Be(1);
        warning.SourceColumn.Should().BeGreaterThan(0, "a warning says where it was raised");
    }

    // ----- headers and footers --------------------------------------------------------------------

    [Fact]
    public void EachPageOfAHeaderCanBeGivenItsOwn()
    {
        var section = SectionOf(
            "\\firstpageheader{first}\\evenpageheader{even}\\primaryheader{primary}\\paragraph{t}");

        TextOf(section.Headers.FirstPage.Elements[0] as Paragraph).Should().Be("first");
        TextOf(section.Headers.EvenPage.Elements[0] as Paragraph).Should().Be("even");
        TextOf(section.Headers.Primary.Elements[0] as Paragraph).Should().Be("primary");
    }

    [Fact]
    public void EachPageOfAFooterCanBeGivenItsOwn()
    {
        var section = SectionOf(
            "\\firstpagefooter{first}\\evenpagefooter{even}\\primaryfooter{primary}\\paragraph{t}");

        TextOf(section.Footers.FirstPage.Elements[0] as Paragraph).Should().Be("first");
        TextOf(section.Footers.EvenPage.Elements[0] as Paragraph).Should().Be("even");
        TextOf(section.Footers.Primary.Elements[0] as Paragraph).Should().Be("primary");
        section.Headers.HasHeaderFooter(HeaderFooterIndex.FirstPage).Should().BeFalse(
            "a footer keyword says nothing about the header");
    }

    [Fact]
    public void AHeaderWithoutAPageIsEveryPagesHeaderAndCarriesItsAttributes()
    {
        var section = SectionOf(
            "\\header[Format{Alignment = Center}]{\\paragraph{a}\\paragraph{b}}\\paragraph{t}");

        foreach (var header in new[] { section.Headers.Primary, section.Headers.EvenPage, section.Headers.FirstPage })
        {
            header.Format.Alignment.Should().Be(ParagraphAlignment.Center);
            header.Elements.OfType<Paragraph>().Select(TextOf).Should().Equal("a", "b");
        }
    }

    // ----- paragraph content ----------------------------------------------------------------------

    [Fact]
    public void AnEmptyLineInsideAParagraphIsAParagraphBreakRatherThanANewParagraph()
    {
        var section = SectionOf("\\paragraph{one\n\ntwo}");

        section.Elements.Count.Should().Be(1);
        var paragraph = section.Elements[0] as Paragraph;
        // ReSharper disable once PossibleNullReferenceException
        paragraph.Elements.OfType<Character>().Single().SymbolName.Should().Be(SymbolName.ParaBreak);
        paragraph.Elements.OfType<Text>().Select(text => text.Content).Should().Equal("one", "two");
    }

    [Fact]
    public void AnEmptyLineInsideFormattedTextJoinsTheTwoLines()
    {
        // Below the paragraph's own level an empty line ends nothing; it is read as the one space
        // that separates the words either side of it.
        var formatted = FirstParagraphOf("\\bold{one\n\ntwo}").Elements.OfType<FormattedText>().Single();

        string.Concat(formatted.Elements.OfType<Text>().Select(text => text.Content)).Should().Be("one two");
    }

    [Fact]
    public void EmptyLinesBeforeTheEndOfFormattedTextAreDropped()
    {
        var formatted = FirstParagraphOf("\\bold{one\n\n}").Elements.OfType<FormattedText>().Single();

        string.Concat(formatted.Elements.OfType<Text>().Select(text => text.Content)).Should().Be("one");
    }

    [Fact]
    public void ALineEndAfterAKeywordContinuesTheParagraph()
    {
        var paragraph = FirstParagraphOf("\\bold{x}\nmore");

        paragraph.Elements.OfType<FormattedText>().Should().ContainSingle();
        TextOf(paragraph).Should().Be(" more", "the line end is the space between the two");
    }

    [Fact]
    public void AnEmptyLineAfterAKeywordIsAParagraphBreakToo()
    {
        var paragraph = FirstParagraphOf("\\bold{x}\n\nmore");

        paragraph.Elements.OfType<FormattedText>().Should().ContainSingle();
        paragraph.Elements.OfType<Character>().Single().SymbolName.Should().Be(SymbolName.ParaBreak);
        TextOf(paragraph).Should().Be("more");
    }

    [Fact]
    public void ALineEndAfterATabAddsNoSpaceOfItsOwn()
    {
        // The tab already separates the words, so the line end that follows it is not read as a
        // second separator the way it would be after a word.
        var paragraph = FirstParagraphOf("a\\tab\nb");

        paragraph.Elements.OfType<Character>().Single().SymbolName.Should().Be(SymbolName.Tab);
        TextOf(paragraph).Should().Be("ab");
    }

    [Fact]
    public void TheSpaceBeforeATabIsDropped()
    {
        var paragraph = FirstParagraphOf("a \\tab b");

        paragraph.Elements.OfType<Text>().First().Content.Should().Be("a",
            "the tab is the separator, so a blank written before it is not kept");
    }

    [Fact]
    public void ACommentInsideAParagraphRunsToTheEndOfItsLine()
    {
        TextOf(FirstParagraphOf("a // not text\nb")).Should().Be("a b");
    }

    [Fact]
    public void AParagraphCanOpenWithACommentLine()
    {
        TextOf(FirstParagraphOf("// not text\nb")).Should().Be("b");
    }

    [Fact]
    public void ALineOfCommentBetweenTwoLinesOfTextDoesNotEndTheParagraph()
    {
        // A line holding nothing but a comment is not an empty line, so the paragraph goes on.
        var section = SectionOf("one\n// not text\ntwo");

        section.Elements.Count.Should().Be(1);
        TextOf(section.Elements[0] as Paragraph).Should().Be("one two");
    }

    [Fact]
    public void ALineThatStartsWithASingleSlashIsText()
    {
        TextOf(FirstParagraphOf("one\n/two")).Should().Be("one /two");
    }

    [Fact]
    public void ALineEndBeforeTheClosingBraceIsNotText()
    {
        TextOf(FirstParagraphOf("one\n")).Should().Be("one");
    }

    [Fact]
    public void ACarriageReturnBeforeALineFeedIsOneLineEnd()
    {
        TextOf(FirstParagraphOf("a\r\nb")).Should().Be("a b");
    }

    [Fact]
    public void ACarriageReturnOnItsOwnIsALineEnd()
    {
        // A lone carriage return - an old Mac OS line end - ends a line as a line feed does, and
        // so reads as the space a line end is. It used to be kept in the text as the character it
        // is, which made a whole file written that way a single line. So a Text holding a CR no
        // longer survives being written and read back, exactly as one holding an LF never has:
        // a line end in paragraph text is white space in MDDDL, and the writer escapes neither.
        TextOf(FirstParagraphOf("a\rb")).Should().Be("a b");
    }

    [Fact]
    public void ABackslashAndAHyphenIsASoftHyphen()
    {
        TextOf(FirstParagraphOf("Hyphen\\-ation")).Should().Be("Hyphen\u00ADation");
    }

    [Fact]
    public void ASoftHyphenCanOpenAParagraph()
    {
        TextOf(FirstParagraphOf("\\-start")).Should().Be("\u00ADstart");
    }

    [Fact]
    public void AnEscapedBraceCanOpenAParagraph()
    {
        TextOf(FirstParagraphOf("\\{brace")).Should().Be("{brace");
    }

    [Fact]
    public void BareSectionContentCanOpenWithAFormattingKeyword()
    {
        // The section has to look past the backslash to decide whether this is the start of a
        // paragraph written without its keyword or the first of a list of blocks.
        var section = SectionOf("\\bold{x} y");

        var paragraph = section.Elements.OfType<Paragraph>().Single();
        paragraph.Elements.OfType<FormattedText>().Should().ContainSingle();
        TextOf(paragraph).Should().Be(" y");
    }

    [Fact]
    public void BareSectionContentCanOpenWithAComment()
    {
        TextOf(SectionOf("// not text\ntext").Elements[0] as Paragraph).Should().Be("text");
    }

    [Fact]
    public void APageBreakSitsBetweenTheParagraphsEitherSideOfIt()
    {
        var section = SectionOf("\\paragraph{x}\\pagebreak\\paragraph{y}");

        section.Elements.Count.Should().Be(3);
        section.Elements[1].Should().BeOfType<PageBreak>();
    }

    // ----- font size and colour shorthands ---------------------------------------------------------

    [Fact]
    public void FontSizeIsAShorthandForFormattedTextOfThatSize()
    {
        var formatted = FirstParagraphOf("\\fontsize(14){big}").Elements.OfType<FormattedText>().Single();

        formatted.Font.Size.Point.Should().BeApproximately(14, 1e-4);
        formatted.Elements.OfType<Text>().Single().Content.Should().Be("big");
    }

    [Fact]
    public void FontSizeTakesAUnitWhenItIsQuoted()
    {
        var formatted = FirstParagraphOf("\\fontsize(\"1cm\"){big}").Elements.OfType<FormattedText>().Single();

        formatted.Font.Size.Centimeter.Should().BeApproximately(1, 1e-4);
    }

    [Theory]
    [InlineData("Red", 0xFFFF0000u)]
    [InlineData("RGB(1, 2, 3)", 0xFF010203u)]
    [InlineData("0xFF00FF00", 0xFF00FF00u)]
    public void FontColourIsAShorthandForFormattedTextOfThatColour(string colour, uint argb)
    {
        var formatted = FirstParagraphOf("\\fontcolor(" + colour + "){c}")
            .Elements.OfType<FormattedText>().Single();

        formatted.Font.Color.Argb.Should().Be(argb);
        formatted.Elements.OfType<Text>().Single().Content.Should().Be("c");
    }

    [Fact]
    public void FormattedTextCanNameAStyleAndSetAFontAtOnce()
    {
        var formatted = FirstParagraphOf("\\font(\"Heading1\")[Size = 3]{x}")
            .Elements.OfType<FormattedText>().Single();

        formatted.Style.Should().Be("Heading1");
        formatted.Font.Size.Point.Should().BeApproximately(3, 1e-4);
    }

    [Fact]
    public void AHyperlinkCanHoldFormattedText()
    {
        var link = FirstParagraphOf("\\hyperlink[Name = \"n\"]{a\\bold{b}}").Elements.OfType<Hyperlink>().Single();

        link.Elements.OfType<Text>().Single().Content.Should().Be("a");
        link.Elements.OfType<FormattedText>().Should().ContainSingle();
    }

    // ----- symbols, spaces and characters -----------------------------------------------------------

    [Fact]
    public void ASymbolCanSayHowManyTimesItIsRepeated()
    {
        var character = FirstParagraphOf("\\symbol(Euro, 3)").Elements.OfType<Character>().Single();

        character.SymbolName.Should().Be(SymbolName.Euro);
        character.Count.Should().Be(3);
    }

    [Theory]
    [InlineData("\\space(Em)", SymbolName.Em, 1)]
    [InlineData("\\space(Em, 3)", SymbolName.Em, 3)]
    [InlineData("\\space(En)", SymbolName.En, 1)]
    [InlineData("\\space(EmQuarter, 2)", SymbolName.EmQuarter, 2)]
    [InlineData("\\space(4)", SymbolName.Blank, 4)]
    [InlineData("\\space (3)", SymbolName.Blank, 3)]
    public void ASpaceCanSayWhatKindAndHowMany(string ddl, SymbolName kind, int count)
    {
        var character = FirstParagraphOf("a" + ddl + "b").Elements.OfType<Character>().Single();

        character.SymbolName.Should().Be(kind);
        character.Count.Should().Be(count);
        TextOf(FirstParagraphOf("a" + ddl + "b")).Should().Be("ab");
    }

    [Fact]
    public void ABackslashAndAParenthesisIsShortForChr()
    {
        var character = FirstParagraphOf("\\(65, 3)").Elements.OfType<Character>().Single();

        character.Char.Should().Be('A');
        character.Count.Should().Be(3);
    }

    [Fact]
    public void ACharacterNumberCanBeWrittenInHex()
    {
        FirstParagraphOf("\\chr(0x41)").Elements.OfType<Character>().Single().Char.Should().Be('A');
    }

    // ----- fields and footnotes -------------------------------------------------------------------

    [Fact]
    public void ASectionPagesFieldIsReadAsOne()
    {
        FirstParagraphOf("\\field(SectionPages)").Elements.OfType<Fields.SectionPagesField>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AFieldNameIsNotCaseSensitive()
    {
        FirstParagraphOf("\\field(numpages)").Elements.OfType<Fields.NumPagesField>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AFootnoteCanHoldParagraphsWrittenOutInFull()
    {
        var footnote = FirstParagraphOf("x\\footnote{\\paragraph{one}\\paragraph{two}}")
            .Elements.OfType<Footnote>().Single();

        footnote.Elements.OfType<Paragraph>().Select(TextOf).Should().Equal("one", "two");
    }

    [Fact]
    public void AFootnoteKeepsTheAttributesGivenToIt()
    {
        var footnote = FirstParagraphOf("x\\footnote[Reference = \"*\"]{note}")
            .Elements.OfType<Footnote>().Single();

        footnote.Reference.Should().Be("*");
        TextOf(footnote.Elements[0] as Paragraph).Should().Be("note");
    }

    /// <summary>
    ///   A note inside a note is read as written, but the renderer refuses it - the inner note has
    ///   no page of its own to go at the foot of - so the reader warns, and says where, rather
    ///   than leaving the first word of it to a render that fails with no line number.
    /// </summary>
    [Fact]
    public void AFootnoteInsideAFootnoteIsReadAndWarnedAbout()
    {
        var errors = new DdlReaderErrors();

        var document = (Document)DdlReader.ObjectFromString(
            "\\document{\\section{\\paragraph{x\\footnote{outer\\footnote{inner}}}}}", errors);

        var outer = ((Paragraph)document.LastSection.Elements[0]).Elements.OfType<Footnote>().Single();
        var inner = ((Paragraph)outer.Elements[0]).Elements.OfType<Footnote>().Single();
        TextOf(inner.Elements[0] as Paragraph).Should().Be("inner");

        var warning = errors.Cast<DdlReaderError>().Should().ContainSingle().Subject;
        warning.ErrorLevel.Should().Be(DdlErrorLevel.Warning);
        warning.ErrorMessage.Should().Be(
            "A footnote inside another footnote cannot be rendered. "
            + "Move it to a paragraph in the section itself.");
        warning.SourceLine.Should().Be(1);
        warning.SourceColumn.Should().BeGreaterThan(0, "a warning says where it was raised");
        errors.ErrorCount.Should().Be(0, "a warning is not an error");
    }

    [Fact]
    public void AFootnoteDeeperInsideAFootnoteIsWarnedAboutToo()
    {
        // The inner note is inside formatted text inside a second paragraph of the outer one, so
        // the test is whether any footnote is above it, not whether its paragraph's parent is one.
        var errors = new DdlReaderErrors();

        DdlReader.ObjectFromString(
            "\\document{\\section{\\paragraph{x\\footnote{\\paragraph{one}\\paragraph{\\bold{b\\footnote{c}}}}}}}",
            errors);

        errors.Cast<DdlReaderError>().Should().ContainSingle()
            .Which.ErrorLevel.Should().Be(DdlErrorLevel.Warning);
    }

    [Fact]
    public void FootnotesSideBySideAreNotWarnedAbout()
    {
        ReadOnItsOwn("\\document{\\section{\\paragraph{a\\footnote{one} b\\footnote{two}}}}");
    }

    // ----- tables ---------------------------------------------------------------------------------

    [Fact]
    public void TheColumnsAndRowsOfATableCanCarryAttributesOfTheirOwn()
    {
        var table = SectionOf(
                "\\table{\\columns[Width = \"3cm\"]{\\column\\column}"
                + "\\rows[Height = \"1cm\"]{\\row[Height = \"2cm\"]{\\cell{a}\\cell{b}}\\row{\\cell{c}\\cell{d}}}}")
            .Elements[0] as Table;

        // ReSharper disable once PossibleNullReferenceException
        table.Columns.Width.Centimeter.Should().BeApproximately(3, 1e-4);
        table.Rows.Height.Centimeter.Should().BeApproximately(1, 1e-4);
        table.Rows[0].Height.Centimeter.Should().BeApproximately(2, 1e-4);
        table.Rows.Count.Should().Be(2);
    }

    [Fact]
    public void AColumnCanBeWrittenWithAnEmptyBlock()
    {
        var table = SectionOf(
                "\\table{\\columns{\\column[Width = \"2cm\"]{}\\column{}}\\rows{\\row{\\cell{a}\\cell{b}}}}")
            .Elements[0] as Table;

        // ReSharper disable once PossibleNullReferenceException
        table.Columns.Count.Should().Be(2);
        table.Columns[0].Width.Centimeter.Should().BeApproximately(2, 1e-4);
    }

    [Fact]
    public void ACellCanHoldParagraphsWrittenOutInFullAndCarryItsOwnFormat()
    {
        var table = SectionOf(
                "\\table{\\columns{\\column}\\rows{\\row{\\cell[Format{Alignment = Right}]"
                + "{\\paragraph{p}\\paragraph{q}}}}}")
            .Elements[0] as Table;

        // ReSharper disable once PossibleNullReferenceException
        table[0, 0].Format.Alignment.Should().Be(ParagraphAlignment.Right);
        table[0, 0].Elements.OfType<Paragraph>().Select(TextOf).Should().Equal("p", "q");
    }

    [Theory]
    [InlineData("\\row{\\cell{}}")]
    [InlineData("\\row{\\cell}")]
    [InlineData("\\row")]
    public void ARowAndItsCellsCanBeEmpty(string row)
    {
        var table = SectionOf("\\table{\\columns{\\column}\\rows{" + row + "}}").Elements[0] as Table;

        // ReSharper disable once PossibleNullReferenceException
        table.Rows.Count.Should().Be(1);
        table[0, 0].Elements.Count.Should().Be(0);
    }

    [Fact]
    public void ACellCanHoldATable()
    {
        var table = SectionOf(
                "\\table{\\columns{\\column}\\rows{\\row{\\cell{"
                + "\\table{\\columns{\\column}\\rows{\\row{\\cell{in}}}}}}}}")
            .Elements[0] as Table;

        // ReSharper disable once PossibleNullReferenceException
        var inner = table[0, 0].Elements.OfType<Table>().Single();
        TextOf(inner[0, 0].Elements[0] as Paragraph).Should().Be("in");
    }

    // ----- text frames and barcodes -------------------------------------------------------------------

    [Fact]
    public void ATextFrameCanHoldParagraphsWrittenOutInFull()
    {
        var frame = SectionOf("\\textframe{\\paragraph{a}\\paragraph{b}}").Elements[0] as TextFrame;

        // ReSharper disable once PossibleNullReferenceException
        frame.Elements.OfType<Paragraph>().Select(TextOf).Should().Equal("a", "b");
    }

    [Fact]
    public void AShapePositionCanBeANamedPlaceOrADistance()
    {
        // Left and Top are structures rather than numbers or enums, and are read by handing the
        // token to the structure to make sense of - a name and a measurement both go through it.
        var named = SectionOf("\\textframe[Left = Center Top = \"2cm\"]{f}").Elements[0] as TextFrame;
        var measured = SectionOf("\\textframe[Left = \"1cm\" Top = Bottom]{f}").Elements[0] as TextFrame;

        // ReSharper disable once PossibleNullReferenceException
        named.Left.ShapePosition.Should().Be(ShapePosition.Center);
        named.Top.Position.Centimeter.Should().BeApproximately(2, 1e-4);
        // ReSharper disable once PossibleNullReferenceException
        measured.Left.Position.Centimeter.Should().BeApproximately(1, 1e-4);
        measured.Top.ShapePosition.Should().Be(ShapePosition.Bottom);
    }

    [Fact]
    public void ABarcodeCanNameItsTypeAndCarryAttributes()
    {
        var barcode = SectionOf("\\barcode(\"12345\", Barcode39)[Width = \"4cm\"]").Elements[0] as Barcode;

        // ReSharper disable once PossibleNullReferenceException
        barcode.Code.Should().Be("12345");
        barcode.Type.Should().Be(BarcodeType.Barcode39);
        barcode.Width.Centimeter.Should().BeApproximately(4, 1e-4);
    }

    // ----- charts ---------------------------------------------------------------------------------

    [Fact]
    public void AChartCarriesTheAttributesGivenToIt()
    {
        var chart = SectionOf("\\chart(Line)[Width = \"8cm\"]{}").Elements[0] as Chart;

        // ReSharper disable once PossibleNullReferenceException
        chart.Width.Centimeter.Should().BeApproximately(8, 1e-4);
    }

    [Fact]
    public void EachOfTheThreeAxesIsReadOntoItself()
    {
        var chart = ChartFrom(
            "\\xaxis[MajorTickMark = Inside]\\yaxis[MajorTickMark = Outside]\\zaxis[MajorTickMark = Cross]");

        chart.XAxis.MajorTickMark.Should().Be(TickMarkType.Inside);
        chart.YAxis.MajorTickMark.Should().Be(TickMarkType.Outside);
        chart.ZAxis.MajorTickMark.Should().Be(TickMarkType.Cross);
    }

    [Fact]
    public void WhatIsBetweenTheBracesOfAnAxisIsSkipped()
    {
        var chart = ChartFrom("\\xaxis[Title{Caption = \"x\"}]{ ignored 1 2 3 }\\yaxis[HasMajorGridlines = true]");

        chart.XAxis.Title.Caption.Should().Be("x");
        chart.YAxis.HasMajorGridlines.Should().BeTrue("the axis after the skipped block is still read");
    }

    [Fact]
    public void ThePlotAreaCanBeNamedWithNothingAfterIt()
    {
        var chart = ChartFrom("\\plotarea\\yaxis[HasMajorGridlines = true]");

        chart.YAxis.HasMajorGridlines.Should().BeTrue();
    }

    [Fact]
    public void WhatIsBetweenTheBracesOfThePlotAreaIsSkipped()
    {
        var chart = ChartFrom("\\plotarea[TopPadding = \"1cm\"]{ anything 1 2 3 }\\yaxis[HasMajorGridlines = true]");

        chart.PlotArea.TopPadding.Centimeter.Should().BeApproximately(1, 1e-4);
        chart.YAxis.HasMajorGridlines.Should().BeTrue();
    }

    [Fact]
    public void ASeriesIsItsNumbersInOrderWithItsGaps()
    {
        var series = ChartFrom("\\series[Name = \"s\"]{1, null, 3.5}").SeriesCollection[0];

        series.Name.Should().Be("s");
        series.Count.Should().Be(3);
        ((Point)series.Elements[0]).Value.Should().Be(1);
        series.Elements[1].Should().BeNull("null is a gap");
        ((Point)series.Elements[2]).Value.Should().Be(3.5);
    }

    [Fact]
    public void APointInASeriesCanBeWrittenOutWithAttributesOfItsOwn()
    {
        var series = ChartFrom("\\series{\\point{4}, \\point[FillFormat{Color = Red}]{5}}").SeriesCollection[0];

        series.Count.Should().Be(2);
        ((Point)series.Elements[0]).Value.Should().Be(4);
        var second = (Point)series.Elements[1];
        second.Value.Should().Be(5);
        second.FillFormat.Color.Should().Be(Colors.Red);
    }

    [Fact]
    public void XValuesCanBeStringsNumbersAndGaps()
    {
        var chart = ChartFrom("\\xvalues{\"a\", \"b\", null, 3, 4.5, 0x10}");

        // XSeries keeps its values to itself, so what was read is looked at the way it is written.
        DdlWriter.WriteToString(chart.XValues[0])
            .Should().Contain("\"a\", \"b\", null, \"3\", \"4.5\", \"0x10\",");
    }

    [Fact]
    public void ALegendCanCarryAttributesAndWhatIsInItsBracesIsSkipped()
    {
        var chart = ChartFrom("\\leftarea{\\legend[Format{Font{Size = 7}}]{ignored}}");

        var legend = chart.LeftArea.Elements.OfType<Legend>().Single();
        legend.Format.Font.Size.Point.Should().BeApproximately(7, 1e-4);
    }

    // ----- attribute statements -------------------------------------------------------------------

    [Fact]
    public void AnAttributeCanBeReachedByADottedPath()
    {
        var paragraph = SectionOf("\\paragraph[Format.Font.Size = 10 Format.Font.Bold = true]{t}")
            .Elements[0] as Paragraph;

        // ReSharper disable once PossibleNullReferenceException
        paragraph.Format.Font.Size.Point.Should().BeApproximately(10, 1e-4);
        paragraph.Format.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void ATabStopCanBeAddedWithAttributesOfItsOwn()
    {
        // ReSharper disable once PossibleNullReferenceException
        var stops = (SectionOf(
                "\\paragraph[Format{TabStops += {Position = \"2cm\" Alignment = Right}}]{t}")
            .Elements[0] as Paragraph).Format.TabStops;

        stops.Count.Should().Be(1);
        stops[0].Position.Centimeter.Should().BeApproximately(2, 1e-4);
        stops[0].Alignment.Should().Be(TabAlignment.Right);
    }

    [Theory]
    [InlineData("2", 2.0)]
    [InlineData("2.5", 2.5)]
    public void ATabStopCanBeAddedAsABareNumberOfPoints(string position, double points)
    {
        // ReSharper disable once PossibleNullReferenceException
        var stops = (SectionOf("\\paragraph[Format{TabStops += " + position + "}]{t}")
            .Elements[0] as Paragraph).Format.TabStops;

        stops[0].Position.Point.Should().BeApproximately(points, 1e-4);
    }

    [Fact]
    public void NullClearsBordersShadingAndTabStops()
    {
        // A cleared value is not the same as one never set: it is written back out as null, which
        // is what lets a paragraph switch off what its style turned on.
        var paragraph = SectionOf(
                "\\paragraph[Format{Borders = null Shading = null TabStops = null}]{t}")
            .Elements[0] as Paragraph;

        // ReSharper disable once PossibleNullReferenceException
        paragraph.Format.Borders.BordersCleared.Should().BeTrue();
        paragraph.Format.TabStops.TabsCleared.Should().BeTrue();
        DdlWriter.WriteToString(paragraph).Should()
            .Contain("Borders = null").And.Contain("Shading = null").And.Contain("TabStops = null");
    }

    [Fact]
    public void NullClearsASingleBorder()
    {
        var paragraph = SectionOf("\\paragraph[Format{Borders{Top = null}}]{t}").Elements[0] as Paragraph;

        DdlWriter.WriteToString(paragraph).Should().Contain("Top = null");
    }

    [Theory]
    [InlineData(".5", 0.5)]
    [InlineData("1.", 1.0)]
    [InlineData("-3", -3.0)]
    [InlineData("+3", 3.0)]
    public void ANumberCanBeWrittenWithASignOrWithoutADigitOnOneSideOfThePoint(string literal, double points)
    {
        var paragraph = SectionOf("\\paragraph[Format{Font{Size = " + literal + "}}]{t}").Elements[0] as Paragraph;

        // ReSharper disable once PossibleNullReferenceException
        paragraph.Format.Font.Size.Point.Should().BeApproximately(points, 1e-4);
    }

    [Fact]
    public void AnIntegerAttributeCanBeWrittenAsAQuotedString()
    {
        Read("\\document{\\section[PageSetup{StartingNumber = \"5\"}]{\\paragraph{t}}}")
            .LastSection.PageSetup.StartingNumber.Should().Be(5);
    }

    [Fact]
    public void AColourCanBeWrittenAsADecimalNumber()
    {
        var paragraph = SectionOf("\\paragraph[Format{Font{Color = 16711680}}]{t}").Elements[0] as Paragraph;

        // The number is the whole ARGB value, alpha included, so sixteen million - FF0000 - has an
        // alpha of nought. That is what the writer's hex means as well.
        // ReSharper disable once PossibleNullReferenceException
        paragraph.Format.Font.Color.Argb.Should().Be(0x00FF0000u);
        DdlWriter.WriteToString(paragraph).Should().Contain("Color = 0xFF0000");
    }

    [Fact]
    public void AVerbatimStringWritesAQuoteByDoublingIt()
    {
        Read("\\document[Info{Title = @\"say \"\"hi\"\"\"}]{\\section{\\paragraph{t}}}")
            .Info.Title.Should().Be("say \"hi\"");
    }

    [Fact]
    public void ACarriageReturnBetweenTokensIsWhiteSpace()
    {
        Read("\\document\r{\\section{\\paragraph{t}}}").Sections.Count.Should().Be(1);
    }
}
