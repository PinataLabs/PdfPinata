using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// An open run of straight segments through any number of vertices, drawn on the page as an
/// annotation - ISO 32000-1 section 12.5.6.9 - with an ending at each end, as a
/// <see cref="PdfLineAnnotation"/> has.
/// </summary>
public sealed class PdfPolyLineAnnotation : PdfPolyAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyLineAnnotation"/> class.
    /// </summary>
    public PdfPolyLineAnnotation()
        : base("/PolyLine")
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyLineAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfPolyLineAnnotation(PdfDocument document)
        : base(document, "/PolyLine")
    { }

    /// <summary>
    /// Wraps an annotation dictionary read from a document.
    /// </summary>
    internal PdfPolyLineAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// What is drawn at the first vertex.
    /// </summary>
    public PdfLineEnding StartEnding
    {
        get => LineEndings.Read(Elements.GetArray(Keys.LE), 0);
        set
        {
            Elements[Keys.LE] = LineEndings.Write(Owner, value, EndEnding);
            Touch();
        }
    }

    /// <summary>
    /// What is drawn at the last vertex.
    /// </summary>
    public PdfLineEnding EndEnding
    {
        get => LineEndings.Read(Elements.GetArray(Keys.LE), 1);
        set
        {
            Elements[Keys.LE] = LineEndings.Write(Owner, StartEnding, value);
            Touch();
        }
    }

    private protected override double Reach(double width) =>
        width / 2 + (StartEnding != PdfLineEnding.None || EndEnding != PdfLineEnding.None
            ? LineEndings.Size(width)
            : 0);

    private protected override void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XPoint[] vertices)
    {
        gfx.DrawLines(pen, vertices);

        // Each ending points away from its neighbouring vertex, as a line's points away from the
        // other end.
        var size = LineEndings.Size(pen.Width);
        var last = vertices.Length - 1;
        LineEndings.Draw(gfx, StartEnding, vertices[0],
            LineEndings.Direction(vertices[1], vertices[0]), pen, brush, size);
        LineEndings.Draw(gfx, EndEnding, vertices[last],
            LineEndings.Direction(vertices[last - 1], vertices[last]), pen, brush, size);
    }
}
