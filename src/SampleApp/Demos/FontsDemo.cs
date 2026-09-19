using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The three families the app carries, the four styles of each, a size ramp, and the six ways
///   a line can be drawn under or through a word.
/// </summary>
internal sealed class FontsDemo : PdfDemo
{
    public FontsDemo() : base() { }

    public override string Name => "Fonts";

    public override string Summary => "Families, weights, slants, sizes and decorations.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Three families: a sans, a serif and a monospace",
        "Real bold and italic beside simulated ones",
        "A size ramp placed by MeasureString rather than by a fixed step",
        "The six XTextDecoration line styles, and a decoration in its own colour",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";
        const string Serif = "Liberation Serif";
        const string Mono = "Source Code Pro";

        PdfDocument document = new PdfDocument();

        // ---- Page one: families and styles -------------------------------------------
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        XFont heading = new XFont(Sans, 9, XFontStyle.Bold);
        XFont note = new XFont(Sans, 8);
        double y = 60;

        void Heading(string text)
        {
            gfx.DrawString(text.ToUpperInvariant(), heading, XBrushes.SteelBlue,
                new XPoint(56, y));
            gfx.DrawLine(XPens.LightGray, 56, y + 5, 540, y + 5);
            y += 22;
        }

        Heading("Families and styles");

        // docs:begin families-and-styles
        (string Family, string Label)[] families =
        {
            (Sans, "Liberation Sans - TrueType outlines, the metrics of Arial"),
            (Serif, "Liberation Serif - TrueType outlines, the metrics of Times New Roman"),
            (Mono, "Source Code Pro - PostScript (CFF) outlines, regular face only"),
        };

        (XFontStyle Style, string Label)[] styles =
        {
            (XFontStyle.Regular, "Regular"),
            (XFontStyle.Bold, "Bold"),
            (XFontStyle.Italic, "Italic"),
            (XFontStyle.BoldItalic, "Bold italic"),
        };

        foreach ((string family, string label) in families)
        {
            gfx.DrawString(label, note, XBrushes.DimGray, new XPoint(56, y));
            y += 16;

            foreach ((XFontStyle style, string styleLabel) in styles)
            {
                gfx.DrawString($"{styleLabel} - Sphinx of black quartz, judge my vow",
                    new XFont(family, 13, style), XBrushes.Black, new XPoint(70, y));
                y += 19;
            }

            y += 10;
        }
        // docs:end families-and-styles

        // Only a regular face of Source Code Pro is carried, so the bold and italic above
        // are not designed faces at all: the resolver answers with XStyleSimulations and
        // the library strokes the outline to fake weight and skews it to fake slant. Set
        // the two rows side by side and the difference is plain - simulated bold is
        // uniformly fatter, where a drawn bold face redistributes weight around the letter.
        Heading("Simulated against designed");

        // docs:begin simulated
        gfx.DrawString("Source Code Pro bold is stroked:", note, XBrushes.DimGray,
            new XPoint(56, y));
        gfx.DrawString("Handgloves 123", new XFont(Mono, 20, XFontStyle.Bold),
            XBrushes.Black, new XPoint(250, y + 2));
        y += 26;

        gfx.DrawString("Liberation Sans bold is drawn:", note, XBrushes.DimGray,
            new XPoint(56, y));
        gfx.DrawString("Handgloves 123", new XFont(Sans, 20, XFontStyle.Bold),
            XBrushes.Black, new XPoint(250, y + 2));
        y += 34;
        // docs:end simulated

        // A family nothing here carries. The resolver answers it rather than failing, so a
        // document written against fonts that are not present still lays out identically
        // everywhere instead of falling back to whatever the machine has.
        Heading("A family that is not carried");
        gfx.DrawString("new XFont(\"Comic Sans MS\", 13) resolves to the sans:", note,
            XBrushes.DimGray, new XPoint(56, y));
        gfx.DrawString("and is drawn like this", new XFont("Comic Sans MS", 13),
            XBrushes.Black, new XPoint(320, y));

        // ---- Page two: sizes and decorations -----------------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);
        y = 60;

        Heading("A size ramp");

        // Each line is placed from the height of the one before it rather than by a fixed
        // step, so the gaps stay right as the size changes. MeasureString answers in the
        // same units the page is drawn in. The size label goes on the left, where a long
        // word set large cannot grow into it.
        // docs:begin size-ramp
        foreach (double size in new[] { 6.0, 8, 10, 12, 16, 21, 28, 38 })
        {
            XFont font = new XFont(Serif, size);
            XSize measured = gfx.MeasureString("Handgloves", font);

            gfx.DrawString($"{size:0}pt", note, XBrushes.LightSlateGray,
                new XPoint(56, y + measured.Height));
            gfx.DrawString("Handgloves", font, XBrushes.Black,
                new XPoint(96, y + measured.Height));

            y += measured.Height + 4;
        }
        // docs:end size-ramp

        y += 28;
        Heading("Decorations");

        // XFontStyle carries underline and strikeout, which is the older way and gives no
        // say over how the line is drawn.
        // docs:begin font-style-decorations
        gfx.DrawString("XFontStyle.Underline", new XFont(Sans, 12, XFontStyle.Underline),
            XBrushes.Black, new XPoint(70, y));
        gfx.DrawString("XFontStyle.Strikeout", new XFont(Sans, 12, XFontStyle.Strikeout),
            XBrushes.Black, new XPoint(260, y));
        y += 26;
        // docs:end font-style-decorations

        // XStringFormat carries the same two as XTextDecoration, which chooses the pattern
        // and lets the line take a colour of its own - the one thing a caller cannot do by
        // drawing the rule by hand afterwards, since it would have to measure the text.
        // docs:begin string-format-decorations
        XFont body = new XFont(Sans, 12);
        foreach (XTextDecoration decoration in new[]
                 {
                     XTextDecoration.Single, XTextDecoration.Words, XTextDecoration.Dotted,
                     XTextDecoration.Dash, XTextDecoration.DotDash, XTextDecoration.DotDotDash,
                 })
        {
            XStringFormat format = new XStringFormat
            {
                Underline = decoration,
                DecorationColor = XColors.Crimson,
            };

            gfx.DrawString($"XTextDecoration.{decoration} underlines these words", body,
                XBrushes.Black, new XRect(70, y, 400, 18), format);
            y += 24;
        }

        gfx.DrawString("and a strikeout, dashed, in the colour of the text", body,
            XBrushes.Black, new XRect(70, y, 400, 18),
            new XStringFormat { Strikeout = XTextDecoration.Dash });
        // docs:end string-format-decorations
        #endregion

        return document;
    }
}
