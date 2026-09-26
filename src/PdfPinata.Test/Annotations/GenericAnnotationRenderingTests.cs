using System;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   That an annotation of a subtype this library has no class for is actually drawn.
/// </summary>
/// <remarks>
///   <para>
///     The dictionary tests can only say the right keys are there. Whether a reader makes any mark
///     from them is the question that matters, and for <c>/Square</c>, <c>/Circle</c>, <c>/Line</c>
///     and <c>/FreeText</c> the answer turns entirely on <c>/AP</c>: a reader draws those from
///     their appearance stream and from nothing else, so one carrying only a <c>/Rect</c> covers
///     its rectangle in nothing at all.
///   </para>
///   <para>
///     That is the same trap <c>text-markup-annotations.md</c> records for <c>/Highlight</c>, where
///     an annotation of the right subtype without <c>/QuadPoints</c> rasterized to no coloured
///     pixels. These count pixels for the same reason.
///   </para>
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class GenericAnnotationRenderingTests : IDisposable
{
    private const string OutDir = "Out/GenericAnnotations";

    /// <summary>
    ///   Kept until the test is over: the pages are handed out for counting, and the bitmap
    ///   behind one is unmanaged, so leaving them to the collector exhausts the test host.
    /// </summary>
    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    static GenericAnnotationRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    private static readonly XRect Where = new(40, 40, 120, 60);

    [GoldenImageFact]
    public void ASquareWithAnAppearanceIsPainted()
    {
        var page = Rasterize("square-drawn", annotation =>
            annotation.SetAppearance(Filled(annotation.Owner, XColors.RoyalBlue)));

        // 120 x 60 points at the rasterizing resolution is several thousand pixels.
        Count(page, IsBlue).Should().BeGreaterThan(1000);
    }

    [GoldenImageFact]
    public void ASquareWithoutAnAppearanceIsNotPaintedAtAll()
    {
        var page = Rasterize("square-bare", _ => { });

        // The whole reason SetAppearance had to exist. A /Square carrying a rectangle and no
        // /AP is a well-formed annotation that every reader draws nothing for, so a caller who
        // could only set the subtype had no way to put a square on a page.
        Count(page, IsBlue).Should().Be(0);
        Count(page, IsAnythingButWhite).Should().Be(0);
    }

    [GoldenImageFact]
    public void OnlyTheAppearanceNamedByTheStateIsPainted()
    {
        var page = Rasterize("square-states", annotation =>
        {
            annotation.SetAppearance("/On", Filled(annotation.Owner, XColors.RoyalBlue));
            annotation.SetAppearance("/Off", new XForm(annotation.Owner, Where.Size));

            // Both are in the file; /Off is showing because it was named last, and it draws
            // nothing.
        });

        Count(page, IsAnythingButWhite).Should().Be(0);
    }

    private IMagickImage<byte> Rasterize(string name, Action<PdfGenericAnnotation> arrange)
    {
        GlobalFontSettings.FontResolver ??= new PinnedFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();

        var annotation = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(annotation);

        var gfx = XGraphics.FromPdfPage(page);
        annotation.Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(Where));

        arrange(annotation);

        return _rasterized.FirstPageOf(document, name);
    }

    private static XForm Filled(PdfDocument document, XColor colour)
    {
        var form = new XForm(document, Where.Size);
        using var gfx = XGraphics.FromForm(form);
        gfx.DrawRectangle(new XSolidBrush(colour), 0, 0, Where.Width, Where.Height);

        return form;
    }

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    private static bool IsAnythingButWhite(IMagickColor<byte> c) => c.R < 240 || c.G < 240 || c.B < 240;

    private static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }
}
