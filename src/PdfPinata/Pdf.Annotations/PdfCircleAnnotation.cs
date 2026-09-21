using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// An ellipse drawn on the page as an annotation rather than as page content - PDFKit's
/// <c>ellipseAnnotation</c>, and ISO 32000-1 section 12.5.6.8.
/// </summary>
/// <remarks>
/// <para>
/// The specification calls this subtype <c>/Circle</c> and then says it is an ellipse: the shape
/// is inscribed in <see cref="PdfAnnotation.Rectangle"/>, so it is only a circle when that
/// rectangle is square. The name is kept because it is the name in the file.
/// </para>
/// <para>
/// Everything but the shape is <see cref="PdfSquareCircleAnnotation"/>, which is where the
/// interior colour, the border and the appearance stream live.
/// </para>
/// </remarks>
public sealed class PdfCircleAnnotation : PdfSquareCircleAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCircleAnnotation"/> class.
    /// </summary>
    public PdfCircleAnnotation()
        : base("/Circle")
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCircleAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfCircleAnnotation(PdfDocument document)
        : base(document, "/Circle")
    { }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfCircleAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <inheritdoc/>
    protected override void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XRect box)
    {
        if (pen == null)
            gfx.DrawEllipse(brush, box);
        else if (brush == null)
            gfx.DrawEllipse(pen, box);
        else
            gfx.DrawEllipse(pen, brush, box);
    }
}
