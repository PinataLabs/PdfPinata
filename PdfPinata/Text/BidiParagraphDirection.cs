namespace PdfPinata.Text;

/// <summary>
/// Which way a paragraph runs, before the bidirectional algorithm looks at what is in it.
/// </summary>
public enum BidiParagraphDirection
{
    /// <summary>
    /// Take the direction from the first strong character, per UAX #9 rules P2 and P3, and left to
    /// right if there is none. This is what a word processor does with a paragraph you have not
    /// told it about, and the right default for text of unknown provenance.
    /// </summary>
    Automatic = 0,

    /// <summary>Left to right, whatever the text says.</summary>
    LeftToRight = 1,

    /// <summary>Right to left, whatever the text says.</summary>
    RightToLeft = 2,
}
