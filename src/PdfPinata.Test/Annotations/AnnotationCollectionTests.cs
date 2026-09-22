using System;
using System.Collections;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   <see cref="PdfAnnotations"/> is the array a page keeps its annotations in, and it is both a
///   <see cref="PdfArray"/> of references and a typed collection that hands back annotation
///   objects. The typed half is where an annotation read from a file is given the class that knows
///   what it is, which is the one thing the array underneath cannot do for itself.
/// </summary>
public class AnnotationCollectionTests
{
    static PdfRectangle ARectangle(double x = 10, double y = 10) => new(new XRect(x, y, 100, 20));

    static PdfPage APageWith(params PdfAnnotation[] annotations)
    {
        var page = new PdfDocument().AddPage();
        foreach (var annotation in annotations)
            page.Annotations.Add(annotation);
        return page;
    }

    static PdfTextAnnotation ANote(string text = "a note")
    {
        return new PdfTextAnnotation { Rectangle = ARectangle(), Contents = text };
    }

    // ----- what the collection holds ------------------------------------------------------------------

    [Fact]
    public void APageStartsWithNoAnnotationsAndSaysSoWithoutMakingAny()
    {
        var page = new PdfDocument().AddPage();

        page.HasAnnotations.Should().BeFalse();
        page.Elements.ContainsKey("/Annots").Should().BeFalse("asking must not write");
    }

    [Fact]
    public void AnAnnotationAddedToAPageIsCountedAndFoundAgain()
    {
        var note = ANote();
        var page = APageWith(note);

        page.HasAnnotations.Should().BeTrue();
        page.Annotations.Count.Should().Be(1);
        page.Annotations[0].Should().BeSameAs(note);
    }

    [Fact]
    public void AnAnnotationCanBeTakenOffThePageAgain()
    {
        var first = ANote("first");
        var second = ANote("second");
        var page = APageWith(first, second);

        page.Annotations.Remove(first);

        page.Annotations.Count.Should().Be(1);
        page.Annotations[0].Elements.GetString("/Contents").Should().Be("second");
    }

    [Fact]
    public void AnAnnotationFromAnotherDocumentCannotBeRemovedFromThisOne()
    {
        var page = APageWith(ANote());
        var elsewhere = new PdfDocument().AddPage();
        var stranger = ANote("elsewhere");
        elsewhere.Annotations.Add(stranger);

        var removing = () => page.Annotations.Remove(stranger);

        removing.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EveryAnnotationCanBeClearedAtOnce()
    {
        var page = APageWith(ANote("one"), ANote("two"), ANote("three"));

        page.Annotations.Clear();

        page.Annotations.Count.Should().Be(0);
    }

    [Fact]
    public void TheCollectionEnumeratesTheAnnotationsInIt()
    {
        var one = ANote("one");
        var two = ANote("two");
        var page = APageWith(one, two);

        // Typed: the loop variable is a PdfAnnotation, with no cast.
        var seen = new List<PdfAnnotation>();
        foreach (var annotation in page.Annotations)
            seen.Add(annotation);

        seen.Should().Equal(one, two);

        var untyped = new List<object>();
        foreach (var annotation in (IEnumerable)page.Annotations)
            untyped.Add(annotation);

        untyped.Should().Equal(one, two);
    }

    [Fact]
    public void EnumeratedAsAnArrayTheCollectionStillYieldsTheAnnotations()
    {
        // The typed enumerator hides PdfArray's rather than overriding it, and a caller holding the
        // collection as an array - or as IEnumerable<PdfItem>, which is what LINQ sees - must not
        // be handed the references underneath instead.
        var one = ANote("one");
        var two = ANote("two");
        var page = APageWith(one, two);

        var asArray = new List<PdfItem>();
        foreach (var item in (PdfArray)page.Annotations)
            asArray.Add(item);

        asArray.Should().Equal(one, two);
        ((IEnumerable<PdfItem>)page.Annotations).Should().Equal(one, two);
    }

    [Fact]
    public void AnAnnotationReadFromAFileIsEnumeratedAsItsOwnClass()
    {
        var document = new PdfDocument();
        document.AddPage().Annotations.Add(ANote());

        var reopened = ReadBack(document);
        var seen = new List<PdfAnnotation>();
        foreach (var annotation in reopened.Pages[0].Annotations)
            seen.Add(annotation);

        seen.Should().ContainSingle().Which.Should().BeOfType<PdfTextAnnotation>();
    }

    // ----- annotations read back out of a file ---------------------------------------------------------

    /// <summary>
    ///   An annotation in a file is a plain dictionary. Asking the collection for one gives it the
    ///   class its subtype names, so a caller reading a file works with the same typed annotation
    ///   it was written from. It used to be a <see cref="PdfGenericAnnotation"/> whatever the
    ///   subtype said; that is now what a subtype with no class of its own becomes.
    /// </summary>
    [Fact]
    public void AnAnnotationReadBackIsHandedOverAsTheClassItsSubtypeNames()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Annotations.Add(ANote("kept"));
        page.AddWebLink(ARectangle(10, 60), "https://example.invalid/");

        var reopened = ReadBack(document);
        var annotations = reopened.Pages[0].Annotations;

        annotations.Count.Should().Be(2);
        annotations[0].Should().BeOfType<PdfTextAnnotation>();
        annotations[0].Contents.Should().Be("kept");
        annotations[1].Should().BeOfType<PdfLinkAnnotation>();
        annotations[1].Elements.GetName("/Subtype").Should().Be("/Link");
    }

