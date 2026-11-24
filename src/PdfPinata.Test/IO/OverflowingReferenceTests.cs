using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   A number a reference or a trailer's /Prev needs that no integer holds - a garbage run of
///   digits such as "99999999999999999999", which the lexer reads as a real - names no object and
///   no position. The reference reads as the null object, as a reference to an object the file
///   never defines does, and a /Prev that names no position ends the walk through the revisions.
///   Both used to end the read in an InvalidCastException instead.
///   See https://github.com/PinataLabs/PdfPinata/issues/426.
/// </summary>
public class OverflowingReferenceTests
{
    [Test]
    public void AReferenceWhoseObjectNumberFitsNoIntegerReadsAsNull()
    {
        var array = AnnotationsArrayOf(DocumentWith("/Annots [5 0 R 99999999999999999999 0 R]"));

        array.Elements.Count.Should().Be(2);
        array.Elements[1].Should().BeOfType(PdfNull.Value.GetType());
    }

    [Test]
    public void AReferenceWhoseObjectNumberFitsALongButNoIntReadsAsNull()
    {
        var array = AnnotationsArrayOf(DocumentWith("/Annots [5 0 R 4000000000 0 R]"));

        array.Elements.Count.Should().Be(2);
        array.Elements[1].Should().BeOfType(PdfNull.Value.GetType());
    }

    [Test]
    public void TheReferenceBesideAnOverflowingOneIsStillRead()
    {
        var array = AnnotationsArrayOf(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]/Annots [4 0 R 99999999999999999999 0 R]>>",
            "<</Type/Annot/Subtype/Link>>"
        ]));

        var annotation = ((PdfReference)array.Elements[0]).Value.Should().BeAssignableTo<PdfDictionary>().Subject;
        annotation.Elements.GetName("/Subtype").Should().Be("/Link");
    }

    [Test]
    [Arguments("/Prev")]
    [Arguments("/XRefStm")]
    public void ATrailerPositionThatNamesNoPositionOpensTheDocument(string key)
    {
        var document = Read(TrailerWith(key + " 99999999999999999999"), PdfReadAccuracy.Moderate);

        document.Pages.Count.Should().Be(1);
    }

    [Test]
    [Arguments("/Prev")]
    [Arguments("/XRefStm")]
    public void UnderStrictATrailerPositionThatNamesNoPositionIsReported(string key)
    {
        var document = TrailerWith(key + " 99999999999999999999");

        var read = () => Read(document, PdfReadAccuracy.Strict);

        read.Should().Throw<PdfReaderException>().WithMessage("*" + key + "*names no position*");
    }

    /// <summary>
    ///   A one page document carrying the given extra entry in its trailer.
    /// </summary>
    private static byte[] TrailerWith(string trailerEntry) => RawPdf.Build([
        "<</Type/Catalog/Pages 2 0 R>>",
        "<</Type/Pages/Kids[3 0 R]/Count 1>>",
        "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>"
    ], trailerEntry);

    /// <summary>
    ///   The /Annots array of the first page of the given document.
    /// </summary>
    private static PdfArray AnnotationsArrayOf(byte[] document)
    {
        return Read(document).Pages[0].Elements.GetArray("/Annots")!;
    }

    private static PdfDocument Read(byte[] document, PdfReadAccuracy accuracy = PdfReadAccuracy.Moderate)
    {
        return Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify, accuracy);
    }

    /// <summary>
    ///   A one page document carrying the given extra entry in its page dictionary.
    /// </summary>
    private static byte[] DocumentWith(string pageEntry) => RawPdf.Build([
        "<</Type/Catalog/Pages 2 0 R>>",
        "<</Type/Pages/Kids[3 0 R]/Count 1>>",
        "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]" + pageEntry + ">>"
    ]);
}
