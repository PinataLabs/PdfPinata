using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// A rectangle drawn on the page as an annotation rather than as page content - PDFKit's
/// <c>rectAnnotation</c>, and ISO 32000-1 section 12.5.6.8.
/// </summary>
/// <remarks>
/// <para>
/// Being an annotation rather than a rectangle drawn onto the page, it can be hidden, printed or
/// not printed, moved, given a tooltip through <see cref="PdfAnnotation.Contents"/>, and edited by
/// a reader - none of which <c>XGraphics.DrawRectangle</c> can offer, because that is ink.
/// </para>
/// <para>
/// Everything but the shape is <see cref="PdfSquareCircleAnnotation"/>, which is where the
/// interior colour, the border and the appearance stream live.
/// </para>
/// </remarks>
public sealed class PdfSquareAnnotation : PdfSquareCircleAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareAnnotation"/> class.
    /// </summary>
    public PdfSquareAnnotation()
        : base("/Square")
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfSquareAnnotation(PdfDocument document)
        : base(document, "/Square")
    { }

    /// <inheritdoc/>
    protected override void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XRect box)
    {
        if (pen == null)
            gfx.DrawRectangle(brush, box);
        else if (brush == null)
            gfx.DrawRectangle(pen, box);
        else
            gfx.DrawRectangle(pen, brush, box);
    }
}
