using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Flowing text into rectangles with <see cref="XTextFormatter"/>: wrapping, alignment, columns,
///   truncation, and measuring a box before anything is drawn in it.
/// </summary>
internal sealed class LayoutDemo : PdfDemo
{
    public LayoutDemo() : base() { }

    public override string Name => "Layout";

    public override string Summary => "Wrapping, alignment, columns, lists and truncation.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "The four paragraph alignments, justified among them",
        "Columns and the gutter between them",
        "Ellipsis truncation when the text will not fit the box",
        "Lists built by hand, because there is no list API on this side",
        "GetLayout measuring the box the text will need",
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";
        const string Serif = "Liberation Serif";

        const string Paragraph =
            "The quick brown fox jumps over the lazy dog, and does so repeatedly until "
            + "there is enough text here to wrap onto several lines and show what the "
            + "formatter does with the space left at the end of each of them.";

        PdfDocument document = new PdfDocument();
        XFont body = new XFont(Serif, 10);
        XFont note = new XFont(Sans, 8);
        XFont headingFont = new XFont(Sans, 9, XFontStyle.Bold);
        XPen boxPen = new XPen(XColors.Gainsboro, 0.5);

        // ---- Page one: wrapping, alignment and truncation ------------------------------
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);
        XTextFormatter formatter = new XTextFormatter(gfx);

        // Headings take a left edge and a width, so that one over a right hand column does
        // not rule a line straight through the column beside it.
        void Heading(string text, double y, double x = 48, double width = 500)
        {
            gfx.DrawString(text.ToUpperInvariant(), headingFont, XBrushes.SteelBlue,
                new XPoint(x, y));
            gfx.DrawLine(XPens.LightGray, x, y + 5, x + width, y + 5);
        }

        Heading("The four alignments", 56, 48, 240);

        (XParagraphAlignment Alignment, string Label)[] alignments =
        {
            (XParagraphAlignment.Left, "Left - ragged on the right"),
            (XParagraphAlignment.Center, "Center - ragged on both"),
            (XParagraphAlignment.Right, "Right - ragged on the left"),
            (XParagraphAlignment.Justify, "Justify - flush both sides, last line left"),
        };

        double y = 78;
        foreach ((XParagraphAlignment alignment, string label) in alignments)
        {
            gfx.DrawString(label, note, XBrushes.DimGray, new XPoint(48, y));

            XRect rect = new XRect(48, y + 6, 240, 62);
            gfx.DrawRectangle(boxPen, rect);
            formatter.Alignment = alignment;
            formatter.DrawString(Paragraph, body, XBrushes.Black, rect);

            y += 84;
        }

        formatter.Alignment = XParagraphAlignment.Left;

        Heading("When it will not fit", 56, 320, 228);

        // Vertical overflow is off by default, so a box too short for its text simply
        // loses the rest. Setting Ellipsis marks where the loss happened instead of
        // letting the text stop mid-sentence as though it had finished.
        XRect tooShort = new XRect(320, 78, 228, 34);
        gfx.DrawRectangle(boxPen, tooShort);
        formatter.Ellipsis = XTextFormatter.DefaultEllipsis;
        formatter.DrawString(Paragraph, body, XBrushes.Black, tooShort);
        formatter.Ellipsis = null;
        gfx.DrawString("Ellipsis marks what was cut", note, XBrushes.DimGray,
            new XPoint(320, 126));

        // With LineBreak off nothing wraps: the text runs straight out of the box and off
        // the page. The line breaks written into the string are still obeyed.
        XRect noWrap = new XRect(320, 150, 228, 30);
        gfx.DrawRectangle(boxPen, noWrap);
        formatter.LineBreak = false;
        formatter.DrawString("LineBreak = false runs on past the right edge", body,
            XBrushes.Black, noWrap);
        formatter.LineBreak = true;

        // ---- Page two: columns, indents and gaps ---------------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);
        formatter = new XTextFormatter(gfx);

        Heading("Columns", 56);

        // The rectangle is divided into Columns of equal width with ColumnGap between
        // them, and the text fills each in turn before moving to the next.
        XRect columns = new XRect(48, 78, 500, 180);
        gfx.DrawRectangle(boxPen, columns);
        formatter.Columns = 3;
        formatter.ColumnGap = 16;
        formatter.Alignment = XParagraphAlignment.Justify;
        formatter.DrawString(
            string.Concat(Paragraph, " ", Paragraph, " ", Paragraph, " ", Paragraph, " ",
                Paragraph, " ", Paragraph),
            body, XBrushes.Black, columns);
        formatter.Columns = 1;
        formatter.Alignment = XParagraphAlignment.Left;

        gfx.DrawString("Columns = 3, ColumnGap = 16, justified", note, XBrushes.DimGray,
            new XPoint(48, 272));

        Heading("Indents and gaps", 296);

        string twoParagraphs = Paragraph + "\n" + Paragraph;

        XRect plain = new XRect(48, 318, 240, 130);
        gfx.DrawRectangle(boxPen, plain);
        formatter.DrawString(twoParagraphs, body, XBrushes.Black, plain);
        gfx.DrawString("as it comes", note, XBrushes.DimGray, new XPoint(48, 460));

        // Indent moves the first line of each paragraph, ParagraphGap opens the space
        // between one paragraph and the next, LineGap the space between every line.
        XRect indented = new XRect(308, 318, 240, 130);
        gfx.DrawRectangle(boxPen, indented);
        formatter.Indent = 14;
        formatter.ParagraphGap = 6;
        formatter.LineGap = 1.5;
        formatter.DrawString(twoParagraphs, body, XBrushes.Black, indented);
        formatter.Indent = 0;
        formatter.ParagraphGap = 0;
        formatter.LineGap = 0;
        gfx.DrawString("Indent 14, ParagraphGap 6, LineGap 1.5", note, XBrushes.DimGray,
            new XPoint(308, 460));

        Heading("Lists, by hand", 486);

        // There is no list support on this side of the library - MigraDoc has ListInfo,
        // and the Tables and Invoice demos use it. Here the marker is drawn separately
        // and the text flows into a rectangle inset by the width of the marker, which is
        // the whole of what a hanging indent is.
        string[] items =
        {
            "A marker drawn at the left of the line",
            "The text flowed into a rectangle that starts after it, so the second and "
                + "later lines of a long item line up under the first rather than under "
                + "the marker",
            "Which is all a hanging indent is",
        };

        y = 506;
        for (int index = 0; index < items.Length; index++)
        {
            gfx.DrawString($"{index + 1}.", body, XBrushes.Black, new XPoint(48, y + 8));

            XRect itemRect = new XRect(68, y, 480, 40);
            formatter.DrawString(items[index], body, XBrushes.Black, itemRect);

            // Measure the item to find where the next one starts, rather than assuming
            // every item is one line.
            y += formatter.GetLayout(items[index], body, XBrushes.Black, itemRect).Height + 4;
        }

        // ---- Page three: measuring, vertical alignment and rotation ---------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);
        formatter = new XTextFormatter(gfx);

        Heading("Vertical alignment", 56);

        foreach ((XVerticalAlignment alignment, int column) in new[]
                 {
                     (XVerticalAlignment.Top, 0),
                     (XVerticalAlignment.Middle, 1),
                     (XVerticalAlignment.Bottom, 2),
                 })
        {
            XRect rect = new XRect(48 + column * 172, 78, 160, 110);
            gfx.DrawRectangle(boxPen, rect);
            formatter.DrawString($"{alignment} in a box taller than the text needs", body,
                XBrushes.Black, rect,
                new TextFormatAlignment { Horizontal = XParagraphAlignment.Left, Vertical = alignment });
        }

        Heading("Measuring before drawing", 212);

        // GetLayout answers "how much room would this need" without drawing anything, so
        // a box can be sized to its text rather than the text squeezed into a guess.
        // Vertical overflow is allowed while measuring - the point is to find out how tall
        // it wants to be - and turned back off before anything is drawn.
        const string toMeasure =
            "Text to determine the size of the box I would like to place the text in";

        formatter.AllowVerticalOverflow = true;
        XRect measured = formatter.GetLayout(toMeasure, body, XBrushes.Black,
            new XRect(0, 0, 200, 200));
        formatter.AllowVerticalOverflow = false;

        measured.Location = new XPoint(48, 234);

        // A wash over the box, so it is visible that the text really does fit the space
        // that was measured for it.
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(20, 0, 0, 0)), measured);
        formatter.DrawString(toMeasure, body, XBrushes.Black, measured);

        gfx.DrawString($"GetLayout returned {measured.Width:0.#} x {measured.Height:0.#} points",
            note, XBrushes.DimGray, new XPoint(48, 234 + measured.Height + 14));

        Heading("Turned", 330);

        // The rectangle is still given in page coordinates, and Rotation turns the text
        // within it about the rectangle's top left corner, anticlockwise for a positive
        // angle. So the text of a box turned 90 degrees runs upwards from that corner and
        // out of the rectangle entirely - the corner is the anchor, not the box.
        double[] rotations = { 0.0, 15.0, 45.0, 90.0 };
        for (int index = 0; index < rotations.Length; index++)
        {
            double left = 90 + index * 130;
            XRect rect = new XRect(left, 560, 130, 60);

            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 245, 220)), left - 2, 558, 4, 4);

            formatter.Rotation = rotations[index];
            formatter.DrawString("Text turned about the corner", body, XBrushes.Black, rect);
            formatter.Rotation = 0;

            gfx.DrawString($"{rotations[index]:0}°", note, XBrushes.DimGray,
                new XPoint(left, 640));
        }

        gfx.DrawString("The mark shows the corner each block is turned about.", note,
            XBrushes.DimGray, new XPoint(90, 656));
        #endregion

        return document;
    }
}
