using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   The DOM writes a value to DDL only when that value is set, so whether a value is null decides
///   what a document serializes to. Nothing else in the suite reads or writes DDL, which leaves the
///   nullable value types free to change what they emit without a test noticing.
///
///   These tests pin the emitted DDL - which attributes appear, which are left out, and that a
///   document survives a write, a read and a second write unchanged.
/// </summary>
public class DdlRoundTripTests
{
    /// <summary>
    ///   A document touching all four nullable value types, each with one value set and a
    ///   neighbouring value of the same type deliberately left alone.
    /// </summary>
    private static Document ADocumentWithSomeValuesSetAndSomeLeftUnset()
    {
        var document = new Document();

        document.Info.Title = "A title";        // NString set, Info.Author left unset
        document.FootnoteStartingNumber = 7;    // NInt set

        var section = document.AddSection();
        section.PageSetup.StartingNumber = 3;   // NInt set
        section.PageSetup.MirrorMargins = true; // NBool set, OddAndEvenPagesHeaderFooter unset

        var paragraph = section.AddParagraph("Hello");
        paragraph.Format.Font.Bold = true;      // NBool set, Italic left unset
        paragraph.Format.Font.Name = "Arial";   // NString set

        return document;
    }

    private static string Ddl(Document document) => DdlWriter.WriteToString(document);

    [Fact]
    public void ADocumentSurvivesAWriteAndAReadUnchanged()
    {
        var written = Ddl(ADocumentWithSomeValuesSetAndSomeLeftUnset());

        var rewritten = Ddl(DdlReader.DocumentFromString(written));

        rewritten.Should().Be(written);
    }

    [Fact]
    public void ValuesThatWereSetAreWritten()
    {
        var ddl = Ddl(ADocumentWithSomeValuesSetAndSomeLeftUnset());

        ddl.Should().Contain("Title = \"A title\"");
        ddl.Should().Contain("FootnoteStartingNumber = 7");
        ddl.Should().Contain("StartingNumber = 3");
        ddl.Should().Contain("MirrorMargins = true");
        ddl.Should().Contain("Bold = true");
        ddl.Should().Contain("Name = \"Arial\"");
    }

    [Fact]
    public void ValuesThatWereNeverSetAreLeftOut()
    {
        var ddl = Ddl(ADocumentWithSomeValuesSetAndSomeLeftUnset());

        ddl.Should().NotContain("Author");
        ddl.Should().NotContain("Italic");
        ddl.Should().NotContain("OddAndEvenPagesHeaderFooter");
    }

    [Fact]
    public void AValueSetToItsDefaultIsStillWritten()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Font.Bold = false;
        document.FootnoteStartingNumber = 0;

        var ddl = Ddl(document);

        ddl.Should().Contain("Bold = false", "an explicit false is not the same as unset");
        ddl.Should().Contain("FootnoteStartingNumber = 0", "an explicit zero is not the same as unset");
    }

    /// <summary>
    ///   DocumentInfo used to ask whether the string was empty rather than whether it was null, so
    ///   an explicitly assigned "" was written the same way an unassigned one was - not at all -
    ///   and a round trip lost the difference. It now asks about nullness like the rest of the DOM.
    /// </summary>
    [Fact]
    public void AnExplicitlyEmptyStringIsWrittenBecauseItWasSet()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello");

        document.Info.Title = "";

        document.Info.IsNull("Title").Should().BeFalse("the value was assigned");
        Ddl(document).Should().Contain("Title = \"\"");
    }

    [Fact]
    public void AnUnassignedInfoStringIsStillNotWritten()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello");

        document.Info.Title = "A title";

        var ddl = Ddl(document);
        ddl.Should().Contain("Title");
        ddl.Should().NotContain("Subject").And.NotContain("Author").And.NotContain("Keywords");
    }

    [Fact]
    public void AnExplicitlyEmptyInfoStringSurvivesTheRoundTrip()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello");
        document.Info.Title = "";

        var written = Ddl(document);
        var reread = DdlReader.DocumentFromString(written);

        reread.Info.IsNull("Title").Should().BeFalse("an assigned empty string is not unset");
        reread.Info.Title.Should().BeEmpty();
        Ddl(reread).Should().Be(written);
    }

    [Fact]
    public void AValueSetToItsDefaultSurvivesTheRoundTripAsSetRatherThanUnset()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello").Format.Font.Bold = false;

        var reread = DdlReader.DocumentFromString(Ddl(document));

        var font = ((Paragraph)reread.LastSection.Elements[0]).Format.Font;
        font.IsNull("Bold").Should().BeFalse();
        font.Bold.Should().BeFalse();
    }

    [Fact]
    public void AnEmptyDocumentSurvivesTheRoundTrip()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello");

        var written = Ddl(document);

        Ddl(DdlReader.DocumentFromString(written)).Should().Be(written);
    }

    [Fact]
    public void ValuesReadBackFromDdlMatchWhatWasWritten()
    {
        var reread = DdlReader.DocumentFromString(Ddl(ADocumentWithSomeValuesSetAndSomeLeftUnset()));

        reread.Info.Title.Should().Be("A title");
        reread.FootnoteStartingNumber.Should().Be(7);
        reread.LastSection.PageSetup.StartingNumber.Should().Be(3);
        reread.LastSection.PageSetup.MirrorMargins.Should().BeTrue();

        var font = ((Paragraph)reread.LastSection.Elements[0]).Format.Font;
        font.Bold.Should().BeTrue();
        font.Name.Should().Be("Arial");
        font.IsNull("Italic").Should().BeTrue("a value never written stays unset when read back");
    }
}
