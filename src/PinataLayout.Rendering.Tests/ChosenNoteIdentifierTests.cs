using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A note's identifier, when the caller wants to choose it rather than take the generated one.
/// </summary>
/// <remarks>
///   <para>
///     ISO 14289-1 7.9 requires every <c>/Note</c> to carry an <c>/ID</c>, and the renderer has
///     always written one: <c>note1</c>, <c>note2</c>, in the order the notes are cited. That is
///     what nearly every document wants and it stays the default.
///   </para>
///   <para>
///     It is the wrong answer when the identifier has to mean something outside the document,
///     because something else refers to it — and until now there was no way to say so, since nothing
///     exposed the structure element to the caller. <see cref="Footnote.Identifier"/> is where that
///     is said, beside <c>Table.Summary</c> and <c>Shape.AlternativeText</c>, which are on the DOM
///     for the same reason: they are things only the author knows.
///   </para>
/// </remarks>
public class ChosenNoteIdentifierTests
{
    [Fact]
    public void AnUnsetIdentifierIsStillGenerated()
    {
        // The default, and the behaviour every existing document depends on.
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        Structure.Of(document).Single("Note").Id.Should().Be("note1");
    }

    [Fact]
    public void AChosenIdentifierIsWhatIsWritten()
    {
        var document = Document(out var section);
        var footnote = section.AddParagraph("A claim").AddFootnote("The support.");
        footnote.Identifier = "clause-4-note";

        Structure.Of(document).Single("Note").Id.Should().Be("clause-4-note");
    }

    [Fact]
    public void ChoosingOneDoesNotRenumberTheNotesAroundIt()
    {
        // The counter advances for every note whether or not its name came from the counter, so the
        // notes that did take a generated name keep the number their citation order gives them. A
        // counter that only advanced on generated names would make note 3 answer to "note2".
        var document = Document(out var section);
        var paragraph = section.AddParagraph("Three claims");
        paragraph.AddFootnote("The first.");
        paragraph.AddFootnote("The second.").Identifier = "chosen";
        paragraph.AddFootnote("The third.");

        Structure.Of(document).OfTag("Note").Select(note => note.Id)
            .Should().Equal(new[] { "note1", "chosen", "note3" });
    }

    [Fact]
    public void TwoNotesUnderOneChosenNameAreRefused()
    {
        // The identifier tree refuses it, and has to: an identifier is what something else points
        // at, so a name that reaches two elements reaches neither.
        var document = Document(out var section);
        var paragraph = section.AddParagraph("Two claims");
        paragraph.AddFootnote("The first.").Identifier = "same";
        paragraph.AddFootnote("The second.").Identifier = "same";

        Action rendering = () => Structure.Of(document);

        rendering.Should().Throw<InvalidOperationException>().WithMessage("*same*");
    }

    [Fact]
    public void CollidingWithAGeneratedNameIsRefusedToo()
    {
        // The trap the prefix exists to make visible rather than to prevent: "note2" is exactly the
        // name a caller reaches for, and the second note is about to be given it.
        var document = Document(out var section);
        var paragraph = section.AddParagraph("Two claims");
        paragraph.AddFootnote("The first.").Identifier = "note2";
        paragraph.AddFootnote("The second.");

        Action rendering = () => Structure.Of(document);

        rendering.Should().Throw<InvalidOperationException>().WithMessage("*note2*");
    }

    [Fact]
    public void TheIdentifierSurvivesAWriteAndReadOfTheDocumentModel()
    {
        // It is a DOM property, so it belongs in MDDDL like every other one - a document written out
        // and read back has to be the same document.
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.").Identifier = "clause-4-note";

        var writer = new StringWriter();
        new DdlWriter(writer).WriteDocument(document);
        var reread = DdlReader.DocumentFromString(writer.ToString());

        var paragraph = (Paragraph)reread.LastSection.Elements[0];
        var footnote = paragraph.Elements.OfType<Footnote>().Single();

        footnote.Identifier.Should().Be("clause-4-note");
    }

    [Fact]
    public void AnUnsetIdentifierIsNotWrittenToTheDocumentModel()
    {
        // Nothing gained by writing the empty string into every footnote in every MDDDL file.
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var writer = new StringWriter();
        new DdlWriter(writer).WriteDocument(document);

        writer.ToString().Should().NotContain("Identifier");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    private static Document Document(out Section section)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Sans";
        normal.Font.Size = 11;

        document.Styles[StyleNames.Footnote].Font.Size = 8;

        section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        return document;
    }
}
