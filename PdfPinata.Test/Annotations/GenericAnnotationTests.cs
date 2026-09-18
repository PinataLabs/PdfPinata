using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   Adding an annotation of a subtype this library has no class for, and giving it the
///   appearance a reader draws it from.
/// </summary>
/// <remarks>
///   <para>
///     Neither was possible from outside the assembly. <see cref="PdfAnnotation"/> is abstract with
///     no public way to set <c>/Subtype</c> and <c>PdfGenericAnnotation</c> was <c>internal</c>, so
///     <see cref="PdfAnnotations.Add"/> — which takes a <see cref="PdfAnnotation"/> — could not be
///     handed one. And every constructor of <c>PdfFormXObject</c> is still internal, so the
///     appearance stream could be drawn on an <see cref="XForm"/> and then not given to anything.
///   </para>
///   <para>
///     Between them that is what made <c>/Square</c>, <c>/Circle</c>, <c>/Line</c> and
///     <c>/FreeText</c> unreachable rather than merely unimplemented.
///   </para>
/// </remarks>
public class GenericAnnotationTests
{
    [Theory]
    [InlineData("/Square", "/Square")]
    [InlineData("Square", "/Square")]
    [InlineData("/FreeText", "/FreeText")]
    [InlineData("Circle", "/Circle")]
    public void AnAnnotationNamesTheSubtypeItWasGiven(string given, string written)
    {
        PdfDocument document = new PdfDocument();
        PdfGenericAnnotation annotation = new PdfGenericAnnotation(given);
        document.AddPage().Annotations.Add(annotation);

        annotation.Elements.GetName("/Subtype").Should().Be(written);
        annotation.Subtype.Should().Be(written);

        // Every annotation is one of these, whatever its subtype.
        annotation.Elements.GetName("/Type").Should().Be("/Annot");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAnnotationWithNoSubtypeIsRefused(string subtype)
    {
        Action act = () => new PdfGenericAnnotation(subtype);

        // A dictionary with no /Subtype is not an annotation any reader can do anything with,
        // and the failure would otherwise turn up in the file rather than at the call.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnAppearanceIsWrittenAsTheNormalOneUnderAp()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfGenericAnnotation square = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(square);
        square.Rectangle = new PdfRectangle(new XRect(40, 40, 120, 60));

        square.SetAppearance(Filled(document, new XSize(120, 60), XColors.RoyalBlue));

        PdfDictionary appearance = square.Elements.GetDictionary("/AP");
        appearance.Should().NotBeNull();

        PdfDictionary normal = (PdfDictionary)appearance.Elements.GetObject("/N");
        normal.Elements.GetName("/Subtype").Should().Be("/Form");
        normal.Stream.Should().NotBeNull();
        normal.Stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnAppearanceMustBelongToTheSameDocument()
    {
        PdfDocument document = new PdfDocument();
        PdfGenericAnnotation square = new PdfGenericAnnotation("/Square");
        document.AddPage().Annotations.Add(square);

        XForm elsewhere = Filled(new PdfDocument(), new XSize(20, 20), XColors.Red);

        Action act = () => square.SetAppearance(elsewhere);

        // A reference into another document's object table would be written as a number that
        // means something else here, which is a corrupt file rather than a missing drawing.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnAppearanceNeedsTheAnnotationToBeOnAPageFirst()
    {
        PdfDocument document = new PdfDocument();
        PdfGenericAnnotation square = new PdfGenericAnnotation("/Square");

        Action act = () => square.SetAppearance(Filled(document, new XSize(20, 20), XColors.Red));

        // Until it is added it has no Owner, so there is no object table to put the form in.
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NamedAppearancesAccumulateAndTheLastOneNamedIsShown()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfGenericAnnotation widget = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(widget);

        widget.SetAppearance("/Off", Filled(document, new XSize(16, 16), XColors.White));
        widget.SetAppearance("Yes", Filled(document, new XSize(16, 16), XColors.Black));

        PdfDictionary states =
            (PdfDictionary)widget.Elements.GetDictionary("/AP").Elements.GetObject("/N");

        // Both are in the file at once - a check box needs them there to be toggled between -
        // and /AS says which is showing. The solidus is added to the one given without it.
        states.Elements.ContainsKey("/Off").Should().BeTrue();
        states.Elements.ContainsKey("/Yes").Should().BeTrue();
        widget.Elements.GetName("/AS").Should().Be("/Yes");
    }

    [Fact]
    public void ASingleAppearanceReplacesASetOfNamedOnes()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfGenericAnnotation annotation = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(annotation);

        annotation.SetAppearance("/Yes", Filled(document, new XSize(16, 16), XColors.Black));
        annotation.SetAppearance(Filled(document, new XSize(16, 16), XColors.Red));

        PdfDictionary normal =
            (PdfDictionary)annotation.Elements.GetDictionary("/AP").Elements.GetObject("/N");

        // One appearance is a form rather than a dictionary of them, and /AS naming a state that
        // is no longer there would leave a reader with nothing to draw.
        normal.Elements.GetName("/Subtype").Should().Be("/Form");
        annotation.Elements.ContainsKey("/AS").Should().BeFalse();
    }

    [Fact]
    public void AFormCannotBeDrawnOnAfterItHasBeenGivenToAnAnnotation()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        PdfGenericAnnotation square = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(square);

        XForm form = new XForm(document, new XSize(40, 40));
        square.SetAppearance(form);

        Action act = () => XGraphics.FromForm(form);

        // SetAppearance finishes the drawing, which is what closes the content stream and sets
        // its length. Documented on the method, and pinned here.
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AnAppearanceThatDrawsNothingIsStillAnAppearance()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        PdfGenericAnnotation widget = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(widget);

        // The "off" state of a check box is an empty content stream. Finishing a form that was
        // never drawn on used to throw a NullReferenceException out of XForm.Finish, which
        // disposed an XGraphics that had never been made.
        Action act = () => widget.SetAppearance("/Off", new XForm(document, new XSize(16, 16)));

        act.Should().NotThrow();

        PdfDictionary states =
            (PdfDictionary)widget.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        states.Elements.ContainsKey("/Off").Should().BeTrue();
    }

    [Fact]
    public void TheShowingStateCanBeNamedWithOrWithoutItsSolidus()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfGenericAnnotation widget = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(widget);

        widget.SetAppearance("/Off", Filled(document, new XSize(16, 16), XColors.White));
        widget.SetAppearance("/Yes", Filled(document, new XSize(16, 16), XColors.Black));

        widget.AppearanceState = "Off";
        widget.AppearanceState.Should().Be("/Off");

        widget.AppearanceState = "/Yes";
        widget.AppearanceState.Should().Be("/Yes");

        widget.AppearanceState = null;
        widget.Elements.ContainsKey("/AS").Should().BeFalse();
    }

    [Fact]
    public void AnAppearanceStateWithNoNameIsRefusedTheWaySettingOneIs()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfGenericAnnotation widget = new PdfGenericAnnotation("/Square");
        page.Annotations.Add(widget);

        // /AS names a state and the empty name names none. SetAppearance has always said so;
        // this used to read the first character of an empty string and throw an
        // IndexOutOfRangeException, which names neither the parameter nor the mistake.
        Action naming = () => widget.AppearanceState = "";

        naming.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    /// <summary>
    ///   An appearance stream that fills itself with one colour, which is the smallest drawing
    ///   that proves a reader painted it.
    /// </summary>
    static XForm Filled(PdfDocument document, XSize size, XColor colour)
    {
        XForm form = new XForm(document, size);
        using (XGraphics gfx = XGraphics.FromForm(form))
        {
            gfx.DrawRectangle(new XSolidBrush(colour), 0, 0, size.Width, size.Height);
        }

        return form;
    }
}
