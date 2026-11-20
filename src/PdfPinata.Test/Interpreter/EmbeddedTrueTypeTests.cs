using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Interpreter.EmbeddedFonts;
using static PdfPinata.Test.Interpreter.TrueTypeFontBuilder;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Text set in a TrueType font program embedded as <c>/FontFile2</c>: which glyph each code draws,
/// and the outline that arrives at the device for it.
/// </summary>
/// <remarks>
/// An outline arrives in ems, so a glyph of Liberation Sans is its <c>glyf</c> coordinates over
/// 2048, and a bar of <see cref="TrueTypeFontBuilder"/> is its width over 1000 wide.
/// </remarks>
public class EmbeddedTrueTypeTests
{
    private const double Em = 2048;

    [Test]
    [Arguments("A", 4, 0, 1362, 1409, 2)]
    [Arguments("H", 168, 0, 1312, 1409, 1)]
    [Arguments("O", 97, -20, 1495, 1430, 2)]
    [Arguments(".", 187, 0, 382, 219, 1)]
    // Aacute is a composite glyph: A, and the accent placed 475 units across.
    [Arguments("Á", 4, 0, 1362, 1776, 3)]
    public void AGlyphOfLiberationSansArrivesWithTheBoundsAndContoursItsGlyfTableGives(
        string text, int xMin, int yMin, int xMax, int yMax, int contours)
    {
        var drawn = Liberation($"BT /F1 10 Tf ({text}) Tj ET");

        var path = drawn.Device.Fills.Single().Path;
        Bounds(path).Should().Be(Ems(xMin, yMin, xMax, yMax));
        Figures(path).Should().Be(contours);
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void AGlyphOfStraightLinesIsAMoveALineToEachOtherPointAndAClose()
    {
        var path = Liberation("BT /F1 10 Tf (H) Tj ET").Device.Fills.Single().Path;

        // H has twelve points, all on the curve.
        path.Verbs.ToArray().Should().Equal(
            new[] { PagePathVerb.MoveTo }.Concat(Enumerable.Repeat(PagePathVerb.LineTo, 11)).Append(PagePathVerb.Close));
        path.Points[0].Should().Be(new XPoint(1121 / Em, 0));
    }

    [Test]
    public void GlyphsArePlacedByTheWidthsThePdfStatesAndNotByTheFontProgramsOwn()
    {
        // Every code is 500 thousandths wide in the font dictionary. Liberation Sans's own
        // advances for these four are all different.
        var drawn = Liberation("BT /F1 10 Tf 10 20 Td (AHO.) Tj ET");

        drawn.Device.Fills.Select(fill => fill.Origin).Should().Equal(
            new XPoint(10, 80), new XPoint(15, 80), new XPoint(20, 80), new XPoint(25, 80));
    }

    [Test]
    public void ASpaceDrawsNothingAndTheTextStillAdvancesPastIt()
    {
        var drawn = Liberation("BT /F1 10 Tf 10 20 Td (A A) Tj ET");

        drawn.Device.Fills.Select(fill => fill.Origin).Should().Equal(new XPoint(10, 80), new XPoint(20, 80));
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void TheSameCodeIsTheSameSharedPathEachTimeItIsDrawn()
    {
        var fills = Liberation("BT /F1 10 Tf (AHA) Tj ET").Device.Fills.ToArray();

        fills.Should().OnlyContain(fill => fill.Path.IsShared);
        fills[2].Path.Should().BeSameAs(fills[0].Path);
    }

    [Test]
    public void DifferencesNameGlyphsThatAreFoundThroughTheUnicodeCmapOrThePostTable()
    {
        var builder = new TrueTypeFontBuilder();
        var bar100 = builder.AddBar(100);
        var bar200 = builder.AddBar(200);
        var bar300 = builder.AddBar(300);
        var bar400 = builder.AddBar(400);
        builder.Map(3, 1, new Dictionary<int, int> { [0x41] = bar100, [0xE9] = bar200, [0x20AC] = bar400 })
            .Name(null, null, null, "ornament");
        var program = builder.Build();

        var drawn = Drawn.Page("BT /F1 10 Tf (ABCDE) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Simple(document, program, 32,
                Differences(document, "/WinAnsiEncoding", 66, "/eacute", "/ornament", "/uni0041"))));

        // A is the base encoding's; B, C and D are named by /Differences, and E is the base
        // encoding's again, which the font has no glyph for.
        Widths(drawn).Should().Equal(0.1, 0.2, 0.3, 0.1);
        drawn.Problems.Should().Equal("MissingGlyph /F1");
    }

