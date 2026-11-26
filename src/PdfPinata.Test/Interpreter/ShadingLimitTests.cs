using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Shadings, functions and patterns that are malformed or made to be costly: each is held to a
/// limit, reported, and the rest of the page is drawn.
/// </summary>
/// <remarks>
/// Each test hands its page to <see cref="Interruptibly"/> under a timeout, because what such
/// input does where a limit is missing is as likely to be a loop that never ends as a wrong
/// answer.
/// </remarks>
public class ShadingLimitTests
{
    private const string Square = "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)";

    /// <summary>A page that paints an axial shading with a function, and then a square.</summary>
    private static Task<Drawn> Shaded(Func<PdfDocument, object> function, string colorSpace = "/DeviceRGB") =>
        Interruptibly.Run(() => Drawn.Saved("/Sh0 sh 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Object(document,
                ("/ShadingType", "2"), ("/ColorSpace", colorSpace), ("/Coords", "[0 0 100 0]"),
                ("/Function", function(document))))));

    private static Task<Drawn> Tiled(string content, string cell, params (string Key, object Value)[] more) =>
        Interruptibly.Run(() => Drawn.Saved(content, (document, resources) =>
            Drawn.Add(document, resources, "/Pattern", "/T0", TilingPatternTests.Tiling(document, cell, more))));

    private static string Stitched(int deep) => deep == 0
        ? ShadingTests.RedToBlue
        : "<< /FunctionType 3 /Domain [0 1] /Bounds [] /Encode [0 1] /Functions [" + Stitched(deep - 1) + "] >>";

    [Test, Timeout(30_000)]
    public async Task ASampledFunctionWhoseSizeIsVastIsRefusedBeforeItsTableIsRead()
    {
        // Eight inputs of a million samples each: no stream is that long, and none is looked for.
        var drawn = await Shaded(document => Drawn.Stream(document, new byte[16],
            ("/FunctionType", "0"), ("/Domain", "[0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1]"), ("/Range", "[0 1 0 1 0 1]"),
            ("/Size", "[1000000 1000000 1000000 1000000 1000000 1000000 1000000 1000000]"), ("/BitsPerSample", "32")));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ASampledFunctionOfOneInputOverTheTableLimitIsRefused()
    {
        var drawn = await Shaded(document => Drawn.Stream(document, new byte[16],
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1 0 1 0 1]"), ("/Size", "[2000000000]"),
            ("/BitsPerSample", "8")));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ASampledFunctionWhoseStreamIsShorterThanItsSizeIsInvalid()
    {
        var drawn = await Shaded(document => Drawn.Stream(document, new byte[8],
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1 0 1 0 1]"), ("/Size", "[3]"),
            ("/BitsPerSample", "8")));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ASampledFunctionOfMoreInputsThanAreReadIsInvalid()
    {
        var drawn = await Shaded(document => Drawn.Stream(document, new byte[1024],
            ("/FunctionType", "0"), ("/Domain", "[0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1]"), ("/Range", "[0 1 0 1 0 1]"),
            ("/Size", "[1 1 1 1 1 1 1 1 1]"), ("/BitsPerSample", "8")));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task FunctionsNestedAsDeepAsTheLimitAreRead()
    {
        var drawn = await Shaded(_ => Stitched(8));

        drawn.Device.Fills.First().Stops.Should().Equal("0 rgb(1,0,0)", "1 rgb(0,0,1)");
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task FunctionsNestedDeeperThanTheLimitAreNotRead()
    {
        var drawn = await Shaded(_ => Stitched(9));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task AFunctionMadeOfItselfIsNotRead()
    {
        var drawn = await Shaded(document =>
        {
            var stitching = Drawn.Object(document, ("/FunctionType", "3"), ("/Domain", "[0 1]"), ("/Bounds", "[]"),
                ("/Encode", "[0 1]"));
            stitching.Elements["/Functions"] = new PdfArray(document, stitching.Reference);
            return stitching;
        });

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task AnArrayOfFunctionsThatHoldsItselfIsNotRead()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Saved("/Sh0 sh 0 0 5 5 re f", (document, resources) =>
        {
            var shading = Drawn.Object(document, ("/ShadingType", "2"), ("/ColorSpace", "/DeviceGray"),
                ("/Coords", "[0 0 100 0]"));
            var holder = new PdfArray(document);
            document.Internals.AddObject(holder);
            holder.Elements.Add(holder.Reference);
            shading.Elements["/Function"] = holder.Reference;
            Drawn.Add(document, resources, "/Shading", "/Sh0", shading);
        }));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    private static PdfDictionary RedToBlue(PdfDocument document) =>
        Drawn.Object(document, ("/FunctionType", "2"), ("/Domain", "[0 1]"), ("/C0", "[1 0 0]"), ("/C1", "[0 0 1]"),
            ("/N", "1"));

    /// <summary>A stitching of its pieces, each over an equal share of the domain.</summary>
    private static PdfDictionary Stitching(PdfDocument document, params PdfDictionary[] pieces) =>
        Stitching(document, "0 1", pieces);

    /// <summary>A stitching of its pieces, each over an equal share of the domain and given the inputs <paramref name="encode"/> names.</summary>
    private static PdfDictionary Stitching(PdfDocument document, string encode, params PdfDictionary[] pieces)
    {
        var bounds = string.Join(" ", Enumerable.Range(1, pieces.Length - 1)
            .Select(bound => ((double)bound / pieces.Length).ToString(CultureInfo.InvariantCulture)));
        var stitching = Drawn.Object(document, ("/FunctionType", "3"), ("/Domain", "[0 1]"), ("/Bounds", $"[{bounds}]"),
            ("/Encode", $"[{string.Concat(Enumerable.Repeat(encode + " ", pieces.Length))}]"));
        var functions = new PdfArray(document);
        foreach (var piece in pieces)
            functions.Elements.Add(piece.Reference);
        stitching.Elements["/Functions"] = functions;
        return stitching;
    }

    /// <summary>Stitchings <paramref name="deep"/> deep, each of whose pieces all name the one below it.</summary>
    private static PdfDictionary SharedStitching(PdfDocument document, int width, int deep, string encode = "0 1")
    {
        var below = RedToBlue(document);
        for (var level = 0; level < deep; level++)
            below = Stitching(document, encode, Enumerable.Repeat(below, width).ToArray());
        return below;
    }

    [Test, Timeout(30_000)]
    public async Task AStitchingWhosePiecesNameOneFunctionIsReadOnceForEachObject()
    {
        // Five objects, and 256 to the fourth paths through them.
        var drawn = await Shaded(document => SharedStitching(document, 256, 4));

        drawn.Device.Fills.First().ShaderAs<PageGradient>().Stops.Count.Should().BeInRange(2, 256);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    [Arguments("0 0")]
    [Arguments("2 3")]
    public async Task AStitchingWhosePiecesNameOneFunctionIsSearchedForBreakpointsOnceForEachObject(string encode)
    {
        // Each piece's own breakpoints fall outside it, so none fills the list and every piece
        // below is looked at: 256 to the fifth of them, were each looked at on each path. That
        // would not finish in hours, and the timeout is what fails it; a clock of its own in the
        // test added only failures on loaded machines.
        var drawn = await Shaded(document => SharedStitching(document, 256, 5, encode));

        drawn.Device.Fills.First().ShaderAs<PageGradient>().Stops.Count.Should().BeInRange(2, 256);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(120_000)]
    public async Task AFunctionOfMoreObjectsThanARenderReadsIsNotRead()
    {
        // 17 stitchings of 256 functions, each its own object, and the stitching of those: 4,370.
        var drawn = await Shaded(document => Stitching(document, Enumerable.Range(0, 17)
            .Select(_ => Stitching(document, Enumerable.Range(0, 256).Select(_ => RedToBlue(document)).ToArray()))
            .ToArray()));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task AFunctionAlreadyReadIsStillTooDeepWhereItIsNamedDeeper()
    {
        // As deep as the limit where the first shading names it, and one deeper in the second.
        var drawn = await Interruptibly.Run(() => Drawn.Saved("/Sh0 sh /Sh1 sh", (document, resources) =>
        {
            var deepest = SharedStitching(document, 1, 8);
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Object(document, ("/ShadingType", "2"),
                ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[0 0 100 0]"), ("/Function", deepest)));
            Drawn.Add(document, resources, "/Shading", "/Sh1", Drawn.Object(document, ("/ShadingType", "2"),
                ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[0 0 100 0]"), ("/Function", Stitching(document, deepest))));
        }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("LimitReached /Sh1");
    }

    /// <summary>
    /// A calculator program of two inputs and three outputs about <paramref name="length"/> bytes
    /// long, which a type 1 shading runs 65 by 65 times. What a render spends on a function is
    /// the most it could cost, its length, so the program skips all but a few of its steps and
    /// the test spends the render's work without waiting for it.
    /// </summary>
    private static string LongProgram(int length)
    {
        var program = new StringBuilder("{ pop pop false { ");
        const string Step = "1 add 1 sub ", End = "} if 0.5 0.5 0.5 }";
        while (program.Length + Step.Length + End.Length <= length)
            program.Append(Step);
        return program.Append(End).ToString();
    }

    private static PdfDictionary FunctionGrid(PdfDocument document, PdfDictionary function, int scale) =>
        Drawn.Object(document, ("/ShadingType", "1"), ("/ColorSpace", "/DeviceRGB"), ("/Domain", "[0 1 0 1]"),
            ("/Matrix", $"[{scale} 0 0 100 0 0]"), ("/Function", function));

    [Test, Timeout(60_000)]
    [Arguments(64)]
    [Arguments(1024)]
    public async Task ShadingsThatShareACostlyProgramAreDrawnUntilTheRenderHasSpentItsWork(int count)
    {
        // One grid of the longest program read is most of what a render spends on shadings.
        var drawn = await Interruptibly.Run(() => Drawn.Saved(
            string.Concat(Enumerable.Range(0, count).Select(index => $"/S{index} sh ")), (document, resources) =>
            {
                var function = Drawn.Stream(document, LongProgram(1 << 16), ("/FunctionType", "4"),
                    ("/Domain", "[0 1 0 1]"), ("/Range", "[0 1 0 1 0 1]"));
                for (var index = 0; index < count; index++)
                    Drawn.Add(document, resources, "/Shading", $"/S{index}", FunctionGrid(document, function, 100 + index));
            }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().StartWith("LimitReached /S1").And.OnlyContain(problem => problem.StartsWith("LimitReached /S"));
    }

    [Test, Timeout(120_000)]
    public async Task AShadingPastThoseKeptIsReadAgainOnlyUntilTheRenderHasSpentItsWork()
    {
        // The first 1,024 shadings are kept, so the one after them is read at each sh.
        var content = new StringBuilder();
        for (var index = 0; index < 1024; index++)
            content.Append($"/F{index} sh ");
        for (var index = 0; index < 200; index++)
            content.Append("/Heavy sh ");

        var drawn = await Interruptibly.Run(() => Drawn.Saved(content.ToString(), (document, resources) =>
        {
            for (var index = 0; index < 1024; index++)
                Drawn.Add(document, resources, "/Shading", $"/F{index}", Drawn.Object(document, ("/ShadingType", "2"),
                    ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[0 0 100 0]"), ("/Function", RedToBlue(document))));
            Drawn.Add(document, resources, "/Shading", "/Heavy", FunctionGrid(document, Drawn.Stream(document,
                LongProgram(8000), ("/FunctionType", "4"), ("/Domain", "[0 1 0 1]"), ("/Range", "[0 1 0 1 0 1]")), 100));
        }));

        drawn.Device.Fills.Count().Should().BeInRange(1025, 1100);
        drawn.Problems.Should().Equal("LimitReached /Heavy");
    }

    private static object Calculator(PdfDocument document, string program) =>
        Drawn.Stream(document, program, ("/FunctionType", "4"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"));

    private static string Conditions(int deep) => deep == 0 ? "0.5" : "true { " + Conditions(deep - 1) + " } if";

    [Test, Timeout(30_000)]
    public async Task ACalculatorProgramNestedAsDeepAsTheLimitIsRun()
    {
        // The program's own braces are the first of the 32.
        var drawn = await Shaded(document => Calculator(document, "{ pop " + Conditions(31) + " }"), "/DeviceGray");

        drawn.Device.Fills.First().Stops.Should().Equal("0 rgb(0.5,0.5,0.5)", "1 rgb(0.5,0.5,0.5)");
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task ACalculatorProgramNestedDeeperThanTheLimitIsNotRead()
    {
        var drawn = await Shaded(document => Calculator(document, "{ pop " + Conditions(32) + " }"), "/DeviceGray");

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ACalculatorProgramOfNothingButOpeningBracesIsNotRead()
    {
        var drawn = await Shaded(document => Calculator(document, new string('{', 60_000)), "/DeviceGray");

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ACalculatorProgramLongerThanTheLimitIsNotRead()
    {
        var program = new StringBuilder("{ pop 0.5 ");
        while (program.Length <= 1 << 16)
            program.Append("1 pop ");
        var drawn = await Shaded(document => Calculator(document, program + "}"), "/DeviceGray");

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    [Arguments("{ 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 " +
               "1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 }",
        "a hundred numbers over the input overflow the stack")]
    [Arguments("{ pop pop 1 }", "nothing is left to pop")]
    [Arguments("{ 1000000000 copy }", "more is copied than there is")]
    [Arguments("{ 50 copy 50 copy 50 copy }", "the copies overflow the stack")]
    [Arguments("{ 1 1000000000 1 roll }", "more is rolled than there is")]
    [Arguments("{ 1000000000 index }", "nothing lies that far down")]
    [Arguments("{ 1 0 idiv }", "nothing is divided by zero")]
    [Arguments("{ 1 0 mod }", "nothing is divided by zero")]
    [Arguments("{ 1 0 div pop 0.5 }", "nothing is divided by zero")]
    [Arguments("{ 0 0 div pop 0.5 }", "nothing is divided by zero")]
    [Arguments("{ -1 sqrt pop 0.5 }", "a negative number has no root")]
    [Arguments("{ 0 ln pop 0.5 }", "zero has no logarithm")]
    [Arguments("{ -1 log pop 0.5 }", "a negative number has no logarithm")]
    [Arguments("{ true add }", "a truth value is no number")]
    [Arguments("{ 1 { 1 } if }", "a number is no truth value")]
    [Arguments("{ 1 lt }", "the result is a truth value and no colour")]
    public async Task ACalculatorProgramThatDoesWhatItCannotAnswersZeros(string program, string because)
    {
        var drawn = await Shaded(document => Calculator(document, program), "/DeviceGray");

        drawn.Device.Fills.First().Stops.Should().Equal(["0 rgb(0,0,0)", "1 rgb(0,0,0)"], because);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    [Arguments("{ 1 1 1e300 roll pop }")]
    [Arguments("{ 1e300 1e300 mul 100000 bitshift cvi }")]
    public async Task ACalculatorProgramThatComputesNoNumberAnswersOneInItsRange(string program)
    {
        var drawn = await Shaded(document => Calculator(document, program), "/DeviceGray");

        drawn.Device.Fills.First().ShaderAs<PageGradient>().Stops.Should().OnlyContain(stop => stop.Color.Red >= 0 && stop.Color.Red <= 1);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    [Arguments("{ 1 frobnicate }")]
    [Arguments("{ 1 { 2 } }")]
    [Arguments("{ 1 } 2")]
    [Arguments("{ 1 ")]
    [Arguments("1 }")]
    [Arguments("")]
    public async Task ACalculatorProgramThatIsNotOneIsInvalid(string program)
    {
        var drawn = await Shaded(document => Calculator(document, program), "/DeviceGray");

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task AGradientIsHandedOverWithNoMoreStopsThanTheLimit()
    {
        // A wave of two hundred cycles: no number of straight pieces under the limit follows it.
        var drawn = await Shaded(document => Calculator(document, "{ 72000 mul sin 2 div 0.5 add }"), "/DeviceGray");

        drawn.Device.Fills.First().ShaderAs<PageGradient>().Stops.Count.Should().BeInRange(2, 256);
    }

    [Test, Timeout(30_000)]
    public async Task AStitchingOfMorePiecesThanStopsAreKeptIsStillWithinTheLimit()
    {
        var pieces = string.Concat(Enumerable.Range(0, 200).Select(piece =>
            $"<< /FunctionType 2 /Domain [0 1] /C0 [{piece % 2}] /C1 [{piece % 2}] /N 1 >> "));
        var bounds = string.Join(" ", Enumerable.Range(1, 199).Select(bound => (bound / 200.0).ToString("0.###")));
        var encode = string.Concat(Enumerable.Repeat("0 1 ", 200));
        var drawn = await Shaded(_ =>
            $"<< /FunctionType 3 /Domain [0 1] /Bounds [{bounds}] /Encode [{encode}] /Functions [{pieces}] >>", "/DeviceGray");

        drawn.Device.Fills.First().ShaderAs<PageGradient>().Stops.Count.Should().BeInRange(2, 256);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    [Arguments("/XStep", "0")]
    [Arguments("/YStep", "0")]
    [Arguments("/BBox", "[0 0 0 8]")]
    [Arguments("/PaintType", "3")]
    [Arguments("/Matrix", "[1 0 0]")]
    [Arguments("/Matrix", "[0 0 0 0 0 0]")]
    public async Task ATilingPatternThatCannotTileIsInvalidAndPaintsNothing(string key, string value)
    {
        var drawn = await Tiled("/Pattern cs /T0 scn 0 0 50 50 re f 0 g 0 0 5 5 re f", "0 0 4 4 re f", (key, value));

        drawn.Device.Body.Should().Equal(Square);
        drawn.Problems.Should().Equal("InvalidValue /T0");
    }

    [Test, Timeout(30_000)]
    public async Task ATilingPatternWhoseTilesAreBelowAQuarterOfAPixelIsPaintedAsOneColour()
    {
        var drawn = await Tiled("/Pattern cs /T0 scn 0 0 5 5 re f", "1 0 0 rg 0 0 8 8 re f",
            ("/Matrix", "[0.03 0 0 0.03 0 0]"));

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(1,0,0)");
        drawn.Problems.Should().Equal("LimitReached /T0");
    }

    [Test, Timeout(30_000)]
    public async Task ATilingPatternWhoseTilesAreAQuarterOfAPixelIsTiled()
    {
        var drawn = await Tiled("/Pattern cs /T0 scn 0 0 5 5 re f", "1 0 0 rg 0 0 8 8 re f",
            ("/Matrix", "[0.03125 0 0 0.03125 0 0]"));

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z tiling");
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task ACellThatOverlapsItsNeighboursMoreTimesThanTheLimitIsDrawnOnceToATile()
    {
        var drawn = await Tiled("/Pattern cs /T0 scn 0 0 5 5 re f", "1 0 0 rg 0 0 8 8 re f",
            ("/XStep", "0.001"), ("/YStep", "0.001"), ("/Matrix", "[1000 0 0 1000 0 0]"));

        var device = new RecordingDevice();
        drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell.Replay(device, XMatrix.Identity);
        device.Fills.Should().HaveCount(1);
        drawn.Problems.Should().Equal("LimitReached /T0");
    }

    [Test, Timeout(120_000)]
    public async Task ACellThatDrawsMoreThanAPictureKeepsIsCutShortAndStaysInBalance()
    {
        var cell = string.Concat(Enumerable.Repeat("q 0 0 1 1 re f ", 70_000));
        var drawn = await Tiled("/Pattern cs /T0 scn 0 0 5 5 re f", cell);

        var device = new RecordingDevice();
        drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell.Replay(device, XMatrix.Identity);
        device.Calls.Count.Should().BeLessThan(70_000);
        device.Calls.Count(call => call.Kind == "save").Should().Be(device.Calls.Count(call => call.Kind == "restore"));
        drawn.Problems.Should().Contain("LimitReached page/T0");
    }

    [Test, Timeout(30_000)]
    public async Task PatternsThatEachPaintWithTheNextStopAtTheDepthFormsStopAt()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Saved("/Pattern cs /T0 scn 0 0 5 5 re f", (document, resources) =>
        {
            for (var index = 0; index < 40; index++)
            {
                Drawn.Add(document, resources, "/Pattern", "/T" + index, TilingPatternTests.Tiling(document,
                    $"/Pattern cs /T{index + 1} scn 0 0 8 8 re f"));
            }
        }));

        drawn.Device.Fills.Should().HaveCount(1);
        drawn.Problems.Should().Equal("LimitReached /T32");
    }

    [Test, Timeout(30_000)]
    public async Task AnUncolouredPatternPaintedInMoreColoursThanAreKeptIsStillDrawnInEach()
    {
        var content = new StringBuilder("/Tinted cs ");
        for (var index = 0; index < 40; index++)
            content.Append(CultureInfo.InvariantCulture, $"{index / 40.0:0.###} 0 0 /T0 scn 0 0 5 5 re f ");

        var drawn = await Interruptibly.Run(() => Drawn.Saved(content.ToString(), (document, resources) =>
        {
            Drawn.Add(document, resources, "/Pattern", "/T0",
                TilingPatternTests.Tiling(document, "0 0 4 4 re f", ("/PaintType", "2")));
            Drawn.AddText(document, resources, "/ColorSpace", "/Tinted", "[/Pattern /DeviceRGB]");
        }));

        var tilings = drawn.Device.Fills.Select(fill => fill.ShaderAs<PageTiling>()).ToList();
        tilings.Should().HaveCount(40);
        tilings.Count(tiling => tiling.IsShared).Should().Be(32);
        drawn.Problems.Should().BeEmpty();
    }

    // What ShadingWork.MaximumBytes is in the library, which this assembly cannot see.
    private const long ShadingWork_MaximumBytes = 128L << 20;

    /// <summary><paramref name="length"/> zero bytes, deflated.</summary>
    private static byte[] DeflatedZeros(long length)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var block = new byte[1 << 20];
            for (long written = 0; written < length; written += block.Length)
                zlib.Write(block, 0, (int)Math.Min(block.Length, length - written));
        }

        return output.ToArray();
    }

    /// <summary>A page that paints <paramref name="count"/> axial shadings, /S0 on, and what drawing it allocated.</summary>
    private static Task<(Drawn Drawn, long Allocated)> ShadedMany(int count, Func<PdfDocument, int, PdfDictionary> function) =>
        Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var drawn = Drawn.Saved(string.Concat(Enumerable.Range(0, count).Select(index => $"/S{index} sh ")),
                (document, resources) =>
                {
                    for (var index = 0; index < count; index++)
                    {
                        Drawn.Add(document, resources, "/Shading", $"/S{index}", Drawn.Object(document,
                            ("/ShadingType", "2"), ("/ColorSpace", "/DeviceGray"), ("/Coords", $"[0 0 {100 + index} 0]"),
                            ("/Function", function(document, index))));
                    }
                });
            return (drawn, GC.GetAllocatedBytesForCurrentThread() - before);
        });

    [Test, Timeout(120_000)]
    public async Task ACalculatorProgramThatInflatesPastTheLimitIsDecodedNoFurtherAndOnce()
    {
        // 256 MiB of zeros, which a program of 64 KiB at most is never read to the end of. What
        // the render allocates says so whatever the machine's load: reading to the end would
        // allocate the whole 256 MiB.
        var bomb = DeflatedZeros(256L << 20);
        PdfDictionary program = null;
        var (drawn, allocated) = await ShadedMany(64, (document, _) => program ??= Drawn.Stream(document, bomb,
            ("/FunctionType", "4"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"), ("/Filter", "/FlateDecode")));

        allocated.Should().BeLessThan(64L << 20);
        drawn.Device.Fills.Should().BeEmpty();
        drawn.Problems.Should().Equal(Enumerable.Range(0, 64).Select(index => $"LimitReached /S{index}"));
    }

    [Test, Timeout(120_000)]
    public async Task ASampledFunctionWhoseStreamFailsIsDecodedOnceAndReportedForEachShading()
    {
        // A table of 16 MiB whose stream is a byte short of it.
        var table = DeflatedZeros((1L << 24) - 1);
        PdfDictionary function = null;
        var (drawn, allocated) = await ShadedMany(64, (document, _) => function ??= Drawn.Stream(document, table,
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"), ("/Size", "[16777216]"),
            ("/BitsPerSample", "8"), ("/Filter", "/FlateDecode")));

        // Each decode of it allocates more than 16 MiB.
        allocated.Should().BeLessThan(128L << 20);
        drawn.Device.Fills.Should().BeEmpty();
        drawn.Problems.Should().Equal(Enumerable.Range(0, 64).Select(index => $"InvalidValue /S{index}"));
    }

    [Test, Timeout(30_000)]
    public async Task EachShadingThatNamesAFunctionThatCannotBeReadIsReportedWithTheFirstFailure()
    {
        PdfDictionary program = null;
        var (drawn, _) = await ShadedMany(3, (document, _) => program ??= Drawn.Stream(document, "{ 1 frobnicate }",
            ("/FunctionType", "4"), ("/Domain", "[0 1]"), ("/Range", "[0 1]")));

        drawn.Device.Fills.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /S0", "InvalidValue /S1", "InvalidValue /S2");
        drawn.Result.Diagnostics.Select(diagnostic => diagnostic.Message).Distinct().Should().ContainSingle();
    }

    [Test, Timeout(300_000)]
    public async Task SampleTablesAreReadUntilTheRenderHasDecodedAllItMay()
    {
        // Sixteen tables of 16 MiB, each its own object: twice what a render decodes.
        var table = DeflatedZeros(1L << 24);
        var (drawn, _) = await ShadedMany(16, (document, _) => Drawn.Stream(document, table,
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"), ("/Size", "[16777216]"),
            ("/BitsPerSample", "8"), ("/Filter", "/FlateDecode")));

        const int kept = (int)(ShadingWork_MaximumBytes >> 24);
        drawn.Device.Fills.Should().HaveCount(kept);
        drawn.Problems.Should().Equal(Enumerable.Range(kept, 16 - kept).Select(index => $"LimitReached /S{index}"));
    }

    [Test, Timeout(300_000)]
    public async Task ShadingsThatDecodeNothingAreDrawnAfterTheRenderHasDecodedAllItMay()
    {
        // Nine tables of 16 MiB spend the bytes. Then an exponential function, which has no
        // stream, and the first table again, which was read already: neither decodes anything.
        // Last a mesh, whose own stream is the one thing here past the limit.
        var table = DeflatedZeros(1L << 24);
        PdfDictionary first = null;
        var drawn = await Interruptibly.Run(() => Drawn.Saved(
            string.Concat(Enumerable.Range(0, 11).Select(index => $"/S{index} sh ")) + "/Mesh sh",
            (document, resources) =>
            {
                for (var index = 0; index < 11; index++)
                {
                    var function = index switch
                    {
                        9 => Drawn.Object(document, ("/FunctionType", "2"), ("/Domain", "[0 1]"), ("/C0", "[0]"),
                            ("/C1", "[1]"), ("/N", "1")),
                        10 => first,
                        _ => Drawn.Stream(document, table, ("/FunctionType", "0"), ("/Domain", "[0 1]"),
                            ("/Range", "[0 1]"), ("/Size", "[16777216]"), ("/BitsPerSample", "8"),
                            ("/Filter", "/FlateDecode"))
                    };
                    first ??= function;
                    Drawn.Add(document, resources, "/Shading", $"/S{index}", Drawn.Object(document,
                        ("/ShadingType", "2"), ("/ColorSpace", "/DeviceGray"), ("/Coords", $"[0 0 {100 + index} 0]"),
                        ("/Function", function)));
                }

                Drawn.Add(document, resources, "/Shading", "/Mesh", Drawn.Stream(document, new byte[9],
                    ("/ShadingType", "4"), ("/ColorSpace", "/DeviceGray"), ("/BitsPerCoordinate", "8"),
                    ("/BitsPerComponent", "8"), ("/BitsPerFlag", "8"), ("/Decode", "[0 100 0 100 0 1]")));
            }));

        const int kept = (int)(ShadingWork_MaximumBytes >> 24);
        drawn.Device.Fills.Should().HaveCount(kept + 2);
        drawn.Problems.Should().Equal("LimitReached /S8", "LimitReached /Mesh");
    }

    [Test, Timeout(300_000)]
    public async Task AMeshLaidOutWronglyIsReportedAsThatAfterTheRenderHasDecodedAllItMay()
    {
        // Nine tables of 16 MiB spend the bytes. The mesh's /BitsPerCoordinate is no width a
        // coordinate may have, so it fails before its stream would be decoded.
        var table = DeflatedZeros(1L << 24);
        var drawn = await Interruptibly.Run(() => Drawn.Saved(
            string.Concat(Enumerable.Range(0, 9).Select(index => $"/S{index} sh ")) + "/Mesh sh",
            (document, resources) =>
            {
                for (var index = 0; index < 9; index++)
                {
                    Drawn.Add(document, resources, "/Shading", $"/S{index}", Drawn.Object(document,
                        ("/ShadingType", "2"), ("/ColorSpace", "/DeviceGray"), ("/Coords", $"[0 0 {100 + index} 0]"),
                        ("/Function", Drawn.Stream(document, table, ("/FunctionType", "0"), ("/Domain", "[0 1]"),
                            ("/Range", "[0 1]"), ("/Size", "[16777216]"), ("/BitsPerSample", "8"),
                            ("/Filter", "/FlateDecode")))));
                }

                Drawn.Add(document, resources, "/Shading", "/Mesh", Drawn.Stream(document, new byte[9],
                    ("/ShadingType", "4"), ("/ColorSpace", "/DeviceGray"), ("/BitsPerCoordinate", "3"),
                    ("/BitsPerComponent", "8"), ("/BitsPerFlag", "8"), ("/Decode", "[0 100 0 100 0 1]")));
            }));

        drawn.Problems.Should().Equal("LimitReached /S8", "InvalidValue /Mesh");
    }

    [Test, Timeout(300_000)]
    public async Task AMeshIsDecodedNoFurtherThanTheTrianglesItIsDrawnAs()
    {
        // 256 MiB of zeros: a vertex of three bytes, flag 0, at the origin, so a triangle in nine.
        var bomb = DeflatedZeros(256L << 20);
        var (drawn, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var drawn = Drawn.Saved("/Sh0 sh", (document, resources) =>
                Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Stream(document, bomb, ("/ShadingType", "4"),
                    ("/ColorSpace", "/DeviceGray"), ("/BitsPerCoordinate", "8"), ("/BitsPerComponent", "8"),
                    ("/BitsPerFlag", "8"), ("/Decode", "[0 100 0 100 0 1]"), ("/Filter", "/FlateDecode"))));
            return (drawn, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        allocated.Should().BeLessThan(256L << 20);
        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }
}
