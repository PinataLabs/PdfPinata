using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A hybrid-reference file: one revision described twice over, by a classic cross-reference
///   table and by a cross-reference stream the trailer names in /XRefStm.
/// </summary>
/// <remarks>
///   <para>
///     ISO 32000-1 7.5.8.4. The point of the arrangement is to stay readable by a reader that knows
///     nothing of object streams: every object living in one is marked <b>free</b> in the classic
///     table, so such a reader sees a document without them, and the stream beside it says where
///     they really are for a reader that does. Acrobat writes files this way, and so does Canva,
///     which is where the file in the report came from.
///   </para>
///   <para>
///     /XRefStm was declared here and read by nothing, so every compressed object was lost: the
///     table calls it free, the stream saying otherwise was never opened, and what is left is a
///     reference to an object the document does not have. Upstream that surfaces as
///     <c>KeyNotFoundException</c> while the page is imported; here it is quieter and worse,
///     because a dangling reference reads as null by design - so the page opens, imports and saves
///     with the resource simply gone. See https://github.com/empira/PDFsharp/issues/388.
///   </para>
///   <para>
///     The documents below are written by hand, because this library writes neither kind of hybrid
///     file. Object 7 - a graphics state the page names - is the compressed one, and every test
///     here turns on whether it is there.
///   </para>
/// </remarks>
public class HybridCrossReferenceTests
{
    [Fact]
    public void TheObjectTheTableCallsFreeIsFoundThroughTheStream()
    {
        GraphicsStateOf(Opened(Hybrid())).Should().NotBeNull();
    }

    [Fact]
    public void ItIsTheSameObjectTheOrdinaryFileHas()
    {
        // The control: the same page, with the object written the ordinary way and named by the
        // table. Both files describe the same document, which is what the report asks for, so what
        // is read out of them has to match.
        GraphicsStateOf(Opened(Hybrid())).Elements.GetReal("/CA")
            .Should().Be(GraphicsStateOf(Opened(Plain())).Elements.GetReal("/CA"));
    }

    [Fact]
    public void ThePageCarriesItIntoADocumentItIsImportedInto()
    {
        // The reported failure. The import walks the page's object graph and copies what it finds,
        // so an object the reader never saw is not copied - and, reading as null rather than as an
        // error, it leaves a page that looks complete.
        var target = new PdfDocument();
        _ = target.AddPage(Opened(Hybrid(), PdfDocumentOpenMode.Import).Pages[0]);

        GraphicsStateOf(target.Pages[0]).Should().NotBeNull();
    }

