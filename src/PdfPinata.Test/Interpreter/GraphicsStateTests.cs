using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// The graphics-state stack, the transform, colour and the parameters of <c>/ExtGState</c>.
/// </summary>
public class GraphicsStateTests
{
    [Test]
    public void ATransformIsAppliedBeforeTheOneAlreadyInForce()
    {
        // Scaled and then moved: the move is in the scaled space, so it is doubled too.
        var drawn = Drawn.Page("2 0 0 2 0 0 cm 1 0 0 1 10 10 cm 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M20,80 L30,80 L30,70 L20,70 Z rgb(0,0,0)");
    }

    [Test]
    public void ATransformChangedInTheMiddleOfAPathMovesThePenAndNotThePointsBeforeIt()
    {
        var drawn = Drawn.Page("0 0 m 10 0 l 2 0 0 2 50 0 cm 10 0 l 4 w S");

        // The first two points stay where they were placed. The third is placed by the new
        // transform, and the line is as wide as the new transform makes it.
        var stroke = drawn.Device.Strokes.Single();
        stroke.DevicePath.Should().Be("M0,100 L10,100 L70,100");
        stroke.Transform.M11.Should().Be(2);
        stroke.Stroke.Width.Should().Be(4);
    }

    [Test]
    public void ARestoreInTheMiddleOfAPathLeavesItsPointsWhereTheyWere()
    {
        var drawn = Drawn.Page("q 1 0 0 1 50 0 cm 0 0 m 10 0 l Q 10 10 l S");

        drawn.Device.Strokes.Single().DevicePath.Should().Be("M50,100 L60,100 L10,90");
    }

    [Test]
    public void ACurveFromTheCurrentPointStartsWhereThePointWasPlacedAndNotWhereTheNewTransformPutsIt()
    {
        var drawn = Drawn.Page("10 0 m 2 0 0 2 0 0 cm 10 10 20 10 v S");

        // The current point was placed before the transform changed and is the first control
        // point as it stands. The other two are placed by the new transform.
        drawn.Device.Strokes.Single().DevicePath.Should().Be("M10,100 C10,100 20,80 40,80");
    }

    [Test]
    public void ARectangleAddedAfterTheTransformChangesIsPlacedByTheNewTransform()
    {
        var drawn = Drawn.Page("0 0 5 5 re 2 0 0 2 50 0 cm 0 0 5 5 re f");

        drawn.Device.Fills.Single().DevicePath.Should().Be(
            "M0,100 L5,100 L5,95 L0,95 Z M50,100 L60,100 L60,90 L50,90 Z");
    }

    [Test]
    public void APathPaintedUnderTheTransformItBeganUnderIsHandedOverAsItWasWritten()
    {
        var drawn = Drawn.Page("2 0 0 2 0 0 cm 1 1 m q 3 0 0 3 0 0 cm 1 1 l Q 5 5 l S");

        // Only the point added under another transform is rewritten: the others are the
        // content's own numbers, exactly.
        var stroke = drawn.Device.Strokes.Single();
        stroke.Path.Points.ToArray().Should().Equal(new XPoint(1, 1), new XPoint(3, 3), new XPoint(5, 5));
        stroke.Transform.M11.Should().Be(2);
    }

