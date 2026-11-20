using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfSplitter" /> takes pages out of a file into new files, reading the source
///   <c>Lazy</c>, so that what it costs grows with the pages taken and not with the file (#366).
/// </summary>
public class PdfSplitterTests
{
    private const long Megabyte = 1L << 20;
    private const long Gigabyte = 1L << 30;
    private const long FourGigabytes = 1L << 32;

    // ----- What is taken out ------------------------------------------------------------------------

    [Test]
    public void ARangeOfPagesIsWrittenAsTheSourceHasThem()
    {
        using var files = new Files(Pages(6));

        PdfSplitter.ExtractPages(files.Source, files.Output, firstPage: 2, pageCount: 3);

        ContentsOf(files.Output).Should().Equal(ContentsOfSourcePages(2, 3, 4));
    }

    [Test]
    public void PagesAreWrittenInTheOrderAskedForAsOftenAsTheyAreAskedFor()
    {
        using var files = new Files(Pages(6));

        PdfSplitter.ExtractPages(files.Source, files.Output, [5, 2, 5]);

        ContentsOf(files.Output).Should().Equal(ContentsOfSourcePages(5, 2, 5));
    }

    [Test]
    public void TheSourcesInformationIsCopied()
    {
        using var files = new Files(PagesWithInformation(3));

        PdfSplitter.ExtractPages(files.Source, files.Output, 1, 1);

        using var output = OpenForImport(files.Output);
        output.Info.Title.Should().Be("A title");
        output.Info.Author.Should().Be("An author");
        output.Info.Subject.Should().Be("A subject");
        output.Info.Keywords.Should().Be("some words");
        output.Info.Creator.Should().Be("A creator");
        output.Info.Producer.Should().Contain("(Original: Acme Writer)");
        output.Info.CreationDate.Should().Be(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToLocalTime());
        output.Info.ModificationDate.Should().BeAfter(output.Info.CreationDate);
    }

    [Test]
    public void AnExistingOutputFileIsOverwritten()
    {
        using var files = new Files(Pages(6));
        File.WriteAllBytes(files.Output, new byte[10 * Megabyte]);

        PdfSplitter.ExtractPages(files.Source, files.Output, 1, 2);

        new FileInfo(files.Output).Length.Should().BeLessThan(Megabyte);
        ContentsOf(files.Output).Should().Equal(ContentsOfSourcePages(1, 2));
    }

    [Test]
    public void TheSourceFileIsClosedAfterwards()
    {
        using var files = new Files(Pages(2));

        PdfSplitter.ExtractPages(files.Source, files.Output, 1, 1);

        using var exclusive = new FileStream(files.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        exclusive.Length.Should().BeGreaterThan(0);
    }

    // ----- Splitting ------------------------------------------------------------------------------

    [Test]
    public void SplittingWritesPartsOfTheSizeAskedForAndTheRestInALastPart()
    {
        using var files = new Files(Pages(7));
        var asked = new List<int>();

        var parts = PdfSplitter.Split(files.Source, 3, part =>
        {
            asked.Add(part);
            return files.Part(part);
        });

        parts.Should().Be(3);
        asked.Should().Equal(1, 2, 3);
        ContentsOf(files.Part(1)).Should().Equal(ContentsOfSourcePages(1, 2, 3));
        ContentsOf(files.Part(2)).Should().Equal(ContentsOfSourcePages(4, 5, 6));
        ContentsOf(files.Part(3)).Should().Equal(ContentsOfSourcePages(7));
    }

    [Test]
    public void EachPartCarriesTheSourcesInformation()
    {
        using var files = new Files(PagesWithInformation(4));

        PdfSplitter.Split(files.Source, 2, files.Part);

        using var second = OpenForImport(files.Part(2));
        second.Info.Title.Should().Be("A title");
    }

    // ----- Streams --------------------------------------------------------------------------------

    [Test]
    public void PagesAreTakenFromOneStreamIntoAnotherThatIsLeftOpen()
    {
        var source = new MemoryStream(Pages(6));
        var output = new MemoryStream();

        PdfSplitter.ExtractPages(source, output, firstPage: 4, pageCount: 2);

        output.CanWrite.Should().BeTrue();
        source.CanRead.Should().BeTrue();
        ContentsOf(output.ToArray()).Should().Equal(ContentsOfSourcePages(4, 5));
    }

    [Test]
    public void PagesListedAreTakenFromOneStreamIntoAnother()
    {
        var output = new MemoryStream();

        PdfSplitter.ExtractPages(new MemoryStream(Pages(6)), output, [6, 1]);

        output.CanWrite.Should().BeTrue();
        ContentsOf(output.ToArray()).Should().Equal(ContentsOfSourcePages(6, 1));
    }

    [Test]
    public void AStreamIsSplitIntoStreamsThatAreLeftOpen()
    {
        var source = new MemoryStream(Pages(5));
        var outputs = new List<MemoryStream>();

        var parts = PdfSplitter.Split(source, 2, part =>
        {
            outputs.Add(new MemoryStream());
            return outputs[part - 1];
        });

        parts.Should().Be(3);
        source.CanRead.Should().BeTrue();
        outputs.Should().AllSatisfy(output => output.CanWrite.Should().BeTrue());
        ContentsOf(outputs[0].ToArray()).Should().Equal(ContentsOfSourcePages(1, 2));
        ContentsOf(outputs[1].ToArray()).Should().Equal(ContentsOfSourcePages(3, 4));
        ContentsOf(outputs[2].ToArray()).Should().Equal(ContentsOfSourcePages(5));
    }

    // ----- What is refused ------------------------------------------------------------------------

    [Test]
    [Arguments(0, 1, 0)]
    [Arguments(7, 1, 7)]
    [Arguments(5, 3, 7)]
    public void ARangeOutsideTheDocumentIsRefusedByThePageItMisses(int firstPage, int pageCount, int missing)
    {
        using var files = new Files(Pages(6));

        var extract = () => PdfSplitter.ExtractPages(files.Source, files.Output, firstPage, pageCount);

        extract.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"*page {missing}*6 pages*");
        File.Exists(files.Output).Should().BeFalse();
    }

