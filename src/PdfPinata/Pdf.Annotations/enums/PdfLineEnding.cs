namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// The shape drawn at one end of a <see cref="PdfLineAnnotation"/> - ISO 32000-1 Table 176, in
/// full, which is the whole of what may appear in a <c>/LE</c> array.
/// </summary>
/// <remarks>
/// The member names are the names written to the file, so <see cref="None"/> is <c>/None</c> and
/// not an absent entry: a line saying it ends in nothing is a line, where one saying nothing is a
/// line a reader may finish however it likes.
/// </remarks>
public enum PdfLineEnding
{
    /// <summary>
    /// No line ending. The line stops where it stops.
    /// </summary>
    None,

    /// <summary>
    /// A square filled with the annotation's interior colour, centred on the endpoint.
    /// </summary>
    Square,

    /// <summary>
    /// A circle filled with the annotation's interior colour, centred on the endpoint.
    /// </summary>
    Circle,

    /// <summary>
    /// A diamond filled with the annotation's interior colour, centred on the endpoint.
    /// </summary>
    Diamond,

    /// <summary>
    /// Two short lines meeting at an acute angle to form an open arrowhead.
    /// </summary>
    OpenArrow,

    /// <summary>
    /// A triangular closed arrowhead, filled with the annotation's interior colour.
    /// </summary>
    ClosedArrow,

    /// <summary>
    /// A short line at the endpoint, perpendicular to the line itself.
    /// </summary>
    Butt,

    /// <summary>
    /// An open arrowhead pointing back along the line rather than away from it.
    /// </summary>
    ROpenArrow,

    /// <summary>
    /// A closed arrowhead pointing back along the line rather than away from it.
    /// </summary>
    RClosedArrow,

    /// <summary>
    /// A short line at the endpoint, about thirty degrees clockwise from perpendicular.
    /// </summary>
    Slash,
}
