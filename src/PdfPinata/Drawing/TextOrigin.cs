namespace PdfPinata.Drawing;

/// <summary>
/// Where the baseline of a string starts, given a layout rectangle and a string format.
/// </summary>
/// <remarks>
/// One copy, called both by <see cref="PdfPinata.Drawing.Pdf.XGraphicsPdfRenderer"/> when it
/// draws a string and by <see cref="XGraphicsPath"/> when it adds one to a path. Two copies would
/// drift, and text added to a path would then land somewhere other than the same text drawn.
/// </remarks>
static class TextOrigin
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
        double x = rect.X;
        double y = rect.Y;

        switch (format.Alignment)
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

        double lineSpace = font.GetHeight();
        double cyAscent = lineSpace * font.CellAscent / font.CellSpace;
        double cyDescent = lineSpace * font.CellDescent / font.CellSpace;
        // Half the height of a lowercase x, for the one alignment that is measured against it.
        double cyXHeight = lineSpace * font.Metrics.XHeight / font.CellSpace;

        if (downwards)
        {
            switch (format.LineAlignment)
            {
                case XLineAlignment.Near:
                    y += cyAscent;
                    break;

                case XLineAlignment.Center:
                    // TODO: Use CapHeight. PDFlib also uses 3/4 of ascent
                    y += (cyAscent * 3 / 4) / 2 + rect.Height / 2;
                    break;

                case XLineAlignment.Far:
                    y += -cyDescent + rect.Height;
                    break;

                case XLineAlignment.BaseLine:
                    // Nothing to do. The baseline is the top edge, and the height is unread -
                    // which is why a height is surplus information rather than a contradiction.
                    break;

                case XLineAlignment.Hanging:
                    // As Near, but hung off the position rather than off the top of the rectangle.
                    y += cyAscent;
                    break;

                case XLineAlignment.Ideographic:
                    y += -cyDescent;
                    break;

                case XLineAlignment.SvgMiddle:
                    y += cyXHeight / 2;
                    break;
            }
        }
        else
        {
            switch (format.LineAlignment)
            {
                case XLineAlignment.Near:
                    y += cyDescent;
                    break;

                case XLineAlignment.Center:
                    // TODO: Use CapHeight. PDFlib also uses 3/4 of ascent
                    y += -(cyAscent * 3 / 4) / 2 + rect.Height / 2;
                    break;

                case XLineAlignment.Far:
                    y += -cyAscent + rect.Height;
                    break;

                case XLineAlignment.BaseLine:
                    // Nothing to do.
                    break;

                case XLineAlignment.Hanging:
                    y += -cyAscent;
                    break;

                case XLineAlignment.Ideographic:
                    y += cyDescent;
                    break;

                case XLineAlignment.SvgMiddle:
                    y += -cyXHeight / 2;
                    break;
            }
        }

        return new XPoint(x, y);
    }
}
