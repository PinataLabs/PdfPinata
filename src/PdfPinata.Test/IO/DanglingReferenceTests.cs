using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A reference to an object the file never defines is the null object, and an entry whose
///   value is the null object is the same as an entry that is not there at all. The reader
///   already turned such a reference into a null, but the dictionary handed that null on as
///   though it were a value of the declared type, so reading the annotations of a page whose
///   /Annots dangles threw rather than reporting that the page has none.
///   See https://github.com/empira/PDFsharp/issues/248.
/// </summary>
public class DanglingReferenceTests
{
    [Fact]
    public void APageWhoseAnnotationsDangleHasNone()
    {
        var page = FirstPageOf(DocumentWith("/Annots 9 0 R"));

        page.HasAnnotations.Should().BeFalse();
    }

    [Fact]
    public void APageWithNoAnnotationsAtAllHasNone()
    {
        // Asking used to throw here too, on a page that is in no way corrupt.
        var page = FirstPageOf(DocumentWith(""));

        page.HasAnnotations.Should().BeFalse();
    }

    [Fact]
    public void AnAnnotationCanBeAddedToAPageWhoseAnnotationsDangle()
    {
        // The reported failure: signing a document whose /Annots refers to an object that is
        // not in the file.
        var page = FirstPageOf(DocumentWith("/Annots 9 0 R"));

        page.Annotations.Count.Should().Be(0);
        page.Annotations.Add(new PdfTextAnnotation { Title = "A", Contents = "note" });

        page.Annotations.Count.Should().Be(1);
        page.HasAnnotations.Should().BeTrue();
    }

    [Fact]
    public void AnAddedAnnotationSurvivesBeingWrittenAndReadBack()
    {
        var document = Read(DocumentWith("/Annots 9 0 R"));
        document.Pages[0].Annotations.Add(new PdfTextAnnotation { Title = "A", Contents = "note" });

        using var written = new MemoryStream();
        document.Save(written, false);
        written.Position = 0;

        var reopened = Pdf.IO.PdfReader.Open(written, PdfDocumentOpenMode.Modify);
        reopened.Pages[0].Annotations.Count.Should().Be(1);
    }

    [Fact]
    public void APageWhoseBoxDanglesFallsBackToTheDefaultRatherThanThrowing()
    {
        // The same rule reached through a different accessor: a rectangle rather than an array.
        var page = FirstPageOf(DocumentWith("/CropBox 9 0 R"));

        page.Elements.GetRectangle("/CropBox").IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData("/Rotate 9 0 R")]
    [InlineData("/Group 9 0 R")]
    [InlineData("/UserUnit 9 0 R")]
    public void ADanglingEntryOfAnyKindReadsAsNoEntry(string entry)
    {
        var page = FirstPageOf(DocumentWith(entry));

        page.Elements.GetInteger("/Rotate").Should().Be(0);
        page.Elements.GetDictionary("/Group").Should().BeNull();
        page.Elements.GetReal("/UserUnit").Should().Be(0);
    }

    [Fact]
    public void AnEntryThatIsWrittenAsTheNullObjectReadsAsNoEntryEither()
    {
        // Not a dangling reference this time but a null spelled out, which the specification
        // says means the same thing.
        var page = FirstPageOf(DocumentWith("/Annots null"));

        page.HasAnnotations.Should().BeFalse();
    }

    [Fact]
    public void AnEntryThatIsThereIsStillRead()
    {
        var page = FirstPageOf(DocumentWith("/Rotate 90"));

        page.Elements.GetInteger("/Rotate").Should().Be(90);
    }

    private static PdfPage FirstPageOf(byte[] document) => Read(document).Pages[0];

    private static PdfDocument Read(byte[] document)
    {
        return Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);
    }

    /// <summary>
    ///   A one page document carrying the given extra entry in its page dictionary. Object 9,
    ///   which the entries above refer to, is deliberately never written.
    /// </summary>
    private static byte[] DocumentWith(string pageEntry) => RawPdf.Build(new[]
    {
        "<</Type/Catalog/Pages 2 0 R>>",
        "<</Type/Pages/Kids[3 0 R]/Count 1>>",
        "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]" + pageEntry + ">>"
    });
}
