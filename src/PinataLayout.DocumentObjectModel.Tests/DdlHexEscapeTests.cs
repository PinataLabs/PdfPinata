using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The <c>\x</c> escape in a quoted MDDDL string: one or two hex digits naming a character.
///   <para>
///   The scanner used to read the digits and then append the literal text <c>?????</c> in place
///   of the character they named, with the conversion left as a comment beside it. It also
///   stepped over the character after the last digit, because reading the digits had already
///   left it there and the bottom of the string loop advanced again - so <c>"\x41 b"</c> lost its
///   space, and <c>"\x41"</c> lost its closing quote and ran on to the end of the line, where it
///   was reported as a newline in a string.
///   </para>
///   <para>
///   The strings here are written into a document title, which the writer puts out as a quoted
///   literal, because that is the path the escape is read on. Every read runs under a timeout:
///   a string whose closing quote is missed can take the rest of the document with it.
///   </para>
/// </summary>
public class DdlHexEscapeTests
{
    const int Patience = 5000;

    const string Placeholder = "PLACEHOLDER";

    /// <summary>
    ///   A whole document whose title is the given literal body, written between quotes exactly as
    ///   given - escapes and all - and followed by a paragraph, so that a string which fails to end
    ///   where it should shows up as a document that no longer reads.
    /// </summary>
    static string DocumentTitled(string literalBody)
    {
        var document = new Document();
        document.Info.Title = Placeholder;
        document.AddSection().AddParagraph().AddText("body");
        var ddl = DdlWriter.WriteToString(document);
        ddl.Should().Contain("\"" + Placeholder + "\"", "the title is written as a quoted literal");
        return ddl.Replace("\"" + Placeholder + "\"", "\"" + literalBody + "\"");
    }

    static Task<Document> Read(string literalBody) =>
        Task.Run(() => DdlReader.DocumentFromString(DocumentTitled(literalBody)));

    static string TextOf(Document document) =>
        string.Concat(((Paragraph)document.LastSection.Elements[0]).Elements.OfType<Text>().Select(text => text.Content));

    [Theory(Timeout = Patience)]
    [InlineData("\\x41", "A")]
    [InlineData("\\x7e", "~")]
    [InlineData("\\x7E", "~")]
    [InlineData("\\x9", "\t")]
    [InlineData("\\xe9", "é")]
    [InlineData("a\\x42z", "aBz")]
    [InlineData("\\x41\\x42", "AB")]
    public async Task AHexEscapeIsTheCharacterItsDigitsName(string literalBody, string expected)
    {
        var document = await Read(literalBody);

        document.Info.Title.Should().Be(expected);
    }

    [Fact(Timeout = Patience)]
    public async Task TheCharacterAfterTheDigitsIsKept()
    {
        var document = await Read("\\x41 b");

        document.Info.Title.Should().Be("A b", "the space after the digits is part of the string");
    }

    [Fact(Timeout = Patience)]
    public async Task AnEscapeAtTheEndOfTheStringStillLetsTheStringEnd()
    {
        // The closing quote is the character after the digits here, and stepping over it is what
        // ran the string on into the next line.
        var document = await Read("A\\x41");

        document.Info.Title.Should().Be("AA");
        TextOf(document).Should().Be("body", "everything after the title is still read");
    }

    [Theory(Timeout = Patience)]
    [InlineData("\\x")]
    [InlineData("\\xg")]
    [InlineData("\\x414")]
    // A letter that is a hex digit counts as one, so this is three digits rather than two and a c.
    [InlineData("a\\x42c")]
    public async Task AnEscapeWithNoDigitsOrMoreThanTwoIsRefused(string literalBody)
    {
        var complaints = await Task.Run(() => ReaderDiagnostics.ComplaintsAbout(DocumentTitled(literalBody)));

        complaints.Should().Contain("Invalid escape sequence.");
    }
}
