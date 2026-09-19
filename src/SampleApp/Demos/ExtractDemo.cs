using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Reading the text back out of a page: what it says, where it says it, and the one thing the
///   extractor deliberately will not guess at.
/// </summary>
/// <remarks>
///   The document this builds reads itself. Pages one and two are written, saved, opened again and
///   extracted; pages three and four are what came back. Nothing here is a description of the
///   output - it is the output.
/// </remarks>
internal sealed class ExtractDemo : PdfDemo
{
    public ExtractDemo() : base() { }

    public override string Name => "Extract";

    public override string Summary => "PdfTextExtractor: the text of a page, and where on the page it is.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "PdfTextExtractor.ExtractText, and PdfTextExtractor.ExtractRuns for the positions too",
        "That a run is one show-text operator, not one box per glyph - and why",
        "Text decoded through the font's own /ToUnicode map, so a subset font still reads back",
        "A run under a scaled transformation reporting its width and size in user space",
        "That runs come back in drawing order, which on a two-column page is not reading order",
        "That white-on-white text extracts perfectly, because invisible is not absent"
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        var label = new XFont(BundledFontResolver.SansFamily, 9.5, XFontStyle.Bold);
        var body = new XFont(BundledFontResolver.SansFamily, 9);
        var mono = new XFont(BundledFontResolver.MonoFamily, 7.5);

        // ----- pages one and two: the text that will be read back ----------------------------------

        using var source = new PdfDocument();
        source.Info.Title = "Extract";

        var first = source.AddPage();
        using (var gfx = XGraphics.FromPdfPage(first))
        {
            gfx.DrawString("A page to be read back", heading, XBrushes.Black, 50, 60);

            gfx.DrawString("Ordinary prose, one line per call:", label, XBrushes.Black, 50, 95);
            gfx.DrawString("The quick brown fox jumps over the lazy dog.", body, XBrushes.Black, 50, 112);
            gfx.DrawString("Every font here is embedded and subsetted, so the codes in the",
                body, XBrushes.Black, 50, 126);
            gfx.DrawString("content stream are glyph numbers rather than characters.",
                body, XBrushes.Black, 50, 140);

            // A serif face at a different size, so the extracted runs differ in more than position.
            gfx.DrawString("A different face, at a different size.",
                new XFont(BundledFontResolver.SerifFamily, 14), XBrushes.Black, 50, 168);

            gfx.DrawString("Under a scaled transformation:", label, XBrushes.Black, 50, 205);

            // Both the width and the size come back in user space, measured through the same matrix.
            // A test that only translates cannot see the difference, because a translation scales
            // by one - which is why this demo scales.
            var saved = gfx.Save();
            gfx.TranslateTransform(50, 230);
            gfx.ScaleTransform(2.0, 2.0);
            gfx.DrawString("Twice the size, drawn at half of it.", body, XBrushes.Black, 0, 0);
            gfx.Restore(saved);

            gfx.DrawString("Text nobody can read, which is still text:", label, XBrushes.Black, 50, 275);
            gfx.DrawString("White on white, and extracted all the same.",
                body, XBrushes.White, 50, 292);

            gfx.DrawString("Two columns, drawn line by line:", label, XBrushes.Black, 50, 330);

            string[] left =
            {
                "A page is a bag of glyphs at",
                "positions. Nothing in the file",
                "says which of them belong",
                "together, or in what order a",
                "person would read them."
            };

            string[] right =
            {
                "So an extractor reports what",
                "it can prove: one run per",
                "show-text operator, with the",
                "origin and the total width it",
                "advanced by."
            };

            // Drawn a line at a time across both columns, which is the order a typesetter would
            // never use and a naive loop always does. The point of the exercise is on page three.
            for (var line = 0; line < left.Length; line++)
            {
                double y = 350 + line * 14;
                gfx.DrawString(left[line], body, XBrushes.Black, 50, y);
                gfx.DrawString(right[line], body, XBrushes.Black, 300, y);
            }

            gfx.DrawString("Rotated, which keeps its origin and its width:", label, XBrushes.Black, 50, 450);

            var turned = gfx.Save();
            gfx.TranslateTransform(60, 560);
            gfx.RotateTransform(-30);
            gfx.DrawString("Thirty degrees off the horizontal.", body, XBrushes.Black, 0, 0);
            gfx.Restore(turned);
        }

