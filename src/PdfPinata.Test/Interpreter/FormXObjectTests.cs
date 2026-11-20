using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Form XObjects, and the two things a <c>Do</c> may name that are not drawn yet.
/// </summary>
public class FormXObjectTests
{
    [Test]
    public void AFormIsDrawnUnderItsMatrixAndClippedToItsBox()
    {
        var drawn = Drawn.Page("1 0 0 1 10 0 cm /Fm1 Do", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            form.Elements.SetMatrix("/Matrix", new XMatrix(2, 0, 0, 2, 0, 20));
            form.Elements["/BBox"] = new PdfRectangle(new XRect(0, 0, 10, 10));
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        // The form's space is doubled and raised by 20, inside a page space moved right by 10.
        drawn.Device.Body.Should().Equal(
            "save",
            "clip nonzero M10,80 L30,80 L30,60 L10,60 Z",
            "fill nonzero M10,80 L20,80 L20,70 L10,70 Z rgb(0,0,0)",
            "restore");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void WhatAFormChangesInTheGraphicsStateIsUndoneWhenItEnds()
    {
        var drawn = Drawn.Page("/Fm1 Do 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "1 0 0 rg 3 0 0 3 0 0 cm q q")));

        drawn.Device.Fills.Single().ToString().Should().Be("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Device.Calls.Count(call => call.Kind == "save")
            .Should().Be(drawn.Device.Calls.Count(call => call.Kind == "restore"));
    }

    [Test]
    public void ARestoreInAFormCannotReachPastWhereTheFormBegan()
    {
        var drawn = Drawn.Page("q 1 0 0 rg /Fm1 Do 0 0 5 5 re f Q", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "Q Q Q")));

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0), "the page's own saved state is still the page's");
        drawn.Problems.Should().Equal("OperatorOutOfPlace Q");
        drawn.Result.Diagnostics.Single().Stream.Should().Be("page/Fm1");
    }

    [Test]
    public void AFormStartsFromTheGraphicsStateInForceWhereItIsDrawn()
    {
        var drawn = Drawn.Page("0 1 0 rg /Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f")));

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(0, 1, 0));
    }

    [Test]
    public void AFormLooksNamesUpInItsOwnResources()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            var inner = Drawn.Form(document, "1 0 0 rg 0 0 5 5 re f");
            var outer = Drawn.Form(document, "/Inner Do");
            var own = new PdfDictionary(document);
            outer.Elements["/Resources"] = own;
            Drawn.Add(document, own, "/XObject", "/Inner", inner);
            Drawn.Add(document, resources, "/XObject", "/Fm1", outer);
        });

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AFormWithNoResourcesOfItsOwnUsesThoseOfWhateverDrewIt()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            Drawn.Add(document, resources, "/XObject", "/Inner", Drawn.Form(document, "0 0 5 5 re f"));
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "/Inner Do"));
        });

        drawn.Device.Fills.Should().ContainSingle();
    }

    [Test]
    public void AFormThatDrawsItselfIsDrawnOnceAndReported()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f /Fm1 Do")));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("LimitReached /Fm1");
    }

    [Test]
    public void AFormWithNoBoxIsDrawnUnclippedAndReported()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            form.Elements.Remove("/BBox");
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Body.Should().Equal("save", "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)", "restore");
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
    }

    [Test]
    public void AFormWhoseMatrixIsNotSixNumbersIsNotDrawn()
    {
        var drawn = Drawn.Page("/Fm1 Do 0 0 5 5 re f", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 50 50 re f");
            form.Elements["/Matrix"] = new PdfArray(document, new PdfReal(1), new PdfReal(0));
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
    }

    [Test]
    public void AnXObjectTheResourcesDoNotHoldIsReported()
    {
        Drawn.Page("/Nope Do").Problems.Should().Equal("MissingResource /Nope");
    }

    [Test]
    public void AnImageXObjectIsReportedBecauseImagesAreNotDrawnYet()
    {
        var drawn = Drawn.Page("/Im1 Do 0 0 5 5 re f", (document, resources) =>
        {
            var image = new PdfDictionary(document);
            image.Elements.SetName("/Subtype", "/Image");
            image.CreateStream([0]);
            Drawn.Add(document, resources, "/XObject", "/Im1", image);
        });

        drawn.Device.Fills.Should().ContainSingle("the page is drawn but for the image");
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
    }

    [Test]
    public void AnInlineImageIsReportedAndWhatFollowsItIsDrawn()
    {
        var drawn = Drawn.Page("BI /W 1 /H 1 /CS /G /BPC 8 ID \u00ff\nEI 0 0 5 5 re f");

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedImage BI");
    }
}
