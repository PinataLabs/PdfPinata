using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
/// How heavily a simulated bold is stroked and how far apart its glyphs are set is the font
/// resolver's choice, per face, through <see cref="FontResolverInfo.BoldSimulation"/> - empira/PDFsharp#412.
/// </summary>
/// <remarks>
/// It used to be one constant, 2% of the em for both, which is the XPS specification's figure.
/// Microsoft Word strokes at 1/35 of the em and adds no spacing at all, so a simulated bold drawn
/// here drifted further from Word's with every glyph on the line.
/// </remarks>
public class BoldSimulationTests
{
    private const double FontSize = 12;

    private const string Text = "Listing Schedule";

    private static PdfPage PageShowing(params XFont[] fonts)
    {
        return DrawnText.PageShowing(gfx =>
        {
            var y = 40.0;
            foreach (var font in fonts)
            {
                gfx.DrawString(Text, font, XBrushes.Black, 20, y);
                y += 30;
            }
        });
    }

    private static double WidthOf(string family, XFontStyle style, XPdfFontOptions options)
    {
        using var gfx = XGraphics.CreateMeasureContext(
            new XSize(600, 800), XGraphicsUnit.Point, XPageDirection.Downwards);

        return gfx.MeasureString(Text, new XFont(family, FontSize, style, options)).Width;
    }

    // ----- measuring -----------------------------------------------------------------------------

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AWordCompatibleBoldMeasuresExactlyAsWideAsTheRegularFace(bool unicode)
    {
        var options = unicode ? XPdfFontOptions.UnicodeDefault : XPdfFontOptions.WinAnsiDefault;
        // The whole of the issue: Word sets a simulated bold at the regular advances, so a line of
        // it has to wrap where the regular line does or the two documents part company.
        var regular = WidthOf(PinnedFontResolver.WordBoldFamilyName, XFontStyle.Regular, options);
        var bold = WidthOf(PinnedFontResolver.WordBoldFamilyName, XFontStyle.Bold, options);

        bold.Should().Be(regular);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ADefaultBoldStillMeasuresTwoPercentOfTheEmWiderPerGlyph(bool unicode)
    {
        var options = unicode ? XPdfFontOptions.UnicodeDefault : XPdfFontOptions.WinAnsiDefault;
        var regular = WidthOf(PinnedFontResolver.DefaultBoldFamilyName, XFontStyle.Regular, options);
        var bold = WidthOf(PinnedFontResolver.DefaultBoldFamilyName, XFontStyle.Bold, options);

        bold.Should().BeApproximately(regular + Text.Length * FontSize * 0.02, 1e-9);
    }

    // ----- drawing -------------------------------------------------------------------------------

    [Test]
    public void AWordCompatibleBoldIsStrokedAtAThirtyFifthOfTheEmAndNotSpacedOut()
    {
        var page = PageShowing(new XFont(PinnedFontResolver.WordBoldFamilyName, FontSize, XFontStyle.Bold));

        TextOperators.NumbersGivenTo(page, OpCodeName.Tr).Should().Contain(2);
        TextOperators.NumbersGivenTo(page, OpCodeName.w).Should().ContainSingle()
            .Which.Should().BeApproximately(FontSize / 35, 1e-3);
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should().OnlyContain(spacing => spacing == 0);
    }

    [Test]
    public void TwoFamiliesSimulatingBoldOverTheSameFileEachDrawTheirOwnWay()
    {
        // One file, one set of style flags, two answers: if the resolver's answers were cached by
        // file and flags alone, whichever family was asked for first would decide for both.
        var page = PageShowing(
            new XFont(PinnedFontResolver.WordBoldFamilyName, FontSize, XFontStyle.Bold),
            new XFont(PinnedFontResolver.DefaultBoldFamilyName, FontSize, XFontStyle.Bold));

        TextOperators.NumbersGivenTo(page, OpCodeName.w).Should()
            .SatisfyRespectively(
                word => word.Should().BeApproximately(FontSize / 35, 1e-3),
                @default => @default.Should().BeApproximately(FontSize * 0.02, 1e-3));
        TextOperators.NumbersGivenTo(page, OpCodeName.Tc).Should()
            .ContainSingle().Which.Should().BeApproximately(FontSize * 0.02, 1e-3);
    }

    // ----- the resolver's answer -----------------------------------------------------------------

    [Test]
    public void AnAnswerThatNamesNoSimulationSimulatesTheDefaultWay()
    {
        new FontResolverInfo("face", XStyleSimulations.BoldSimulation).BoldSimulation
            .Should().Be(XBoldSimulation.Default);
        new FontResolverInfo("face", XStyleSimulations.BoldSimulation, null).BoldSimulation
            .Should().Be(XBoldSimulation.Default);
    }

    [Test]
    public void FontResolverBaseAnswersWithTheSimulationItWasGiven()
    {
        var resolver = new Probe { BoldSimulation = XBoldSimulation.WordCompatible };
        resolver.SetupFontsFiles([PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf")]);

        var info = resolver.ResolveTypeface("Liberation Sans", true, false);

        info.MustSimulateBold.Should().BeTrue();
        info.BoldSimulation.Should().BeSameAs(XBoldSimulation.WordCompatible);
    }

    [Test]
    public void FontResolverBaseRefusesNoSimulationAtAll()
    {
        var resolver = new Probe();

        var assign = () => resolver.BoldSimulation = null;

        assign.Should().Throw<ArgumentNullException>();
    }

    // ----- the value -----------------------------------------------------------------------------

    [Test]
    [Arguments(0.0, 0.0)]
    [Arguments(-0.01, 0.0)]
    [Arguments(double.NaN, 0.0)]
    [Arguments(double.PositiveInfinity, 0.0)]
    [Arguments(0.02, -0.01)]
    [Arguments(0.02, double.NaN)]
    [Arguments(0.02, double.PositiveInfinity)]
    public void ASimulationThatCannotBeDrawnIsRefused(double strokeWidth, double characterSpacing)
    {
        // A stroke of width zero is not no stroke in PDF but the thinnest line the device draws.
        var create = () => new XBoldSimulation(strokeWidth, characterSpacing);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void TwoSimulationsDrawingTheSameAreEqual()
    {
        new XBoldSimulation(1.0 / 35, 0).Should().Be(XBoldSimulation.WordCompatible);
        new XBoldSimulation(0.02, 0.02).GetHashCode().Should().Be(XBoldSimulation.Default.GetHashCode());
        XBoldSimulation.WordCompatible.Should().NotBe(XBoldSimulation.Default);
    }

    private sealed class Probe : SkiaFontResolver
    {
        public Probe() => NullIfFontNotFound = true;
    }
}
