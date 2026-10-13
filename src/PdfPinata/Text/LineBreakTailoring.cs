using System;

namespace PdfPinata.Text;

/// <summary>
/// Departures from UAX #14's default rules that a caller may ask the line breaking algorithm for.
/// With none, it is the default algorithm and passes <c>LineBreakTest.txt</c> in full.
/// </summary>
[Flags]
public enum LineBreakTailoring
{
    /// <summary>The default algorithm.</summary>
    None = 0,

    /// <summary>
    /// Resolve a letter of class <see cref="LineBreakClass.SA"/> - Thai, Lao, Khmer, Myanmar and
    /// the other scripts of South East Asia written without spaces between words - to
    /// <see cref="LineBreakClass.ID"/> rather than to <see cref="LineBreakClass.AL"/>, so that a
    /// line may be broken between any two of them, as between two ideographs. A mark of the class
    /// still resolves to <see cref="LineBreakClass.CM"/> and stays with its letter.
    /// </summary>
    /// <remarks>
    /// Rule LB1 leaves SA to "criteria outside the scope of this algorithm" - a dictionary of the
    /// language - and resolves it to AL in their absence, which joins the whole of a Thai sentence
    /// into one unbreakable word. Breaking anywhere is the better default for a layout engine
    /// that has no dictionary: a line broken between two syllables of a word is a worse line than
    /// one broken between two words, but a line that cannot be broken at all runs off the page.
    /// </remarks>
    SouthEastAsianAsIdeographs = 1,
}
