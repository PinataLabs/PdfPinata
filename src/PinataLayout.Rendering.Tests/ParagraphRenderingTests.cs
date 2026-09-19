using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   What a paragraph draws: the runs it is built from, the sizes and decorations they carry, and
///   where the alignment, the indents and the tab stops put them.
/// </summary>
/// <remarks>
///   Promoted from PinataLayout 1.32's TestParagraphRenderer, which built one document per feature and
///   saved each to a file. The arrangements are kept and the looking is replaced by assertions.
///
///   Two of them are no longer quite what was there. The alignment harness had every arrangement
///   but one commented out, and its tab harness had the tab itself commented out, so it laid out a
///   paragraph with a tab stop and no tab in it. Both are restored here, because a tab stop that
///   nothing tabs to is the one arrangement that cannot fail.
/// </remarks>
public class ParagraphRenderingTests
{
    [Fact]
    public void ABlankCharacterSetsTheSameTextAtTheSameSpacingAsASpaceInTheRunDoes()
    {
        // The harness this comes from built its text out of AddCharacter(SymbolName.Blank) rather
        // than out of spaces, which is a separate path through the renderer: a blank is an element
        // of the paragraph where a space is part of a run.
        var withBlanks = Rendered.FirstPageOf(Numbered(SeparatedBy.ABlankElement));
        var withSpaces = Rendered.FirstPageOf(Numbered(SeparatedBy.ASpaceInTheText));

        Glyphs.On(withBlanks).Should().Equal(Glyphs.On(withSpaces),
            "a blank is a space and draws no glyph of its own");
        GapsBetweenRunsOn(withBlanks).Should().Equal(GapsBetweenRunsOn(withSpaces),
            "a blank takes the same width as the space it stands for");
    }

    [Fact]
    public void ABlankAtTheHeadOfALineIsKeptWhereALeadingSpaceIsTrimmed()
    {
        // The one place the two differ, and it is a constant offset rather than a drift: the line
        // begins with a separator either way, and only the one written as text is dropped. Pinned
        // rather than asserted away, because a change to either path would move exactly this.
        var withBlanks = TextBaselines.PositionsOf(Rendered.FirstPageOf(Numbered(SeparatedBy.ABlankElement)));
        var withSpaces = TextBaselines.PositionsOf(Rendered.FirstPageOf(Numbered(SeparatedBy.ASpaceInTheText)));

        var offsets = withBlanks.Zip(withSpaces, (blank, space) => Math.Round(blank.X - space.X, 4))
            .Distinct()
            .ToList();

        offsets.Should().ContainSingle("every run is moved across by the same untrimmed blank");
        offsets.Single().Should().BePositive();
    }

    [Fact]
    public void EveryFontSizeAParagraphNamesReachesThePage()
    {
        var page = Rendered.FirstPageOf(Formatted());

        // A run whose size never reaches a Tf is drawn at whatever size was last set, which is a
        // failure that looks like a layout wobble rather than like a lost setting. The default of
        // ten is on the page as well, from the run that closes the paragraph.
        FontSizesOn(page).Should().Contain(new[] { 6.0, 8.0, 14.0, 16.0, 20.0 });
    }

    [Fact]
    public void AStruckRunIsRuledThroughAndTheTextBesideItIsNot()
    {
        var page = Rendered.FirstPageOf(StruckThenPlain());

        // Strikethrough is drawn as a rule rather than as part of the glyphs, so nothing stops it
        // being drawn the width of the whole line. The run after the struck one starts where the
        // rule has to have stopped.
        var runs = TextBaselines.PositionsOf(page).Select(position => position.X).ToList();
        var rules = StrokedLines.Of(page).Where(line => line.IsHorizontal).ToList();

        rules.Should().ContainSingle();
        Math.Min(rules[0].X1, rules[0].X2).Should().BeApproximately(runs[0], 0.1);
        Math.Max(rules[0].X1, rules[0].X2).Should().BeLessThan(runs[1]);
    }

