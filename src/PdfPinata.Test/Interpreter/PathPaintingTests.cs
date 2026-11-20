using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Path construction, painting and clipping, as a device is handed them.
/// </summary>
public class PathPaintingTests
{
    [Test]
    public void EveryRenderIsBracketedByASaveAClipToThePageAndARestore()
    {
        var drawn = Drawn.Page("");

        drawn.Device.Lines.Should().Equal("save", "clip nonzero M0,100 L200,100 L200,0 L0,0 Z", "restore");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void ARectangleIsFilledByNonzeroWindingInTheFillColour()
    {
        var drawn = Drawn.Page("1 0 0 rg 10 10 30 20 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M10,90 L40,90 L40,70 L10,70 Z rgb(1,0,0)");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void FStarFillsByTheEvenOddRule()
    {
        var drawn = Drawn.Page("0 0 50 50 re 10 10 30 30 re f*");

        drawn.Device.Body.Should().Equal(
            "fill evenodd M0,100 L50,100 L50,50 L0,50 Z M10,90 L40,90 L40,60 L10,60 Z rgb(0,0,0)");
    }

    [Test]
    public void LinesAndCurvesAreHandedOverAsTheyAreWritten()
    {
        var drawn = Drawn.Page("10 10 m 20 10 l 30 10 30 20 40 20 c h f");

        drawn.Device.Body.Should().Equal("fill nonzero M10,90 L20,90 C30,90 30,80 40,80 Z rgb(0,0,0)");
    }

    [Test]
    public void VTakesTheCurrentPointForItsFirstControlPointAndYTheEndPointForItsSecond()
    {
        var drawn = Drawn.Page("10 10 m 20 20 30 10 v 40 0 50 10 y S");

        drawn.Device.Body.Should().Equal("stroke M10,90 C10,90 20,80 30,90 C40,100 50,90 50,90 rgb(0,0,0)");
    }

    [Test]
    public void AStrokeCarriesTheLineStateInTheCoordinatesOfThePath()
    {
        var drawn = Drawn.Page("2 0 0 2 0 0 cm 3 w 1 J 2 j 4 M [3 1] 2 d 0 0 1 RG 10 10 m 40 10 l S");

        var stroke = drawn.Device.Strokes.Single();
        stroke.DevicePath.Should().Be("M20,80 L80,80");
        stroke.Paint.Blue.Should().Be(1);
        stroke.Stroke.Width.Should().Be(3, "the width is in the path's coordinates, and the transform doubles it");
        stroke.Stroke.LineCap.Should().Be(XLineCap.Round);
        stroke.Stroke.LineJoin.Should().Be(XLineJoin.Bevel);
        stroke.Stroke.MiterLimit.Should().Be(4);
        stroke.Stroke.DashPattern.Should().Equal(3, 1);
        stroke.Stroke.DashPhase.Should().Be(2);
        stroke.Transform.M11.Should().Be(2);
    }

    [Test]
    public void TheLineStateStartsOneUnitWideFlatMitredAndSolid()
    {
        var stroke = Drawn.Page("0 0 m 10 0 l S").Device.Strokes.Single().Stroke;

        stroke.Width.Should().Be(1);
        stroke.LineCap.Should().Be(XLineCap.Flat);
        stroke.LineJoin.Should().Be(XLineJoin.Miter);
        stroke.MiterLimit.Should().Be(10);
        stroke.DashPattern.Should().BeEmpty();
    }

    [Test]
    public void ADashOfAnOddNumberOfLengthsIsHandedOverTwice()
    {
        var stroke = Drawn.Page("[2 1 3] 0 d 0 0 m 10 0 l S").Device.Strokes.Single().Stroke;

        stroke.DashPattern.Should().Equal(2, 1, 3, 2, 1, 3);
    }

    [Test]
    public void ADashOfNothingButZerosIsASolidLine()
    {
        var stroke = Drawn.Page("[3 1] 0 d [0 0] 0 d 0 0 m 10 0 l S").Device.Strokes.Single().Stroke;

        stroke.DashPattern.Should().BeEmpty();
    }

    [Test]
    public void SmallSClosesThePathBeforeStrokingIt()
    {
        var drawn = Drawn.Page("0 0 m 10 0 l 10 10 l s");

        drawn.Device.Body.Should().Equal("stroke M0,100 L10,100 L10,90 Z rgb(0,0,0)");
    }

    [Test]
    public void BFillsAndThenStrokesTheSamePath()
    {
        var drawn = Drawn.Page("1 0 0 rg 0 1 0 RG 0 0 10 10 re B");

        drawn.Device.Body.Should().Equal(
            "fill nonzero M0,100 L10,100 L10,90 L0,90 Z rgb(1,0,0)",
            "stroke M0,100 L10,100 L10,90 L0,90 Z rgb(0,1,0)");
    }

    [Test]
    public void TheClosingAndEvenOddFormsOfBEachDoWhatTheirNameSays()
    {
        Drawn.Page("0 0 m 10 0 l 10 10 l b").Device.Body.Should().Equal(
            "fill nonzero M0,100 L10,100 L10,90 Z rgb(0,0,0)", "stroke M0,100 L10,100 L10,90 Z rgb(0,0,0)");
        Drawn.Page("0 0 m 10 0 l 10 10 l b*").Device.Body.Should().Equal(
            "fill evenodd M0,100 L10,100 L10,90 Z rgb(0,0,0)", "stroke M0,100 L10,100 L10,90 Z rgb(0,0,0)");
        Drawn.Page("0 0 m 10 0 l 10 10 l B*").Device.Body.Should().Equal(
            "fill evenodd M0,100 L10,100 L10,90 rgb(0,0,0)", "stroke M0,100 L10,100 L10,90 rgb(0,0,0)");
    }

    [Test]
    public void NEndsThePathWithoutPaintingIt()
    {
        var drawn = Drawn.Page("0 0 10 10 re n 20 20 5 5 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M20,80 L25,80 L25,75 L20,75 Z rgb(0,0,0)");
    }

    [Test]
    public void AClipTakesEffectAfterThePaintingOperatorThatFollowsTheW()
    {
        var drawn = Drawn.Page("0 0 10 10 re W f 0 0 100 100 re f");

        drawn.Device.Body.Should().Equal(
            "fill nonzero M0,100 L10,100 L10,90 L0,90 Z rgb(0,0,0)",
            "clip nonzero M0,100 L10,100 L10,90 L0,90 Z",
            "fill nonzero M0,100 L100,100 L100,0 L0,0 Z rgb(0,0,0)");
    }

    [Test]
    public void WStarClipsByTheEvenOddRuleWhateverThePaintingOperatorFillsBy()
    {
        var drawn = Drawn.Page("0 0 10 10 re W* f");

        drawn.Device.Body.Should().Equal(
            "fill nonzero M0,100 L10,100 L10,90 L0,90 Z rgb(0,0,0)",
            "clip evenodd M0,100 L10,100 L10,90 L0,90 Z");
    }

    [Test]
    public void TheUsualClipIsAWAndAnNWhichPaintsNothing()
    {
        var drawn = Drawn.Page("5 5 10 10 re W n");

        drawn.Device.Body.Should().Equal("clip nonzero M5,95 L15,95 L15,85 L5,85 Z");
    }

    [Test]
    public void AClipToAPathWithNothingInItClipsEverythingAway()
    {
        var drawn = Drawn.Page("W n");

        var clip = drawn.Device.Clips.Single();
        clip.Path.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void AClipIsUndoneByTheRestoreThatMatchesTheSaveBeforeIt()
    {
        var drawn = Drawn.Page("q 0 0 10 10 re W n Q 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(
            "save",
            "clip nonzero M0,100 L10,100 L10,90 L0,90 Z",
            "restore",
            "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
    }

    [Test]
    public void ALineBeforeAnyPointIsReportedAndNotDrawn()
    {
        var drawn = Drawn.Page("10 10 l 20 20 l S 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Problems.Should().Equal("OperatorOutOfPlace l");
        drawn.Result.Diagnostics.Single().Occurrences.Should().Be(2);
    }

    [Test]
    public void ALineAfterAFigureIsClosedStartsFromWhereThatFigureBegan()
    {
        var drawn = Drawn.Page("10 10 m 20 10 l h 15 20 l S");

        drawn.Device.Body.Should().Equal("stroke M10,90 L20,90 Z M10,90 L15,80 rgb(0,0,0)");
    }

    [Test]
    public void APathOfTheContentsOwnIsNotOneADeviceIsHandedAgain()
    {
        var drawn = Drawn.Page("0 0 10 10 re f");

        drawn.Device.Fills.Single().Path.IsShared.Should().BeFalse();
    }
}
