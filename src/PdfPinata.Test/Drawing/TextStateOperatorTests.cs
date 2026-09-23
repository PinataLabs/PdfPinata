using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The text state a caller sets on an <see cref="XStringFormat"/> reaches the content stream:
///   character spacing as Tc, word spacing as Tw, horizontal scaling as Tz.
///   <para>
///   Before this the renderer wrote Tc only to fake bold and wrote Tw and Tz never, so the three
///   properties measured for in <see cref="TextStateMeasurementTests"/> had no effect on what was
///   drawn. These check the other half of that: that what is measured is what is written.
///   </para>
/// </summary>
public class TextStateOperatorTests
{
    private const double FontSize = 12;

    /// <summary>
    ///   How close a number read back out of the content stream can be to the one that went in.
    ///   Text matrices are written to four decimal places, so tan(20°) comes back as 0.3639 and
    ///   nothing finer than this can be asserted about it.
    /// </summary>
    private const double StreamPrecision = 1e-4;

    /// <summary>Liberation Sans, encoded as WinAnsi - the default, and what Tw can speak for.</summary>
    private static XFont WinAnsiFont => new("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    /// <summary>The same face embedded as Identity-H, whose two-byte codes Tw cannot reach.</summary>
    private static XFont UnicodeFont => new("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.UnicodeDefault);

    /// <summary>
    ///   Source Code Pro ships only a regular face, so asking for bold gets bold simulation -
    ///   which draws its own character spacing, and is the thing a caller's spacing has to
    ///   compose with rather than replace.
    /// </summary>
    private static XFont BoldSimulatedFont =>
        new(PinnedFontResolver.CffFamilyName, FontSize, XFontStyle.Bold, XPdfFontOptions.WinAnsiDefault);

    private static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        draw(gfx);
        return page;
    }

    private static PdfPage PageShowing(string text, XFont font, XStringFormat format)
    {
        return PageShowing(gfx => gfx.DrawString(text, font, XBrushes.Black, 20, 40, format));
    }

    // ----- nothing asked for, nothing written ---------------------------------------------------

    [Fact]
    public void AFormatThatAsksForNothingWritesNoTextStateAtAll()
    {
        // The guard on every existing document: a default format has to leave the content stream
        // exactly as it was before any of this existed.
        var page = PageShowing("Hello world", WinAnsiFont, XStringFormats.Default);

        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().BeEmpty();
        TextOperators.NumbersGivenTo(page, OpCodeName.Tw).Should().BeEmpty();
        TextOperators.NumbersGivenTo(page, OpCodeName.Tz).Should().BeEmpty();
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.Tj);
    }

    // ----- character spacing, A3 ----------------------------------------------------------------

