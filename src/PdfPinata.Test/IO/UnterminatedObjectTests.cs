using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   XnView writes objects that open a dictionary and then end at "endobj" without closing it,
///   which is enough to make a whole document unreadable. The end of the object has to be taken
///   as the end of the dictionary or array that was left open.
///   See https://github.com/ststeiger/PdfSharpCore/issues/278.
/// </summary>
public class UnterminatedObjectTests
{
    [Theory]
    [InlineData(PdfReadAccuracy.Strict)]
    [InlineData(PdfReadAccuracy.Moderate)]
    public void ADocumentHoldingAnObjectThatOpensADictionaryAndNeverClosesItCanBeRead(PdfReadAccuracy accuracy)
    {
        using var input = new MemoryStream(BuildDocumentWhoseFifthObjectIs("<<"));

        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import, accuracy);

        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void TheDictionaryThatWasNeverClosedIsReadAsAnEmptyOne()
    {
        using var input = new MemoryStream(BuildDocumentWhoseFifthObjectIs("<<"));
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var truncated = document.Internals.Catalog.Elements.GetDictionary("/Extra");

        truncated.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void ADictionaryCutShortAfterAKeyKeepsThePairsItDoesHave()
    {
        using var input = new MemoryStream(BuildDocumentWhoseFifthObjectIs("<</Type/Metadata/Subtype"));
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var truncated = document.Internals.Catalog.Elements.GetDictionary("/Extra");

        truncated.Elements.GetName("/Type").Should().Be("/Metadata");
        // The key whose value the writer never got to is the one entry that has to be dropped.
        truncated.Elements.Count.Should().Be(1);
    }

    [Fact]
    public void AnArrayThatWasNeverClosedKeepsTheItemsItDoesHave()
    {
        using var input = new MemoryStream(BuildDocumentWhoseFifthObjectIs("[ 1 2 3"));
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var truncated = document.Internals.Catalog.Elements.GetArray("/Extra");

        truncated.Elements.Count.Should().Be(3);
    }

    [Fact]
    public void ADocumentHoldingAnUnterminatedObjectCanBeMergedAndSaved()
    {
        using var input = new MemoryStream(BuildDocumentWhoseFifthObjectIs("<<"));
        var inputDocument = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var merged = new PdfDocument();
        foreach (var page in inputDocument.Pages)
            _ = merged.AddPage(page);

        using var output = new MemoryStream();
        merged.Save(output, false);

        output.Position = 0;
        PdfPinata.Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Import).PageCount.Should().Be(1);
    }

    /// <summary>
    ///   A single page document whose fifth object is written as given and referenced from the
    ///   catalog, so that a test can reach what reading it made of the object.
    /// </summary>
    private static byte[] BuildDocumentWhoseFifthObjectIs(string body)
    {
        const string content = "0 0 1 RG 10 10 100 100 re S";
        return RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R/Extra 5 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            RawPdf.Stream("", content),
            body
        ]);
    }
}
