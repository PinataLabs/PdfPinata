using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Optional content that is switched off is not drawn, and is not reported (ISO 32000-1 8.11).
/// The groups and the cases of a membership are in <see cref="FormXObjectTests"/>.
/// </summary>
/// <remarks>
/// Every page here has the group <c>/L1</c>, off in the default configuration, and the group
/// <c>/L2</c>, on.
/// </remarks>
public class OptionalContentTests
{
    private static Drawn L1Off(string content, Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> more = null,
        Func<PageOptionalContent> optionalContent = null) =>
        FormXObjectTests.Layered(content,
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            more, optionalContent);

    /// <summary>Where each fill's first point landed on the device, left to right.</summary>
    private static double[] Lefts(Drawn drawn) =>
        drawn.Device.Fills.Select(fill => fill.Path.Points.ToArray()[0]).Zip(drawn.Device.Fills, (point, fill) => fill.At(point.X, point.Y).X)
            .ToArray();

    private const string Both = "/OC /L1 BDC 1 1 5 5 re f EMC /OC /L2 BDC 20 1 5 5 re f EMC";

    [Test]
    public void ASectionThatIsHiddenStillChangesTheGraphicsState()
    {
        // The cm, the colour and the clip are made where nothing is drawn and are in force after it.
        var drawn = L1Off("q /OC /L1 BDC 1 0 0 1 50 0 cm 0 1 0 rg 0 0 3 3 re W n 0 0 5 5 re f EMC 0 0 5 5 re f Q");

        var fill = drawn.Device.Fills.Should().ContainSingle().Subject;
        fill.Origin.X.Should().Be(50);
        fill.Paint.Should().Be(new PagePaint(0, 1, 0));
        drawn.Device.Clips.Should().ContainSingle("the clip made inside the hidden section is made");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AGraphicsStateSavedInAHiddenSectionIsRestoredAfterIt()
    {
        var drawn = L1Off("/OC /L1 BDC q 1 0 0 1 50 0 cm EMC 0 0 5 5 re f Q 0 0 5 5 re f");

        drawn.Device.Fills.Select(fill => fill.Origin.X).Should().Equal(50, 0);
    }

    [Test]
    public void AStrokeAndAnImageInAHiddenSectionAreNotDrawn()
    {
        var drawn = L1Off("/OC /L1 BDC 0 0 m 10 10 l S 5 5 5 5 re f BI /W 1 /H 1 /CS /G /BPC 8 ID \0 EI EMC");

        drawn.Device.Calls.Where(call => call.Kind is "fill" or "stroke" or "image").Should().BeEmpty();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AFormDrawnInAHiddenSectionIsNotDrawn()
    {
        var drawn = L1Off("/OC /L1 BDC /Fm1 Do EMC /Fm1 Do",
            (document, resources, _, _) => Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f")));

        drawn.Device.Fills.Should().ContainSingle("only the form drawn outside the section is drawn");
    }

    [Test]
    public void SequencesInsideAHiddenOneAreHiddenAndTheEndOfEachIsKept()
    {
        var drawn = L1Off("/OC /L1 BDC /Span BDC EMC 1 1 5 5 re f /Tag BMC 2 2 5 5 re f EMC 3 3 5 5 re f EMC 4 4 5 5 re f");

        Lefts(drawn).Should().Equal(4);
    }

    [Test]
    public void ASequenceThatIsHiddenInsideOneThatIsNotHidesOnlyItself()
    {
        var drawn = L1Off("/OC /L2 BDC 1 1 5 5 re f /OC /L1 BDC 2 2 5 5 re f EMC 3 3 5 5 re f EMC 4 4 5 5 re f");

        Lefts(drawn).Should().Equal(1, 3, 4);
    }

    [Test]
    public void AGroupThatIsOffInsideAGroupThatIsOffStaysHiddenUntilTheOutsideEnds()
    {
        var drawn = L1Off("/OC /L1 BDC /OC /L1 BDC EMC 1 1 5 5 re f EMC 2 2 5 5 re f");

        Lefts(drawn).Should().Equal(2);
    }

    [Test]
    public void AnEmcWithNoSequenceOpenIsReportedAndDoesNothing()
    {
        var drawn = L1Off("EMC 1 1 5 5 re f /OC /L1 BDC EMC EMC 2 2 5 5 re f");

        Lefts(drawn).Should().Equal(1, 2);
        drawn.Problems.Should().Equal("OperatorOutOfPlace EMC");
    }

    [Test]
    public void ASequenceNeverEndedHidesTheRestOfTheStreamAndIsReported()
    {
        var drawn = L1Off("0 0 5 5 re f /OC /L1 BDC 10 10 5 5 re f 20 20 5 5 re f");

        Lefts(drawn).Should().Equal(0);
        drawn.Problems.Should().Equal("OperatorOutOfPlace BDC");
    }

    [Test]
    public void ASequenceNeverEndedInAFormDoesNotHideWhatDrewTheForm()
    {
        var drawn = L1Off("/Fm1 Do 30 0 5 5 re f",
            (document, resources, first, _) =>
            {
                var form = Drawn.Form(document, "/OC /L1 BDC 0 0 5 5 re f");
                Drawn.Add(document, resources, "/XObject", "/Fm1", form);
            });

        Lefts(drawn).Should().Equal(30);
    }

    [Test]
    public void AnEmcInAFormCannotEndASequenceTheFormWasDrawnIn()
    {
        var drawn = L1Off("/OC /L2 BDC /Fm1 Do 20 0 5 5 re f EMC 30 0 5 5 re f",
            (document, resources, _, _) => Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "EMC 0 0 5 5 re f")));

        Lefts(drawn).Should().Equal(0, 20, 30);
        drawn.Problems.Should().Equal("OperatorOutOfPlace EMC");
    }

    [Test]
    [Arguments("a name the properties do not hold")]
    [Arguments("a property that is not a dictionary")]
    [Arguments("a dictionary written in the content")]
    public void AnOcThatDoesNotNameAGroupIsDrawnAndDoesNotThrow(string case_)
    {
        var content = case_ switch
        {
            "a dictionary written in the content" => "/OC << /Type /OCG >> BDC 1 1 5 5 re f EMC",
            _ => "/OC /P1 BDC 1 1 5 5 re f EMC"
        };
        var drawn = L1Off(content, (document, resources, _, _) =>
        {
            if (case_ == "a property that is not a dictionary")
            {
                var properties = new PdfDictionary(document);
                properties.Elements.SetInteger("/P1", 5);
                resources.Elements["/Properties"] = properties;
            }
        });

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    [Arguments("an integer")]
    [Arguments("a name")]
    [Arguments("a dictionary that is not a group")]
    public void AnOcEntryThatIsNotAGroupLeavesTheXObjectDrawn(string case_)
    {
        var drawn = L1Off("/Fm1 Do", (document, resources, _, _) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            switch (case_)
            {
                case "an integer": form.Elements.SetInteger("/OC", 3); break;
                case "a name": form.Elements.SetName("/OC", "/L1"); break;
                default:
                    var other = new PdfDictionary(document);
                    other.Elements.SetName("/Type", "/Foo");
                    form.Elements["/OC"] = other;
                    break;
            }

            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Fills.Should().ContainSingle();
    }

    // A named configuration is read as /D is, and does not inherit from it: /L1 is switched off by
    // /D alone, and /L2 by the configuration alone.
    private static void AddConfiguration(PdfDocument document, string name, Action<PdfDictionary, PdfDictionary, PdfDictionary> fill,
        PdfDictionary first, PdfDictionary second)
    {
        var configuration = new PdfDictionary(document);
        configuration.Elements.SetString("/Name", name);
        fill(configuration, first, second);
        var properties = document.Internals.Catalog.Elements.GetDictionary("/OCProperties");
        var configs = properties.Elements.GetArray("/Configs");
        if (configs == null)
            properties.Elements["/Configs"] = configs = new PdfArray(document);
        configs.Elements.Add(configuration);
    }

    private static Drawn Print(PageOptionalContent selection, Action<PdfDictionary, PdfDictionary, PdfDictionary> fill = null) =>
        L1Off(Both, (document, _, first, second) =>
            AddConfiguration(document, "Print", fill ?? ((configuration, _, _) =>
                configuration.Elements["/OFF"] = new PdfArray(document, second.Reference)), first, second), () => selection);

    [Test]
    public void TheDefaultConfigurationIsUsedWhereNoneIsChosen()
    {
        Lefts(Print(null)).Should().Equal(20);
        Lefts(Print(new PageOptionalContent())).Should().Equal(20);
    }

    [Test]
    public void ANamedConfigurationIsUsedInPlaceOfTheDefault()
    {
        var drawn = Print(new PageOptionalContent { Configuration = "Print" });

        Lefts(drawn).Should().Equal(1);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AConfigurationNoEntryNamesIsReportedAndTheDefaultIsUsed()
    {
        var drawn = Print(new PageOptionalContent { Configuration = "Screen" });

        Lefts(drawn).Should().Equal(20);
        drawn.Problems.Should().Equal("InvalidValue /OC");
    }

    [Test]
    public void ANamedConfigurationWithNoBaseStateStartsWithEveryGroupOn()
    {
        var drawn = Print(new PageOptionalContent { Configuration = "Print" }, (_, _, _) => { });

        Lefts(drawn).Should().Equal(1, 20);
    }

    [Test]
    public void ANamedConfigurationKeepsItsOwnIntent()
    {
        // Every group is /View. A configuration for /Design alone counts none of them, so what it
        // switches off has no effect (8.11.2.3).
        var drawn = Print(new PageOptionalContent { Configuration = "Print" }, (configuration, _, second) =>
        {
            configuration.Elements.SetName("/Intent", "/Design");
            configuration.Elements["/OFF"] = new PdfArray(configuration.Owner, second.Reference);
        });

        Lefts(drawn).Should().Equal(1, 20);
    }

    /// <summary>The page <see cref="Both"/> drawn with the selection <paramref name="select"/> makes from its two groups.</summary>
    private static Drawn WithGroups(Func<PdfDictionary, PdfDictionary, PageOptionalContent> select)
    {
        PdfDictionary first = null, second = null;
        return L1Off(Both, (_, _, one, two) => (first, second) = (one, two), () => select(first, second));
    }

    [Test]
    public void AGroupTheCallerSwitchesOnIsDrawnWhateverTheConfigurationSays() =>
        Lefts(WithGroups((first, _) => new PageOptionalContent { On = [first] })).Should().Equal(1, 20);

    [Test]
    public void AGroupTheCallerSwitchesOffIsHiddenWhateverTheConfigurationSays() =>
        Lefts(WithGroups((_, second) => new PageOptionalContent { Off = [second] })).Should().BeEmpty();

    [Test]
    public void AGroupInBothListsIsOff() =>
        Lefts(WithGroups((first, second) => new PageOptionalContent { On = [first, second], Off = [second] })).Should().Equal(1);

    [Test]
    public void GroupsTheCallerSwitchesAreSwitchedOverANamedConfiguration()
    {
        // "Print" has the second group off and the first on; the caller turns them round.
        PdfDictionary first = null, second = null;
        var drawn = L1Off(Both,
            (document, _, one, two) =>
            {
                (first, second) = (one, two);
                AddConfiguration(document, "Print", (configuration, _, off) =>
                    configuration.Elements["/OFF"] = new PdfArray(document, off.Reference), one, two);
            },
            () => new PageOptionalContent { Configuration = "Print", On = [second], Off = [first] });

        Lefts(drawn).Should().Equal(20);
    }

    [Test]
    public void ASwitchedGroupCountsOnlyWhereTheConfigurationNamesItsIntent()
    {
        // The caller switches a /Design group off, and the configuration has no /Design intent.
        PdfDictionary design = null;
        var drawn = FormXObjectTests.Layered("/OC /L3 BDC 1 1 5 5 re f EMC",
            (_, _, _, _) => { },
            (document, resources, _, _) =>
            {
                design = FormXObjectTests.Group(document);
                design.Elements.SetName("/Intent", "/Design");
                Drawn.Add(document, resources, "/Properties", "/L3", design);
            },
            () => new PageOptionalContent { Off = [design] });

        Lefts(drawn).Should().Equal(1);
    }

    [Test]
    public void AConfigurationWithBaseStateUnchangedStartsFromTheDefault()
    {
        // /D has L1 off and L2 on, and "Print" says nothing but that it leaves them as they were.
        var drawn = Print(new PageOptionalContent { Configuration = "Print" },
            (configuration, _, _) => configuration.Elements.SetName("/BaseState", "/Unchanged"));

        Lefts(drawn).Should().Equal(20);
    }

    [Test]
    public void AnUnchangedConfigurationAppliesItsOwnListsOverTheDefault()
    {
        var switchedOn = Print(new PageOptionalContent { Configuration = "Print" }, (configuration, first, _) =>
        {
            configuration.Elements.SetName("/BaseState", "/Unchanged");
            configuration.Elements["/ON"] = new PdfArray(configuration.Owner, first.Reference);
        });
        var switchedOff = Print(new PageOptionalContent { Configuration = "Print" }, (configuration, _, second) =>
        {
            configuration.Elements.SetName("/BaseState", "/Unchanged");
            configuration.Elements["/OFF"] = new PdfArray(configuration.Owner, second.Reference);
        });

        Lefts(switchedOn).Should().Equal(1, 20);
        Lefts(switchedOff).Should().BeEmpty();
    }

    [Test]
    public void AnUnchangedConfigurationOverADefaultThatStartsOffKeepsWhatTheDefaultSwitchedOn()
    {
        var drawn = L1Off(Both, (document, _, first, second) =>
        {
            var defaults = document.Internals.Catalog.Elements.GetDictionary("/OCProperties").Elements.GetDictionary("/D");
            defaults.Elements.SetName("/BaseState", "/OFF");
            defaults.Elements.Remove("/OFF");
            defaults.Elements["/ON"] = new PdfArray(document, second.Reference);
            AddConfiguration(document, "Print", (configuration, _, _) =>
                configuration.Elements.SetName("/BaseState", "/Unchanged"), first, second);
        }, () => new PageOptionalContent { Configuration = "Print" });

        Lefts(drawn).Should().Equal(20);
    }

    /// <summary>
    /// A page with the group <c>/L1</c>, an annotation in it or the content in it, and a
    /// <c>/Configs</c> holding an entry whose <c>/Name</c> is not a string beside "Print".
    /// </summary>
    private static Drawn MalformedConfigs(bool inAnnotation, Func<PdfDocument, PdfItem> badName, bool printHidesTheGroup)
    {
        PdfDictionary group = null;
        var page = Drawn.PageOf(inAnnotation ? "" : "/OC /L1 BDC 0 0 5 5 re f EMC", (document, resources) =>
        {
            group = FormXObjectTests.Group(document);
            Drawn.Add(document, resources, "/Properties", "/L1", group);

            var malformed = new PdfDictionary(document);
            malformed.Elements["/Name"] = badName(document);
            var print = new PdfDictionary(document);
            print.Elements.SetString("/Name", "Print");
            if (printHidesTheGroup)
                print.Elements["/OFF"] = new PdfArray(document, group.Reference);

            var properties = new PdfDictionary(document);
            properties.Elements["/OCGs"] = new PdfArray(document, group.Reference);
            properties.Elements["/D"] = new PdfDictionary(document);
            properties.Elements["/Configs"] = new PdfArray(document, malformed, print);
            document.Internals.Catalog.Elements["/OCProperties"] = properties;
        });

        if (inAnnotation)
        {
            var document = page.Owner;
            var annotation = new PdfDictionary(document);
            document.Internals.AddObject(annotation);
            annotation.Elements.SetName("/Type", "/Annot");
            annotation.Elements.SetName("/Subtype", "/Square");
            annotation.Elements["/Rect"] = new PdfRectangle(new XPoint(0, 0), new XPoint(50, 50));
            var appearance = new PdfDictionary(document);
            appearance.Elements["/N"] = Drawn.Form(document, "0 0 5 5 re f").Reference;
            annotation.Elements["/AP"] = appearance;
            annotation.Elements["/OC"] = group.Reference;
            annotation.Elements.SetInteger("/F", 4);
            page.Elements["/Annots"] = new PdfArray(document, annotation.Reference);
        }

        var device = new RecordingDevice();
        var result = PageRendering.Render(page, device, optionalContent: new PageOptionalContent { Configuration = "Print" });
        return new Drawn(result, device);
    }

    private static PdfItem NumberName(PdfDocument document)
    {
        var holder = new PdfDictionary(document);
        holder.Elements.SetInteger("/Number", 5);
        return holder.Elements["/Number"];
    }

    private static PdfItem DictionaryName(PdfDocument document) => new PdfDictionary(document);

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public void AConfigurationWhoseNameIsNotAStringIsPassedOverNotThrownAt(bool dictionary, bool inAnnotation)
    {
        Drawn drawn = null;
        var act = () => drawn = MalformedConfigs(inAnnotation, dictionary ? DictionaryName : NumberName, printHidesTheGroup: true);

        act.Should().NotThrow();
        drawn.Device.Fills.Should().BeEmpty();
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void AnAnnotationInAGroupTheNamedConfigurationLeavesOnIsDrawnBesideAMalformedEntry()
    {
        var drawn = MalformedConfigs(inAnnotation: true, NumberName, printHidesTheGroup: false);

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().BeEmpty();
    }

    // Built of objects and not of PDF text, because these pages are drawn without being saved.
    private static PdfDictionary GrayRamp(PdfDocument document)
    {
        var function = new PdfDictionary(document);
        function.Elements["/FunctionType"] = new PdfPinata.Pdf.PdfInteger(2);
        function.Elements["/Domain"] = new PdfArray(document, new PdfPinata.Pdf.PdfInteger(0), new PdfPinata.Pdf.PdfInteger(1));
        function.Elements["/C0"] = new PdfArray(document, new PdfPinata.Pdf.PdfInteger(0));
        function.Elements["/C1"] = new PdfArray(document, new PdfPinata.Pdf.PdfInteger(1));
        function.Elements["/N"] = new PdfPinata.Pdf.PdfInteger(1);

        var shading = new PdfDictionary(document);
        shading.Elements["/ShadingType"] = new PdfPinata.Pdf.PdfInteger(2);
        shading.Elements["/ColorSpace"] = new PdfName("/DeviceGray");
        shading.Elements["/Coords"] = new PdfArray(document, new PdfPinata.Pdf.PdfInteger(0), new PdfPinata.Pdf.PdfInteger(0), new PdfPinata.Pdf.PdfInteger(100),
            new PdfPinata.Pdf.PdfInteger(0));
        shading.Elements["/Function"] = function;
        return shading;
    }

    [Test]
    public void AShadingInHiddenContentIsNotReadAndOneInVisibleContentIsDrawn()
    {
        // The hidden one names no shading at all: read, it would be reported missing.
        var hidden = L1Off("/OC /L1 BDC /Missing sh EMC");
        var visible = L1Off("/OC /L2 BDC /Sh1 sh EMC", (document, resources, _, _) =>
            Drawn.Add(document, resources, "/Shading", "/Sh1", GrayRamp(document)));

        hidden.Problems.Should().BeEmpty();
        hidden.Result.IsComplete.Should().BeTrue();
        hidden.Device.Fills.Should().BeEmpty();
        visible.Problems.Should().BeEmpty();
        visible.Device.Fills.Should().ContainSingle();
    }

    [Test]
    public void AProblemWithTheGraphicsStateInHiddenContentIsStillReported()
    {
        var drawn = L1Off("/OC /L1 BDC /GS1 gs EMC");

        drawn.Problems.Should().Equal("MissingResource /GS1");
    }
}