    [Fact]
    public void CharacterSpacingIsWrittenAsTc()
    {
        var format = XStringFormats.Default;
        format.CharacterSpacing = 1.5;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Tc)
            .Should().Equal(1.5);
    }

    [Fact]
    public void ANegativeCharacterSpacingIsWrittenAsItIs()
    {
        var format = XStringFormats.Default;
        format.CharacterSpacing = -0.75;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Tc)
            .Should().Equal(-0.75);
    }

    [Fact]
    public void CharacterSpacingAddsToBoldSimulationRatherThanReplacingIt()
    {
        // Bold simulation strokes the glyphs and spaces them out to match. Overwriting that
        // spacing with the caller's would quietly un-bolden the text, so the two are added.
        var simulatedOnly = TextOperators.NumbersGivenTo(
            PageShowing("Hello", BoldSimulatedFont, XStringFormats.Default), OpCodeName.Tc);

        simulatedOnly.Should().ContainSingle().Which.Should().BeGreaterThan(0);

        var format = XStringFormats.Default;
        format.CharacterSpacing = 2;
        var withBoth = TextOperators.NumbersGivenTo(
            PageShowing("Hello", BoldSimulatedFont, format), OpCodeName.Tc);

        withBoth.Should().ContainSingle().Which.Should().BeApproximately(simulatedOnly[0] + 2, 1e-6);
    }

    // ----- word spacing, A4 ---------------------------------------------------------------------

    [Fact]
    public void WordSpacingIsWrittenAsTwForAFontEncodedAsWinAnsi()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 4;

        var page = PageShowing("a b c", WinAnsiFont, format);

        TextOperators.NumbersGivenTo(page, OpCodeName.Tw).Should().Equal(4);
        // One byte per code, so Tw reaches the spaces and the run stays in one piece.
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.Tj);
    }

    [Fact]
    public void WordSpacingIsDrawnByHandForAFontEncodedAsUnicode()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 5;

        var page = PageShowing("a b c", UnicodeFont, format);

        // Tw counts single-byte code 32 only, and Identity-H writes two bytes per code, so a Tw
        // here would be accepted and ignored. The run is broken up and moved apart instead.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tw).Should().NotContain(5);
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.TJ);

        // -wordSpacing * 1000 / fontSize, the number that buys one word spacing back.
        var expected = -5 * 1000 / FontSize;
        TextOperators.TjAdjustments(page).Should().HaveCount(2)
            .And.AllSatisfy(adjustment => adjustment.Should().BeApproximately(expected, 0.001));
    }

    [Fact]
    public void AUnicodeRunIsBrokenIntoOnePieceMoreThanItHasSpaces()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 5;

        // "a b c" - the space stays with the word in front of it, so the pieces are "a ", "b "
        // and "c", with the gap opened up after each of the first two.
        TextOperators.TjRunCounts(PageShowing("a b c", UnicodeFont, format)).Should().Equal(3);
    }

    [Fact]
    public void ATrailingSpaceStillGetsItsWordSpacing()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 5;

        // Nothing follows the space, so there is no run after the gap - but the gap is still
        // owed, because measurement counted it.
        var page = PageShowing("a ", UnicodeFont, format);

        TextOperators.TjRunCounts(page).Should().Equal(1);
        TextOperators.TjAdjustments(page).Should().HaveCount(1);
    }

    [Fact]
    public void AUnicodeRunWithNoWordSpacingIsStillDrawnInOneGo()
    {
        // A TJ array of one run would be correct but wasteful, and would change every existing
        // document that uses a Unicode font.
        var page = PageShowing("a b c", UnicodeFont, XStringFormats.Default);

        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.Tj);
    }

    [Fact]
    public void AUnicodeRunWithoutSpacesIsDrawnInOneGoEvenWhenWordSpacingIsAskedFor()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 5;

        var page = PageShowing("abc", UnicodeFont, format);

        // A word spacing asked for and no space to pay it out at, so nothing has to be displaced
        // and the array a displacement would need is not written at all - the same economy
        // AUnicodeRunWithNoWordSpacingIsStillDrawnInOneGo asks for, and which this case used to
        // miss by writing a TJ of one run and no adjustments.
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.Tj);
        TextOperators.TjAdjustments(page).Should().BeEmpty();
    }

    // ----- horizontal scaling, A5 ---------------------------------------------------------------

    [Fact]
    public void HorizontalScalingIsWrittenAsTz()
    {
        var format = XStringFormats.Default;
        format.HorizontalScaling = 75;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Tz)
            .Should().Equal(75);
    }

    [Fact]
    public void AScalingOfAHundredIsNotWrittenBecauseThatIsWhereItStarts()
    {
        var format = XStringFormats.Default;
        format.HorizontalScaling = 100;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Tz)
            .Should().BeEmpty();
    }

    // ----- text rise, A6 ------------------------------------------------------------------------

    [Fact]
    public void TextRiseIsWrittenAsTs()
    {
        var format = XStringFormats.Default;
        format.TextRise = 5;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Ts)
            .Should().Equal(5);
    }

    [Fact]
    public void ANegativeTextRiseLowersTheTextAndIsWrittenAsItIs()
    {
        var format = XStringFormats.Default;
        format.TextRise = -3;

        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, format), OpCodeName.Ts)
            .Should().Equal(-3);
    }

    [Fact]
    public void ARiseOfNothingIsNotWritten()
    {
        TextOperators.NumbersGivenTo(PageShowing("Hello", WinAnsiFont, XStringFormats.Default), OpCodeName.Ts)
            .Should().BeEmpty();
    }

    [Fact]
    public void TextRiseDoesNotDisturbWhereTheNextStringGoes()
    {
        var raised = XStringFormats.Default;
        raised.TextRise = 8;

        var page = PageShowing(gfx =>
        {
            gfx.DrawString("raised", WinAnsiFont, XBrushes.Black, 20, 40, raised);
            gfx.DrawString("level", WinAnsiFont, XBrushes.Black, 20, 40, XStringFormats.Default);
        });

        // Ts belongs to the text rendering matrix, not the text matrix, so both strings are
        // positioned to the same baseline and only the glyphs of the first are lifted off it.
        var positions = TextBaselines.PositionsOf(page);
        positions.Should().HaveCount(2);
        positions[1].Y.Should().BeApproximately(positions[0].Y, 1e-6);
    }

    // ----- oblique angle, D3 --------------------------------------------------------------------

    [Fact]
    public void AnUprightStringNeedsNoTextMatrixAtAll()
    {
        var page = PageShowing("Hello", WinAnsiFont, XStringFormats.Default);

        TextOperators.CountOf(page, OpCodeName.Tm).Should().Be(0);
        TextOperators.CountOf(page, OpCodeName.Td).Should().Be(1);
    }

    [Fact]
    public void AnObliqueAngleLeansTheTextMatrixByItsTangent()
    {
        var format = XStringFormats.Default;
        format.ObliqueAngle = 20;

        // Only Tm can lean the text, so asking for an angle costs a text matrix.
        TextOperators.TextMatrixSkews(PageShowing("Hello", WinAnsiFont, format))
            .Should().ContainSingle()
            .Which.Should().BeApproximately(Math.Tan(20 * Math.PI / 180), StreamPrecision);
    }

    [Fact]
    public void ANegativeObliqueAngleLeansTheOtherWay()
    {
        var format = XStringFormats.Default;
        format.ObliqueAngle = -15;

        TextOperators.TextMatrixSkews(PageShowing("Hello", WinAnsiFont, format))
            .Should().ContainSingle()
            .Which.Should().BeApproximately(Math.Tan(-15 * Math.PI / 180), StreamPrecision);
    }

    [Fact]
    public void AnObliqueAngleAddsToTheLeanItalicSimulationAlreadyGives()
    {
        // Source Code Pro has no italic face either, so asking for one skews the regular.
        var italicSimulated = new XFont(PinnedFontResolver.CffFamilyName, FontSize, XFontStyle.Italic,
            XPdfFontOptions.WinAnsiDefault);

        var simulatedOnly = TextOperators.TextMatrixSkews(
            PageShowing("Hello", italicSimulated, XStringFormats.Default));
        simulatedOnly.Should().ContainSingle().Which.Should().BeGreaterThan(0);

        var format = XStringFormats.Default;
        format.ObliqueAngle = 10;
        var withBoth = TextOperators.TextMatrixSkews(PageShowing("Hello", italicSimulated, format));

        // Shearing by one amount and then another is shearing by the sum, so the two compose by
        // adding rather than one winning.
        withBoth.Should().ContainSingle()
            .Which.Should().BeApproximately(simulatedOnly[0] + Math.Tan(10 * Math.PI / 180), StreamPrecision);
    }

    [Fact]
    public void TheLeanIsSetOnceForTwoStringsThatShareIt()
    {
        var format = XStringFormats.Default;
        format.ObliqueAngle = 20;

        var page = PageShowing(gfx =>
        {
            gfx.DrawString("first", WinAnsiFont, XBrushes.Black, 20, 40, format);
            gfx.DrawString("second", WinAnsiFont, XBrushes.Black, 20, 60, format);
        });

        // The second string moves with Td, which is shorter than a second Tm - and which is why
        // the offset it is given has to be corrected for the lean it travels through.
        TextOperators.CountOf(page, OpCodeName.Tm).Should().Be(1);
        TextOperators.CountOf(page, OpCodeName.Td).Should().Be(1);
    }

    [Fact]
    public void GoingBackToUprightSetsTheTextMatrixStraightAgain()
    {
        var leaning = XStringFormats.Default;
        leaning.ObliqueAngle = 20;

        var page = PageShowing(gfx =>
        {
            gfx.DrawString("leaning", WinAnsiFont, XBrushes.Black, 20, 40, leaning);
            gfx.DrawString("upright", WinAnsiFont, XBrushes.Black, 20, 60, XStringFormats.Default);
        });

        // Standing the text back up needs saying, or the second string keeps the first's lean.
        TextOperators.TextMatrixSkews(page).Should().HaveCount(2);
        TextOperators.TextMatrixSkews(page)[1].Should().Be(0);
    }

    [Fact]
    public void ATdThroughALeaningMatrixIsCorrectedForTheLean()
    {
        var leaning = XStringFormats.Default;
        leaning.ObliqueAngle = 20;

        var page = PageShowing(gfx =>
        {
            // Same x, thirty points apart down the page.
            gfx.DrawString("first", WinAnsiFont, XBrushes.Black, 20, 40, leaning);
            gfx.DrawString("second", WinAnsiFont, XBrushes.Black, 20, 70, leaning);
        });

        var offsets = TextOperators.TdOffsets(page);
        offsets.Should().ContainSingle();
        Math.Abs(offsets[0].Y).Should().BeApproximately(30, StreamPrecision);

        // A leaning text matrix carries a Td offset sideways by the height it moves through. The
        // two strings were asked for at the same x, so the offset that gets them there is not
        // zero but exactly minus that carry - and without the correction they would step sideways
        // by eleven points a line.
        offsets[0].X.Should().BeApproximately(-Math.Tan(20 * Math.PI / 180) * offsets[0].Y, StreamPrecision);
        offsets[0].X.Should().NotBe(0);
    }

    // ----- the state is state -------------------------------------------------------------------

    [Fact]
    public void TheTextStateIsWrittenOnceForTwoStringsThatShareIt()
    {
        var format = XStringFormats.Default;
        format.CharacterSpacing = 1;
        format.HorizontalScaling = 80;

        var page = PageShowing(gfx =>
        {
            gfx.DrawString("first", WinAnsiFont, XBrushes.Black, 20, 40, format);
            gfx.DrawString("second", WinAnsiFont, XBrushes.Black, 20, 60, format);
        });

        // These are graphics state parameters, not something each string carries with it.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().Equal(1);
        TextOperators.NumbersGivenTo(page, OpCodeName.Tz).Should().Equal(80);
    }

    [Fact]
    public void TheTextStateIsWrittenAgainWhenItChanges()
    {
        var spaced = XStringFormats.Default;
        spaced.CharacterSpacing = 3;

        var page = PageShowing(gfx =>
        {
            gfx.DrawString("spaced", WinAnsiFont, XBrushes.Black, 20, 40, spaced);
            gfx.DrawString("plain", WinAnsiFont, XBrushes.Black, 20, 60, XStringFormats.Default);
        });

        // Back to nothing has to be said out loud, or the second string keeps the first's spacing.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().Equal(3, 0);
    }

    [Fact]
    public void TheTextStateGoesBackWithTheGraphicsState()
    {
        var spaced = XStringFormats.Default;
        spaced.CharacterSpacing = 3;

        var page = PageShowing(gfx =>
        {
            var state = gfx.Save();
            gfx.DrawString("spaced", WinAnsiFont, XBrushes.Black, 20, 40, spaced);
            gfx.Restore(state);
            gfx.DrawString("plain", WinAnsiFont, XBrushes.Black, 20, 60, XStringFormats.Default);
        });

        // Q puts Tc back to 0 along with everything else, so the renderer must not think it is
        // still 3 and write a 0 Tc that is already true - nor think it is 0 and write nothing
        // when it is not. One Tc, for the string that asked for one.
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().Equal(3);
    }

    // ----- the characters drawn are the characters measured --------------------------------------
    //
    // FontHelper.MeasureString has always turned a tab into a space and dropped every other
    // character below 32; DrawString handed all of them to the cmap as ordinary code points and
    // drew whatever box came back. Both now filter through TextNormalization, so what is written
    // here is what was measured. The strings are read back as literals, which is why these use the
    // WinAnsi font: an Identity-H run writes glyph numbers instead.

    [Fact]
    public void ATabIsDrawnAsTheSpaceItIsMeasuredAs()
    {
        var page = PageShowing("Handgloves\tand quartz", WinAnsiFont, XStringFormats.Default);

        TextOperators.ShownStrings(page).Should().Equal("Handgloves and quartz");
    }

    [Fact]
    public void AControlCharacterOtherThanATabIsNotDrawnAtAll()
    {
        // A carriage return has never been measured and is now not drawn either. Nothing here
        // folds it into a line break the way XTextFormatter does - that is a separate question,
        // and answering it in passing would be a second change wearing this one's clothes.
        var page = PageShowing("Hand\rgloves", WinAnsiFont, XStringFormats.Default);

        TextOperators.ShownStrings(page).Should().Equal("Handgloves");
    }

    [Fact]
    public void AStringOfNothingButControlCharactersDrawsNothingAtAll()
    {
        // It normalizes to empty, and an empty string is not a font realization, a pen movement
        // and a Tj with nothing in it.
        var page = PageShowing("\r\n\v\f", WinAnsiFont, XStringFormats.Default);

        TextOperators.ShowTextOperators(page).Should().BeEmpty();
    }

    [Fact]
    public void ATabInAUnicodeRunIsDrawnAsASpaceToo()
    {
        // The filtering happens before the font.Unicode branch, so the Identity-H path gets it
        // for the same one call. Read here as a word spacing rather than as a literal: Tw cannot
        // reach a two-byte code, so the renderer breaks the run at every space and moves the pen
        // by hand - and a tab that survived unfiltered would be one glyph and no break.
        var format = XStringFormats.Default;
        format.WordSpacing = 4;

        var page = PageShowing("Hand\tgloves", UnicodeFont, format);

        // Two pieces, split at the space the tab became.
        TextOperators.TjRunCounts(page).Should().Equal(2);
        TextOperators.TjAdjustments(page).Should().ContainSingle();
    }

    [Fact]
    public void AWinAnsiStringIsPlacedByTheWidthMeasureStringReportsForTheCharactersItDraws()
    {
        // The assertion this whole change exists to make possible. Far alignment places the
        // origin at rect.Right - MeasureString(s).Width, so the gap between the right edge and
        // where the run actually starts is exactly the width that was measured. Before this,
        // DrawString measured the unfiltered string and then drew a different one.
        const string text = "Hand\tgloves";
        var font = WinAnsiFont;
        var format = XStringFormats.Default;
        format.Alignment = XStringAlignment.Far;

        var rect = new XRect(20, 40, 300, 0);
        double measured = 0;
        var page = PageShowing(gfx =>
        {
            measured = gfx.MeasureString(text, font, format).Width;
            gfx.DrawString(text, font, XBrushes.Black, rect, format);
        });

        var shown = TextOperators.ShownWithPositions(page);
        shown.Should().ContainSingle();
        shown[0].Text.Should().Be("Hand gloves");
        (rect.Right - shown[0].X).Should().BeApproximately(measured, StreamPrecision);
    }

    [Fact]
    public void ALineFeedIsAbsorbedByDrawStringWhileMeasureStringStillReportsTwoLines()
    {
        // The one disagreement this change does not close, pinned so that it cannot go silent a
        // second time. MeasureString splits on \n and reports the height of two lines;
        // DrawString draws one line with the newline dropped rather than boxed. A future change
        // that makes DrawString split on \n as well is meant to fail here, on purpose.
        const string text = "A newline\nbecomes";
        var font = WinAnsiFont;

        double twoLines = 0, oneLine = 0;
        var page = PageShowing(gfx =>
        {
            twoLines = gfx.MeasureString(text, font, XStringFormats.Default).Height;
            oneLine = gfx.MeasureString("A newline", font, XStringFormats.Default).Height;
            gfx.DrawString(text, font, XBrushes.Black, 20, 40, XStringFormats.Default);
        });

        twoLines.Should().BeGreaterThan(oneLine);

        // One run, both words on it, and no line feed anywhere in what was written.
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.Tj);
        TextOperators.ShownStrings(page).Should().Equal("A newlinebecomes");
    }
}
