using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   Builds a document with an interactive form in it, because there is no other way to get one.
/// </summary>
/// <remarks>
///   <para>
///   Every field class in <c>PdfPinata.Pdf.AcroForms</c> has internal constructors only: a
///   field reaches a caller by being read out of a document, where the field collection looks at
///   the <c>/FT</c> entry and the flags and decides which class the dictionary becomes. So a test
///   that wants a <c>PdfCheckBoxField</c> has to write the dictionary that makes one, save the
///   document, and read it back.
///   </para>
///   <para>
///   That round trip is the point rather than an inconvenience - it is exactly what a caller
///   opening somebody else's form goes through, and it means these tests exercise the type
///   transformation as well as whatever they were written for.
///   </para>
/// </remarks>
internal sealed class AcroFormBuilder
{
    private readonly List<PdfDictionary> _fields = [];
    private readonly List<PdfDictionary> _widgets = [];
    private readonly PdfPage _page;

    internal AcroFormBuilder()
    {
        _page = Document.AddPage();
    }

    /// <summary>The document being built, for tests that need to reach past the form.</summary>
    internal PdfDocument Document { get; } = new();

    /// <summary>
    ///   Adds a field. <paramref name="describe"/> receives the field's dictionary, already
    ///   carrying the entries every widget needs, and adds whatever the field type wants.
    /// </summary>
    internal AcroFormBuilder With(string fieldType, string name,
        System.Action<PdfDictionary> describe = null)
    {
        var field = new PdfDictionary(Document);
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetName(PdfAcroField.Keys.FT, fieldType);
        field.Elements.SetString(PdfAcroField.Keys.T, name);
        field.Elements.SetString(PdfAcroField.Keys.DA, "/Helv 10 Tf 0 g");
        field.Elements[PdfAcroField.Keys.Rect] = new PdfRectangle(new XRect(20, 20, 200, 20));
        describe?.Invoke(field);

        Document.Internals.AddObject(field);
        _fields.Add(field);
        return this;
    }

    /// <summary>
    ///   Adds a field with children of its own, so that the parts of the API that walk the tree
    ///   have a tree to walk.
    /// </summary>
    /// <remarks>
    ///   The children are widget annotations in their own right: each carries a rectangle and a
    ///   <c>/Parent</c> back to the field above it, and each is listed in the page's
    ///   <c>/Annots</c>, while only the parent goes into the form's <c>/Fields</c>. PDFsharp's
    ///   tree walking reads <c>/Kids</c> and <c>/T</c> and would be satisfied with less, but a
    ///   fixture that is not a form a viewer would accept is not worth much as a fixture.
    /// </remarks>
    internal AcroFormBuilder WithParent(string name, params (string Type, string Name)[] kids)
    {
        var parent = new PdfDictionary(Document);
        parent.Elements.SetString(PdfAcroField.Keys.T, name);
        // Before the children, so that they have something to point at.
        Document.Internals.AddObject(parent);

        var children = new PdfArray(Document);
        for (var idx = 0; idx < kids.Length; idx++)
        {
            var (type, kidName) = kids[idx];
            var kid = new PdfDictionary(Document);
            kid.Elements.SetName("/Type", "/Annot");
            kid.Elements.SetName("/Subtype", "/Widget");
            kid.Elements.SetName(PdfAcroField.Keys.FT, type);
            kid.Elements.SetString(PdfAcroField.Keys.T, kidName);
            kid.Elements[PdfAcroField.Keys.Parent] = parent.Reference;
            kid.Elements[PdfAcroField.Keys.Rect] =
                new PdfRectangle(new XRect(20, 20 + 30 * idx, 200, 20));
            Document.Internals.AddObject(kid);
            children.Elements.Add(kid.Reference);
            _widgets.Add(kid);
        }
        parent.Elements[PdfAcroField.Keys.Kids] = children;

        _fields.Add(parent);
        return this;
    }

