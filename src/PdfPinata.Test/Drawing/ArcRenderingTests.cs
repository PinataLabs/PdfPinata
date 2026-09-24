using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    // No sweep at all, off a quadrant edge.
    [InlineData(45, 0)]
    [InlineData(359, 0)]
    [InlineData(-45, 0)]
    [InlineData(405, 0)]
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

    /// <summary>
    ///   An arc with no sweep is a single curve that never leaves the point it starts from.
    /// </summary>
    /// <remarks>
    ///   Off a quadrant edge its control points used to be 0/0, and the writer refused the NaN
    ///   (#130). On an edge it was cut as though it ran from one quadrant into the next, so it went
    ///   the whole way round instead; and from exactly 360, or -360, the quadrant it ends in came out
    ///   as 4, which a walk through 0 to 3 never reaches, so it never returned (#129). A loop like
    ///   that hangs the test host rather than failing a test, so this runs where a <c>Timeout</c>
    ///   can interrupt it.
    /// </remarks>
    [Theory(Timeout = 30000)]
    [InlineData(45)]
    [InlineData(359)]
    [InlineData(-45)]
    [InlineData(405)]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(360)]
    [InlineData(-360)]
    public async Task DrawArcWithNoSweepDrawsOneDegenerateCurveAtTheStart(double startAngle)
    {
        var content = await Interruptibly.Run(() =>
            ContentOf(gfx => gfx.DrawArc(XPens.Black, 10, 20, 100, 60, startAngle, 0)));

        content.Should().NotContain("NaN");
        ShouldBeOneCurveThatStaysAtItsStart(PathConstruction(content));
    }

    [Theory(Timeout = 30000)]
    [InlineData(-360)]
    [InlineData(359)]
    [InlineData(45)]
    [InlineData(0)]
    public async Task AnArcWithNoSweepAddedToAPathIsDrawnAndSaved(double startAngle)
    {
        var (content, saved) = await Interruptibly.Run(() =>
        {
            var document = new PdfDocument();
            document.Options.CompressContentStreams = false;
            var page = document.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                var path = new XGraphicsPath();
                path.AddArc(10, 20, 100, 60, startAngle, 0);
                gfx.DrawPath(XPens.Black, path);
            }

            using var stream = new MemoryStream();
            document.Save(stream);
            return (Encoding.Latin1.GetString(PageContent.Of(page)), stream.Length);
        });

        content.Should().NotContain("NaN");
        ShouldBeOneCurveThatStaysAtItsStart(PathConstruction(content));
        saved.Should().BePositive();
    }

    [Fact]
    public void AnArcSweptFromAWholeTurnIsStillDrawnFromZero()
    {
        // Only a zero sweep from 360 changed: one swept forwards from there was always drawn from 0.
        var fromAWholeTurn = ContentOf(gfx => gfx.DrawArc(XPens.Black, 10, 20, 100, 60, 360, 90));
        var fromZero = ContentOf(gfx => gfx.DrawArc(XPens.Black, 10, 20, 100, 60, 0, 90));

        PathConstruction(fromAWholeTurn).Should().Be("110 792 m\n110 775.431 87.614 762 60 762 c\n");
        PathConstruction(fromAWholeTurn).Should().Be(PathConstruction(fromZero));
    }

    private static void ShouldBeOneCurveThatStaysAtItsStart(string construction)
    {
        construction.Should().MatchRegex(@"^[-0-9. ]+ m\n[-0-9. ]+ c\n$");

        var numbers = NumbersOf(construction);
        for (var idx = 2; idx < numbers.Length; idx += 2)
        {
            numbers[idx].Should().BeApproximately(numbers[0], 0.001);
            numbers[idx + 1].Should().BeApproximately(numbers[1], 0.001);
        }
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