    [Test]
    public void AListNamingAPageOutsideTheDocumentIsRefusedByThatPage()
    {
        using var files = new Files(Pages(6));

        var extract = () => PdfSplitter.ExtractPages(files.Source, files.Output, [2, 9, 3]);

        extract.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*page 9*6 pages*");
    }

    [Test]
    public void ARefusalLeavesAnExistingOutputAsItWas()
    {
        using var files = new Files(Pages(6));
        File.WriteAllBytes(files.Output, [1, 2, 3]);

        var extract = () => PdfSplitter.ExtractPages(files.Source, files.Output, [8]);

        extract.Should().Throw<ArgumentOutOfRangeException>();
        File.ReadAllBytes(files.Output).Should().Equal(1, 2, 3);
    }

    [Test]
    public void NoPagesAtAllIsRefused()
    {
        using var files = new Files(Pages(6));

        var none = () => PdfSplitter.ExtractPages(files.Source, files.Output, []);
        var noneCounted = () => PdfSplitter.ExtractPages(files.Source, files.Output, 1, 0);

        none.Should().Throw<ArgumentException>().WithParameterName("pageNumbers");
        noneCounted.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageCount");
    }

    [Test]
    public void PartsOfNoPagesAreRefused()
    {
        using var files = new Files(Pages(6));

        var split = () => PdfSplitter.Split(files.Source, 0, files.Part);

        split.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pagesPerFile");
    }

    [Test]
    public void WritingOverTheSourceIsRefused()
    {
        using var files = new Files(Pages(3));
        var before = File.ReadAllBytes(files.Source);

        var extract = () => PdfSplitter.ExtractPages(files.Source, files.Source, 1, 1);
        var split = () => PdfSplitter.Split(files.Source, 1, _ => files.Source);

        extract.Should().Throw<ArgumentException>().WithParameterName("outputPath");
        split.Should().Throw<ArgumentException>().WithParameterName("outputPathForPart");
        File.ReadAllBytes(files.Source).Should().Equal(before);
    }

    // ----- What it costs --------------------------------------------------------------------------

    [Test]
    public void TakingPagesOutAllocatesTheSameHoweverLargeTheFileIs()
    {
        // The first run of anything allocates what later runs find already there.
        AllocatedTakingTheMiddleTwoPagesOutOf(LazyStreamTests.PagesOfImages(4, 4096, FourGigabytes));

        var fromFourGigabytes =
            AllocatedTakingTheMiddleTwoPagesOutOf(LazyStreamTests.PagesOfImages(4, 16 * Megabyte, FourGigabytes));
        var fromEightGigabytes =
            AllocatedTakingTheMiddleTwoPagesOutOf(LazyStreamTests.PagesOfImages(4, Gigabyte, FourGigabytes));

        fromEightGigabytes.Should().BeLessThan(Megabyte);
        ((double)fromEightGigabytes).Should().BeApproximately(fromFourGigabytes, fromFourGigabytes * 0.1);
    }

