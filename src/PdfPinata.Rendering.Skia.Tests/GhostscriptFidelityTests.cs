using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Rendering.Skia.Tests.Helpers;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// Hand-written pages that each draw one feature, rendered here and by Ghostscript and compared.
/// </summary>
/// <remarks>
/// Each tolerance is set a little above what the comparison measured when it was written, so a
/// change that moves a page is seen. Most of what remains is the two rasterizers' anti-aliasing:
/// Ghostscript's comes from sampling each pixel 4 by 4, and Skia's from the coverage it computes.
/// Where more remains, the test says what it is.
///
/// DeviceCMYK is not compared here. Ghostscript converts it to RGB through an ICC profile, and page
/// rendering converts it by the formula ISO 32000-1 10.3.5 gives, with no colour management, so
/// the two differ by design; <c>SkiaPageRenderingTests</c> checks the colours it paints instead.
/// Text and images are not here yet, because the glyph sources and the image decoding they need
/// are other issues.
/// </remarks>
[Rasterizing]
public class GhostscriptFidelityTests
{
    private const string Circle =
        "150 75 m 150 102.6 127.6 125 100 125 c 72.4 125 50 102.6 50 75 c " +
        "50 47.4 72.4 25 100 25 c 127.6 25 150 47.4 150 75 c h ";

    [Test]
    public void FillsInDeviceRgbAndDeviceGray() =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 10 10 80 60 re f " +
                "0 0.5 0.25 rg 110 10 80 60 re f " +
                "0.5 g 10 80 80 60 re f " +
                "0.2 0.4 0.9 rg 110 80 80 60 re f"),
            nameof(FillsInDeviceRgbAndDeviceGray), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    // The hairline, width 0, lies on a boundary between two rows of pixels. Ghostscript paints one
    // of them a light grey and Skia paints both a middle grey, which is most of the share over the
    // threshold.
    [Test]
    public void StrokesWithEachCapAndJoin() =>
        Fidelity.Of(HandWritten.Page(
                "12 w 0 0 1 RG " +
                "0 J 20 130 m 60 130 l S 1 J 80 130 m 120 130 l S 2 J 140 130 m 180 130 l S " +
                "0 J 0 j 20 20 m 40 90 l 60 20 l S " +
                "1 j 80 20 m 100 90 l 120 20 l S " +
                "2 j 140 20 m 160 90 l 180 20 l S " +
                "0 w 0 0 0 RG 10 105 m 190 105 l S"),
            nameof(StrokesWithEachCapAndJoin), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.5, shareOverThreshold: 0.01);

    // A dash of no length that falls exactly on the end of a path is a dot to Ghostscript and
    // nothing to Skia, and the dashes round the closed rectangle's corners join differently.
    [Test]
    public void DashedLines() =>
        Fidelity.Of(HandWritten.Page(
                "4 w 0.6 0 0 RG " +
                "[12 6] 0 d 10 130 m 190 130 l S " +
                "[12 6] 9 d 10 110 m 190 110 l S " +
                "[9] 0 d 10 90 m 190 90 l S " +
                "1 J [0 10] 0 d 10 70 m 190 70 l S " +
                "0 J [20 5 5 5] 0 d 20 15 m 20 50 l 180 50 l 180 15 l h S"),
            nameof(DashedLines), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.5, shareOverThreshold: 0.02);

    [Test]
    public void CurvesUnderATransformThatScalesUnevenly() =>
        Fidelity.Of(HandWritten.Page(
                "q 1 0 0 0.5 0 37.5 cm 6 w 0 0.5 0 RG " + Circle + "S Q " +
                "q 0.3 0 0 0.3 10 10 cm 0.8 0.2 0 rg " + Circle + "f Q"),
            nameof(CurvesUnderATransformThatScalesUnevenly), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.75, shareOverThreshold: 0.004);

    [Test]
    public void NonzeroAndEvenOddFills() =>
        Fidelity.Of(HandWritten.Page(
                "0 0 0.7 rg " +
                "10 20 80 110 re 25 40 50 70 re f " +
                "110 20 80 110 re 125 40 50 70 re f*"),
            nameof(NonzeroAndEvenOddFills), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    [Test]
    public void ClipsNestedAndRestored() =>
        Fidelity.Of(HandWritten.Page(
                "q " + Circle + "W n " +
                "q 0 0 100 150 re W n 1 0 0 rg 0 0 200 150 re f Q " +
                "0 0 1 rg 100 0 100 150 re f Q " +
                "q 10 10 40 40 re 20 20 20 20 re W* n 0 0.6 0 rg 0 0 60 60 re f Q " +
                "0 g 160 10 30 30 re f"),
            nameof(ClipsNestedAndRestored), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1, shareOverThreshold: 0.008);

    [Test]
    public void ConstantAlpha() =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 20 20 100 80 re f /Half gs 0 0 1 rg 80 50 100 80 re f " +
                "0 1 0 RG 10 w 20 130 m 180 20 l S",
                page: (document, page) => HandWritten.ExtGState(document, page, "/Half", state =>
                {
                    state.Elements.SetReal("/ca", 0.5);
                    state.Elements.SetReal("/CA", 0.5);
                })),
            nameof(ConstantAlpha), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public void APageTurnedByRotate(int rotate) =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 10 10 60 30 re f 0 0 1 rg 150 100 40 40 re f " +
                "3 w 0 g 10 140 m 100 75 l 190 140 l S",
                page: (_, page) => page.Rotate = rotate),
            nameof(APageTurnedByRotate) + rotate, new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.6, shareOverThreshold: 0.001);

    [Test]
    public void ARegionAtAScale() =>
        Fidelity.Of(HandWritten.Page(
                "0.8 0.4 0 rg " + Circle + "f 4 w 0 0 0.5 RG 0 0 m 200 150 l S"),
            nameof(ARegionAtAScale), new PageRenderOptions { Scale = 3, Region = new XRect(40, 30, 90, 60) })
            .ShouldBeWithin(meanChannelDifference: 0.6, shareOverThreshold: 0.001);

    // Every /Rotate matrix is symmetric, so only a cm that turns or skews content tells a matrix
    // from its transpose.
    [Test]
    public void ContentTurnedAndSkewedByCm() =>
        Fidelity.Of(HandWritten.Page(
                "q 0.866 0.5 -0.5 0.866 100 20 cm 1 0 0 rg 0 0 80 30 re f 4 w 0 0 1 RG 0 40 m 70 40 l S Q " +
                "q 1 0 0.4 1 10 90 cm 0 0.6 0 rg 0 0 50 40 re f Q"),
            nameof(ContentTurnedAndSkewedByCm), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);
}
