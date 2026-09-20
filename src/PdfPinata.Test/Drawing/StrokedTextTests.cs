using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   Text can be outlined as well as filled. <see cref="XGraphics.DrawString(string, XFont, XPen, XBrush, XRect, XStringFormat)"/>
///   takes a pen beside its brush, the way every other Draw method
///   in the library does, and the two decide the PDF text rendering mode between them: a brush
///   alone fills, a pen alone strokes, both do both.
///   <para>
///   Before this, <c>DrawString</c> took a brush and nothing else, and the graphics state threw
///   for every rendering mode but 0 and 2 - 2 being reserved for bold simulation, which strokes a
///   face that has no bold of its own to fatten it. Stroked text was unreachable.
///   </para>
/// </summary>
public class StrokedTextTests
{
    const double FontSize = 24;

    static XFont PlainFont => new XFont("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    /// <summary>
    ///   Source Code Pro ships a regular face only, so asking for bold gets bold simulation.
    /// </summary>
    static XFont BoldSimulatedFont =>
        new XFont(PinnedFontResolver.CffFamilyName, FontSize, XFontStyle.Bold, XPdfFontOptions.WinAnsiDefault);

    static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);
        return page;
    }

    static PdfPage PageShowing(XFont font, XPen pen, XBrush brush)
    {
        return PageShowing(gfx => gfx.DrawString("Hello", font, pen, brush, 20, 40));
    }

    // ----- which mode the pen and brush ask for -------------------------------------------------

    [Fact]
    public void ABrushAloneFillsTheText()
    {
        // Mode 0 is where a content stream starts, so filling is what costs nothing to say.
        TextOperators.NumbersGivenTo(PageShowing(PlainFont, null, XBrushes.Black), OpCodeName.Tr)
            .Should().BeEmpty();
    }

    [Fact]
    public void APenAloneStrokesTheText()
    {
        TextOperators.NumbersGivenTo(PageShowing(PlainFont, new XPen(XColors.Red, 1), null), OpCodeName.Tr)
            .Should().Equal(1);
    }

    [Fact]
    public void APenAndABrushFillTheTextAndStrokeIt()
    {
        TextOperators.NumbersGivenTo(PageShowing(PlainFont, new XPen(XColors.Red, 1), XBrushes.Black), OpCodeName.Tr)
            .Should().Equal(2);
    }

    [Fact]
    public void TheOldOverloadStillFillsAndNothingElse()
    {
        // Every existing caller goes through here, and has to come out where it always did.
        var page = PageShowing(gfx => gfx.DrawString("Hello", PlainFont, XBrushes.Black, 20, 40));

        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().BeEmpty();
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().BeEmpty();
    }

    [Fact]
    public void NeitherAPenNorABrushIsRejected()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        // The same answer DrawRectangle gives to the same question.
        gfx.Invoking(g => g.DrawString("Hello", PlainFont, null, null, 20, 40))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ABrushIsStillRequiredByTheOverloadThatOnlyTakesOne()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        gfx.Invoking(g => g.DrawString("Hello", PlainFont, null, 20, 40))
            .Should().Throw<ArgumentNullException>();
    }

    // ----- the pen is the caller's ---------------------------------------------------------------

    [Fact]
    public void TheStrokeIsAsWideAsThePenAsksFor()
    {
        var page = PageShowing(PlainFont, new XPen(XColors.Red, 2.5), XBrushes.Black);

        TextOperators.NumbersGivenTo(page, OpCodeName.w).Should().Contain(2.5);
    }

    [Fact]
    public void TheStrokeIsTheColourThePenAsksFor()
    {
        var page = PageShowing(PlainFont, new XPen(XColors.Red, 1), XBrushes.Black);

        // RG is the stroking colour; rg, which the brush sets, is a different operator.
        TextOperators.OperandsGivenTo(page, OpCodeName.RG).Should().ContainEquivalentOf(new[] { 1d, 0d, 0d });
    }

    [Fact]
    public void TheCallersPenReplacesTheOneBoldSimulationWouldHaveStrokedWith()
    {
        // What bold simulation strokes with when left to itself: a hairline worked out from the
        // em size, not something a caller would ever pick by accident.
        var simulated = TextOperators.NumbersGivenTo(
            PageShowing(BoldSimulatedFont, null, XBrushes.Black), OpCodeName.w);
        simulated.Should().ContainSingle().Which.Should().BeGreaterThan(0).And.BeLessThan(1);

        var withPen = TextOperators.NumbersGivenTo(
            PageShowing(BoldSimulatedFont, new XPen(XColors.Red, 3), XBrushes.Black), OpCodeName.w);

        // The caller asked to stroke the text themselves, so their pen wins outright rather than
        // being drawn over or added to.
        withPen.Should().Contain(3);
        withPen.Should().NotContain(simulated[0]);
    }

    [Fact]
    public void BoldSimulationStillStrokesWhenNoPenIsGiven()
    {
        var page = PageShowing(BoldSimulatedFont, null, XBrushes.Black);

        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().Equal(2);
    }

    // ----- what the widening is keyed on, B3 ----------------------------------------------------

    [Fact]
    public void StrokingTextDoesNotWidenItTheWayBoldSimulationDoes()
    {
        var page = PageShowing(PlainFont, new XPen(XColors.Red, 1), XBrushes.Black);

        // Bold simulation spaces the glyphs out to match the fattening it does. That widening used
        // to be keyed on the rendering mode being 2, which a caller stroking their own text now
        // reaches without owing any of it.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().Equal(2);
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().BeEmpty();
    }

    [Fact]
    public void BoldSimulationStillWidensWhenTheCallerStrokesAsWell()
    {
        var simulatedOnly = TextOperators.NumbersGivenTo(
            PageShowing(BoldSimulatedFont, null, XBrushes.Black), OpCodeName.Tc);
        simulatedOnly.Should().ContainSingle().Which.Should().BeGreaterThan(0);

        var withPen = TextOperators.NumbersGivenTo(
            PageShowing(BoldSimulatedFont, new XPen(XColors.Red, 3), XBrushes.Black), OpCodeName.Tc);

        // The face is still the wrong one and still needs the room; only the pen changed.
        withPen.Should().Equal(simulatedOnly[0]);
    }

    [Fact]
    public void ACharacterSpacingSurvivesStrokedText()
    {
        var format = XStringFormats.Default;
        format.CharacterSpacing = 4;

        var page = PageShowing(gfx =>
            gfx.DrawString("Hello", PlainFont, new XPen(XColors.Red, 1), XBrushes.Black, 20, 40, format));

        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().Equal(4);
    }

    // ----- the mode is state ---------------------------------------------------------------------

    [Fact]
    public void GoingBackToPlainFilledTextSaysSo()
    {
        var page = PageShowing(gfx =>
        {
            gfx.DrawString("outlined", PlainFont, new XPen(XColors.Red, 1), null, 20, 40);
            gfx.DrawString("filled", PlainFont, XBrushes.Black, 20, 60);
        });

        // Tr is graphics state and stays set, so returning to a plain fill has to be written out.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().Equal(1, 0);
    }

    [Fact]
    public void TheModeIsSetOnceForTwoStringsThatShareIt()
    {
        var page = PageShowing(gfx =>
        {
            gfx.DrawString("first", PlainFont, new XPen(XColors.Red, 1), XBrushes.Black, 20, 40);
            gfx.DrawString("second", PlainFont, new XPen(XColors.Red, 1), XBrushes.Black, 20, 60);
        });

        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().Equal(2);
    }
}
