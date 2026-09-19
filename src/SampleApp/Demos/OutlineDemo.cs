using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Hierarchical bookmarks: a tree of outline entries, each landing on the heading it names
///   rather than on the page the heading happens to be on.
/// </summary>
/// <remarks>
///   <para>
///     The distinction in that sentence is the whole point of the demo. An entry built from a page
///     alone writes <c>/XYZ null null null</c>, which leaves a reader wherever the page was already
///     scrolled to - so a contents list of twelve entries pointing at one page appears to do nothing
///     eleven times. Setting <see cref="PdfOutline.Top"/> is what makes it land.
///   </para>
///   <para>
///     Building this demo is what found the defect behind <see cref="PdfOutline.Opened"/>, which
///     used to be inert: a reader takes an entry's expanded state from <c>/Count</c>, and nothing
///     wrote it, so every tree arrived collapsed however it had been built. Chapter 2 below is the
///     regression test a human can see - it is the one branch asked to arrive shut, and if it ever
///     looks like the others again, the count has stopped being written.
///   </para>
/// </remarks>
internal sealed class OutlineDemo : PdfDemo
{
    public OutlineDemo() : base() { }

    public override string Name => "Outline";

    public override string Summary => "A tree of bookmarks, each landing on its own heading.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "A three-level outline built with PdfDocument.Outlines and PdfOutline.Outlines",
        "Entries that land on a heading part-way down a page, not merely on the page",
        "The bold, italic and coloured entry styles",
        "Branches that arrive expanded, and chapter 2 which arrives collapsed",
        "The destination types: Xyz with a zoom, Fit, FitH and FitR",
        "A drawn table of contents where every line links to the place its bookmark points at"
    };

    public override int PageCount => 5;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";

        var document = new PdfDocument();
        document.Info.Title = "Outline";

        // docs:begin page-mode
        // Ask the reader to show the bookmark panel when the document opens. Without this an
        // outline is there but folded away, and a demo of bookmarks that shows none is no demo.
        document.PageMode = PdfPageMode.UseOutlines;
        // docs:end page-mode

        var titleFont = new XFont(Sans, 22, XFontStyle.Bold);
        var chapterFont = new XFont(Sans, 16, XFontStyle.Bold);
        var sectionFont = new XFont(Sans, 12, XFontStyle.Bold);
        var subFont = new XFont(Sans, 10, XFontStyle.Bold);
        var body = new XFont(Sans, 9.5);
        var noteFont = new XFont(Sans, 7.5);
        var mono = new XFont("Source Code Pro", 8);

        // docs:begin top-of
        // An outline destination is a position in default page space, measured up from the foot
        // of the page. Everything drawn below is placed in world space, measured down from the
        // head of it, so every heading's position has to be converted on the way into the tree.
        double TopOf(XGraphics on, double worldY)
        {
            return on.Transformer.WorldToDefaultPage(new XRect(0, worldY, 0, 0)).Y;
        }
        // docs:end top-of

        // docs:begin heading
        // Draws a heading and hands back the place an entry pointing at it should land: a little
        // above the text, so the heading is not flush against the top edge of the window.
        //
        // The same place is also named here, after the heading's own text, so that the contents
        // list on page one can link to it without knowing which page it ended up on. Naming it in
        // the one place that knows where the heading went is what keeps the bookmark and the
        // contents line pointing at the same spot.
        double Heading(XGraphics on, string text, XFont font, double x, double baseline)
        {
            on.DrawString(text, font, XBrushes.Black, new XPoint(x, baseline));

            var ascent = font.GetHeight() * font.CellAscent / font.CellSpace;
            var landing = baseline - ascent - 10;

            on.AddNamedDestination(text, new XPoint(x, landing));

            return TopOf(on, landing);
        }
        // docs:end heading

        void Paragraphs(XGraphics on, double x, double baseline, int count)
        {
            for (var line = 0; line < count; line++)
            {
                on.DrawString(
                    "Body text, here only so that the headings are not adjacent and a bookmark "
                    + "has somewhere to scroll to.",
                    body, XBrushes.DimGray, new XPoint(x, baseline + line * 13));
            }
        }

        // ---- Page one: the title, and what an outline is -----------------------------
        var titlePage = document.AddPage();
        var titleGfx = XGraphics.FromPdfPage(titlePage);

        titleGfx.DrawString("Hierarchical bookmarks", titleFont, XBrushes.Black,
            new XPoint(56, 96));
        titleGfx.DrawLine(new XPen(XColors.SteelBlue, 2), 56, 108, 539, 108);
        titleGfx.DrawString(
            "PDF calls them outline entries. Every reader calls the panel \"Bookmarks\".",
            body, XBrushes.DimGray, new XPoint(56, 126));

        string[] explanation =
        {
            "PdfDocument.Outlines is the root collection. Each PdfOutline it returns has an",
            "Outlines collection of its own, and that is the whole of the hierarchy - there is no",
            "depth limit and no separate node type.",
            "",
            "    var chapter = document.Outlines.Add(\"Chapter 1\", page, opened: true);",
            "    var section = chapter.Outlines.Add(\"1.1 The first section\", page);",
            "    section.Top = 640;                     // where on the page to land",
            "",
            "Add(title, page) alone writes /XYZ null null null - a destination naming a page and",
            "nothing else, which leaves the reader wherever that page is already scrolled to. Set",
            "Top and the entry lands on the heading. Every entry in this document sets it, which",
            "is why clicking down the tree moves rather than appearing not to.",
            "",
            "Style and TextColor are the entry's own - chapter 3 below is bold italic and red.",
            "Opened decides whether a branch arrives expanded, and is written as /Count: the number",
            "of rows the branch would add, negated when it is shut. Chapters 1 and 3 are open",
            "below and chapter 2 is not, so the panel should show its sections only after a click."
        };

        double y = 164;
        foreach (var line in explanation)
        {
            titleGfx.DrawString(line, line.StartsWith("    ") ? mono : body, XBrushes.Black,
                new XPoint(56, y));
            y += 14;
        }

        // ---- A drawn contents page, beside the outline -------------------------------
        //
        // The outline panel and this list are two views of one structure, so both are built
        // from the same records below rather than written out twice - and every line of the list
        // links to the same named destination its bookmark points at.
        y += 16;
        titleGfx.DrawString("Contents", sectionFont, XBrushes.Black, new XPoint(56, y));
        titleGfx.DrawLine(XPens.LightGray, 56, y + 6, 539, y + 6);
        var contentsY = y + 26;

        // docs:begin contents-link
        // One line of the contents, linked to the heading it names. The hot area is measured
        // rather than guessed: a rectangle wider than its text swallows clicks meant for the line
        // beside it, and one the height of the font rather than of the row leaves no dead gap.
        void ContentsLine(string text, XFont font, XBrush brush, double indent, double advance,
            string? pageNumber)
        {
            titleGfx.DrawString(text, font, brush, new XPoint(indent, contentsY));

            var ascent = font.GetHeight() * font.CellAscent / font.CellSpace;
            var size = titleGfx.MeasureString(text, font);
            titleGfx.AddNamedLink(
                new XRect(indent, contentsY - ascent, size.Width, font.GetHeight()), text);

            if (pageNumber != null)
            {
                titleGfx.DrawString(pageNumber, body, XBrushes.DimGray, new XPoint(500, contentsY));
            }

            contentsY += advance;
        }
        // docs:end contents-link

        // ---- Pages two to four: three chapters ---------------------------------------
        (string Chapter, bool Opened, XColor Colour, PdfOutlineStyle Style, string[] Sections)[] book =
        {
            ("1. Setting out", true, XColors.Black, PdfOutlineStyle.Bold,
                new[] { "1.1 What a bookmark is", "1.2 Where it points", "1.3 What it costs" }),
            ("2. In the middle", false, XColors.Black, PdfOutlineStyle.Regular,
                new[] { "2.1 Nesting", "2.2 Opened and collapsed" }),
            ("3. Coming back", true, XColors.Firebrick, PdfOutlineStyle.BoldItalic,
                new[] { "3.1 Styles", "3.2 Colours" })
        };

        foreach (var part in book)
        {
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var chapterTop = Heading(gfx, part.Chapter, chapterFont, 56, 96);
            gfx.DrawLine(new XPen(XColors.SteelBlue, 1.5), 56, 106, 539, 106);
            Paragraphs(gfx, 56, 128, 2);

            // docs:begin chapter
            // Add(title, page, opened, style, colour) is the widest overload. The colour and the
            // style are the entry's own - they say nothing about the heading on the page.
            var chapter = document.Outlines.Add(part.Chapter, page, part.Opened,
                part.Style, part.Colour);
            chapter.Top = chapterTop;
            // docs:end chapter

            ContentsLine(part.Chapter, subFont, XBrushes.Black, 56, 15,
                document.PageCount.ToString());

            double sectionY = 176;
            foreach (var title in part.Sections)
            {
                // docs:begin section
                var sectionTop = Heading(gfx, title, sectionFont, 56, sectionY);
                Paragraphs(gfx, 56, sectionY + 20, 2);

                var section = chapter.Outlines.Add(title, page);
                section.Top = sectionTop;
                // docs:end section

                ContentsLine(title, body, XBrushes.DimGray, 76, 13, null);

                // The third level, on the first section of the first chapter alone - enough to
                // show that the tree keeps going, without three pages of scaffolding.
                if (part.Chapter.StartsWith("1.") && title.StartsWith("1.1"))
                {
                    var subY = sectionY + 56;
                    foreach (var leaf in new[] { "1.1.1 A subsection", "1.1.2 And another" })
                    {
                        var subTop = Heading(gfx, leaf, subFont, 76, subY);
                        Paragraphs(gfx, 76, subY + 16, 1);

                        var sub = section.Outlines.Add(leaf, page);
                        sub.Top = subTop;

                        ContentsLine(leaf, noteFont, XBrushes.Gray, 96, 12, null);

                        subY += 44;
                    }

                    // Set after the entry was added rather than passed to Add, which is the case
                    // the old counting missed: it ran once, inside Add, and never looked again.
                    section.Opened = true;
                    sectionY += 100;
                }

                sectionY += 92;
            }
        }

        // ---- Page five: the destination types -----------------------------------------
        var appendix = document.AddPage();
        var appendixGfx = XGraphics.FromPdfPage(appendix);

        var appendixTop = Heading(appendixGfx, "Appendix. Destination types", chapterFont,
            56, 96);
        appendixGfx.DrawLine(new XPen(XColors.SteelBlue, 1.5), 56, 106, 539, 106);
        appendixGfx.DrawString(
            "PdfOutline.PageDestinationType decides which of the coordinates below are read.",
            body, XBrushes.DimGray, new XPoint(56, 128));

        var appendixEntry = document.Outlines.Add("Appendix. Destination types", appendix,
            true, PdfOutlineStyle.Bold);
        appendixEntry.Top = appendixTop;

        ContentsLine("Appendix. Destination types", subFont, XBrushes.Black, 56, 15, "5");

        (string Label, string Reads, PdfPageDestinationType Type)[] destinations =
        {
            ("Xyz - a corner and a zoom", "Left, Top, Zoom", PdfPageDestinationType.Xyz),
            ("Fit - the whole page in the window", "nothing", PdfPageDestinationType.Fit),
            ("FitH - the width, at a height", "Top", PdfPageDestinationType.FitH),
            ("FitV - the height, at a left edge", "Left", PdfPageDestinationType.FitV),
            ("FitR - a rectangle of the page", "Left, Bottom, Right, Top", PdfPageDestinationType.FitR),
            ("FitB - the ink, not the page", "nothing", PdfPageDestinationType.FitB),
            ("FitBH - the ink's width, at a height", "Top", PdfPageDestinationType.FitBH),
            ("FitBV - the ink's height, at a left edge", "Left", PdfPageDestinationType.FitBV)
        };

        double rowY = 170;
        appendixGfx.DrawString("type", noteFont, XBrushes.SteelBlue, new XPoint(56, rowY));
        appendixGfx.DrawString("coordinates read", noteFont, XBrushes.SteelBlue,
            new XPoint(320, rowY));
        rowY += 6;
        appendixGfx.DrawLine(XPens.LightGray, 56, rowY, 539, rowY);
        rowY += 18;

        foreach (var row in destinations)
        {
            var entryTop = TopOf(appendixGfx, rowY - 12);

            appendixGfx.DrawString(row.Label, body, XBrushes.Black, new XPoint(56, rowY));
            appendixGfx.DrawString(row.Reads, mono, XBrushes.DimGray, new XPoint(320, rowY));

            // docs:begin destination-types
            var entry = appendixEntry.Outlines.Add(row.Label, appendix);
            entry.PageDestinationType = row.Type;
            entry.Top = entryTop;
            entry.Left = 40;
            entry.Right = 555;
            entry.Bottom = entryTop - 120;

            // Xyz alone reads Zoom. 2 is 200%; leaving it unset keeps the reader's own.
            if (row.Type == PdfPageDestinationType.Xyz)
                entry.Zoom = 2;
            // docs:end destination-types

            rowY += 22;
        }

        rowY += 24;
        appendixGfx.DrawString(
            "Click each of these in the bookmark panel: they all point at this page, and what",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        appendixGfx.DrawString(
            "changes is how the reader frames it. Xyz is the default, and the only one that zooms.",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        appendixGfx.DrawString(
            "PdfDocument.PageMode = PdfPageMode.UseOutlines is what opened the panel for you, and",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        appendixGfx.DrawString(
            "Opened on each entry is what decided how much of the tree was already unfolded in it.",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        #endregion

        return document;
    }
}
