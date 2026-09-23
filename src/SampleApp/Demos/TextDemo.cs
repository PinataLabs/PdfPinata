using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Where a string goes, what state it is drawn under, and how it is painted.
/// </summary>
internal sealed class TextDemo : PdfDemo
{
    public TextDemo() : base() { }

    public override string Name => "Text";

    public override string Summary => "Placement, spacing, painting and links.";

    public override IReadOnlyList<string> Shows => [
        "The nine XStringFormats presets, each in the box it was given",
        "Character and word spacing, horizontal scaling, text rise and slant",
        "Fill, stroke, and fill with stroke - the PDF text rendering modes",
        "Colour from RGB, CMYK and grey, and a link over a word"
    ];

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";

        var document = new PdfDocument();
        var body = new XFont(Sans, 11);
        var note = new XFont(Sans, 8);
        var headingFont = new XFont(Sans, 9, XFontStyle.Bold);
        var boxPen = new XPen(XColors.Gainsboro, 0.5);

        // ---- Page one: where the string goes -----------------------------------------
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        void Heading(string text, double y)
        {
            gfx.DrawString(text.ToUpperInvariant(), headingFont, XBrushes.SteelBlue,
                new XPoint(48, y));
            gfx.DrawLine(XPens.LightGray, 48, y + 5, 548, y + 5);
        }

        Heading("A point, or a rectangle and a format", 56);

        // The point overload puts the baseline of the text at the point. Nothing is
        // centred, nothing is measured, and the string runs to the right of it.
        // docs:begin point-and-rect
        gfx.DrawLine(XPens.Crimson, 48, 90, 300, 90);
        gfx.DrawString("Drawn at a point: this is the baseline", body, XBrushes.Black,
            new XPoint(48, 90));

        // The rectangle overload places the string inside the box according to the
        // format. The box itself is never drawn by the library.
        var box = new XRect(320, 76, 228, 28);
        gfx.DrawRectangle(boxPen, box);
        gfx.DrawString("Centred in a rectangle", body, XBrushes.Black, box,
            XStringFormats.Center);
        // docs:end point-and-rect

        Heading("The nine presets", 124);

        // Every combination of near, centre and far in both directions. Each is drawn in
        // the same rectangle so it is the format alone that moves the words.
        // docs:begin presets
        (string Name, XStringFormat Format)[] presets =
        [
            ("TopLeft", XStringFormats.TopLeft),
            ("TopCenter", XStringFormats.TopCenter),
            ("TopRight", XStringFormats.TopRight),
            ("CenterLeft", XStringFormats.CenterLeft),
            ("Center", XStringFormats.Center),
            ("CenterRight", XStringFormats.CenterRight),
            ("BottomLeft", XStringFormats.BottomLeft),
            ("BottomCenter", XStringFormats.BottomCenter),
            ("BottomRight", XStringFormats.BottomRight)
        ];

        for (var index = 0; index < presets.Length; index++)
        {
            // ReSharper disable once PossibleLossOfFraction
            var cell = new XRect(48 + index % 3 * 172, 144 + index / 3 * 72, 160, 60);
            gfx.DrawRectangle(boxPen, cell);
            gfx.DrawString(presets[index].Name, body, XBrushes.Black, cell,
                presets[index].Format);
        }
        // docs:end presets

        Heading("Measuring, and the baseline", 372);

        // MeasureString answers in the units the page is drawn in, so a rule of exactly
        // the width of the text can be drawn under it.
        // docs:begin measure
        const string measured = "MeasureString gives this rule its length";
        var size = gfx.MeasureString(measured, body);
        gfx.DrawString(measured, body, XBrushes.Black, new XPoint(48, 400));
        gfx.DrawLine(new XPen(XColors.SteelBlue, 1), 48, 404, 48 + size.Width, 404);
        gfx.DrawString($"{size.Width:0.#} x {size.Height:0.#} points", note,
            XBrushes.DimGray, new XPoint(48 + size.Width + 10, 400));
        // docs:end measure

