using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="DdlEncoder"/> is the writing half of MDDDL's escaping: the two public methods that
///   turn a string into something the scanner will read back as that string. They escape different
///   things, because they are for different places in the grammar - running paragraph text, where a
///   brace would open a nested element and <c>//</c> would begin a comment, and a quoted literal,
///   where neither means anything but a quote ends the string.
///   <para>
///   <c>StringToText</c> had one caller and 37% coverage; <c>StringToLiteral</c> had neither a
///   caller anywhere in the repository nor a single executed line. Both are public, so the second is
///   API a consumer can reach even though nothing here does.
///   </para>
///   <para>
///   The escaping tables below say what each produces. The round trips after them are the assertion
///   that matters: the point of an escape is not its spelling but that the scanner gives the string
///   back, so those write a real document and read it again.
///   </para>
/// </summary>
public class DdlEncoderTests
{
    static Document RoundTrip(Document document) =>
        DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

    // ReSharper disable PossibleNullReferenceException
    static string TextOf(Document document) =>
        string.Concat((document.LastSection.Elements[0] as Paragraph)
            .Elements.OfType<Text>().Select(text => text.Content));
    // ReSharper restore PossibleNullReferenceException

    static Document DocumentSaying(string text)
    {
        var document = new Document();
        document.AddSection().AddParagraph().AddText(text);
        return document;
    }

    // ----- StringToText: running paragraph text ---------------------------------------------------

    [Theory]
    // Nothing that means anything to the scanner passes through untouched.
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a b c", "a b c")]
    // A backslash is the escape character, so it escapes itself.
    [InlineData("\\", "\\\\")]
    [InlineData("a\\b", "a\\\\b")]
    // Braces open and close nested elements in paragraph content.
    [InlineData("{", "\\{")]
    [InlineData("}", "\\}")]
    [InlineData("{}", "\\{\\}")]
    [InlineData("a{b}c", "a\\{b\\}c")]
    // Two slashes begin a comment; one does not, and is left alone.
    [InlineData("/", "/")]
    [InlineData("a/b", "a/b")]
    [InlineData("//", "\\//")]
    [InlineData("a//b", "a\\//b")]
    // Every slash that begins a pair is escaped, so a run of them cannot leave two adjacent
    // unescaped slashes behind. See finding F22: escaping only the first of each pair left
    // "///" as "\///", which reads back as one slash and then a comment.
    [InlineData("///", "\\/\\//")]
    [InlineData("////", "\\/\\/\\//")]
    // A slash at the very end has no character after it to look at, which is the bound the
    // lookahead exists for.
    [InlineData("a/", "a/")]
    public void TextEscapesOnlyWhatWouldEndTheTextEarly(string input, string expected)
    {
        DdlEncoder.StringToText(input).Should().Be(expected);
    }

    [Fact]
    public void TextHandsBackNullRatherThanEncodingIt()
    {
        // Unlike StringToLiteral, which answers an empty literal. Pinned because the two public
        // methods of the same class disagree about null and either could look like the mistake.
        DdlEncoder.StringToText(null).Should().BeNull();
    }

    // ----- StringToLiteral: a quoted string --------------------------------------------------------

    [Theory]
    // Nothing to escape, but always quoted.
    [InlineData("abc", "\"abc\"")]
    [InlineData("a b c", "\"a b c\"")]
    // The quote would end the literal, and the backslash escapes itself.
    [InlineData("\"", "\"\\\"\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("\\", "\"\\\\\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    // Braces and comment markers mean nothing inside a literal, so they are not escaped. This is
    // the difference from StringToText, and it is deliberate rather than an omission.
    [InlineData("{}", "\"{}\"")]
    [InlineData("//", "\"//\"")]
    [InlineData("a{b}//c", "\"a{b}//c\"")]
    public void ALiteralEscapesOnlyTheQuoteAndTheBackslash(string input, string expected)
    {
        DdlEncoder.StringToLiteral(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingAtAllIsWrittenAsAnEmptyLiteral(string input)
    {
        DdlEncoder.StringToLiteral(input).Should().Be("\"\"");
    }

    // ----- the round trips, which are what the escaping is for -------------------------------------

    [Theory]
    [InlineData("plain")]
    [InlineData("a b c")]
    [InlineData("a\\b")]
    [InlineData("a{b}c")]
    [InlineData("a}b{c")]
    [InlineData("a//b")]
    [InlineData("a/b")]
    [InlineData("a///b")]
    [InlineData("a////b")]
    [InlineData("C:\\Temp\\file.txt")]
    [InlineData("100% of {this} is a//comment")]
    public void TextSurvivesBeingWrittenAndReadAgain(string text)
    {
        TextOf(RoundTrip(DocumentSaying(text))).Should().Be(text,
            "the point of an escape is that the scanner gives the string back");
    }

    /// <summary>
    ///   A known defect, pinned so that fixing it is visible rather than silent - see the backlog
    ///   spec's finding F23.
    /// </summary>
    /// <remarks>
    ///   A paragraph carrying no style or format of its own is written as bare text inside the
    ///   section, without the <c>\paragraph</c> keyword around it. The escapes in that text are
    ///   only honoured once the scanner is reading paragraph content, and it is the first plain
    ///   character that gets it there - so text whose <em>first</em> character needs escaping is
    ///   written correctly and then read at section level, where <c>\{</c> is a keyword rather than
    ///   an escaped brace. The brace nesting goes wrong and the file will not read back.
    ///   <para>
    ///   Any text starting with a brace, or with a slash that begins a comment, is enough. The same
    ///   text with a single letter in front of it round trips, which is what the theory above says.
    ///   </para>
    /// </remarks>
    [Theory]
    [InlineData("{}")]
    [InlineData("{a}")]
    [InlineData("}")]
    [InlineData("//")]
    [InlineData("///")]
    public void TextBeginningWithAnEscapedCharacterCannotBeReadBack(string text)
    {
        var ddl = DdlWriter.WriteToString(DocumentSaying(text));

        var reading = () => DdlReader.DocumentFromString(ddl);

        reading.Should().Throw<Exception>("the escape is written but read outside paragraph content");
    }

    [Fact]
    public void ADocumentTitleSurvivesTheQuotesAndBackslashesInIt()
    {
        // The literal path rather than the text path: Info.Title is written as a quoted string.
        var document = DocumentSaying("body");
        document.Info.Title = "A \"quoted\" title with a \\ in it";

        RoundTrip(document).Info.Title.Should().Be("A \"quoted\" title with a \\ in it");
    }
}
