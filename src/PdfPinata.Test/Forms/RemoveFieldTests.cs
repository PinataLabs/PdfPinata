using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Structure;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   Taking a form field out of a document: <see cref="PdfAcroField.PdfAcroFieldCollection.Remove"/>.
///   A field is listed in a form's <c>/Fields</c> or a parent's <c>/Kids</c>, and drawn by widgets
///   that every page lists in its <c>/Annots</c> and a tagged document points at from the structure
///   tree - so removing it by hand leaves a widget on the page with no field behind it, or a
///   structure element pointing at an annotation that is gone. Issue #352.
/// </summary>
/// <remarks>
///   Every test calls the public method, saves, reopens the file and reads the three places a
///   field leaves behind: what the form still lists, what each page's <c>/Annots</c> still holds,
///   and what the structure tree still points at. The last two are read from the raw dictionaries,
///   because an annotation that survives is exactly what a typed read might hide.
/// </remarks>
public class RemoveFieldTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    private static PdfTextField AddText(PdfDocument document, PdfPage page, string name)
    {
        var field = new PdfTextField(document) { Name = name };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, Box);
        return field;
    }

    /// <summary>
    ///   The partial name of the field each widget in every page's <c>/Annots</c> belongs to - its
    ///   own <c>/T</c> when it is merged with its field, else its parent's - read from the raw
    ///   dictionaries.
    /// </summary>
    private static List<string> WidgetOwners(PdfDocument document)
    {
        var owners = new List<string>();
        foreach (var page in document.Pages)
        {
            var annots = page.Elements.GetArray("/Annots");
            if (annots == null)
                continue;

            for (var idx = 0; idx < annots.Elements.Count; idx++)
            {
                var annot = annots.Elements.GetDictionary(idx);
                if (annot.Elements.GetName("/Subtype") != "/Widget")
                    continue;
                owners.Add(annot.Elements.GetString("/T") is { Length: > 0 } own
                    ? own
                    : annot.Elements.GetDictionary("/Parent").Elements.GetString("/T"));
            }
        }
        return owners;
    }

    // ----- the form and the pages --------------------------------------------------------------------

    [Test]
    public void ARemovedFieldIsGoneFromTheFormAndItsWidgetFromThePage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "keep");
        var drop = AddText(document, page, "drop");

        document.AcroForm.Fields.Remove(drop).Should().BeTrue();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("keep");
        reopened.AcroForm.Fields["drop"].Should().BeNull();
        WidgetOwners(reopened).Should().Equal("keep");
    }

    [Test]
    public void AFieldWithWidgetsOnSeveralPagesIsRemovedFromAllOfThem()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var third = document.AddPage();
        AddText(document, second, "keep");
        var drop = AddText(document, first, "drop");
        drop.AddWidget(second, Box);
        drop.AddWidget(third, Box);
        // The widget on the third page claims the second, as the optional /P of a file from other
        // software sometimes does: the widget is found by what the pages list, not by what it says.
        drop.Widgets[2].Elements.SetReference("/P", second);

        document.AcroForm.Fields.Remove(drop);

        var reopened = document.Reopened();
        WidgetOwners(reopened).Should().Equal("keep");
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("keep");
    }

    [Test]
    public void ANestedFieldIsRemovedFromItsParentsKidsAndTheParentSurvives()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var group = new PdfTextField(document) { Name = "group" };
        document.GetOrCreateAcroForm().Fields.Add(group);
        var first = new PdfTextField(document) { Name = "a" };
        var second = new PdfTextField(document) { Name = "b" };
        group.Fields.Add(first);
        group.Fields.Add(second);
        first.AddWidget(page, Box);
        second.AddWidget(page, Box);

        group.Fields.Remove(first).Should().BeTrue();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("group.b");
        reopened.AcroForm.Fields["group"].Should().NotBeNull();
        WidgetOwners(reopened).Should().Equal("b");
    }

    [Test]
    public void RemovingTheLastKidLeavesTheParentInPlace()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var group = new PdfTextField(document) { Name = "group" };
        document.GetOrCreateAcroForm().Fields.Add(group);
        var only = new PdfTextField(document) { Name = "only" };
        group.Fields.Add(only);
        only.AddWidget(page, Box);

        group.Fields.RemoveAt(0);

        var reopened = document.Reopened();
        var parent = reopened.AcroForm.Fields["group"];
        parent.Should().NotBeNull("the parent is a field in its own right");
        parent.Fields.Count.Should().Be(0);
        WidgetOwners(reopened).Should().BeEmpty();
    }

    [Test]
    public void RemovingAParentRemovesItsDescendantsAndTheirWidgets()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        AddText(document, first, "keep");
        var group = new PdfTextField(document) { Name = "group" };
        document.GetOrCreateAcroForm().Fields.Add(group);
        var inner = new PdfTextField(document) { Name = "inner" };
        group.Fields.Add(inner);
        var deep = new PdfTextField(document) { Name = "deep" };
        inner.Fields.Add(deep);
        deep.AddWidget(first, Box);
        deep.AddWidget(second, Box);

        document.AcroForm.Fields.Remove(group).Should().BeTrue();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("keep");
        WidgetOwners(reopened).Should().Equal("keep");
    }

    [Test]
    public void AFieldMergedWithItsWidgetIsRemovedFromTheFormAndThePage()
    {
        var document = AMergedFieldAndWidget("drop", "keep");
        var field = document.AcroForm.Fields["drop"];
        field.Widgets.Single().Field.Should().BeSameAs(field, "it is a field merged with its widget");

        document.AcroForm.Fields.Remove(field).Should().BeTrue();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("keep");
        WidgetOwners(reopened).Should().Equal("keep");
    }

    /// <summary>
    ///   Text fields whose only widget is their own dictionary, as plenty of software other than this
    ///   library writes a field with one widget - listed both in the form and on the page - read back
    ///   from a file.
    /// </summary>
    private static PdfDocument AMergedFieldAndWidget(params string[] names)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        foreach (var name in names)
        {
            var field = new PdfTextField(document) { Name = name };
            form.Fields.Add(field);
            field.Elements.SetName("/Type", "/Annot");
            field.Elements.SetName("/Subtype", "/Widget");
            field.Elements.SetRectangle("/Rect", Box);
            field.Elements.SetReference("/P", page);
            page.Annotations.Elements.Add(field.Reference);
        }
        return document.Reopened();
    }

    [Test]
    public void AFieldNotInTheCollectionIsNotFoundAndNothingChanges()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var group = new PdfTextField(document) { Name = "group" };
        document.GetOrCreateAcroForm().Fields.Add(group);
        var nested = new PdfTextField(document) { Name = "nested" };
        group.Fields.Add(nested);
        nested.AddWidget(page, Box);

        // The form lists the group, not what is nested under it.
        document.AcroForm.Fields.Remove(nested).Should().BeFalse();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("group.nested");
        WidgetOwners(reopened).Should().Equal("nested");
    }


    [Test]
    public void AFieldListedTwiceIsRemovedFromEveryEntry()
    {
        // A file can list a field twice; the second entry would be a field with no widgets.
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "keep");
        var drop = AddText(document, page, "drop");
        document.AcroForm.Elements.GetArray("/Fields").Elements.Add(drop.Reference);

        document.AcroForm.Fields.Remove(drop).Should().BeTrue();

        document.Reopened().AcroForm.Fields.DescendantNames.Should().Equal("keep");
    }
    [Test]
    public void ARemovedFieldLeavesTheCalculationOrder()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var keep = AddText(document, page, "keep");
        var drop = AddText(document, page, "drop");
        var order = new PdfArray(document);
        order.Elements.Add(drop.Reference);
        order.Elements.Add(keep.Reference);
        document.AcroForm.Elements["/CO"] = order;

        document.AcroForm.Fields.Remove(drop);

        var reopened = document.Reopened();
        var saved = reopened.AcroForm.Elements.GetArray("/CO");
        saved.Elements.Count.Should().Be(1);
        saved.Elements.GetDictionary(0).Elements.GetString("/T").Should().Be("keep");
    }

    [Test]
    public void RemovingAtAPositionCountsFieldsOnly()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "first");
        AddText(document, page, "second");

        document.AcroForm.Fields.RemoveAt(1);

        document.Reopened().AcroForm.Fields.DescendantNames.Should().Equal("first");
        Action tooFar = () => document.AcroForm.Fields.RemoveAt(1);
        tooFar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ARemovalInAnAppendedRevisionIsInTheRevision()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "keep");
        AddText(document, page, "drop");
        var original = Saved.Bytes(document);

        var updated = Revisions.AppendChange(original, appended =>
            appended.AcroForm.Fields.Remove(appended.AcroForm.Fields["drop"]));

        var reopened = Saved.Open(updated);
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("keep");
        WidgetOwners(reopened).Should().Equal("keep");
    }

    // ----- the structure tree --------------------------------------------------------------------------

    /// <summary>
    ///   A tagged form of two text fields on one page, each widget the content of a <c>/Form</c>
    ///   element of its own.
    /// </summary>
    private static PdfDocument ATaggedForm()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        document.Structure.RegisterPage(page);
        foreach (var name in new[] { "keep", "drop" })
        {
            var field = AddText(document, page, name);
            var element = document.Structure.CreateElement(new PdfTag("/Form"));
            document.Structure.AddAnnotation(element, page, field.Widgets[0]);
        }
        return document;
    }

    /// <summary>
    ///   The structure tree of a saved document holds exactly the widgets that are still on a page:
    ///   one <c>/OBJR</c> and one <c>/Form</c> element for each, no <c>/OBJR</c> to anything a page
    ///   does not list, and a parent tree entry for each widget's <c>/StructParent</c> and for
    ///   nothing else but the page.
    /// </summary>
    private static void AssertTreeHoldsOnlyTheWidgetsOnThePages(PdfDocument saved)
    {
        var listed = new List<PdfDictionary>();
        foreach (var page in saved.Pages)
        {
            var annots = page.Elements.GetArray("/Annots");
            for (var idx = 0; annots != null && idx < annots.Elements.Count; idx++)
                listed.Add(annots.Elements.GetDictionary(idx));
        }

        var root = saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");
        var objrs = new List<PdfDictionary>();
        var forms = new List<PdfDictionary>();
        Walk(root.Elements["/K"], objrs, forms, 0);

        var named = objrs.Select(objr => objr.Elements.GetDictionary("/Obj")).ToList();
        named.Should().HaveCount(listed.Count, "each annotation a page lists has one object reference");
        new HashSet<PdfDictionary>(named).SetEquals(listed)
            .Should().BeTrue("every object reference names an annotation a page lists");
        forms.Should().HaveCount(listed.Count, "a /Form element with nothing in it stands for nothing");

        var keys = ParentTreeKeys(root);
        var wanted = listed.Select(annot => annot.Elements.GetInteger("/StructParent")).ToList();
        foreach (var page in saved.Pages)
            wanted.Add(page.Elements.GetInteger("/StructParents"));
        keys.Should().BeEquivalentTo(wanted, "the parent tree names the annotations on the pages and the pages");
    }

    private static void Walk(PdfItem item, List<PdfDictionary> objrs, List<PdfDictionary> forms, int depth)
    {
        item = item is global::PdfPinata.Pdf.Advanced.PdfReference reference ? reference.Value : item;
        if (item == null || depth > 64)
            return;

        if (item is PdfArray array)
        {
            foreach (var kid in array.Elements.Items)
                Walk(kid, objrs, forms, depth + 1);
        }
        else if (item is PdfDictionary dict)
        {
            if (dict.Elements.GetName("/Type") == "/OBJR")
                objrs.Add(dict);
            if (dict.Elements.GetName("/S") == "/Form")
                forms.Add(dict);
            Walk(dict.Elements["/K"], objrs, forms, depth + 1);
        }
    }

    /// <summary>The keys of the parent tree, read from its <c>/Nums</c> arrays.</summary>
    private static List<int> ParentTreeKeys(PdfDictionary root)
    {
        var keys = new List<int>();
        var pending = new Stack<PdfDictionary>();
        pending.Push(root.Elements.GetDictionary("/ParentTree"));
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.Elements.GetArray("/Nums") is { } nums)
            {
                for (var idx = 0; idx < nums.Elements.Count; idx += 2)
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

    [Test]
    public void ATaggedFormRemovedInTheSessionThatMadeItKeepsAValidStructureTree()
    {
        var document = ATaggedForm();
        AssertTreeHoldsOnlyTheWidgetsOnThePages(document.Reopened());

        document.AcroForm.Fields.Remove(document.AcroForm.Fields["drop"]);

        var reopened = document.Reopened();
        WidgetOwners(reopened).Should().Equal("keep");
        AssertTreeHoldsOnlyTheWidgetsOnThePages(reopened);
    }

    [Test]
    public void ATaggedFormReadFromAFileKeepsAValidStructureTreeAfterARemoval()
    {
        var document = ATaggedForm().Reopened();
        document.Internals.Catalog.Elements.ContainsKey("/StructTreeRoot").Should().BeTrue("the tree is the file's, not this session's");

        document.AcroForm.Fields.Remove(document.AcroForm.Fields["drop"]);

        var reopened = document.Reopened();
        WidgetOwners(reopened).Should().Equal("keep");
        AssertTreeHoldsOnlyTheWidgetsOnThePages(reopened);
    }

    [Test]
    public void RemovingEveryFieldOfATaggedFormLeavesNoFormElementsAtAll()
    {
        var document = ATaggedForm().Reopened();

        document.AcroForm.Fields.RemoveAt(1);
        document.AcroForm.Fields.RemoveAt(0);

        var reopened = document.Reopened();
        WidgetOwners(reopened).Should().BeEmpty();
        AssertTreeHoldsOnlyTheWidgetsOnThePages(reopened);
    }

    [Test]
    public void ARemovalFromATaggedDocumentIsNotUndoneByTheNextSave()
    {
        // The builder holds an annotation's parent tree entry until it writes the tree at save, so a
        // removal that touched only the tree would come back on the first save and again on the next.
        var document = ATaggedForm();
        document.AcroForm.Fields.Remove(document.AcroForm.Fields["drop"]);
        using var first = new MemoryStream();
        document.Save(first, false);

        AssertTreeHoldsOnlyTheWidgetsOnThePages(document.Reopened());
    }

    // ----- refusals --------------------------------------------------------------------------------------

    private static PdfSignatureField ASignatureField(PdfDocument document, PdfPage page, PdfAcroField.PdfAcroFieldCollection fields,
        bool signed)
    {
        var field = new PdfSignatureField(document) { Name = "approval" };
        fields.Add(field);
        field.AddWidget(page, Box);
        if (signed)
        {
            var signature = new PdfDictionary(document);
            signature.Elements.SetName("/Type", "/Sig");
            field.Elements["/V"] = signature;
        }
        return field;
    }

    [Test]
    public void ASignedSignatureFieldCannotBeRemoved()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = ASignatureField(document, page, document.GetOrCreateAcroForm().Fields, signed: true);

        Action act = () => document.AcroForm.Fields.Remove(field);

        act.Should().Throw<InvalidOperationException>().WithMessage("*approval*signed*");
        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("approval");
        WidgetOwners(reopened).Should().Equal("approval");
    }

    [Test]
    public void AParentOfASignedSignatureFieldCannotBeRemovedEither()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var other = AddText(document, page, "other");
        var group = new PdfTextField(document) { Name = "group" };
        document.AcroForm.Fields.Add(group);
        var inner = new PdfTextField(document) { Name = "inner" };
        group.Fields.Add(inner);
        ASignatureField(document, page, inner.Fields, signed: true);

        Action act = () => document.AcroForm.Fields.Remove(group);

        act.Should().Throw<InvalidOperationException>().WithMessage("*approval*signed*");
        var reopened = document.Reopened();
        reopened.AcroForm.Fields.DescendantNames.Should().Equal("other", "group.inner.approval");
        WidgetOwners(reopened).Should().BeEquivalentTo("other", "approval");
    }

    [Test]
    public void AnUnsignedSignatureFieldIsRemovedLikeAnyOther()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = ASignatureField(document, page, document.GetOrCreateAcroForm().Fields, signed: false);

        document.AcroForm.Fields.Remove(field).Should().BeTrue();

        var reopened = document.Reopened();
        reopened.AcroForm.Fields.Count.Should().Be(0);
        WidgetOwners(reopened).Should().BeEmpty();
    }

    [Test]
    [Arguments(PdfDocumentOpenMode.ReadOnly)]
    [Arguments(PdfDocumentOpenMode.Import)]
    public void ADocumentOpenedWithoutTheRightToModifyRefusesARemoval(PdfDocumentOpenMode mode)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        AddText(document, page, "text");
        var opened = document.Reopened(mode);

        Action act = () => opened.AcroForm.Fields.Remove(opened.AcroForm.Fields[0]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*removing a form field*");
        opened.AcroForm.Fields.DescendantNames.Should().Equal("text");
        WidgetOwners(opened).Should().Equal("text");
    }
}