    [Fact]
    public void ItSurvivesBeingSavedAsAnOrdinaryFile()
    {
        // What is written out is a plain file - the /XRefStm entry is dropped on the way - so the
        // object has to be reachable the ordinary way in the copy or it is lost for good.
        var document = Opened(Hybrid());
        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        GraphicsStateOf(PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0])
            .Should().NotBeNull();
    }

    [Fact]
    public void TheDocumentIsStillTheOneTheClassicTrailerDescribes()
    {
        // A cross-reference stream is a trailer as well as a table, and this one says the catalog
        // is elsewhere. It is read for its entries alone, so the classic trailer beside it - the
        // one this revision really has - is what the document keeps.
        var document = Opened(Hybrid(rootInTheStream: "2 0 R"));

        document.Internals.Catalog.Reference!.ObjectID.ObjectNumber.Should().Be(1);
        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void TheStreamIsReadWhetherOrNotTheTableListsIt()
    {
        // Which is how a real one looks: the stream is an object of the file like any other, and a
        // table that calls the compressed objects free commonly says nothing about it either.
        GraphicsStateOf(Opened(Hybrid(listTheStream: false))).Should().NotBeNull();
    }

    // ----- when the stream cannot be read -------------------------------------------------------

    [Fact]
    public void ADamagedStreamIsRefusedByNameRatherThanBySymptom()
    {
        // /W says every entry is 27 bytes long, and there are four bytes to read them out of.
        Action open = () => Opened(Damaged());

        open.Should().Throw<PdfReaderException>().WithMessage("*/XRefStm*");
    }

    [Fact]
    public void APositionOutsideTheFileIsRefusedTheSameWay()
    {
        Action open = () => Opened(Hybrid(namedPosition: 9_999_999));

        open.Should().Throw<PdfReaderException>().WithMessage("*not inside the file*");
    }

    [Fact]
    public void APositionWhereThereIsNoStreamIsRefusedTheSameWay()
    {
        // In range, and pointing at a dictionary rather than at a cross-reference stream - which
        // used to be read as nothing at all and let through.
        Action open = () => Opened(Hybrid(nameTheCatalog: true));

        open.Should().Throw<PdfReaderException>().WithMessage("*no cross-reference stream*");
    }

    [Fact]
    public void ModerateAccuracyReadsTheDocumentTheTableDescribes()
    {
        // The classic table is a complete section of its own, which is the whole point of writing
        // a file this way, so the document opens. It is not the whole document, though: whatever
        // only the stream said where to find is missing - here, the one compressed object.
        var document = Opened(Damaged(), accuracy: PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
        GraphicsStateOf(document).Should().BeNull();
    }

    [Fact]
    public void ModerateAccuracyKeepsNothingOfAStreamDamagedPartWayThrough()
    {
        // The stream's first entry is good and says where object 7 is; its second points at no
        // object at all. Dropping the stream means dropping all of it, not keeping what was read
        // before the damage was found.
        var document = Opened(DamagedPartWay(), accuracy: PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
        GraphicsStateOf(document).Should().BeNull();
    }

    [Fact]
    public void ModerateAccuracyKeepsNothingOfACompressedEntryReadBeforeTheDamage()
    {
        // The same, with the good entry a compressed one. Those are resolved by PdfReader once the
        // whole trailer chain is read, from every cross-reference stream the parser kept - so a
        // stream dropped from the table but not from that list still put object 7 in the document.
        var document = Opened(DamagedAfterACompressedEntry(), accuracy: PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
        GraphicsStateOf(document).Should().BeNull();
    }

    [Fact]
    public void ANewerRevisionGivingTheStreamsNumberToAnotherObjectKeepsThatObject()
    {
        // The newer revision is read first, so its entry for 6 is in the table, with a position and
        // no value, when the older revision's /XRefStm is merged. Hanging the stream on that entry
        // made the newer object read as a cross-reference stream - empira/PDFsharp#353's shape.
        var document = Opened(WithAnUpdateReusingTheStreamsNumber());

        document.Internals.Catalog.Elements.GetDictionary("/Extra")!.Elements.GetName("/Kind").Should().Be("/Newer");
        GraphicsStateOf(document).Should().NotBeNull("the stream still says where object 7 is");
    }

    [Fact]
    public void ModerateAccuracyOpensAFileWhoseXRefStmNamesNoStream()
    {
        var document = Opened(Hybrid(nameTheCatalog: true), accuracy: PdfReadAccuracy.Moderate);

        document.PageCount.Should().Be(1);
        GraphicsStateOf(document).Should().BeNull();
    }

    // ----- the documents ------------------------------------------------------------------------

    private static PdfDocument Opened(byte[] pdf, PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify,
        PdfReadAccuracy accuracy = PdfReadAccuracy.Strict)
    {
        return PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), mode, accuracy);
    }

    /// <summary>The graphics state the page names, which is the object every test is about.</summary>
    private static PdfDictionary GraphicsStateOf(PdfDocument document) => GraphicsStateOf(document.Pages[0]);

    private static PdfDictionary GraphicsStateOf(PdfPage page)
    {
        return page.Elements.GetDictionary("/Resources")?
            .Elements.GetDictionary("/ExtGState")?
            .Elements.GetDictionary("/GS1");
    }

    /// <summary>
    ///   A hybrid-reference file of seven objects, the seventh of them inside an object stream and
    ///   named by nothing but the cross-reference stream.
    /// </summary>
    /// <param name="namedPosition">What /XRefStm says, when it is not to say where the stream is.</param>
    /// <param name="listTheStream">Whether the classic table has an entry for the stream itself.</param>
    /// <param name="rootInTheStream">The /Root the stream carries in its own trailer dictionary.</param>
    /// <param name="nameTheCatalog">Whether /XRefStm names the catalog's dictionary instead.</param>
    private static byte[] Hybrid(int? namedPosition = null, bool listTheStream = true, string rootInTheStream = "1 0 R",
        bool nameTheCatalog = false)
    {
        var pdf = new Builder();

        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(3, Page);
        pdf.Object(4, RawPdf.Stream("", Content));

        // Object 7 is written in here and nowhere else. /First counts the bytes of the pairs that
        // say which objects are in the stream and where each one starts.
        var packed = "7 0\n" + GraphicsState;
        pdf.Object(5, "<< /Type /ObjStm /N 1 /First 4 /Length " + packed.Length + " >>stream\n" +
                      packed + "\nendstream");

        // One entry, of the three bytes /W asks for: type 2, in object stream 5, first of the
        // objects in it.
        pdf.Object(6, "<< /Type /XRef /Size 8 /W [1 2 1] /Index [7 1] /Root " + rootInTheStream +
                      " /Length 4 >>stream\n\u0002\u0000\u0005\u0000\nendstream");

        var named = nameTheCatalog ? DictionaryOf(pdf, 1) : namedPosition ?? pdf.PositionOf(6);

        return pdf.Finish(
            inUse: listTheStream ? 6 : 5,
            free: [7],
            trailerExtras: " /XRefStm " + named);
    }

    /// <summary>
    ///   A hybrid file whose stream locates object 7 the ordinary way and then names, for object 8,
    ///   a position where no object starts.
    /// </summary>
    private static byte[] DamagedPartWay()
    {
        var pdf = new Builder();

        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(3, Page);
        pdf.Object(4, RawPdf.Stream("", Content));
        pdf.Object(5, "<< /Kind /Placeholder >>");

        // Written plainly, but free in the table, so that only the stream can say where it is.
        pdf.Object(7, GraphicsState);

        // Two entries of type 1, a two-byte position and generation 0 each: object 7, then object 8
        // at the start of the catalog's dictionary, where there is no object number to read.
        var entries = Entry(pdf.PositionOf(7)) + Entry(DictionaryOf(pdf, 1));
        pdf.Object(6, "<< /Type /XRef /Size 9 /W [1 2 1] /Index [7 2] /Root 1 0 R /Length " + entries.Length +
                      " >>stream\n" + entries + "\nendstream");

        return pdf.Finish(inUse: 6, free: [7], trailerExtras: " /XRefStm " + pdf.PositionOf(6));

        static string Entry(int position) => "\u0001" + (char)(position >> 8) + (char)(position & 0xFF) + "\u0000";
    }

    /// <summary>
    ///   A hybrid file whose stream locates object 7 inside object stream 5 and then names, for
    ///   object 8, a position where no object starts.
    /// </summary>
    private static byte[] DamagedAfterACompressedEntry()
    {
        var pdf = new Builder();

        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(3, Page);
        pdf.Object(4, RawPdf.Stream("", Content));

        var packed = "7 0\n" + GraphicsState;
        pdf.Object(5, "<< /Type /ObjStm /N 1 /First 4 /Length " + packed.Length + " >>stream\n" +
                      packed + "\nendstream");

        // Type 2, in object stream 5, first in it; then type 1 at the catalog's dictionary.
        var position = DictionaryOf(pdf, 1);
        var entries = "\u0002\u0000\u0005\u0000" +
                      "\u0001" + (char)(position >> 8) + (char)(position & 0xFF) + "\u0000";
        pdf.Object(6, "<< /Type /XRef /Size 9 /W [1 2 1] /Index [7 2] /Root 1 0 R /Length " + entries.Length +
                      " >>stream\n" + entries + "\nendstream");

        return pdf.Finish(inUse: 6, free: [7], trailerExtras: " /XRefStm " + pdf.PositionOf(6));
    }

    /// <summary>
    ///   <see cref="Hybrid" /> with a classic incremental update after it that gives number 6 - the
    ///   cross-reference stream's - to a new object, and rewrites the catalog to name it.
    /// </summary>
    private static byte[] WithAnUpdateReusingTheStreamsNumber()
    {
        var original = Encoding.Latin1.GetString(Hybrid());
        var marker = original.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var previous = original.Substring(marker, original.IndexOf('\n', marker) - marker);

        var pdf = new StringBuilder(original);
        var catalog = pdf.Length;
        pdf.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R /Extra 6 0 R >>\nendobj\n");
        var newer = pdf.Length;
        pdf.Append("6 0 obj\n<< /Kind /Newer >>\nendobj\n");

        var startOfTable = pdf.Length;
        pdf.Append("xref\n1 1\n").Append(catalog.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("6 1\n").Append(newer.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 8 /Root 1 0 R /Prev ").Append(previous).Append(" >>\n");
        pdf.Append("startxref\n").Append(startOfTable).Append("\n%%EOF\n");

        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    /// <summary>Where an object's body starts, past its <c>n 0 obj</c> line.</summary>
    private static int DictionaryOf(Builder pdf, int id) => pdf.PositionOf(id) + (id + " 0 obj\n").Length;

    /// <summary>The same page with object 7 written the ordinary way, table entry and all.</summary>
    private static byte[] Plain()
    {
        var pdf = new Builder();

        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(3, Page);
        pdf.Object(4, RawPdf.Stream("", Content));
        pdf.Object(5, "<< /Kind /Placeholder >>");
        pdf.Object(6, "<< /Kind /Placeholder >>");
        pdf.Object(7, GraphicsState);

        return pdf.Finish(inUse: 7, free: [], trailerExtras: "");
    }

    /// <summary>The same file with the entries in its stream made unreadable, and nothing else.</summary>
    /// <remarks>
    ///   A replacement of the same length, so that every position written into the table still
    ///   holds and the only thing wrong with the file is the thing under test.
    /// </remarks>
    private static byte[] Damaged()
    {
        var pdf = Encoding.Latin1.GetString(Hybrid()).Replace("/W [1 2 1]", "/W [9 9 9]");
        return Encoding.Latin1.GetBytes(pdf);
    }

    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R " +
                        "/Resources << /ExtGState << /GS1 7 0 R >> >> >>";

    private const string Content = "0 0 1 rg 20 20 100 100 re f";

    private const string GraphicsState = "<< /Type /ExtGState /CA 0.5 >>";

    /// <summary>
    ///   Writes the objects and a classic cross-reference table over them.
    /// </summary>
    /// <remarks>
    ///   The one thing <see cref="RawPdf" /> cannot do here: it numbers the objects itself and
    ///   marks every one of them in use, and a hybrid file is a file with a free entry in the
    ///   middle of it.
    /// </remarks>
    private sealed class Builder
    {
        private readonly StringBuilder _pdf = new("%PDF-1.5\n");
        private readonly Dictionary<int, int> _positions = new();

        internal void Object(int id, string body)
        {
            _positions[id] = _pdf.Length;
            _pdf.Append(id).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        }

        internal int PositionOf(int id) => _positions[id];

        internal byte[] Finish(int inUse, IReadOnlyList<int> free, string trailerExtras)
        {
            var startOfTable = _pdf.Length;

            _pdf.Append("xref\n0 ").Append(inUse + 1).Append('\n');
            _pdf.Append("0000000000 65535 f \n");
            for (var id = 1; id <= inUse; id++)
                _pdf.Append(_positions[id].ToString("D10")).Append(" 00000 n \n");

            foreach (var id in free)
                _pdf.Append(id).Append(" 1\n0000000000 65535 f \n");

            _pdf.Append("trailer\n<< /Size 8 /Root 1 0 R").Append(trailerExtras).Append(" >>\n");
            _pdf.Append("startxref\n").Append(startOfTable).Append("\n%%EOF\n");

            // Plain ASCII but for the entries of the cross-reference stream, which are bytes. One
            // character is one byte either way, so the positions written above hold.
            return Encoding.Latin1.GetBytes(_pdf.ToString());
        }
    }
}