    [Test]
    public void APathBegunUnderATransformWithNoInverseIsStillPainted()
    {
        var drawn = Drawn.Page("q 1 0 0 0 0 0 cm 0 0 m 10 10 l Q 20 20 l S");

        // The points under the flattening transform land on one line, and the last is placed
        // by the transform restored before it.
        drawn.Device.Strokes.Single().ToString().Should().Be("stroke M0,100 L10,100 L20,80 rgb(0,0,0)");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void APathBegunUnderTwoTransformsWithNoInverseIsPaintedWhereTheyPutIt()
    {
        var drawn = Drawn.Page("q 1 0 0 0 0 0 cm 0 0 m 10 10 l Q q 0 0 0 1 0 0 cm 20 20 l Q 30 30 l S");

        drawn.Device.Strokes.Single().ToString().Should().Be("stroke M0,100 L10,100 L0,80 L30,70 rgb(0,0,0)");
    }

    [Test]
    public void APathPaintedUnderTheTransformWithNoInverseItWasBuiltUnderIsHandedOverAsWritten()
    {
        var drawn = Drawn.Page("1 0 0 0 0 0 cm 0 0 m 10 10 l S");

        drawn.Device.Strokes.Single().Path.Points.ToArray().Should().Equal(new XPoint(0, 0), new XPoint(10, 10));
    }

    [Test]
    public void RestoringPutsBackTheTransformTheColourAndTheLineState()
    {
        var drawn = Drawn.Page("q 1 0 0 1 50 0 cm 1 0 0 rg 5 w 0 0 5 5 re B Q 0 0 5 5 re B");

        var fills = drawn.Device.Fills.ToArray();
        fills[0].ToString().Should().Be("fill nonzero M50,100 L55,100 L55,95 L50,95 Z rgb(1,0,0)");
        fills[1].ToString().Should().Be("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Device.Strokes.Select(stroke => stroke.Stroke.Width).Should().Equal(5, 1);
    }

    [Test]
    public void ARestoreWithNothingSavedIsReportedAndChangesNothing()
    {
        var drawn = Drawn.Page("1 0 0 rg Q 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(1,0,0)");
        drawn.Problems.Should().Equal("OperatorOutOfPlace Q");
    }

    [Test]
    public void StatesLeftSavedAreRestoredOnTheDeviceWhenThePageEnds()
    {
        var drawn = Drawn.Page("q q q 0 0 5 5 re f");

        drawn.Device.Calls.Count(call => call.Kind == "save").Should().Be(4);
        drawn.Device.Calls.Count(call => call.Kind == "restore").Should().Be(4);
        drawn.Device.Calls[^1].Kind.Should().Be("restore");
        drawn.Result.IsComplete.Should().BeTrue("a page that ends with states saved is drawn in full");
    }

    [Test]
    public void GraySetsAllThreeOfRedGreenAndBlue()
    {
        var drawn = Drawn.Page("0.5 g 0.25 G 0 0 5 5 re B");

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(0.5, 0.5, 0.5));
        drawn.Device.Strokes.Single().Paint.Should().Be(new PagePaint(0.25, 0.25, 0.25));
    }

    [Test]
    public void CmykIsTurnedToRgbByComplementAndMultiply()
    {
        var drawn = Drawn.Page("0.2 0.4 0.6 0.5 k 0 0 5 5 re f");

        var paint = drawn.Device.Fills.Single().Paint;
        paint.Red.Should().BeApproximately(0.8 * 0.5, 1e-12);
        paint.Green.Should().BeApproximately(0.6 * 0.5, 1e-12);
        paint.Blue.Should().BeApproximately(0.4 * 0.5, 1e-12);
    }

    [Test]
    public void AComponentOutOfRangeIsHeldToItsRange()
    {
        var drawn = Drawn.Page("2 -1 0.5 rg 0 0 5 5 re f");

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0.5));
    }

    [Test]
    public void SelectingADeviceSpaceSetsItsInitialColourAndScSetsAColourInIt()
    {
        var drawn = Drawn.Page("1 0 0 rg /DeviceCMYK cs 0 0 5 5 re f 1 0 0 0 sc 0 0 5 5 re f /DeviceRGB CS 0 1 0 SC 0 0 m 5 5 l S");

        drawn.Device.Fills.Select(fill => fill.Paint).Should().Equal(new PagePaint(0, 0, 0), new PagePaint(0, 1, 1));
        drawn.Device.Strokes.Single().Paint.Should().Be(new PagePaint(0, 1, 0));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AColourSpaceThatIsNotResolvedYetIsReportedAndDrawnInTheFallbackColour()
    {
        var drawn = Drawn.Page("1 0 0 rg /Cs1 cs 0.5 scn 0 0 5 5 re f", (document, resources) =>
        {
            var separation = new PdfArray(document, new PdfName("/Separation"), new PdfName("/Spot"),
                new PdfName("/DeviceRGB"), new PdfDictionary(document));
            document.Internals.AddObject(separation);
            Drawn.Add(document, resources, "/ColorSpace", "/Cs1", separation);
        });

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("UnsupportedColorSpace /Separation");
    }

    [Test]
    public void AColourSpaceTheResourcesDoNotHoldLeavesTheColourAsItWas()
    {
        var drawn = Drawn.Page("1 0 0 rg /Nope cs 0 0 5 5 re f");

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0));
        drawn.Problems.Should().Equal("MissingResource /Nope");
    }

    [Test]
    public void AColourWithTooFewComponentsForItsSpaceIsSkipped()
    {
        var drawn = Drawn.Page("/DeviceRGB cs 1 1 sc 0 0 5 5 re f");

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(0, 0, 0));
        drawn.Problems.Should().Equal("WrongOperands sc");
    }

    [Test]
    public void AGraphicsStateDictionarySetsTheLineStateAndBothAlphas()
    {
        var drawn = Drawn.Page("/GS1 gs 1 0 0 rg 0 0 1 RG 0 0 5 5 re B", (document, resources) =>
        {
            var state = new PdfDictionary(document);
            state.Elements.SetReal("/LW", 4);
            state.Elements.SetInteger("/LC", 2);
            state.Elements.SetInteger("/LJ", 1);
            state.Elements.SetReal("/ML", 3);
            state.Elements["/D"] = new PdfArray(document, new PdfArray(document, new PdfReal(5), new PdfReal(2)),
                new PdfReal(1));
            state.Elements.SetReal("/CA", 0.25);
            state.Elements.SetReal("/ca", 0.5);
            Drawn.Add(document, resources, "/ExtGState", "/GS1", state);
        });

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0, 0.5));
        var stroke = drawn.Device.Strokes.Single();
        stroke.Paint.Should().Be(new PagePaint(0, 0, 1, 0.25));
        stroke.Stroke.Width.Should().Be(4);
        stroke.Stroke.LineCap.Should().Be(XLineCap.Square);
        stroke.Stroke.LineJoin.Should().Be(XLineJoin.Round);
        stroke.Stroke.MiterLimit.Should().Be(3);
        stroke.Stroke.DashPattern.Should().Equal(5, 2);
        stroke.Stroke.DashPhase.Should().Be(1);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void TheAlphaOfAGraphicsStateDictionaryIsSavedAndRestoredLikeTheRest()
    {
        var drawn = Drawn.Page("q /GS1 gs 0 0 5 5 re f Q 0 0 5 5 re f", (document, resources) =>
        {
            var state = new PdfDictionary(document);
            state.Elements.SetReal("/ca", 0.5);
            Drawn.Add(document, resources, "/ExtGState", "/GS1", state);
        });

        drawn.Device.Fills.Select(fill => fill.Paint.Alpha).Should().Equal(0.5, 1);
    }

    [Test]
    public void ABlendModeAndASoftMaskAreReportedBecauseTheyAreNotApplied()
    {
        var drawn = Drawn.Page("/GS1 gs 0 0 5 5 re f", (document, resources) =>
        {
            var state = new PdfDictionary(document);
            state.Elements.SetName("/BM", "/Multiply");
            state.Elements["/SMask"] = new PdfDictionary(document);
            Drawn.Add(document, resources, "/ExtGState", "/GS1", state);
        });

        drawn.Device.Fills.Should().ContainSingle("what cannot be blended is still drawn");
        drawn.Problems.Should().Equal("UnsupportedFeature /BM", "UnsupportedFeature /SMask");
    }

    [Test]
    public void AGraphicsStateDictionaryTheResourcesDoNotHoldIsReported()
    {
        Drawn.Page("/GS9 gs").Problems.Should().Equal("MissingResource /GS9");
    }

    [Test]
    public void AValueOutOfRangeIsReportedAndLeftAsItWas()
    {
        var drawn = Drawn.Page("3 w -2 w 7 J 0.5 M [1 -1] 0 d 0 0 m 5 5 l S");

        var stroke = drawn.Device.Strokes.Single().Stroke;
        stroke.Width.Should().Be(3);
        stroke.LineCap.Should().Be(XLineCap.Flat);
        stroke.MiterLimit.Should().Be(10);
        stroke.DashPattern.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue w", "InvalidValue J", "InvalidValue M", "InvalidValue d");
    }

    [Test]
    public void AShadingIsReportedBecauseItIsNotDrawn()
    {
        Drawn.Page("/Sh1 sh").Problems.Should().Equal("UnsupportedFeature sh");
    }
}
