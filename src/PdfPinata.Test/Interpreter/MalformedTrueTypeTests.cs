using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Interpreter.EmbeddedFonts;
using static PdfPinata.Test.Interpreter.TrueTypeFontBuilder;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// An embedded TrueType font program that is broken costs the glyphs it cannot give and nothing
/// else: the page is drawn, each glyph lost is reported, and no font makes the render run away.
/// </summary>
/// <remarks>
/// Each test draws on a thread of its own and is bounded, because a decoder that follows a
/// composite glyph into itself does not fail: it never ends.
/// </remarks>
public class MalformedTrueTypeTests
{
    [Test, Timeout(30000)]
    public async Task AGlyphCutOffByTheEndOfTheFileIsReportedAndTheGlyphsBeforeItAreDrawn()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        builder.AddBar(200);
        var program = builder.Build();

        // The font program ends with the glyf table, and its last bytes are the second bar's y
        // coordinates and the padding after them.
        var truncated = program.AsSpan(0, program.Length - 6).ToArray();

        var drawn = await Interruptibly.Run(() => Draw("<00010002>", truncated));

        drawn.Device.Fills.Select(fill => Bounds(fill.Path).Width).Should().Equal(0.1);
        MissingGlyph(drawn).Message.Should().Contain("outside the 'glyf' table");
    }

    [Test, Timeout(30000)]
    public async Task AGlyphWhosePointsRunPastItsOwnDataIsReported()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);

        // One contour said to end at point 200, and the data for none of them.
        builder.Add([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 200, 0, 0]);
        var program = builder.Build();

        var drawn = await Interruptibly.Run(() => Draw("<00010002>", program));

        drawn.Device.Fills.Select(fill => Bounds(fill.Path).Width).Should().Equal(0.1);
        MissingGlyph(drawn).Message.Should().Contain("ends inside a table");
    }

    [Test, Timeout(30000)]
    public async Task AGlyphThatLocaPutsPastTheEndOfGlyfIsReported()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        builder.AddBar(200);
        var program = builder.Build(locaEnd: glyph => glyph == 2 ? 0x7FFFFFF0u : null);

        var drawn = await Interruptibly.Run(() => Draw("<00010002>", program));

        drawn.Device.Fills.Select(fill => Bounds(fill.Path).Width).Should().Equal(0.1);
        MissingGlyph(drawn).Message.Should().Contain("outside the 'glyf' table");
    }

    [Test, Timeout(30000)]
    public async Task ACompositeGlyphBuiltOfItselfIsReportedRatherThanFollowedForever()
    {
        var builder = new TrueTypeFontBuilder();
        var bar = builder.AddBar(100);
        var itself = builder.AddEmpty();
        builder.Replace(itself, Composite(new Component(bar), new Component(itself)));
        var program = builder.Build();

        var drawn = await Interruptibly.Run(() => Draw("<0002>", program));

        drawn.Device.Fills.Should().BeEmpty();
        MissingGlyph(drawn).Message.Should().Contain("deep");
    }

    [Test, Timeout(30000)]
    public async Task CompositesThatEachUseTheOneBelowSeveralTimesAreCutOffBeforeTheyMultiply()
    {
        // Seven levels, each of four of the one below, are 16384 bars, within the depth allowed.
        var builder = new TrueTypeFontBuilder();
        var below = builder.AddBar(100);
        for (var level = 0; level < 7; level++)
            below = builder.Add(Composite(Enumerable.Repeat(new Component(below), 4).ToArray()));
        var program = builder.Build();

        var drawn = await Interruptibly.Run(() => Draw($"<{below:X4}>", program));

        drawn.Device.Fills.Should().BeEmpty();
        MissingGlyph(drawn).Message.Should().Contain("components");
    }

    [Test, Timeout(30000)]
    public async Task ACompositeOfManyReferencesToAGlyphOfManyPointsIsCutOffBeforeItIsBuilt()
    {
        // One glyph of 65,535 points in a few hundred bytes: every flag repeated, every
        // coordinate the same as the last. A thousand references to it would be 65 million points.
        var builder = new TrueTypeFontBuilder();
        var dense = builder.Add([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFE, 0, 0,
            ..Enumerable.Range(0, 257).SelectMany(_ => new byte[] { 0x39, 0xFF })]);
        var many = builder.Add(Composite(Enumerable.Repeat(new Component(dense), 1000).ToArray()));
        var program = builder.Build();

        var drawn = await Interruptibly.Run(() => Draw($"<{dense:X4}{many:X4}>", program));

        drawn.Device.Fills.Should().ContainSingle("the dense glyph is drawn on its own");
        MissingGlyph(drawn).Message.Should().Contain("points");
    }

    [Test, Timeout(30000)]
    public async Task AFontProgramThatIsNotATrueTypeFontIsReportedForEachGlyphShown()
    {
        var drawn = await Interruptibly.Run(() => Draw("<00010002>", [0, 1, 2, 3, 4, 5, 6, 7], "0 0 5 5 re f"));

        drawn.Device.Fills.Should().ContainSingle("the rectangle after the text is drawn");
        MissingGlyph(drawn).Message.Should().Contain("not a TrueType font");
        MissingGlyph(drawn).Occurrences.Should().Be(2);
    }

    [Test, Timeout(120000), NotInParallel]
    public async Task CidsThatAMapSendsToOneGlyphOfManyPointsHoldOnePathBetweenThem()
    {
        // 60,000 CIDs, each mapped to one glyph of 65,535 points by a 120 KB /CIDToGIDMap. Kept
        // once for each code, that was 1 MB of outline for each CID, and an OutOfMemoryException.
        const int cids = 60000;
        var builder = new TrueTypeFontBuilder();
        var dense = builder.Add(Dense);
        var program = builder.Build();
        var map = new byte[2 * (cids + 1)];
        var shown = new StringBuilder();
        for (var cid = 1; cid <= cids; cid++)
        {
            map[2 * cid + 1] = (byte)dense;
            shown.Append($"{cid:X4}");
        }

        var page = Drawn.PageOf($"BT /F1 10 Tf <{shown}> Tj ET",
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F1", EmbeddedFonts.Composite(document, program, map)));
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(cids);
        device.SharedPaths.Should().Be(1, "every CID draws the one glyph");
        device.UnsharedFills.Should().Be(0);
        retained.Should().BeLessThan(64L << 20, "one glyph of 65,535 points is about a megabyte");
        GC.KeepAlive(page);
    }

    [Test, Timeout(120000), NotInParallel]
    public async Task GlyphsWhoseLocaEntriesShareOneGlyphsDataAreKeptOnlyUpToTheFontsBudget()
    {
        // Every other loca entry points at one glyph of 65,535 points, so 300 glyphs are 300
        // copies of it. A font keeps 1,048,576 points of outline, which is 16 of them; the rest
        // are decoded each time they are drawn, and handed over unshared.
        const int glyphs = 300;
        var builder = new TrueTypeFontBuilder();
        var dense = builder.Add(Dense);
        for (var i = 1; i < 2 * glyphs; i++)
            builder.AddEmpty();
        var start = builder.OffsetOf(dense);
        var end = builder.OffsetOf(dense + 1);
        var program = builder.Build(glyph => glyph >= dense ? (glyph - dense) % 2 == 0 ? end : start : null);
        var shown = new StringBuilder();
        for (var i = 0; i < glyphs; i++)
            shown.Append($"{dense + 2 * i:X4}");

        var page = Drawn.PageOf($"BT /F1 10 Tf <{shown}{shown}> Tj ET",
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F1", EmbeddedFonts.Composite(document, program)));
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(2 * glyphs, "every glyph is drawn, kept or not");
        device.SharedPaths.Should().Be(16);
        device.UnsharedFills.Should().Be(2 * (glyphs - 16));
        retained.Should().BeLessThan(64L << 20, "300 glyphs kept would be over 300 MB");
        GC.KeepAlive(page);
    }

    [Test, Timeout(120000), NotInParallel]
    public async Task TextThatClipsToOneGlyphOfManyPointsManyTimesClipsToNoMoreThanALimit()
    {
        // Each glyph shown in a clip mode is copied into the clip. 60,000 of one glyph of 65,535
        // points would be four billion points from 240 KB of text.
        const int cids = 60000;
        var builder = new TrueTypeFontBuilder();
        builder.Add(Dense);
        var program = builder.Build();
        var shown = string.Concat(Enumerable.Repeat("0001", cids));

        var page = Drawn.PageOf($"BT /F1 10 Tf 7 Tr <{shown}> Tj ET 0 0 5 5 re f",
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F1", EmbeddedFonts.Composite(document, program)));
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().ContainSingle().Which.Problem.Should().Be(PageRenderProblem.LimitReached);
        device.LargestClip.Should().BeInRange(1, 1 << 22);
        device.Fills.Should().Be(1, "the rectangle after the text is drawn");
        retained.Should().BeLessThan(64L << 20);
        GC.KeepAlive(page);
    }

    /// <summary>
    /// One glyph of 65,535 points in a few hundred bytes: every flag repeated, every coordinate
    /// the same as the last.
    /// </summary>
    private static byte[] Dense =>
        [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFE, 0, 0, ..Enumerable.Range(0, 257).SelectMany(_ => new byte[] { 0x39, 0xFF })];

    /// <summary>What a render returns, and how much more memory is live after it than before.</summary>
    private static (PageRenderResult Result, long Retained) Retained(Func<PageRenderResult> render)
    {
        var before = GC.GetTotalMemory(true);
        var result = render();
        return (result, GC.GetTotalMemory(true) - before);
    }

    /// <summary>A device that keeps no path but those a font shares, and counts what it is handed.</summary>
    private sealed class PathCountingDevice : PageDevice
    {
        private readonly HashSet<PagePath> _shared = new(ReferenceEqualityComparer.Instance);

        public int Fills { get; private set; }
        public int UnsharedFills { get; private set; }
        public int SharedPaths => _shared.Count;
        public int LargestClip { get; private set; }

        public override void Save() { }
        public override void Restore() { }
        public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode) =>
            LargestClip = Math.Max(LargestClip, path.Points.Length);
        public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint) { }
        public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint) { }

        public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint)
        {
            Fills++;
            if (path.IsShared)
                _shared.Add(path);
            else
                UnsharedFills++;
        }
    }

    private static PageRenderDiagnostic MissingGlyph(Drawn drawn) =>
        drawn.Result.Diagnostics.Should().ContainSingle(d => d.Problem == PageRenderProblem.MissingGlyph).Subject;

    private static Drawn Draw(string shown, byte[] program, string after = "") =>
        Drawn.Page($"BT /F1 10 Tf {shown} Tj ET {after}",
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F1", EmbeddedFonts.Composite(document, program)));
}
