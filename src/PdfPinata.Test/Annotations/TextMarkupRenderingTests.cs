using System;
using System.Collections.Generic;
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
///   That the text markup annotations are actually drawn. The dictionary tests can only say
///   that the right keys are present; whether a viewer makes any mark from them is a different
///   question, and the one issue #342 was really asking. A /Highlight carrying a rectangle and
///   no quadrilaterals rasterizes to no coloured pixels at all, which is what these count.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class TextMarkupRenderingTests : IDisposable
{
    private const string OutDir = "Out/TextMarkupAnnotations";

    /// <summary>
    ///   Everything rasterized by one test, kept until the test is over.
    /// </summary>
    /// <remarks>
    ///   The pages are handed out for counting pixels, so they cannot be disposed of where they
    ///   are made. They cannot be left to the collector either: the bitmap behind one is
    ///   unmanaged and the collector cannot see how big it is, so a run of these tests exhausts
    ///   the process rather than provoking a collection - and the test host is killed with no
    ///   test having failed. xUnit builds a new instance of this class per test, so disposing
    ///   here frees them between tests.
    /// </remarks>
    private readonly List<MagickImageCollection> _rasterized = [];

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    /// <summary>
    ///   The line of text, and the band around it to mark up, in the space the drawing uses.
    /// </summary>
    private static readonly XRect Line = new(30, 30, 70, 16);
    private const double Baseline = 42;

    static TextMarkupRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    [GoldenImageFact]
    public void AHighlightWashesTheLineItCovers()
    {
        var page = Rasterize(new PdfHighlightAnnotation(), "highlight");

        Count(page, IsYellow).Should().BeGreaterThan(1000);
    }

    /// <summary>
    ///   The wash is drawn under the Multiply blend mode, so the text stays legible through it.
    ///   A plain fill of the same rectangle would bury the glyphs, and the count of dark pixels
    ///   is what tells the two apart.
    /// </summary>
    [GoldenImageFact]
    public void AHighlightDoesNotPaintOverTheTextItCovers()
    {
        var highlighted = Rasterize(new PdfHighlightAnnotation(), "highlight_legible");
        var plain = Rasterize(null, "plain");

        Count(highlighted, IsDark).Should().BeCloseTo(Count(plain, IsDark), 60);
    }

    [GoldenImageFact]
    public void AnUnderlineRulesALineUnderTheText()
    {
        var underlined = Rasterize(new PdfUnderlineAnnotation(), "underline");
        var plain = Rasterize(null, "plain");

        Count(underlined, IsDark).Should().BeGreaterThan(Count(plain, IsDark) + 100);
    }

    [GoldenImageFact]
    public void AStrikeOutRulesALineThroughTheText()
    {
        var page = Rasterize(new PdfStrikeOutAnnotation(), "strikeout");

        Count(page, IsRed).Should().BeGreaterThan(100);
    }

    [GoldenImageFact]
    public void ASquigglyRulesAWavyLineUnderTheText()
    {
        var page = Rasterize(new PdfSquigglyAnnotation(), "squiggly");

        Count(page, IsGreen).Should().BeGreaterThan(100);
    }

    /// <summary>
    ///   A run of text that wraps is marked up as one annotation over several quadrilaterals,
    ///   and every one of them has to be drawn.
    /// </summary>
    [GoldenImageFact]
    public void EveryQuadOfAnAnnotationIsDrawn()
    {
        var one = Rasterize(new PdfHighlightAnnotation(), "one_quad",
            (markup, page) => markup.AddQuad(page(Line)));
        var two = Rasterize(new PdfHighlightAnnotation(), "two_quads", (markup, page) =>
        {
            markup.AddQuad(page(Line));
            markup.AddQuad(page(new XRect(Line.X, Line.Y + 30, Line.Width, Line.Height)));
        });

        // Not exactly twice: the first band is over the text and loses the pixels the glyphs
        // take up, while the second is over empty page and keeps all of its own. So the second
        // band is worth a little more than the first, and the ratio sits just above two.
        var band = Count(one, IsYellow);
        Count(two, IsYellow).Should().BeGreaterThan((int)(band * 1.9)).And.BeLessThan((int)(band * 2.3));
    }

    /// <summary>
    ///   The annotation of issue #342 itself: a rectangle, a colour, and no quadrilaterals.
    ///   Before this was implemented the same dictionary rasterized to nothing whatsoever.
    /// </summary>
    [GoldenImageFact]
    public void AnAnnotationGivenOnlyARectangleIsDrawn()
    {
        var rendered = Rasterize(new PdfHighlightAnnotation(), "rectangle_only",
            (markup, page) => markup.Rectangle = new PdfRectangle(page(Line)));

        Count(rendered, IsYellow).Should().BeGreaterThan(1000);
    }

    [GoldenImageFact]
    public void OpacityThinsTheWash()
    {
        var solid = Rasterize(new PdfHighlightAnnotation(), "opaque", (markup, page) =>
        {
            markup.AddQuad(page(Line));
            markup.Opacity = 1.0;
        });
        var faint = Rasterize(new PdfHighlightAnnotation(), "faint", (markup, page) =>
        {
            markup.AddQuad(page(Line));
            markup.Opacity = 0.25;
        });

        // At a quarter opacity the yellow is diluted towards white, so it no longer answers to
        // the same test, while the fully opaque one does.
        Count(solid, IsYellow).Should().BeGreaterThan(1000);
        Count(faint, IsYellow).Should().Be(0);
    }

    private IMagickImage<byte> Rasterize(PdfTextMarkupAnnotation annotation, string name)
    {
        return Rasterize(annotation, name, (markup, page) => markup.AddQuad(page(Line)));
    }

    /// <summary>
    ///   Draws a line of text, marks it up and rasterizes the page. The arrangement is handed a
    ///   converter into the space annotations are placed in, which is measured from the bottom
    ///   left of the page rather than from the top left the drawing uses.
    /// </summary>
    private IMagickImage<byte> Rasterize(PdfTextMarkupAnnotation annotation, string name,
        Action<PdfTextMarkupAnnotation, Func<XRect, XRect>> arrange)
    {
        GlobalFontSettings.FontResolver ??= new PinnedFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString("Hello world!", new XFont("Arial", 12), XBrushes.Black,
            Line.X, Baseline, XStringFormats.Default);

        if (annotation != null)
        {
            page.Annotations.Add(annotation);
            arrange(annotation, gfx.Transformer.WorldToDefaultPage);
        }

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    private static bool IsYellow(IMagickColor<byte> c) => c.R > 200 && c.G > 200 && c.B < 120;
    private static bool IsRed(IMagickColor<byte> c) => c.R > 150 && c.G < 100 && c.B < 100;
    private static bool IsGreen(IMagickColor<byte> c) => c.G > 100 && c.R < 100 && c.B < 100;
    private static bool IsDark(IMagickColor<byte> c) => c.R < 100 && c.G < 100 && c.B < 100;

    private static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p => { var c = p.ToColor(); return c != null && match(c); });
    }
}
