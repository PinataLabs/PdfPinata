using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.IO;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Every field is written by <c>Serializer.WriteField</c>: <c>\field(Kind)</c>, then its name
///   and format in brackets. The field serializers used to build that string for themselves, and
///   had drifted on escaping - the bookmark field escaped a backslash and a quote in its name, the
///   page reference field wrote its name raw, and no field escaped its format - so a name or a
///   format with a quote in it serialized to DDL that did not read back.
/// </summary>
public class DdlFieldSerializationTests
{
    private const string Awkward = "say \"hi\" to C:\\temp";

    private static Paragraph ReadBack(Document document)
    {
        var ddl = DdlWriter.WriteToString(document);
        var reread = DdlReader.DocumentFromString(ddl);
        return reread.Sections[0].Elements.OfType<Paragraph>().Single();
    }

    private static Document WithParagraph(out Paragraph paragraph)
    {
        var document = new Document();
        paragraph = document.AddSection().AddParagraph();
        return document;
    }

    [Test]
    public void APageReferenceNameWithAQuoteAndABackslashReadsBack()
    {
        var document = WithParagraph(out var paragraph);
        paragraph.AddPageRefField(Awkward);

        var field = ReadBack(document).Elements.OfType<PageRefField>().Single();

        field.Name.Should().Be(Awkward);
    }

    [Test]
    public void ABookmarkNameWithAQuoteAndABackslashReadsBack()
    {
        var document = WithParagraph(out var paragraph);
        paragraph.AddBookmark(Awkward);

        var field = ReadBack(document).Elements.OfType<BookmarkField>().Single();

        field.Name.Should().Be(Awkward);
    }

    [Test]
    public void ADateFormatWithAQuoteAndABackslashReadsBack()
    {
        var document = WithParagraph(out var paragraph);
        paragraph.AddDateField(Awkward);

        var field = ReadBack(document).Elements.OfType<DateField>().Single();

        field.Format.Should().Be(Awkward);
    }

    [Test]
    public void APageReferenceKeepsBothItsNameAndItsFormat()
    {
        var document = WithParagraph(out var paragraph);
        paragraph.AddPageRefField("chapter \"one\"").Format = "ROMAN";

        var field = ReadBack(document).Elements.OfType<PageRefField>().Single();

        field.Name.Should().Be("chapter \"one\"");
        field.Format.Should().Be("ROMAN");
    }

    [Test]
    public void AFieldIsWrittenAsItWasBeforeItsSerializersWereShared()
    {
        // Byte for byte what the separate serializers wrote, for every value that needs no
        // escaping - including the empty brackets after a field with nothing in them, which stop a
        // '[' in the text that follows from being read as the field's attributes.
        var document = WithParagraph(out var paragraph);
        paragraph.AddPageField();
        paragraph.AddPageField().Format = "ROMAN";
        paragraph.AddNumPagesField();
        paragraph.AddSectionField();
        paragraph.AddSectionPagesField().Format = "alphabetic";
        paragraph.AddDateField();
        paragraph.AddDateField("dd.MM.yyyy");
        paragraph.AddInfoField(InfoFieldType.Author);
        paragraph.AddBookmark("intro");
        paragraph.AddPageRefField("intro");
        paragraph.AddPageRefField("intro").Format = "roman";

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().ContainAll(
            "\\field(Page)[]",
            "\\field(Page)[Format = \"ROMAN\"]",
            "\\field(NumPages)[]",
            "\\field(Section)[]",
            "\\field(SectionPages)[Format = \"alphabetic\"]",
            "\\field(Date)[]",
            "\\field(Date)[Format = \"dd.MM.yyyy\"]",
            "\\field(Info)[Name = \"Author\"]",
            "\\field(Bookmark)[Name = \"intro\"]",
            "\\field(PageRef)[Name = \"intro\"]",
            "\\field(PageRef)[Name = \"intro\" Format = \"roman\"]");
    }
}
