using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   A document opened <c>Lazy</c> reads an object when it is asked for and not before, so what it
///   costs to open, and to take a page out of, does not grow with the file (#366).
/// </summary>
/// <remarks>
///   Laziness is asserted by where the reads fell, through a <see cref="RecordingStream" /> over a
///   <see cref="SplicedPdf" /> that knows the byte range of every object it wrote. Each such
///   document keeps its cross-reference section more than one scan buffer from its objects: the
///   search for "startxref" reads the last 64 KiB of any file, and would otherwise be seen reading
///   them.
/// </remarks>
public class LazyOpenModeTests
{
    private const PdfDocumentOpenMode Lazy = PdfDocumentOpenMode.Lazy;

    private const long FourGigabytes = 1L << 32;

    /// <summary>Past the kilobyte every mode reads for the header.</summary>
    private const long BeyondTheHeader = 8192;

    /// <summary>More than the scan for "startxref" reads at a time.</summary>
    private const int BeyondTheScan = 70 * 1024;

    // ----- What opening reads --------------------------------------------------------------------

    [Test]
    public void OpeningReadsTheSameBytesHoweverMuchLiesBetweenTheHeaderAndTheObjects()
    {
        var smaller = new RecordingStream(ThreePages(FourGigabytes + 4096).ToStream());
        var larger = new RecordingStream(ThreePages(2 * FourGigabytes + 4096).ToStream());

        using var first = Open(smaller);
        using var second = Open(larger);

        // The header's kilobyte, one scan buffer for "startxref" and the section it names.
        smaller.BytesRead.Should().BeLessThan(96 * 1024);
        larger.BytesRead.Should().Be(smaller.BytesRead);
    }

    [Test]
    [Arguments("a classic table")]
    [Arguments("a cross-reference stream")]
    [Arguments("a hybrid file")]
    public void OpeningReadsNoObject(string indexedBy)
    {
        var pdf = ThreePagesIndexedBy(indexedBy, BeyondTheHeader);
        var stream = new RecordingStream(pdf.ToStream());

        using var document = Open(stream);

        var last = indexedBy == "a classic table" ? LastObjectOfThreePages : ObjectStreamId;
        for (var id = 1; id <= last; id++)
            stream.HasRead(pdf.BodyOf(id)).Should().BeFalse("object {0} was not asked for", id);
    }

