using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The single-character escapes in a quoted MDDDL string, and the refusal of any other letter
///   after a backslash. <c>DdlHexEscapeTests</c> covers <c>\x</c>; this pins the rest of
///   <c>DdlScanner.ScanStringLiteral</c>, read the same way: as a document title, which the writer
///   puts out as a quoted literal.
/// </summary>
public class DdlStringEscapeTests
{
    private const int Patience = 5000;

    private const string Placeholder = "PLACEHOLDER";

    private static string DocumentTitled(string literalBody)
    {
        var document = new Document { Info = { Title = Placeholder } };
        document.AddSection().AddParagraph().AddText("body");
        var ddl = DdlWriter.WriteToString(document);
        return ddl.Replace("\"" + Placeholder + "\"", "\"" + literalBody + "\"");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\a", "\a")]
    [InlineData("\\b", "\b")]
    [InlineData("\\f", "\f")]
    [InlineData("\\n", "\n")]
    [InlineData("\\r", "\r")]
    [InlineData("\\t", "\t")]
    [InlineData("\\v", "\v")]
    [InlineData("\\'", "'")]
    [InlineData("\\\"", "\"")]
    [InlineData("\\\\", "\\")]
    [InlineData("a\\tb\\\\c", "a\tb\\c")]
    public async Task ASimpleEscapeIsTheCharacterItNames(string literalBody, string expected)
    {
        var document = await Task.Run(() => DdlReader.DocumentFromString(DocumentTitled(literalBody)));

        document.Info.Title.Should().Be(expected);
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\q")]
    [InlineData("\\0")]
    [InlineData("a\\Nb")]
    public async Task AnyOtherEscapeIsRefused(string literalBody)
    {
        var complaints = await Task.Run(() => ReaderDiagnostics.ComplaintsAbout(DocumentTitled(literalBody)));

        complaints.Should().Contain("Invalid escape sequence.");
    }
}
