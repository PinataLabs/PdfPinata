using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// PDF functions, read through the one thing a caller can see of them: the stops of the gradient
/// a device is handed.
/// </summary>
public class ShadingFunctionTests
{
    /// <summary>The fill of an axial shading from 0 to 100 along x whose function is written as PDF text.</summary>
    private static RecordingDevice.Call Shaded(string function, string colorSpace = "/DeviceRGB", string domain = null) =>
        Shaded(_ => function, colorSpace, domain);

    private static RecordingDevice.Call Shaded(Func<PdfDocument, object> function, string colorSpace = "/DeviceRGB",
        string domain = null)
    {
        var drawn = Drawn.Saved("/Sh0 sh", (document, resources) =>
        {
            var shading = Drawn.Object(document, ("/ShadingType", "2"), ("/ColorSpace", colorSpace),
                ("/Coords", "[0 0 100 0]"), ("/Function", function(document)));
            if (domain != null)
                shading.Elements["/Domain"] = new PdfLiteral(domain);
            Drawn.Add(document, resources, "/Shading", "/Sh0", shading);
        });

        drawn.Problems.Should().BeEmpty();
        return drawn.Device.Fills.Single();
    }

    /// <summary>The colour a gradient has at an offset, read off its stops as a device reads them.</summary>
    private static (double Red, double Green, double Blue) ColourAt(RecordingDevice.Call fill, double offset)
    {
        var stops = fill.ShaderAs<PageGradient>().Stops;
        for (var index = 1; index < stops.Count; index++)
        {
            PageGradientStop a = stops[index - 1], b = stops[index];
            if (offset > b.Offset || b.Offset <= a.Offset)
                continue;

            var along = (offset - a.Offset) / (b.Offset - a.Offset);
            return (a.Color.Red + along * (b.Color.Red - a.Color.Red),
                a.Color.Green + along * (b.Color.Green - a.Color.Green),
                a.Color.Blue + along * (b.Color.Blue - a.Color.Blue));
        }

        return (stops[^1].Color.Red, stops[^1].Color.Green, stops[^1].Color.Blue);
    }

    [Test]
    public void AnExponentialFunctionOfExponentOneIsTwoStops()
    {
        Shaded(ShadingTests.RedToBlue).Stops.Should().Equal("0 rgb(1,0,0)", "1 rgb(0,0,1)");
    }

    [Test]
    public void AnExponentialFunctionThatCurvesIsStopsEnoughToFollowItToOnePartIn255()
    {
        var fill = Shaded("<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 3 >>", "/DeviceGray");

        var stops = fill.ShaderAs<PageGradient>().Stops;
        stops.Count.Should().BeInRange(3, 256);
        stops.Select(stop => stop.Offset).Should().BeInAscendingOrder();
        (stops[0].Offset, stops[^1].Offset).Should().Be((0, 1));
        for (var at = 0.0; at <= 1; at += 1.0 / 97)
            ColourAt(fill, at).Red.Should().BeApproximately(at * at * at, 1.5 / 255);
    }

    [Test]
    public void AShadingsDomainIsTakenOutOfItsStops()
    {
        // The function is read from 0.5 to 1 and the stops still run from 0 to 1.
        Shaded(ShadingTests.RedToBlue, domain: "[0.5 1]").Stops.Should().Equal("0 rgb(0.5,0,0.5)", "1 rgb(0,0,1)");
    }

    [Test]
    public void AnArrayOfFunctionsIsOneFunctionOfAsManyOutputs()
    {
        var fill = Shaded(
            "[<< /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> " +
            "<< /FunctionType 2 /Domain [0 1] /C0 [0.5] /C1 [0.5] /N 1 >> " +
            "<< /FunctionType 2 /Domain [0 1] /N 1 >>]");

        fill.Stops.Should().Equal("0 rgb(1,0.5,0)", "1 rgb(0,0.5,1)");
    }

    [Test]
    public void AStitchingFunctionKeepsAHardEdgeAtEachBoundWhereItsPiecesDisagree()
    {
        var fill = Shaded(
            "<< /FunctionType 3 /Domain [0 1] /Bounds [0.25 0.5] /Encode [0 1 1 0 0 1] /Functions [" +
            "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [1 0 0] /N 1 >> " +
            ShadingTests.RedToBlue + " " +
            "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 1 0] /N 1 >>] >>");

        // Two stops at a quarter, where red meets the blue end of a ramp run backwards. One at a
        // half, where that ramp ends on the red the last piece begins on.
        fill.Stops.Should().Equal(
            "0 rgb(1,0,0)", "0.25 rgb(1,0,0)", "0.25 rgb(0,0,1)", "0.5 rgb(1,0,0)", "1 rgb(0,1,0)");
    }

