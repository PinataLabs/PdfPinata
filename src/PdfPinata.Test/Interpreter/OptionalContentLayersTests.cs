using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// The model of a document's layers lists the groups a render takes, and says what the render
/// then draws (ISO 32000-1 8.11.4).
/// </summary>
/// <remarks>
/// The page draws a square for each of four groups, at x of 1, 20, 40 and 60: Base, Notes, Alt and
/// Extra. The default configuration has Notes and Extra off. The document also has a group Design
/// of intent Design that nothing is drawn in.
/// </remarks>
public class OptionalContentLayersTests
{
    private const string Content =
        "/OC /L1 BDC 1 1 5 5 re f EMC /OC /L2 BDC 20 1 5 5 re f EMC /OC /L3 BDC 40 1 5 5 re f EMC /OC /L4 BDC 60 1 5 5 re f EMC";

    private static PdfDictionary NamedGroup(PdfDocument document, string name, string intent = null)
    {
        var group = Interpreter.FormXObjectTests.Group(document);
        group.Elements.SetString("/Name", name);
        if (intent != null)
            group.Elements.SetName("/Intent", intent);
        return group;
    }

    private static PdfArray Refs(PdfDocument document, params PdfDictionary[] groups) =>
        new(document, groups.Select(group => (PdfItem)group.Reference).ToArray());

    /// <summary>A document, saved and opened again, with the groups, configurations and order of the class comment.</summary>
    private static PdfPage Layered(Action<PdfDocument, PdfDictionary, PdfDictionary> more = null)
    {
        var page = Drawn.PageOf(Content, (document, resources) =>
        {
            var baseGroup = NamedGroup(document, "Base");
            var notes = NamedGroup(document, "Notes");
            var alt = NamedGroup(document, "Alt");
            var extra = NamedGroup(document, "Extra");
            var design = NamedGroup(document, "Design", "/Design");
            Drawn.Add(document, resources, "/Properties", "/L1", baseGroup);
            Drawn.Add(document, resources, "/Properties", "/L2", notes);
            Drawn.Add(document, resources, "/Properties", "/L3", alt);
            Drawn.Add(document, resources, "/Properties", "/L4", extra);

            var defaults = new PdfDictionary(document);
            defaults.Elements["/OFF"] = Refs(document, notes, extra);
            defaults.Elements["/Order"] = new PdfArray(document, baseGroup.Reference, Refs(document, notes),
                new PdfArray(document, new PdfString("Choices"), alt.Reference, extra.Reference), design.Reference);
            defaults.Elements["/RBGroups"] = new PdfArray(document, Refs(document, alt, extra));

            var print = new PdfDictionary(document);
            print.Elements.SetString("/Name", "Print");
            print.Elements.SetString("/Creator", "A test");
            print.Elements.SetName("/BaseState", "/OFF");
            print.Elements["/ON"] = Refs(document, baseGroup);

            var edit = new PdfDictionary(document);
            edit.Elements.SetString("/Name", "Edit");
            edit.Elements.SetName("/BaseState", "/Unchanged");
            edit.Elements["/ON"] = Refs(document, notes);
            edit.Elements["/OFF"] = Refs(document, baseGroup);

            var properties = new PdfDictionary(document);
            properties.Elements["/OCGs"] = Refs(document, baseGroup, notes, alt, extra, design);
            properties.Elements["/D"] = defaults;
            properties.Elements["/Configs"] = new PdfArray(document, print, edit);
            document.Internals.Catalog.Elements["/OCProperties"] = properties;
            more?.Invoke(document, properties, defaults);
        });
        return page.Owner.Reopened().Pages[0];
    }

    private static double[] DrawnAt(PdfPage page, PageOptionalContent selection)
    {
        var device = new RecordingDevice();
        PageRendering.Render(page, device, optionalContent: selection);
        return device.Fills.Select(fill => fill.At(fill.Path.Points.ToArray()[0].X, 0).X).OrderBy(x => x).ToArray();
    }

    private static string[] Names(IEnumerable<OptionalContentGroup> groups) => groups.Select(group => group.Name).ToArray();

    [Test]
    public void TheGroupsAreListedByNameAndIntentInTheOrderOfTheDocument()
    {
        var layers = OptionalContentLayers.Read(Layered().Owner);

        Names(layers.Groups).Should().Equal("Base", "Notes", "Alt", "Extra", "Design");
        layers.Groups[0].Intents.Should().Equal("View");
        layers.Groups[4].Intents.Should().Equal("Design");
    }

    [Test]
    public void TheDefaultConfigurationAndEachConfigurationSayWhichGroupsAreOn()
    {
        var layers = OptionalContentLayers.Read(Layered().Owner);
        string[] On(OptionalContentConfiguration configuration) => Names(layers.Groups.Where(configuration.IsOn));

        layers.Default.Name.Should().BeNull();
        layers.Default.BaseState.Should().Be(OptionalContentBaseState.On);
        On(layers.Default).Should().Equal("Base", "Alt", "Design");

        layers.Configurations.Select(configuration => configuration.Name).Should().Equal("Print", "Edit");
        layers.Configurations[0].Creator.Should().Be("A test");
        layers.Configurations[0].BaseState.Should().Be(OptionalContentBaseState.Off);
        On(layers.Configurations[0]).Should().Equal("Base");
        layers.Configurations[1].BaseState.Should().Be(OptionalContentBaseState.Unchanged);
        On(layers.Configurations[1]).Should().Equal("Notes", "Alt", "Design");
    }

