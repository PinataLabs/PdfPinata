using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   What the reader says about MDDDL written wrongly, keyword by keyword. Every block the parser
///   reads - a style, a header, a table, a chart, a series, an attribute statement - has its own
///   <c>catch</c> that reports the fault and skips to the end of the block, so what a caller is
///   told, and how much of the document survives, depends on where the mistake is.
///   <para>
///   The first complaint is the one asserted. It is the one that names the mistake; a fault deep
///   inside nested braces is often followed by more, because skipping to the end of the block
///   that failed leaves the parser one brace out, and the read then ends by throwing
///   "End of file expected". Those followers are the recovery's doing rather than the text's,
///   and nothing here depends on them.
///   </para>
///   <para>
///   Every read runs under a timeout. Some malformed documents are never finished with at all -
///   <c>DdlReadingTests</c> pins the ones known - and a test that meets a new one must fail
///   rather than take the test host with it. xUnit honours <c>Timeout</c> only on an async test,
///   hence the <c>Task.Run</c>.
///   </para>
/// </summary>
public class DdlMalformedInputTests
{
    const int Patience = 5000;

    static Task<IReadOnlyList<string>> ComplaintsAbout(string ddl) =>
        Task.Run(() => ReaderDiagnostics.ComplaintsAbout(ddl));

    static Task<IReadOnlyList<string>> ComplaintsAboutParagraph(string paragraphBody) =>
        ComplaintsAbout("\\document{\\section{\\paragraph{" + paragraphBody + "}}}");

    static Task<IReadOnlyList<string>> ComplaintsAboutChart(string chartBody) =>
        ComplaintsAbout("\\document{\\section{\\chart(Line){" + chartBody + "}\\paragraph{after}}}");

    static Task<IReadOnlyList<string>> ComplaintsAboutParagraphFormat(string formatBody) =>
        ComplaintsAbout("\\document{\\section{\\paragraph[Format{" + formatBody + "}]{t}}}");

    /// <summary>
    ///   Reads a document the reader is expected to finish despite what is wrong with it, and
    ///   hands back what it read along with the errors - leaving out warnings - it reported.
    /// </summary>
    static Task<(Document Document, IReadOnlyList<DdlReaderError> Errors)> ReadDespite(string ddl) =>
        Task.Run(() =>
        {
            var errors = new DdlReaderErrors();
            var document = (Document)DdlReader.ObjectFromString(ddl, errors);
            return (document, ReaderDiagnostics.ErrorsIn(errors));
        });