    [Fact]
    public void EachAlignmentInTurnStartsALineFurtherAcrossThePage()
    {
        // Asserted as an order rather than as three positions, so that the test says what
        // alignment means and not what Liberation Sans measures one particular line at. The same
        // line is set three times, so only the alignment can move it.
        var starts = new[] { ParagraphAlignment.Left, ParagraphAlignment.Center, ParagraphAlignment.Right }
            .Select(FirstRunOf)
            .ToList();

        starts.Should().BeInAscendingOrder();
        starts.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ANegativeFirstLineIndentHangsTheFirstLineLeftOfTheRest()
    {
        var page = Rendered.FirstPageOf(Hanging());

        // Two centimetres of left indent with two taken off the first line puts the first line
        // back at the margin and every other line in from it. A first line indent applied to all
        // of them, or to none, reads as one column either way.
        var lines = FirstRunOfEachLine(page);

        lines.Should().HaveCountGreaterThan(2, "the paragraph has to wrap for there to be a rest");
        (lines[1] - lines[0]).Should().BeApproximately(Unit.FromCentimeter(2).Point, 0.1);
        lines.Skip(1).Should().AllSatisfy(start => start.Should().BeApproximately(lines[1], 0.1));
    }

    [Fact]
    public void ARightAlignedTabStopEndsTheTextThatFollowsItAtTheStop()
    {
        // Where the run ends cannot be read off the content - that would mean measuring the
        // glyphs. So the stop is moved instead: the same run set against two different stops has
        // to sit the same distance short of each of them, which is only true if it is its right
        // edge that the stop holds.
        var far = Tabbed(Unit.FromCentimeter(20));
        var near = Tabbed(Unit.FromCentimeter(10));

        far.Runs.Should().HaveCount(2);
        far.Runs[0].Should().BeApproximately(0, 0.1, "the left margin is nil in this arrangement");
        far.Runs[1].Should().BeLessThan(far.Stop, "a right aligned stop holds the end of the run");

        (far.Stop - far.Runs[1]).Should().BeApproximately(near.Stop - near.Runs[1], 0.01);
    }

    [Fact]
    public void AParagraphsBordersStandOffItsTextByTheDistancesItNames()
    {
        var page = Rendered.FirstPageOf(Bordered());
        var lines = StrokedLines.Of(page);

        // Every side carries its own width and its own distance from the text, and the four are
        // read from four different places. A border drawn at the text's own edge, or all four
        // drawn at one distance, is what these numbers rule out.
        var textStart = TextBaselines.PositionsOf(page).Single().X;
        var left = lines.Single(line => line.IsVertical && line.X1 < textStart);
        var right = lines.Single(line => line.IsVertical && line.X1 > textStart);

        // The rule runs down the middle of the border, so it stands half a width beyond the
        // distance the format names.
        (textStart - left.X1).Should().BeApproximately(Unit.FromCentimeter(0.5).Point + left.Width / 2, 0.1);
        (right.X1 - textStart).Should().BeGreaterThan(Unit.FromCentimeter(2).Point);

        lines.Select(line => Math.Round(line.Width, 2)).Distinct()
            .Should().BeEquivalentTo(new[] { 3.0, 4.0, 7.0 },
                "the top is four, the left is seven, and the bottom and the right are both three");
    }

    enum SeparatedBy
    {
        ABlankElement,
        ASpaceInTheText,
    }

    /// <summary>
    ///   Eleven numbers with a gap before each of them, put there in one of the two ways a
    ///   paragraph can be given one.
    /// </summary>
    static Document Numbered(SeparatedBy separator)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();

        for (var idx = 0; idx <= 10; ++idx)
        {
            if (separator == SeparatedBy.ABlankElement)
            {
                paragraph.AddCharacter(SymbolName.Blank);
                paragraph.AddText(idx.ToString());
            }
            else
            {
                paragraph.AddText(" " + idx);
            }
        }

        return document;
    }

    /// <summary>The formatted paragraph of the original harness, at the five sizes it named.</summary>
    static Document Formatted()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();

        Sized(paragraph, 16, Strikethrough.None, TextFormat.Bold);
        Sized(paragraph, 6, Strikethrough.None, TextFormat.Italic);
        Sized(paragraph, 8, Strikethrough.DotDash);
        Sized(paragraph, 14, Strikethrough.DotDotDash);
        Sized(paragraph, 20, Strikethrough.Dotted);

