using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
///   A document opened <c>Lazy</c> leaves a stream's data in the file until something asks for it,
///   and a page taken out of one is saved by copying that data from the file to the output a
///   chunk at a time, so that taking pages out costs the same however large their images are
///   (#366).
/// </summary>
/// <remarks>
///   The large documents are <see cref="SplicedPdf" />s whose images are runs of one byte that
///   nothing holds, saved to a <see cref="RunCollapsingStream" /> that holds none of it either.
/// </remarks>
public class LazyStreamTests
{
    private const PdfDocumentOpenMode Lazy = PdfDocumentOpenMode.Lazy;

    private const long Megabyte = 1L << 20;
    private const long Gigabyte = 1L << 30;
    private const long FourGigabytes = 1L << 32;

    /// <summary>More than the scan for "startxref" reads at a time.</summary>
    private const int BeyondTheScan = 70 * 1024;

    /// <summary>More than three of the chunks a stream is copied in, and not a whole number of them.</summary>
    private const int SeveralChunks = 300_007;

    // ----- What it costs ---------------------------------------------------------------------------

    [Test]
    public void TakingPagesOutAllocatesTheSameHoweverLargeTheirImagesAre()
    {
        // The first run of anything allocates what later runs find already there.
        TakeTheLastThreePagesOutOf(PagesOfImages(4, 4096, FourGigabytes));

        var fromFourGigabytes = TakeTheLastThreePagesOutOf(PagesOfImages(4, 16 * Megabyte, FourGigabytes));
        var fromEightGigabytes = TakeTheLastThreePagesOutOf(PagesOfImages(4, Gigabyte, FourGigabytes));

        fromEightGigabytes.Saved.Should().BeGreaterThan(3 * Gigabyte);
        fromEightGigabytes.Allocated.Should().BeLessThan(Megabyte);
        ((double)fromEightGigabytes.Allocated).Should().BeApproximately(fromFourGigabytes.Allocated,
            fromFourGigabytes.Allocated * 0.1);

        // Each image once, and not once to hold it and again to write it.
        fromEightGigabytes.Read.Should().BeInRange(3 * Gigabyte, 3 * Gigabyte + Megabyte);
    }

    [Test]
    public void AStreamWithNoLengthIsMeasuredWithoutBeingHeld()
    {
        TakeTheLastThreePagesOutOf(PagesOfImages(4, 4096, lengthEntry: ""));

        var taken = TakeTheLastThreePagesOutOf(PagesOfImages(4, 256 * Megabyte, lengthEntry: ""));

        taken.Saved.Should().BeGreaterThan(3 * 256 * Megabyte);
        taken.Allocated.Should().BeLessThan(Megabyte);
    }

    [Test]
    public void ReadingAStreamsDictionaryReadsNoneOfItsData()
    {
        var pdf = PagesOfImages(1, Megabyte);
        var stream = new RecordingStream(pdf.ToStream());
        using var document = Open(stream);

        var image = ImageOf(document.Pages[0]);

        image.Elements.GetInteger("/Width").Should().Be(1);
        image.Stream.Length.Should().Be((int)Megabyte);
        stream.HasRead(pdf.DataOf(ImageId(0))).Should().BeFalse();
    }

    [Test]
    public void AStreamAskedForItsDataReadsItOnce()
    {
        var pdf = PagesOfImages(1, SeveralChunks);
        var stream = new RecordingStream(pdf.ToStream());
        using var document = Open(stream);
        var image = ImageOf(document.Pages[0]);

        var first = image.Stream.Value;
        stream.Forget();
        var second = image.Stream.Value;

        first.Should().HaveCount(SeveralChunks).And.OnlyContain(data => data == SplicedPdf.Filler);
        second.Should().BeSameAs(first);
        stream.BytesRead.Should().Be(0);
    }

    [Test]
    public void ImportingAPageReadsNoneOfItsImage()
    {
        var pdf = PagesOfImages(2, Megabyte);
        var stream = new RecordingStream(pdf.ToStream());
        using var source = Open(stream);
        var target = new PdfDocument();

        target.AddPage(source.Pages[1]);

        stream.HasRead(pdf.DataOf(ImageId(1))).Should().BeFalse();
        ImageOf(target.Pages[0]).Stream.Value.Should().HaveCount((int)Megabyte);
    }

    // ----- What is written -------------------------------------------------------------------------

