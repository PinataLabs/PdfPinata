using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Everything a document says about how it should be presented rather than about what is on it.
/// </summary>
internal sealed class NavigationDemo : PdfDemo
{
    public NavigationDemo() : base()
    {
    }

    public override string Name => "Navigation";

    public override string Summary => "Page labels, viewer preferences, layout, language and private data.";

    public override IReadOnlyList<string> Shows =>
    [
        "PdfPageLabels - roman front matter and arabic body, so a reader's page box reads iv, not 4",
        "All six label styles, and the prefix that goes in front of one",
        "PdfViewerPreferences - hide the toolbar, centre the window, show the title rather than the file name",
        "PageLayout and PageMode, which decide what a reader shows when the document opens",
        "Document.Language, which is what a screen reader announces the document in",
        "CustomValues - private data in the catalog that survives a round trip and no reader displays",
        "NamedDestinations, the table, as against the single named destination the Text demo makes"
    ];

    public override int PageCount => 6;

    #region example
    protected override PdfDocument Build(DemoContext context)
    {
        var document = new PdfDocument();
        document.Info.Title = "Navigation";

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        var body = new XFont("Liberation Sans", 9);
        var mono = new XFont("Source Code Pro", 8.5);
        var big = new XFont("Liberation Sans", 40, XFontStyle.Bold);

        // Six pages: three of front matter and three of body, which is what gives the page labels
        // below something to label differently.
        (string Kind, string Title)[] pages =
        [
            ("front", "Half title"),
            ("front", "Title page"),
            ("front", "Contents"),
            ("body", "Chapter one"),
            ("body", "Chapter two"),
            ("body", "What the settings are")
        ];

        var made = new List<PdfPage>();
        for (var index = 0; index < pages.Length; index++)
        {
            var page = document.AddPage();
            made.Add(page);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString(pages[index].Title, big, XBrushes.Black,
                new XRect(0, 120, page.Width.Point, 60), XStringFormats.TopCenter);
            gfx.DrawString(
                pages[index].Kind == "front" ? "front matter" : "body",
                body, XBrushes.DimGray,
                new XRect(0, 190, page.Width.Point, 20), XStringFormats.TopCenter);
        }

        // ----- page labels -----

        // docs:begin page-labels
        // A reader shows these where it shows a page number, so the fourth sheet of this document
        // reads "iv" and the tenth would read "1". They are ranges: each Add says where a run
        // starts and how it is numbered, and the run lasts until the next one begins.
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal, prefix: null, start: 1);
        // docs:end page-labels

        // ----- viewer preferences, layout and mode -----

        // docs:begin viewer-preferences
        // What a reader does when the document opens. None of it changes a pixel of any page.
        document.PageLayout = PdfPageLayout.TwoColumnRight;
        document.PageMode = PdfPageMode.UseOutlines;

        document.ViewerPreferences.CenterWindow = true;
        document.ViewerPreferences.FitWindow = true;
        document.ViewerPreferences.DisplayDocTitle = true;
        document.ViewerPreferences.HideToolbar = false;
        document.ViewerPreferences.HideMenubar = false;
        // docs:end viewer-preferences

        // docs:begin language
        // What a screen reader announces the document in, and what a reader uses to pick
        // hyphenation rules. A single tag, and nothing else in the file records it.
        document.Language = "en-GB";
        // docs:end language

        // ----- private data and named destinations -----

        // docs:begin custom-values
        // Anything the producer wants to carry that is not part of the page. It lives in the
        // catalog under a key of the caller's choosing, no reader displays it, and it survives a
        // round trip - which is exactly what a pipeline needs to recognize its own output later.
        var pipeline = "{\"stage\":\"demonstration\",\"run\":42}"u8.ToArray();
        document.CustomValues["/Pipeline"] = new PdfCustomValue(pipeline);
        // docs:end custom-values

        // docs:begin named-destinations
        // A destination named rather than numbered, so a link can point at "chapter-two" and go on
        // pointing at it after pages are inserted in front of it. The table is the document-wide
        // one; the Text demo makes a single named destination on its own page.
        document.NamedDestinations.Add("half-title", made[0]);
        document.NamedDestinations.Add("contents", made[2]);
        document.NamedDestinations.Add("chapter-one", made[3]);
        document.NamedDestinations.Add("chapter-two", made[4], top: 200);
        // docs:end named-destinations

        // Outlines, so that PageMode.UseOutlines has something to open.
        document.Outlines.Add("Front matter", made[0], true);
        document.Outlines[0].Outlines.Add("Contents", made[2]);
        document.Outlines.Add("Chapter one", made[3], true);
        document.Outlines.Add("Chapter two", made[4], true);

        // ----- the last page reports it all back -----

