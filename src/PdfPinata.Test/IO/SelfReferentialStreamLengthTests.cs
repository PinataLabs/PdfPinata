using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   An indirect /Length is resolved by reading the object it names, and a stream whose length
///   names the stream itself, or a stream that names another whose length names the first, can
///   only be resolved by reading an object that is still being read. That recursed until the
///   stack overflowed, which no caller can catch: one malformed file ended the whole process.
///   Such a length is unknown, like a missing one, and the stream is read up to "endstream".
///   See https://github.com/PinataLabs/PdfPinata/issues/128.
/// </summary>
public class SelfReferentialStreamLengthTests
{
    private const string Content = "0 0 m 100 100 l S";
    private const string OtherContent = "0 0 m 100 0 l S";

    [Theory]
    [InlineData(PdfDocumentOpenMode.Import)]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    public void AStreamWhoseLengthIsTheStreamItselfIsReadUpToTheEndOfTheStream(PdfDocumentOpenMode openMode)
    {
        var document = Read(RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            "<</Length 4 0 R>>stream\n" + Content + "\nendstream"
        }), openMode);

        ContentsOf(document).Should().Equal(Content);
    }

    [Theory]
    [InlineData(PdfDocumentOpenMode.Import)]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    public void TwoStreamsWhoseLengthsAreEachOtherAreBothReadUpToTheEndOfTheStream(PdfDocumentOpenMode openMode)
    {
        var document = Read(RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents[4 0 R 5 0 R]>>",
            "<</Length 5 0 R>>stream\n" + Content + "\nendstream",
            "<</Length 4 0 R>>stream\n" + OtherContent + "\nendstream"
        }), openMode);

        ContentsOf(document).Should().Equal(Content, OtherContent);
    }

    [Fact]
    public void TheLengthRecoveredFromASelfReferentialStreamIsRecorded()
    {
        // The reference is replaced by what was read, so a document written again describes its
        // stream correctly rather than repeating the cycle.
        var document = Read(RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            "<</Length 4 0 R>>stream\n" + Content + "\nendstream"
        }), PdfDocumentOpenMode.Modify);

        document.Pages[0].Contents.Elements.GetDictionary(0).Elements.GetInteger("/Length")
            .Should().Be(Content.Length);
    }

    private static PdfDocument Read(byte[] document, PdfDocumentOpenMode openMode)
    {
        using var input = new MemoryStream(document);
        return Pdf.IO.PdfReader.Open(input, openMode);
    }

    private static List<string> ContentsOf(PdfDocument document)
    {
        var contents = new List<string>();
        foreach (var content in document.Pages[0].Contents)
            contents.Add(Encoding.Latin1.GetString(content.Stream.UnfilteredValue));
        return contents;
    }
}
