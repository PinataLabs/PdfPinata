using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// A closed shape through any number of vertices, drawn on the page as an annotation -
/// ISO 32000-1 section 12.5.6.9. The last vertex is joined to the first.
/// </summary>
public sealed class PdfPolygonAnnotation : PdfPolyAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolygonAnnotation"/> class.
    /// </summary>
    public PdfPolygonAnnotation()
        : base("/Polygon")
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolygonAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfPolygonAnnotation(PdfDocument document)
        : base(document, "/Polygon")
    { }

    /// <summary>
    /// Wraps an annotation dictionary read from a document.
    /// </summary>
    internal PdfPolygonAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    private protected override void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XPoint[] vertices)
    {
        if (brush == null)
            gfx.DrawPolygon(pen, vertices);
        else
            gfx.DrawPolygon(pen, brush, vertices, XFillMode.Winding);
    }
}