    /// <summary>Writes the document out and reads it back, so the fields become field objects.</summary>
    internal PdfDocument Build()
    {
        var annotations = new PdfArray(Document);
        var fields = new PdfArray(Document);
        foreach (var field in _fields)
        {
            // Only the top of the tree is a form field. A field that is its own widget is drawn on
            // the page as well; a parent that only holds children is not.
            fields.Elements.Add(field.Reference);
            if (field.Elements.ContainsKey(PdfAcroField.Keys.Rect))
                annotations.Elements.Add(field.Reference);
        }
        foreach (var widget in _widgets)
            annotations.Elements.Add(widget.Reference);
        _page.Elements.SetObject("/Annots", annotations);

        var form = new PdfDictionary(Document);
        form.Elements.SetObject(PdfAcroForm.Keys.Fields, fields);
        Document.Internals.AddObject(form);
        Document.Internals.Catalog.Elements["/AcroForm"] = form.Reference;

        return Document.Reopened();
    }

    // ----- the entries individual field types want ------------------------------------------------

    /// <summary>An appearance dictionary naming the on and off states of a tickable field.</summary>
    internal static void WithOnAndOffAppearances(PdfDictionary field, string onState = "/Yes")
    {
        var document = field.Owner;
        var normal = new PdfDictionary(document)
        {
            Elements =
            {
                [onState] = new PdfDictionary(document),
                ["/Off"] = new PdfDictionary(document)
            }
        };

        var appearance = new PdfDictionary(document) { Elements = { ["/N"] = normal } };
        field.Elements["/AP"] = appearance;
    }

    /// <summary>
    ///   The <c>/Opt</c> array a choice field or a radio group picks its value out of. Spelled out
    ///   rather than taken from a Keys class, because the two that declare it - PdfChoiceField and
    ///   PdfRadioButtonField - declare it separately and neither is reachable from here.
    /// </summary>
    internal static void WithOptions(PdfDictionary field, params string[] options)
    {
        var document = field.Owner;
        var opt = new PdfArray(document);
        foreach (var option in options)
            opt.Elements.Add(new PdfString(option));
        field.Elements["/Opt"] = opt;
    }

    internal static void WithFlags(PdfDictionary field, PdfAcroFieldFlags flags) =>
        field.Elements.SetInteger(PdfAcroField.Keys.Ff, (int)flags);

    /// <summary>
    ///   Adds a field that has both a type of its own and children of its own, describing each
    ///   child separately. <see cref="WithParent"/> makes a bare grouping node; this makes the
    ///   shape the check box setter calls "fields that exist twice with the same name", where the
    ///   parent is the tick box and the children are the two widgets that draw it.
    /// </summary>
    /// <remarks>
    ///   The children carry no <c>/T</c>: a child without a partial name is the same field as its
    ///   parent rather than a field of its own, which is what makes the pair one tick box in two
    ///   places rather than two tick boxes.
    /// </remarks>
    internal AcroFormBuilder WithTypedParent(string fieldType, string name,
        params System.Action<PdfDictionary>[] describeKids) =>
        WithDescribedParent(name, parent =>
        {
            parent.Elements.SetName(PdfAcroField.Keys.FT, fieldType);
            parent.Elements.SetString(PdfAcroField.Keys.DA, "/Helv 10 Tf 0 g");
        }, describeKids);

    /// <summary>
    ///   Adds a field with children, describing the parent and each child separately - for the
    ///   entries a child inherits, which the parent has to be given and the child has to lack.
    /// </summary>
    internal AcroFormBuilder WithDescribedParent(string name,
        System.Action<PdfDictionary> describeParent, params System.Action<PdfDictionary>[] describeKids)
    {
        var parent = new PdfDictionary(Document);
        parent.Elements.SetString(PdfAcroField.Keys.T, name);
        describeParent?.Invoke(parent);
        // Before the children, so that they have something to point at.
        Document.Internals.AddObject(parent);

        var children = new PdfArray(Document);
        for (var idx = 0; idx < describeKids.Length; idx++)
        {
            var kid = new PdfDictionary(Document);
            kid.Elements.SetName("/Type", "/Annot");
            kid.Elements.SetName("/Subtype", "/Widget");
            kid.Elements[PdfAcroField.Keys.Parent] = parent.Reference;
            kid.Elements[PdfAcroField.Keys.Rect] =
                new PdfRectangle(new XRect(20, 20 + 30 * idx, 200, 20));
            describeKids[idx]?.Invoke(kid);
            Document.Internals.AddObject(kid);
            children.Elements.Add(kid.Reference);
            _widgets.Add(kid);
        }
        parent.Elements[PdfAcroField.Keys.Kids] = children;

        _fields.Add(parent);
        return this;
    }
}
