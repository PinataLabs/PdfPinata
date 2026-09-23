using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The three things a path built from text can do that no <c>DrawString</c> overload can. Text
///   drawn with a pen, a brush or both covers stroking and filling with a solid colour, and a
///   caller who wants only that should use it. These need geometry.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class GlyphOutlineRenderingTests : IDisposable
{
    private const string OutDir = "Out/GlyphOutlines";
    private const double EmSize = 96;
    private const string Text = "PATH";

    private static readonly XRect Box = new XRect(40, 60, 500, 120);

    private readonly List<MagickImageCollection> _rasterized = [];

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static GlyphOutlineRenderingTests() => GhostscriptSetup.Configure();

    [GoldenImageFact]
    public void GlyphsCanBeFilledWithAGradient()
    {
        var page = Rasterize("gradient_text", gfx =>
        {
            var brush = new XLinearGradientBrush(Box, XColors.Red, XColors.Blue,
                XLinearGradientMode.Horizontal);
            gfx.DrawPath(brush, TextPath());
        });

        // Red at the first letter, blue at the last, which is what a gradient across the glyphs
        // means and what no DrawString overload can produce.
        var ink = InkOf(page);
        var leftmost = ink.OrderBy(pixel => pixel.X).First().Colour;
        var rightmost = ink.OrderByDescending(pixel => pixel.X).First().Colour;

        leftmost.R.Should().BeGreaterThan(leftmost.B);
        rightmost.B.Should().BeGreaterThan(rightmost.R);
    }

    [GoldenImageFact]
    public void GlyphsCanBeUsedAsAClipForAPhotograph()
    {
        var photograph = XImage.FromFile(PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg"));

        var clipped = Rasterize("clipped_text", gfx =>
        {
            gfx.IntersectClip(TextPath());
            gfx.DrawImage(photograph, Box.X, Box.Y - EmSize, Box.Width, Box.Height + EmSize);
        });

        var unclipped = Rasterize("unclipped_photograph", gfx =>
            gfx.DrawImage(photograph, Box.X, Box.Y - EmSize, Box.Width, Box.Height + EmSize));

        // The photograph shows only inside the glyphs, so far less of it survives - but some of
        // it does, which is what tells a clip apart from a page that drew nothing.
        var through = CountInk(clipped);
        var whole = CountInk(unclipped);

        through.Should().BeGreaterThan(1000);
        through.Should().BeLessThan(whole / 3);
    }

    [GoldenImageFact]
    public void AnEmptyStringDrawsNothingAtAll()
    {
        var page = Rasterize("empty_text", gfx =>
        {
            var path = new XGraphicsPath();
            path.AddString("", new XFontFamily("Arial"), XFontStyle.Regular, EmSize, Box,
                XStringFormats.TopLeft);
            gfx.DrawPath(XBrushes.Black, path);
        });

        CountInk(page).Should().Be(0);
    }

    private static XGraphicsPath TextPath()
    {
        var path = new XGraphicsPath();
        path.AddString(Text, new XFontFamily("Arial"), XFontStyle.Bold, EmSize, Box,
            XStringFormats.TopLeft);
        return path;
    }

    // ----- rasterizing and reading pixels ---------------------------------------------------------

    private IMagickImage<byte> Rasterize(string name, Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    /// <summary>Every pixel of the page that is not the paper, with where it is.</summary>
    private static IReadOnlyList<(int X, IMagickColor<byte> Colour)> InkOf(IMagickImage<byte> page)
    {
        using var pixels = page.GetPixels();
        return [..pixels
            .Select(pixel => (pixel.X, Colour: pixel.ToColor()))
            .Where(pixel => pixel.Colour != null && !IsPaper(pixel.Colour))];
    }

    private static int CountInk(IMagickImage<byte> page)
    {
        using var pixels = page.GetPixels();
        return pixels.Count(pixel =>
        {
            var colour = pixel.ToColor();
            return colour != null && !IsPaper(colour);
        });
    }

    /// <summary>White, or near enough that an anti-aliased edge counts as paper.</summary>
    private static bool IsPaper(IMagickColor<byte> colour) => colour.R > 240 && colour.G > 240 && colour.B > 240;
}
