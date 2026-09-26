using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using PdfPinata.Test.Pdfs.AcroForms;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A field's <c>/Kids</c> holds two kinds of object - the fields nested under it and the widget
///   annotations it is drawn as - and <see cref="PdfAcroField.Fields"/> and
///   <see cref="PdfAcroField.Widgets"/> each list one kind. A dictionary that is both, a field
///   merged with its only widget, is one field whose widget is a view of it, and every way of
///   reaching it gives the same two objects. Issue #146; see
///   <c>docs/specs/field-and-widget-model.md</c>.
/// </summary>
public class FieldAndWidgetTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    private static (PdfDocument Document, PdfTextField Field, PdfWidgetAnnotation Widget) ATextFieldOnAPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        return (document, field, widget);
    }

    /// <summary>
    ///   A text field whose only widget is its own dictionary, as plenty of software other than
    ///   this library writes a field with one widget - listed both in the form and on the page.
    /// </summary>
    private static PdfDocument AMergedFieldAndWidget(PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "merged" };
        form.Fields.Add(field);
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetRectangle("/Rect", Box);
        field.Elements.SetReference("/P", page);
        page.Annotations.Elements.Add(field.Reference);
        return document.Reopened(mode);
    }

    [Fact]
    public void AFieldsWidgetIsAWidgetAndNotAField()
    {
        var (_, field, widget) = ATextFieldOnAPage();

        field.Fields.Count.Should().Be(0, "a widget has no name and is not a field");
        field.Fields.OfType<PdfAcroField>().Should().BeEmpty();
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(widget);
        field.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void AWidgetKnowsTheFieldItDraws()
    {
        var (_, field, widget) = ATextFieldOnAPage();

        widget.Field.Should().BeSameAs(field);
    }

    [Fact]
    public void WalkingTheFieldsLeavesTheWidgetThePageHas()
    {
        // The walk the issue reports: before, every widget it met was retyped as a field, which
        // took the dictionary away from the page's annotations and back again on every read.
        var (document, field, widget) = ATextFieldOnAPage();
        var page = document.Pages[0];

        static void Walk(PdfAcroField node)
        {
            foreach (var kid in node.Fields)
                Walk(kid);
        }

        foreach (var root in document.AcroForm.Fields)
            Walk(root);

        page.Annotations[0].Should().BeSameAs(widget);
        document.AcroForm.Fields[0].Should().BeSameAs(field);
        page.Annotations[0].Should().BeSameAs(widget);
    }

    [Fact]
    public void ReadBackTheFieldAndThePageShareOneWidget()
    {
        var (document, _, _) = ATextFieldOnAPage();
        var reopened = document.Reopened();

        var field = reopened.AcroForm.Fields["text"];
        var fromThePage = reopened.Pages[0].Annotations[0];

        field.Fields.Count.Should().Be(0);
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(fromThePage);
        ((PdfWidgetAnnotation)fromThePage).Field.Should().BeSameAs(field);
        reopened.AcroForm.Fields["text"].Should().BeSameAs(field);
    }

    [Fact]
    public void ATextValueGoesIntoTheFieldAndNotIntoAWidget()
    {
        var (_, field, widget) = ATextFieldOnAPage();

        field.Text = "filled";

        field.Elements.GetString(PdfAcroField.Keys.V).Should().Be("filled");
        widget.Elements.ContainsKey(PdfAcroField.Keys.V).Should().BeFalse();
        widget.Elements.ContainsKey(PdfAnnotation.Keys.AP).Should().BeTrue("the widget is what is drawn");
    }

    [Fact]
    public void NestedFieldsAndTheirWidgetsSortApart()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var name = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(name);
        var first = new PdfTextField(document) { Name = "first" };
        var last = new PdfTextField(document) { Name = "last" };
        name.Fields.Add(first);
        name.Fields.Add(last);
        var firstWidget = first.AddWidget(page, Box);
        last.AddWidget(page, new PdfRectangle(new XRect(60, 660, 200, 20)));

        name.Fields.OfType<PdfAcroField>().Should().Equal(first, last);
        name.Widgets.Should().BeEmpty("a field that holds fields is drawn by theirs");
        name.IsTerminal.Should().BeFalse();
        first.Widgets.Should().ContainSingle().Which.Should().BeSameAs(firstWidget);
        form.Fields.DescendantNames.Should().Equal("name.first", "name.last");
        form.Fields["name.first"].Should().BeSameAs(first);
    }

    [Fact]
    public void AKidsArrayHoldingBothKindsIsSortedKidByKid()
    {
        // Not a shape ISO 32000-1 allows, but one a file can have. A test on the whole array - "the
        // parent holds fields if any kid has a name" - would read the widget as a nameless field.
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var parent = new PdfTextField(document) { Name = "parent" };
        form.Fields.Add(parent);
        var child = new PdfTextField(document) { Name = "child" };
        parent.Fields.Add(child);
        var widget = parent.AddWidget(page, Box);

        parent.Fields.OfType<PdfAcroField>().Should().Equal(child);
        parent.Widgets.Should().Equal(widget);
    }

    [Fact]
    public void ATwinTickBoxHasTwoWidgetsAndNoFields()
    {
        var document = new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid => AcroFormBuilder.WithOnAndOffAppearances(kid, "/Yes"),
                kid => AcroFormBuilder.WithOnAndOffAppearances(kid, "/Yes"))
            .Build();
        var field = document.AcroForm.Fields["agree"];

        field.Fields.Count.Should().Be(0);
        field.Widgets.Should().HaveCount(2);
        field.Widgets.Should().OnlyContain(widget => widget.Field == field);
    }

    [Fact]
    public void ATextFieldDoesNotDrawItsValueIntoTheFieldsNestedUnderIt()
    {
        // The kids here are fields merged with their widgets, so each has a rectangle. The value
        // of the parent used to be drawn into every kid with a rectangle, over the kids' own.
        var document = new AcroFormBuilder()
            .WithDescribedParent("name",
                parent => parent.Elements.SetName(PdfAcroField.Keys.FT, "/Tx"),
                kid => kid.Elements.SetString(PdfAcroField.Keys.T, "first"),
                kid => kid.Elements.SetString(PdfAcroField.Keys.T, "last"))
            .Build();
        var parent = (PdfTextField)document.AcroForm.Fields["name"];

        parent.Text = "the parent's value";

        parent.Fields.Should().HaveCount(2);
        parent.Fields.OfType<PdfAcroField>()
            .Should().OnlyContain(kid => !kid.Elements.ContainsKey(PdfAnnotation.Keys.AP));
    }

    [Fact]
    public void AMergedDictionaryIsOneFieldAndOneWidgetReadFieldFirst()
    {
        var document = AMergedFieldAndWidget();

        var field = document.AcroForm.Fields[0];
        var annotation = document.Pages[0].Annotations[0];

        annotation.Should().BeOfType<PdfWidgetAnnotation>();
        document.AcroForm.Fields[0].Should().BeSameAs(field);
        document.Pages[0].Annotations[0].Should().BeSameAs(annotation);
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(annotation);
        ((PdfWidgetAnnotation)annotation).Field.Should().BeSameAs(field);
    }

    [Fact]
    public void AMergedDictionaryIsOneFieldAndOneWidgetReadPageFirst()
    {
        var document = AMergedFieldAndWidget();

        var annotation = (PdfWidgetAnnotation)document.Pages[0].Annotations[0];
        var field = document.AcroForm.Fields[0];

        field.Should().BeOfType<PdfTextField>();
        annotation.Field.Should().BeSameAs(field);
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(annotation);
        document.Pages[0].Annotations[0].Should().BeSameAs(annotation);
    }

    [Fact]
    public void AMergedFieldStillReachesItsKidsAfterThePageIsRead()
    {
        // This threw NotImplementedException for /Kids: reading the page retyped the dictionary as
        // a widget, which took its entries - and the meta information that says what /Kids holds -
        // away from the field object already made over it.
        var document = AMergedFieldAndWidget();

        var field = document.AcroForm.Fields[0];
        _ = document.Pages[0].Annotations[0];

        field.Fields.Count.Should().Be(0);
        field.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void AValueWrittenThroughTheFieldIsTheWidgetsToo()
    {
        var document = AMergedFieldAndWidget();
        var annotation = document.Pages[0].Annotations[0];
        var field = (PdfTextField)document.AcroForm.Fields[0];

        field.Text = "filled";

        annotation.Elements.GetString(PdfAcroField.Keys.V).Should().Be("filled");
        annotation.Elements.ContainsKey(PdfAnnotation.Keys.AP).Should().BeTrue();

        var reopened = document.Reopened();
        ((PdfTextField)reopened.AcroForm.Fields["merged"]).Text.Should().Be("filled");
    }

    [Fact]
    public void AChangeMadeOnlyThroughTheWidgetIsInTheAppendedRevision()
    {
        // The view writes into the field's entries, which are what an incremental save asks about.
        var stream = new MemoryStream();
        var original = AMergedFieldAndWidget();
        original.Save(stream, false);
        stream.Position = 0;

        var document = Reader.Open(stream, PdfDocumentOpenMode.Append);
        _ = document.AcroForm.Fields[0];
        var widget = document.Pages[0].Annotations[0];
        widget.Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.ReadOnly;

        var output = new MemoryStream();
        document.SaveIncremental(output);
        output.Position = 0;

        var reread = Reader.Open(output, PdfDocumentOpenMode.Modify);
        reread.Pages[0].Annotations[0].Flags
            .Should().Be(PdfAnnotationFlags.Print | PdfAnnotationFlags.ReadOnly);
    }

    [Fact]
    public void ACopiedFieldAddsToItsOwnKidsRatherThanTheOriginals()
    {
        // Copying is memberwise, and a page imported from another document copies its fields. A
        // copy that kept the original's view of /Kids added its children under the original.
        var (document, field, _) = ATextFieldOnAPage();
        _ = field.Fields.Count;
        _ = field.Widgets;

        var copy = (PdfTextField)field.Clone();
        document.Internals.AddObject(copy);
        var child = new PdfTextField(document) { Name = "child" };
        copy.Fields.Add(child);

        copy.Fields.Should().NotBeSameAs(field.Fields);
        child.Elements.GetReference(PdfAcroField.Keys.Parent).Should().BeSameAs(copy.Reference,
            "the child is the copy's, and /Parent says so");
    }

    [Fact]
    public void ACopiedFormHasItsOwnViewOfItsFields()
    {
        var (document, _, _) = ATextFieldOnAPage();
        var form = document.AcroForm;
        _ = form.Fields.Count;

        var copy = (PdfAcroForm)form.Clone();

        copy.Fields.Should().NotBeSameAs(form.Fields);
    }

    [Fact]
    public void ASecondWidgetOnAMergedFieldSeparatesTheFirst()
    {
        var document = AMergedFieldAndWidget();
        var page = document.Pages[0];
        var field = document.AcroForm.Fields[0];
        var first = (PdfWidgetAnnotation)page.Annotations[0];

        var second = field.AddWidget(page, new PdfRectangle(new XRect(60, 660, 200, 20)));

        field.Widgets.Should().Equal(first, second);
        first.Reference.Should().NotBeSameAs(field.Reference, "the first widget is a dictionary of its own now");
        first.Field.Should().BeSameAs(field);
        first.Rectangle.Should().Be(Box);
        page.Annotations.Should().Equal(first, second);
        field.Fields.Should().BeEmpty();
        field.Name.Should().Be("merged");
        field.Elements.ContainsKey(PdfAnnotation.Keys.Rect).Should().BeFalse();
        field.Elements.ContainsKey(PdfAnnotation.Keys.Subtype).Should().BeFalse();
        first.Elements.ContainsKey(PdfAcroField.Keys.T).Should().BeFalse();
    }

    [Fact]
    public void ASeparatedFieldIsWrittenAsAFieldWithTwoWidgets()
    {
        var document = AMergedFieldAndWidget();
        var field = (PdfTextField)document.AcroForm.Fields[0];
        field.AddWidget(document.Pages[0], new PdfRectangle(new XRect(60, 660, 200, 20)));
        field.Text = "twice";

        var reopened = document.Reopened();
        var read = (PdfTextField)reopened.AcroForm.Fields["merged"];

        read.Text.Should().Be("twice");
        read.Widgets.Should().HaveCount(2);
        read.Widgets.Should().OnlyContain(widget => widget.Elements.ContainsKey(PdfAnnotation.Keys.AP));
        reopened.Pages[0].Annotations.Should().Equal(read.Widgets);
        read.Elements.ContainsKey(PdfAnnotation.Keys.Rect).Should().BeFalse();
    }

    [Fact]
    public void AMergedFieldThatCannotBeChangedIsLeftMerged()
    {
        var stream = new MemoryStream();
        AMergedFieldAndWidget().Save(stream, false);
        stream.Position = 0;
        var document = Reader.Open(stream, PdfDocumentOpenMode.Import);
        var field = document.AcroForm.Fields[0];

        var act = () => field.AddWidget(document.Pages[0], new PdfRectangle(new XRect(60, 660, 200, 20)));

        act.Should().Throw<System.InvalidOperationException>();
        field.Elements.ContainsKey(PdfAnnotation.Keys.Rect).Should().BeTrue();
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(document.Pages[0].Annotations[0]);
    }

    [Fact]
    public void AMergedTickBoxStaysTickedWhenItGainsAWidget()
    {
        // /V is the field's and stays with it, and /AS is the widget's and goes with it, so the
        // state has to be read from the field once it has a widget of its own.
        var document = new AcroFormBuilder()
            .With("/Btn", "agree", field => AcroFormBuilder.WithOnAndOffAppearances(field, "/Yes"))
            .Build();
        var box = (PdfCheckBoxField)document.AcroForm.Fields["agree"];
        box.Checked = true;

        box.AddWidget(document.Pages[0], new PdfRectangle(new XRect(60, 660, 20, 20)));

        box.Checked.Should().BeTrue();
        box.Elements.GetName(PdfAcroField.Keys.V).Should().Be("/Yes");
        box.Widgets[0].Elements.GetName(PdfAnnotation.Keys.AS).Should().Be("/Yes");
        box.Elements.ContainsKey(PdfAnnotation.Keys.AS).Should().BeFalse();
    }

    [Fact]
    public void SeparatingAMergedFieldGivesTheWidgetItsOwnTriggers()
    {
        // Focus and blur belong to the annotation, keystroke and validate to the field. Left on a
        // field that is no longer an annotation, the first two would never run.
        var document = AMergedFieldAndWidget();
        var field = document.AcroForm.Fields[0];
        var actions = new PdfDictionary(document);
        foreach (var trigger in new[] { "/Fo", "/Bl", "/K", "/V" })
            actions.Elements[trigger] = new PdfDictionary(document);
        field.Elements[PdfAcroField.Keys.AA] = actions;

        field.AddWidget(document.Pages[0], new PdfRectangle(new XRect(60, 660, 200, 20)));

        var separated = field.Widgets[0].Elements.GetDictionary(PdfAcroField.Keys.AA);
        separated.Elements.Keys.Should().BeEquivalentTo("/Fo", "/Bl");
        field.Elements.GetDictionary(PdfAcroField.Keys.AA).Elements.Keys.Should().BeEquivalentTo("/K", "/V");
    }

    [Fact]
    public void AFieldReportsTheAppearanceStatesOfItsSeparateWidgets()
    {
        var document = new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid => AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja"),
                kid => AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja"))
            .Build();

        document.AcroForm.Fields["agree"].GetAppearanceNames().Should().BeEquivalentTo("/Ja", "/Off");
    }

    [Fact]
    public void AMergedTickBoxViewedBeforeItIsAddedKeepsItsValue()
    {
        // The view took the field's reference when it was made, which was none, and the setter
        // then took the view for a separate widget and removed the /V it had just written.
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var box = new PdfCheckBoxField(document) { Name = "agree" };
        box.Elements.SetName("/Type", "/Annot");
        box.Elements.SetName("/Subtype", "/Widget");
        box.Elements.SetRectangle("/Rect", Box);
        AcroFormBuilder.WithOnAndOffAppearances(box, "/Yes");
        _ = box.Widgets;

        form.Fields.Add(box);
        page.Annotations.Elements.Add(box.Reference);
        box.Checked = true;

        box.Elements.GetName(PdfAcroField.Keys.V).Should().Be("/Yes");
        box.Checked.Should().BeTrue();
        box.Widgets[0].Reference.Should().BeSameAs(box.Reference);
    }
}