        paragraph.AddText(" ...ready.");
        return document;
    }

    static void Sized(Paragraph paragraph, double size, Strikethrough struck, TextFormat format = TextFormat.NotBold)
    {
        var text = paragraph.AddFormattedText(size.ToString(), format);
        text.Font.Size = size;
        text.Font.Strikethrough = struck;
        text.AddText(" ");
    }

    static Document StruckThenPlain()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();

        var struck = paragraph.AddFormattedText("struck");
        struck.Font.Strikethrough = Strikethrough.Single;
        paragraph.AddText(" plain and unstruck text here");
        return document;
    }

    static Document Hanging()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();

        for (var idx = 0; idx < 60; idx++)
        {
            paragraph.AddText("word" + idx);
            paragraph.AddText(" ");
        }

        paragraph.Format.LeftIndent = "2cm";
        paragraph.Format.FirstLineIndent = "-2cm";
        return document;
    }

    /// <summary>
    ///   A line with a tab in it, set against a right aligned stop at the position given, and
    ///   where each of its two runs started.
    /// </summary>
    static (double Stop, IReadOnlyList<double> Runs) Tabbed(Unit stop)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = 0;
        section.PageSetup.RightMargin = 0;

        var paragraph = section.AddParagraph();
        paragraph.Format.TabStops.AddTabStop(stop, TabAlignment.Right);
        paragraph.AddText("before");
        paragraph.AddTab();
        paragraph.AddText("after");

        var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(document))
            .Select(position => position.X)
            .ToList();

        return (stop.Point, runs);
    }

    /// <summary>The bordered paragraph of the original harness, distances and all.</summary>
    static Document Bordered()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("bordered");
        var borders = paragraph.Format.Borders;

        borders.Top.Color = Colors.Gray;
        borders.Top.Width = 4;
        borders.Top.Style = BorderStyle.DashDot;
        borders.Left.Color = Colors.Red;
        borders.Left.Style = BorderStyle.Dot;
        borders.Left.Width = 7;
        borders.Bottom.Color = Colors.Red;
        borders.Bottom.Width = 3;
        borders.Bottom.Style = BorderStyle.DashLargeGap;
        borders.Right.Style = BorderStyle.DashSmallGap;
        borders.Right.Width = 3;

        borders.DistanceFromTop = "1.5cm";
        borders.DistanceFromBottom = "1cm";
        borders.DistanceFromLeft = "0.5cm";
        borders.DistanceFromRight = "2cm";

        paragraph.Format.Shading.Color = Colors.LightBlue;
        return document;
    }

    static double FirstRunOf(ParagraphAlignment alignment)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("a short line");
        paragraph.Format.Alignment = alignment;

        return TextBaselines.PositionsOf(Rendered.FirstPageOf(document))[0].X;
    }

    /// <summary>Where each line of the page starts, from the top downwards.</summary>
    static IReadOnlyList<double> FirstRunOfEachLine(PdfPage page)
    {
        return TextBaselines.PositionsOf(page)
            .GroupBy(position => Math.Round(position.Y, 2))
            .OrderByDescending(line => line.Key)
            .Select(line => line.First().X)
            .ToList();
    }

    /// <summary>
    ///   The distance from each run on the page to the one after it, which is what the separators
    ///   between them are worth.
    /// </summary>
    static IReadOnlyList<double> GapsBetweenRunsOn(PdfPage page)
    {
        var runs = TextBaselines.PositionsOf(page).Select(position => position.X).ToList();

        return runs.Zip(runs.Skip(1), (one, next) => Math.Round(next - one, 4)).ToList();
    }

    /// <summary>The sizes the page sets its font to, which Tf carries as its second operand.</summary>
    static IReadOnlyList<double> FontSizesOn(PdfPage page)
    {
        return TextOperators.OperandsGivenTo(page, OpCodeName.Tf)
            .Where(operands => operands.Length == 2)
            .Select(operands => operands[1])
            .Distinct()
            .ToList();
    }
}
