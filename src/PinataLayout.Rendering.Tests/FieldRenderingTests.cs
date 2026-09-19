using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Fields are written into the document as placeholders and are only worth anything once the
///   renderer has replaced them with what they stand for, in the format each of them names.
/// </summary>
/// <remarks>
///   Promoted from MigraDoc 1.32's TestParagraphRenderer.Fields, which put one of every field on a
///   page and saved it. A field that renders as nothing at all, or as its own name, looks like a
///   blank on that page and like a passing run to anything that only asks whether it threw - so
///   each assertion here says what the field is expected to read.
/// </remarks>
public class FieldRenderingTests
{
    [Fact]
    public void APageFieldAskedForRomanNumeralsRendersTheNumberAsOne()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("Page: ");
        paragraph.AddPageField().Format = "ROMAN";
        paragraph.AddLineBreak();
        paragraph.AddText("NumPages: ");
        paragraph.AddNumPagesField();

        // A single page, so the page number is one and the count of them is one - which in roman
        // numerals and in arabic respectively is "I" and "1". Both come from the same field
        // machinery and only the format tells them apart.
        Glyphs.On(Rendered.FirstPageOf(document))
            .Should().Equal(Glyphs.For("Page: I", "NumPages: 1"));
    }

    [Fact]
    public void ASectionFieldAskedForLettersRendersTheSectionAsA()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("Section: ");
        paragraph.AddSectionField().Format = "ALPHABETIC";

        Glyphs.On(Rendered.FirstPageOf(document))
            .Should().Equal(Glyphs.For("Section: A"));
    }

    [Fact]
    public void ABookmarkAndTheReferenceToItRenderThePageItIsOn()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("Bookmark: ");
        paragraph.AddBookmark("Egal");
        paragraph.AddLineBreak();
        paragraph.AddText("PageRef: ");
        paragraph.AddPageRefField("Egal");

        // The bookmark itself draws nothing - it marks a place - and the reference to it resolves
        // to the page that place is on, which here is the first. A reference that never resolves
        // renders as a blank and takes the whole line's meaning with it.
        Glyphs.On(Rendered.FirstPageOf(document))
            .Should().Equal(Glyphs.For("Bookmark: ", "PageRef: 1"));
    }

    /// <summary>
    ///   The one case a field cannot answer that survives all the way onto the page. While the
    ///   document is being formatted an unresolved reference is only unresolved <em>yet</em> - the
    ///   bookmark may be placed further down - so the line is measured against a two-digit guess
    ///   and measured again once the answer is in. By rendering time there is nothing left to wait
    ///   for, and the reader is told which name the document does not have rather than shown a gap.
    /// </summary>
    /// <remarks>
    ///   <see cref="FieldEvaluator"/> answers null for all three of these and never a placeholder,
    ///   so which of them a reader sees is decided here, in the renderer, and only a rendered page
    ///   can show that it was decided right.
    /// </remarks>
    [Fact]
    public void AReferenceToABookmarkThatDoesNotExistSaysSoOnThePage()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("PageRef: ");
        paragraph.AddPageRefField("NoSuchPlace");

        var runs = Glyphs.RunsOn(Rendered.FirstPageOf(document));

        // Two runs, because a field's value is worked out while the page is drawn and so never
        // passes the flattener that cuts a paragraph's text into words and turns its blanks into
        // positioning. The label is a word; the message is drawn whole, spaces and all.
        runs.Should().HaveCount(2);
        runs[0].Should().Equal(Glyphs.For("PageRef:"));

        // "Bookmark" is eight glyphs, so the ninth is the space - the one thing in the message that
        // has no counterpart in text the flattener has been over.
        var space = runs[1][8];
        runs[1].Where(glyph => glyph != space).Should()
            .Equal(Glyphs.For("Bookmark 'NoSuchPlace' is not defined within the document."));
    }

    /// <summary>
    ///   An InfoField in a heading used to draw on the page and then vanish from the outline
    ///   entry, because the predicate deciding which leaves contribute to a title asked for
    ///   <c>DocumentInfo</c> - the document's own info object, never a paragraph's leaf - and so
    ///   never recognised the field type that is one.
    /// </summary>
    [Fact]
    public void AHeadingBuiltFromDocumentInformationCarriesThatTextIntoTheOutline()
    {
        var document = new Document();
        document.Info.Title = "Annual Report";
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Style = StyleNames.Heading1;
        paragraph.Format.OutlineLevel = OutlineLevel.Level1;
        paragraph.AddText("Part One: ");
        paragraph.AddInfoField(InfoFieldType.Title);

        Rendered.Of(document).Outlines.Select(outline => outline.Title)
            .Should().Equal("Part One: Annual Report");
    }
}
