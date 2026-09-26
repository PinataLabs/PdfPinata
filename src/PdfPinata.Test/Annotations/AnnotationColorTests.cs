using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   <see cref="PdfAnnotation.Color"/>, the annotation's <c>/C</c> entry. ISO 32000-1 lets any
///   array be written as an indirect object, and the getter used to cast the entry straight to an
///   array - so a reference was not one, and a colour a file stated plainly read back as black.
/// </summary>
public class AnnotationColorTests
{
    private static readonly XColor Teal = XColor.FromArgb(0, 127, 128);

    [Fact]
    public void AColourWrittenAsAnIndirectArrayIsReadThroughItsReference()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) };
        document.AddPage().Annotations.Add(note);

        var colour = new PdfArray(document, new PdfReal(0), new PdfReal(0.5), new PdfReal(1));
        document.Internals.AddObject(colour);
        note.Elements[PdfAnnotation.Keys.C] = colour;

        note.Elements[PdfAnnotation.Keys.C].Should().BeOfType<PdfReference>(
            "an indirect object is stored as its reference, which is the case being tested");
        note.Color.Should().Be(XColor.FromArgb(0, 128, 255));
    }

    [Fact]
    public void AnIndirectColourSurvivesARoundTripThroughAFile()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) };
        document.AddPage().Annotations.Add(note);

        var colour = new PdfArray(document, new PdfReal(0), new PdfReal(0.5), new PdfReal(1));
        document.Internals.AddObject(colour);
        note.Elements[PdfAnnotation.Keys.C] = colour;

        Reopened(document).Pages[0].Annotations[0].Color.Should().Be(XColor.FromArgb(0, 128, 255));
    }

    [Fact]
    public void AColourAssignedIsWrittenAsThreeComponentsAndReadBackUnrounded()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) };
        document.AddPage().Annotations.Add(note);

        note.Color = Teal;

        var written = note.Elements.GetArray(PdfAnnotation.Keys.C);
        written.Elements.Count.Should().Be(3);

        // 127 goes out as 0.4980392 and comes back as 126.999996, which truncating would lose.
        Reopened(document).Pages[0].Annotations[0].Color.Should().Be(Teal);
    }

    [Fact]
    public void AnAnnotationWithNoColourReadsAsBlack()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)) };
        document.AddPage().Annotations.Add(note);

        note.Color.Should().Be(XColors.Black);

        note.Elements[PdfAnnotation.Keys.C] = new PdfArray(document);
        note.Color.Should().Be(XColors.Black, "an empty /C means no colour, and black is the fallback");
    }

    private static PdfDocument Reopened(PdfDocument document) => document.Reopened();
}
