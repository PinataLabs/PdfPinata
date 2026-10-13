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
///   Where the text after a tab goes when the tab cannot be set on the line it falls on (#406).
/// </summary>
/// <remarks>
///   <para>
///   A tab has nowhere to go on a line when no stop is left before the margin, and a left
///   aligned one has nowhere useful to go when the word after it does not fit beside its stop.
///   Either way the line breaks at the tab - and the tab used to be formatted a second time at
///   the head of the next line, where it found the first stop free and indented the text after
///   it to there, a step in from every other line of the paragraph. A word processor does not do
///   that: the tab ends the line it is on, the way a blank does, and the next line begins at the
///   indent.
///   </para>
///   <para>
///   A right, center or decimal aligned tab is a different thing, because the stop holds the text
///   after it in place rather than marking where it begins. The page number after a heading in a
///   table of contents still belongs at the stop when it is pushed onto a line of its own, so
///   such a tab is still carried over to the next line.
///   </para>
///   <para>
///   The arrangement is a measure of 140pt - a 180pt page with 20pt margins - with a default stop
///   every 36pt, so the stops on a line are at 36, 72 and 108. Set in Liberation Sans at 10pt,
///   "First long segment" is about 82pt wide and "First long segment ends here" about 130pt.
///   </para>
/// </remarks>
public class TabAtALineBreakTests
{
    private const double Margin = 20;
    private const double Measure = 140;

    /// <summary>
    ///   The stop after "First long segment" is the one at 108, and "Second" does not fit
    ///   between it and the margin.
    /// </summary>
    [Test]
    public void ALeftAlignedTabWhoseWordDoesNotFitBesideItsStopDoesNotIndentTheNextLine()
    {
        var lines = LinesOf(Page(paragraph =>
        {
            paragraph.AddText("First long segment");
            paragraph.AddTab();
            paragraph.AddText("Second");
        }));

        lines.Should().HaveCount(2, "the tab cannot be set on the first line");
        lines[1].Should().BeApproximately(Margin, 0.01);
    }

    /// <summary>
    ///   Past "First long segment ends here" the next stop would be at 144, beyond the margin.
    /// </summary>
    [Test]
    public void ATabWithNoStopLeftBeforeTheMarginDoesNotIndentTheNextLine()
    {
        var lines = LinesOf(Page(paragraph =>
        {
            paragraph.AddText("First long segment ends here");
            paragraph.AddTab();
            paragraph.AddText("Second");
        }));

        lines.Should().HaveCount(2, "the tab cannot be set on the first line");
        lines[1].Should().BeApproximately(Margin, 0.01);
    }

    /// <summary>
    ///   Three of the tabs take the stops at 36, 72 and 108, and the fourth has none. That it
    ///   follows other tabs rather than text changes nothing about where the line after it
    ///   begins.
    /// </summary>
    [Test]
    public void ATabWithNoStopLeftDoesNotIndentTheNextLineAfterOtherTabsEither()
    {
        var lines = LinesOf(Page(paragraph =>
        {
            paragraph.AddText("A");
            paragraph.AddTab();
            paragraph.AddTab();
            paragraph.AddTab();
            paragraph.AddTab();
            paragraph.AddText("B");
        }));

        lines.Should().HaveCount(2, "the fourth tab cannot be set on the first line");
        lines[1].Should().BeApproximately(Margin, 0.01);
    }

    /// <summary>
    ///   A justified line with a tab in it is measured a second time when it is drawn, up to and
    ///   including its last leaf - which is now the tab that ended it.
    /// </summary>
    [Test]
    public void AJustifiedParagraphEndsTheLineAtTheTabToo()
    {
        var lines = LinesOf(Page(paragraph =>
        {
            paragraph.Format.Alignment = ParagraphAlignment.Justify;
            paragraph.AddText("First long segment");
            paragraph.AddTab();
            paragraph.AddText("Second and a third line");
        }));

        lines.Should().HaveCount(2);
        lines[1].Should().BeApproximately(Margin, 0.01);
    }

    /// <summary>
    ///   One line to a page, so the line after the tab is the first of the next page - and a
    ///   paragraph carried onto a new page begins after the last leaf of the line before it,
    ///   which has to be the tab and not the word before it.
    /// </summary>
    [Test]
    public void ATabEndingTheLastLineOnAPageDoesNotIndentTheFirstLineOfTheNext()
    {
        var pages = Rendered.Of(Document(paragraph =>
        {
            paragraph.AddText("First long segment");
            paragraph.AddTab();
            paragraph.AddText("Second");
        }, onePerPage: true)).Pages;

        pages.Count.Should().Be(2, "the arrangement fits one line to a page");
        LinesOf(pages[1]).Should().Equal(Margin);
    }

    /// <summary>
    ///   A right aligned stop at the margin with "12" after it: the number does not fit after
    ///   "First long segment ends here", so it goes onto the next line - still ending at the
    ///   stop, as a page number in a table of contents does.
    /// </summary>
    [Test]
    public void ARightAlignedTabIsStillSetOnTheNextLineWhenTheTextAfterItDoesNotFit()
    {
        var page = Page(paragraph =>
        {
            paragraph.Format.TabStops.AddTabStop(Unit.FromPoint(Measure), TabAlignment.Right);
            paragraph.AddText("First long segment ends here");
            paragraph.AddTab();
            paragraph.AddText("12");
        });

        var lines = LinesOf(page);
        lines.Should().HaveCount(2, "the number cannot be set on the first line");
        lines[1].Should().BeApproximately(Margin + Measure - WidthOf("12"), 0.01);
    }

    private static PdfPage Page(Action<Paragraph> fill) => Rendered.FirstPageOf(Document(fill));

    private static Document Document(Action<Paragraph> fill, bool onePerPage = false)
    {
        var document = new Document { DefaultTabStop = Unit.FromPoint(36) };
        var section = document.AddSection();
        section.PageSetup.PageWidth = Unit.FromPoint(Margin + Measure + Margin);
        section.PageSetup.LeftMargin = Unit.FromPoint(Margin);
        section.PageSetup.RightMargin = Unit.FromPoint(Margin);
        if (onePerPage)
        {
            section.PageSetup.PageHeight = Unit.FromPoint(40);
            section.PageSetup.TopMargin = Unit.FromPoint(10);
            section.PageSetup.BottomMargin = Unit.FromPoint(10);
        }

        fill(section.AddParagraph());
        return document;
    }

    /// <summary>Where each line of the page starts, from the top downwards.</summary>
    private static IReadOnlyList<double> LinesOf(PdfPage page)
    {
        return [..TextBaselines.PositionsOf(page)
            .GroupBy(position => Math.Round(position.Y, 2))
            .OrderByDescending(line => line.Key)
            .Select(line => line.Min(position => position.X))];
    }

    /// <summary>
    ///   How wide the text given is when a paragraph sets it, read as where a second run placed
    ///   straight after it begins.
    /// </summary>
    private static double WidthOf(string text)
    {
        var page = Page(paragraph =>
        {
            paragraph.AddText(text);
            paragraph.AddText("|");
        });

        return TextBaselines.PositionsOf(page)[^1].X - Margin;
    }
}
