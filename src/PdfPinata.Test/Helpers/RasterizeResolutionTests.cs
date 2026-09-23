using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   What <see cref="PdfHelper.Rasterize" /> decides to draw a document at.
/// </summary>
/// <remarks>
///   <para>
///   The reference images every golden-image test compares against were made at 300 dpi, so a
///   change here that quietly lowered the resolution of an ordinary page would not fail with a
///   wrong number - it would fail as dozens of unrelated image comparisons drifting at once. These
///   pin the boundary instead: an ordinary page is still drawn at exactly 300 dpi, and only a page
///   over the limit is reduced.
///   </para>
///   <para>
///   No page is rasterized here. The decision is arithmetic over the page sizes, so it can be
///   asked for directly, and these run in milliseconds rather than the seconds a drawing costs.
///   </para>
/// </remarks>
public class RasterizeResolutionTests
{
    /// <summary>
    ///   A square page of exactly <see cref="PdfHelper.MaxPixelsPerDocument" /> pixels at full
    ///   resolution. 300 dpi over 72 points to the inch is 25/6 pixels per point, so a page of
    ///   960 points square comes to 4000 pixels square, which is 16 million of them.
    /// </summary>
    private const double PointsAtExactlyTheLimit = 960;

    private static PdfDocument DocumentOf(params (double Width, double Height)[] pages)
    {
        var document = new PdfDocument();

        foreach (var size in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(size.Width);
            page.Height = XUnit.FromPoint(size.Height);
        }

        return document;
    }

    [Fact]
    public void AnOrdinaryPageIsDrawnAtFullResolution()
    {
        // A4, which is what almost every page in these tests is.
        PdfHelper.ResolutionFor(DocumentOf((595.276, 841.89))).Should().Be(300);
    }

    /// <summary>
    ///   The limit is a page that may be drawn, not one that may not.
    /// </summary>
    [Fact]
    public void APageExactlyOnTheLimitIsDrawnAtFullResolution()
    {
        var document = DocumentOf((PointsAtExactlyTheLimit, PointsAtExactlyTheLimit));

        PdfHelper.PixelsIn(document, PdfHelper.ResolutionFor(document))
            .Should().Be(PdfHelper.MaxPixelsPerDocument, "the page was chosen to land exactly on it");
        PdfHelper.ResolutionFor(document).Should().Be(300);
    }

    /// <summary>
    ///   Twice the length of a side is four times the pixels, so it takes half the resolution to
    ///   come back to the limit.
    /// </summary>
    [Fact]
    public void APageOverTheLimitIsDrawnAtALowerResolution()
    {
        var document = DocumentOf((PointsAtExactlyTheLimit * 2, PointsAtExactlyTheLimit * 2));

        PdfHelper.ResolutionFor(document).Should().Be(150);
        PdfHelper.PixelsIn(document, 150).Should().BeLessThanOrEqualTo(PdfHelper.MaxPixelsPerDocument);
    }

    /// <summary>
    ///   One resolution is chosen for the whole document, so it has to be the one that suits the
    ///   page that needs it most. A document is drawn in one call and its pages are compared with
    ///   each other, so they cannot be drawn at sizes that do not correspond.
    /// </summary>
    [Fact]
    public void OneOversizedPageBringsTheWholeDocumentDown()
    {
        var oversized = (PointsAtExactlyTheLimit * 2, PointsAtExactlyTheLimit * 2);

        // Four times the limit in that page alone, plus two A4s beside it.
        PdfHelper.ResolutionFor(DocumentOf((595.276, 841.89), oversized, (595.276, 841.89)))
            .Should().BeLessThan(150, "the big page alone is over the limit, and it is not alone");
    }

    [Fact]
    public void ManyOrdinaryPagesCountTowardsTheLimitToo()
    {
        // The gap the per-page limit left. Every one of these is far inside what a single page may
        // be, and Rasterize holds all of them at once - which is how test.pdf, four pages of 9.7
        // megapixels, came to ask for 38.7 and took the test host with it.
        var a4 = (595.276, 841.89);
        var document = DocumentOf([..Enumerable.Repeat(a4, 12)]);

        var resolution = PdfHelper.ResolutionFor(document);

        resolution.Should().BeLessThan(300);
        PdfHelper.PixelsIn(document, resolution)
            .Should().BeLessThanOrEqualTo(PdfHelper.MaxPixelsPerDocument);
    }

    [Fact]
    public void ADocumentTooBigToDrawEvenAtOneDotPerInchIsRefused()
    {
        // The largest page a reader will accept is 14400 points a side, and 401 of them come to
        // more than the limit at 1 dpi. Absurd, and reachable only now that every page counts -
        // which is why it is refused out loud rather than floored at 1 dpi and drawn over the
        // limit anyway.
        var biggest = (14400.0, 14400.0);
        var document = DocumentOf([..Enumerable.Repeat(biggest, 401)]);

        var resolve = () => PdfHelper.ResolutionFor(document);

        resolve.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    ///   The page this limit exists for. FamilyTree.pdf is 58 inches wide by 23, which at 300 dpi
    ///   is 122 million pixels and about 940MB to draw - enough to end the test host, because
    ///   Ghostscript is loaded into it rather than run as a command. See
    ///   docs/specs/test-host-crash-investigation.md.
    /// </summary>
    [Fact]
    public void TheOversizedAssetIsBroughtUnderTheLimit()
    {
        // Qualified: the test assembly has a PdfReader of its own that would be found first.
        var document = global::PdfPinata.Pdf.IO.PdfReader.Open(
            PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"), PdfDocumentOpenMode.Import);

        PdfHelper.PixelsIn(document, 300)
            .Should().BeGreaterThan(100e6, "this is the page the limit is here for");

        var resolution = PdfHelper.ResolutionFor(document);

        resolution.Should().Be(108);
        PdfHelper.PixelsIn(document, resolution)
            .Should().BeLessThanOrEqualTo(PdfHelper.MaxPixelsPerDocument);
    }
}
