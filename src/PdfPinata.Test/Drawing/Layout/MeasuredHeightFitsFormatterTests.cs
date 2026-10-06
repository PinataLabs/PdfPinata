using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using TUnit.Core;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   Whether a rectangle as tall as <see cref="XGraphics.MeasureString(string, XFont)"/> says the
///   text is holds every line of it when <see cref="XTextFormatter"/> lays the text out.
///   empira/PDFsharp#198.
/// </summary>
/// <remarks>
///   The two compute the same height by different routes: the measure as
///   <c>(n - 1) * lineSpacing + ascender + descender</c> straight from the font units, the formatter
///   by stepping <c>y</c> down a line at a time and comparing it with the rectangle less an ascent
///   and a descent it derives from the line spacing. Equal in exact arithmetic, a rounding apart in
///   floating point, and the last line used to be refused whenever the rounding fell against it.
/// </remarks>
public sealed class MeasuredHeightFitsFormatterTests : IDisposable
{
    // One each: the tests in a class run alongside one another, and an XGraphics is not safe to share.
    private readonly XGraphics _gfx = XGraphics.FromPdfPage(new PdfDocument().AddPage());

    public void Dispose() => _gfx.Dispose();

    private static string Lines(int count) => string.Join("\n", Enumerable.Range(1, count).Select(i => "line " + i));

    private static int LinesLaidOut(XRect laidOut, XFont font) => (int)Math.Round(laidOut.Height / font.GetHeight());

    [Test]
    public void ARectangleAsTallAsTheMeasuredTextHoldsEveryLineOfIt()
    {
        var dropped = new List<string>();

        foreach (var style in new[] { XFontStyle.Regular, XFontStyle.Bold })
        {
            for (var tenths = 10; tenths <= 1000; tenths += 3)
                AddDroppedLines(dropped, style, tenths);
        }

        dropped.Should().BeEmpty();
    }

    /// <summary>
    ///   Lays out one to 25 lines at one size and style, each in a rectangle as tall as they
    ///   measure, and describes every layout that did not hold them all.
    /// </summary>
    private void AddDroppedLines(List<string> dropped, XFontStyle style, int tenths)
    {
        var font = new XFont("Arial", tenths / 10.0, style);
        for (var count = 1; count <= 25; count++)
        {
            var text = Lines(count);
            var height = _gfx.MeasureString(text, font).Height;

            var laidOut = new XTextFormatter(_gfx)
                .GetLayout(text, font, XBrushes.Black, new XRect(0, 0, 500, height));

            var lines = LinesLaidOut(laidOut, font);
            if (lines != count)
                dropped.Add($"{style} {tenths / 10.0}pt: {lines} of {count} lines in {height:R}");
        }
    }

    [Test]
    public void TheIssuesOwnSizeFitsTwoLinesInTheMeasuredHeight()
    {
        var font = new XFont("Arial", 20);
        const string text = "hello\nworld";

        var measured = _gfx.MeasureString(text, font).Height;
        var laidOut = new XTextFormatter(_gfx).GetLayout(text, font, XBrushes.Black, new XRect(200, 200, 50, measured));

        LinesLaidOut(laidOut, font).Should().Be(2);
    }

    [Test]
    public void TwiceTheOneLineHeightIsNotRoomForTwoLinesBecauseTheLineGapLiesBetweenThem()
    {
        // The issue's own arithmetic, which is what it reported and is by design: one line
        // measures ascender plus descender, and the second line starts a full line spacing -
        // ascender, descender and line gap - below the first.
        var font = new XFont("Arial", 20);
        var oneLine = _gfx.MeasureString("hello", font).Height;
        var twoLines = _gfx.MeasureString("hello\nworld", font).Height;

        twoLines.Should().BeApproximately(oneLine + font.GetHeight(), 1e-9);
        (2 * oneLine).Should().BeLessThan(twoLines);

        var laidOut = new XTextFormatter(_gfx).GetLayout("hello\nworld", font, XBrushes.Black,
            new XRect(200, 200, 50, 2 * oneLine));
        LinesLaidOut(laidOut, font).Should().Be(1);
    }

    [Test]
    public void ARectangleAHundredthOfAPointShortStillLeavesTheLastLineOut()
    {
        // The tolerance forgives rounding, not a rectangle that is really too short.
        var font = new XFont("Arial", 12);
        var text = Lines(5);
        var height = _gfx.MeasureString(text, font).Height - 0.01;

        var laidOut = new XTextFormatter(_gfx).GetLayout(text, font, XBrushes.Black, new XRect(0, 0, 500, height));

        LinesLaidOut(laidOut, font).Should().Be(4);
    }
}
