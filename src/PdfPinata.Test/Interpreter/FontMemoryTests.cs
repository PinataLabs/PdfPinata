using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Interpreter.EmbeddedFonts;
using static PdfPinata.Test.Interpreter.MalformedTrueTypeTests;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// What page rendering keeps for a document's fonts is bounded for the document, however many font
/// dictionaries the file has and whatever they share: each test is a file of some kilobytes that
/// once held hundreds of megabytes, or took minutes.
/// </summary>
/// <remarks>
/// The rule these pin is <c>RenderFontCache</c>'s: whatever is read from a font and kept is kept
/// under the object it was read from and charged to the document's budget. Each test measures
/// live memory, so each runs alone.
/// </remarks>
public class FontMemoryTests
{
    private const long Megabyte = 1L << 20;

    [Test]
    public void AFontAndItsGlyphSourceKeepNothingThatGrows()
    {
        // The rule in structure: these hold the font's own objects and a few numbers, and
        // everything read from them is in the document's cache, where it is charged. A field of
        // any other type is a collection or a buffer waiting to be forgotten by the budget.
        var rendering = typeof(PageRendering).Assembly;
        Type[] fixedSize =
        [
            typeof(bool), typeof(int), typeof(long), typeof(double), typeof(string),
            typeof(PdfDictionary), typeof(PdfArray), typeof(PageGlyphSource),
            rendering.GetType("PdfPinata.Pdf.Rendering.RenderFontCache", true),
            rendering.GetType("PdfPinata.Pdf.Rendering.FontWidths", true),
            rendering.GetType("PdfPinata.Pdf.Rendering.VerticalMetrics", true)
        ];

        foreach (var name in new[] { "RenderFont", "EmbeddedTrueTypeGlyphSource", "FontWidths", "VerticalMetrics" })
        {
            var type = rendering.GetType("PdfPinata.Pdf.Rendering." + name, true);
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            fields.Should().NotBeEmpty();
            fields.Where(field => !fixedSize.Contains(field.FieldType)).Select(field => $"{name}.{field.Name}")
                .Should().BeEmpty("what a font reads and keeps belongs in RenderFontCache, under the object it was read from");
        }
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task FontDictionariesOverOneFontOfEmptyGlyphsKeepOneEntryForEachGlyph()
    {
        // 128 Type 0 dictionaries over one descendant whose 16,384 glyphs are all empty. Kept for
        // each dictionary and charged nothing, two million empty outlines were over 100 MiB.
        const int fonts = 128, glyphs = 16384;
        var page = Drawn.PageOf(ShowingEveryCid(fonts, glyphs), (document, resources) =>
            AddSharing(document, resources, fonts, Composite(document, EmptyGlyphs(glyphs))));

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, new PathCountingDevice())));

        result.Diagnostics.Should().BeEmpty();
        retained.Should().BeLessThan(16 * Megabyte, "16,384 entries are under 2 MiB");
        GC.KeepAlive(page);
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task EmptyGlyphsOfManyFontProgramsAreKeptOnlyUpToTheOutlineBudget()
    {
        // 32 fonts with a program each, 65,534 empty glyphs in each. An empty outline was charged
        // nothing, so the budget never filled: two million entries were 130 MiB.
        const int fonts = 32, glyphs = 65534;
        var page = Drawn.PageOf(ShowingEveryCid(fonts, glyphs), (document, resources) =>
        {
            for (var k = 0; k < fonts; k++)
                Drawn.Add(document, resources, "/Font", $"/F{k}", Composite(document, EmptyGlyphs(glyphs, k)));
        });

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, new PathCountingDevice())));

        result.Diagnostics.Should().BeEmpty();
        retained.Should().BeLessThan(64 * Megabyte, "the outline budget is 18 MiB, and the programs are 9 MiB");
        GC.KeepAlive(page);
    }

    [Test, Timeout(300000), NotInParallel]
    [Arguments("/W", "[0 65535 500]", "/Identity-H")]
    [Arguments("/W2", "[0 65535 -1000 500 880]", "/Identity-V")]
    public async Task FontDictionariesOverOneDescendantReadItsWidthsOnce(string key, string array, string cmap)
    {
        // An array of three or five numbers gives all 65,536 CIDs a width. Read into each of 512
        // font dictionaries of 150 bytes, /W held 1 GiB and /W2 1.6 GiB.
        const int fonts = 512;
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <0001> Tj ");
        var page = Drawn.PageOf(content.Append("ET").ToString(), (document, resources) =>
        {
            var first = Composite(document, OneBar());
            first.Elements.SetName("/Encoding", cmap);
            Descendant(first).Elements[key] = Numbers(document, array);
            AddSharing(document, resources, fonts, first);
        });
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(fonts);
        retained.Should().BeLessThan(16 * Megabyte, "one table of 65,536 CIDs is 0.5 MiB of widths or 1.5 MiB of vertical metrics");
        GC.KeepAlive(page);
    }

    [Test, Timeout(300000), NotInParallel]
    [Arguments("/W", "[0 65535 500]", "/Identity-H")]
    [Arguments("/W2", "[0 65535 -1000 500 880]", "/Identity-V")]
    public async Task WidthsOfManyDescendantsAreHeldNoMoreThanTheDocumentsBudgetTogether(string key, string array, string cmap)
    {
        // 400 fonts that share nothing, each with an array of its own: 200 MiB of widths or 600 MiB
        // of vertical metrics, of which the document holds 144 MiB and reads the rest again.
        const int fonts = 400;
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <0001> Tj ");
        var page = Drawn.PageOf(content.Append("/F0 1 Tf <0001> Tj ET").ToString(), (document, resources) =>
        {
            var program = OneBar();
            for (var k = 0; k < fonts; k++)
            {
                var font = Composite(document, program);
                font.Elements.SetName("/Encoding", cmap);
                Descendant(font).Elements[key] = Numbers(document, array);
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }
        });
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(fonts + 1, "the first font's widths are read again for the last glyph");
        retained.Should().BeLessThan(176 * Megabyte, "the document's budget for font data is 144 MiB");
        GC.KeepAlive(page);
    }

    [Test, Timeout(120000), NotInParallel]
    public async Task AWidthIsTheSameReadFromTheSharedTableAsItWasReadForEachFont()
    {
        // /W in both its forms, a CID given a width twice, and a CID given none: read once into
        // a table, they advance each glyph as far as they did read into each font.
        var page = Drawn.PageOf("BT /F1 10 Tf <00010002000300040001> Tj ET", (document, resources) =>
        {
            var builder = new TrueTypeFontBuilder();
            for (var i = 0; i < 4; i++)
                builder.AddBar(100);
            var font = Composite(document, builder.Build());
            var descendant = Descendant(font);
            descendant.Elements.SetInteger("/DW", 300);
            descendant.Elements["/W"] = new PdfArray(document, new PdfReal(1), Numbers(document, "[250 750]"),
                new PdfReal(1), new PdfReal(1), new PdfReal(500));
            Drawn.Add(document, resources, "/Font", "/F1", font);
        });
        var device = new RecordingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        result.Diagnostics.Should().BeEmpty();
        var origins = device.Fills.Select(fill => Math.Round(fill.Transform.OffsetX, 6)).ToArray();
        origins.Zip(origins.Skip(1), (a, b) => Math.Round(b - a, 6)).Should().Equal(5, 7.5, 3, 3);
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task FontDictionariesOverOneFontProgramDecodeItOnce()
    {
        // 64 font dictionaries over one /FontFile2 stream that decodes to 8 MiB from a few
        // kilobytes. Each decoded a copy of its own, 512 MiB of decoding for one page.
        const int fonts = 64;
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <0001> Tj ");
        var page = Drawn.PageOf(content.Append("ET").ToString(), (document, resources) =>
        {
            var padded = new byte[8 << 20];
            OneBar().CopyTo(padded, 0);
            var first = Composite(document, padded);
            Deflated(first);
            Drawn.Add(document, resources, "/Font", "/F0", first);
            for (var k = 1; k < fonts; k++)
            {
                // A descendant of its own for each, and one font descriptor between them all.
                var font = Composite(document, []);
                Descendant(font).Elements["/FontDescriptor"] = Descendant(first).Elements["/FontDescriptor"];
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }
        });
        var device = new PathCountingDevice();

        var (result, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var rendered = PageRendering.Render(page, device);
            return (rendered, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(fonts);
        allocated.Should().BeLessThan(64 * Megabyte, "decoding 8 MiB once allocates a few times that, and 64 times allocates 512 MiB");
        device.SharedPaths.Should().Be(1, "every dictionary draws the one glyph of the one program");
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task TwoLargeFontProgramsSetInTurnAreEachReadOnce()
    {
        // Two fonts whose programs decode to 40 MiB each, larger than any font there is,
        // selected in turn 200 times. Both fit the document's budget, so neither is read again:
        // where only one fitted, each selection decoded 40 MiB, 130 ms of it.
        const int turns = 200;
        var content = new StringBuilder("BT ");
        for (var turn = 0; turn < turns; turn++)
            content.Append($"/F{turn % 2} 1 Tf <0001> Tj ");
        var page = Drawn.PageOf(content.Append("ET").ToString(), (document, resources) =>
        {
            for (var k = 0; k < 2; k++)
            {
                var padded = new byte[(40 << 20) - k];
                OneBar().CopyTo(padded, 0);
                var font = Composite(document, padded);
                Deflated(font);
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }
        });
        var device = new PathCountingDevice();

        var (result, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var rendered = PageRendering.Render(page, device);
            return (rendered, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        result.Diagnostics.Should().BeEmpty();
        device.Fills.Should().Be(turns);
        allocated.Should().BeLessThan(512 * Megabyte, "two programs decoded once, where 200 decodings allocate over 8 GiB");
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task AFontProgramLargerThanTheLimitIsReportedAndNotKept()
    {
        // A stream of a few kilobytes that decodes to 72 MiB of font program. The largest real
        // fonts are 20 to 50 MB, and the limit is 64 MiB.
        var page = Drawn.PageOf("BT /F1 10 Tf <0001> Tj ET 0 0 5 5 re f", (document, resources) =>
        {
            var padded = new byte[72 << 20];
            OneBar().CopyTo(padded, 0);
            var font = Composite(document, padded);
            Deflated(font);
            Drawn.Add(document, resources, "/Font", "/F1", font);
        });
        var device = new PathCountingDevice();

        var (result, retained) = await Interruptibly.Run(() => Retained(() => PageRendering.Render(page, device)));

        var missing = result.Diagnostics.Should().ContainSingle().Subject;
        missing.Problem.Should().Be(PageRenderProblem.MissingGlyph);
        missing.Message.Should().Contain("larger than 64 MiB");
        device.Fills.Should().Be(1, "the rectangle after the text is drawn");
        retained.Should().BeLessThan(8 * Megabyte, "the program is not kept");
        GC.KeepAlive(page);
    }

    [Test, Timeout(300000), NotInParallel]
    public async Task APageThatReadsFontProgramsOverAndOverIsStoppedAtItsAllowance()
    {
        // Three fonts of 63 MiB each, of which the document holds two, selected in turn: every
        // selection reads a program that was let go of for the one before, a fifth of a second
        // of decoding each, for 15 bytes of content. A page reads 1 GiB of font program, 17 of
        // these, and after that a program is not read again for the page: the two fonts still
        // held go on drawing, and the third is reported.
        const int turns = 400;
        var content = new StringBuilder("BT ");
        for (var turn = 0; turn < turns; turn++)
            content.Append($"/F{turn % 3} 1 Tf <0001> Tj ");
        var document = new PdfDocument();
        for (var p = 0; p < 2; p++)
        {
            var page = document.AddPage();
            page.Contents.AppendContent().CreateStream(Encoding.Latin1.GetBytes(p == 0 ? content + "ET" : "BT /F0 1 Tf <0001> Tj ET"));
            var resources = new PdfDictionary(document);
            page.Elements["/Resources"] = resources;
            for (var k = 0; k < 3 && p == 0; k++)
            {
                var padded = new byte[(63 << 20) + k];
                OneBar().CopyTo(padded, 0);
                var font = Composite(document, padded);
                Deflated(font);
                Drawn.Add(document, resources, "/Font", $"/F{k}", font);
            }

            if (p == 1)
                resources.Elements["/Font"] = document.Pages[0].Elements.GetDictionary("/Resources").Elements["/Font"];
        }

        var first = new PathCountingDevice();
        var second = new PathCountingDevice();
        var (firstResult, secondResult) = await Interruptibly.Run(() =>
            (PageRendering.Render(document.Pages[0], first), PageRendering.Render(document.Pages[1], second)));

        firstResult.Diagnostics.Should().NotBeEmpty().And.OnlyContain(d => d.Problem == PageRenderProblem.MissingGlyph
                                                                           && d.Message.Contains("MiB of font program"));
        first.Fills.Should().Be(17 + 255, "17 programs are read, and then two of every three glyphs are of a font still held");
        secondResult.Diagnostics.Should().BeEmpty("the next page has an allowance of its own");
        second.Fills.Should().Be(1);
    }

    [Test, Timeout(120000), NotInParallel]
    public async Task ALargeFormat12CmapOutOfOrderCostsOnePassHoweverManyFontsAndCodesReadIt()
    {
        // A million groups past every code the text shows, the last two the wrong way round: 12 MiB
        // of subtable. Four million groups that were all one group, which deflate to kilobytes, were
        // searched group by group for each code of each font, and took 48 seconds for one font
        // dictionary and 197 for four.
        var page = Drawn.PageOf(ShowingEveryCode(8), (document, resources) =>
        {
            var builder = new TrueTypeFontBuilder();
            builder.AddBar(100);
            var groups = Enumerable.Range(0, 1_000_000).Select(g => (0x10000u + (uint)g, 1)).ToArray();
            (groups[^1], groups[^2]) = (groups[^2], groups[^1]);
            builder.Map(3, 10, EmbeddedTrueTypeTests.Format12Groups(1_000_000, groups));
            AddSimpleOverOneProgram(document, resources, 8, builder.Build());
        });
        var device = new PathCountingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        result.Diagnostics.Should().NotBeEmpty().And.OnlyContain(d => d.Problem == PageRenderProblem.MissingGlyph
                                                                      && d.Message.Contains("out of order"));
        device.Fills.Should().Be(8, "the subtable is refused, as FreeType refuses it, and code 0 draws the missing glyph");
    }

    [Test, Timeout(60000), NotInParallel]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ALargeFormat4CmapIsSearchedByHalvesWhereItIsInOrderAndRefusedWhereItIsNot(bool inOrder)
    {
        // 8000 segments that end below every code the text shows, about as many as a subtable's
        // 16-bit length has room for. Every code of every font dictionary walked them all, some
        // millions of reads for each font dictionary: 1024 of them, 150 bytes each, took over a
        // minute, and take four seconds searched by halves. 64 are drawn here, to keep this quick.
        const int fonts = 64;
        var page = Drawn.PageOf(ShowingEveryCode(fonts), (document, resources) =>
        {
            var builder = new TrueTypeFontBuilder();
            var bar = builder.AddBar(100);
            var segments = Enumerable.Repeat((Character: 0x10, Glyph: bar), 8000).ToList();
            segments.Add((0xFFFF, 0));
            if (!inOrder)
                segments[0] = (0x11, bar);
            builder.Map(3, 1, EmbeddedTrueTypeTests.Format4Segments(segments.ToArray()));
            AddSimpleOverOneProgram(document, resources, fonts, builder.Build());
        });
        var device = new PathCountingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        device.Fills.Should().Be(inOrder ? 2 * fonts : fonts, "code 0 draws the missing glyph, and code 0x10 the bar where the subtable is read");
        result.Diagnostics.Should().NotBeEmpty().And.OnlyContain(d => d.Problem == PageRenderProblem.MissingGlyph);
        var refused = result.Diagnostics.Count(d => d.Message.Contains("too many segments to search out of order"));
        refused.Should().Be(inOrder ? 0 : result.Diagnostics.Count);
    }

    /// <summary>A font program of one glyph after the missing glyph: a bar 100 units wide.</summary>
    private static byte[] OneBar()
    {
        var builder = new TrueTypeFontBuilder();
        builder.AddBar(100);
        return builder.Build();
    }

    /// <summary>A font program whose glyphs all have no contours, made different from another by a byte after it.</summary>
    private static byte[] EmptyGlyphs(int glyphs, int unlike = 0)
    {
        var builder = new TrueTypeFontBuilder();
        for (var i = 1; i < glyphs; i++)
            builder.AddEmpty();
        var program = builder.Build();
        Array.Resize(ref program, program.Length + 1 + unlike);
        return program;
    }

    /// <summary>Text that shows the first CIDs of each of some fonts, <c>/F0</c> on.</summary>
    private static string ShowingEveryCid(int fonts, int cids)
    {
        var shown = new StringBuilder(4 * cids);
        for (var cid = 0; cid < cids; cid++)
            shown.Append(cid.ToString("X4"));

        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <{shown}> Tj ");
        return content.Append("ET").ToString();
    }

    /// <summary>Text that shows all 256 codes in each of some simple fonts, <c>/F0</c> on.</summary>
    private static string ShowingEveryCode(int fonts)
    {
        var shown = string.Concat(Enumerable.Range(0, 256).Select(code => code.ToString("X2")));
        var content = new StringBuilder("BT ");
        for (var k = 0; k < fonts; k++)
            content.Append($"/F{k} 1 Tf <{shown}> Tj ");
        return content.Append("ET").ToString();
    }

    /// <summary>Adds a Type 0 font as <c>/F0</c>, and as many more dictionaries over its descendant as make up the count.</summary>
    private static void AddSharing(PdfDocument document, PdfDictionary resources, int fonts, PdfDictionary first)
    {
        Drawn.Add(document, resources, "/Font", "/F0", first);
        for (var k = 1; k < fonts; k++)
        {
            var font = new PdfDictionary(document);
            font.Elements.SetName("/Type", "/Font");
            font.Elements.SetName("/Subtype", "/Type0");
            font.Elements.SetName("/BaseFont", "/Embedded");
            font.Elements["/Encoding"] = first.Elements["/Encoding"];
            font.Elements["/DescendantFonts"] = first.Elements["/DescendantFonts"];
            Drawn.Add(document, resources, "/Font", $"/F{k}", font);
        }
    }

    /// <summary>Adds simple fonts <c>/F0</c> on, each a dictionary of its own over one font descriptor and program.</summary>
    private static void AddSimpleOverOneProgram(PdfDocument document, PdfDictionary resources, int fonts, byte[] program)
    {
        var first = Simple(document, program, 32, new PdfName("/WinAnsiEncoding"));
        Drawn.Add(document, resources, "/Font", "/F0", first);
        for (var k = 1; k < fonts; k++)
        {
            var font = Simple(document, [], 32, new PdfName("/WinAnsiEncoding"));
            font.Elements["/FontDescriptor"] = first.Elements["/FontDescriptor"];
            Drawn.Add(document, resources, "/Font", $"/F{k}", font);
        }
    }

    private static PdfDictionary Descendant(PdfDictionary font) =>
        (PdfDictionary)((PdfReference)font.Elements.GetArray("/DescendantFonts").Elements[0]).Value;

    private static PdfArray Numbers(PdfDocument document, string array) =>
        new(document, array.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(number => (PdfItem)new PdfReal(double.Parse(number, System.Globalization.CultureInfo.InvariantCulture))).ToArray());
}
