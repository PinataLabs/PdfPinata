using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="TextStateOperatorTests"/> checks that Tc, Tw and Tz are written and what they are
///   given. These check that they do something: the page is rasterized and the ink measured.
///   <para>
///   Worth the Ghostscript dependency for one reason above the others. The word spacing a Unicode
///   font gets is drawn by hand, as numbers inside a TJ array, and a number there is
///   <em>subtracted</em> from the horizontal position - so the number that opens a gap is a
///   negative one. That sign is read off the specification and is exactly the kind of thing that
///   is easy to have backwards; a test that only checks the number it wrote would agree with
///   itself either way.
///   </para>
/// </summary>
[Collection(RasterizingCollection.Name)]
public class TextStateRenderingTests
{
    private const double FontSize = 24;
    private const double PageWidth = 400;
    private const double PageHeight = 80;

    /// <summary>Rasterization is at 300 dpi, and PDF measures in 72nds of an inch.</summary>
    private const double PixelsPerPoint = 300.0 / 72.0;

    private static XFont WinAnsiFont => new("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    private static XFont UnicodeFont => new("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.UnicodeDefault);

    /// <summary>
    ///   Every inked pixel of the page, as (x, y) in pixels from the top left.
    /// </summary>
    private static List<(int X, int Y)> InkOf(string text, XFont font, XStringFormat format, double size = FontSize)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = PageWidth;
        page.Height = PageHeight;

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString(text, new XFont(font.Name, size, font.Style, font.PdfOptions),
                XBrushes.Black, 20, 55, format);

        // Freed as soon as the pixels have been read out of it. A rasterized page holds unmanaged
        // memory, and these tests make more of them than anything else in the suite.
        using var output = PdfHelper.Rasterize(document);

