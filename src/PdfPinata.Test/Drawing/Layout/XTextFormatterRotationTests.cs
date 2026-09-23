using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   <see cref="XTextFormatter.Rotation"/> turns the whole block of text about the top left corner
///   of its layout rectangle. Checked by rasterizing, because which way round a positive angle
///   turns is a sign in a transform and would read just as plausibly either way.
/// </summary>
[Collection(RasterizingCollection.Name)]
public class XTextFormatterRotationTests
{
    private const double PageSide = 200;
    private const double PixelsPerPoint = 300.0 / 72.0;

    /// <summary>The corner the text is turned about, well inside the page so it can turn any way.</summary>
    private static XRect Layout => new(100, 100, 90, 40);

    private static XFont Font => new("Arial", 14, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    private static List<(int X, int Y)> InkOf(double rotation)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = PageSide;
        page.Height = PageSide;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var formatter = new XTextFormatter(gfx) { Rotation = rotation };
            formatter.DrawString("Handles", Font, XBrushes.Black, Layout);
        }

        using var output = PdfHelper.Rasterize(document);
        var ink = PageInk.DarkPixelsOf(output.ImageCollection[0]);
        ink.Should().NotBeEmpty("the page should have text drawn on it");
        return ink;
    }

    /// <summary>The pixel row of the corner the text turns about.</summary>
    private static double OriginRow => Layout.Y * PixelsPerPoint;

    /// <summary>The pixel column of the corner the text turns about.</summary>
    private static double OriginColumn => Layout.X * PixelsPerPoint;

    [GoldenImageFact]
    public void TextThatIsNotTurnedRunsAcrossThePage()
    {
        var ink = InkOf(0);

        (ink.Max(p => p.X) - ink.Min(p => p.X)).Should()
            .BeGreaterThan(ink.Max(p => p.Y) - ink.Min(p => p.Y));
    }

    [GoldenImageFact]
    public void AQuarterTurnStandsTheTextOnEnd()
    {
        var ink = InkOf(90);

        (ink.Max(p => p.Y) - ink.Min(p => p.Y)).Should()
            .BeGreaterThan(ink.Max(p => p.X) - ink.Min(p => p.X));
    }

    [GoldenImageFact]
    public void APositiveAngleTurnsTheTextAnticlockwise()
    {
        // Unturned the text runs right from the corner and hangs below it. Turned a quarter
        // anticlockwise it should run *up* from that same corner instead.
        var ink = InkOf(90);

        var above = ink.Count(p => p.Y < OriginRow);
        var below = ink.Count(p => p.Y > OriginRow);

        above.Should().BeGreaterThan(below * 4);
    }

    [GoldenImageFact]
    public void ANegativeAngleTurnsTheTextClockwise()
    {
        var ink = InkOf(-90);

        var above = ink.Count(p => p.Y < OriginRow);
        var below = ink.Count(p => p.Y > OriginRow);

        below.Should().BeGreaterThan(above * 4);
    }

    [GoldenImageFact]
    public void TheTextIsTurnedAboutTheCornerItStartsFrom()
    {
        // A half turn puts the text on the far side of the corner in both directions, which it
        // could not do if it were turned about the middle of the rectangle or of the page.
        var straight = InkOf(0);
        var turned = InkOf(180);

        straight.Count(p => p.X > OriginColumn).Should().BeGreaterThan(straight.Count / 2);
        turned.Count(p => p.X < OriginColumn).Should().BeGreaterThan(turned.Count / 2);
    }
}
