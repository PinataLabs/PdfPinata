using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
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

    private static PageRenderDiagnostic MissingGlyph(Drawn drawn) =>
        drawn.Result.Diagnostics.Should().ContainSingle(d => d.Problem == PageRenderProblem.MissingGlyph).Subject;

    private static Drawn Draw(string shown, byte[] program, string after = "") =>
        Drawn.Page($"BT /F1 10 Tf {shown} Tj ET {after}",
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F1", EmbeddedFonts.Composite(document, program)));
}
