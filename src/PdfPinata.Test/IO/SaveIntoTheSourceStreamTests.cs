using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Modifying a document in place means reading it from a stream and saving it back into the same
///   stream. Reading leaves the position near the end of the stream, so the save used to start there
///   and keep the whole original file in front of the new one. The document still opened, because a
///   reader locates the last startxref, and the only visible symptom was a file that had roughly
///   doubled in size.
///   See https://github.com/ststeiger/PdfSharpCore/issues/422.
/// </summary>
public class SaveIntoTheSourceStreamTests
{
    [Fact]
    public void SavingIntoTheSourceStreamDoesNotKeepTheOriginalFileInFrontOfTheNewOne()
    {
        var original = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));

        using var inPlace = new MemoryStream();
        inPlace.Write(original, 0, original.Length);
        inPlace.Position = 0;
        Pdf.IO.PdfReader.Open(inPlace, PdfDocumentOpenMode.Modify).Save(inPlace);

        using var input = new MemoryStream(original, false);
        using var separate = new MemoryStream();
        Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify).Save(separate);

        // The two files differ in their document ID and modification date, but nothing else, so
        // saving in place must cost exactly as many bytes as saving into a stream of its own.
        inPlace.Length.Should().Be(separate.Length);
    }

    /// <summary>
    ///   A file that carries the original in front of the new one holds two PDF headers: the one
    ///   the original starts with and the one the save wrote. Counting them tells the two apart,
    ///   where looking at the first bytes of the file cannot, since both files start the same way.
    /// </summary>
    [Fact]
    public void ADocumentSavedIntoItsOwnSourceStreamHasOnlyOnePdfHeader()
    {
        using var pdf = new MemoryStream();
        var original = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));
        pdf.Write(original, 0, original.Length);
        pdf.Position = 0;

        Pdf.IO.PdfReader.Open(pdf, PdfDocumentOpenMode.Modify).Save(pdf);

        var saved = Encoding.Latin1.GetString(pdf.ToArray());
        saved.Should().StartWith("%PDF-");
        saved.IndexOf("%PDF-", 1, StringComparison.Ordinal).Should().Be(-1);
    }

    [Fact]
    public void ADocumentSavedIntoItsOwnSourceStreamCanBeReadBack()
    {
        using var pdf = new MemoryStream();
        var original = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));
        pdf.Write(original, 0, original.Length);
        pdf.Position = 0;

        var pageCount = Pdf.IO.PdfReader.Open(pdf, PdfDocumentOpenMode.Modify).Pages.Count;
        Pdf.IO.PdfReader.Open(pdf, PdfDocumentOpenMode.Modify).Save(pdf);

        Pdf.IO.PdfReader.Open(pdf, PdfDocumentOpenMode.Import).PageCount.Should().Be(pageCount);
    }

    /// <summary>
    ///   The stream is the only copy of the document the caller has left. A save that fails has
    ///   to leave it as it was, rather than empty it and then find it has nothing to put back.
    /// </summary>
    [Fact]
    public void ASaveThatFailsLeavesTheSourceStreamAsItWas()
    {
        using var pdf = new MemoryStream();
        var original = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));
        pdf.Write(original, 0, original.Length);
        pdf.Position = 0;

        var document = Pdf.IO.PdfReader.Open(pdf, PdfDocumentOpenMode.Modify);
        // A document with no pages cannot be written, and says so once the save is under way.
        while (document.Pages.Count > 0)
            document.Pages.RemoveAt(0);

        // ReSharper disable once AccessToDisposedClosure
        document.Invoking(d => d.Save(pdf)).Should().Throw<InvalidOperationException>();

        pdf.ToArray().Should().Equal(original);
    }

    /// <summary>
    ///   Only the stream the document was read from is rewound. A stream the caller has placed
    ///   content in and positioned deliberately is still written to where it was left.
    /// </summary>
    [Fact]
    public void SavingIntoAnUnrelatedStreamWritesAtThePositionTheCallerLeftIt()
    {
        using var input = File.OpenRead(PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf"));
        var document = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var preamble = Encoding.ASCII.GetBytes("preamble");
        using var output = new MemoryStream();
        output.Write(preamble, 0, preamble.Length);

        document.Save(output);

        var written = output.ToArray();
        var signature = Encoding.ASCII.GetBytes("%PDF-");
        written.Length.Should().BeGreaterThan(preamble.Length + signature.Length);
        written[..preamble.Length].Should().Equal(preamble);
        written[preamble.Length..(preamble.Length + signature.Length)].Should().Equal(signature);
    }
}
