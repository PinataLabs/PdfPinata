using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The two font encodings, what each can carry, and how the two kinds of outline are embedded.
/// </summary>
internal sealed class UnicodeDemo : PdfDemo
{
    public UnicodeDemo() : base() { }

    public override string Name => "Unicode";

    public override string Summary => "WinAnsi against Unicode encoding, and how each face is embedded.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "XPdfFontOptions.WinAnsiDefault against UnicodeDefault, on the same strings",
        "What WinAnsi cannot carry, drawn beside what it can",
        "The difference in the file: a simple font with a readable literal, or a CID font with glyph ids",
        "PdfDocumentRenderer(unicode: true) - the same switch, under a name that hides it",
        "TrueType embedded as a subset in /FontFile2, CFF embedded whole in /FontFile3",
        "Why CJK is not on this page",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();
        document.Info.Title = "Unicode";

        XFont heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        XFont label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        XFont body = new XFont("Liberation Sans", 9);
        XFont mono = new XFont("Source Code Pro", 8.5);

        // The same family and size, differing only in what encoding the font is written with. The
        // options go in the constructor: there is no property to change afterwards, because the
        // encoding decides which kind of PDF font object gets built.
        XFont winAnsi = new XFont("Liberation Sans", 13, XFontStyle.Regular,
            XPdfFontOptions.WinAnsiDefault);
        XFont unicode = new XFont("Liberation Sans", 13, XFontStyle.Regular,
            XPdfFontOptions.UnicodeDefault);

        (string Text, string What)[] samples =
        {
            ("The quick brown fox", "Plain ASCII - both encodings carry it"),
            ("Café, naïve, Straße, £42", "Latin-1 - inside WinAnsi's 256 places"),
            ("Ελληνικά", "Greek - outside WinAnsi"),
            ("Кириллица", "Cyrillic - outside WinAnsi"),
            ("Ćwiczenia, Łódź", "Latin Extended - outside WinAnsi"),
            ("→ ← ↑ ↓ ∑ ∞", "Arrows and mathematics - outside WinAnsi"),
        };

        // ----- page 1: what each encoding can carry -----

        PdfPage page1 = document.AddPage();
        XGraphics gfx1 = XGraphics.FromPdfPage(page1);
        XTextFormatter prose1 = new XTextFormatter(gfx1);

        gfx1.DrawString("What each encoding carries", heading, XBrushes.Black, new XPoint(50, 60));

        prose1.DrawString(
            "An XFont carries an XPdfFontOptions, and the encoding in it decides what kind of font "
            + "object is written into the PDF. WinAnsi writes a simple font with a single-byte "
            + "encoding and 256 places to put a character in; Unicode writes a CID font, which has "
            + "no such limit. Both rows below are the same string in the same face at the same "
            + "size. Where they differ, the character was not one of WinAnsi's 256.",
            body, XBrushes.Black, new XRect(50, 80, 495, 60));

        gfx1.DrawString("WinAnsiDefault", label, XBrushes.Black, new XPoint(50, 158));
        gfx1.DrawString("UnicodeDefault", label, XBrushes.Black, new XPoint(280, 158));
        gfx1.DrawLine(new XPen(XColors.Gainsboro, 0.5), 50, 165, 545, 165);

        double y = 190;
        foreach ((string Text, string What) sample in samples)
        {
            gfx1.DrawString(sample.Text, winAnsi, XBrushes.Black, new XPoint(50, y));
            gfx1.DrawString(sample.Text, unicode, XBrushes.Black, new XPoint(280, y));
            gfx1.DrawString(sample.What, body, XBrushes.DimGray, new XPoint(50, y + 13));
            y += 36;
        }

        prose1.DrawString(
            "Nothing threw. A character WinAnsi has no place for is not an error - it is dropped or "
            + "replaced on the way out, which is exactly the failure that gets noticed after the "
            + "document has been sent. Unicode is the safe default and is what "
            + "PdfDocumentRenderer(unicode: true) selects for a PinataLayout document, under a name "
            + "that gives no hint that it is this setting.",
            body, XBrushes.Black, new XRect(50, y + 10, 495, 60));

        // ----- page 2: what it does to the file -----

        // Two one-string documents, saved, so the difference can be read out of the files rather
        // than described. Nothing is written to disk.
        (string Encoding, string Subtype, string FontFile, int Length, long Bytes) Probe(
            XPdfFontOptions options, string text, string family)
        {
            using MemoryStream buffer = new MemoryStream();
            using (PdfDocument probe = new PdfDocument())
            {
                probe.Options.CompressContentStreams = true;
                PdfPage page = probe.AddPage();
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawString(text, new XFont(family, 12, XFontStyle.Regular, options),
                        XBrushes.Black, new XPoint(50, 50));
                }

                probe.Save(buffer, false);
            }

