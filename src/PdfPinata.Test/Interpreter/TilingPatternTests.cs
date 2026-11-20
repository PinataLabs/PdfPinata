using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Tiling patterns: the cell a device is handed as a picture, what the picture draws when it is
/// replayed, and the rectangle it repeats by.
/// </summary>
public class TilingPatternTests
{
    private const string CellBox = "M0,0 L8,0 L8,8 L0,8 Z";

    internal static PdfDictionary Tiling(PdfDocument document, string content, params (string Key, object Value)[] more) =>
        Drawn.Stream(document, content, [
            ("/PatternType", "1"), ("/PaintType", "1"), ("/TilingType", "1"), ("/BBox", "[0 0 8 8]"),
            ("/XStep", "8"), ("/YStep", "8"),
            .. more
        ]);

    private static Drawn WithTiling(string content, string cell, bool drawsShaders = true,
        params (string Key, object Value)[] more) =>
        Drawn.Saved(content, (document, resources) =>
        {
            Drawn.Add(document, resources, "/Pattern", "/T0", Tiling(document, cell, more));
            Drawn.Add(document, resources, "/Shading", "/Sh0", ShadingTests.Axial(document));
            Drawn.AddText(document, resources, "/ColorSpace", "/Tinted", "[/Pattern /DeviceRGB]");
        }, drawsShaders);

    /// <summary>What a tiling's cell draws, replayed as it stands onto a device of its own.</summary>
    private static string[] CellOf(RecordingDevice.Call call)
    {
        var device = new RecordingDevice();
        call.ShaderAs<PageTiling>().Cell.Replay(device, XMatrix.Identity).Should().BeTrue();
        return device.Lines.ToArray();
    }