        // XLineAlignment.BaseLine puts the baseline of the text on the top edge of the
        // rectangle rather than fitting the text inside it, which is the right choice
        // when the position that matters is the line the text sits on. The rectangle's
        // height must be exactly 0 - there is nothing for the text to be aligned within,
        // and passing a height throws rather than quietly ignoring it.
        // docs:begin baseline
        gfx.DrawLine(XPens.Crimson, 48, 440, 300, 440);
        gfx.DrawString("BaseLine sits on the rule", body, XBrushes.Black,
            new XRect(48, 440, 252, 0),
            new XStringFormat { LineAlignment = XLineAlignment.BaseLine });
        // docs:end baseline

        Heading("DrawString does not wrap", 480);

        // DrawString draws one line. A newline is not a line break here and is not drawn
        // either - it is dropped, the way MeasureString has always dropped it, so the two
        // words either side of it run together rather than being separated by the box the
        // font draws for a character it has no glyph for. A tab is drawn as the single
        // space it measures as. Wrapping and breaking are XTextFormatter's job - see the
        // Layout demo.
        // docs:begin no-wrap
        gfx.DrawString("A newline\nvanishes between these words, a tab\tis the space it "
            + "measures as, and a long line runs off the edge of the page rather than "
            + "wrapping", body, XBrushes.Black,
            new XPoint(48, 510));
        // docs:end no-wrap

        // ---- Page two: the state a string is drawn under -------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);

        Heading("Spacing and scaling", 56);

        var sample = new XFont(Sans, 13);
        double y = 84;

        void Row(string label, XStringFormat format)
        {
            gfx.DrawString(label, note, XBrushes.DimGray, new XPoint(48, y));
            gfx.DrawString("Handgloves and quartz", sample, XBrushes.Black,
                new XRect(190, y - 12, 360, 20), format);
            y += 30;
        }

        // docs:begin spacing
        // Tc in the content stream: extra space after every glyph, negative to tighten.
        foreach (var spacing in new[] { -0.4, 0.0, 2.0 })
            Row($"CharacterSpacing = {spacing}", new XStringFormat { CharacterSpacing = spacing });

        y += 8;

        // Tw: extra space after every space character only, so it stretches the gaps
        // between words and leaves the words themselves alone.
        foreach (var spacing in new[] { 0.0, 4.0, 10.0 })
            Row($"WordSpacing = {spacing}", new XStringFormat { WordSpacing = spacing });

        y += 8;

        // Tz: the glyphs are stretched or squeezed horizontally, a percentage of normal.
        foreach (var scale in new[] { 60.0, 100.0, 150.0 })
            Row($"HorizontalScaling = {scale}", new XStringFormat { HorizontalScaling = scale });

        y += 8;

        // A skew rather than an italic: the upright glyphs are slanted, where a real italic
        // is a different set of shapes. The Fonts demo sets the two side by side.
        Row("ObliqueAngle = 12", new XStringFormat { ObliqueAngle = 12 });
        // docs:end spacing

        // Ts: the baseline moves up or down without changing the size of the glyphs, so a
        // superscript needs the rise and a smaller font together.
        Heading("Text rise", y + 6);
        y += 34;

        // docs:begin text-rise
        var small = new XFont(Sans, 8);
        double x = 48;
        gfx.DrawString("H", sample, XBrushes.Black, new XPoint(x, y));
        x += gfx.MeasureString("H", sample).Width;
        gfx.DrawString("2", small, XBrushes.Black, new XRect(x, y, 20, 0),
            new XStringFormat { TextRise = -3, LineAlignment = XLineAlignment.BaseLine });
        x += gfx.MeasureString("2", small).Width;
        gfx.DrawString("SO", sample, XBrushes.Black, new XPoint(x, y));
        x += gfx.MeasureString("SO", sample).Width;
        gfx.DrawString("4", small, XBrushes.Black, new XRect(x, y, 20, 0),
            new XStringFormat { TextRise = -3, LineAlignment = XLineAlignment.BaseLine });
        x += gfx.MeasureString("4", small).Width + 24;

        gfx.DrawString("x", sample, XBrushes.Black, new XPoint(x, y));
        x += gfx.MeasureString("x", sample).Width;
        gfx.DrawString("2", small, XBrushes.Black, new XRect(x, y, 20, 0),
            new XStringFormat { TextRise = 5, LineAlignment = XLineAlignment.BaseLine });
        // docs:end text-rise

