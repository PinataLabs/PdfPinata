using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Drawing;
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
///     there was a space there. Only a non-breakable blank joined two leaves. Now two leaves are
///     joined unless the line breaking algorithm, UAX #14, allows a break where they meet - after
///     a hyphen, a zero-width space or a dash, between two ideographs, but not before a closing
///     mark or a nonstarter - and a run of them goes down to the next line whole when it does not
///     fit.
///   </para>
///   <para>
///     Most paragraphs here are sixty points wide, which holds "aaaa bbbb" but not "aaaa bbbbcccc",
///     so the question each test asks is whether "bbbb" stays on the first line or goes down with
///     "cccc". The word of three runs is the exception: its column is measured to hold
///     "Reference number" and the word's first two runs but not its third.
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

            case "a hyphen before a digit in the same run":
                // Not a seam at all: one run, split into leaves after its hyphen. UAX #14 would
                // keep "-1" together (LB25), but a line has always been breakable after every
                // hyphen-minus inside a run, and breaking inside a run is not what changed.
                paragraph.AddText("aaaa bbbb-1cc");
                return document;

            case "a Thai letter":
                // Thai is written without spaces between words too, and finding the words needs a
                // dictionary. Without one a seam beside a Thai letter stays a place a line may be
                // broken, as beside an ideograph, rather than UAX #14's default of joining.
                paragraph.AddText("aaaa bbbb" + ThaiKoKai);
                break;

            case "a Thai letter after it":
                paragraph.AddText("aaaa bbbb");
                paragraph.AddFormattedText(ThaiKoKai + "cccc").Font.Bold = true;
                return document;

            case "two Thai letters":
                paragraph.AddText("aaaa bbbb" + ThaiKoKai);
                paragraph.AddFormattedText(ThaiKhoKhai + "cccc").Font.Bold = true;
                return document;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(seam), seam, "No such seam.");
        }
        paragraph.AddFormattedText("cccc").Font.Bold = true;
        return document;
    }

    /// <summary>U+6F22, the ideograph for Han.</summary>
    private const char Ideograph = (char)0x6F22;

    /// <summary>U+0E01 and U+0E02, the Thai letters ko kai and kho khai.</summary>
    private const char ThaiKoKai = (char)0x0E01;
    private const char ThaiKhoKhai = (char)0x0E02;

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
    [Arguments("a hyphen before a digit in the same run")]
    [Arguments("a Thai letter")]
    [Arguments("a Thai letter after it")]
    [Arguments("two Thai letters")]
    public void ALineIsStillBrokenWhereTheFirstRunEndsInABreakOpportunity(string seam)
    {
        var placed = BodyRunsOf(BbbbAndCcccBreakableAt(seam));

        placed[1].Y.Should().Be(placed[0].Y, "\"bbbb\" fits on the first line and may stay there");
        placed[^1].Y.Should().BeLessThan(placed[0].Y);
    }

    /// <summary>
    ///   "Reference number", then a word of three runs of formatting - <paramref name="first"/> in
    ///   the paragraph's own font, <paramref name="second"/> and <paramref name="third"/> in the
    ///   fonts given for them - and " February", in a paragraph just wide enough for the first two
    ///   runs of the word but not the third.
    /// </summary>
    private static Document AWordOfThreeRuns(string first, string second, RunFont secondFont, string third, RunFont thirdFont)
    {
        var document = new Document();
        document.Styles.Normal.Font.Name = "Arial";
        document.Styles.Normal.Font.Size = 9;

        using var measure = XGraphics.CreateMeasureContext(new XSize(100, 100), XGraphicsUnit.Point, XPageDirection.Downwards);
        var normal = new XFont("Arial", 9);
        var width = measure.MeasureString("Reference number ", normal).Width
            + measure.MeasureString(first, normal).Width
            + measure.MeasureString(second, secondFont.ToXFont()).Width
            + 0.2;

        var section = document.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(20);
        section.PageSetup.RightMargin = Unit.FromPoint(20);
        section.PageSetup.PageWidth = Unit.FromPoint(width + 40);

        var paragraph = section.AddParagraph();
        paragraph.AddText("Reference number ");
        paragraph.AddFormattedText(first);
        secondFont.Dress(paragraph.AddFormattedText(second));
        thirdFont.Dress(paragraph.AddFormattedText(third));
        paragraph.AddText(" February");
        return document;
    }

    private sealed record RunFont(string Family, double Size, XFontStyle Style = XFontStyle.Regular)
    {
        internal XFont ToXFont() => new(Family, Size, Style);

        internal void Dress(FormattedText text)
        {
            text.Font.Name = Family;
            text.Font.Size = Size;
            text.Font.Bold = (Style & XFontStyle.Bold) != 0;
            text.Font.Italic = (Style & XFontStyle.Italic) != 0;
        }
    }

    [Test]
    [Arguments("1", "5", "0")]
    [Arguments("A", "b", "c")]
    public void AWordOfThreeRunsInThreeStylesGoesDownWhole(string first, string second, string third)
    {
        // empira/PDFsharp#407: a regular "1", a bold "5" and an italic "0" left "15" at the end of
        // one line and "0" alone at the start of the next.
        var placed = BodyRunsOf(AWordOfThreeRuns(
            first, second, new RunFont("Arial", 9, XFontStyle.Bold), third, new RunFont("Arial", 9, XFontStyle.Italic)));

        placed.Should().HaveCount(6, "\"Reference\", \"number\", the word's three runs and \"February\" are drawn");
        placed[2].Y.Should().BeLessThan(placed[1].Y, "the word goes down from the line \"number\" is on");
        placed[3].Y.Should().Be(placed[2].Y);
        placed[4].Y.Should().Be(placed[2].Y);
    }

    [Test]
    public void TheFirstTwoRunsStayOnTheFirstLineWhereTheThirdStartsWithASpace()
    {
        // The control: with a space before the third run, a line may be broken there, and the
        // first two runs fit after "number" and stay - so the word going down whole above is the
        // join and not the measure.
        var placed = BodyRunsOf(AWordOfThreeRuns(
            "1", "5", new RunFont("Arial", 9, XFontStyle.Bold), " 0", new RunFont("Arial", 9, XFontStyle.Italic)));

        placed.Should().HaveCount(6, "\"Reference\", \"number\", the three runs and \"February\" are drawn");
        placed[2].Y.Should().Be(placed[1].Y, "\"15\" fits on the line \"number\" is on");
        placed[3].Y.Should().Be(placed[1].Y);
        placed[4].Y.Should().BeLessThan(placed[1].Y, "\"0\" goes down on its own");
    }

    [Test]
    public void AWordOfThreeRunsInDifferentFamiliesAndSizesGoesDownWhole()
    {
        var placed = BodyRunsOf(AWordOfThreeRuns(
            "1", "5", new RunFont(PinnedFontResolver.CffFamilyName, 9), "0", new RunFont("Arial", 14)));

        placed.Should().HaveCount(6, "\"Reference\", \"number\", the word's three runs and \"February\" are drawn");
        placed[2].Y.Should().BeLessThan(placed[1].Y, "the word goes down from the line \"number\" is on");
        placed[3].Y.Should().Be(placed[2].Y);
        placed[4].Y.Should().Be(placed[2].Y);
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
    [Arguments((char)0x3005)] // ideographic iteration mark
    [Arguments((char)0x309D)] // hiragana iteration mark
    [Arguments((char)0x30FC)] // katakana-hiragana prolonged sound mark
    [Arguments((char)0x3041)] // hiragana letter small a
    [Arguments((char)0x30C3)] // katakana letter small tu
    [Arguments((char)0x30FB)] // katakana middle dot
    public void ANonstarterIsNotLeftToStartALineAfterAnIdeograph(char mark)
    {
        // UAX #14's nonstarters (NS) and the small kana (CJ, which rule LB1 makes NS): iteration
        // marks, the prolonged sound mark, the small kana and the middle dot, none of which
        // Japanese sets at the start of a line. Neither is a closing mark, so a seam before one
        // used to be a place a line could be broken.
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa ");
        paragraph.AddFormattedText("bbbb" + Ideograph).Font.Bold = true;
        paragraph.AddText(mark + "cccc");

        var placed = BodyRunsOf(document);

        placed[1].Y.Should().BeLessThan(placed[0].Y, "the ideograph goes down with the mark after it");
        placed[^1].Y.Should().Be(placed[1].Y);
    }

    [Test]
    [Arguments("\u2014", ")")]      // em dash, right parenthesis
    [Arguments("\u2013", "\u300D")] // en dash, right corner bracket
    [Arguments("-", ")")]           // hyphen-minus, right parenthesis
    [Arguments("-", "\u3002")]      // hyphen-minus, ideographic full stop
    public void ADashIsNotBrokenFromTheClosingMarkAfterIt(string dash, string mark)
    {
        // A line may be broken after a hyphen or a dash, but not where what follows it closes
        // what went before: UAX #14's LB13 forbids a break before a closing mark whatever precedes
        // it, and a line starting with ")" is worse than one a dash ends early.
        var paragraph = AParagraphSixtyPointsWide(out var document);
        paragraph.AddText("aaaa bbbb" + dash);
        paragraph.AddFormattedText(mark + "cccc").Font.Bold = true;

        var placed = BodyRunsOf(document);

        placed[1].Y.Should().BeLessThan(placed[0].Y, "the dash goes down with the mark after it");
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
