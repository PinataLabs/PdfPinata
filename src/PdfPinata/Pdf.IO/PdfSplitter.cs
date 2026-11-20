using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Takes pages out of a PDF file into new files: a range of pages, a list of pages, or every page
/// in parts of a given size. The source is opened with <see cref="PdfDocumentOpenMode.Lazy"/>, so
/// what this costs grows with the pages taken out and not with the size of the file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pages are numbered from 1</b>, as a reader shows them, and so are the parts of a split.
/// </para>
/// <para>
/// <b>Only the pages are copied</b>, with what they draw with and their annotations, as
/// <see cref="PdfPages.Add(PdfPage, AnnotationCopyingType)"/> copies them. Bookmarks, the
/// interactive form, the structure tree and page labels are not. A link to a page that is not
/// taken out goes nowhere.
/// </para>
/// <para>
/// <b>The document information is copied</b>: the title, author, subject, keywords, creator and
/// creation date. The output is dated when it is written, and its producer names this library and
/// the source's producer, as a document opened with <see cref="PdfDocumentOpenMode.Modify"/> and
/// saved again would be.
/// </para>
/// <para>
/// An encrypted source is refused with a <see cref="PdfReaderException"/>, because
/// <see cref="PdfDocumentOpenMode.Lazy"/> refuses it. Open it with
/// <see cref="PdfDocumentOpenMode.Import"/> and the password, and add its pages to a new document,
/// instead.
/// </para>
/// </remarks>
public static class PdfSplitter
{
    /// <summary>
    /// Writes <paramref name="pageCount"/> pages of the file at <paramref name="sourcePath"/>,
    /// starting at page <paramref name="firstPage"/>, to a new file at
    /// <paramref name="outputPath"/>. A file already there is overwritten.
    /// </summary>
    /// <param name="sourcePath">The PDF file to take pages from.</param>
    /// <param name="outputPath">The file to write. It may not be the source.</param>
    /// <param name="firstPage">The first page to take, counted from 1.</param>
    /// <param name="pageCount">How many pages to take; at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageCount"/> is less than 1, or a page of the range is not in the document.
    /// The message names the page and how many pages the document has. Nothing is written.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="outputPath"/> is the source.</exception>
    public static void ExtractPages(string sourcePath, string outputPath, int firstPage, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(outputPath);
        EnsureSomePages(pageCount);
        EnsureNotTheSource(sourcePath, outputPath, nameof(outputPath));

        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Lazy);
        using var target = Assemble(source, PagesInRange(firstPage, pageCount, source.PageCount));
        target.Save(outputPath);
    }

    /// <summary>
    /// Writes the pages <paramref name="pageNumbers"/> names, in that order, of the file at
    /// <paramref name="sourcePath"/> to a new file at <paramref name="outputPath"/>. A page named
    /// twice is written twice. A file already there is overwritten.
    /// </summary>
    /// <param name="sourcePath">The PDF file to take pages from.</param>
    /// <param name="outputPath">The file to write. It may not be the source.</param>
    /// <param name="pageNumbers">The pages to take, counted from 1; at least one.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="pageNumbers"/> names no page, or <paramref name="outputPath"/> is the source.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A page named is not in the document. The message names the page and how many pages the
    /// document has. Nothing is written.
    /// </exception>
    public static void ExtractPages(string sourcePath, string outputPath, IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(outputPath);
        var pages = Selection(pageNumbers);
        EnsureNotTheSource(sourcePath, outputPath, nameof(outputPath));

        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Lazy);
        using var target = Assemble(source, PagesListed(pages, source.PageCount));
        target.Save(outputPath);
    }

    /// <summary>
    /// Writes every page of the file at <paramref name="sourcePath"/> into files of
    /// <paramref name="pagesPerFile"/> pages each, the last holding what remains. Each is written
    /// and released before the next is begun, so a split holds one part at a time.
    /// </summary>
    /// <param name="sourcePath">The PDF file to split.</param>
    /// <param name="pagesPerFile">How many pages each part holds; at least one.</param>
    /// <param name="outputPathForPart">
    /// Answers the file to write a part to, given the part's number counted from 1: part 1 holds
    /// pages 1 to <paramref name="pagesPerFile"/>. A file already there is overwritten. It may not
    /// be the source.
    /// </param>
    /// <returns>How many parts were written; 0 for a document with no pages.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerFile"/> is less than 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputPathForPart"/> answered the source.</exception>
    public static int Split(string sourcePath, int pagesPerFile, Func<int, string> outputPathForPart)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(outputPathForPart);
        EnsurePagesPerFile(pagesPerFile);

        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Lazy);
        return SplitInto(source, pagesPerFile, (part, target) =>
        {
            var path = outputPathForPart(part)
                       ?? throw new ArgumentException($"No file was given for part {part}.", nameof(outputPathForPart));
            EnsureNotTheSource(sourcePath, path, nameof(outputPathForPart));
            target.Save(path);
        });
    }

    /// <summary>
    /// Writes <paramref name="pageCount"/> pages of the PDF in <paramref name="source"/>, starting
    /// at page <paramref name="firstPage"/>, to <paramref name="output"/>.
    /// </summary>
    /// <param name="source">
    /// The PDF to take pages from. It must be able to seek, and is read from but not closed.
    /// </param>
    /// <param name="output">
    /// Where to write. It is written from where it stands and is not truncated, as
    /// <see cref="PdfDocument.Save(Stream)"/> writes, so pass an empty stream: bytes already beyond
    /// the end of what is written stay there. It is not closed, and is rewound to its start once
    /// written.
    /// </param>
    /// <param name="firstPage">The first page to take, counted from 1.</param>
    /// <param name="pageCount">How many pages to take; at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageCount"/> is less than 1, or a page of the range is not in the document.
    /// The message names the page and how many pages the document has. Nothing is written.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> cannot seek.</exception>
    public static void ExtractPages(Stream source, Stream output, int firstPage, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        EnsureSomePages(pageCount);

        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Lazy);
        using var target = Assemble(document, PagesInRange(firstPage, pageCount, document.PageCount));
        target.Save(output);
    }

    /// <summary>
    /// Writes the pages <paramref name="pageNumbers"/> names, in that order, of the PDF in
    /// <paramref name="source"/> to <paramref name="output"/>. A page named twice is written twice.
    /// </summary>
    /// <param name="source">
    /// The PDF to take pages from. It must be able to seek, and is read from but not closed.
    /// </param>
    /// <param name="output">
    /// Where to write. It is written from where it stands and is not truncated, as
    /// <see cref="PdfDocument.Save(Stream)"/> writes, so pass an empty stream: bytes already beyond
    /// the end of what is written stay there. It is not closed, and is rewound to its start once
    /// written.
    /// </param>
    /// <param name="pageNumbers">The pages to take, counted from 1; at least one.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="pageNumbers"/> names no page, or <paramref name="source"/> cannot seek.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A page named is not in the document. The message names the page and how many pages the
    /// document has. Nothing is written.
    /// </exception>
    public static void ExtractPages(Stream source, Stream output, IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        var pages = Selection(pageNumbers);

        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Lazy);
        using var target = Assemble(document, PagesListed(pages, document.PageCount));
        target.Save(output);
    }

    /// <summary>
    /// Writes every page of the PDF in <paramref name="source"/> into streams of
    /// <paramref name="pagesPerFile"/> pages each, the last holding what remains. Each is written
    /// and released before the next is begun, so a split holds one part at a time.
    /// </summary>
    /// <param name="source">
    /// The PDF to split. It must be able to seek, and is read from but not closed.
    /// </param>
    /// <param name="pagesPerFile">How many pages each part holds; at least one.</param>
    /// <param name="outputForPart">
    /// Answers the stream to write a part to, given the part's number counted from 1: part 1 holds
    /// pages 1 to <paramref name="pagesPerFile"/>. The stream stays the caller's. It is written from
    /// where it stands and is not truncated, as <see cref="PdfDocument.Save(Stream)"/> writes, so
    /// answer an empty one. It is not closed, and is rewound to its start once written.
    /// </param>
    /// <returns>How many parts were written; 0 for a document with no pages.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerFile"/> is less than 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> cannot seek.</exception>
    public static int Split(Stream source, int pagesPerFile, Func<int, Stream> outputForPart)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(outputForPart);
        EnsurePagesPerFile(pagesPerFile);

        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Lazy);
        return SplitInto(document, pagesPerFile, (part, target) =>
            target.Save(outputForPart(part)
                        ?? throw new ArgumentException($"No stream was given for part {part}.", nameof(outputForPart))));
    }

    /// <summary>
    /// Builds each part of <paramref name="source"/> and hands it to <paramref name="save"/>, which
    /// has to write it before <paramref name="source"/> is disposed: its streams are still in the
    /// source.
    /// </summary>
    private static int SplitInto(PdfDocument source, int pagesPerFile, Action<int, PdfDocument> save)
    {
        var pageCount = source.PageCount;
        var parts = pageCount / pagesPerFile + (pageCount % pagesPerFile == 0 ? 0 : 1);
        for (var part = 1; part <= parts; part++)
        {
            var firstPage = (part - 1) * pagesPerFile + 1;
            using var target = Assemble(source, Enumerable.Range(firstPage, Math.Min(pagesPerFile, pageCount - firstPage + 1)));
            save(part, target);
        }

        return parts;
    }

    /// <summary>A new document of the pages of <paramref name="source"/> numbered, from 1.</summary>
    private static PdfDocument Assemble(PdfDocument source, IEnumerable<int> pageNumbers)
    {
        var target = new PdfDocument();
        CopyInformation(source.Info, target.Info);
        foreach (var number in pageNumbers)
            _ = target.Pages.Add(source.Pages[number - 1]);
        return target;
    }

    /// <summary>
    /// The entries of the source's information dictionary that describe its content rather than
    /// the file. The producer is copied so that saving says "(Original: …)" after it.
    /// </summary>
    private static readonly string[] CopiedInformation =
    [
        PdfDocumentInformation.Keys.Title,
        PdfDocumentInformation.Keys.Author,
        PdfDocumentInformation.Keys.Subject,
        PdfDocumentInformation.Keys.Keywords,
        PdfDocumentInformation.Keys.Creator,
        PdfDocumentInformation.Keys.Producer,
        PdfDocumentInformation.Keys.CreationDate
    ];

    private static void CopyInformation(PdfDocumentInformation from, PdfDocumentInformation to)
    {
        foreach (var key in CopiedInformation)
        {
            // A direct string is a simple value, which two documents can share. One written as an
            // object of its own is left behind rather than imported.
            if (from.Elements.GetValue(key) is PdfString value)
                to.Elements[key] = value;
        }

        to.Elements.SetDateTime(PdfDocumentInformation.Keys.ModDate, GlobalTimeSettings.Now);
    }

    private static int[] Selection(IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(pageNumbers);
        var pages = pageNumbers.ToArray();
        if (pages.Length == 0)
            throw new ArgumentException("At least one page has to be named.", nameof(pageNumbers));
        return pages;
    }

    private static int[] PagesListed(int[] pages, int pageCount)
    {
        foreach (var page in pages)
        {
            if (page < 1 || page > pageCount)
                throw NoSuchPage("pageNumbers", page, pageCount);
        }

        return pages;
    }

    private static IEnumerable<int> PagesInRange(int firstPage, int pageCount, int documentPageCount)
    {
        if (firstPage < 1 || firstPage > documentPageCount)
            throw NoSuchPage(nameof(firstPage), firstPage, documentPageCount);
        if (pageCount > documentPageCount - firstPage + 1)
            throw NoSuchPage(nameof(pageCount), documentPageCount + 1, documentPageCount);
        return Enumerable.Range(firstPage, pageCount);
    }

    private static ArgumentOutOfRangeException NoSuchPage(string parameter, int page, int pageCount) =>
        new(parameter, page,
            $"There is no page {page} in a document of {pageCount} {(pageCount == 1 ? "page" : "pages")}; pages are numbered from 1.");

    private static void EnsureSomePages(int pageCount)
    {
        if (pageCount < 1)
            throw new ArgumentOutOfRangeException(nameof(pageCount), pageCount, "At least one page has to be taken.");
    }

    private static void EnsurePagesPerFile(int pagesPerFile)
    {
        if (pagesPerFile < 1)
            throw new ArgumentOutOfRangeException(nameof(pagesPerFile), pagesPerFile, "Each part has to hold at least one page.");
    }

    /// <summary>
    /// Refuses to write over the source, which is still being read from while the output is
    /// written. Opening the output empties it, so the pages would be read out of an empty file.
    /// </summary>
    private static void EnsureNotTheSource(string sourcePath, string outputPath, string parameter)
    {
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), comparison))
            throw new ArgumentException($"Pages cannot be written over the file they are taken from, '{sourcePath}'.", parameter);
    }
}