    [Test]
    public void AStitchingFunctionInAStitchingFunctionHandsOnItsBoundsToo()
    {
        var inner =
            "<< /FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [" +
            "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [0] /N 1 >> " +
            "<< /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [1] /N 1 >>] >>";
        var fill = Shaded(
            "<< /FunctionType 3 /Domain [0 2] /Bounds [1] /Encode [0 1 0 1] /Functions [" + inner + " " + inner + "] >>",
            "/DeviceGray", "[0 2]");

        fill.Stops.Should().Equal(
            "0 rgb(0,0,0)", "0.25 rgb(0,0,0)", "0.25 rgb(1,1,1)", "0.5 rgb(1,1,1)",
            "0.5 rgb(0,0,0)", "0.75 rgb(0,0,0)", "0.75 rgb(1,1,1)", "1 rgb(1,1,1)");
    }

    [Test]
    public void ASampledFunctionIsAStopAtEachSample()
    {
        var fill = Shaded(document => Drawn.Stream(document, [255, 0, 0, 0, 255, 0, 0, 0, 255],
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1 0 1 0 1]"), ("/Size", "[3]"),
            ("/BitsPerSample", "8")));

        fill.Stops.Should().Equal("0 rgb(1,0,0)", "0.5 rgb(0,1,0)", "1 rgb(0,0,1)");
    }

    [Test]
    public void ASampledFunctionOfOrderThreeIsReadInStraightLinesAndSaysSo()
    {
        var drawn = Drawn.Saved("/Sh0 sh", (document, resources) => Drawn.Add(document, resources, "/Shading", "/Sh0",
            Drawn.Object(document, ("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[0 0 100 0]"),
                ("/Function", Drawn.Stream(document, [255, 0, 0, 0, 255, 0, 0, 0, 255], ("/FunctionType", "0"),
                    ("/Domain", "[0 1]"), ("/Range", "[0 1 0 1 0 1]"), ("/Size", "[3]"), ("/BitsPerSample", "8"),
                    ("/Order", "3"))))));

        drawn.Device.Fills.Single().Stops.Should().Equal("0 rgb(1,0,0)", "0.5 rgb(0,1,0)", "1 rgb(0,0,1)");
        drawn.Problems.Should().Equal("UnsupportedFeature /Order");
    }

    [Test]
    public void ASampledFunctionAppliesEncodeAndDecode()
    {
        // The table is read backwards, and its samples are taken to a narrower range.
        var fill = Shaded(document => Drawn.Stream(document, [0, 255],
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"), ("/Size", "[2]"),
            ("/BitsPerSample", "8"), ("/Encode", "[1 0]"), ("/Decode", "[0.2 0.6]")), "/DeviceGray");

        fill.Stops.Should().Equal("0 rgb(0.6,0.6,0.6)", "1 rgb(0.2,0.2,0.2)");
    }

    [Test]
    [Arguments(1, new byte[] { 0b0100_0000 })]
    [Arguments(2, new byte[] { 0b0011_0000 })]
    [Arguments(4, new byte[] { 0x0F })]
    [Arguments(8, new byte[] { 0, 255 })]
    [Arguments(12, new byte[] { 0x00, 0x0F, 0xFF })]
    [Arguments(16, new byte[] { 0, 0, 255, 255 })]
    [Arguments(24, new byte[] { 0, 0, 0, 255, 255, 255 })]
    [Arguments(32, new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 })]
    public void ASampledFunctionReadsSamplesOfEveryWidth(int bitsPerSample, byte[] zeroThenFull)
    {
        var fill = Shaded(document => Drawn.Stream(document, zeroThenFull,
            ("/FunctionType", "0"), ("/Domain", "[0 1]"), ("/Range", "[0 1]"), ("/Size", "[2]"),
            ("/BitsPerSample", bitsPerSample.ToString())), "/DeviceGray");

        fill.Stops.Should().Equal("0 rgb(0,0,0)", "1 rgb(1,1,1)");
    }

    private static object Calculator(PdfDocument document, string program, string range = "[0 1 0 1 0 1]") =>
        Drawn.Stream(document, program, ("/FunctionType", "4"), ("/Domain", "[0 1]"), ("/Range", range));

    [Test]
    public void ACalculatorFunctionComputesWithItsStack()
    {
        // t squared in blue, and what is left of one in red.
        var fill = Shaded(document => Calculator(document, "{ dup mul dup 1 exch sub exch 0 exch }"));

        var (red, green, blue) = ColourAt(fill, 0.5);
        red.Should().BeApproximately(0.75, 1.5 / 255);
        green.Should().Be(0);
        blue.Should().BeApproximately(0.25, 1.5 / 255);
    }

    [Test]
    public void ACalculatorFunctionTakesTheBranchItsConditionChooses()
    {
        var fill = Shaded(document => Calculator(document,
            "{ dup 0.5 gt { pop 1 0 0 } { 0.25 lt { 0 1 0 } { 0 0 1 } ifelse } ifelse }"));

        ColourAt(fill, 0.1).Should().Be((0, 1, 0));
        ColourAt(fill, 0.4).Should().Be((0, 0, 1));
        ColourAt(fill, 0.9).Should().Be((1, 0, 0));
    }

    [Test]
    [Arguments("{ 360 mul sin 2 div 0.5 add }", 0.25, 1.0)]
    [Arguments("{ 360 mul cos 2 div 0.5 add }", 0.5, 0.0)]
    [Arguments("{ pop 1 1 atan 90 div }", 0.5, 0.5)]
    [Arguments("{ pop 7 2 idiv 6 div }", 0.5, 0.5)]
    [Arguments("{ pop 7 4 mod 6 div }", 0.5, 0.5)]
    [Arguments("{ pop 0.25 sqrt }", 0.5, 0.5)]
    [Arguments("{ pop 2 -1 exp }", 0.5, 0.5)]
    [Arguments("{ pop 100 log 4 div }", 0.5, 0.5)]
    [Arguments("{ pop 1 ln 0.5 add }", 0.5, 0.5)]
    [Arguments("{ pop -0.5 abs }", 0.5, 0.5)]
    [Arguments("{ pop 0.5 neg neg }", 0.5, 0.5)]
    [Arguments("{ pop 1.4 round 2.6 floor add 0.2 ceiling add 8 div }", 0.5, 0.5)]
    [Arguments("{ pop 3.7 truncate 2.9 cvi add 10 div 0 cvr add }", 0.5, 0.5)]
    [Arguments("{ pop 1 2 bitshift 12 -1 bitshift add 20 div }", 0.5, 0.5)]
    [Arguments("{ pop 12 10 and 12 10 or add 12 10 xor add 56 div }", 0.5, 0.5)]
    [Arguments("{ pop true false or true and false xor not { 0.25 } { 0.5 } ifelse }", 0.5, 0.5)]
    [Arguments("{ pop 1 2 eq 1 2 ne and 2 1 ge and { 0.25 } { 0.5 } ifelse }", 0.5, 0.5)]
    [Arguments("{ pop 1 2 le 1 2 lt and 2 1 gt and { 0.5 } if }", 0.5, 0.5)]
    [Arguments("{ pop true 1 eq 1 true eq or { 0.25 } { 0.5 } ifelse }", 0.5, 0.5)]
    [Arguments("{ pop false 0 ne 0 false ne and { 0.5 } { 0.25 } ifelse }", 0.5, 0.5)]
    [Arguments("{ pop 0.1 0.2 0.3 3 1 roll pop pop 0.2 add }", 0.5, 0.5)]
    [Arguments("{ pop 0.1 0.2 0.3 3 -1 roll 0.4 add 3 1 roll pop pop }", 0.5, 0.5)]
    [Arguments("{ pop 0.2 0.3 2 copy add add add 2 div }", 0.5, 0.5)]
    [Arguments("{ pop 0.5 0.1 0.2 2 index 3 1 roll pop pop exch pop }", 0.5, 0.5)]
    [Arguments("{ % a comment\n pop 0.5 }", 0.5, 0.5)]
    public void ACalculatorFunctionKnowsEachOperator(string program, double at, double gray)
    {
        var fill = Shaded(document => Calculator(document, program, "[0 1]"), "/DeviceGray");

        ColourAt(fill, at).Red.Should().BeApproximately(gray, 1.5 / 255);
    }

    [Test]
    public void AFunctionsOutputsAreHeldToItsRange()
    {
        var fill = Shaded(document => Calculator(document, "{ pop 7 -7 0.5 }", "[0 0.75 0.25 1 0 1]"));

        fill.Stops.Should().Equal("0 rgb(0.75,0.25,0.5)", "1 rgb(0.75,0.25,0.5)");
    }
}