    [Test]
    public void AStreamIsCopiedAsTheBytesItsValueAnswers()
    {
        var data = Scrambled(SeveralChunks);
        var bytes = OnePageWithAnImageOf(data).ToBytes();
        using var source = Open(new MemoryStream(bytes));
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);

        var saved = Saved.Open(Saved.Bytes(target), PdfDocumentOpenMode.Import);

        var image = ImageOf(saved.Pages[0]);
        image.Stream.Value.Should().Equal(Encoding.Latin1.GetBytes(data));
        image.Stream.Value.Should().Equal(ImageOf(source.Pages[0]).Stream.Value);
        image.Elements.GetName("/Filter").Should().Be("/DCTDecode");
    }

    [Test]
    public void AStreamCopiedIntoAnEncryptedDocumentIsReadBackAsItWas()
    {
        var data = Scrambled(SeveralChunks);
        using var source = Open(new MemoryStream(OnePageWithAnImageOf(data).ToBytes()));
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);
        target.SecuritySettings.UserPassword = "user";

        var saved = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(Saved.Bytes(target)), "user",
            PdfDocumentOpenMode.Import);

        ImageOf(saved.Pages[0]).Stream.Value.Should().Equal(Encoding.Latin1.GetBytes(data));
    }

    [Test]
    [Arguments("FamilyTree.pdf")]
    [Arguments("Pdf20.pdf")]
    [Arguments("test.pdf")]
    public void PagesTakenOutOfAFileLazilyAreWrittenAsThoseTakenForImportAre(string asset) =>
        ShouldBeTakenOutTheSameLazilyAsForImport(File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(asset)));

    [Test]
    public void FilteredStreamsAreWrittenAsTheyWereRead()
    {
        var bytes = Saved.Bytes(DocumentWithAJpegAndAPng());
        var source = Saved.Open(bytes, PdfDocumentOpenMode.Import);

        ShouldBeTakenOutTheSameLazilyAsForImport(bytes);

        var taken = Saved.Open(ImportedFrom(bytes, Lazy), PdfDocumentOpenMode.Import);
        ImagesOf(taken).Should().BeEquivalentTo(ImagesOf(source));
        ImagesOf(taken).Select(image => image.Filter).Should().Contain(["/DCTDecode", "/FlateDecode"]);
    }

    [Test]
    public void AStreamLongerThanTwoGigabytesIsCopiedAndItsLengthWritten()
    {
        var pdf = PagesOfImages(1, 3 * Gigabyte);
        using var source = Open(pdf.ToStream());
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);
        var output = new RunCollapsingStream();

        target.Save(output);

        using var saved = Open(output.Written());
        var image = ImageOf(saved.Pages[0]);
        image.Elements["/Length"].Should().BeOfType<PdfLong>().Which.Value.Should().Be(3 * Gigabyte);
        var value = () => image.Stream.Value;
        value.Should().Throw<InvalidOperationException>().WithMessage("*3221225472 bytes*");
    }

    // ----- /Length ---------------------------------------------------------------------------------

    [Test]
    [Arguments("/Length 6 0 R")]
    [Arguments("")]
    [Arguments("/Length 12")]
    [Arguments("/Length 99999999")]
    [Arguments("/Length 5 0 R")]
    public void AStreamIsCopiedWholeWhateverItSaysOfItsLength(string lengthEntry)
    {
        var pdf = PagesOfImages(1, SeveralChunks, lengthEntry: lengthEntry);
        using var source = Open(new MemoryStream(pdf.ToBytes()));
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);

        var saved = Saved.Open(Saved.Bytes(target), PdfDocumentOpenMode.Import);

        var image = ImageOf(saved.Pages[0]);
        image.Stream.Value.Should().HaveCount(SeveralChunks).And.OnlyContain(data => data == SplicedPdf.Filler);
        image.Elements.GetInteger("/Length").Should().Be(SeveralChunks);
    }

    // ----- The order of things ---------------------------------------------------------------------

    [Test]
    public void SavingAfterTheSourceWasDisposedSaysWhichFileAndWhichObject()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        File.WriteAllBytes(path, PagesOfImages(1, SeveralChunks).ToBytes());
        try
        {
            var source = PdfPinata.Pdf.IO.PdfReader.Open(path, Lazy);
            var target = new PdfDocument();
            target.AddPage(source.Pages[0]);
            source.Dispose();

            var save = () => target.Save(new MemoryStream());

            save.Should().Throw<ObjectDisposedException>()
                .Which.Message.Should().Contain(path).And.MatchRegex(@"object [45] 0\b");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void SavingAfterASourceReadFromAStreamWasDisposedSaysWhichObject()
    {
        var source = Open(new MemoryStream(PagesOfImages(1, SeveralChunks).ToBytes()));
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);
        source.Dispose();

        var save = () => target.Save(new MemoryStream());

        save.Should().Throw<ObjectDisposedException>()
            .Which.Message.Should().Contain("a stream").And.MatchRegex(@"object [45] 0\b");
    }

    [Test]
    public void SavingAfterTheCallerClosedTheSourcesStreamSaysWhichObject()
    {
        var stream = new MemoryStream(PagesOfImages(1, SeveralChunks).ToBytes());
        using var source = Open(stream);
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);
        stream.Dispose();

        var save = () => target.Save(new MemoryStream());

        save.Should().Throw<ObjectDisposedException>().Which.Message.Should().MatchRegex(@"object [45] 0\b");
    }

    [Test]
    public void AStreamAlreadyReadIsStillSavedAfterTheSourceIsDisposed()
    {
        var source = Open(new MemoryStream(PagesOfImages(1, SeveralChunks).ToBytes()));
        var target = new PdfDocument();
        target.AddPage(source.Pages[0]);
        _ = PageContent.Of(target.Pages[0]);
        _ = ImageOf(target.Pages[0]).Stream.Value;
        source.Dispose();

        var saved = Saved.Open(Saved.Bytes(target), PdfDocumentOpenMode.Import);

        ImageOf(saved.Pages[0]).Stream.Value.Should().HaveCount(SeveralChunks);
    }

    [Test]
    public void StreamsReadFromSeveralThreadsAtOnceAreEachTheirOwn()
    {
        const int pages = 16;
        var pdf = new SplicedPdf();
        pdf.Pages(pages);
        pdf.ClassicTable(firstId: 0, count: 3 + 2 * pages, trailer: "/Size " + (3 + 2 * pages) + " /Root 1 0 R");
        using var document = Open(new MemoryStream(pdf.ToBytes()));
        var contents = Enumerable.Range(0, pages)
            .Select(page => document.Pages[page].Elements.GetDictionary("/Contents")).ToArray();
        var read = new string[pages];

        Parallel.For(0, pages, page => read[page] = Encoding.Latin1.GetString(contents[page].Stream.Value));

        read.Should().Equal(Enumerable.Range(0, pages).Select(SplicedPdf.ContentOfPage));
    }

    // ----- The documents ---------------------------------------------------------------------------

    private const string ImageEntries =
        "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /DCTDecode";

    private static int PageId(int index) => 3 + 3 * index;
    private static int ContentId(int index) => 4 + 3 * index;
    private static int ImageId(int index) => 5 + 3 * index;

    private static PdfDocument Open(Stream stream) => PdfPinata.Pdf.IO.PdfReader.Open(stream, Lazy);

    /// <summary>
    ///   Pages that each draw an image of their own, <paramref name="imageLength" /> bytes of
    ///   filler, behind a classic table. The object after the last image is that length as a
    ///   number, for a <paramref name="lengthEntry" /> that names it.
    /// </summary>
    private static SplicedPdf PagesOfImages(int count, long imageLength, long origin = 0, string lengthEntry = null)
    {
        var pdf = new SplicedPdf(origin);
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(index => PageId(index) + " 0 R"));
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [" + kids + "] /Count " + count + " >>");
        for (var index = 0; index < count; index++)
        {
            pdf.Object(PageId(index), PageDrawingImage(index));
            pdf.Object(ContentId(index), SplicedPdf.Stream("q 200 0 0 200 0 0 cm /Im0 Do Q"));
            pdf.StreamOfFiller(ImageId(index), imageLength, ImageEntries, lengthEntry);
        }

        var lengthId = ImageId(count - 1) + 1;
        pdf.Object(lengthId, imageLength.ToString());
        pdf.Padding(BeyondTheScan);
        pdf.ClassicTable(firstId: 0, count: lengthId + 1, trailer: "/Size " + (lengthId + 1) + " /Root 1 0 R");
        return pdf;
    }

    /// <summary>One page drawing an image whose data is <paramref name="data" />, a character to a byte.</summary>
    private static SplicedPdf OnePageWithAnImageOf(string data)
    {
        var pdf = new SplicedPdf();
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(PageId(0), PageDrawingImage(0));
        pdf.Object(ContentId(0), SplicedPdf.Stream("q 200 0 0 200 0 0 cm /Im0 Do Q"));
        pdf.Object(ImageId(0), SplicedPdf.Stream(data, ImageEntries));
        pdf.ClassicTable(firstId: 0, count: 6, trailer: "/Size 6 /Root 1 0 R");
        return pdf;
    }

    private static string PageDrawingImage(int index) =>
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents " + ContentId(index) + " 0 R " +
        "/Resources << /XObject << /Im0 " + ImageId(index) + " 0 R >> >> >>";

    /// <summary>Bytes in no order, so that a chunk copied out of place shows.</summary>
    private static string Scrambled(int length)
    {
        var random = new Random(366);
        var data = new StringBuilder(length);
        for (var index = 0; index < length; index++)
            data.Append((char)random.Next(256));
        return data.ToString();
    }

    private static PdfDocument DocumentWithAJpegAndAPng()
    {
        var document = new PdfDocument();
        foreach (var asset in new[] { "frog-and-toad.jpg", "lenna.png" })
        {
            using var gfx = XGraphics.FromPdfPage(document.AddPage());
            using var image = XImage.FromFile(PathHelper.GetInstance().GetAssetPath(asset));
            gfx.DrawImage(image, 20, 20, 200, 200);
            gfx.DrawRectangle(XBrushes.Blue, 20, 240, 100, 40);
        }

        return document;
    }

    private static PdfDictionary ImageOf(PdfPage page) =>
        page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/XObject").Elements.GetDictionary("/Im0");

    /// <summary>The filter and the data of every image in <paramref name="document" />, in no order.</summary>
    private static (string Filter, string Data)[] ImagesOf(PdfDocument document) =>
    [
        .. document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Where(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image")
            .Select(image => (image.Elements.GetName("/Filter"), Convert.ToBase64String(image.Stream.Value)))
    ];

    /// <summary>
    ///   Opens <paramref name="pdf" /> lazily, takes every page but the first into a new document
    ///   and saves it, and answers what that allocated, read from the source and wrote.
    /// </summary>
    private static (long Allocated, long Read, long Saved) TakeTheLastThreePagesOutOf(SplicedPdf pdf)
    {
        var stream = new RecordingStream(pdf.ToStream());
        var output = new RunCollapsingStream();
        var before = GC.GetAllocatedBytesForCurrentThread();

        using (var source = Open(stream))
        {
            var target = new PdfDocument();
            for (var page = 1; page < source.PageCount; page++)
                target.AddPage(source.Pages[page]);
            target.Save(output);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        using var saved = Open(output.Written());
        saved.PageCount.Should().Be(3);
        return (allocated, stream.BytesRead, output.Length);
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

    /// <summary>
    ///   Every page of the document in <paramref name="bytes" /> taken into a new one is saved as
    ///   the same pages, the same objects and the same stream data whichever way it was opened.
    /// </summary>
    private static void ShouldBeTakenOutTheSameLazilyAsForImport(byte[] bytes)
    {
        var fromLazy = Saved.Open(ImportedFrom(bytes, Lazy), PdfDocumentOpenMode.Import);
        var fromImport = Saved.Open(ImportedFrom(bytes, PdfDocumentOpenMode.Import), PdfDocumentOpenMode.Import);

        fromLazy.PageCount.Should().Be(fromImport.PageCount);
        for (var page = 0; page < fromImport.PageCount; page++)
            PageContent.Of(fromLazy.Pages[page]).Should().Equal(PageContent.Of(fromImport.Pages[page]));

        var expected = fromImport.Internals.GetAllObjects();
        var actual = fromLazy.Internals.GetAllObjects();
        actual.Should().HaveSameCount(expected);
        for (var index = 0; index < expected.Length; index++)
        {
            var id = PdfInternals.GetObjectID(expected[index]);
            if (expected[index] is not PdfDictionary dictionary)
            {
                actual[index].ToString().Should().Be(expected[index].ToString(), "object {0} is the same value", id);
                continue;
            }

            var taken = actual[index].Should().BeAssignableTo<PdfDictionary>().Subject;
            taken.Elements.Keys.Should().BeEquivalentTo(dictionary.Elements.Keys, "object {0} has the same entries", id);
            taken.Elements.GetName("/Filter").Should().Be(dictionary.Elements.GetName("/Filter"));
            (taken.Stream?.Value).Should().Equal(dictionary.Stream?.Value, "object {0} has the same data", id);
        }
    }
}