    [Test]
    public void AColouredCellIsRecordedOnceInPatternSpaceAndSaysWhatItDraws()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 3 0 0 3 0 0 cm 0 0 30 30 re f 0 0 5 5 re f",
            "1 0 0 rg 0 0 4 4 re f", more: ("/Matrix", "[2 0 0 2 10 0]"));

        var fills = drawn.Device.Fills.ToList();
        fills.Select(fill => fill.ToString()).Should().Equal(
            "fill nonzero M0,100 L90,100 L90,10 L0,10 Z tiling",
            "fill nonzero M0,100 L15,100 L15,85 L0,85 Z tiling");

        var tiling = fills[0].ShaderAs<PageTiling>();
        fills[1].ShaderAs<PageTiling>().Should().BeSameAs(tiling);
        tiling.IsShared.Should().BeTrue();
        tiling.Tile.Should().Be(new XRect(0, 0, 8, 8));
        fills[0].Shader!.Value.Transform.Should().Be(new XMatrix(2, 0, 0, -2, 10, 100));

        // In the pattern's own coordinates: neither the pattern's matrix nor the page's is in it.
        CellOf(fills[0]).Should().Equal(
            "save",
            "clip nonzero " + CellBox,
            "fill nonzero M0,0 L4,0 L4,4 L0,4 Z rgb(1,0,0)",
            "restore");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void APictureIsReplayedUnderTheTransformItIsGiven()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "1 0 0 rg 0 0 4 4 re f");

        var device = new RecordingDevice();
        var cell = drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell;
        cell.Replay(device, new XMatrix(2, 0, 0, 2, 100, 0));

        cell.IsEmpty.Should().BeFalse();
        device.Lines.Should().Contain("fill nonzero M100,0 L108,0 L108,8 L100,8 Z rgb(1,0,0)");
    }

    [Test]
    public void AnUncolouredCellIsRecordedInTheColourItIsPaintedInAndItsOwnColoursArePassedOver()
    {
        var drawn = WithTiling(
            "/Tinted cs 0 0 1 /T0 scn 0 0 5 5 re f 0 1 0 /T0 scn 0 0 5 5 re f 0 0 1 /T0 scn 0 0 5 5 re f",
            "1 0 0 rg 0 0 4 4 re f 1 0 0 RG 0 0 m 8 8 l S", more: ("/PaintType", "2"));

        var fills = drawn.Device.Fills.ToList();
        CellOf(fills[0]).Should().Equal(
            "save",
            "clip nonzero " + CellBox,
            "fill nonzero M0,0 L4,0 L4,4 L0,4 Z rgb(0,0,1)",
            "stroke M0,0 L8,8 rgb(0,0,1)",
            "restore");
        CellOf(fills[1]).Should().Contain("fill nonzero M0,0 L4,0 L4,4 L0,4 Z rgb(0,1,0)");

        fills[1].Shader!.Value.Shader.Should().NotBeSameAs(fills[0].Shader!.Value.Shader);
        fills[2].Shader!.Value.Shader.Should().BeSameAs(fills[0].Shader!.Value.Shader, "one recording serves one colour");
    }

    [Test]
    public void AnUncolouredPatternWithNoColourToPaintItInPaintsNothingAndIsReported()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "0 0 4 4 re f", more: ("/PaintType", "2"));

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue scn");
    }

    [Test]
    public void AStrokeInATilingPatternIsAStrokeWithTheShader()
    {
        var drawn = WithTiling("/Pattern CS /T0 SCN 0 0 m 50 0 l S", "0 0 4 4 re f");

        drawn.Device.Body.Should().Equal("stroke M0,100 L50,100 tiling");
    }

    [Test]
    public void AStepLongerThanTheCellLeavesTheTileEmptyRoundIt()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "0 0 4 4 re f", more: [("/XStep", "20"), ("/YStep", "12")]);

        var fill = drawn.Device.Fills.Single();
        fill.ShaderAs<PageTiling>().Tile.Should().Be(new XRect(0, 0, 20, 12));
        CellOf(fill).Should().HaveCount(4).And.Contain("clip nonzero " + CellBox);
    }

    [Test]
    public void AStepShorterThanTheCellDrawsTheNeighboursThatReachIntoTheTile()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "1 0 0 rg 0 0 8 8 re f", more: ("/XStep", "4"));

        var fill = drawn.Device.Fills.Single();
        fill.ShaderAs<PageTiling>().Tile.Should().Be(new XRect(0, 0, 4, 8));

        // The cell one step to the left reaches into the tile, and is under the tile's own.
        CellOf(fill).Should().Equal(
            "save",
            "clip nonzero M0,0 L4,0 L4,8 L0,8 Z",
            "save",
            "clip nonzero M-4,0 L4,0 L4,8 L-4,8 Z",
            "fill nonzero M-4,0 L4,0 L4,8 L-4,8 Z rgb(1,0,0)",
            "restore",
            "save",
            "clip nonzero " + CellBox,
            "fill nonzero " + CellBox + " rgb(1,0,0)",
            "restore",
            "restore");
    }

    [Test]
    public void ANegativeStepTilesAsItsSizeDoes()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "0 0 4 4 re f", more: [("/XStep", "-8"), ("/YStep", "-10")]);

        drawn.Device.Fills.Single().ShaderAs<PageTiling>().Tile.Should().Be(new XRect(0, 0, 8, 10));
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void ShInACellFillsTheCellAndNotThePage()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "/Sh0 sh");

        CellOf(drawn.Device.Fills.Single()).Should().Equal(
            "save",
            "clip nonzero " + CellBox,
            "fill nonzero " + CellBox + " axial",
            "restore");
    }

    [Test]
    public void APatternNamedInACellLiesInTheCellsSpace()
    {
        var drawn = Drawn.Saved("/Pattern cs /T0 scn 5 0 0 5 0 0 cm 0 0 5 5 re f", (document, resources) =>
        {
            Drawn.Add(document, resources, "/Pattern", "/P0",
                ShadingTests.PatternOf(document, ShadingTests.Axial(document), "[1 0 0 1 3 0]"));
            Drawn.Add(document, resources, "/Pattern", "/T0",
                Tiling(document, "/Pattern cs /P0 scn 2 0 0 2 0 0 cm 0 0 2 2 re f", ("/Matrix", "[4 0 0 4 0 0]")));
        });

        var device = new RecordingDevice();
        drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell.Replay(device, XMatrix.Identity);

        // The inner pattern's matrix alone: the cell's space is the outer pattern's, whatever lies outside it.
        var inner = device.Fills.Single();
        inner.ShaderAs<PageAxialShading>().Should().NotBeNull();
        inner.Shader!.Value.Transform.Should().Be(new XMatrix(1, 0, 0, 1, 3, 0));
    }

    [Test]
    public void ACellThatPaintsWithItsOwnPatternIsDrawnOnceAndThePatternInItPaintsNothing()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f",
            "/Pattern cs /T0 scn 0 0 8 8 re f 1 0 0 rg 0 0 4 4 re f");

        CellOf(drawn.Device.Fills.Single()).Should().Equal(
            "save",
            "clip nonzero " + CellBox,
            "fill nonzero M0,0 L4,0 L4,4 L0,4 Z rgb(1,0,0)",
            "restore");
        drawn.Problems.Should().Equal("LimitReached /T0");
    }

    [Test]
    public void ADeviceThatDrawsNoShadersIsHandedOneColourForATilingAndTheRenderSaysSo()
    {
        var drawn = WithTiling(
            "/Pattern cs /T0 scn 0 0 5 5 re f /Tinted cs 0 0 1 /T1 scn 0 0 5 5 re f", "1 0 0 rg 0 0 4 4 re f 0 0 1 rg 4 4 4 4 re f",
            drawsShaders: false);

        // A coloured cell's colours, each call counted once.
        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0.5,0,0.5)");
        drawn.Problems.Should().Equal("UnsupportedFeature /Pattern", "MissingResource /T1");
    }

    [Test]
    public void APictureReplayedOnADeviceThatDrawsNoShadersDrawsOneColourAndSaysSo()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "/Sh0 sh");

        var device = new RecordingDevice { DrawsShaders = false };
        var complete = drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell.Replay(device, XMatrix.Identity);

        complete.Should().BeFalse();
        device.Lines.Should().Contain("fill nonzero " + CellBox + " rgb(0.5,0,0.5)");
    }

    [Test]
    public void APictureRestoresWhatItSavedWhenTheDeviceThrows()
    {
        var drawn = WithTiling("/Pattern cs /T0 scn 0 0 5 5 re f", "q q 0 0 4 4 re f Q Q");
        var cell = drawn.Device.Fills.Single().ShaderAs<PageTiling>().Cell;

        var device = new ThrowingOnFill();
        var replay = () => cell.Replay(device, XMatrix.Identity);

        replay.Should().Throw<InvalidOperationException>();
        device.Depth.Should().Be(0);
    }

    private sealed class ThrowingOnFill : PageDevice
    {
        public int Depth { get; private set; }

        public override void Save() => Depth++;

        public override void Restore() => Depth--;

        public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
        {
        }

        public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint) =>
            throw new InvalidOperationException("No fills here.");

        public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint)
        {
        }

        public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint)
        {
        }
    }
}
