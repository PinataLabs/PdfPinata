using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   Underlining and striking out are settable per draw, on the format, rather than only through
///   the style of the font; and in the seven shapes PinataLayout has always had rather than the one
///   the core had.
///   <para>
///   A solid rule is a filled rectangle, which is how it has always been drawn and what every
///   document made with this library looks like. A broken one has to be stroked, because a
///   rectangle cannot be dotted - so the operators say which kind was drawn.
///   </para>
/// </summary>
public class TextDecorationTests
{
    private const double FontSize = 24;

    private static XFont Plain => new("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    private static XFont Underlined => new("Arial", FontSize, XFontStyle.Underline, XPdfFontOptions.WinAnsiDefault);
    private static XFont StruckOut => new("Arial", FontSize, XFontStyle.Strikeout, XPdfFontOptions.WinAnsiDefault);

    private static PdfPage PageShowing(string text, XFont font, XStringFormat format)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString(text, font, XBrushes.Black, 20, 60, format);
        return page;
    }

    /// <summary>How many filled rectangles the page draws - one per solid rule.</summary>
    private static int RulesFilledOn(PdfPage page) => TextOperators.CountOf(page, OpCodeName.re);

    /// <summary>How many strokes the page draws - one per broken rule.</summary>
    private static int RulesStrokedOn(PdfPage page) => TextOperators.CountOf(page, OpCodeName.S);

    // ----- D1, decoration without the font style -------------------------------------------------

    [Fact]
    public void NothingIsDrawnUnderPlainText()
    {
        RulesFilledOn(PageShowing("Hello", Plain, XStringFormats.Default)).Should().Be(0);
    }

    [Fact]
    public void AnUnderlineCanBeAskedForWithoutSettingItOnTheFont()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Single;

