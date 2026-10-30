using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.Structure;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using PdfInt = PdfPinata.Pdf.PdfInteger;

namespace PdfPinata.Test.Forms;

/// <summary>
///   Flattening a tagged form: <see cref="PdfAcroForm.Flatten()"/> on a document with a structure
///   tree. Issue #357.
/// </summary>
/// <remarks>
///   <para>
///     Each widget's <c>/Form</c> element held an <c>/OBJR</c> to the annotation. Flattening draws
///     the appearance into the page, so the tree has to say what that drawing is: a marked-content
///     sequence with an <c>/MCID</c>, a marked-content reference to it in the element that held the
///     <c>/OBJR</c>, and a parent tree that maps the identifier back to the element.
///   </para>
///   <para>
///     Every test flattens, saves, reopens, and reads the three places those meet - the content
///     stream, the structure tree and the parent tree - as raw dictionaries and bytes. A test that
///     asked the builder instead would pass for a tree that the file does not hold.
///   </para>
/// </remarks>
public class FormFlatteningTaggedTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    // ----- building ---------------------------------------------------------------------------------------

    /// <summary>
    ///   A tagged form: a heading tagged by hand, so that the page already holds marked content with
    ///   an identifier of its own, and a <c>/Form</c> element for each field's widget.
    /// </summary>
    private static PdfDocument ATaggedForm(string formTag = "/Form", params (string Name, string Value)[] fields)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        TagHeading(document, page);
        if (formTag != "/Form")
            document.Structure.Root.RoleMap[formTag.TrimStart('/')] = "/Form";

        if (fields.Length == 0)
            fields = [("name", "Ada Lovelace"), ("country", "Canada")];

        var root = document.Structure.CreateElement(PdfTag.Section);
        var top = 700.0;
        foreach (var (name, value) in fields)
        {
            AddTaggedText(document, page, root, formTag, name, value, new PdfRectangle(new XRect(60, top, 200, 20)));
            top -= 40;
        }
        return document;
    }

    private static void TagHeading(PdfDocument document, PdfPage page)
    {
        var heading = document.Structure.CreateElement(PdfTag.H1);
        using var gfx = XGraphics.FromPdfPage(page);
        using (gfx.BeginMarkedContent(heading))
            gfx.DrawString("Registration", new XFont("Arial", 18), XBrushes.Black, 60, 60);
    }

    private static PdfTextField AddTaggedText(PdfDocument document, PdfPage page, PdfStructureElement parent, string formTag,
        string name, string value, PdfRectangle box, string alternateText = null)
    {
        var field = new PdfTextField(document) { Name = name };
        document.GetOrCreateAcroForm().Fields.Add(field);
        var widget = field.AddWidget(page, box);
        field.Text = value;
        if (alternateText != null)
            field.ToolTip = alternateText;

        var element = document.Structure.CreateElement(new PdfTag(formTag), parent);
        document.Structure.AddAnnotation(element, page, widget);
        return field;
    }

    // ----- reading ------------------------------------------------------------------------------------------

    private sealed record Sequence(string Tag, int Mcid, string Body);

    private static string ContentOf(PdfPage page) =>
        System.Text.Encoding.ASCII.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);

    /// <summary>Every <c>tag &lt;&lt;/MCID n&gt;&gt; BDC … EMC</c> on a page, in the order drawn.</summary>
    private static List<Sequence> Sequences(PdfPage page) =>
        Regex.Matches(ContentOf(page), @"(?<tag>/\w+)\s*<<\s*/MCID\s+(?<id>\d+)\s*>>\s*BDC(?<body>.*?)EMC", RegexOptions.Singleline)
            .Select(match => new Sequence(match.Groups["tag"].Value, int.Parse(match.Groups["id"].Value), match.Groups["body"].Value))
            .ToList();

    /// <summary>The sequences that draw a flattened form: the ones that paint a form XObject.</summary>
    private static List<Sequence> Drawn(PdfPage page) => Sequences(page).Where(sequence => sequence.Body.Contains(" Do")).ToList();

    /// <summary>An item as the object it refers to, when it refers to one.</summary>
    private static PdfItem Deref(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    private static PdfDictionary StructureRoot(PdfDocument document) =>
        document.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");

    private static IEnumerable<PdfDictionary> Elements(PdfDictionary root)
    {
        var pending = new Stack<PdfItem>();
        pending.Push(root.Elements["/K"]);
        while (pending.Count > 0)
        {
            switch (Deref(pending.Pop()))
            {
                case PdfArray array:
                    foreach (var kid in Enumerable.Reverse(array.Elements.Items))
                        pending.Push(kid);
                    break;
                case PdfDictionary dict when dict.Elements.ContainsKey("/S"):
                    yield return dict;
                    pending.Push(dict.Elements["/K"]);
                    break;
            }
        }
    }

    /// <summary>The kids of an element, as the items they are.</summary>
    private static List<PdfItem> KidsOf(PdfDictionary element) =>
        Deref(element.Elements["/K"]) switch
        {
            PdfArray array => array.Elements.Items.Select(Deref).ToList(),
            null => [],
            var single => [single]
        };

    private static List<PdfDictionary> ObjectReferences(PdfDocument document) =>
        Elements(StructureRoot(document)).SelectMany(KidsOf).OfType<PdfDictionary>()
            .Where(kid => kid.Elements.GetName("/Type") == "/OBJR").ToList();

    /// <summary>
    ///   The elements whose kids name this marked-content identifier on this page: a bare integer is
    ///   read against the element's own <c>/Pg</c>, and a marked-content reference against its own.
    /// </summary>
    private static List<PdfDictionary> ElementsNaming(PdfDocument document, PdfPage page, int mcid)
    {
        var found = new List<PdfDictionary>();
        foreach (var element in Elements(StructureRoot(document)))
        {
            foreach (var kid in KidsOf(element))
            {
                var named = kid switch
                {
                    PdfInt integer when integer.Value == mcid => element.Elements.GetDictionary("/Pg"),
                    PdfDictionary mcr when mcr.Elements.GetName("/Type") == "/MCR" && mcr.Elements.GetInteger("/MCID") == mcid
                        => mcr.Elements.GetDictionary("/Pg") ?? element.Elements.GetDictionary("/Pg"),
                    _ => null
                };
                if (ReferenceEquals(named, page))
                    found.Add(element);
            }
        }
        return found;
    }

    private static PdfDictionary ParentTreeNode(PdfDocument document, int key)
    {
        var pending = new Stack<PdfDictionary>();
        pending.Push(StructureRoot(document).Elements.GetDictionary("/ParentTree"));
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.Elements.GetArray("/Nums") is { } nums)
            {
                for (var idx = 0; idx + 1 < nums.Elements.Count; idx += 2)
                {
                    if (nums.Elements.GetInteger(idx) == key)
                        return new PdfDictionary(document) { Elements = { ["/Value"] = nums.Elements[idx + 1] } };
                }
            }
            if (node.Elements.GetArray("/Kids") is { } kids)
            {
                for (var idx = 0; idx < kids.Elements.Count; idx++)
                    pending.Push(kids.Elements.GetDictionary(idx));
            }
        }
        return null;
    }

    private static List<int> ParentTreeKeys(PdfDocument document)
    {
        var keys = new List<int>();
        var pending = new Stack<PdfDictionary>();
        pending.Push(StructureRoot(document).Elements.GetDictionary("/ParentTree"));
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.Elements.GetArray("/Nums") is { } nums)
            {
                for (var idx = 0; idx + 1 < nums.Elements.Count; idx += 2)
                    keys.Add(nums.Elements.GetInteger(idx));
            }
            if (node.Elements.GetArray("/Kids") is { } kids)
            {
                for (var idx = 0; idx < kids.Elements.Count; idx++)
                    pending.Push(kids.Elements.GetDictionary(idx));
            }
        }
        return keys;
    }

    /// <summary>
    ///   The element the parent tree names for a marked-content identifier on a page, found by the
    ///   page's <c>/StructParents</c>.
    /// </summary>
    private static PdfDictionary ParentTreeElement(PdfDocument document, PdfPage page, int mcid)
    {
        var key = page.Elements.GetInteger("/StructParents");
        var entry = ParentTreeNode(document, key)?.Elements["/Value"];
        return Deref(entry) is PdfArray array && mcid < array.Elements.Count
            ? array.Elements.GetDictionary(mcid)
            : null;
    }

    /// <summary>
    ///   Flattens the form and answers what a reader would open: the document as it is when the tree was
    ///   built in this session, or as it is after a trip through a file first - the two shapes of tree
    ///   <see cref="PdfStructureBuilder"/> and the plain dictionaries a reader finds.
    /// </summary>
    private static PdfDocument Flattened(PdfDocument built, bool readFromFile)
    {
        var target = readFromFile ? built.Reopened() : built;
        target.AcroForm.Flatten();
        return target.Reopened();
    }

    // ----- the structure tree, the parent tree and the content stream agree ------------------------------------

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void EachFlattenedValueIsMarkedContentThatItsElementNamesAndTheParentTreeMapsBack(bool readFromFile)
    {
        var reopened = Flattened(ATaggedForm(), readFromFile);

        var page = reopened.Pages[0];
        var drawn = Drawn(page);
        drawn.Should().HaveCount(2);
        drawn.Select(sequence => sequence.Mcid).Should().OnlyHaveUniqueItems();
        Sequences(page).Select(sequence => sequence.Mcid).Should().OnlyHaveUniqueItems("the heading's identifier is still its own");

        foreach (var sequence in drawn)
        {
            sequence.Tag.Should().Be("/Span");
            var named = ElementsNaming(reopened, page, sequence.Mcid);
            named.Should().ContainSingle();
            named[0].Elements.GetName("/S").Should().Be("/Span");
            ParentTreeElement(reopened, page, sequence.Mcid).Should().BeSameAs(named[0]);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void TheStructureTreeHoldsNoObjectReferenceAndTheParentTreeNoEntryForARemovedWidget(bool readFromFile)
    {
        var reopened = Flattened(ATaggedForm(), readFromFile);

        ObjectReferences(reopened).Should().BeEmpty();
        reopened.AcroForm.Should().BeNull();
        ParentTreeKeys(reopened).Should().BeEquivalentTo(new[] { reopened.Pages[0].Elements.GetInteger("/StructParents") },
            "the page's own entry is all that is left, and the annotations' keys went with the annotations");
        Elements(StructureRoot(reopened)).Count(element => element.Elements.GetName("/S") == "/Form")
            .Should().Be(0, "a /Form with no widget in it would be a control that is not there");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void APageWithNothingElseTaggedOnItIsGivenStructParents(bool readFromFile)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddTaggedText(document, page, document.Structure.CreateElement(PdfTag.Section), "/Form", "name", "Ada Lovelace", Box);
        if (readFromFile)
            document = document.Reopened();
        document.Pages[0].Elements.ContainsKey("/StructParents").Should().BeFalse("the page had no tagged content");

        var reopened = Flattened(document, false);

        var flattened = reopened.Pages[0];
        flattened.Elements.ContainsKey("/StructParents").Should().BeTrue();
        var drawn = Drawn(flattened).Should().ContainSingle().Subject;
        ParentTreeElement(reopened, flattened, drawn.Mcid).Should().BeSameAs(ElementsNaming(reopened, flattened, drawn.Mcid).Single());
        ParentTreeKeys(reopened).Should().BeEquivalentTo(new[] { flattened.Elements.GetInteger("/StructParents") });
        StructureRoot(reopened).Elements.GetInteger("/ParentTreeNextKey")
            .Should().BeGreaterThan(flattened.Elements.GetInteger("/StructParents"));
    }

    [Test]
    public void AnIdentifierTheContentStreamAlreadyUsesIsNotHandedOutAgainWhateverTheParentTreeSays()
    {
        var reopened = ATaggedForm().Reopened();
        var page = reopened.Pages[0];
        var array = Deref(ParentTreeNode(reopened, page.Elements.GetInteger("/StructParents")).Elements["/Value"]) as PdfArray;
        array.Elements.Clear();
        Sequences(page).Should().ContainSingle("the heading, whose identifier the content stream still names");

        reopened.AcroForm.Flatten();

        var sequences = Sequences(reopened.Reopened().Pages[0]);
        sequences.Select(sequence => sequence.Mcid).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    // ----- several pages, many widgets ---------------------------------------------------------------------

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void WidgetsOnTwoPagesAreEachMarkedOnTheirOwnPage(bool readFromFile)
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        TagHeading(document, first);
        var section = document.Structure.CreateElement(PdfTag.Section);
        AddTaggedText(document, first, section, "/Form", "a", "First page value", Box);
        AddTaggedText(document, second, section, "/Form", "b", "Second page value", Box);
        AddTaggedText(document, second, section, "/Form", "c", "Another second page value", new PdfRectangle(new XRect(60, 660, 200, 20)));

        var reopened = Flattened(document, readFromFile);

        reopened.Pages[0].Elements.GetInteger("/StructParents").Should().NotBe(reopened.Pages[1].Elements.GetInteger("/StructParents"));
        Drawn(reopened.Pages[0]).Should().HaveCount(1);
        Drawn(reopened.Pages[1]).Should().HaveCount(2);
        foreach (var page in reopened.Pages)
        {
            foreach (var sequence in Drawn(page))
            {
                var named = ElementsNaming(reopened, page, sequence.Mcid);
                named.Should().ContainSingle();
                ParentTreeElement(reopened, page, sequence.Mcid).Should().BeSameAs(named[0]);
            }
        }
        ObjectReferences(reopened).Should().BeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void OneElementHoldingAWidgetOnTwoPagesNamesEachMarkOnItsOwnPage(bool readFromFile)
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var field = AddTaggedText(document, first, document.Structure.CreateElement(PdfTag.Section), "/Form", "shared", "Shared value", Box);
        // The one widget is listed on both pages, which a file may do, and has the one element.
        second.Elements.GetArray("/Annots").Should().BeNull();
        var annots = new PdfArray(document);
        annots.Elements.Add(field.Widgets[0].Reference);
        second.Elements["/Annots"] = annots;

        var reopened = Flattened(document, readFromFile);

        var marks = new List<(PdfPage Page, int Mcid)>();
        foreach (var page in reopened.Pages)
            marks.AddRange(Drawn(page).Select(sequence => (page, sequence.Mcid)));
        marks.Should().HaveCount(2);
        var elements = marks.Select(mark => ElementsNaming(reopened, mark.Page, mark.Mcid).Single()).Distinct().ToList();
        elements.Should().ContainSingle("both pages' marks are the content of the one element that held the widget");
        foreach (var (page, mcid) in marks)
            ParentTreeElement(reopened, page, mcid).Should().BeSameAs(elements[0]);
        ObjectReferences(reopened).Should().BeEmpty();
    }

    [Test]
    public void ManyWidgetsSpillTheParentTreeOverSeveralNodesAndAreAllMapped()
    {
        var fields = Enumerable.Range(0, 70).Select(idx => ("f" + idx, "Value " + idx)).ToArray();
        var document = ATaggedForm("/Form", fields);

        var reopened = Flattened(document, readFromFile: true);

        var page = reopened.Pages[0];
        var drawn = Drawn(page);
        drawn.Should().HaveCount(70);
        drawn.Select(sequence => sequence.Mcid).Should().OnlyHaveUniqueItems();
        foreach (var sequence in drawn)
            ParentTreeElement(reopened, page, sequence.Mcid).Should().BeSameAs(ElementsNaming(reopened, page, sequence.Mcid).Single());
        ParentTreeKeys(reopened).Should().BeEquivalentTo(new[] { page.Elements.GetInteger("/StructParents") });
    }

    // ----- what the element becomes --------------------------------------------------------------------------

    [Test]
    [Arguments("/Form", false)]
    [Arguments("/Form", true)]
    [Arguments("/Control", false)]
    [Arguments("/Control", true)]
    public void AFormElementBecomesASpanWhateverItWasCalledThatTheRoleMapReadsAsAForm(string tag, bool readFromFile)
    {
        var reopened = Flattened(ATaggedForm(tag), readFromFile);

        var names = Elements(StructureRoot(reopened)).Select(element => element.Elements.GetName("/S")).ToList();
        names.Should().NotContain(tag, "the control is gone");
        names.Count(name => name == "/Span").Should().Be(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void TheFieldsToolTipBecomesTheElementsAlternateTextUnlessItHasOne(bool readFromFile)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var section = document.Structure.CreateElement(PdfTag.Section);
        AddTaggedText(document, page, section, "/Form", "name", "Ada Lovelace", Box, alternateText: "Full name");
        AddTaggedText(document, page, section, "/Form", "town", "London", new PdfRectangle(new XRect(60, 660, 200, 20)), alternateText: "Town");
        AddTaggedText(document, page, section, "/Form", "bare", "None", new PdfRectangle(new XRect(60, 620, 200, 20)));

        // An element that has words of its own keeps them over the field's tool tip.
        var own = document.Structure.CreateElement(new PdfTag("/Form"), section);
        own.AlternateText = "The element's own words";
        var owned = new PdfTextField(document) { Name = "owned", ToolTip = "Ignored" };
        document.AcroForm.Fields.Add(owned);
        var ownedWidget = owned.AddWidget(page, new PdfRectangle(new XRect(60, 580, 200, 20)));
        owned.Text = "Owned";
        document.Structure.AddAnnotation(own, page, ownedWidget);

        var reopened = Flattened(document, readFromFile);

        var spans = Elements(StructureRoot(reopened)).Where(element => element.Elements.GetName("/S") == "/Span").ToList();
        spans.Select(span => span.Elements.GetString("/Alt")).Should().BeEquivalentTo(
            new[] { "Full name", "Town", "", "The element's own words" });
    }

    // ----- what is not drawn, and what has nowhere to go ---------------------------------------------------------

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AHiddenWidgetDrawsNothingAndItsElementGoesWhileTheDrawnOnesStay(bool readFromFile)
    {
        var document = ATaggedForm(fields: [("shown", "Shown value"), ("secret", "Secret value")]);
        document.AcroForm.Fields["secret"].Widgets[0].Flags |= PdfAnnotationFlags.Hidden;

        var reopened = Flattened(document, readFromFile);

        var page = reopened.Pages[0];
        Drawn(page).Should().ContainSingle();
        ObjectReferences(reopened).Should().BeEmpty();
        var names = Elements(StructureRoot(reopened)).Select(element => element.Elements.GetName("/S")).ToList();
        names.Count(name => name == "/Span").Should().Be(1);
        names.Should().NotContain("/Form", "the hidden control's element held nothing else");
        PdfTextExtractor.ExtractText(page).Should().Contain("Shown value").And.NotContain("Secret value");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AnElementHoldingADrawnAndAHiddenWidgetKeepsTheDrawnOne(bool readFromFile)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var element = document.Structure.CreateElement(new PdfTag("/Form"));
        var field = new PdfTextField(document) { Name = "pair" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        var drawn = field.AddWidget(page, Box);
        var hidden = field.AddWidget(page, new PdfRectangle(new XRect(60, 660, 200, 20)));
        field.Text = "Drawn value";
        hidden.Flags |= PdfAnnotationFlags.Hidden;
        document.Structure.AddAnnotation(element, page, drawn);
        document.Structure.AddAnnotation(element, page, hidden);

        var reopened = Flattened(document, readFromFile);

        var sequence = Drawn(reopened.Pages[0]).Should().ContainSingle().Subject;
        ElementsNaming(reopened, reopened.Pages[0], sequence.Mcid).Should().ContainSingle()
            .Which.Elements.GetName("/S").Should().Be("/Span");
        ObjectReferences(reopened).Should().BeEmpty();
        ParentTreeKeys(reopened).Should().BeEquivalentTo(new[] { reopened.Pages[0].Elements.GetInteger("/StructParents") });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AWidgetNoElementHoldsIsDrawnAsAnArtifact(bool readFromFile)
    {
        var document = ATaggedForm(fields: [("tagged", "In the tree")]);
        var page = document.Pages[0];
        var orphan = new PdfTextField(document) { Name = "orphan" };
        document.AcroForm.Fields.Add(orphan);
        orphan.AddWidget(page, new PdfRectangle(new XRect(60, 500, 200, 20)));
        orphan.Text = "Not in the tree";

        var reopened = Flattened(document, readFromFile);

        var content = ContentOf(reopened.Pages[0]);
        Drawn(reopened.Pages[0]).Should().ContainSingle("the one the tree holds");
        Regex.Count(content, @"/Artifact\s+BMC\s+q[^E]*? Do Q\s*EMC").Should().Be(1, "the one it does not");

        // An artifact is what the extractor leaves out of the text, and keeps among the runs.
        PdfTextExtractor.ExtractText(reopened.Pages[0]).Should().NotContain("Not in the tree");
        PdfTextExtractor.ExtractRuns(reopened.Pages[0]).Where(run => run.IsArtifact).Select(run => run.Text)
            .Should().Contain("Not in the tree");
    }

    // ----- reading it back ---------------------------------------------------------------------------------------

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void TheValuesAreStillTextOnThePageInTheMarkedContent(bool readFromFile)
    {
        var reopened = Flattened(ATaggedForm(), readFromFile);

        var page = reopened.Pages[0];
        PdfTextExtractor.ExtractText(page).Should().Contain("Ada Lovelace").And.Contain("Canada").And.Contain("Registration");

        // The sequence open around the Do applies to the text in the form it draws. The run's own
        // innermost sequence is the appearance's /Tx, which is a plain BMC, so what is read here is
        // that the value is content and not an artifact; the identifier is read from the stream above.
        var runs = PdfTextExtractor.ExtractRuns(page);
        foreach (var value in new[] { "Ada Lovelace", "Canada" })
            runs.Should().ContainSingle(candidate => candidate.Text.Contains(value)).Which.IsArtifact.Should().BeFalse();
        Drawn(page).Should().HaveCount(2);
    }

    // ----- refusals -----------------------------------------------------------------------------------------------

    [Test]
    public void ASignedFormIsStillRefusedAndNothingChanges()
    {
        var document = ATaggedForm();
        var page = document.Pages[0];
        var signature = new PdfSignatureField(document) { Name = "approval" };
        document.AcroForm.Fields.Add(signature);
        signature.AddWidget(page, new PdfRectangle(new XRect(60, 400, 200, 40)));
        signature.Elements.SetString("/V", "signed");

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*signed*");
        var after = document.Reopened();
        after.AcroForm.Should().NotBeNull();
        Drawn(after.Pages[0]).Should().BeEmpty();
        ObjectReferences(after).Should().HaveCount(2);
    }

    [Test]
    public void ADocumentMarkedAsTaggedWithNoStructureTreeIsRefusedAndNothingChanges()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = new PdfTextField(document) { Name = "name" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, Box);
        var markInfo = new PdfDictionary(document);
        markInfo.Elements.SetBoolean("/Marked", true);
        document.Internals.Catalog.Elements["/MarkInfo"] = markInfo;

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*no structure tree*");
        document.Reopened().AcroForm.Should().NotBeNull();
    }

    [Test]
    public void AnUntaggedDocumentIsFlattenedWithNoMarkedContentAtAll()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = new PdfTextField(document) { Name = "name" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, Box);
        field.Text = "Ada";

        document.AcroForm.Flatten();

        ContentOf(document.Reopened().Pages[0]).Should().NotContain("BDC").And.NotContain("BMC");
    }

    [Test]
    public void AWidgetWithNoStructParentStillGetsItsMarkMappedInThePagesExistingArray()
    {
        // Another producer's file: the element holds the widget, but the widget never said which
        // parent tree key it is filed under, so there is no key of the annotation's to take out.
        var document = ATaggedForm(fields: [("name", "Ada Lovelace")]).Reopened();
        var widget = document.AcroForm.Fields["name"].Widgets[0];
        widget.Elements.Remove("/StructParent");
        var reopened = Flattened(document, readFromFile: false);

        var page = reopened.Pages[0];
        var drawn = Drawn(page).Should().ContainSingle().Subject;
        ElementsNaming(reopened, page, drawn.Mcid).Should().ContainSingle();
        ParentTreeElement(reopened, page, drawn.Mcid).Should().BeSameAs(ElementsNaming(reopened, page, drawn.Mcid)[0]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AnElementThatHoldsAnotherAnnotationAsWellKeepsItsTypeAndItsSequenceSaysSo(bool readFromFile)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var element = document.Structure.CreateElement(new PdfTag("/Form"));
        var field = new PdfTextField(document) { Name = "name" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        field.Text = "Ada";
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) };
        page.Annotations.Add(note);
        document.Structure.AddAnnotation(element, page, widget);
        document.Structure.AddAnnotation(element, page, note);

        var reopened = Flattened(document, readFromFile);

        var sequence = Drawn(reopened.Pages[0]).Should().ContainSingle().Subject;
        sequence.Tag.Should().Be("/Form", "the sequence says what its element is");
        ElementsNaming(reopened, reopened.Pages[0], sequence.Mcid).Should().ContainSingle()
            .Which.Elements.GetName("/S").Should().Be("/Form");
    }

    // ----- the identifiers a page already uses ----------------------------------------------------------------

    /// <summary>
    ///   A tagged form whose page also carries <paramref name="operators"/>, written to a file and read
    ///   back - a page of another producer, whose content says things the parent tree does not.
    /// </summary>
    private static PdfDocument ATaggedFormWithContent(string operators, bool namedProperty = false)
    {
        var document = ATaggedForm();
        var page = document.Pages[0];
        if (namedProperty)
        {
            var one = new PdfDictionary(document);
            one.Elements.SetInteger("/MCID", 1);
            var properties = new PdfDictionary(document);
            properties.Elements["/Pr1"] = one;
            page.Resources.Elements["/Properties"] = properties;
        }
        page.Contents.AppendContent().CreateStream(System.Text.Encoding.ASCII.GetBytes(operators));
        return document.Reopened();
    }

    private static int ParentArrayLength(PdfDocument document, PdfPage page) =>
        (Deref(ParentTreeNode(document, page.Elements.GetInteger("/StructParents")).Elements["/Value"]) as PdfArray).Elements.Count;

    /// <summary>The identifiers the two flattened values were given, after flattening the form.</summary>
    private static List<int> NewIdentifiers(PdfDocument document, out int arrayLength)
    {
        var reopened = Flattened(document, readFromFile: false);
        var page = reopened.Pages[0];
        arrayLength = ParentArrayLength(reopened, page);
        return Drawn(page).Select(sequence => sequence.Mcid).Order().ToList();
    }

    [Test]
    [Timeout(30_000)]
    public void TextThatSaysMcidIsNotAnIdentifier(CancellationToken cancellationToken)
    {
        var document = ATaggedFormWithContent("BT (/MCID 100000000) Tj ET\n");

        var identifiers = NewIdentifiers(document, out var arrayLength);

        identifiers.Should().Equal(new[] { 1, 2 }); // the heading has 0 and the text is not a sequence
        arrayLength.Should().Be(3);
    }

    [Test]
    [Timeout(30_000)]
    public void ACommentThatSaysMcidIsNotAnIdentifier(CancellationToken cancellationToken)
    {
        var document = ATaggedFormWithContent("q Q % /MCID 100000000\n");

        var identifiers = NewIdentifiers(document, out var arrayLength);

        identifiers.Should().Equal(new[] { 1, 2 });
        arrayLength.Should().Be(3);
    }

    [Test]
    [Timeout(30_000)]
    public void ARealSequenceWithAHugeIdentifierTakesOnlyItsOwnNumber(CancellationToken cancellationToken)
    {
        var document = ATaggedFormWithContent("/Span <</MCID 100000000>> BDC q Q EMC\n");

        var identifiers = NewIdentifiers(document, out var arrayLength);

        identifiers.Should().Equal(new[] { 1, 2 }, "the numbers are the lowest that are free, so the huge one is steered round and not counted up from");
        arrayLength.Should().Be(3, "the array grows by the identifiers there are, not by the largest one named");
    }

    [Test]
    [Timeout(30_000)]
    public void AnIdentifierANamedPropertyListGivesIsInUse(CancellationToken cancellationToken)
    {
        var document = ATaggedFormWithContent("/Span /Pr1 BDC q Q EMC\n", namedProperty: true);

        var identifiers = NewIdentifiers(document, out _);

        identifiers.Should().Equal(new[] { 2, 3 }); // 0 is the heading and 1 is the property list
    }

    [Test]
    public void APageWhoseContentCannotBeReadRefusesTheFlattenBeforeAnythingChanges()
    {
        var document = ATaggedFormWithContent("1.2.3 w\n");

        var flatten = () => document.AcroForm.Flatten();

        flatten.Should().Throw<InvalidOperationException>().WithMessage("*content cannot be read*");
        document.AcroForm.Should().NotBeNull();
        ObjectReferences(document).Should().HaveCount(2);
        document.Pages[0].Elements.GetArray("/Annots").Elements.Count.Should().Be(2);
    }
}