    [Test]
    public void SplittingAllocatesTheSameHoweverLargeTheFileIs()
    {
        AllocatedSplittingInTwo(LazyStreamTests.PagesOfImages(4, 4096, FourGigabytes));

        var fromFourGigabytes = AllocatedSplittingInTwo(LazyStreamTests.PagesOfImages(4, 16 * Megabyte, FourGigabytes));
        var fromEightGigabytes = AllocatedSplittingInTwo(LazyStreamTests.PagesOfImages(4, Gigabyte, FourGigabytes));

        fromEightGigabytes.Should().BeLessThan(2 * Megabyte);
        ((double)fromEightGigabytes).Should().BeApproximately(fromFourGigabytes, fromFourGigabytes * 0.1);
    }

    /// <summary>
    ///   The acceptance run of #366, on a file too large to commit: the first ten pages of it, taken
    ///   out within the managed memory the spec allows.
    /// </summary>
    [Test, LargePdf]
    public void TheFirstTenPagesOfALargeFileAreTakenOutWithinSixtyFourMegabytes()
    {
        var output = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        try
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            PdfSplitter.ExtractPages(LargePdfAttribute.Path, output, 1, 10);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.Should().BeLessThan(64 * Megabyte);
            using var taken = OpenForImport(output);
            taken.PageCount.Should().Be(10);
        }
        finally
        {
            File.Delete(output);
        }
    }

    // ----- The documents --------------------------------------------------------------------------

    /// <summary>A document of <paramref name="count" /> pages, each drawing a content of its own.</summary>
    private static byte[] Pages(int count)
    {
        var pdf = new SplicedPdf();
        pdf.Pages(count);
        var size = SplicedPdf.ContentId(count - 1) + 1;
        pdf.ClassicTable(firstId: 0, count: size, trailer: "/Size " + size + " /Root 1 0 R");
        return pdf.ToBytes();
    }

    /// <summary>As <see cref="Pages" />, with an information dictionary naming another producer.</summary>
    private static byte[] PagesWithInformation(int count)
    {
        var pdf = new SplicedPdf();
        pdf.Pages(count);
        var info = SplicedPdf.ContentId(count - 1) + 1;
        pdf.Object(info, "<< /Title (A title) /Author (An author) /Subject (A subject) /Keywords (some words) " +
                         "/Creator (A creator) /Producer (Acme Writer) /CreationDate (D:20200102030405Z) >>");
        pdf.ClassicTable(firstId: 0, count: info + 1,
            trailer: "/Size " + (info + 1) + " /Root 1 0 R /Info " + info + " 0 R");
        return pdf.ToBytes();
    }

    /// <summary>What <see cref="SplicedPdf.Pages" /> draws on each of the pages numbered, from one.</summary>
    private static string[] ContentsOfSourcePages(params int[] pageNumbers) =>
        [.. pageNumbers.Select(number => SplicedPdf.ContentOfPage(number - 1))];

    private static string[] ContentsOf(string path) => ContentsOf(File.ReadAllBytes(path));

    private static string[] ContentsOf(byte[] bytes)
    {
        using var document = Saved.Open(bytes, PdfDocumentOpenMode.Import);
        return [.. document.Pages.Cast<PdfPage>().Select(page => System.Text.Encoding.Latin1.GetString(PageContent.Of(page)))];
    }

    private static PdfDocument OpenForImport(string path) =>
        PdfPinata.Pdf.IO.PdfReader.Open(path, PdfDocumentOpenMode.Import);

    private static long AllocatedTakingTheMiddleTwoPagesOutOf(SplicedPdf pdf)
    {
        using var source = pdf.ToStream();
        var output = new RunCollapsingStream();
        var before = GC.GetAllocatedBytesForCurrentThread();

        PdfSplitter.ExtractPages(source, output, 2, 2);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var taken = PdfPinata.Pdf.IO.PdfReader.Open(output.Written(), PdfDocumentOpenMode.Lazy);
        taken.PageCount.Should().Be(2);
        return allocated;
    }

    private static long AllocatedSplittingInTwo(SplicedPdf pdf)
    {
        using var source = pdf.ToStream();
        var outputs = new[] { new RunCollapsingStream(), new RunCollapsingStream() };
        var before = GC.GetAllocatedBytesForCurrentThread();

        var parts = PdfSplitter.Split(source, 2, part => outputs[part - 1]);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        parts.Should().Be(2);
        foreach (var output in outputs)
        {
            using var part = PdfPinata.Pdf.IO.PdfReader.Open(output.Written(), PdfDocumentOpenMode.Lazy);
            part.PageCount.Should().Be(2);
        }

        return allocated;
    }

    /// <summary>A source file written for one test, and where its outputs go, removed afterwards.</summary>
    private sealed class Files : IDisposable
    {
        private readonly string _directory =
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;

        internal Files(byte[] source) => File.WriteAllBytes(Source, source);

        internal string Source => Path.Combine(_directory, "source.pdf");

        internal string Output => Path.Combine(_directory, "output.pdf");

        internal string Part(int part) => Path.Combine(_directory, "part-" + part + ".pdf");

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
