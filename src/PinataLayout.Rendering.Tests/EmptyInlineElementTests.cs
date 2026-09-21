using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   An inline element that holds nothing: <c>AddFormattedText("")</c> and a hyperlink with no
///   text in it.
/// </summary>
/// <remarks>
///   <para>
///     Both are accepted while the document is built and neither draws anything, which is the whole
///     of what they should do. What made them worth a file of their own is how they reach the
///     renderer: <c>ParagraphIterator</c> hands back the element's own empty collection as a leaf,
///     one level above the word a leaf usually is, because there is nothing inside it to descend to.
///   </para>
///   <para>
///     Everything the renderer does with a leaf tolerates that - <c>FormatElement</c> and
///     <c>RenderElement</c> both fall through to a default that draws nothing - except the walk
///     that asks which hyperlink the leaf sits in, which stepped two levels at a time and so
///     stepped straight over the paragraph and off the top of the document.
///   </para>
/// </remarks>
public class EmptyInlineElementTests
{
    [Fact]
    public void AnEmptyFormattedTextLeavesTheRestOfTheParagraphToDraw()
    {
        var withEmpty = Paragraph(paragraph =>
        {
            paragraph.AddFormattedText("");
            paragraph.AddText("content");
        });

        var withoutIt = Paragraph(paragraph => paragraph.AddText("content"));

        Glyphs.On(Rendered.FirstPageOf(withEmpty))
            .Should().Equal(Glyphs.On(Rendered.FirstPageOf(withoutIt)),
                "an element holding no text contributes nothing to the page");
    }

    [Fact]
    public void AnEmptyFormattedTextOnItsOwnIsAParagraphWithNoWordsInIt()
    {
        // Nothing follows it, so the empty collection is the paragraph's only leaf and is both the
        // first and the last of the line.
        var document = Paragraph(paragraph => paragraph.AddFormattedText(""));

        Glyphs.On(Rendered.FirstPageOf(document)).Should().BeEmpty();
    }

    [Fact]
    public void AnEmptyHyperlinkIsTheSameKindOfLeafAndAlsoDrawsNothing()
    {
        var document = Paragraph(paragraph =>
        {
            paragraph.AddHyperlink("https://example.org/terms", HyperlinkType.Web);
            paragraph.AddText("content");
        });

        var withoutIt = Paragraph(paragraph => paragraph.AddText("content"));

        Glyphs.On(Rendered.FirstPageOf(document))
            .Should().Equal(Glyphs.On(Rendered.FirstPageOf(withoutIt)));
    }

    [Fact]
    public void AnEmptyFormattedTextInsideAHyperlinkStillLeavesTheWordsInTheLink()
    {
        // The other half of the same walk: it exists to find the hyperlink a word sits in, and a
        // leaf that starts a level higher must not lose it.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("See ");
        var hyperlink = paragraph.AddHyperlink("https://example.org/terms", HyperlinkType.Web);
        hyperlink.AddFormattedText("");
        hyperlink.AddText("the terms");
        paragraph.AddText(".");

        var link = Structure.Of(document).Single("Link");

        link.MarkCount.Should().BeGreaterThan(0);
        link.AnnotationCount.Should().Be(1);
    }

    static Document Paragraph(Action<Paragraph> build)
    {
        var document = new Document();
        build(document.AddSection().AddParagraph());
        return document;
    }
}
