using System;

namespace PdfPinata.Text;

/// <summary>
/// Reading UTF-16 text a code point at a time, whatever the text is.
/// </summary>
/// <remarks>
/// <c>System.Text.Rune</c> does this on net8.0 and net10.0 but is not in netstandard2.1, the leg
/// Unity consumes, and it reads a lone surrogate as the replacement character where this has to
/// read it as itself.
/// </remarks>
internal static class CodePoints
{
    /// <summary>
    /// The code point at <paramref name="idx"/>, and in <paramref name="width"/> how many code
    /// units it takes: two for a surrogate pair, one for anything else.
    /// </summary>
    /// <remarks>
    /// The pair is looked for inside <paramref name="text"/>, so a text that ends between the halves
    /// of a surrogate pair reads what is left of it as itself rather than reaching past its own end.
    /// A surrogate that is not half of a pair is read as a code point of its own, where
    /// <see cref="char.ConvertToUtf32(char, char)"/> would throw: that is not an answer a layout path
    /// can use, and the Unicode tables give such a code point a class like any other.
    /// </remarks>
    internal static int At(ReadOnlySpan<char> text, int idx, out int width)
    {
        if (char.IsHighSurrogate(text[idx]) && idx + 1 < text.Length && char.IsLowSurrogate(text[idx + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(text[idx], text[idx + 1]);
        }

        width = 1;
        return text[idx];
    }
}
