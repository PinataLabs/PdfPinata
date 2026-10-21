using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   A real read from a file is kept even when it is beyond the range a PDF real may have, and the
///   code that rebuilds such a number afterwards - moving it with a resized page, or writing an
///   annotation's geometry back - keeps it too, rather than refusing what the parser accepted.
///   Issue #336.
/// </summary>
public class OutOfRangeRealResizeTests
{
    // Larger than float.MaxValue (about 3.4e38), written out in digits as a file would have it.
    private static readonly string Huge = "1" + new string('0', 39) + ".5";

    private static PdfDocument Open(string annotation)
    {
        var bytes = RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Annots[4 0 R]>>",
            annotation
        ]);
        return Saved.Open(bytes);
    }

    private static void Halve(PdfPage page)
    {
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(new XSize(100, 100), options);
    }

    [Test]
    [Arguments("/XYZ {0} {0} 0")]
    [Arguments("/FitR 0 0 {0} {0}")]
    [Arguments("/FitH {0}")]
    [Arguments("/FitV {0}")]
    public void APageWhoseDestinationHasAnOutOfRangeRealCanBeResized(string form)
    {
        var document = Open($"<</Type/Annot/Subtype/Link/Rect[0 0 10 10]/Dest[3 0 R {string.Format(form, Huge)}]>>");

        var resize = () => Halve(document.Pages[0]);

        resize.Should().NotThrow();
        Saved.Bytes(document).Should().NotBeEmpty();
    }

    [Test]
    [Arguments("/Ink", "/InkList[[0 0 {0} 10]]")]
    [Arguments("/Line", "/L[0 0 {0} 10]")]
    [Arguments("/Polygon", "/Vertices[0 0 {0} 10 20 20]")]
    [Arguments("/Highlight", "/QuadPoints[0 10 {0} 10 0 0 {0} 0]")]
    [Arguments("/Square", "/RD[{0} 0 0 0]")]
    public void APageWhoseAnnotationHasAnOutOfRangeRealCanBeResized(string subtype, string entry)
    {
        var document = Open($"<</Type/Annot/Subtype{subtype}/Rect[0 0 10 10]{string.Format(entry, Huge)}>>");

        var resize = () => Halve(document.Pages[0]);

        resize.Should().NotThrow();
        Saved.Bytes(document).Should().NotBeEmpty();
    }

    [Test]
    public void MovingOneEndOfALineKeepsAnOutOfRangeOtherEnd()
    {
        var document = Open($"<</Type/Annot/Subtype/Line/Rect[0 0 10 10]/L[0 0 {Huge} 10]>>");
        var line = document.Pages[0].Annotations.OfType<PdfLineAnnotation>().Single();

        var move = () => line.Start = new XPoint(5, 5);

        move.Should().NotThrow();
        line.End.X.Should().BeGreaterThanOrEqualTo(1e39);
    }

    [Test]
    public void RedrawingAMarkupWithAnOutOfRangeQuadPointDoesNotThrow()
    {
        var document = Open($"<</Type/Annot/Subtype/Highlight/Rect[0 0 10 10]/QuadPoints[0 10 {Huge} 10 0 0 {Huge} 0]>>");
        var highlight = document.Pages[0].Annotations.OfType<PdfHighlightAnnotation>().Single();

        var recolour = () => highlight.Color = XColors.Yellow;

        recolour.Should().NotThrow();
    }

    [Test]
    public void ACallersOwnOutOfRangeRealIsStillRefused()
    {
        var document = Open("<</Type/Annot/Subtype/Line/Rect[0 0 10 10]/L[0 0 10 10]>>");
        var line = document.Pages[0].Annotations.OfType<PdfLineAnnotation>().Single();

        var move = () => line.Start = new XPoint(1e39, 5);

        move.Should().Throw<System.ArgumentException>();
    }
}
