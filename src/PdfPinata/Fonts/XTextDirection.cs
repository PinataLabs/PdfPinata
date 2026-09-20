namespace PdfPinata.Fonts;

/// <summary>
/// The direction a run of text is written in.
/// </summary>
/// <remarks>
/// This is the direction of a single already-itemised run, not of a paragraph. A paragraph
/// containing both Arabic and Latin is several runs, each with a direction of its own; deciding
/// where one ends and the next begins is the job of the Unicode Bidirectional Algorithm and not
/// of this enumeration.
/// </remarks>
public enum XTextDirection
{
    /// <summary>
    /// The run advances to the right. The default, and what every path in this library did before
    /// there was anything to choose.
    /// </summary>
    LeftToRight = 0,

    /// <summary>
    /// The run advances to the left. The glyphs are in visual order, leftmost first, so a renderer
    /// draws them exactly as it draws a left-to-right run - the reordering has already happened.
    /// </summary>
    RightToLeft = 1
}
