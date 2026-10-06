using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using System;
using System.IO;
using TUnit.Core;

namespace PdfPinata.Test.IO;

public class PdfReader
{
    [Test]
    public void Should_beAbleToReadExistingPdf_When_inputIsStream()
    {
        using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));
        var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
        AssertIsAValidPdfDocumentWithProperties(inputDocument, 38148);
    }

    [Test]
    public void ADocumentReadFromAPathKnowsItsFullPathAndSize()
    {
        var path = PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf");

        var inputDocument = Pdf.IO.PdfReader.Open(path, PdfDocumentOpenMode.Import);

        inputDocument.FullPath.Should().Be(Path.GetFullPath(path));
        AssertIsAValidPdfDocumentWithProperties(inputDocument, 38148);
    }

    [Test]
    public void WillThrowExceptionWhenReadingInvalidPdf()
    {
        using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("NotAValid.pdf"));
        // ReSharper disable once AccessToDisposedClosure
        Action act = () => Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.ReadOnly);
        act.Should().Throw<InvalidOperationException>().WithMessage("The file is not a valid PDF document.");
    }

    internal static void AssertIsAValidPdfDocumentWithProperties(PdfDocument inputDocument, int expectedFileSize)
    {
        inputDocument.Should().NotBeNull();
        inputDocument.FileSize.Should().Be(expectedFileSize);
        inputDocument.Info.Should().NotBeNull();
        inputDocument.PageCount.Should().BeGreaterThan(0);
    }
}
