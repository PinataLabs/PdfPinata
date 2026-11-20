using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
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

    // On a thread of its own and under a timeout: a guard that stopped guarding would recurse
    // until the stack ran out, which ends the test host and fails no test.
    [Test, Timeout(30_000)]
    public async Task AFormThatDrawsItselfIsDrawnOnceAndReported()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("/Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f /Fm1 Do"))));

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

    private static Drawn GroupDrawnUnder(string state, Action<PdfDictionary> group = null) =>
        Drawn.Page("/GS1 gs /Fm1 Do", (document, resources) =>
        {
            var parameters = new PdfDictionary(document);
            foreach (var entry in state.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                parameters.Elements.SetReal(entry.Split('=')[0], double.Parse(entry.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture));
            Drawn.Add(document, resources, "/ExtGState", "/GS1", parameters);

            var form = Drawn.Form(document, "0 0 50 50 re f 25 25 50 50 re f");
            var transparency = new PdfDictionary(document);
            transparency.Elements.SetName("/S", "/Transparency");
            group?.Invoke(transparency);
            form.Elements["/Group"] = transparency;
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

    [Test]
    [Arguments("/ca=0.5")]
    [Arguments("/CA=0.5")]
    public void ATransparencyGroupDrawnAtAnAlphaIsReportedBecauseItIsDrawnPieceByPiece(string state)
    {
        var drawn = GroupDrawnUnder(state);

        // Two fills that overlap, each at the alpha, where a group is one thing at the alpha.
        drawn.Device.Fills.Should().HaveCount(2);
        drawn.Problems.Should().Equal("UnsupportedFeature /Group");
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test]
    public void ATransparencyGroupDrawnWithNoAlphaIsDrawnAsAGroupWouldBeAndIsNotReported()
    {
        var drawn = GroupDrawnUnder("/ca=1 /CA=1");

        drawn.Device.Fills.Should().HaveCount(2);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AKnockoutGroupIsReportedWhateverTheAlpha()
    {
        var drawn = GroupDrawnUnder("/ca=1", group => group.Elements.SetBoolean("/K", true));

        drawn.Problems.Should().Equal("UnsupportedFeature /Group");
    }

    /// <summary>
    /// A page whose content is in the optional content group <c>/L1</c>, with a second group
    /// <c>/L2</c> beside it, in a document whose default configuration is <paramref name="configure"/>d.
    /// </summary>
    private static Drawn Layered(string content, Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> configure,
        Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> more = null) =>
        Drawn.Page(content, (document, resources) =>
        {
            var first = Group(document);
            var second = Group(document);
            Drawn.Add(document, resources, "/Properties", "/L1", first);
            Drawn.Add(document, resources, "/Properties", "/L2", second);

            var configuration = new PdfDictionary(document);
            configure(document, configuration, first, second);
            var properties = new PdfDictionary(document);
            properties.Elements["/OCGs"] = new PdfArray(document, first.Reference, second.Reference);
            properties.Elements["/D"] = configuration;
            document.Internals.Catalog.Elements["/OCProperties"] = properties;
            more?.Invoke(document, resources, first, second);
        });

    private static PdfDictionary Group(PdfDocument document)
    {
        var group = new PdfDictionary(document);
        group.Elements.SetName("/Type", "/OCG");
        document.Internals.AddObject(group);
        return group;
    }

    private const string InFirstGroup = "/OC /L1 BDC 0 0 5 5 re f EMC";

    [Test]
    public void ContentOfAGroupTheDocumentSwitchesOffIsDrawnAndReported()
    {
        var drawn = Layered(InFirstGroup, (document, configuration, first, _) =>
            configuration.Elements["/OFF"] = new PdfArray(document, first.Reference));

        drawn.Device.Fills.Should().ContainSingle("optional content is not applied yet");
        drawn.Problems.Should().Equal("UnsupportedFeature /OC");
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test]
    public void ContentOfAGroupThatIsOnIsNotReported()
    {
        var drawn = Layered(InFirstGroup, (document, configuration, _, second) =>
            configuration.Elements["/OFF"] = new PdfArray(document, second.Reference));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void WhereEveryGroupStartsOffOnlyTheGroupsListedAreOn()
    {
        var on = Layered(InFirstGroup, (document, configuration, first, _) =>
        {
            configuration.Elements.SetName("/BaseState", "/OFF");
            configuration.Elements["/ON"] = new PdfArray(document, first.Reference);
        });
        var off = Layered(InFirstGroup, (_, configuration, _, _) => configuration.Elements.SetName("/BaseState", "/OFF"));

        on.Result.IsComplete.Should().BeTrue();
        off.Problems.Should().Equal("UnsupportedFeature /OC");
    }

    [Test]
    [Arguments(null, "/L1", true)]
    [Arguments(null, "/L1 /L2", false)]
    [Arguments("/AnyOn", "/L1 /L2", false)]
    [Arguments("/AllOn", "/L1 /L2", true)]
    [Arguments("/AnyOff", "/L1 /L2", false)]
    [Arguments("/AnyOff", "/L2", true)]
    [Arguments("/AllOff", "/L1", false)]
    [Arguments("/AllOff", "/L1 /L2", true)]
    public void AMembershipDictionaryIsOffByItsPolicyOverItsGroups(string policy, string groups, bool reported)
    {
        // The first group is off and the second is on.
        var drawn = Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, second) =>
            {
                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/OCGs"] = new PdfArray(document,
                    groups.Split(' ').Select(name => (PdfItem)(name == "/L1" ? first : second).Reference).ToArray());
                if (policy != null)
                    membership.Elements.SetName("/P", policy);
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

        drawn.Result.IsComplete.Should().Be(!reported);
    }

    [Test]
    [Arguments("/Not", false, false)]
    [Arguments("/Not", true, true)]
    [Arguments("/And", false, true)]
    [Arguments("/Or", false, false)]
    public void AMembershipDictionaryWithAVisibilityExpressionIsOffByTheExpression(string operation, bool ofTheGroupThatIsOn,
        bool reported)
    {
        // The first group is off and the second is on. The policy says visible, and is not used.
        var drawn = Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, second) =>
            {
                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/OCGs"] = second.Reference;
                membership.Elements["/VE"] = operation == "/Not"
                    ? new PdfArray(document, new PdfName("/Not"), (ofTheGroupThatIsOn ? second : first).Reference)
                    : new PdfArray(document, new PdfName(operation), first.Reference,
                        new PdfArray(document, new PdfName("/Not"), new PdfArray(document, new PdfName("/Not"), second.Reference)));
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

        drawn.Result.IsComplete.Should().Be(!reported);
    }

    [Test]
    public void AFormThatBelongsToAGroupSwitchedOffIsDrawnAndReported()
    {
        var drawn = Layered("/Fm1 Do",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, _) =>
            {
                var form = Drawn.Form(document, "0 0 5 5 re f");
                form.Elements["/OC"] = first.Reference;
                Drawn.Add(document, resources, "/XObject", "/Fm1", form);
            });

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedFeature /OC");
    }

    [Test]
    public void MarkedContentInADocumentWithNoOptionalContentIsNotReported()
    {
        var drawn = Drawn.Page("/OC /L1 BDC /Span << /ActualText (a) >> BDC 0 0 5 5 re f EMC EMC", (document, resources) =>
            Drawn.Add(document, resources, "/Properties", "/L1", Group(document)));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
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