    [Test]
    public void TheOrderIsATreeOfGroupsAndLabels()
    {
        var order = OptionalContentLayers.Read(Layered().Owner).Default.Order;

        order.Select(node => node.Group?.Name ?? "label " + node.Label).Should().Equal("Base", "label Choices", "Design");
        order[0].Children.Select(node => node.Group.Name).Should().Equal("Notes");
        order[1].Group.Should().BeNull();
        order[1].Children.Select(node => node.Group.Name).Should().Equal("Alt", "Extra");
    }

    [Test]
    public void TheRadioButtonGroupsAreSetsOfGroups()
    {
        var sets = OptionalContentLayers.Read(Layered().Owner).Default.RadioButtonGroups;

        sets.Should().ContainSingle().Which.Select(group => group.Name).Should().Equal("Alt", "Extra");
        OptionalContentLayers.Read(Layered().Owner).Configurations[0].RadioButtonGroups.Should().BeEmpty();
    }

    [Test]
    public void AGroupFromTheModelSwitchedOnIsDrawn()
    {
        var page = Layered();
        var layers = OptionalContentLayers.Read(page.Owner);
        var notes = layers.Groups.Single(group => group.Name == "Notes");
        var baseGroup = layers.Groups.Single(group => group.Name == "Base");

        DrawnAt(page, null).Should().Equal(1, 40);
        DrawnAt(page, new PageOptionalContent { On = [notes.Dictionary] }).Should().Equal(1, 20, 40);
        DrawnAt(page, new PageOptionalContent { Off = [baseGroup.Dictionary] }).Should().Equal(40);
        DrawnAt(page, new PageOptionalContent { On = layers.Groups.Select(group => group.Dictionary).ToArray() })
            .Should().Equal(1, 20, 40, 60);
    }

    [Test]
    public void AConfigurationFromTheModelIsDrawnAsItSaysItsGroupsAre()
    {
        var page = Layered();
        var layers = OptionalContentLayers.Read(page.Owner);
        var positions = new Dictionary<string, double> { ["Base"] = 1, ["Notes"] = 20, ["Alt"] = 40, ["Extra"] = 60 };

        foreach (var configuration in layers.Configurations)
        {
            var expected = layers.Groups.Where(group => configuration.IsOn(group) && positions.ContainsKey(group.Name))
                .Select(group => positions[group.Name]).OrderBy(x => x);

            DrawnAt(page, new PageOptionalContent { Configuration = configuration.Name }).Should().Equal(expected);
        }
    }

    [Test]
    public void ADocumentWithNoOptionalContentIsAnEmptyModel()
    {
        var layers = OptionalContentLayers.Read(new PdfDocument());

        layers.Groups.Should().BeEmpty();
        layers.Configurations.Should().BeEmpty();
        layers.Default.Order.Should().BeEmpty();
        layers.Default.RadioButtonGroups.Should().BeEmpty();
        layers.Default.IsOn(null).Should().BeFalse();
        OptionalContentLayers.Read(null).Groups.Should().BeEmpty();
    }

    [Test]
    public void EntriesThatAreNotWhatTheyShouldBeAreLeftOut()
    {
        var page = Layered((document, properties, defaults) =>
        {
            var groups = properties.Elements.GetArray("/OCGs");
            var first = groups.Elements.GetDictionary(0);
            var notAGroup = new PdfDictionary(document);
            notAGroup.Elements.SetName("/Type", "/OCMD");
            groups.Elements.Add(new global::PdfPinata.Pdf.PdfInteger(7));
            groups.Elements.Add(notAGroup);
            groups.Elements.Add(first.Reference);

            properties.Elements.GetArray("/Configs").Elements.Add(new PdfName("/Odd"));
            defaults.Elements["/RBGroups"] = new PdfArray(document, new global::PdfPinata.Pdf.PdfInteger(1), new PdfArray(document), notAGroup);
            defaults.Elements["/Order"] = new PdfArray(document, new global::PdfPinata.Pdf.PdfInteger(1), notAGroup, new PdfArray(document), first.Reference);
        });

        var layers = OptionalContentLayers.Read(page.Owner);

        Names(layers.Groups).Should().Equal("Base", "Notes", "Alt", "Extra", "Design");
        layers.Configurations.Should().HaveCount(2);
        layers.Default.RadioButtonGroups.Should().BeEmpty();
        layers.Default.Order.Should().ContainSingle().Which.Group.Name.Should().Be("Base");
    }

    [Test]
    public void AnOrderThatHoldsItselfIsReadOnce()
    {
        var page = Layered((document, _, defaults) =>
        {
            var order = new PdfArray(document);
            document.Internals.AddObject(order);
            var groups = document.Internals.Catalog.Elements.GetDictionary("/OCProperties").Elements.GetArray("/OCGs");
            order.Elements.Add(groups.Elements[0]);
            order.Elements.Add(order.Reference);
            order.Elements.Add(order.Reference);
            defaults.Elements["/Order"] = order.Reference;
        });

        var order = OptionalContentLayers.Read(page.Owner).Default.Order;

        order.Should().ContainSingle().Which.Group.Name.Should().Be("Base");
        order[0].Children.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task AnOrderOfArraysSharedAtEveryLevelIsReadOnceEach()
    {
        var document = Layered().Owner;
        var rung = new PdfArray(document);
        for (var level = 0; level < 40; level++)
            rung = new PdfArray(document, rung, rung);
        document.Internals.Catalog.Elements.GetDictionary("/OCProperties").Elements.GetDictionary("/D").Elements["/Order"] = rung;

        var order = await Interruptibly.Run(() => OptionalContentLayers.Read(document).Default.Order);

        order.Should().BeEmpty();
    }
}
