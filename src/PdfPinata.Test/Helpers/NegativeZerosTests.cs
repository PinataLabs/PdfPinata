using AwesomeAssertions;
using PdfPinata.Test.IO;
using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   What <see cref="NegativeZeros"/> finds: a negative zero among a content stream's numbers, in a
///   page or in a form, and nothing in a string, a name, a comment or a number that is not zero.
/// </summary>
public class NegativeZerosTests
{
    [Test]
    public void ANegativeZeroIsFoundInAPageAndInAFormAndNowhereElse()
    {
        const string page = "1 -0 -0.0 1 0 0 cm -.5 -10 m (-0) Tj <2D30> Tj /-0 gs % -0\n-0.";
        const string form = "q -0 0 m Q";
        var bytes = RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R/Resources<</XObject<</X 5 0 R>>>>>>",
            RawPdf.Stream("", page),
            RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 10 10]", form)
        ]);

        var found = NegativeZeros.In(Saved.Open(bytes, Pdf.IO.PdfDocumentOpenMode.Import));

        found.Should().Equal("page 1: -0", "page 1: -0.0", "page 1: -0.", "form 5: -0");
    }
}
