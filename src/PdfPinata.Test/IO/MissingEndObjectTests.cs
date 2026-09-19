using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   An object that does not say "endobj" leaves the object number of the next one where the
///   keyword should be, and the parser reported that as a token it did not expect. The object
///   before it is whole, and objects are found through the cross-reference table rather than by
///   reading on from where the last one ended, so there is nothing to be gained by refusing the
///   file. An object that did not parse is still reported.
///   See https://github.com/empira/PDFsharp/issues/211.
/// </summary>
/// <remarks>
///   Reading malformed input can hang rather than fail, so every test here runs the parser inside
///   a <see cref="Task"/> that xUnit can time out.
/// </remarks>
public class MissingEndObjectTests
{
    [Fact(Timeout = 5000)]
    public async Task ADocumentWhoseObjectDoesNotSayEndobjIsRead()
    {
        // The reported file: "Token '60' was not expected", where 60 is the object that follows.
        var document = await Read(Document(endobj: false));

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task TheObjectAfterTheOneMissingEndobjIsReadAsWell()
    {
        var document = await Read(Document(endobj: false));

        // Object 4 is written after the page and is what the page's /Contents refers to.
        document.Pages[0].Contents.Elements.Count.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task ADocumentThatSaysEndobjIsStillRead()
    {
        var document = await Read(Document(endobj: true));

        document.PageCount.Should().Be(1);
    }

    [Theory(Timeout = 5000)]
    [InlineData("42")]              // an integer
    [InlineData("true")]            // a boolean
    [InlineData("/Name")]           // a name
    [InlineData("(a string)")]      // a string
    [InlineData("[1 2 3]")]         // an array
    [InlineData("null")]            // the null object
    public async Task AnObjectOfAnyKindMayLeaveEndobjOut(string body)
    {
        // Object 5 holds the body and does not close; object 6 follows it, so the object number
        // of the next object is what stands where the keyword should be.
        var document = await Read(Document(endobj: true, spare: body, endobjOnTheSpare: false));

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task TheLastObjectOfTheBodyMayLeaveEndobjOut()
    {
        // Nothing follows the last object but the cross-reference table.
        var document = await Read(Document(endobj: true, endobjOnTheLastObject: false));

        document.PageCount.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task AnObjectThatDoesNotParseIsStillReported()
    {
        // "endobj" missing is one thing; a body that is not an object at all is another, and
        // tolerating the first must not quietly accept the second.
        var broken = Document(endobj: true, spare: "<</Good true>> } (rubbish)");

        var read = async () => await Read(broken);

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    [Fact(Timeout = 5000)]
    public async Task ANumberLeftAfterAnObjectIsStillReported()
    {
        // A number only ends an object if it is the object number of the next one. "123" here is
        // followed by "6 0 obj" rather than by a generation number and "obj", so it opens nothing.
        var broken = Document(endobj: true, spare: "<</Good true>> 123", endobjOnTheSpare: false);

        var read = async () => await Read(broken);

        await read.Should().ThrowAsync<PdfReaderException>();
    }

    static Task<PdfDocument> Read(byte[] document)
    {
        return Interruptibly.Run(() => Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify));
    }

    /// <summary>
    ///   A one page document of six objects. The page dictionary is object 3 and its content
    ///   stream object 4; <paramref name="endobj"/> says whether either of them closes with
    ///   "endobj". Object 5 holds <paramref name="spare"/> and closes only if
    ///   <paramref name="endobjOnTheSpare"/> says so, and object 6 ends the body and closes only
    ///   if <paramref name="endobjOnTheLastObject"/> says so.
    /// </summary>
    static byte[] Document(
        bool endobj,
        string spare = "<</Spare true>>",
        bool endobjOnTheSpare = true,
        bool endobjOnTheLastObject = true)
    {
        var pdf = new MemoryStream();
        var offsets = new Dictionary<int, long>();
        const string content = "BT ET";
        const int objects = 6;

        void Write(string text) => pdf.Write(Encoding.Latin1.GetBytes(text));

        void WriteObject(int number, string body, bool close)
        {
            offsets[number] = pdf.Position;
            Write(number + " 0 obj\n" + body + "\n" + (close ? "endobj\n" : ""));
        }

        Write("%PDF-1.4\n");
        WriteObject(1, "<</Type/Catalog/Pages 2 0 R>>", true);
        WriteObject(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>", true);
        WriteObject(3, "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]/Contents 4 0 R>>", endobj);
        WriteObject(4, "<</Length " + content.Length + ">>stream\n" + content + "\nendstream", endobj);
        WriteObject(5, spare, endobjOnTheSpare);
        WriteObject(6, "<</Last true>>", endobjOnTheLastObject);

        var startOfCrossReferenceTable = pdf.Position;
        Write("xref\n0 " + (objects + 1) + "\n0000000000 65535 f \n");
        for (var number = 1; number <= objects; number++)
            Write(offsets[number].ToString("0000000000") + " 00000 n \n");
        Write("trailer\n<</Size " + (objects + 1) + "/Root 1 0 R>>\n");
        Write("startxref\n" + startOfCrossReferenceTable + "\n%%EOF\n");

        return pdf.ToArray();
    }
}