    [Test]
    public void CountingThePagesReadsTheirDictionariesAndNoContent()
    {
        var pdf = ThreePages(FourGigabytes + 4096);
        var stream = new RecordingStream(pdf.ToStream());
        using var document = Open(stream);

        document.PageCount.Should().Be(3);

        for (var page = 0; page < 3; page++)
        {
            stream.HasRead(pdf.BodyOf(SplicedPdf.PageId(page))).Should().BeTrue();
            stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(page))).Should().BeFalse();
        }
    }

    [Test]
    public void ReadingAPageReadsThatPagesContentAndNoOthers()
    {
        var pdf = ThreePages(FourGigabytes + 4096);
        var stream = new RecordingStream(pdf.ToStream());
        using var document = Open(stream);
        document.PageCount.Should().Be(3);
        stream.Forget();

        var content = Encoding.Latin1.GetString(PageContent.Of(document.Pages[1]));

        content.Should().Be(SplicedPdf.ContentOfPage(1));
        stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(1))).Should().BeTrue();
        stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(0))).Should().BeFalse();
        stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(2))).Should().BeFalse();
        stream.BytesRead.Should().BeLessThan(1024);
    }

    [Test]
    public void MovingTheStreamBetweenTwoReadsDoesNoHarm()
    {
        var stream = ThreePages().ToStream();
        using var document = Open(stream);

        var first = Encoding.Latin1.GetString(PageContent.Of(document.Pages[0]));
        stream.Position = 5;
        var second = Encoding.Latin1.GetString(PageContent.Of(document.Pages[2]));

        first.Should().Be(SplicedPdf.ContentOfPage(0));
        second.Should().Be(SplicedPdf.ContentOfPage(2));
    }

    // ----- Lazy reads what the eager modes read --------------------------------------------------

    [Test]
    [Arguments("FamilyTree.pdf")]
    [Arguments("Pdf20.pdf")]
    [Arguments("test.pdf")]
    public void AFileReadsTheSameAsItDoesForImport(string asset) =>
        ShouldReadTheSameLazilyAsForImport(File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(asset)));

    [Test]
    [Arguments("a classic table")]
    [Arguments("a cross-reference stream")]
    [Arguments("a hybrid file")]
    public void AHandWrittenDocumentReadsTheSameAsItDoesForImport(string indexedBy) =>
        ShouldReadTheSameLazilyAsForImport(ThreePagesIndexedBy(indexedBy).ToBytes());

    [Test]
    public void ADocumentThisLibraryWroteWithACrossReferenceStreamReadsTheSameAsItDoesForImport() =>
        ShouldReadTheSameLazilyAsForImport(Saved.Bytes(DrawnDocument(PdfCrossReferenceFormat.Stream)));

    [Test]
    public void ACompressedObjectResolves()
    {
        using var document = Open(new MemoryStream(ThreePagesIndexedBy("a cross-reference stream").ToBytes()));

        GraphicsStateOf(document.Pages[0]).Elements.GetReal("/CA").Should().Be(0.5);
    }

    [Test]
    public void AnObjectANewerRevisionWroteOutInFullIsNotReplacedByItsCompressedCopy()
    {
        var pdf = ThreePagesWithACompressedGraphicsState();
        var first = pdf.CrossReferenceStream(XRefStreamId);
        pdf.Object(GraphicsStateId, "<< /Type /ExtGState /CA 0.75 >>");
        pdf.CrossReferenceStream(XRefStreamId + 1, "/Prev " + first, only: [GraphicsStateId]);

        using var document = Open(new MemoryStream(pdf.ToBytes()));

        GraphicsStateOf(document.Pages[0]).Elements.GetReal("/CA").Should().Be(0.75);
    }

    // ----- More object streams than are kept decoded ---------------------------------------------

    [Test]
    public void ObjectsInMoreObjectStreamsThanAreKeptDecodedAllResolve()
    {
        using var document = Open(new MemoryStream(SixPagesEachInAnObjectStreamOfItsOwn().ToBytes()));

        document.PageCount.Should().Be(6);
        for (var page = 0; page < 6; page++)
        {
            Encoding.Latin1.GetString(PageContent.Of(document.Pages[page])).Should().Be(SplicedPdf.ContentOfPage(page));
            GraphicsStateOf(document.Pages[page]).Elements.GetInteger("/Page").Should().Be(page);
        }
    }

    [Test]
    public void AnObjectStreamNoLongerKeptIsReadAgainAndOneStillKeptIsNot()
    {
        var pdf = SixPagesEachInAnObjectStreamOfItsOwn();
        var stream = new RecordingStream(pdf.ToStream());
        using var document = Open(stream);

        // The six page dictionaries, one from each object stream in turn.
        document.PageCount.Should().Be(6);
        stream.Forget();

        GraphicsStateOf(document.Pages[5]).Elements.GetInteger("/Page").Should().Be(5);
        stream.HasRead(pdf.BodyOf(ObjectStreamOfPage(5))).Should()
            .BeFalse("the stream used last is still decoded");

        GraphicsStateOf(document.Pages[0]).Elements.GetInteger("/Page").Should().Be(0);
        stream.HasRead(pdf.BodyOf(ObjectStreamOfPage(0))).Should()
            .BeTrue("five streams have been used since the first, and four are kept");
    }

    // ----- /Length ---------------------------------------------------------------------------------

    [Test]
    public void AnIndirectLengthWrittenAfterItsStreamIsResolved()
    {
        var pdf = new SplicedPdf();
        pdf.OnePage("unused");
        pdf.Object(4, "<< /Length 5 0 R >>stream\n" + SplicedPdf.ContentOfPage(0) + "\nendstream");
        pdf.Object(5, SplicedPdf.ContentOfPage(0).Length.ToString());
        pdf.ClassicTable(firstId: 0, count: 6, trailer: "/Size 6 /Root 1 0 R");

        using var document = Open(new MemoryStream(pdf.ToBytes()));

        Encoding.Latin1.GetString(PageContent.Of(document.Pages[0])).Should().Be(SplicedPdf.ContentOfPage(0));
    }

    [Test]
    public void AnIndirectLengthInsideAnObjectStreamIsResolved()
    {
        var pdf = new SplicedPdf();
        pdf.OnePage("unused");
        pdf.Object(4, "<< /Length 5 0 R >>stream\n" + SplicedPdf.ContentOfPage(0) + "\nendstream");
        pdf.ObjectStream(6, (5, SplicedPdf.ContentOfPage(0).Length.ToString()));
        pdf.CrossReferenceStream(7);

        using var document = Open(new MemoryStream(pdf.ToBytes()));

        Encoding.Latin1.GetString(PageContent.Of(document.Pages[0])).Should().Be(SplicedPdf.ContentOfPage(0));
    }

    [Test]
    public void AStreamWhoseLengthIsItselfIsReadUpToItsEnd()
    {
        var pdf = new SplicedPdf();
        pdf.OnePage("unused");
        pdf.Object(4, "<< /Length 4 0 R >>stream\n" + SplicedPdf.ContentOfPage(0) + "\nendstream");
        pdf.ClassicTable(firstId: 0, count: 5, trailer: "/Size 5 /Root 1 0 R");

        using var document = Open(new MemoryStream(pdf.ToBytes()));

        Encoding.Latin1.GetString(PageContent.Of(document.Pages[0])).Should().Be(SplicedPdf.ContentOfPage(0));
    }

    // ----- An object that is not where the table says ---------------------------------------------

    [Test]
    public void UnderStrictAnObjectWhoseHeaderNamesAnotherIsRefusedWhenItIsAskedFor()
    {
        var pdf = ThreePagesTheSecondContentUnderTheHeader(40);
        using var document = Open(new MemoryStream(pdf.ToBytes()), PdfReadAccuracy.Strict);
        document.PageCount.Should().Be(3);

        var read = () => PageContent.Of(document.Pages[1]);

        read.Should().Throw<PdfReaderException>()
            .WithMessage($"*{SplicedPdf.ContentId(1)} 0*{pdf.PositionOf(SplicedPdf.ContentId(1))}*40 0*");
    }

    [Test]
    public void UnderModerateSuchAnObjectIsReadUnderTheNumberTheTableGivesIt()
    {
        var pdf = ThreePagesTheSecondContentUnderTheHeader(40);
        using var document = Open(new MemoryStream(pdf.ToBytes()), PdfReadAccuracy.Moderate);

        Encoding.Latin1.GetString(PageContent.Of(document.Pages[1])).Should().Be(SplicedPdf.ContentOfPage(1));
    }

    [Test]
    [Arguments(PdfReadAccuracy.Strict)]
    [Arguments(PdfReadAccuracy.Moderate)]
    public void ATableWhoseNumbersAreOffByOneIsRefusedRatherThanRepaired(PdfReadAccuracy accuracy)
    {
        // The eager modes file such an entry under the number the object's header gives, which
        // moves an entry of the table; a table nothing has probed cannot do that after the fact.
        var pdf = ThreePagesTheSecondContentUnderTheHeader(SplicedPdf.ContentId(1) - 1);
        using var document = Open(new MemoryStream(pdf.ToBytes()), accuracy);

        var read = () => PageContent.Of(document.Pages[1]);

        read.Should().Throw<PdfReaderException>().WithMessage("*off by one*PdfDocumentOpenMode.Import*");
    }

    [Test]
    [Arguments(PdfReadAccuracy.Strict)]
    [Arguments(PdfReadAccuracy.Moderate)]
    public void AnEntryThatPointsAtNoObjectIsRefusedWhenItsObjectIsAskedFor(PdfReadAccuracy accuracy)
    {
        var pdf = new SplicedPdf();
        pdf.Pages(3);
        pdf.InPlaceOfObject(SplicedPdf.ContentId(1), "<< /Not /AnObject >>");
        pdf.ClassicTable(firstId: 0, count: LastObjectOfThreePages + 1,
            trailer: "/Size " + (LastObjectOfThreePages + 1) + " /Root 1 0 R");
        using var document = Open(new MemoryStream(pdf.ToBytes()), accuracy);
        document.PageCount.Should().Be(3);

        var read = () => PageContent.Of(document.Pages[1]);

        read.Should().Throw<PdfReaderException>()
            .WithMessage($"Object {SplicedPdf.ContentId(1)} 0 is not at position {pdf.PositionOf(SplicedPdf.ContentId(1))}*");
    }

    // ----- Importing -------------------------------------------------------------------------------

    [Test]
    public void PagesImportedFromALazySourceAreWrittenAsThoseFromAnImportSourceAre()
    {
        var bytes = Saved.Bytes(DrawnDocument(PdfCrossReferenceFormat.Classic));

        var fromLazy = Saved.Open(ImportedFrom(bytes, Lazy), PdfDocumentOpenMode.Import);
        var fromImport = Saved.Open(ImportedFrom(bytes, PdfDocumentOpenMode.Import), PdfDocumentOpenMode.Import);

        fromLazy.PageCount.Should().Be(fromImport.PageCount).And.Be(3);
        for (var page = 0; page < 3; page++)
            PageContent.Of(fromLazy.Pages[page]).Should().Equal(PageContent.Of(fromImport.Pages[page]));
    }

    [Test]
    public void ImportingAPageReadsNoOtherPagesContent()
    {
        var pdf = ThreePages(FourGigabytes + 4096);
        var stream = new RecordingStream(pdf.ToStream());
        using var source = Open(stream);
        var target = new PdfDocument();

        target.AddPage(source.Pages[1]);

        Encoding.Latin1.GetString(PageContent.Of(target.Pages[0])).Should().Be(SplicedPdf.ContentOfPage(1));
        stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(0))).Should().BeFalse();
        stream.HasRead(pdf.BodyOf(SplicedPdf.ContentId(2))).Should().BeFalse();
    }

    // ----- Who owns the source ---------------------------------------------------------------------

    [Test]
    public void ADocumentOpenedByPathKeepsItsFileUntilItIsDisposed()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        File.WriteAllBytes(path, ThreePages().ToBytes());
        try
        {
            var openExclusively = () => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose();
            var document = PdfPinata.Pdf.IO.PdfReader.Open(path, Lazy);

            // Nothing was read when Open returned, so this is read from the file now.
            Encoding.Latin1.GetString(PageContent.Of(document.Pages[2])).Should().Be(SplicedPdf.ContentOfPage(2));
            openExclusively.Should().Throw<IOException>("the document still reads from the file");

            document.Dispose();

            openExclusively.Should().NotThrow("disposing the document closes the file it opened");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AFileThatCannotBeOpenedLazilyIsNotLeftOpen()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes("%PDF-1.7\nnothing to see here\n%%EOF\n"));
        try
        {
            var open = () => PdfPinata.Pdf.IO.PdfReader.Open(path, Lazy);
            open.Should().Throw<Exception>();

            var openExclusively = () => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose();
            openExclusively.Should().NotThrow();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AfterDisposeAnUnreadObjectThrowsAndAReadOneStillAnswers()
    {
        var document = Open(new MemoryStream(ThreePages().ToBytes()));
        var read = document.Pages[0];
        var unread = document.Pages[1];
        PageContent.Of(read);

        document.Dispose();

        Encoding.Latin1.GetString(PageContent.Of(read)).Should().Be(SplicedPdf.ContentOfPage(0));
        var resolve = () => PageContent.Of(unread);
        resolve.Should().Throw<ObjectDisposedException>().WithMessage($"*{SplicedPdf.ContentId(1)} 0*");
    }

    [Test]
    public void TheCallersStreamIsLeftOpenWhenTheDocumentIsDisposed()
    {
        var stream = new MemoryStream(ThreePages().ToBytes());

        Open(stream).Dispose();

        stream.CanRead.Should().BeTrue();
    }

    [Test]
    public void AStreamThatCannotSeekIsRefusedBeforeAnythingIsRead()
    {
        var stream = new RecordingStream(new ForwardOnlyStream(ThreePages().ToBytes()));

        var open = () => Open(stream);

        open.Should().Throw<ArgumentException>().WithMessage("*PdfDocumentOpenMode.Lazy*seek*");
        stream.BytesRead.Should().Be(0);
    }

    // ----- Encryption ------------------------------------------------------------------------------

    [Test]
    public void AnEncryptedDocumentIsRefusedEvenWithItsPassword()
    {
        var encrypted = new PdfDocument();
        encrypted.AddPage();
        encrypted.SecuritySettings.UserPassword = "user";
        var bytes = Saved.Bytes(encrypted);

        var open = () => PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), "user", Lazy);

        open.Should().Throw<PdfReaderException>()
            .WithMessage("*encrypted*PdfDocumentOpenMode.Lazy*PdfDocumentOpenMode.Import*");
    }

    [Test]
    public void AnEncryptionDictionaryWrittenDirectlyInTheTrailerIsRefusedToo()
    {
        var bytes = RawPdf.Build(
        [
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Count 1/Kids[3 0 R]>>",
            "<</Type/Page/MediaBox[0 0 595 842]/Parent 2 0 R>>"
        ], "/Encrypt<</Filter/Standard/V 1/R 2/O(owner)/U(user)/P -4>>");

        var open = () => Open(new MemoryStream(bytes));

        open.Should().Throw<PdfReaderException>().WithMessage("*encrypted*PdfDocumentOpenMode.Lazy*");
    }

    // ----- The documents ---------------------------------------------------------------------------

    private const int LastObjectOfThreePages = 8;
    private const int ObjectStreamId = 9;
    private const int HybridStreamId = 10;
    private const int GraphicsStateId = 11;
    private const int XRefStreamId = 12;

    private static PdfDocument Open(Stream stream, PdfReadAccuracy accuracy = PdfReadAccuracy.Strict) =>
        PdfPinata.Pdf.IO.PdfReader.Open(stream, Lazy, accuracy);

    /// <summary>Three pages behind a classic table, their objects beginning at <paramref name="origin" />.</summary>
    private static SplicedPdf ThreePages(long origin = 0)
    {
        var pdf = new SplicedPdf(origin);
        pdf.Pages(3);
        pdf.Padding(BeyondTheScan);
        pdf.ClassicTable(firstId: 0, count: LastObjectOfThreePages + 1,
            trailer: "/Size " + (LastObjectOfThreePages + 1) + " /Root 1 0 R");
        return pdf;
    }

    /// <summary>
    ///   Three pages whose first names a graphics state that lives in an object stream, with no
    ///   cross-reference section written yet.
    /// </summary>
    private static SplicedPdf ThreePagesWithACompressedGraphicsState(long origin = 0)
    {
        var pdf = new SplicedPdf(origin);
        pdf.Pages(3);
        pdf.Object(SplicedPdf.PageId(0), SplicedPdf.PageDictionary(0,
            "/Resources << /ExtGState << /GS1 " + GraphicsStateId + " 0 R >> >>"));
        pdf.ObjectStream(ObjectStreamId, (GraphicsStateId, "<< /Type /ExtGState /CA 0.5 >>"));
        pdf.Padding(BeyondTheScan);
        return pdf;
    }

    private static SplicedPdf ThreePagesIndexedBy(string indexedBy, long origin = 0)
    {
        if (indexedBy == "a classic table")
            return ThreePages(origin);

        var pdf = ThreePagesWithACompressedGraphicsState(origin);
        if (indexedBy == "a cross-reference stream")
        {
            pdf.CrossReferenceStream(XRefStreamId);
            return pdf;
        }

        // ISO 32000-1 7.5.8.4: the graphics state is free in the classic table, and only the
        // stream /XRefStm names says which object stream it is in.
        pdf.Object(HybridStreamId, SplicedPdf.Stream(SplicedPdf.Row(2, ObjectStreamId, 0),
            "/Type /XRef /Size 12 /W [1 5 1] /Index [" + GraphicsStateId + " 1] /Root 1 0 R"));
        pdf.ClassicTable(firstId: 0, count: HybridStreamId + 1,
            trailer: "/Size 12 /Root 1 0 R /XRefStm " + pdf.PositionOf(HybridStreamId), free: [GraphicsStateId]);
        return pdf;
    }

    private static int ObjectStreamOfPage(int page) => 20 + page;

    /// <summary>
    ///   Six pages, the dictionary of each and a graphics state it names compressed together in an
    ///   object stream no other page shares.
    /// </summary>
    private static SplicedPdf SixPagesEachInAnObjectStreamOfItsOwn()
    {
        var pdf = new SplicedPdf();
        pdf.Pages(6);
        for (var page = 0; page < 6; page++)
        {
            var graphicsState = 30 + page;
            pdf.ObjectStream(ObjectStreamOfPage(page),
                (SplicedPdf.PageId(page), SplicedPdf.PageDictionary(page,
                    "/Resources << /ExtGState << /GS1 " + graphicsState + " 0 R >> >>")),
                (graphicsState, "<< /Type /ExtGState /Page " + page + " >>"));
        }

        pdf.Padding(BeyondTheScan);
        pdf.CrossReferenceStream(40);
        return pdf;
    }

    /// <summary>Three pages, the content of the second written under a header naming another object.</summary>
    private static SplicedPdf ThreePagesTheSecondContentUnderTheHeader(int headerId)
    {
        var pdf = new SplicedPdf();
        pdf.Pages(3);
        pdf.ObjectUnderTheHeader(SplicedPdf.ContentId(1), headerId, SplicedPdf.Stream(SplicedPdf.ContentOfPage(1)));
        pdf.ClassicTable(firstId: 0, count: LastObjectOfThreePages + 1,
            trailer: "/Size " + (LastObjectOfThreePages + 1) + " /Root 1 0 R");
        return pdf;
    }

    private static PdfDocument DrawnDocument(PdfCrossReferenceFormat format)
    {
        var document = new PdfDocument();
        document.Options.CrossReferenceFormat = format;
        for (var page = 0; page < 3; page++)
        {
            using var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawRectangle(XBrushes.Blue, 20, 20 + 30 * page, 100, 40);
        }

        return document;
    }

    /// <summary>A new document holding every page of the one in <paramref name="bytes" />, saved.</summary>
    private static byte[] ImportedFrom(byte[] bytes, PdfDocumentOpenMode mode)
    {
        using var source = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), mode);
        var target = new PdfDocument();
        for (var page = 0; page < source.PageCount; page++)
            target.AddPage(source.Pages[page]);
        return Saved.Bytes(target);
    }

    private static PdfDictionary GraphicsStateOf(PdfPage page) =>
        page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/ExtGState").Elements.GetDictionary("/GS1");

    /// <summary>
    ///   Every indirect object of the document in <paramref name="bytes" />, and the content of
    ///   every page, is what the eager reader makes of it.
    /// </summary>
    private static void ShouldReadTheSameLazilyAsForImport(byte[] bytes)
    {
        var eager = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        using var lazy = Open(new MemoryStream(bytes));

        lazy.PageCount.Should().Be(eager.PageCount);
        for (var page = 0; page < eager.PageCount; page++)
            PageContent.Of(lazy.Pages[page]).Should().Equal(PageContent.Of(eager.Pages[page]));

        foreach (var expected in eager.Internals.GetAllObjects())
        {
            if (expected == null)
                continue;

            var id = PdfInternals.GetObjectID(expected);
            var actual = lazy.Internals.GetObject(id);
            actual.Should().NotBeNull("object {0} is in the file", id);

            if (expected is not PdfDictionary dictionary)
            {
                actual.ToString().Should().Be(expected.ToString(), "object {0} is the same value", id);
                continue;
            }

            // The eager reader keeps an object stream decoded and a cross-reference stream as it
            // was read for its entries; neither is an object of the document.
            var type = dictionary.Elements.GetName("/Type");
            if (type is "/ObjStm" or "/XRef")
                continue;

            var read = actual.Should().BeAssignableTo<PdfDictionary>().Subject;
            read.Elements.Keys.Should().BeEquivalentTo(dictionary.Elements.Keys, "object {0} has the same entries", id);
            (read.Stream?.Value).Should().Equal(dictionary.Stream?.Value, "object {0} has the same data", id);
        }
    }

    /// <summary>A stream that reads forwards and cannot be asked where it is.</summary>
    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