    [Fact]
    public void AnAnnotationOfASubtypeWithNoClassOfItsOwnIsStillHandedBack()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var unknown = new PdfDictionary(document);
        unknown.Elements.SetName("/Type", "/Annot");
        unknown.Elements.SetName("/Subtype", "/Wibble");
        unknown.Elements.SetRectangle("/Rect", ARectangle());
        document.Internals.AddObject(unknown);
        page.Elements.GetValue("/Annots", VCF.Create);
        page.Elements.GetArray("/Annots")!.Elements.Add(unknown.Reference);

        var reopened = ReadBack(document);
        var annotation = reopened.Pages[0].Annotations[0];

        annotation.Should().NotBeNull();
        annotation.Elements.GetName("/Subtype").Should().Be("/Wibble");
    }

    [Fact]
    public void AnAnnotationHeldDirectlyRatherThanByReferenceIsStillHandedBack()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var direct = new PdfDictionary(document);
        direct.Elements.SetName("/Type", "/Annot");
        direct.Elements.SetName("/Subtype", "/Wibble");
        direct.Elements.SetRectangle("/Rect", ARectangle());
        page.Elements.GetValue("/Annots", VCF.Create);
        page.Elements.GetArray("/Annots")!.Elements.Add(direct);

        var annotation = page.Annotations[0];

        annotation.Should().NotBeNull();
        annotation.Elements.GetName("/Subtype").Should().Be("/Wibble");
    }

    /// <summary>
    ///   An imported annotation's <c>/P</c> points back at the page it was on in the file it came
    ///   from. Placing the page fixes it to point at the page it is on now.
    /// </summary>
    [Fact]
    public void AnImportedAnnotationIsPointedAtThePageItEndsUpOn()
    {
        var source = new PdfDocument();
        var sourcePage = source.AddPage();
        var note = ANote("travelling");
        sourcePage.Annotations.Add(note);
        note.Elements["/P"] = sourcePage.Reference;

        var imported = ReadBack(source, PdfDocumentOpenMode.Import);
        var target = new PdfDocument();
        var placed = target.AddPage(imported.Pages[0]);

        var annotation = placed.Annotations[0];

        annotation.Elements.GetReference("/P").Should().BeSameAs(placed.Reference);
    }

    static PdfDocument ReadBack(PdfDocument document,
        PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify)
    {
        var output = new System.IO.MemoryStream();
        document.Save(output, false);
        return PdfPinata.Pdf.IO.PdfReader.Open(new System.IO.MemoryStream(output.ToArray()), mode);
    }
}
