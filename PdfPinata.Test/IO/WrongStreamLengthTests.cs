using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The /Length of a stream is what a reader goes by, and producers get it wrong. The documents
///   in https://github.com/empira/PDFsharp/issues/29 declare more than 1600 streams shorter than
///   they are, and reading one has to end up with the stream that is there rather than with the
///   one the dictionary describes - or with an exception about an unexpected token.
/// </summary>
public class WrongStreamLengthTests
{
    private const string Content = "0 0 1 RG 10 10 100 100 re S";

    [Theory]
    [InlineData(-27, PdfReadAccuracy.Strict)]
    [InlineData(-5, PdfReadAccuracy.Strict)]
    // One byte short leaves nothing between the data and the keyword but the byte itself, which
    // is the shape a reader is most likely to take for the end of the stream.
    [InlineData(-1, PdfReadAccuracy.Strict)]
    [InlineData(-1, PdfReadAccuracy.Moderate)]
    [InlineData(5, PdfReadAccuracy.Strict)]
    [InlineData(27, PdfReadAccuracy.Strict)]
    public void AStreamDeclaredTheWrongLengthIsReadAsTheStreamItIs(int difference, PdfReadAccuracy accuracy)
    {
        var document = Read(DocumentWhoseContentDeclares(Content.Length + difference), accuracy);

        ContentOf(document).Should().Be(Content);
    }

    [Theory]
    // The end-of-line before the keyword is not required by anything that gets the length wrong,
    // and it is the end-of-line that belongs to the file rather than to the stream. A blank does
    // not, so a stream recovered from one keeps it.
    [InlineData("\n", "")]
    [InlineData("\r\n", "")]
    [InlineData("\r", "")]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    public void AStreamEndingWithoutTheUsualLineBreakIsStillReadAsTheStreamItIs(string separator, string kept)
    {
        var content = "<</Length " + (Content.Length - 5) + ">>stream\n" + Content + separator + "endstream";

        var document = Read(DocumentWhoseContentIs(content), PdfReadAccuracy.Strict);

        ContentOf(document).Should().Be(Content + kept);
    }

    [Fact]
    public void ACompressedStreamDeclaredTooShortIsReadAsTheStreamItIs()
    {
        var data = Encoding.Latin1.GetString(new FlateDecode().Encode(Encoding.Latin1.GetBytes(Content)));
        var content = "<</Filter/FlateDecode/Length " + (data.Length - 5) + ">>stream\n" + data + "\nendstream";

        var document = Read(DocumentWhoseContentIs(content), PdfReadAccuracy.Strict);

        ContentOf(document).Should().Be(Content);
    }

    [Fact]
    public void TheLengthRecoveredFromAStreamIsRecorded()
    {
        // The dictionary is written out again when the document is saved, and has to describe
        // the stream it now holds rather than the one it was read as.
        var document = Read(DocumentWhoseContentDeclares(Content.Length - 5), PdfReadAccuracy.Strict);

        ContentDictionaryOf(document).Elements.GetInteger("/Length").Should().Be(Content.Length);
    }

    [Fact]
    public void AWrongLengthGivenIndirectlyIsRecoveredFromJustTheSame()
    {
        var document = Read(RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            "<</Length 5 0 R>>stream\n" + Content + "\nendstream",
            (Content.Length - 5).ToString(),
        }), PdfReadAccuracy.Strict);

        ContentOf(document).Should().Be(Content);
    }

    [Fact]
    public void AStreamDeclaredLongerThanTheFileIsReadAsTheStreamItIs()
    {
        // A length reaching past the end of the file describes nothing that is there, so it is
        // no more use than no length at all.
        var document = Read(DocumentWhoseContentDeclares(500_000), PdfReadAccuracy.Strict);

        ContentOf(document).Should().Be(Content);
    }

    [Fact]
    public void AStreamDeclaredToReachTheVeryEndOfTheFileIsReadAsTheStreamItIs()
    {
        // The end-of-line behind "stream" is where the reader stands when it notes the start of
        // the stream, and it is not part of the data. A length that reaches from there to the last
        // byte of the file therefore fits the file by a byte it cannot use, and reading it hands
        // back one byte less than was asked for. That short read has to be taken for the wrong
        // length it is: what follows it is the end of the file, and a stream is not ended by one.
        var document = Read(DocumentWhoseContentDeclaresItReachesTheEndOfTheFile(), PdfReadAccuracy.Strict);

        ContentOf(document).Should().Be(Content);
    }

    [Fact]
    public void AStreamThatNeverEndsIsStillReportedAsUnreadable()
    {
        // Nothing can be recovered from a document that does not hold the keyword at all, and
        // saying so is better than handing back whatever happens to follow.
        var truncated = "<</Length 500>>stream\n" + Content + "\n";

        var read = () => Read(DocumentWhoseContentIs(truncated), PdfReadAccuracy.Strict);

        read.Should().Throw<InvalidOperationException>().WithMessage("*stream length*");
    }

    private static PdfDocument Read(byte[] document, PdfReadAccuracy accuracy)
    {
        using var input = new MemoryStream(document);
        return Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify, accuracy);
    }

    private static string ContentOf(PdfDocument document)
    {
        return Encoding.Latin1.GetString(ContentDictionaryOf(document).Stream.UnfilteredValue);
    }

    private static PdfDictionary ContentDictionaryOf(PdfDocument document)
    {
        return document.Pages[0].Contents.Elements.GetDictionary(0);
    }

    private static byte[] DocumentWhoseContentDeclares(int length)
    {
        return DocumentWhoseContentIs("<</Length " + length + ">>stream\n" + Content + "\nendstream");
    }

    /// <summary>
    ///   A document whose content stream declares exactly the number of bytes between the
    ///   end-of-line behind its "stream" keyword and the end of the file. The length is written as
    ///   a fixed six digits and then patched in place, so that settling on it does not move the
    ///   cross reference table out from under the offsets that were written for it.
    /// </summary>
    private static byte[] DocumentWhoseContentDeclaresItReachesTheEndOfTheFile()
    {
        var document = DocumentWhoseContentIs("<</Length 000000>>stream\n" + Content + "\nendstream");
        var declaration = Encoding.Latin1.GetString(document).IndexOf("<</Length ", StringComparison.Ordinal);
        var startOfStream = Encoding.Latin1.GetString(document).IndexOf(">>stream\n", StringComparison.Ordinal)
                            + ">>stream".Length;

        var length = Encoding.Latin1.GetBytes((document.Length - startOfStream).ToString("D6"));
        Array.Copy(length, 0, document, declaration + "<</Length ".Length, length.Length);
        return document;
    }

    private static byte[] DocumentWhoseContentIs(string content)
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            content,
        });
    }
}
