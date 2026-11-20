using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
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

    [Test, Timeout(300000), NotInParallel]
    public async Task FontDictionariesSharingOneFontProgramKeepNoMoreOutlineTogetherThanTheDocumentsBudget()
    {
        // 64 Type 0 fonts over one descendant, each drawing 16 glyphs of 65,535 points. Each font
        // dictionary is about 150 bytes of file; kept for each font, the outlines were 1 GiB. They
        // are kept under the font program, so every dictionary over it draws the same 16 paths.
        const int fonts = 64;
        var builder = new TrueTypeFontBuilder();
        for (var i = 0; i < 16; i++)
            builder.Add(Dense);
        var program = builder.Build();
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <000100020003000400050006000700080009000A000B000C000D000E000F0010> Tj ");
        content.Append("ET");

        var page = Drawn.PageOf(content.ToString(), (document, resources) =>
        {
            var first = EmbeddedFonts.Composite(document, program);
            Drawn.Add(document, resources, "/Font", "/F0", first);
            for (var k = 1; k < fonts; k++)
            {
                var font = new PdfDictionary(document);
                font.Elements.SetName("/Type", "/Font");
                font.Elements.SetName("/Subtype", "/Type0");
                font.Elements.SetName("/BaseFont", "/Embedded");
                font.Elements.SetName("/Encoding", "/Identity-H");
                font.Elements["/DescendantFonts"] = first.Elements["/DescendantFonts"];
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }
        });
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(16 * fonts);
        device.SharedPaths.Should().Be(16, "an outline belongs to the font program, whichever font dictionary names it");
        device.UnsharedFills.Should().Be(0);
        retained.Should().BeLessThan(48L << 20, "the budget is 18 MiB of outline");
        GC.KeepAlive(page);
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task ALongRenderForgetsTheFontsOfEarlierPagesSoEachPageKeepsItsGlyphsAndMemoryStaysBounded()
    {
        // Each page has a font of its own, as a document of per-page subsets does, whose program
        // decodes to 3 MiB and whose 16 glyphs fill the document's outline budget. Kept for the
        // document's life, 80 of them were 240 MiB of program and 1280 glyphs of 65,535 points.
        const int pages = 80;
        var document = new PdfDocument();
        for (var p = 0; p < pages; p++)
        {
            var builder = new TrueTypeFontBuilder();
            for (var i = 0; i < 16; i++)
                builder.Add(Dense);
            var padded = new byte[(3 << 20) + p];
            builder.Build().CopyTo(padded, 0);

            var page = document.AddPage();
            page.Contents.AppendContent().CreateStream(Encoding.Latin1.GetBytes(
                "BT /F1 1 Tf <000100020003000400050006000700080009000A000B000C000D000E000F0010> Tj ET"));
            var resources = new PdfDictionary(document);
            page.Elements["/Resources"] = resources;
            var font = EmbeddedFonts.Composite(document, padded);
            Deflated(font);
            Drawn.Add(document, resources, "/Font", "/F1", font);
        }

        var (sharedOnEachPage, retained) = await Interruptibly.Run(() =>
        {
            var shared = new List<int>();
            var before = GC.GetTotalMemory(true);
            foreach (var page in document.Pages)
            {
                var device = new PathCountingDevice();
                PageRendering.Render(page, device).Diagnostics.Should().BeEmpty();
                device.UnsharedFills.Should().Be(0);
                shared.Add(device.SharedPaths);
            }

            return (shared, GC.GetTotalMemory(true) - before);
        });

        sharedOnEachPage.Should().HaveCount(pages).And.OnlyContain(count => count == 16);
        retained.Should().BeLessThan(176L << 20, "the document keeps 144 MiB of font program and 18 MiB of outline");
        GC.KeepAlive(document);
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task TheFontProgramsOfOnePageAreHeldNoMoreThanTheDocumentsBudgetTogether()
    {
        // 60 fonts on one page, each a program that decodes to 4 MiB from a few kilobytes: 240 MiB
        // held, where the document holds 144 MiB of font program and reads one again if asked.
        const int fonts = 60;
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <0001> Tj ");
        content.Append("/F0 1 Tf <0001> Tj ET");

        var page = Drawn.PageOf(content.ToString(), (document, resources) =>
        {
            for (var k = 0; k < fonts; k++)
            {
                var builder = new TrueTypeFontBuilder();
                builder.AddBar(100 + k);
                var padded = new byte[4 << 20];
                builder.Build().CopyTo(padded, 0);
                var font = EmbeddedFonts.Composite(document, padded);
                Deflated(font);
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }
        });
        var device = new RecordingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Select(fill => Math.Round(Bounds(fill.Path).Width, 9)).Should().Equal(
            Enumerable.Range(0, fonts).Select(k => (100 + k) / 1000.0).Append(0.1), "the first font is read again for the last glyph");
        retained.Should().BeLessThan(176L << 20, "60 programs of 4 MiB are 240 MiB");
        GC.KeepAlive(page);
    }

    /// <summary>Compresses a composite font's program, so the document holds kilobytes of what decodes to megabytes.</summary>
    internal static void Deflated(PdfDictionary font)
    {
        var descendant = (PdfDictionary)((PdfReference)font.Elements.GetArray("/DescendantFonts").Elements[0]).Value;
        var descriptor = (PdfDictionary)((PdfReference)descendant.Elements["/FontDescriptor"]).Value;
        var file = (PdfDictionary)((PdfReference)descriptor.Elements["/FontFile2"]).Value;
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(file.Stream.Value);
        file.Stream.Value = compressed.ToArray();
        file.Elements.SetName("/Filter", "/FlateDecode");
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
    internal static byte[] Dense =>
        [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFE, 0, 0, ..Enumerable.Range(0, 257).SelectMany(_ => new byte[] { 0x39, 0xFF })];

    /// <summary>What a render returns, and how much more memory is live after it than before.</summary>
    internal static (PageRenderResult Result, long Retained) Retained(Func<PageRenderResult> render)
    {
        var before = GC.GetTotalMemory(true);
        var result = render();
        return (result, GC.GetTotalMemory(true) - before);
    }

    /// <summary>A device that keeps no path but those a font shares, and counts what it is handed.</summary>
    internal sealed class PathCountingDevice : PageDevice
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
