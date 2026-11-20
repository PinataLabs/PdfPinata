using System.Collections.Generic;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Taking pages out of a PDF into new ones with PdfSplitter, which reads the source lazily so
///   that a file of several gigabytes costs what the pages taken from it cost.
/// </summary>
internal sealed class SplitDemo : PdfDemo
{
    public SplitDemo() : base() { }

    public override string Name => "Split";

    public override string Summary => "PdfSplitter: a range of pages, a list of pages, or parts of N pages, read lazily.";

    public override IReadOnlyList<string> Shows => [
        "PdfSplitter.ExtractPages with a range: pages 3 to 6 of a ten-page source",
        "PdfSplitter.ExtractPages with a list: pages 10, 1 and 5, in that order",
        "PdfSplitter.Split into parts of four pages, the last holding the two left over",
        "The same thing by hand: PdfDocumentOpenMode.Lazy, AddPage, and Save before the source is disposed",
        "That the source's title and author travel with the pages"
    ];

    public override int PageCount => 8;

    #region example
    protected override PdfDocument Build(DemoContext context)
    {
        var heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        var label = new XFont(BundledFontResolver.SansFamily, 9, XFontStyle.Bold);
        var body = new XFont(BundledFontResolver.SansFamily, 9);
        var mono = new XFont(BundledFontResolver.MonoFamily, 8.5);

        // A ten-page source, built in memory and saved, as if it had arrived as a file.
        using var source = TenPageSource();

        // docs:begin extract-range
        // Pages 3 to 6, counted from 1 as a reader shows them. The source has to be able to seek;
        // neither stream is closed.
        using var range = new MemoryStream();
        PdfSplitter.ExtractPages(source, range, firstPage: 3, pageCount: 4);
        // docs:end extract-range

        // docs:begin extract-list
        // Any pages, in any order, as often as you like.
        using var picked = new MemoryStream();
        PdfSplitter.ExtractPages(source, picked, [10, 1, 5]);
        // docs:end extract-list

        // docs:begin split
        // Parts of four pages. The function is handed each part's number, from 1, and answers where
        // to write it; Split answers how many parts there were.
        var parts = new List<MemoryStream>();
        var partCount = PdfSplitter.Split(source, 4, part =>
        {
            var output = new MemoryStream();
            parts.Add(output);
            return output;
        });
        // docs:end split

        // docs:begin by-hand
        // What PdfSplitter does, written out. A Lazy document reads each object from its source when
        // it is first asked for, and a page imported from it keeps its images in the source until
        // the target is saved - so the target is saved first, and the source disposed after.
        using var byHand = new MemoryStream();
        using (var lazy = PdfReader.Open(source, PdfDocumentOpenMode.Lazy))
        {
            using var target = new PdfDocument();
            _ = target.AddPage(lazy.Pages[lazy.PageCount - 1]);
            target.Save(byHand);
        }
        // docs:end by-hand

        // ----- the document this demo writes: a report, then what came out ----------------------

        var document = new PdfDocument();
        document.Info.Title = "Split";
        var report = document.AddPage();

        // Read back for Import, which holds what it reads, because this document is saved after
        // the streams here are gone.
        using var fromRange = PdfReader.Open(range, PdfDocumentOpenMode.Import);
        using var fromList = PdfReader.Open(picked, PdfDocumentOpenMode.Import);
        foreach (var page in fromRange.Pages)
            _ = document.AddPage(page);
        foreach (var page in fromList.Pages)
            _ = document.AddPage(page);

        var partPages = new List<int>();
        foreach (var part in parts)
        {
            // Lazy is also the cheap way to ask a file how many pages it has.
            using var opened = PdfReader.Open(part, PdfDocumentOpenMode.Lazy);
            partPages.Add(opened.PageCount);
        }

        using (var gfx = XGraphics.FromPdfPage(report))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Taking pages out of a PDF", heading, XBrushes.Black, new XPoint(50, 60));

            prose.DrawString(
                "PdfSplitter opens its source with PdfDocumentOpenMode.Lazy, which reads the "
                + "cross-reference table and nothing else until a page is asked for, and copies each "
                + "image from the source to the output in chunks when the output is saved. Taking ten "
                + "pages out of a 6.7 GB file holds about 20 MB, where Import would read all 6.7 GB "
                + "first. The source here is ten pages built in memory.",
                body, XBrushes.Black, new XRect(50, 80, 495, 64));

            (string Call, string Result)[] results =
            [
                ("ExtractPages(source, range, 3, 4)", $"{fromRange.PageCount} pages, {range.Length:N0} bytes. They follow this page."),
                ("ExtractPages(source, picked, [10, 1, 5])", $"{fromList.PageCount} pages, {picked.Length:N0} bytes. They follow those."),
                ("Split(source, 4, ...)", $"{partCount} parts of {string.Join(", ", partPages)} pages."),
                ("By hand, Lazy", $"the last page alone, {byHand.Length:N0} bytes."),
                ("Info.Title, Info.Author", $"\"{fromRange.Info.Title}\", \"{fromRange.Info.Author}\": copied from the source.")
            ];

            double y = 165;
            foreach (var (call, result) in results)
            {
                gfx.DrawString(call, mono, XBrushes.Black, new XPoint(50, y));
                gfx.DrawString(result, body, XBrushes.DimGray, new XPoint(50, y + 12));
                y += 30;
            }

            gfx.DrawString("What is not copied", label, XBrushes.Black, new XPoint(50, y + 15));
            prose.DrawString(
                "Only pages travel: their content, what they draw with and their annotations. "
                + "Bookmarks, the interactive form, the structure tree and page labels belong to the "
                + "source document and stay there, and a link to a page that was not taken out goes "
                + "nowhere.",
                body, XBrushes.Black, new XRect(50, y + 27, 495, 50));
        }

        return document;
    }

    /// <summary>Ten numbered pages, each with the photograph on it, saved and ready to read.</summary>
    private static MemoryStream TenPageSource()
    {
        var huge = new XFont(BundledFontResolver.SansFamily, 96, XFontStyle.Bold);
        var body = new XFont(BundledFontResolver.SansFamily, 11);

        using var document = new PdfDocument();
        document.Info.Title = "Ten pages";
        document.Info.Author = "The Split demo";

        using var photograph = XImage.FromStream(() => Assets.Open(Assets.ImagePrefix + "pdf-pinata.jpg"));
        for (var number = 1; number <= 10; number++)
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            var colour = XColor.FromArgb(40 + number * 20, 90, 200 - number * 15);
            gfx.DrawRectangle(new XSolidBrush(colour), 0, 0, page.Width.Point, 200);
            gfx.DrawString(number.ToString(), huge, XBrushes.White,
                new XRect(0, 40, page.Width.Point, 120), XStringFormats.Center);
            gfx.DrawString($"Page {number} of the source", body, XBrushes.Black, new XPoint(50, 240));
            gfx.DrawImage(photograph, 50, 260, 300, 225);
        }

        var saved = new MemoryStream();
        document.Save(saved, false);
        return saved;
    }
    #endregion
}