        var inked = PageInk.DarkPixelsOf(output.ImageCollection[0]);
        inked.Should().NotBeEmpty("the page should have text drawn on it");
        return inked;
    }

    /// <summary>
    ///   How wide the inked part of the page is, in points. Not the measured width of the string:
    ///   the trailing spacing after the last glyph moves the pen without marking the page, so this
    ///   is one gap short of what MeasureString answers, by design.
    /// </summary>
    private static double InkedWidthOf(string text, XFont font, XStringFormat format)
    {
        var inked = InkOf(text, font, format);
        return (inked.Max(p => p.X) - inked.Min(p => p.X)) / PixelsPerPoint;
    }

    /// <summary>
    ///   How far up the page the highest ink sits, in points from the bottom of the page. Larger
    ///   is higher, whatever the image's own y direction happens to be.
    /// </summary>
    private static double InkedTopOf(string text, XFont font, XStringFormat format)
    {
        // Image y runs down from the top, so the smallest y is the highest ink.
        return PageHeight - InkOf(text, font, format).Min(p => p.Y) / PixelsPerPoint;
    }

    /// <summary>
    ///   How far the ink leans, in points: how much further right the top of the glyphs sits than
    ///   the bottom. Positive leans right, as an italic does.
    /// </summary>
    private static double LeanOf(string text, XFont font, XStringFormat format)
    {
        // A tall glyph at a large size, so that top and bottom are far enough apart to measure.
        var inked = InkOf(text, font, format, 48);

        int top = inked.Min(p => p.Y), bottom = inked.Max(p => p.Y);
        var band = Math.Max(1, (bottom - top) / 6);

        var meanTop = inked.Where(p => p.Y <= top + band).Average(p => (double)p.X);
        var meanBottom = inked.Where(p => p.Y >= bottom - band).Average(p => (double)p.X);
        return (meanTop - meanBottom) / PixelsPerPoint;
    }

    [GoldenImageFact]
    public void CharacterSpacingPushesTheGlyphsApartOnThePage()
    {
        const double spacing = 4;

        var plain = InkedWidthOf("abc", WinAnsiFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.CharacterSpacing = spacing;
        var spaced = InkedWidthOf("abc", WinAnsiFont, format);

        // Three glyphs, so two gaps between them. The spacing after the last one moves the pen
        // past the end and leaves no ink to find.
        (spaced - plain).Should().BeApproximately(2 * spacing, 1.5);
    }

    [GoldenImageFact]
    public void WordSpacingPushesTheWordsApartForAFontEncodedAsWinAnsi()
    {
        const double spacing = 6;

        var plain = InkedWidthOf("a b c", WinAnsiFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.WordSpacing = spacing;
        var spaced = InkedWidthOf("a b c", WinAnsiFont, format);

        // Two spaces, each with ink after it, so the run grows by both.
        (spaced - plain).Should().BeApproximately(2 * spacing, 1.5);
    }

    [GoldenImageFact]
    public void WordSpacingPushesTheWordsApartForAFontEncodedAsUnicode()
    {
        const double spacing = 6;

        var plain = InkedWidthOf("a b c", UnicodeFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.WordSpacing = spacing;
        var spaced = InkedWidthOf("a b c", UnicodeFont, format);

        // The one that goes through the TJ array. Apart, not together - a sign the wrong way
        // round would bring the words closer and this would come out negative.
        (spaced - plain).Should().BeApproximately(2 * spacing, 1.5);
    }

    [GoldenImageFact]
    public void TheTwoEncodingsSpaceTheirWordsOutTheSameAmount()
    {
        var format = XStringFormats.Default;
        format.WordSpacing = 6;

        // Tw for one, a TJ array for the other, and the reader must not be able to tell.
        var winAnsi = InkedWidthOf("a b c", WinAnsiFont, format);
        var unicode = InkedWidthOf("a b c", UnicodeFont, format);

        unicode.Should().BeApproximately(winAnsi, 1.5);
    }

    [GoldenImageFact]
    public void HorizontalScalingSquashesTheTextOnThePage()
    {
        var plain = InkedWidthOf("abcdef", WinAnsiFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.HorizontalScaling = 50;
        var squashed = InkedWidthOf("abcdef", WinAnsiFont, format);

        squashed.Should().BeApproximately(plain / 2, 1.5);
    }

    [GoldenImageFact]
    public void TextRiseLiftsTheTextUpThePage()
    {
        const double rise = 10;

        var level = InkedTopOf("Hxy", WinAnsiFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.TextRise = rise;
        var raised = InkedTopOf("Hxy", WinAnsiFont, format);

        // Up, not down. The rest of the library measures y downwards, so the sign this needs
        // depends on a page direction, and getting it backwards would read just as plausibly.
        (raised - level).Should().BeApproximately(rise, 1.5);
    }

    [GoldenImageFact]
    public void ANegativeTextRiseDropsTheTextDownThePage()
    {
        var level = InkedTopOf("Hxy", WinAnsiFont, XStringFormats.Default);

        var format = XStringFormats.Default;
        format.TextRise = -10;
        var lowered = InkedTopOf("Hxy", WinAnsiFont, format);

        (lowered - level).Should().BeApproximately(-10, 1.5);
    }

    [GoldenImageFact]
    public void AnObliqueAngleLeansTheTextToTheRight()
    {
        var upright = LeanOf("H", WinAnsiFont, XStringFormats.Default);
        upright.Should().BeApproximately(0, 1);

        var format = XStringFormats.Default;
        format.ObliqueAngle = 20;
        var leaning = LeanOf("H", WinAnsiFont, format);

        // Right, the way an italic leans. A skew of the wrong sign leans the text backwards and
        // looks like nothing else in typography.
        leaning.Should().BeGreaterThan(upright + 5);
    }

    [GoldenImageFact]
    public void ANegativeObliqueAngleLeansTheTextToTheLeft()
    {
        var format = XStringFormats.Default;
        format.ObliqueAngle = -20;

        LeanOf("H", WinAnsiFont, format).Should().BeLessThan(-5);
    }

    [GoldenImageFact]
    public void ASteeperObliqueAngleLeansFurther()
    {
        var gentle = XStringFormats.Default;
        gentle.ObliqueAngle = 10;

        var steep = XStringFormats.Default;
        steep.ObliqueAngle = 30;

        LeanOf("H", WinAnsiFont, steep).Should().BeGreaterThan(LeanOf("H", WinAnsiFont, gentle));
    }
}
