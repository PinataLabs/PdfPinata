using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   What ends a line of MDDDL. The scanner looks for an LF wherever a line matters - the end of
///   a <c>//</c> comment, the end of a paragraph's content at the top level, the line number an
///   error is reported at - and skips a CR that comes before one. A CR on its own, which is how a
///   classic Mac OS file ends its lines, used to be skipped as well, so such a file was one long
///   line: the first comment swallowed the rest of the document, and every error was on line 1.
///   <para>
///   Each read runs under a timeout, because a scanner that misses the end of a line can miss the
///   end of the document with it. xUnit honours <c>Timeout</c> only on an async test, hence the
///   <c>Task.Run</c>.
///   </para>
/// </summary>
public class DdlLineEndTests
{
    const int Patience = 5000;

    /// <summary>A document with a comment line, written with LF line ends.</summary>
    const string Commented =
        "\\document\n"
        + "{\n"
        + "  // The comment runs to the end of this line and no further.\n"
        + "  \\section\n"
        + "  {\n"
        + "    \\paragraph\n"
        + "    {\n"
        + "      First \\bold{bold}\n"
        + "    }\n"
        + "    // A second comment, between the paragraphs.\n"
        + "    \\paragraph{Second}\n"
        + "  }\n"
        + "}\n";

    /// <summary>A document whose fifth line holds a colour that is not one.</summary>
    const string FaultyOnLineFive =
        "\\document\n"
        + "{\n"
        + "  // A comment.\n"
        + "  \\section{\n"
        + "    \\paragraph{\\fontcolor(RGB(1, 2)){x}}\n"
        + "  }\n"
        + "}\n";

    static string WithLineEnds(string ddl, string lineEnd) => ddl.Replace("\n", lineEnd);

    static Task<string> RereadAndWritten(string ddl) =>
        Task.Run(() => DdlWriter.WriteToString(DdlReader.DocumentFromString(ddl)));

    [Theory(Timeout = Patience)]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task ADocumentReadsTheSameWhateverEndsItsLines(string lineEnd)
    {
        var expected = await RereadAndWritten(Commented);

        (await RereadAndWritten(WithLineEnds(Commented, lineEnd))).Should().Be(expected);
    }

    [Fact(Timeout = Patience)]
    public async Task ACommentInADocumentWithCarriageReturnsAloneEndsAtTheEndOfItsLine()
    {
        var document = await Task.Run(() => DdlReader.DocumentFromString(WithLineEnds(Commented, "\r")));

        document.Sections.Count.Should().Be(1);
        document.LastSection.Elements.Count.Should().Be(2,
            "the comment before the section would otherwise have swallowed it");
        string.Concat(((Paragraph)document.LastSection.Elements[1]).Elements.OfType<Text>().Select(text => text.Content))
            .Should().Be("Second");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task AnErrorIsReportedOnTheLineItIsOn(string lineEnd)
    {
        var errors = await Task.Run(() =>
        {
            var reported = new DdlReaderErrors();
            try
            {
                DdlReader.ObjectFromString(WithLineEnds(FaultyOnLineFive, lineEnd), reported);
            }
            catch (System.Exception)
            {
                // Only what was reported matters here, and where.
            }
            return ReaderDiagnostics.ErrorsIn(reported);
        });

        errors.Should().NotBeEmpty();
        errors[0].ErrorMessage.Should().Be("',' expected, found ')'.");
        errors[0].SourceLine.Should().Be(5);
    }

    [Fact(Timeout = Patience)]
    public async Task TwoCarriageReturnsAreTwoLineEndsAndACarriageReturnBeforeALineFeedIsOne()
    {
        // "\r\r\n" is a lone CR followed by a CRLF: two line ends, so the fault that follows is on
        // line 3. Were the CRLF counted twice it would be on line 4, and were the lone CR skipped
        // on line 2.
        var errors = await Task.Run(() =>
        {
            var reported = new DdlReaderErrors();
            try
            {
                DdlReader.ObjectFromString(
                    "\\document{\r\r\n\\section{\\paragraph{\\fontcolor(RGB(1, 2)){x}}}}", reported);
            }
            catch (System.Exception)
            {
                // As above.
            }
            return ReaderDiagnostics.ErrorsIn(reported);
        });

        errors.Should().NotBeEmpty();
        errors[0].SourceLine.Should().Be(3);
    }
}
