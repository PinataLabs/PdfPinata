using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Text;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Text that does not run left to right, one letter to one glyph: Arabic and Hebrew reordered,
///   Arabic letters joined by the font's own rules, and a character drawn by a face the document
///   never asked for.
/// </summary>
internal sealed class InternationalDemo : PdfDemo
{
    public InternationalDemo() : base() { }

    public override string Name => "International";

    public override string Summary => "Right-to-left order, complex-script shaping, and font fallback.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Hebrew and Arabic drawn in the order they are read, with no shaper needed for it",
        "An English word inside a right-to-left sentence, keeping its own order",
        "XStringFormat.TextDirection - saying what the text is instead of leaving it to be guessed",
        "Arabic letters joined by the face's own GSUB rules, through PdfPinata.HarfBuzz",
        "GlobalFontSettings.FontFallback - Arabic in a document that asked for a Latin face",
        "XTextFormatter.TextDirection, for a paragraph laid out into a rectangle",
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();
        document.Info.Title = "International";

        // Every character in this file is ASCII, and the right-to-left text is assembled from code
        // points instead. A source file that mixes right-to-left text with left-to-right code is
        // one no editor renders the way anyone means: the quotation marks appear on the wrong side
        // of the string, and a reader cannot see where the text ends and the code begins.

        // "shalom", four Hebrew letters. Liberation Sans has Hebrew in it, so this needs neither a
        // shaper nor a fallback - the only thing that can be wrong about it is the order.
        string hebrew = From(0x05E9, 0x05DC, 0x05D5, 0x05DD);

        // "marhaba", a greeting - and a word whose middle letters join on both sides, which is what
        // makes it worth drawing twice on the second page.
        string arabic = From(0x0645, 0x0631, 0x062D, 0x0628, 0x0627);

        // "arabiyya", the name of the script, in it.
        string arabicName = From(0x0639, 0x0631, 0x0628, 0x064A, 0x0629);

        XFont heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        XFont label = new XFont(BundledFontResolver.SansFamily, 9, XFontStyle.Bold);
        XFont body = new XFont(BundledFontResolver.SansFamily, 9);
        XFont sample = new XFont(BundledFontResolver.SansFamily, 20);
        XFont arabicFace = new XFont(BundledFontResolver.ArabicFamily, 22);

        // ----- page one: the order the letters go on the page -------------------------------------

        PdfPage first = document.AddPage();
        using (XGraphics gfx = XGraphics.FromPdfPage(first))
        {
            gfx.DrawString("Reading order", heading, XBrushes.Black, 50, 60);

            Note(gfx, label, body, 50, 90,
                "Reordering is not shaping, and needs no shaper.",
                "PDF has no notion of direction: a show-text operator paints glyphs at the pen and",
                "moves the pen along. So the library reorders before it draws, and a caller who takes",
                "no HarfBuzz dependency still gets Hebrew and Arabic the right way round - unjoined,",
                "which is wrong, but no longer also backwards, which was the older complaint.");

            gfx.DrawString("Hebrew, drawn by the sans face:", label, XBrushes.Black, 50, 165);
            gfx.DrawString(hebrew, sample, XBrushes.Black, 50, 192);

            gfx.DrawString("The same letters, one call each, left to right:", label, XBrushes.Black, 50, 232);
            double at = 50;
            foreach (char letter in hebrew)
            {
                gfx.DrawString(letter.ToString(), sample, XBrushes.Gray, at, 259);
                at += 26;
            }

            Note(gfx, label, body, 50, 295,
                "Which is the whole of it.",
                "Read those two lines against each other: the word is the row of grey letters in",
                "reverse. The first letter written is the rightmost one drawn, and nothing in the",
                "calling code said so - the string went to DrawString in the order it is typed.");

            // A left-to-right sentence with a right-to-left word in it, and the other way about.
            // The whole line is not reversed in either case: each run turns round inside itself,
            // which is the difference between the bidirectional algorithm and calling Reverse.
            // docs:begin mixed-direction
            gfx.DrawString("A Hebrew word inside an English sentence:", label, XBrushes.Black, 50, 360);
            gfx.DrawString("The word " + hebrew + " means peace.", sample, XBrushes.Black, 50, 387);

            gfx.DrawString("An English word inside a Hebrew sentence:", label, XBrushes.Black, 50, 427);
            gfx.DrawString(hebrew + " peace " + hebrew, sample, XBrushes.Black, 50, 454);
            // docs:end mixed-direction

            // Automatic takes the direction from the first strong character, which is right far more
            // often than not and wrong exactly where it matters: a right-to-left line opening with a
            // brand name, a part number, or a quotation.
            // docs:begin declared-direction
            XStringFormat declared = new XStringFormat();
            declared.TextDirection = BidiParagraphDirection.RightToLeft;

            // A default XStringFormat is not the default DrawString uses. The overload without one
            // passes XStringFormats.Default, whose LineAlignment is BaseLine; a new XStringFormat
            // leaves LineAlignment at its zero value, Near, which measures from the top of the box
            // instead - so the y below would mean two different things on the two lines.
            declared.LineAlignment = XLineAlignment.BaseLine;

            // A digit is not a strong character but a Latin letter is, so this line's first strong
            // character is the A of ACME and Automatic resolves it left to right. That is the case
            // worth showing: with "2026" in front of the Hebrew instead, the first strong character
            // is still the Hebrew, Automatic already answers right to left, and declaring it changes
            // nothing at all.
            string branded = "ACME " + hebrew;

            gfx.DrawString("Guessed from the text, then declared right to left:", label, XBrushes.Black, 50, 500);
            gfx.DrawString(branded, sample, XBrushes.Black, 50, 527);
            gfx.DrawString(branded, sample, XBrushes.Black, 50, 557, declared);
            // docs:end declared-direction

            Note(gfx, label, body, 50, 590,
                "The name moves, and the letters do not.",
                "The first line opens with a Latin word, so the paragraph is guessed left to right and",
                "the name is laid down first, at the left. Declaring it right to left lays the same",
                "first word down at the right-hand end instead, where a Hebrew reader begins, and the",
                "Hebrew after it - reading leftwards. Neither line reverses its own characters; what",
                "changes is which end the line is built from, which is what the paragraph level says.");
        }

