using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Merging, importing, reordering and slimming documents, measured in pages and in bytes.
/// </summary>
internal sealed class AssembleDemo : PdfDemo
{
    public AssembleDemo() : base() { }

    public override string Name => "Assemble";

    public override string Summary => "Merge, import, reorder, duplicate, split, prune and consolidate.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Two documents built in memory and merged into this one with ImportPage",
        "AnnotationCopyingType - a link that survives the import, and the setting that drops it",
        "MovePage and DuplicatePage, with the result visible in the pages that follow",
        "Splitting the merged document back into one file per page, in memory",
        "PruneUnusedResources and ConsolidateImages, reported as the bytes they save",
        "Why a document this library wrote has little to prune, and what does",
    };

    public override int PageCount => 7;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        XFont heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        XFont label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        XFont body = new XFont("Liberation Sans", 9);
        XFont mono = new XFont("Source Code Pro", 8.5);
        XFont huge = new XFont("Liberation Sans", 48, XFontStyle.Bold);

        // Every source page says loudly which document and which page it was, so that the order
        // the assembled document ends up in can be read off the pages themselves.
        void Stamp(PdfPage page, string name, XColor colour)
        {
            using XGraphics gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(new XSolidBrush(colour), 0, 0, page.Width.Point, 90);
            gfx.DrawString(name, huge, XBrushes.White, new XRect(0, 10, page.Width.Point, 70),
                XStringFormats.Center);
        }

        // A source document is a document like any other. Built here, saved to memory and read
        // back in Import mode - which is the mode that permits taking pages *out* of a document,
        // as against Modify, which permits changing them.
        PdfDocument Source(string prefix, int pages, XColor colour, bool withLink, bool withImage,
            out long bytes)
        {
            PdfDocument source = new PdfDocument();
            source.Info.Title = prefix;

            for (int index = 1; index <= pages; index++)
            {
                PdfPage page = source.AddPage();
                Stamp(page, $"{prefix}{index}", colour);

                using XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.DrawString($"Page {index} of document {prefix}", body, XBrushes.Black,
                    new XPoint(50, 130));

                if (withImage)
                {
                    // A fresh XImage per page, deliberately. Two pages sharing one XImage already
                    // share one XObject; two that loaded the same bytes separately do not, and
                    // that is the case ConsolidateImages exists for.
                    using XImage photograph = XImage.FromStream(
                        () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));
                    gfx.DrawImage(photograph, 50, 160, 200, 150);
                }

                if (withLink && index == 1 && pages > 1)
                {
                    gfx.DrawString("This line links to the last page of this document.", body,
                        XBrushes.MediumBlue, new XPoint(50, 340));
                    gfx.AddDocumentLink(new XRect(50, 330, 300, 14), pages - 1);
                }
            }

            // Measured here rather than after reopening: a document opened in Import mode is not
            // one that can be saved, so its size has to be taken while it is still being written.
            using MemoryStream buffer = new MemoryStream();
            source.Save(buffer, false);
            bytes = buffer.Length;

            buffer.Position = 0;
            return PdfReader.Open(buffer, PdfDocumentOpenMode.Import);
        }

        long Bytes(PdfDocument document)
        {
            using MemoryStream buffer = new MemoryStream();
            document.Save(buffer, false);
            return buffer.Length;
        }

        using PdfDocument sourceA = Source("A", 3, XColor.FromArgb(70, 130, 180),
            withLink: true, withImage: true, out long bytesA);
        using PdfDocument sourceB = Source("B", 2, XColor.FromArgb(178, 34, 34),
            withLink: false, withImage: true, out long bytesB);

        // ----- the assembly itself -----

        PdfDocument document = new PdfDocument();
        document.Info.Title = "Assemble";

        // Created first and drawn last, once there are numbers to put on it.
        PdfPage report = document.AddPage();

        // AddPage(PdfPage) takes a page belonging to another document and copies it in. The
        // annotation setting decides what happens to anything interactive on the way: a shallow
        // copy keeps the annotation and its destination if the destination is coming too, a deep
        // copy drags in what it points at, and DoNotCopy leaves it behind.
        foreach (PdfPage page in sourceA.Pages)
            document.AddPage(page, AnnotationCopyingType.ShallowCopy);

        foreach (PdfPage page in sourceB.Pages)
            document.AddPage(page, AnnotationCopyingType.ShallowCopy);

        long bytesMerged = Bytes(document);

        // Both source documents drew the same photograph, and each loaded it separately, so the
        // merged document carries the image twice over. Nothing about the pages changes; one of
        // the two XObjects simply stops being referenced.
        document.ConsolidateImages();
        long bytesConsolidated = Bytes(document);

        // Dropping what no page actually draws with. A document this library wrote gives each page
        // its own resource dictionary, so there is usually nothing here to find - the saving turns
        // up on pages imported from a producer that names every font in the document on every page.
        document.PruneUnusedResources();
        long bytesPruned = Bytes(document);

        // A duplicate of the first imported page, placed at the end. Within one document, so no
        // import is involved and the copy shares what it can with the original.
        document.DuplicatePage(1, document.PageCount);

        // And a reorder. The pages that follow this report are their own evidence: B2 has moved
        // from the end of the run to the front of it.
        document.MovePage(5, 1);

        int annotationsOnFirstImported = document.Pages[2].Annotations.Count;

        // ----- splitting, which is importing read backwards -----

        // The document being assembled cannot be split from directly: its pages belong to a
        // document that is open to be written, and AddPage refuses to hand them to somebody else.
        // Saving it and reopening in Import mode is the whole of the technique - and is the same
        // step the two source documents went through on their way in.
        long splitTotal = 0;
        int splitCount = 0;
        using (MemoryStream buffer = new MemoryStream())
        {
            document.Save(buffer, false);
            buffer.Position = 0;

            using PdfDocument assembled = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);
            foreach (PdfPage page in assembled.Pages)
            {
                using PdfDocument single = new PdfDocument();
                single.AddPage(page);
                splitTotal += Bytes(single);
                splitCount++;
            }
        }

        // ----- the report page, now that everything has a number -----

        using (XGraphics gfx = XGraphics.FromPdfPage(report))
        {
            XTextFormatter prose = new XTextFormatter(gfx);

            gfx.DrawString("Assembling documents", heading, XBrushes.Black, new XPoint(50, 60));

            prose.DrawString(
                "Two documents were built in memory, saved, reopened in Import mode and merged "
                + "into this one. Import mode is what permits taking pages out of a document; "
                + "Modify permits changing them and refuses to let them be extracted. Picking the "
                + "wrong one is the usual reason an assembly API appears to do nothing.",
                body, XBrushes.Black, new XRect(50, 80, 495, 60));

            gfx.DrawString("What was done", label, XBrushes.Black, new XPoint(50, 155));

            (string Step, string Detail)[] steps =
            {
                ("Document A", $"3 pages, a photograph on each, and a link from A1 to A3. {bytesA:N0} bytes."),
                ("Document B", $"2 pages, the same photograph on each. {bytesB:N0} bytes."),
                ("AddPage x 5", $"Every page of both, copied in. {bytesMerged:N0} bytes."),
                ("ConsolidateImages", $"{bytesMerged - bytesConsolidated:N0} bytes saved - the photograph was embedded twice."),
                ("PruneUnusedResources", $"{bytesConsolidated - bytesPruned:N0} bytes saved."),
                ("DuplicatePage(1, 6)", "A copy of the first imported page, placed at the end."),
                ("MovePage(5, 1)", "B2 moved from the end of the run to the front of it."),
                ("Split", $"{splitCount} single-page documents, {splitTotal:N0} bytes between them."),
            };

            double y = 175;
            foreach ((string Step, string Detail) step in steps)
            {
                gfx.DrawString(step.Step, mono, XBrushes.Black, new XPoint(50, y));
                gfx.DrawString(step.Detail, body, XBrushes.DimGray, new XPoint(190, y));
                y += 16;
            }

            gfx.DrawString("What to look for", label, XBrushes.Black, new XPoint(50, y + 20));
            prose.DrawString(
                "The pages after this one read B2, A1, A2, A3, B1, A1 - the order the moves above "
                + "left them in, not the order they were added. The first A1 still carries its "
                + $"link to A3 ({annotationsOnFirstImported} annotation(s) survived the import); "
                + "the copy at the end came from DuplicatePage rather than from another import.",
                body, XBrushes.Black, new XRect(50, y + 32, 495, 60));

            gfx.DrawString("Why the two savings differ so much", label, XBrushes.Black, new XPoint(50, y + 105));
            prose.DrawString(
                "ConsolidateImages finds XObjects with identical bytes and points every reference "
                + "at one of them, which pays whenever two merged documents embedded the same logo "
                + "or photograph. PruneUnusedResources drops what a page names and does not draw "
                + "with, and a document this library wrote gives each page its own resources - so "
                + "there is little to find here. It pays on a page imported from a producer that "
                + "names every font in the document on every page of it, which is why splitting "
                + "such a document can otherwise give every single-page file the weight of the "
                + "whole.",
                body, XBrushes.Black, new XRect(50, y + 117, 495, 100));

            prose.DrawString(
                $"Splitting bears that out from the other side: {splitCount} one-page documents "
                + $"come to {splitTotal:N0} bytes against the {bytesPruned:N0} of the document they "
                + "came from, because each of them carries its own copy of every font and image its "
                + "page draws with.",
                body, XBrushes.Black, new XRect(50, y + 225, 495, 60));
        }
        #endregion

        return document;
    }
}