    static string TextOf(Paragraph paragraph) =>
        string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content));

    // ----- styles -------------------------------------------------------------------------------------

    [Fact(Timeout = Patience)]
    public async Task AColonWithNoBaseStyleAfterItIsAnErrorAndTheNextStyleIsStillRead()
    {
        // The definition block after the colon is what is skipped, so the style after it is
        // where reading picks up again.
        var (document, errors) = await ReadDespite(
            "\\document{\\styles{Mine : {Font{Size = 8}} Other : Normal{Font{Size = 9}}}"
            + "\\section{\\paragraph{t}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Invalid style name 'Mine'.");
        document.Styles["Other"].Font.Size.Point.Should().BeApproximately(9, 1e-4);
        document.Sections.Count.Should().Be(1, "the rest of the document is read");
    }

    [Fact(Timeout = Patience)]
    public async Task ABaseStyleNameThatIsNotANameIsAnError()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\styles{Mine : 3}\\section{\\paragraph{t}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Invalid style name 'Mine'.");
        document.Sections.Count.Should().Be(1, "the rest of the document is read");
    }

    // ----- headers, text frames and barcodes: a block that does not open --------------------------

    [Theory(Timeout = Patience)]
    [InlineData("\\header x \\paragraph{t}")]
    [InlineData("\\textframe x \\paragraph{t}")]
    public async Task ABlockWithNoBraceIsReportedAndTheNextSectionIsStillRead(string sectionBody)
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{" + sectionBody + "}\\section{\\paragraph{second}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("'{' expected, found 'x'.");
        document.Sections.Count.Should().Be(2);
        TextOf(document.Sections[1].Elements[0] as Paragraph).Should().Be("second");
    }

    [Fact(Timeout = Patience)]
    public async Task ABarcodeWithNoOpeningParenthesisIsReported()
    {
        var (document, errors) = await ReadDespite("\\document{\\section{\\barcode \"12345\"}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Missing left parenthesis after '\\barcode'.");
        document.LastSection.Elements.OfType<Barcode>().Should().BeEmpty(
            "the barcode is not created until its code has been read");
    }

    [Fact(Timeout = Patience)]
    public async Task ABarcodeWithNoClosingParenthesisIsReportedAndKeepsItsCode()
    {
        var (document, errors) = await ReadDespite("\\document{\\section{\\barcode(\"12345\" x)}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Missing right parenthesis after '\\barcode'.");
        document.LastSection.Elements.OfType<Barcode>().Single().Code.Should().Be("12345");
    }

    [Fact(Timeout = Patience)]
    public async Task ABarcodeOfNoKnownTypeIsReportedAndTheNextSectionIsStillRead()
    {
        // Worded as an enum attribute of the wrong value is, "'value' 'attribute'.", because
        // the type in the parentheses is the barcode's Type attribute written another way.
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\barcode(\"12345\", NoSuchType)}\\section{\\paragraph{second}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("'NoSuchType' 'type'.");
        document.Sections[0].Elements.OfType<Barcode>().Single().Code.Should().Be("12345");
        document.Sections.Count.Should().Be(2);
        TextOf(document.Sections[1].Elements[0] as Paragraph).Should().Be("second");
    }

    [Fact(Timeout = Patience)]
    public async Task ABarcodeWhoseCodeIsNotAStringNamesWhatWasThere()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\barcode(12345)}\\section{\\paragraph{second}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Unexpected symbol '12345'.");
        document.Sections.Count.Should().Be(2);
        TextOf(document.Sections[1].Elements[0] as Paragraph).Should().Be("second");
    }

    // ----- paragraph content -------------------------------------------------------------------------

    [Theory(Timeout = Patience)]
    [InlineData("a\\nosuch b", "Unexpected symbol '\\nosuch'.")]
    [InlineData("a{b}", "Unexpected symbol '{'.")]
    [InlineData("\\bold{x}{y}", "Unexpected symbol '{'.")]
    [InlineData("a\\space{b}", "Unexpected symbol '{'.")]
    public async Task SomethingThatCannotBeInAParagraphIsNamed(string paragraphBody, string complaint)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First().Should().Be(complaint);
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\bold x", "'{' expected, found 'x'.")]
    [InlineData("\\font[Bold = true] x", "'{' expected, found 'x'.")]
    [InlineData("\\footnote x", "'{' expected, found 'x'.")]
    [InlineData("\\hyperlink x", "'{' expected, found 'x'.")]
    [InlineData("\\fontsize 3{x}", "'(' expected, found '3'.")]
    [InlineData("\\fontsize(3 x", "')' expected, found 'x'.")]
    [InlineData("\\fontsize(14pt){x}", "')' expected, found 'pt'.")]
    [InlineData("\\fontsize(3)x", "'{' expected, found 'x'.")]
    [InlineData("\\fontcolor Red{x}", "'(' expected, found 'Red'.")]
    [InlineData("\\fontcolor(Red x", "')' expected, found 'x'.")]
    [InlineData("\\fontcolor(Red)x", "'{' expected, found 'x'.")]
    [InlineData("\\field Page", "'(' expected, found 'Page'.")]
    [InlineData("\\field(Page x", "')' expected, found 'x'.")]
    [InlineData("\\symbol Euro", "'(' expected, found 'Euro'.")]
    [InlineData("\\chr(65 x)", "')' expected, found 'x'.")]
    [InlineData("\\space(3 x)", "')' expected, found 'x'.")]
    public async Task AParagraphKeywordMissingPartOfItsSyntaxSaysWhatWasExpected(string paragraphBody, string complaint)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First().Should().Be(complaint);
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\fontcolor(\"Red\"){x}", "Invalid color: 'Red'.")]
    [InlineData("\\fontcolor(HSB){x}", "Invalid color: 'HSB'.")]
    [InlineData("\\fontcolor(Lab){x}", "Invalid color: 'Lab'.")]
    [InlineData("\\fontcolor(0x1G){x}", "Invalid color: '0x1G'.")]
    [InlineData("\\fontcolor(99999999999){x}", "Invalid color: '99999999999'.")]
    [InlineData("\\fontcolor(NoSuch){x}", "Invalid color: 'NoSuch'.")]
    [InlineData("\\fontcolor(RGB(1, 2)){x}", "',' expected, found ')'.")]
    public async Task AFontColorThatIsNotAColourIsReported(string paragraphBody, string complaint)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First().Should().Be(complaint);
    }

    [Fact(Timeout = Patience)]
    public async Task AStyleNamedByFormattedTextHasToBeQuoted()
    {
        (await ComplaintsAboutParagraph("\\font(Heading1){x}")).First()
            .Should().Be("String expected: 'Heading1'.");
    }

    [Fact(Timeout = Patience)]
    public async Task ASymbolNameMustBeAName()
    {
        (await ComplaintsAboutParagraph("\\symbol(123)")).First().Should().Be("Unexpected symbol '123'.");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\symbol(Tab)", "Symbol not valid 'Tab'.", "a tab is a symbol name, but not one \\symbol draws")]
    [InlineData("\\space(Euro)", "'Euro' '\\space'.", "the euro is a symbol, not a kind of space")]
    [InlineData("\\space(Nope)", "'Nope' '\\space'.", "and this is not a name at all")]
    [InlineData("\\space(em)", "'em' '\\space'.", "the names are case sensitive here, where an enum attribute's are not")]
    public async Task ASymbolOrSpaceOfTheWrongKindIsRefused(string paragraphBody, string complaint, string why)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First().Should().Be(complaint, why);
    }

    // ----- numbers in paragraph content ------------------------------------------------------------------

    [Theory(Timeout = Patience)]
    [InlineData("\\chr(0x1G)")]
    [InlineData("\\chr(65, 0x1G)")]
    [InlineData("\\symbol(Euro, 0x1G)")]
    public async Task AHexNumberWithALetterThatIsNotAHexDigitIsReported(string paragraphBody)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First().Should().Be("Integer expected: '0x1G'.");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\chr(99999999999)")]
    [InlineData("\\chr(65, 99999999999)")]
    [InlineData("\\chr(0x123456789)")]
    [InlineData("\\space(99999999999)")]
    [InlineData("\\space(-99999999999)")]
    [InlineData("\\space(Em, 99999999999)")]
    [InlineData("\\symbol(Euro, 99999999999)")]
    public async Task ANumberTooLargeForAnIntegerIsReported(string paragraphBody)
    {
        (await ComplaintsAboutParagraph(paragraphBody)).First()
            .Should().Be("Valid range only within '-2147483648 - 2147483647'.");
    }

    // ----- tables --------------------------------------------------------------------------------------

    [Theory(Timeout = Patience)]
    [InlineData("\\table{\\rows{\\row{\\cell{x}}}}", "'\\columns' expected, found '\\rows'.")]
    [InlineData("\\table{\\columns{\\column\\row}\\rows{\\row{\\cell{a}}}}", "'\\column' expected, found '\\row'.")]
    [InlineData("\\table{\\columns{\\column{x}}\\rows{\\row{\\cell{a}}}}", "'}' expected, found 'x'.")]
    [InlineData("\\table{\\columns{\\column}\\rows{\\column}}", "'\\row' expected, found '\\column'.")]
    [InlineData("\\table{\\columns{\\column}\\rows{\\row{\\column}}}", "Unexpected symbol '\\column'.")]
    public async Task ATableOutOfOrderSaysWhatItExpected(string table, string complaint)
    {
        (await ComplaintsAbout("\\document{\\section{" + table + "\\paragraph{after}}}")).First()
            .Should().Be(complaint);
    }

    // ----- charts ---------------------------------------------------------------------------------------

    [Fact(Timeout = Patience)]
    public async Task AChartOfNoKnownTypeIsNotAddedAndTheRestOfTheSectionIsRead()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\chart(NoSuchType){}\\paragraph{after}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Unknown chart type: 'NoSuchType'");
        document.LastSection.Elements.OfType<Chart>().Should().BeEmpty();
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\nosuch")]
    [InlineData("\\toparea{\\nosuch}")]
    public async Task AKeywordAChartDoesNotKnowIsNamed(string chartBody)
    {
        (await ComplaintsAboutChart(chartBody)).First().Should().Be("Unexpected symbol '\\nosuch'.");
    }

    [Fact(Timeout = Patience)]
    public async Task AnAttributeThePlotAreaDoesNotHaveIsNamedAndTheChartIsStillRead()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\chart(Line){\\plotarea[NoSuch = 1]\\yaxis[HasMajorGridlines = true]}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Invalid value name: 'NoSuch'.");
        document.LastSection.Elements.OfType<Chart>().Single().YAxis.HasMajorGridlines.Should().BeTrue();
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\plotarea[3]")]
    [InlineData("\\xaxis[3]")]
    [InlineData("\\leftarea{\\legend[3]}")]
    public async Task AnAttributeBlockInAChartThatHoldsNoAttributeIsReported(string chartBody)
    {
        (await ComplaintsAboutChart(chartBody)).First().Should().Be("']' expected, found '3'.");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\series{1 2}")]
    [InlineData("\\series{1 null}")]
    [InlineData("\\series{1 \\point{2}}")]
    [InlineData("\\xvalues{\"a\" \"b\"}")]
    [InlineData("\\xvalues{\"a\" null}")]
    public async Task TwoValuesInASeriesWithNoCommaBetweenThemAreReported(string chartBody)
    {
        (await ComplaintsAboutChart(chartBody)).First().Should().Be("Missing comma.");
    }

    [Fact(Timeout = Patience)]
    public async Task SomethingThatIsNotAValueInTheXValuesIsNamed()
    {
        (await ComplaintsAboutChart("\\xvalues{\"a\", \\point}")).First().Should().Be("Unexpected symbol '\\point'.");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\series 1, 2", "Missing left brace after '\\series'.")]
    [InlineData("\\xvalues \"a\"", "Missing left brace after '\\xvalues'.")]
    [InlineData("\\series{\\point 4}", "Missing left brace after '\\point'.")]
    public async Task ASeriesOrPointWithNoOpeningBraceIsReportedAndTheSectionIsStillRead(
        string chartBody, string complaint)
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\chart(Line){" + chartBody + "}\\paragraph{after}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(complaint);
        TextOf(document.LastSection.Elements.OfType<Paragraph>().Single()).Should().Be("after");
    }

    [Fact(Timeout = Patience)]
    public async Task APointHoldingMoreThanOneNumberIsReported()
    {
        (await ComplaintsAboutChart("\\series{\\point{4 5}}")).First()
            .Should().Be("Missing right brace after '\\point'.");
    }

    [Fact(Timeout = Patience)]
    public async Task AChartWithNoOpeningParenthesisIsReportedAndTheSectionIsStillRead()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\chart Line{}}\\section{\\paragraph{second}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Missing left parenthesis after '\\chart'.");
        TextOf(document.Sections[1].Elements[0] as Paragraph).Should().Be("second");
    }

    // ----- attribute statements ---------------------------------------------------------------------------

    [Fact(Timeout = Patience)]
    public async Task AnAttributePathCannotReachAnInternalName()
    {
        var (document, errors) = await ReadDespite(
            "\\document{\\section{\\paragraph[Format._Font = 1]{t}}\\section{\\paragraph{u}}}");

        errors.Should().ContainSingle().Which.ErrorMessage
            .Should().Be("Access denied: '_Font' for internal use only.");
        TextOf(document.Sections[0].Elements[0] as Paragraph).Should().Be("t", "the paragraph is still read");
        document.Sections.Count.Should().Be(2);
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\paragraph[Format.Alignment.X = 1]{t}", "Symbol 'Alignment' is not an object.")]
    [InlineData("\\paragraph[Format{Alignment{}}]{t}", "Symbol 'Alignment' is not an object.")]
    [InlineData("\\paragraph[Format. = 1]{t}", "Invalid value name: '='.")]
    [InlineData("\\paragraph[Format{Font : 3}]{t}", "Symbol ':' in this context not allowed.")]
    [InlineData("\\paragraph[Format{TabStops += Center}]{t}", "Unexpected symbol 'Center'.")]
    public async Task AnAttributeStatementOfTheWrongShapeIsReportedAndTheParagraphIsStillRead(
        string paragraph, string complaint)
    {
        var (document, errors) = await ReadDespite("\\document{\\section{" + paragraph + "}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(complaint);
        TextOf(document.LastSection.Elements[0] as Paragraph).Should().Be("t");
    }

    [Fact(Timeout = Patience)]
    public async Task PlusEqualsAgainstAnythingButAParagraphFormatIsRefused()
    {
        // Its twin in DdlCharacterAndPunctuationTests is the right object and the wrong property;
        // this is the wrong object altogether.
        var (document, errors) = await ReadDespite(
            "\\document{\\section[PageSetup{TopMargin += \"1cm\"}]{\\paragraph{t}}}");

        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Symbol '+=' in this context not allowed.");
        TextOf(document.LastSection.Elements[0] as Paragraph).Should().Be("t");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\section[\\-]{\\paragraph{t}}", "']' expected, found '\\-'.")]
    [InlineData("\\section[\\(]{\\paragraph{t}}", "']' expected, found '\\('.")]
    [InlineData("\\section{\\paragraph[Format{Font{Bold = true}} ; ]{t}}", "']' expected, found ';'.")]
    public async Task SomethingInAnAttributeBlockThatIsNotAnAttributeIsNamed(string section, string complaint)
    {
        (await ComplaintsAbout("\\document{" + section + "}")).First().Should().Be(complaint);
    }

    [Theory(Timeout = Patience)]
    [InlineData("Font{Bold = 1}", "Bool expected: '1'.")]
    [InlineData("Font{Name = 3}", "String expected: '3'.")]
    [InlineData("Font{Color = ;}", "String expected: ';'.")]
    [InlineData("Font{Size = \"big\"}", "'big' is not a valid value")]
    [InlineData("LineSpacing = \"x\"", "'x' is not a valid value")]
    [InlineData("Alignment = Nowhere", "'Nowhere' 'alignment'.")]
    [InlineData("Font{Underline = NoSuch}", "'NoSuch' 'underline'.")]
    [InlineData("Font = null", "Assign 'null' to 'font' not allowed.")]
    [InlineData("Font = 3", "Invalid assignment to 'font'.")]
    [InlineData("NoSuch{Size = 3}", "Invalid value name: 'NoSuch'.")]
    [InlineData("Font{Color = \"Red\"}", "ParseColor(color-name)")]
    [InlineData("Font{Color = 0x1G}", "Invalid assignment to 'color'.")]
    [InlineData("Alignment = 3", "Identifier expected: '3'.")]
    public async Task AValueOfTheWrongKindIsNamedInTheFirstComplaint(string formatBody, string complaint)
    {
        (await ComplaintsAboutParagraphFormat(formatBody)).First().Should().Contain(complaint);
    }

    [Theory(Timeout = Patience)]
    [InlineData("1.5", "Integer expected: '1.5'.")]
    [InlineData("\"five\"", "'five' was not in a correct format")]
    [InlineData("= 5", "Integer expected: '='.")]
    public async Task AnIntegerAttributeRefusesWhatIsNotAnInteger(string literal, string complaint)
    {
        (await ComplaintsAbout("\\document{\\section[PageSetup{StartingNumber = " + literal + "}]{\\paragraph{t}}}"))
            .First().Should().Contain(complaint);
    }

    [Fact(Timeout = Patience)]
    public async Task AShapePositionThatIsNeitherAPlaceNorADistanceIsNamed()
    {
        (await ComplaintsAbout("\\document{\\section{\\textframe[Left = Nowhere]{framed}}}"))
            .First().Should().Contain("'Nowhere'");
    }

    [Fact(Timeout = Patience)]
    public async Task ANumberWithTwoPointsIsReadAsTwoNumbers()
    {
        // "1.2" is a number and ".3" is another: a point with a digit after it starts a real
        // literal of its own, so the second point is not a syntax error inside the first number.
        (await ComplaintsAboutParagraphFormat("Font{Size = 1.2.3}")).First()
            .Should().Be("'}' expected, found '.3'.");
    }

    // ----- string literals --------------------------------------------------------------------------------

    [Fact(Timeout = Patience)]
    public async Task AStringLiteralCannotRunOverALineEnd()
    {
        (await ComplaintsAbout("\\document[Info{Title = \"a\nb\"}]{\\section{\\paragraph{t}}}"))
            .First().Should().Be("Newline in string not allowed.");
    }
}