        // ----- page two: the letters themselves ----------------------------------------------------

        PdfPage second = document.AddPage();
        using (XGraphics gfx = XGraphics.FromPdfPage(second))
        {
            gfx.DrawString("Shaping", heading, XBrushes.Black, 50, 60);

            // Read rather than assumed, because this demo is worth running both ways: the smoke
            // test drives it with no shaper registered, and then the page says so.
            bool shaping = GlobalFontSettings.TextShaper != null;

            Note(gfx, label, body, 50, 90,
                shaping ? "A shaper is registered, so these letters join." : "No shaper is registered.",
                "An Arabic letter takes a different form according to what it sits between - initial,",
                "medial, final or isolated - and which form is which is a rule inside the font, in its",
                "GSUB table. Reading that table is what a shaper does. Install PdfPinata.HarfBuzz",
                "and set GlobalFontSettings.TextShaper and this page draws joined letters; leave it",
                "unset and the same string comes out as isolated forms, correctly ordered.");

            gfx.DrawString("The name of the script, in it:", label, XBrushes.Black, 50, 180);
            gfx.DrawString(arabicName, arabicFace, XBrushes.Black, 50, 215);

            gfx.DrawString("A greeting:", label, XBrushes.Black, 50, 260);
            gfx.DrawString(arabic, arabicFace, XBrushes.Black, 50, 295);

            // The same characters with a zero-width non-joiner between each pair, which asks the
            // face for the isolated forms. It is the comparison that makes the line above legible:
            // two rows of the same letters, differing only in shape.
            string unjoined = string.Join(From(0x200C), arabic.ToCharArray());
            gfx.DrawString("The same five letters, asked not to join:", label, XBrushes.Black, 50, 340);
            gfx.DrawString(unjoined, arabicFace, XBrushes.Gray, 50, 375);

            Note(gfx, label, body, 50, 410,
                "U+200C, the zero-width non-joiner.",
                "It is bidirectional class BN, so rule X9 removes it before the algorithm resolves",
                "anything - and it still has to reach the shaper, because it is the character that",
                "says how the letters on either side of it join. So it is inside the run and not on",
                "the page: no glyph is drawn for it, and the two rows differ only in shape.");

            gfx.DrawString("What the pieces are:", label, XBrushes.Black, 50, 495);
            Note(gfx, label, body, 50, 512, null,
                "TextItemizer cuts a string into runs of one direction and one script. ITextShaper is",
                "handed one run at a time, with its script and its language. ShapedGlyph.Cluster maps",
                "each glyph back to the characters it stands for, which is what writes /ToUnicode - so",
                "the text can still be selected and copied out of the finished page, even where one",
                "glyph swallowed two characters.");
        }

