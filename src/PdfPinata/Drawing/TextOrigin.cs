namespace PdfPinata.Drawing;

/// <summary>
/// Where the baseline of a string starts, given a layout rectangle and a string format.
/// </summary>
/// <remarks>
/// One copy, called both by <see cref="PdfPinata.Drawing.Pdf.XGraphicsPdfRenderer"/> when it
/// draws a string and by <see cref="XGraphicsPath"/> when it adds one to a path. Two copies would
/// drift, and text added to a path would then land somewhere other than the same text drawn.
/// </remarks>
internal static class TextOrigin
{
    /// <summary>
    /// The point the first glyph's baseline starts at.
    /// </summary>
    /// <param name="rect">The layout rectangle. Its height is read only by the alignments that
    /// measure against it, which is why a <see cref="XLineAlignment.BaseLine"/> format works with
    /// a rectangle of any height.</param>
    /// <param name="textWidth">The measured width of the text, through the same format.</param>
    /// <param name="font">The font the text is set in.</param>
    /// <param name="format">The alignment to honour.</param>
    /// <param name="downwards">
    /// Whether y increases down the page, which is <see cref="XPageDirection.Downwards"/> and the
    /// default. Everything below is mirrored for a page measured the other way.
    /// </param>
    internal static XPoint For(XRect rect, double textWidth, XFont font, XStringFormat format, bool downwards)
    {
        var x = AlignedX(rect, textWidth, format.Alignment);

        var metrics = new VerticalMetrics(font);
        var y = downwards
            ? BaselineDownwards(rect, metrics, format.LineAlignment)
            : BaselineUpwards(rect, metrics, format.LineAlignment);

        return new XPoint(x, y);
    }

    private static double AlignedX(XRect rect, double textWidth, XStringAlignment alignment)
    {
        var x = rect.X;

        switch (alignment)
        {
            case XStringAlignment.Near:
                // nothing to do
                break;

            case XStringAlignment.Center:
                x += (rect.Width - textWidth) / 2;
                break;

            case XStringAlignment.Far:
                x += rect.Width - textWidth;
                break;
        }

        return x;
    }

    /// <summary>
    /// Where the baseline sits on a page whose y increases downwards.
    /// </summary>
    private static double BaselineDownwards(XRect rect, VerticalMetrics metrics, XLineAlignment alignment)
    {
        var y = rect.Y;

        switch (alignment)
        {
            case XLineAlignment.Near:
                y += metrics.Ascent;
                break;

            case XLineAlignment.Center:
                // Three quarters of the ascent stands in for the cap height, as it does in PDFlib.
                // Deliberately: the font's own CapHeight would move every vertically centred string.
                y += metrics.Ascent * 3 / 4 / 2 + rect.Height / 2;
                break;

            case XLineAlignment.Far:
                y += -metrics.Descent + rect.Height;
                break;

            case XLineAlignment.BaseLine:
                // Nothing to do. The baseline is the top edge, and the height is unread -
                // which is why a height is surplus information rather than a contradiction.
                break;

            case XLineAlignment.Hanging:
                // As Near, but hung off the position rather than off the top of the rectangle.
                y += metrics.Ascent;
                break;

            case XLineAlignment.Ideographic:
                y += -metrics.Descent;
                break;

            case XLineAlignment.SvgMiddle:
                y += metrics.XHeight / 2;
                break;
        }

        return y;
    }

    /// <summary>
    /// Where the baseline sits on a page whose y increases upwards: <see cref="BaselineDownwards"/>
    /// mirrored.
    /// </summary>
    private static double BaselineUpwards(XRect rect, VerticalMetrics metrics, XLineAlignment alignment)
    {
        var y = rect.Y;

        switch (alignment)
        {
            case XLineAlignment.Near:
                y += metrics.Descent;
                break;

            case XLineAlignment.Center:
                // Three quarters of the ascent, deliberately, as above.
                y += -(metrics.Ascent * 3 / 4) / 2 + rect.Height / 2;
                break;

            case XLineAlignment.Far:
                y += -metrics.Ascent + rect.Height;
                break;

            case XLineAlignment.BaseLine:
                // Nothing to do.
                break;

            case XLineAlignment.Hanging:
                y += -metrics.Ascent;
                break;

            case XLineAlignment.Ideographic:
                y += metrics.Descent;
                break;

            case XLineAlignment.SvgMiddle:
                y += -metrics.XHeight / 2;
                break;
        }

        return y;
    }

    /// <summary>
    /// The font's ascent, descent and x-height, scaled to its line spacing.
    /// </summary>
    private readonly struct VerticalMetrics
    {
        internal VerticalMetrics(XFont font)
        {
            var lineSpace = font.GetHeight();
            Ascent = lineSpace * font.CellAscent / font.CellSpace;
            Descent = lineSpace * font.CellDescent / font.CellSpace;
            // Half the height of a lowercase x, for the one alignment that is measured against it.
            XHeight = lineSpace * font.Metrics.XHeight / font.CellSpace;
        }

        internal double Ascent { get; }
        internal double Descent { get; }
        internal double XHeight { get; }
    }
}