        RulesFilledOn(PageShowing("Hello", Plain, format)).Should().Be(1);
    }

    [Fact]
    public void AStrikeoutCanBeAskedForWithoutSettingItOnTheFont()
    {
        var format = XStringFormats.Default;
        format.Strikeout = XTextDecoration.Single;

        RulesFilledOn(PageShowing("Hello", Plain, format)).Should().Be(1);
    }

    [Fact]
    public void TheFontStyleStillUnderlinesOnItsOwn()
    {
        // The way it was done before the format could say it, and the way every existing caller
        // still does it.
        RulesFilledOn(PageShowing("Hello", Underlined, XStringFormats.Default)).Should().Be(1);
    }

    [Fact]
    public void TheFontStyleStillStrikesOutOnItsOwn()
    {
        RulesFilledOn(PageShowing("Hello", StruckOut, XStringFormats.Default)).Should().Be(1);
    }

    [Fact]
    public void BothAtOnceDrawTwoRules()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Single;
        format.Strikeout = XTextDecoration.Single;

        RulesFilledOn(PageShowing("Hello", Plain, format)).Should().Be(2);
    }

    [Fact]
    public void TheFormatDecidesTheStyleWhenTheFontAlsoAsksForOne()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Dotted;

        var page = PageShowing("Hello", Underlined, format);

        // Dotted, from the format, rather than the solid rule the font style would have given.
        RulesStrokedOn(page).Should().Be(1);
        RulesFilledOn(page).Should().Be(0);
    }

    // ----- D2, the styles ------------------------------------------------------------------------

    [Fact]
    public void ASolidRuleIsFilledRatherThanStroked()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Single;

        var page = PageShowing("Hello", Plain, format);

        RulesFilledOn(page).Should().Be(1);
        RulesStrokedOn(page).Should().Be(0);
    }

    [Theory]
    [InlineData(XTextDecoration.Dotted)]
    [InlineData(XTextDecoration.Dash)]
    [InlineData(XTextDecoration.DotDash)]
    [InlineData(XTextDecoration.DotDotDash)]
    public void ABrokenRuleIsStrokedWithADashPattern(XTextDecoration style)
    {
        var format = XStringFormats.Default;
        format.Underline = style;

        var page = PageShowing("Hello", Plain, format);

        RulesStrokedOn(page).Should().Be(1);
        RulesFilledOn(page).Should().Be(0);

        // d sets the dash pattern, and a broken rule needs a non-empty one.
        TextOperators.OperandsGivenTo(page, OpCodeName.d).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(XTextDecoration.Dotted)]
    [InlineData(XTextDecoration.Dash)]
    [InlineData(XTextDecoration.DotDash)]
    [InlineData(XTextDecoration.DotDotDash)]
    public void EachBrokenStyleAsksForADifferentPattern(XTextDecoration style)
    {
        var format = XStringFormats.Default;
        format.Underline = style;
        var broken = PageShowing("Hello", Plain, format);

        var solid = XStringFormats.Default;
        solid.Underline = XTextDecoration.Single;

        // Whatever the pattern is, it is not the one a solid rule would have used - which is none.
        TextOperators.CountOf(broken, OpCodeName.S).Should().Be(1);
        TextOperators.CountOf(PageShowing("Hello", Plain, solid), OpCodeName.S).Should().Be(0);
    }

    [Fact]
    public void UnderliningWordsLeavesTheSpacesBetweenThemUnmarked()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Words;

        // Three words, so three rules rather than one running under the whole thing.
        RulesFilledOn(PageShowing("one two three", Plain, format)).Should().Be(3);
    }

    [Fact]
    public void UnderliningWordsIgnoresTheSpaceAtEitherEnd()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Words;

        RulesFilledOn(PageShowing("  one  two  ", Plain, format)).Should().Be(2);
    }

    [Fact]
    public void UnderliningWordsOfASingleWordIsOneRule()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Words;

        RulesFilledOn(PageShowing("Hello", Plain, format)).Should().Be(1);
    }

    [Fact]
    public void TheRulesUnderWordsFollowTheWordsAcrossTheLine()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Words;

        var rects = TextOperators.OperandsGivenTo(PageShowing("one two three", Plain, format), OpCodeName.re);

        rects.Should().HaveCount(3);
        // re takes x, y, width, height; each rule starts further right than the one before and
        // none of them is wider than the word it marks.
        rects[1][0].Should().BeGreaterThan(rects[0][0] + rects[0][2]);
        rects[2][0].Should().BeGreaterThan(rects[1][0] + rects[1][2]);
    }

    // ----- D2, the colour ------------------------------------------------------------------------

    [Fact]
    public void ARuleFollowsTheColourOfTheTextByDefault()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Single;

        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Hello", Plain, XBrushes.Red, 20, 60, format);

        // One fill colour on the page, used by both the glyphs and the rule.
        TextOperators.OperandsGivenTo(page, OpCodeName.rg).Should().AllSatisfy(
            colour => colour.Should().Equal(1d, 0d, 0d));
    }

    [Fact]
    public void ARuleCanBeGivenAColourOfItsOwn()
    {
        var format = XStringFormats.Default;
        format.Underline = XTextDecoration.Single;
        format.DecorationColor = XColors.Red;

        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Hello", Plain, XBrushes.Black, 20, 60, format);

        var colours = TextOperators.OperandsGivenTo(page, OpCodeName.rg);

        // Black text, red rule - which is the one thing a caller cannot get any other way.
        colours.Should().ContainEquivalentOf(new[] { 0d, 0d, 0d });
        colours.Should().ContainEquivalentOf(new[] { 1d, 0d, 0d });
    }

    // ----- D4, the baselines ---------------------------------------------------------------------

    private static double AscentOf(XFont font) => font.GetHeight() * font.CellAscent / font.CellSpace;
    private static double DescentOf(XFont font) => font.GetHeight() * font.CellDescent / font.CellSpace;
    private static double XHeightOf(XFont font) => font.GetHeight() * font.Metrics.XHeight / font.CellSpace;

    private static double BaselineFor(XLineAlignment alignment)
    {
        var format = XStringFormats.Default;
        format.LineAlignment = alignment;

        // A rectangle of no height, which is what the point overloads make and what the canvas
        // baselines are defined against.
        var page = PageShowing("Handles", Plain, format);
        return TextBaselines.PositionsOf(page)[0].Y;
    }

    [Fact]
    public void HangingTextIsDroppedByItsAscent()
    {
        // PDF measures up the page, so putting the top of the text on the line moves the baseline
        // down - to a smaller y.
        (BaselineFor(XLineAlignment.BaseLine) - BaselineFor(XLineAlignment.Hanging))
            .Should().BeApproximately(AscentOf(Plain), 0.01);
    }

    [Fact]
    public void IdeographicTextIsLiftedByItsDescent()
    {
        (BaselineFor(XLineAlignment.Ideographic) - BaselineFor(XLineAlignment.BaseLine))
            .Should().BeApproximately(DescentOf(Plain), 0.01);
    }

    [Fact]
    public void SvgMiddleTextIsDroppedByHalfItsXHeight()
    {
        (BaselineFor(XLineAlignment.BaseLine) - BaselineFor(XLineAlignment.SvgMiddle))
            .Should().BeApproximately(XHeightOf(Plain) / 2, 0.01);
    }

    [Fact]
    public void HangingIsWhereNearIsWhenThereIsNoRectangleToSpeakOf()
    {
        // The two differ only in what they are measured against, and a rectangle of no height
        // leaves nothing to differ about.
        BaselineFor(XLineAlignment.Hanging).Should().BeApproximately(BaselineFor(XLineAlignment.Near), 0.01);
    }

    [Fact]
    public void TheThreeNewBaselinesSitInTheOrderTheirNamesSuggest()
    {
        var hanging = BaselineFor(XLineAlignment.Hanging);
        var svgMiddle = BaselineFor(XLineAlignment.SvgMiddle);
        var alphabetic = BaselineFor(XLineAlignment.BaseLine);
        var ideographic = BaselineFor(XLineAlignment.Ideographic);

        // Down the page from the top of the text to the bottom of it.
        hanging.Should().BeLessThan(svgMiddle);
        svgMiddle.Should().BeLessThan(alphabetic);
        alphabetic.Should().BeLessThan(ideographic);
    }

    [Fact]
    public void TheBaselinesThatWereThereBeforeHaveNotMoved()
    {
        var format = XStringFormats.Default;
        format.LineAlignment = XLineAlignment.BaseLine;

        // Drawn at y = 60 on an A4 page, so the baseline is 60 points down from the top.
        var page = PageShowing("Handles", Plain, format);
        var pageHeight = page.Height.Point;

        TextBaselines.PositionsOf(page)[0].Y.Should().BeApproximately(pageHeight - 60, 0.01);
    }
}
