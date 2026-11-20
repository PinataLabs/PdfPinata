using System;
using System.Globalization;
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
    [Arguments("{ 1 0 div }")]
    [Arguments("{ -1 sqrt }")]
    [Arguments("{ 0 ln }")]
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

    [Test, Timeout(30_000)]
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
}