        using (var gfx = XGraphics.FromPdfPage(made[5]))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What this document asks a reader for", heading, XBrushes.Black,
                new XPoint(50, 250));

            prose.DrawString(
                "None of the settings below changes a pixel of any page. They are the document "
                + "telling a reader how it would like to be presented, and a reader is free to "
                + "ignore every one of them.",
                body, XBrushes.Black, new XRect(50, 268, 495, 40));

            (string Setting, string Value, string Effect)[] rows =
            [
                ("PageLabels", "i-iii then 1-3", "The page box reads iv on sheet four, not 4"),
                ("PageLayout", "TwoColumnRight", "Two pages side by side, odd ones on the right"),
                ("PageMode", "UseOutlines", "The bookmark panel is open when the file opens"),
                ("CenterWindow", "true", "The window opens in the middle of the screen"),
                ("FitWindow", "true", "The window is sized to the first page"),
                ("DisplayDocTitle", "true", "The title bar shows Info.Title, not the file name"),
                ("Language", "en-GB", "What a screen reader announces it in"),
                ("CustomValues[\"/Pipeline\"]", $"{pipeline.Length} bytes of JSON", "Private data; no reader shows it"),
                ("NamedDestinations", "4 names", "Links that survive pages being inserted")
            ];

            double y = 325;
            foreach (var row in rows)
            {
                gfx.DrawString(row.Setting, mono, XBrushes.Black, new XPoint(50, y));
                gfx.DrawString(row.Value, body, XBrushes.Firebrick, new XPoint(210, y));
                gfx.DrawString(row.Effect, body, XBrushes.DimGray, new XPoint(300, y));
                y += 16;
            }

            gfx.DrawString("The six label styles", label, XBrushes.Black, new XPoint(50, y + 20));

            // Every style, shown as what the first four sheets of a run would read. Built through
            // the API rather than transcribed, so the table cannot drift from the implementation.
            (PdfPageLabelStyle Style, string? Prefix)[] styles =
            [
                (PdfPageLabelStyle.Decimal, null),
                (PdfPageLabelStyle.LowercaseRoman, null),
                (PdfPageLabelStyle.UppercaseRoman, null),
                (PdfPageLabelStyle.LowercaseLetters, null),
                (PdfPageLabelStyle.UppercaseLetters, null),
                (PdfPageLabelStyle.None, "Appendix ")
            ];

            var styleY = y + 40;
            foreach (var style in styles)
            {
                // docs:begin label-probe
                using var probe = new PdfDocument();
                for (var index = 0; index < 4; index++)
                    _ = probe.AddPage();
                probe.PageLabels.Add(0, style.Style, style.Prefix, 1);

                var labels = new List<string>();
                for (var index = 0; index < 4; index++)
                    labels.Add(probe.PageLabels.GetLabel(index));
                // docs:end label-probe

                gfx.DrawString(style.Style.ToString(), mono, XBrushes.Black, new XPoint(50, styleY));
                gfx.DrawString(PrefixNote(style.Prefix), body, XBrushes.DimGray, new XPoint(210, styleY));
                gfx.DrawString(string.Join("   ", labels), body, XBrushes.Firebrick,
                    new XPoint(320, styleY));
                styleY += 15;
            }

            prose.DrawString(
                "PdfPageLabelStyle.None with a prefix is how a run is labelled without being "
                + "numbered at all - every sheet of it reads the prefix and nothing else, which is "
                + "what a run of unnumbered plates wants.",
                body, XBrushes.Black, new XRect(50, styleY + 10, 495, 40));

            // Round-tripped rather than asserted: what survives a save and a reopen is the only
            // version of this that matters.
            using var buffer = new MemoryStream();
            using var copy = new PdfDocument();
            _ = copy.AddPage();
            copy.Language = "en-GB";
            copy.CustomValues["/Pipeline"] = new PdfCustomValue([.."kept"u8]);
            copy.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
            copy.Save(buffer, false);
            buffer.Position = 0;

            using var reopened = PdfReader.Open(buffer, PdfDocumentOpenMode.Modify);

            gfx.DrawString("After a save and a reopen", label, XBrushes.Black,
                new XPoint(50, styleY + 60));
            gfx.DrawString(
                $"Language = \"{reopened.Language}\", "
                + $"first page label = \"{reopened.PageLabels.GetLabel(0)}\", "
                + $"CustomValues[\"/Pipeline\"] = "
                + $"\"{Encoding.UTF8.GetString(reopened.CustomValues["/Pipeline"].Value)}\"",
                mono, XBrushes.Black, new XPoint(50, styleY + 78));
        }

        return document;
    }

    private static string PrefixNote(string? prefix) => prefix is null ? "" : $"prefix \"{prefix}\"";

    #endregion
}