        var second = source.AddPage();
        using (var gfx = XGraphics.FromPdfPage(second))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("A second page, laid out by the formatter", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "XTextFormatter breaks this paragraph into lines and hands each one to DrawString, "
                + "so the extractor sees one run per line and the lines come back in the order they "
                + "were drawn. That is reading order here because the layout is a single column, and "
                + "it is reading order by luck rather than by anything the file records.",
                body, XBrushes.Black, new XRect(50, 85, 495, 70));

            prose.DrawString(
                "Word spacing is the interesting case. The Tw operator adjusts the space between "
                + "words, and it applies to the single byte 32 and to nothing else - not to a "
                + "two-byte code whose low byte happens to be 32, which is what every glyph code in "
                + "a Unicode-encoded font is. That is the trap in the arithmetic, and getting it "
                + "wrong displaces every run after the first space.",
                body, XBrushes.Black, new XRect(50, 170, 495, 70));
        }

        // docs:begin extract
        // Saved and opened again, because that is the situation the extractor is for: a file that
        // arrived from somewhere, whose fonts are subsets and whose codes mean nothing without the
        // /ToUnicode map the file carries.
        var buffer = new MemoryStream();
        source.Save(buffer, false);
        buffer.Position = 0;

        var document = PdfReader.Open(buffer, PdfDocumentOpenMode.Modify);

        var extracted = PdfTextExtractor.ExtractText(document.Pages[0]);
        IReadOnlyList<PdfTextRun> runs = PdfTextExtractor.ExtractRuns(document.Pages[0]);
        IReadOnlyList<PdfTextRun> secondPage = PdfTextExtractor.ExtractRuns(document.Pages[1]);
        // docs:end extract

        // ----- page three: what came back ----------------------------------------------------------

        var third = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(third))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What page one says", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "ExtractText, printed verbatim. It is a convenience over ExtractRuns and no cleverer "
                + "than it: runs sharing a baseline are joined - with a space when there is a gap "
                + "wider than a fifth of the type size, and directly when there is not - and a new "
                + "baseline starts a new line.",
                body, XBrushes.Black, new XRect(50, 80, 495, 48));

            double y = 140;
            foreach (var line in extracted.Replace("\r\n", "\n").Split('\n'))
            {
                if (y > 470)
                    break;

                gfx.DrawString(line.Length == 0 ? " " : line, mono, XBrushes.Black, 56, y);
                y += 10;
            }

            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 0.8),
                new XRect(50, 130, 495, Math.Max(20, y - 136)));

            gfx.DrawString("The two columns came out interleaved", label, XBrushes.Firebrick, 50, y + 20);

            prose.DrawString(
                "Look for the column lines above: each one is a left-hand line and a right-hand line "
                + "run together, because they share a baseline and were drawn one after the other. "
                + "Nothing is wrong. Runs come back in the order they were drawn, which is the order "
                + "the producer chose and need not be reading order - and grouping runs into "
                + "columns, paragraphs and a reading sequence is layout analysis, which is a separate "
                + "piece of work and is deliberately not here.",
                body, XBrushes.Black, new XRect(50, y + 34, 495, 76));

            gfx.DrawString("Invisible is not the same as absent", label, XBrushes.Black, 50, y + 122);

            prose.DrawString(
                "The white-on-white line from page one is in the text above, because painting a "
                + "glyph in the colour of the paper does not stop it being a glyph. The one thing "
                + "the extractor does skip is text render mode 3, which is genuinely invisible and "
                + "is how the OCR layer under a scan is drawn - a caller asking what the page says "
                + "usually does not want it twice. There is no such line here to show: XGraphics has "
                + "no way to draw one, which is why an OCR layer is something this library reads and "
                + "does not write.",
                body, XBrushes.Black, new XRect(50, y + 136, 495, 76));
        }

        // ----- page four: where it says it ---------------------------------------------------------

        var fourth = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(fourth))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Where page one says it", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "ExtractRuns gives the origin, the total width and the type size of every run, in "
                + "user space - the origin at the bottom left of the page, as PDF measures it, "
                + "rather than at the top left as XGraphics does. The name is the resource name the "
                + "content stream selected the font by.",
                body, XBrushes.Black, new XRect(50, 80, 495, 48));

            gfx.DrawString("x", label, XBrushes.Black, 50, 138);
            gfx.DrawString("y", label, XBrushes.Black, 92, 138);
            gfx.DrawString("width", label, XBrushes.Black, 134, 138);
            gfx.DrawString("size", label, XBrushes.Black, 180, 138);
            gfx.DrawString("font", label, XBrushes.Black, 214, 138);
            gfx.DrawString("text", label, XBrushes.Black, 254, 138);

            double y = 152;
            // docs:begin read-runs
            foreach (var run in runs)
            {
                if (y > 600)
                    break;

                gfx.DrawString(Number(run.Origin.X), mono, XBrushes.Black, 50, y);
                gfx.DrawString(Number(run.Origin.Y), mono, XBrushes.Black, 92, y);
                gfx.DrawString(Number(run.Width), mono, XBrushes.Black, 134, y);
                gfx.DrawString(Number(run.FontSize), mono, XBrushes.Black, 180, y);
                gfx.DrawString(run.FontName ?? "-", mono, XBrushes.DimGray, 214, y);
                gfx.DrawString(Shortened(run.Text), mono, XBrushes.Black, 254, y);
                y += 9.4;
            }
            // docs:end read-runs

            gfx.DrawString("One run per operator, not one box per glyph", label, XBrushes.Black, 50, y + 22);

            prose.DrawString(
                "A per-glyph box is exact only when every glyph's own advance is known, and "
                + "reporting an approximate one is worse than reporting none - a caller cannot tell "
                + "the two apart. A run's origin and total width are exact, and are what most "
                + "callers actually want.",
                body, XBrushes.Black, new XRect(50, y + 36, 495, 48));

            gfx.DrawString("The scaled line proves the point", label, XBrushes.Black, 50, y + 96);

            // Reported rather than assumed. If the run cannot be found the sentence says so instead
            // of printing a nought as though it were a measurement - the same rule the refusal pages
            // in the Archive and Accessibility demos follow.
            var scaled = SizeOfScaledRun(runs);

            prose.DrawString(
                (scaled.HasValue
                    ? "The line drawn under a twofold scale reports a size of "
                      + Number(scaled.Value).Trim() + " rather than 9, and a width to match. "
                    : "The line drawn under a twofold scale is not among the runs above, so the "
                      + "measurement this paragraph was going to quote is not there to quote. ")
                + "Both are measured through the same matrix, and they have to be: the run is "
                + "reported in user space, so leaving the current transformation out of one of them "
                + "would give a width in text space and a size in user space, which disagree with "
                + "each other.",
                body, XBrushes.Black, new XRect(50, y + 110, 495, 62));

            gfx.DrawString(
                "Page two came back as " + secondPage.Count.ToString(CultureInfo.InvariantCulture)
                + " runs - one per line the formatter laid out.",
                label, XBrushes.Black, 50, y + 190);
        }
        #endregion

        return document;
    }

    /// <summary>
    ///   The size the doubled run reported, or null if that run is not among them.
    /// </summary>
    /// <remarks>
    ///   Nullable rather than nought, because nought is a plausible size and the page prints this
    ///   number as a fact about its own output. A demo that quietly quotes a default is a demo that
    ///   goes on claiming something after it has stopped being true.
    /// </remarks>
    static double? SizeOfScaledRun(IReadOnlyList<PdfTextRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.Text.StartsWith("Twice the size", StringComparison.Ordinal))
                return run.FontSize;
        }

        return null;
    }

    static string Number(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6);

    static string Shortened(string text) =>
        text.Length <= 44 ? text : string.Concat(text.AsSpan(0, 41), "...");
}
