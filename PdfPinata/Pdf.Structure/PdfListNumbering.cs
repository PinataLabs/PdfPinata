namespace PdfPinata.Pdf.Structure;

/// <summary>
/// How a reader should announce the items of a list — ISO 32000-2 Table 349's values for the
/// standard "List" attribute owner class's <c>ListNumbering</c> entry.
/// </summary>
/// <remarks>
/// A fixed set rather than a string, unlike <see cref="PdfTag"/>: the standard defines exactly
/// these values and nothing names its own, so there is nothing for a caller to extend.
/// </remarks>
public enum PdfListNumbering
{
    /// <summary>No numbering is visible — the list is presented as an unmarked list.</summary>
    None,

    /// <summary>A solid circular bullet.</summary>
    Disc,

    /// <summary>An open circular bullet.</summary>
    Circle,

    /// <summary>A square bullet.</summary>
    Square,

    /// <summary>Decimal Arabic numerals: 1, 2, 3, ...</summary>
    Decimal,

    /// <summary>Uppercase Roman numerals: I, II, III, ...</summary>
    UpperRoman,

    /// <summary>Lowercase Roman numerals: i, ii, iii, ...</summary>
    LowerRoman,

    /// <summary>Uppercase letters: A, B, C, ...</summary>
    UpperAlpha,

    /// <summary>Lowercase letters: a, b, c, ...</summary>
    LowerAlpha,
}
