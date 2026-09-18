using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   What an <see cref="XPen"/> writes into the graphics state, read back off the page.
/// </summary>
/// <remarks>
///   Two defects here, both found by drawing the demonstration app's Vectors demo and looking at
///   the panels that came out blank or identical. Neither threw, and both were invisible from the
///   pen afterwards: every property read back exactly as it had been set.
/// </remarks>
public class PenRenderingTests
{
    static PdfPage Drawn(XPen pen)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawLines(pen, new[]
            {
                new XPoint(100, 300), new XPoint(200, 100), new XPoint(300, 300),
            });
        }

        return page;
    }

    static string ContentOf(PdfPage page)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < page.Contents.Elements.Count; index++)
        {
            builder.Append(Encoding.ASCII.GetString(
                page.Contents.Elements.GetDictionary(index).Stream.UnfilteredValue));
        }

        return builder.ToString();
    }

    /// <summary>The stroking alpha every graphics state the page applies asks for.</summary>
    static IReadOnlyList<double> StrokeAlphasOn(PdfPage page)
    {
        var states = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/ExtGState");
        if (states == null)
            return Array.Empty<double>();

        return ContentOf(page).Split('\n')
            .Where(line => line.EndsWith(" gs"))
            .Select(line => states.Elements.GetDictionary(line.Substring(0, line.Length - 3)))
            .Where(state => state != null && state.Elements.ContainsKey("/CA"))
            .Select(state => state.Elements.GetReal("/CA"))
            .ToList();
    }

    /// <summary>Every miter limit the page sets, in the order it sets them.</summary>
    static IReadOnlyList<double> MiterLimitsOn(PdfPage page)
    {
        return ContentOf(page).Split('\n')
            .Where(line => line.EndsWith(" M"))
            .Select(line => double.Parse(line.AsSpan(0, line.Length - 2),
                System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    // ----- a pen built from a brush -----

    [Fact]
    public void APenMadeFromAGradientBrushStrokesWithTheGradient()
    {
        // XPen's brush constructor never sets a Color, so pen.Color is XColor.Empty - whose alpha
        // is zero. That alpha reached the stroking graphics state and painted the stroke perfectly
        // transparent, so a pen made from a brush drew a correctly-built pattern that nobody could
        // see. Every property of the pen read back exactly as it had been set.
        var brush = new XLinearGradientBrush(
            new XRect(100, 100, 200, 200), XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal);

        var page = Drawn(new XPen(brush, 6));

        ContentOf(page).Should().Contain("/Pattern CS").And.Contain("SCN");
        StrokeAlphasOn(page).Should().NotContain(0, "a stroke with no alpha is a stroke nobody sees");
    }

    [Fact]
    public void APenMadeFromASolidBrushStrokesInThatBrushesColour()
    {
        // A solid brush is a colour and wants the ordinary stroke-colour operator. Handing it to
        // the brush path instead would set the *fill* colour and leave the stroke at whatever the
        // page had last used.
        var page = Drawn(new XPen(new XSolidBrush(XColors.Firebrick), 4));

        ContentOf(page).Should().Contain(" RG", "a solid brush names a stroking colour");
        StrokeAlphasOn(page).Should().NotContain(0);
    }

    [Fact]
    public void APenMadeFromATranslucentSolidBrushKeepsThatTranslucency()
    {
        var translucent = new XSolidBrush(XColor.FromArgb(128, 178, 34, 34));

        var page = Drawn(new XPen(translucent, 4));

        StrokeAlphasOn(page).Should().Contain(alpha => alpha > 0.4 && alpha < 0.6);
    }

    [Fact]
    public void AnOrdinaryColouredPenIsUnaffected()
    {
        // The guard on the three above: the overwhelmingly common case must be untouched.
        var page = Drawn(new XPen(XColors.MidnightBlue, 3));

        ContentOf(page).Should().Contain(" RG");
        StrokeAlphasOn(page).Should().NotContain(0);
    }

    [Fact]
    public void ASolidPenAfterAGradientPenNamesItsColourAgain()
    {
        // A gradient pen writes "/Pattern CS" and an "SCN", which replaces the stroking colour
        // space. The colour remembered for the stroke was the black stood in for the pattern, so
        // the next XPens.Black stroke found its colour already realized, wrote no "RG", and drew
        // in the pattern the gradient had left behind - a black line coming out as a gradient.
        var brush = new XLinearGradientBrush(
            new XRect(100, 100, 200, 200), XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal);

        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawLine(new XPen(brush, 6), 100, 100, 300, 100);
            gfx.DrawLine(new XPen(XColors.Black, 6), 100, 200, 300, 200);
        }

        var content = ContentOf(page);
        content.Should().Contain("/Pattern CS", "the gradient pen still strokes with a pattern");

        var pattern = content.IndexOf("/Pattern CS", System.StringComparison.Ordinal);
        content.IndexOf(" RG", pattern, System.StringComparison.Ordinal)
            .Should().BeGreaterThan(-1, "the solid pen after it has to name its colour again");
    }

    // ----- the miter limit -----

    [Fact]
    public void APenThatMitresItsJoinsWritesItsMiterLimit()
    {
        var page = Drawn(new XPen(XColors.Black, 6) { LineJoin = XLineJoin.Miter, MiterLimit = 3 });

        MiterLimitsOn(page).Should().Equal(3);
    }

    [Fact]
    public void TheMiterLimitIsWrittenWhateverTheLineCapIs()
    {
        // The guard tested _realizedLineCap against a value of XLineJoin. The two agreed only
        // because XLineCap.Flat and XLineJoin.Miter are both zero, so a pen that mitred its joins
        // and rounded its ends never wrote its limit at all - and the limit is exactly the thing
        // that decides whether a sharp corner comes to a point or is cut off.
        var page = Drawn(new XPen(XColors.Black, 6)
        {
            LineJoin = XLineJoin.Miter,
            LineCap = XLineCap.Round,
            MiterLimit = 3,
        });

        MiterLimitsOn(page).Should().Equal(3);
    }

    [Fact]
    public void AMiterLimitThatIsNotAWholeNumberSurvives()
    {
        // It was cast to int on the way out, so 1.5 - an entirely ordinary limit - was written as
        // 1, which bevels every join that is not perfectly straight.
        var page = Drawn(new XPen(XColors.Black, 6) { LineJoin = XLineJoin.Miter, MiterLimit = 1.5 });

        MiterLimitsOn(page).Should().HaveCount(1);
        MiterLimitsOn(page)[0].Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void APenThatDoesNotMitreWritesNoMiterLimit()
    {
        var page = Drawn(new XPen(XColors.Black, 6) { LineJoin = XLineJoin.Round, MiterLimit = 3 });

        MiterLimitsOn(page).Should().BeEmpty();
    }
}
