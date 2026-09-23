using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Reading the trailer of a damaged file ends, one way or the other: the file opens or the read
///   throws, and the parser never loops.
/// </summary>
/// <remarks>
///   <para>
///     The report is empira/PDFsharp#266: a fuzzed file whose last <c>startxref</c> is followed by
///     <c>&lt;9293</c> rather than by an offset, and nothing after it but <c>%%EOF</c>. The offset
///     is read as a token, the <c>&lt;</c> begins a hexadecimal string, and the string has no
///     closing <c>&gt;</c> - so upstream's scanner, which advanced only on a hex digit or the
///     closing bracket, spun on the end of the file for ever. This fork's two lexers already stop at
///     the end of the input (<see cref="LexerHexStringTests"/>), so that file throws here; the first
///     tests pin it at the level the report was made, through <c>PdfReader.Open</c>, alongside the
///     other things that can stand where the offset should.
///   </para>
///   <para>
///     The loop that walks the revisions was the other way <c>ReadTrailer</c> could fail to end.
///     It followed each trailer's <c>/Prev</c> for as long as there was one, so a section naming
///     itself, or two naming each other, was read round and round for ever. The tests below the
///     first group pin that it now stops at a section it has already read: under
///     <see cref="PdfReadAccuracy.Moderate"/> the document opens, and under
///     <see cref="PdfReadAccuracy.Strict"/>, the default, the cycle is reported.
///   </para>
///   <para>
///     Every test runs the read on a thread of its own under a timeout, because what is being
///     tested is that it ends at all.
///   </para>
/// </remarks>
public class TrailerTerminationTests
{
    // ----- What follows startxref -------------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task AStartxrefFollowedByAnUnterminatedHexStringThrows()
    {
        // The shape of the reported file: "startxref\r\n<9293\r\n%%EOF".
        var read = async () => await Read(Document(startxref: "<9293\r\n%%EOF"));

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    [Fact(Timeout = 5000)]
    public async Task AStartxrefFollowedByAnUnterminatedLiteralStringThrows()
    {
        var read = async () => await Read(Document(startxref: "(9293\r\n%%EOF"));

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    [Fact(Timeout = 5000)]
    public async Task AStartxrefFollowedByNothingThrows()
    {
        var read = async () => await Read(Document(startxref: ""));

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    [Fact(Timeout = 5000)]
    public async Task AStartxrefPointingPastTheEndOfTheFileThrows()
    {
        var read = async () => await Read(Document(startxref: "99999999\n%%EOF"));

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    [Fact(Timeout = 5000)]
    public async Task TheReportedShapeStillOpensWhenTheOffsetIsReal()
    {
        // The control: the same document with an offset where the offset belongs.
        var document = await Read(Document());

        document.PageCount.Should().Be(1);
    }

    // ----- A chain of revisions that comes back on itself ------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task ASectionWhosePrevNamesItselfIsReadOnceUnderModerate()
    {
        var document = await Read(Document(prevToSelf: true), PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task TwoSectionsWhosePrevNamesEachOtherAreEachReadOnceUnderModerate()
    {
        var document = await Read(Document(prevCycleOfTwo: true), PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task ASectionWhosePrevNamesItselfIsReportedUnderStrict()
    {
        var read = async () => await Read(Document(prevToSelf: true));

        (await read.Should().ThrowAsync<PdfReaderException>()).WithMessage("*already been read*");
    }

    [Fact(Timeout = 5000)]
    public async Task TwoSectionsWhosePrevNamesEachOtherAreReportedUnderStrict()
    {
        var read = async () => await Read(Document(prevCycleOfTwo: true));

        (await read.Should().ThrowAsync<PdfReaderException>()).WithMessage("*already been read*");
    }

    [Fact(Timeout = 5000)]
    public async Task AChainOfRevisionsThatEndsIsStillFollowedToItsEnd()
    {
        // The control for the four above, read under Strict: the newest section is empty and names
        // the older one, so the document has pages only if /Prev is still followed, and a chain
        // that ends is not reported as a cycle.
        var document = await Read(Document(emptyNewestRevision: true));

        document.PageCount.Should().Be(1);
    }

    private static Task<PdfDocument> Read(byte[] document, PdfReadAccuracy accuracy = PdfReadAccuracy.Strict) =>
        Interruptibly.Run(() =>
            Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify, accuracy));

    /// <summary>
    ///   A one page document of three objects and one classic cross-reference section.
    /// </summary>
    /// <param name="startxref">
    ///   What follows the last <c>startxref</c>, in place of the offset of the newest section and
    ///   the <c>%%EOF</c> after it. Null writes both as they should be.
    /// </param>
    /// <param name="prevToSelf">Whether the section's trailer names the section itself as /Prev.</param>
    /// <param name="prevCycleOfTwo">
    ///   Whether an empty second section follows, naming the first as /Prev while the first names
    ///   the second.
    /// </param>
    /// <param name="emptyNewestRevision">
    ///   Whether an empty second section follows, naming the first as /Prev, with the first naming
    ///   nothing: an ordinary incremental update that changed nothing.
    /// </param>
    private static byte[] Document(
        string startxref = null,
        bool prevToSelf = false,
        bool prevCycleOfTwo = false,
        bool emptyNewestRevision = false)
    {
        var pdf = new MemoryStream();
        var offsets = new long[4];

        Write("%PDF-1.4\n");
        offsets[1] = pdf.Position;
        Write("1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n");
        offsets[2] = pdf.Position;
        Write("2 0 obj\n<</Type/Pages/Kids[3 0 R]/Count 1>>\nendobj\n");
        offsets[3] = pdf.Position;
        Write("3 0 obj\n<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>\nendobj\n");

        var first = pdf.Position;
        var second = first; // Set once the second section is written.

        // A second section's offset is not known while the first is written, so the first names
        // it through a placeholder of fixed width that is filled in afterwards. Leading zeros are
        // as good an integer as any to the parser.
        const string placeholder = "0000000000";
        long placeholderAt = -1;

        Write("xref\n0 4\n0000000000 65535 f \n");
        for (var number = 1; number <= 3; number++)
            Write(offsets[number].ToString("0000000000") + " 00000 n \n");
        Write("trailer\n<</Size 4/Root 1 0 R");
        if (prevToSelf)
            Write("/Prev " + first);
        if (prevCycleOfTwo)
        {
            Write("/Prev ");
            placeholderAt = pdf.Position;
            Write(placeholder);
        }
        Write(">>\n");

        if (prevCycleOfTwo || emptyNewestRevision)
        {
            second = pdf.Position;
            Write("xref\n0 0\ntrailer\n<</Size 4/Root 1 0 R/Prev " + first + ">>\n");
        }

        Write("startxref\n" + (startxref ?? second + "\n%%EOF\n"));

        var bytes = pdf.ToArray();
        if (placeholderAt >= 0)
            Encoding.Latin1.GetBytes(second.ToString(placeholder)).CopyTo(bytes, placeholderAt);

        return bytes;

        void Write(string text) => pdf.Write(Encoding.Latin1.GetBytes(text));
    }
}
