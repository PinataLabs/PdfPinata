using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   pdfTeX has written its PTEX.FullBanner into the document information dictionary as two bare
///   strings with no key in front of them for decades. A dictionary entry is a name followed by a
///   value, so the strings are not conforming, but every other reader steps over them and reads
///   the rest of the document. Refusing the whole file over them makes a large body of TeX output
///   unreadable.
///   The timeouts turn a pairing loop that stops advancing into a failure rather than into a
///   hung test host.
///   See https://github.com/empira/PDFsharp/issues/376.
/// </summary>
public class DictionaryEntryWithoutKeyTests
{
    private const string PdfTeXInfoDictionary =
        "<</Creator (LaTeX with hyperref)/CreationDate (D:20260101120000Z) (PTEX.FullBanner)" +
        "(This is pdfTeX, Version 3.141592653-2.6-1.40.29 \\(TeX Live 2026\\))>>";

    [Theory(Timeout = 5000)]
    [InlineData(PdfDocumentOpenMode.Import)]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    public async Task ADocumentWhoseInformationDictionaryHoldsBareStringsCanBeRead(PdfDocumentOpenMode openMode)
    {
        var document = await Read(PdfTeXInfoDictionary, openMode);

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task TheEntriesThatDoHaveAKeyAreStillRead()
    {
        var document = await Read(PdfTeXInfoDictionary, PdfDocumentOpenMode.Modify);

        document.Info.Creator.Should().Be("LaTeX with hyperref");
    }

    [Fact(Timeout = 5000)]
    public async Task TheValuesWithNoKeyAreDropped()
    {
        var document = await Read(PdfTeXInfoDictionary, PdfDocumentOpenMode.Modify);

        // There is nothing to key the two banner strings under, so the two entries that were
        // written properly are the only ones the dictionary can hold.
        document.Info.Elements.Count.Should().Be(2);
    }

    [Fact(Timeout = 5000)]
    public async Task AValueWithNoKeyDoesNotPairTheEntriesAfterItUpWrongly()
    {
        // A single stray value leaves an odd number of items, so a reader that walks the
        // dictionary two at a time reads /Author as the value of the stray string and (Ada) as
        // the key of the next entry.
        var document = await Read(
            "<</Creator (LaTeX)(PTEX.FullBanner)/Author (Ada)/Subject (Poetry)>>",
            PdfDocumentOpenMode.Modify);

        document.Info.Creator.Should().Be("LaTeX");
        document.Info.Author.Should().Be("Ada");
        document.Info.Subject.Should().Be("Poetry");
        document.Info.Elements.Count.Should().Be(3);
    }

    [Fact(Timeout = 5000)]
    public async Task ADictionaryThatIsNothingButValuesIsReadAsAnEmptyOne()
    {
        var document = await Read("<<(one)(two)(three)>>", PdfDocumentOpenMode.Modify);

        document.Info.Elements.Count.Should().Be(0);
    }

    [Fact(Timeout = 5000)]
    public async Task ADocumentWhoseInformationDictionaryHoldsBareStringsCanBeSaved()
    {
        var creator = await Interruptibly.Run(() =>
        {
            using var input = new MemoryStream(BuildDocumentWithInformationDictionary(PdfTeXInfoDictionary));
            var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

            using var output = new MemoryStream();
            document.Save(output, false);

            output.Position = 0;
            return PdfPinata.Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Import).Info.Creator;
        });

        creator.Should().Be("LaTeX with hyperref");
    }

    /// <summary>
    ///   Reads the document on a thread of its own, so that the Timeout on these tests can fail a
    ///   parse that does not terminate rather than wait on it.
    /// </summary>
    private static Task<PdfDocument> Read(string informationDictionary, PdfDocumentOpenMode openMode)
    {
        return Interruptibly.Run(() =>
        {
            using var input = new MemoryStream(BuildDocumentWithInformationDictionary(informationDictionary));
            return PdfPinata.Pdf.IO.PdfReader.Open(input, openMode);
        });
    }

    /// <summary>
    ///   A single page document whose fifth object is the information dictionary written as given,
    ///   referenced from the trailer the way pdfTeX writes it.
    /// </summary>
    private static byte[] BuildDocumentWithInformationDictionary(string informationDictionary)
    {
        const string content = "0 0 1 RG 10 10 100 100 re S";
        return RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            RawPdf.Stream("", content),
            informationDictionary
        ], "/Info 5 0 R");
    }
}
