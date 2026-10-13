using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Where a run of formatting ends and another begins with nothing between them that a line may
///   be broken at - "H", a subscript "2", "O"; "bold" and an emboldened "er".
/// </summary>
/// <remarks>
///   <para>
///     Each leaf is measured on its own and the line is broken before whichever one does not fit,
///     so every boundary between two leaves used to be a place a line could break, whether or not
///     there was a space there. Only a non-breakable blank joined two leaves. Now two leaves that
///     are part of the same word are joined unless the first ends where a line may be broken -
///     after a hyphen, a zero-width space or a dash - and a run of them goes down to the next line
///     whole when it does not fit.
///   </para>
///   <para>
///     Every paragraph here is sixty points wide, which holds "aaaa bbbb" but not "aaaa bbbbcccc",
///     so the question each test asks is whether "bbbb" stays on the first line or goes down with
///     "cccc".
///   </para>
/// </remarks>
public class FormattingSeamTests
{
    private static Paragraph AParagraphSixtyPointsWide(out Document document)
    {
        document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(10);
        section.PageSetup.RightMargin = Unit.FromPoint(10);
        section.PageSetup.PageWidth = Unit.FromPoint(80);
        return section.AddParagraph();
    }

    /// <summary>
    ///   "aaaa bbbb" and "cccc" with no space between, and what <paramref name="seam"/> names
    ///   between them, if anything.
    /// </summary>
    private static Document BbbbAndCcccMeetingAt(string seam)
    {
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa bbbb");
        switch (seam)
        {
            case "a run in another weight":
                paragraph.AddFormattedText("cccc").Font.Bold = true;
                return document;

            case "a subscript":
                paragraph.AddFormattedText("2").Subscript = true;
                break;

            case "a footnote mark":
                paragraph.AddFootnote("The note.");
                break;

            case "a page field":
                paragraph.AddPageField();
                break;

            case "a symbol":
                paragraph.AddCharacter(SymbolName.Trademark);
                break;

            case "a bookmark":
                paragraph.AddBookmark("inside");
                break;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(seam), seam, "No such seam.");
        }
        paragraph.AddText("cccc");
        return document;
    }

    /// <summary>
    ///   The same, where the seam is somewhere a line may be broken.
    /// </summary>
    private static Document BbbbAndCcccBreakableAt(string seam)
    {
        var paragraph = AParagraphSixtyPointsWide(out var document);
        switch (seam)
        {
            case "a space":
                paragraph.AddText("aaaa bbbb ");
                break;

            case "a hyphen":
                paragraph.AddText("aaaa bbbb-");
                break;

            case "a zero-width space":
                paragraph.AddText("aaaa bbbb" + Chars.ZeroWidthSpace);
                break;

            case "an en dash":
                paragraph.AddText("aaaa bbbb");
                paragraph.AddCharacter(SymbolName.EnDash);
                break;

            case "a hyphen typed as text":
                paragraph.AddText("aaaa bbbb" + (char)0x2010);
                break;

            case "an en dash typed as text":
                paragraph.AddText("aaaa bbbb" + (char)0x2013);
                break;

            case "an em dash typed as text":
                paragraph.AddText("aaaa bbbb" + (char)0x2014);
                break;

            case "an ideograph":
                // Chinese and Japanese are written without spaces, and a line may be broken
                // between any two ideographs. Joining at a seam beside one would make a paragraph
                // with any formatting in it one run.
                paragraph.AddText("aaaa bbbb" + Ideograph);
                break;

            case "an ideograph after it":
                paragraph.AddText("aaaa bbbb");
                paragraph.AddFormattedText(Ideograph + "cccc").Font.Bold = true;
                return document;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(seam), seam, "No such seam.");
        }
        paragraph.AddFormattedText("cccc").Font.Bold = true;
        return document;
    }

    /// <summary>U+6F22, the ideograph for Han.</summary>
    private const char Ideograph = (char)0x6F22;

    /// <summary>
    ///   The runs of text drawn in the body of the page, leaving out the foot, where a footnote's
    ///   own text is set.
    /// </summary>
    private static List<(double X, double Y)> BodyRunsOf(Document document) =>
        TextBaselines.PositionsOf(Rendered.FirstPageOf(document)).Where(run => run.Y > 400).ToList();

    [Test]
    [Arguments("a run in another weight")]
    [Arguments("a subscript")]
    [Arguments("a footnote mark")]
    [Arguments("a page field")]
    [Arguments("a symbol")]
    [Arguments("a bookmark")]
    public void ALineIsNotBrokenWhereOneRunOfFormattingMeetsAnother(string seam)
    {
        var placed = BodyRunsOf(BbbbAndCcccMeetingAt(seam));

        placed[1].Y.Should().BeLessThan(placed[0].Y, "\"bbbb\" goes down to the line \"cccc\" is on");
        placed[^1].Y.Should().Be(placed[1].Y);
    }

    [Test]
    [Arguments("a space")]
    [Arguments("a hyphen")]
    [Arguments("a zero-width space")]
    [Arguments("an en dash")]
    [Arguments("a hyphen typed as text")]
    [Arguments("an en dash typed as text")]
    [Arguments("an em dash typed as text")]
    [Arguments("an ideograph")]
    [Arguments("an ideograph after it")]
    public void ALineIsStillBrokenWhereTheFirstRunEndsInABreakOpportunity(string seam)
    {
        var placed = BodyRunsOf(BbbbAndCcccBreakableAt(seam));

        placed[1].Y.Should().Be(placed[0].Y, "\"bbbb\" fits on the first line and may stay there");
        placed[^1].Y.Should().BeLessThan(placed[0].Y);
    }

    /// <summary>
    ///   "aaaa", a dash, and "cccccc" after a no-break space, which together do not fit.
    /// </summary>
    private static Document ADashBeforeANoBreakSpace(SymbolName dash)
    {
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa ");
        paragraph.AddCharacter(dash);
        paragraph.AddText(Chars.NonBreakableSpace + "cccccc");
        return document;
    }

    [Test]
    public void AnEmDashIsKeptWithTheNoBreakSpaceAfterIt()
    {
        // French sets dialogue as an em dash and a no-break space, so that the dash is never left
        // at the end of a line on its own. UAX #14 (LB12a) agrees: no break before a no-break
        // space, except after a hyphen or a dash that allows a break after it, and an em dash
        // does not.
        var placed = BodyRunsOf(ADashBeforeANoBreakSpace(SymbolName.EmDash));

        placed[1].Y.Should().BeLessThan(placed[0].Y, "the dash goes down with the word after it");
        placed[^1].Y.Should().Be(placed[1].Y);
    }

    [Test]
    public void AnEnDashMayBeBrokenAfterEvenBeforeANoBreakSpace()
    {
        var placed = BodyRunsOf(ADashBeforeANoBreakSpace(SymbolName.EnDash));

        placed[1].Y.Should().Be(placed[0].Y, "the dash stays on the first line");
        placed[^1].Y.Should().BeLessThan(placed[0].Y);
    }

    [Test]
    [Arguments((char)0x300D)] // right corner bracket
    [Arguments((char)0x300F)] // right white corner bracket
    [Arguments((char)0x3011)] // right black lenticular bracket
    [Arguments((char)0xFF09)] // fullwidth right parenthesis
    [Arguments((char)0x201D)] // right double quotation mark
    [Arguments((char)0x3002)] // ideographic full stop
    [Arguments((char)0x3001)] // ideographic comma
    [Arguments((char)0xFF0C)] // fullwidth comma
    [Arguments((char)0xFF0E)] // fullwidth full stop
    [Arguments((char)0xFF01)] // fullwidth exclamation mark
    [Arguments((char)0xFF1F)] // fullwidth question mark
    [Arguments((char)0xFF1A)] // fullwidth colon
    [Arguments((char)0xFF1B)] // fullwidth semicolon
    [Arguments((char)0xFF61)] // halfwidth ideographic full stop
    [Arguments((char)0xFF64)] // halfwidth ideographic comma
    public void AClosingMarkIsNotLeftToStartALineAfterAnIdeograph(char mark)
    {
        // A seam beside an ideograph is a place a line may be broken, but not one before a mark
        // that closes what went before it: UAX #14 forbids a break before a closing bracket or
        // quotation mark, and Chinese and Japanese set none of these at the start of a line. A
        // quotation whose words are emphasised and whose closing mark is not meets it at a seam.
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa ");
        paragraph.AddFormattedText("bbbb" + Ideograph).Font.Bold = true;
        paragraph.AddText(mark + "cccc");

        var placed = BodyRunsOf(document);

        placed[1].Y.Should().BeLessThan(placed[0].Y, "the ideograph goes down with the mark after it");
        placed[^1].Y.Should().Be(placed[1].Y);
    }

    [Test]
    [Arguments((char)0x300C)] // left corner bracket
    [Arguments((char)0x300E)] // left white corner bracket
    [Arguments((char)0x3010)] // left black lenticular bracket
    [Arguments((char)0xFF08)] // fullwidth left parenthesis
    [Arguments((char)0x201C)] // left double quotation mark
    public void AnOpeningMarkIsNotLeftToEndALineBeforeAnIdeograph(char mark)
    {
        // The same the other way round: UAX #14 forbids a break after an opening bracket or
        // quotation mark, and Chinese and Japanese set none of these at the end of a line.
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa bbbb" + mark);
        paragraph.AddFormattedText(Ideograph + "cccc").Font.Bold = true;

        var placed = BodyRunsOf(document);

        placed[1].Y.Should().BeLessThan(placed[0].Y, "the mark goes down with the ideograph after it");
        placed[^1].Y.Should().Be(placed[1].Y);
    }

    [Test]
    public void AJoinedRunTooLongForAnyLineIsSetFromTheStartOfOneAndBrokenAtItsSeams()
    {
        // The last resort, as for a word longer than the measure: a run of joined leaves no line
        // can hold goes down to a line of its own and is broken where it has to be, rather than
        // refusing to lay out.
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("x ");
        paragraph.AddText("aaaa");
        paragraph.AddFormattedText("bbbb").Font.Italic = true;
        paragraph.AddFormattedText("cccc").Font.Bold = true;
        paragraph.AddText("dddd");
        paragraph.AddFormattedText("eeee").Font.Italic = true;

        var placed = BodyRunsOf(document);

        placed.Should().HaveCount(6);
        placed[1].Y.Should().BeLessThan(placed[0].Y, "the run does not start on the line \"x\" is on");
        placed[^1].Y.Should().BeLessThan(placed[1].Y, "the run is broken where the line is full");
    }
}