    [Test]
    public void ANonsymbolicFontWithNoEncodingIsReadInTheStandardEncoding()
    {
        var builder = new TrueTypeFontBuilder();
        var quoteRight = builder.AddBar(100);
        builder.Map(3, 1, new Dictionary<int, int> { [0x2019] = quoteRight });

        // StandardEncoding has quoteright at 0x27, where WinAnsiEncoding has quotesingle.
        var drawn = Drawn.Page("BT /F1 10 Tf (') Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Simple(document, builder.Build(), 32, null)));

        Widths(drawn).Should().Equal(0.1);
    }

    [Test]
    public void ASymbolicFontFindsItsGlyphsInTheSymbolCmapAtTheCodeOrAtF000AndAbove()
    {
        var builder = new TrueTypeFontBuilder();
        var low = builder.AddBar(100);
        var high = builder.AddBar(200);
        builder.Map(3, 0, new Dictionary<int, int> { [0x41] = low, [0xF042] = high });

        var drawn = Drawn.Page("BT /F1 10 Tf (AB) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Simple(document, builder.Build(), 4, new PdfName("/WinAnsiEncoding"))));

        Widths(drawn).Should().Equal(0.1, 0.2);
    }

    [Test]
    public void ASymbolicFontWithOnlyAMacintoshCmapLooksTheCodeUpThere()
    {
        var builder = new TrueTypeFontBuilder();
        var bar = builder.AddBar(300);
        builder.Map(1, 0, new Dictionary<int, int> { [0x61] = bar });

        var drawn = Drawn.Page("BT /F1 10 Tf (a) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Simple(document, builder.Build(), 4, null)));

        Widths(drawn).Should().Equal(0.3);
    }

    [Test]
    public void AFontWithNoCmapAtAllDrawsTheGlyphNumberedByTheCode()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        builder.AddBar(200);

        var drawn = Drawn.Page("BT /F1 10 Tf <0201> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Simple(document, builder.Build(), 4, null)));

        Widths(drawn).Should().Equal(0.2, 0.1);
    }

    [Test]
    public void ACompositeFontMapsEachCidToAGlyphThroughItsCidToGidMapStream()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        builder.AddBar(200);
        builder.AddBar(300);

        // CID 1 is glyph 3 and CID 2 is glyph 1. CID 3 lies past the end of the map.
        var drawn = Drawn.Page("BT /F1 10 Tf <000100020003> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Composite(document, builder.Build(), [0, 0, 0, 3, 0, 1])));

        Widths(drawn).Should().Equal(0.3, 0.1);
        drawn.Problems.Should().Equal("MissingGlyph /F1");
    }

    [Test]
    public void ACompositeFontWithAnIdentityMapDrawsTheGlyphNumberedByTheCid()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        builder.AddBar(200);

        var drawn = Drawn.Page("BT /F1 10 Tf <00020001> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Composite(document, builder.Build())));

        Widths(drawn).Should().Equal(0.2, 0.1);
    }

    [Test]
    public void ACompositeGlyphPlacesItsComponentsByOffsetScaleOrMatchedPoints()
    {
        var builder = new TrueTypeFontBuilder();
        var square = builder.AddRectangle(0, 0, 100, 100);
        var offset = builder.Add(Composite(new Component(square, Dx: 50, Dy: 20)));
        var scaled = builder.Add(Composite(new Component(square, Scale: 0.5)));

        // The second square's point 0, its bottom left corner, onto the first's point 2, its top
        // right corner.
        var matched = builder.Add(Composite(new Component(square), new Component(square, PointMatched: (2, 0))));
        var program = builder.Build();

        var drawn = Drawn.Page($"BT /F1 10 Tf <{offset:X4}{scaled:X4}{matched:X4}> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", Composite(document, program)));

        var paths = drawn.Device.Fills.Select(fill => fill.Path).ToArray();
        Bounds(paths[0]).Should().Be(new Box(0.05, 0.02, 0.15, 0.12));
        Bounds(paths[1]).Should().Be(new Box(0, 0, 0.05, 0.05));
        Bounds(paths[2]).Should().Be(new Box(0, 0, 0.2, 0.2));
        Figures(paths[2]).Should().Be(2);
    }

    [Test]
    public void TextThisLibraryWritesInAnIdentityHSubsetIsDrawnBackGlyphForGlyph()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Hello", new XFont("Arial", 12), XBrushes.Black, 20, 40);

        var device = new RecordingDevice();
        var result = PageRendering.Render(document.Reopened().Pages[0], device);

        result.Diagnostics.Should().BeEmpty();
        var fills = device.Fills.ToArray();
        fills.Should().HaveCount(5);
        fills[3].Path.Should().BeSameAs(fills[2].Path, "both l's are one glyph");

        // Liberation Sans has Arial's widths, which XGraphics wrote as /W: H 722, e 556 and l 222.
        var origins = fills.Select(fill => fill.Origin).ToArray();
        origins.Select(origin => origin.X).Should().Equal(
            new[] { 20, 28.664, 35.336, 38, 40.664 }, (actual, expected) => Math.Abs(actual - expected) < 0.005);
        origins.Should().OnlyContain(origin => Math.Abs(origin.Y - 40) < 1e-9);

        // H, at 12 points: the glyf bounds over 2048, scaled by the type size and turned the right
        // way up on the device.
        var h = fills[0];
        h.At(168 / Em, 0).X.Should().BeApproximately(20 + 168 / Em * 12, 1e-9);
        h.At(0, 1409 / Em).Y.Should().BeApproximately(40 - 1409 / Em * 12, 1e-9);
        Bounds(h.Path).Should().Be(Ems(168, 0, 1312, 1409));
    }

    /// <summary>
    /// The width of the bar each fill draws, in ems, which is the number
    /// <see cref="TrueTypeFontBuilder.AddBar"/> was given over 1000.
    /// </summary>
    private static double[] Widths(Drawn drawn) =>
        drawn.Device.Fills.Select(fill => Math.Round(Bounds(fill.Path).Width, 9)).ToArray();

    /// <summary>A box of Liberation Sans's font units, in ems.</summary>
    private static Box Ems(int xMin, int yMin, int xMax, int yMax) =>
        new(Math.Round(xMin / Em, 9) + 0.0, Math.Round(yMin / Em, 9) + 0.0, Math.Round(xMax / Em, 9), Math.Round(yMax / Em, 9));

    private static Drawn Liberation(string content) =>
        Drawn.Page(content, (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1",
                Simple(document, LiberationSans(), 32, new PdfName("/WinAnsiEncoding"))));
}
