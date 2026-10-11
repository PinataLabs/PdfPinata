using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   An underline or a strikethrough running over a blank set in another size than the words
///   either side of it.
/// </summary>
/// <remarks>
///   A rule is drawn a sixteenth of the font size thick, and is ended and started again wherever
///   that pen changes. A blank in a smaller size changed it, so "before" and "after" with an
///   eight-point space between them were underlined in three strokes, the middle one thinner -
///   a visible step under a gap the reader takes for nothing at all.
/// </remarks>
public class TextRuleAcrossABlankTests
{
    [Test]
    public void AnUnderlineRunsOverASmallerBlankAsOneRule()
    {
        var page = Rendered.FirstPageOf(WordsAroundASmallerBlank(font => font.Underline = Underline.Single));

        var rules = HorizontalRules(page);
        rules.Should().ContainSingle("the blank carries on the rule the word before it began");
        rules[0].Width.Should().BeApproximately(12.0 / 16, 0.001, "the rule is as thick as the words make it");
    }

    [Test]
    public void AStrikethroughRunsOverASmallerBlankAsOneRule()
    {
        var page = Rendered.FirstPageOf(WordsAroundASmallerBlank(font => font.Strikethrough = Strikethrough.Single));

        var rules = HorizontalRules(page);
        rules.Should().ContainSingle("the blank carries on the rule the word before it began");
        rules[0].Width.Should().BeApproximately(12.0 / 16, 0.001, "the rule is as thick as the words make it");
    }

    [Test]
    public void ADashedUnderlineRunsOverASmallerBlankAsOneRule()
    {
        // A dashed rule more than any: starting another one starts the dash pattern again, so the
        // seam would show as a dash cut short.
        var page = Rendered.FirstPageOf(WordsAroundASmallerBlank(font => font.Underline = Underline.Dash));

        var rules = HorizontalRules(page);
        rules.Should().ContainSingle("the blank carries on the rule the word before it began");
        rules[0].Width.Should().BeApproximately(12.0 / 16, 0.001, "the rule is as thick as the words make it");
    }

    [Test]
    public void ABlankTakesTheRuleOfTheWordBeforeItWhereTheWordAfterItIsSmaller()
    {
        // A twelve-point word, then an eight-point blank and word: the blank carries on the
        // twelve-point rule, and the rule changes where the eight-point word begins.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.Font.Size = 12;
        paragraph.Format.Font.Underline = Underline.Single;
        paragraph.AddText("before");
        paragraph.AddFormattedText(" after").Font.Size = 8;

        var page = Rendered.FirstPageOf(document);
        var rules = HorizontalRules(page);

        rules.Select(rule => Math.Round(rule.Width, 3)).Should().Equal(12.0 / 16, 8.0 / 16);
        var placed = Glyphs.PlacedOn(page);
        rules[1].X1.Should().BeApproximately(placed[^1].X, 0.01, "the thinner rule begins under the smaller word");
    }

    [Test]
    public void AWordInAnotherSizeStillHasARuleOfItsOwn()
    {
        // Only a blank gives way. A word in another size is underlined as thick as it is set.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.Font.Size = 12;
        paragraph.Format.Font.Underline = Underline.Single;
        paragraph.AddText("before ");
        paragraph.AddFormattedText("after").Font.Size = 8;

        var rules = HorizontalRules(Rendered.FirstPageOf(document));

        rules.Select(rule => Math.Round(rule.Width, 3)).Should().Equal(12.0 / 16, 8.0 / 16);
    }

    private static Document WordsAroundASmallerBlank(Action<Font> rule)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.Font.Size = 12;
        rule(paragraph.Format.Font);
        paragraph.AddText("before");
        paragraph.AddFormattedText(" ").Font.Size = 8;
        paragraph.AddText("after");
        return document;
    }

    private static List<StrokedLines.Line> HorizontalRules(PdfPage page) =>
        StrokedLines.Of(page).Where(line => line.IsHorizontal && Math.Abs(line.X1 - line.X2) > 0.001).ToList();
}
