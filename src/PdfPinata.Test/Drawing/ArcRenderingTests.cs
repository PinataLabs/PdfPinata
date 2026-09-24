using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   How an arc is cut into Bézier curves, one per quadrant it passes through, read back off the
///   page.
/// </summary>
/// <remarks>
///   <see cref="XGraphics.DrawArc(XPen, double, double, double, double, double, double)"/> writes
///   its curves straight into the content stream and <see cref="XGraphicsPath.AddArc(double, double, double, double, double, double)"/>
///   builds them as path geometry. Both walk the quadrants the same way, so the one pins the other.
/// </remarks>
public class ArcRenderingTests
{
    [Theory]
    // Within one quadrant: a single curve.
    [InlineData(30, 45, "96.029 771.199 m\n88.507 766.509 78.647 763.414 67.937 762.38 c\n")]
    // Counterclockwise from exactly 0, which is started from 360 instead.
    [InlineData(0, -90, "110 792 m\n110 808.569 87.614 822 60 822 c\n")]
    // More than 270 degrees: five curves, the first and last quadrant each visited twice.
    [InlineData(-45, 300,
        "85.725 817.725 m\n100.785 812.303 110 802.538 110 792 c\n110 775.431 87.614 762 60 762 c\n" +
        "32.386 762 10 775.431 10 792 c\n10 806.73 27.824 819.281 52.063 821.62 c\n")]
    // A start angle more than a turn negative, swept backwards by more than 270 degrees.
    [InlineData(-450, -271,
        "60 822 m\n32.386 822 10 808.569 10 792 c\n10 775.431 32.386 762 60 762 c\n" +
        "87.614 762 110 775.431 110 792 c\n110 792.291 109.993 792.582 109.979 792.872 c\n")]
    public void DrawArcWritesOneCurvePerQuadrant(double startAngle, double sweepAngle, string expected)
    {
        var content = ContentOf(gfx => gfx.DrawArc(XPens.Black, 10, 20, 100, 60, startAngle, sweepAngle));

        PathConstruction(content).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(30, 45)]
    [InlineData(90, 0)]
    [InlineData(0, -90)]
    [InlineData(-45, 300)]
    [InlineData(135, -270)]
    [InlineData(359, 2)]
    [InlineData(360, -45)]
    [InlineData(405, 300)]
    [InlineData(-450, -271)]
    [InlineData(720, -350)]
    // Nothing of a whole turn or more: XGraphics.DrawArc draws that as an ellipse instead.
    public void DrawArcAndAddArcCutTheArcTheSameWay(double startAngle, double sweepAngle)
    {
        var drawn = ContentOf(gfx => gfx.DrawArc(XPens.Black, 10, 20, 100, 60, startAngle, sweepAngle));
        var added = ContentOf(gfx =>
        {
            var path = new XGraphicsPath();
            path.AddArc(10, 20, 100, 60, startAngle, sweepAngle);
            gfx.DrawPath(XPens.Black, path);
        });

        var drawnNumbers = NumbersOf(PathConstruction(drawn));
        var addedNumbers = NumbersOf(PathConstruction(added));

        // The path is written to four places and the arc to three, so they agree to the third.
        addedNumbers.Length.Should().Be(drawnNumbers.Length);
        for (var idx = 0; idx < drawnNumbers.Length; idx++)
            addedNumbers[idx].Should().BeApproximately(drawnNumbers[idx], 0.001);
    }

    private static string ContentOf(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = false;
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        return Encoding.Latin1.GetString(PageContent.Of(page));
    }

    /// <summary>The lines that move to, draw lines and draw curves, in the order written.</summary>
    private static string PathConstruction(string content)
    {
        var builder = new StringBuilder();
        foreach (var line in content.Split('\n').Where(line => Regex.IsMatch(line, @"^[-0-9. ]+ [mlc]$")))
            builder.Append(line).Append('\n');
        return builder.ToString();
    }

    private static double[] NumbersOf(string operators)
    {
        return Regex.Matches(operators, @"-?[0-9]+(\.[0-9]+)?")
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();
    }
}
