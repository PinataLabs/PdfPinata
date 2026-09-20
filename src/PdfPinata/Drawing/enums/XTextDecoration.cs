namespace PdfPinata.Drawing;

/// <summary>
/// Specifies how the rule under or through a run of text is drawn.
/// </summary>
/// <remarks>
/// The same six rules PinataLayout offers for its underline and strikethrough, so that the two
/// layers of this library can say the same things. PDFKit has only on and off, which is
/// <see cref="Single"/>.
/// </remarks>
public enum XTextDecoration
{
    /// <summary>
    /// No rule is drawn. The default.
    /// </summary>
    None = 0,

    /// <summary>
    /// One unbroken rule, spaces included.
    /// </summary>
    Single = 1,

    /// <summary>
    /// One rule under each word, leaving the spaces between them unmarked.
    /// </summary>
    Words = 2,

    /// <summary>
    /// A dotted rule.
    /// </summary>
    Dotted = 3,

    /// <summary>
    /// A dashed rule.
    /// </summary>
    Dash = 4,

    /// <summary>
    /// A rule of alternating dashes and dots.
    /// </summary>
    DotDash = 5,

    /// <summary>
    /// A rule of dashes separated by pairs of dots.
    /// </summary>
    DotDotDash = 6
}
