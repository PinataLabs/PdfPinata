using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Fields;

namespace PinataLayout.Rendering;

/// <summary>
/// Turns a footnote's position in the sequence into the mark that stands for it.
/// </summary>
/// <remarks>
/// The five styles map onto <see cref="NumberFormatter"/>, which already writes roman numerals and
/// letter sequences for list numbering. Writing them a second time here would be two things to get
/// right, and the two would drift.
/// </remarks>
internal static class FootnoteNumbering
{
    /// <summary>
    /// The mark for the <paramref name="ordinal"/>th note counted from one.
    /// </summary>
    internal static string Mark(int ordinal, FootnoteNumberStyle style)
    {
        switch (style)
        {
            case FootnoteNumberStyle.LowercaseLetter:
                return NumberFormatter.Format(ordinal, "alphabetic");

            case FootnoteNumberStyle.UppercaseLetter:
                return NumberFormatter.Format(ordinal, "ALPHABETIC");

            case FootnoteNumberStyle.LowercaseRoman:
                return NumberFormatter.Format(ordinal, "roman");

            case FootnoteNumberStyle.UppercaseRoman:
                return NumberFormatter.Format(ordinal, "ROMAN");

            case FootnoteNumberStyle.Arabic:
            default:
                return ordinal.ToString();
        }
    }
}