        gfx.DrawString("a negative rise for the subscript, a positive one for the power",
            note, XBrushes.DimGray, new XPoint(48, y + 22));

        // ---- Page three: paint, colour and links ---------------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);

        Heading("Fill, stroke, or both", 56);

        // There is no rendering-mode property. Which of the brush and the pen is given
        // decides it: brush alone fills (Tr 0), pen alone strokes the outline (Tr 1),
        // and both together fills and then strokes (Tr 2).
        // docs:begin fill-stroke
        var display = new XFont(Sans, 40, XFontStyle.Bold);
        var outline = new XPen(XColors.Crimson, 0.8);

        gfx.DrawString("Filled", display, XBrushes.Black, new XPoint(48, 120));
        gfx.DrawString("Stroked", display, outline, XBrushes.Transparent, new XPoint(48, 172));
        gfx.DrawString("Both", display, outline, new XSolidBrush(XColors.Wheat),
            new XPoint(48, 224));
        // docs:end fill-stroke

        gfx.DrawString("brush only", note, XBrushes.DimGray, new XPoint(300, 116));
        gfx.DrawString("pen only", note, XBrushes.DimGray, new XPoint(300, 168));
        gfx.DrawString("pen and brush", note, XBrushes.DimGray, new XPoint(300, 220));

        Heading("Colour", 260);

        (string Label, XColor Colour)[] colours =
        [
            ("XColor.FromArgb(220, 60, 60)", XColor.FromArgb(220, 60, 60)),
            ("XColor.FromArgb(90, 0, 0, 255) - alpha", XColor.FromArgb(90, 0, 0, 255)),
            ("XColor.FromCmyk(0.8, 0, 0.4, 0.1)", XColor.FromCmyk(0.8, 0, 0.4, 0.1)),
            ("XColor.FromGrayScale(0.45)", XColor.FromGrayScale(0.45))
        ];

        y = 288;
        foreach ((var label, var colour) in colours)
        {
            // A tint behind the alpha row, so that the transparency has something to
            // show through.
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 245, 220)), 44, y - 13, 240, 20);
            gfx.DrawString(label, new XFont(Sans, 13), new XSolidBrush(colour),
                new XPoint(48, y));
            y += 26;
        }

        Heading("Links", y + 6);
        y += 34;

        // AddWebLink takes a rectangle in the same coordinates the drawing uses. The
        // library draws nothing - the blue and the underline are the caller's job, and
        // without them the link is invisible.
        // docs:begin links
        const string linkText = "The PdfPinata repository";
        var linkFont = new XFont(Sans, 12, XFontStyle.Underline);
        var linkSize = gfx.MeasureString(linkText, linkFont);
        gfx.DrawString(linkText, linkFont, XBrushes.MediumBlue, new XPoint(48, y));
        gfx.AddWebLink(new XRect(48, y - linkSize.Height + 3, linkSize.Width, linkSize.Height),
            "https://github.com/PinataLabs/PdfPinata");

        y += 26;

        // A destination is a named place in this document. A named link goes to it
        // without knowing which page it ended up on, which is what keeps it correct
        // after the pages have been moved or the document resized.
        gfx.AddNamedDestination("colours", new XPoint(48, 260));
        const string namedText = "Back up to the colours on this page";
        var namedSize = gfx.MeasureString(namedText, linkFont);
        gfx.DrawString(namedText, linkFont, XBrushes.MediumBlue, new XPoint(48, y));
        gfx.AddNamedLink(new XRect(48, y - namedSize.Height + 3, namedSize.Width, namedSize.Height),
            "colours");

        y += 26;

        const string pageText = "To page one";
        var pageSize = gfx.MeasureString(pageText, linkFont);
        gfx.DrawString(pageText, linkFont, XBrushes.MediumBlue, new XPoint(48, y));
        gfx.AddDocumentLink(new XRect(48, y - pageSize.Height + 3, pageSize.Width, pageSize.Height), 1);
        // docs:end links
        #endregion

        return document;
    }
}