        // ----- page three: a face that has the character ------------------------------------------

        PdfPage third = document.AddPage();
        using (XGraphics gfx = XGraphics.FromPdfPage(third))
        {
            gfx.DrawString("Fallback", heading, XBrushes.Black, 50, 60);

            bool fallback = GlobalFontSettings.FontFallback != null;

            Note(gfx, label, body, 50, 90,
                fallback ? "A fallback is registered." : "No fallback is registered.",
                "Every line on this page asks for the sans face, which has not one Arabic glyph.",
                "Without a fallback each Arabic character is .notdef - an empty box - with no warning",
                "at any layer, even though a face that could draw it is loaded in the same process.",
                "GlobalFontSettings.FontFallback says which families to try instead.");

            // Note the font on every line below: the sans. Nothing here names the Arabic family.
            // docs:begin fallback
            XFont asked = new XFont(BundledFontResolver.SansFamily, 20);

            gfx.DrawString("A Latin face asked to draw Arabic:", label, XBrushes.Black, 50, 180);
            gfx.DrawString(arabic, asked, XBrushes.Black, 50, 210);

            gfx.DrawString("Mixed, in one string and one DrawString call:", label, XBrushes.Black, 50, 255);
            gfx.DrawString("Peace, " + arabic + ", shalom.", asked, XBrushes.Black, 50, 285);
            // docs:end fallback

            Note(gfx, label, body, 50, 320,
                "Two faces on one line, and both embedded in the file.",
                "The run is cut where coverage changes, each piece is drawn by the face that has the",
                "character, and the face the caller asked for is selected again at the end. A space is",
                "never given a face of its own: it carries no shape worth choosing one for, and",
                "claiming it would split a sentence into one run per word and lose the shaping across",
                "every one of the boundaries.");

            // XTextFormatter lays a paragraph into a rectangle and places each line itself, so it
            // has to be told which way the text runs for the same reason a page does.
            // docs:begin rtl-formatter
            XTextFormatter formatter = new XTextFormatter(gfx);
            formatter.TextDirection = BidiParagraphDirection.RightToLeft;
            formatter.Alignment = XParagraphAlignment.Justify;

            XRect column = new XRect(50, 430, 320, 95);
            gfx.DrawString("XTextFormatter, declared right to left and justified:",
                label, XBrushes.Black, 50, 420);
            gfx.DrawRectangle(XPens.LightGray, column);

            StringBuilder paragraph = new StringBuilder();
            for (int word = 0; word < 10; word++)
                paragraph.Append(word % 2 == 0 ? hebrew : arabic).Append(' ');

            formatter.DrawString(paragraph.ToString().Trim(),
                new XFont(BundledFontResolver.SansFamily, 13), XBrushes.Black, column);
            // docs:end rtl-formatter

            Note(gfx, label, body, 50, 550,
                "Justifying is the alignment that needed changing.",
                "Every other one hands a whole line to DrawString, which orders it. Justifying places",
                "each word itself, so it has to ask where each belongs - and order them by the",
                "leftmost position any of their characters ends up at rather than by the first,",
                "because a right-to-left word's first character is its rightmost one.");
        }

        return document;
        #endregion
    }

    /// <summary>
    ///   A string from its code points, so that every character in this file stays ASCII.
    /// </summary>
    static string From(params int[] codePoints)
    {
        StringBuilder built = new StringBuilder(codePoints.Length);
        foreach (int codePoint in codePoints)
            built.Append(char.ConvertFromUtf32(codePoint));

        return built.ToString();
    }

    /// <summary>
    ///   A bold lead-in and the lines beneath it, as a block of small text.
    /// </summary>
    /// <remarks>
    ///   Local to this file rather than shared. The source printed to the terminal is this file, so
    ///   a demo reaching for a drawing helper somewhere else is one whose printed source no longer
    ///   tells the whole story.
    /// </remarks>
    static void Note(XGraphics gfx, XFont label, XFont body, double x, double y,
        string? lead, params string[] lines)
    {
        double at = y;
        if (lead != null)
        {
            gfx.DrawString(lead, label, XBrushes.Black, x, at);
            at += 14;
        }

        foreach (string line in lines)
        {
            gfx.DrawString(line, body, XBrushes.Black, x, at);
            at += 12;
        }
    }
}