            buffer.Position = 0;
            using PdfDocument reopened = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);

            // Walk the page's font resources and report what kind of font object was written and
            // which key the face's bytes ended up under.
            PdfDictionary? fonts = reopened.Pages[0].Elements
                .GetDictionary("/Resources")?.Elements.GetDictionary("/Font");

            string subtype = "none", fontFile = "none";
            int length = 0;

            if (fonts != null)
            {
                foreach (string key in fonts.Elements.KeyNames.Select(name => name.Value))
                {
                    PdfDictionary font = fonts.Elements.GetDictionary(key);
                    subtype = font.Elements.GetName("/Subtype");

                    // A CID font hides the descriptor one level down, under /DescendantFonts.
                    PdfDictionary? descriptor = font.Elements.GetDictionary("/FontDescriptor");
                    if (descriptor == null)
                    {
                        PdfArray descendants = font.Elements.GetArray("/DescendantFonts");
                        if (descendants != null && descendants.Elements.Count > 0)
                        {
                            descriptor = (descendants.Elements.GetDictionary(0))
                                ?.Elements.GetDictionary("/FontDescriptor");
                        }
                    }

                    if (descriptor != null)
                    {
                        foreach (string file in new[] { "/FontFile", "/FontFile2", "/FontFile3" })
                        {
                            PdfDictionary embedded = descriptor.Elements.GetDictionary(file);
                            if (embedded != null)
                            {
                                fontFile = file;
                                length = embedded.Stream?.Length ?? 0;
                            }
                        }
                    }
                }
            }

            return (options.FontEncoding.ToString(), subtype, fontFile, length, buffer.Length);
        }

        var probes = new[]
        {
            Probe(XPdfFontOptions.WinAnsiDefault, "Hello", "Liberation Sans"),
            Probe(XPdfFontOptions.UnicodeDefault, "Hello", "Liberation Sans"),
            Probe(XPdfFontOptions.UnicodeDefault, "Кириллица", "Liberation Sans"),
            Probe(XPdfFontOptions.WinAnsiDefault, "Hello", "Source Code Pro"),
            Probe(XPdfFontOptions.UnicodeDefault, "Hello", "Source Code Pro"),
        };

        string[] descriptions =
        {
            "Liberation Sans, WinAnsi", "Liberation Sans, Unicode",
            "Liberation Sans, Unicode, Cyrillic", "Source Code Pro, WinAnsi",
            "Source Code Pro, Unicode",
        };

        PdfPage page2 = document.AddPage();
        XGraphics gfx2 = XGraphics.FromPdfPage(page2);
        XTextFormatter prose2 = new XTextFormatter(gfx2);

        gfx2.DrawString("What it does to the file", heading, XBrushes.Black, new XPoint(50, 60));

        prose2.DrawString(
            "Five one-word documents, saved and reopened, with the font object each of them wrote "
            + "read back out. The subtype is the kind of PDF font; the key is where the face's own "
            + "bytes ended up.",
            body, XBrushes.Black, new XRect(50, 80, 495, 40));

        gfx2.DrawString("document", label, XBrushes.Black, new XPoint(50, 135));
        gfx2.DrawString("subtype", label, XBrushes.Black, new XPoint(230, 135));
        gfx2.DrawString("key", label, XBrushes.Black, new XPoint(340, 135));
        gfx2.DrawString("face bytes", label, XBrushes.Black, new XPoint(420, 135));
        gfx2.DrawString("file", label, XBrushes.Black, new XPoint(490, 135));

        double row = 155;
        for (int index = 0; index < probes.Length; index++)
        {
            gfx2.DrawString(descriptions[index], body, XBrushes.Black, new XPoint(50, row));
            gfx2.DrawString(probes[index].Subtype, mono, XBrushes.Firebrick, new XPoint(230, row));
            gfx2.DrawString(probes[index].FontFile, mono, XBrushes.Firebrick, new XPoint(340, row));
            gfx2.DrawString($"{probes[index].Length:N0}", body, XBrushes.Black, new XPoint(420, row));
            gfx2.DrawString($"{probes[index].Bytes:N0}", body, XBrushes.DimGray, new XPoint(490, row));
            row += 16;
        }

        gfx2.DrawString("Two outlines, two embedding paths", label, XBrushes.Black,
            new XPoint(50, row + 20));

        prose2.DrawString(
            "Liberation Sans is TrueType, so only the glyphs the document uses are embedded - the "
            + "face is subsetted, and the two Liberation rows differ in size for that reason "
            + "alone. Source Code Pro has PostScript (CFF) outlines, which this library embeds "
            + "whole under /FontFile3 because a CFF subsetter is not written; a document using one "
            + "character of it carries the same bytes as a document using all of them. That is the "
            + "trade recorded in font-embedding-gaps.md, and it is why the size column moves for "
            + "one face and not for the other.",
            body, XBrushes.Black, new XRect(50, row + 33, 495, 80));

        gfx2.DrawString("Why CJK is not on this page", label, XBrushes.Black, new XPoint(50, row + 125));

        prose2.DrawString(
            "Nothing here is a limit of the library: a CID font carries any character a face has a "
            + "glyph for, and this app simply does not carry a face that has CJK glyphs. Liberation "
            + "Sans covers Latin, Latin Extended, Greek and Cyrillic, which is what page one uses. "
            + "A CJK face is several megabytes for one panel, so the demo shows the mechanism and "
            + "leaves the asset to whoever needs it - register any face through IFontResolver and "
            + "the Unicode path above carries it.",
            body, XBrushes.Black, new XRect(50, row + 138, 495, 80));
        #endregion

        return document;
    }
}
