using System;
using System.Collections.Generic;

namespace PdfPinata.Fonts;

/// <summary>
/// A fixed list of families to fall back to, tried in the order given, for every character.
/// </summary>
/// <remarks>
/// <para>
/// The whole of what most documents need. A document that draws Latin with some Arabic in it names
/// the Arabic face and is finished:
/// </para>
/// <code>
/// GlobalFontSettings.FontFallback = new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
/// </code>
/// <para>
/// Naming the families is deliberate rather than a shortcoming of this class. Working out
/// unprompted which of the machine's installed faces can draw a character means reading the
/// <c>cmap</c> of every one of them, which is slow, and answering from it means choosing between
/// faces on grounds - design, weight, language preference - that this library has no way to judge.
/// A list the document's author wrote is both faster and better.
/// </para>
/// </remarks>
public sealed class FontFallbackList : IFontFallback
{
    readonly string[] _families;

    /// <summary>
    /// Initializes a new <see cref="FontFallbackList"/> from the families to try, in order.
    /// </summary>
    public FontFallbackList(params string[] familyNames)
    {
        if (familyNames == null)
            throw new ArgumentNullException(nameof(familyNames));

        _families = (string[])familyNames.Clone();

        foreach (var family in _families)
        {
            if (string.IsNullOrWhiteSpace(family))
                throw new ArgumentException("A font family to fall back to needs a name.",
                    nameof(familyNames));
        }
    }

    /// <summary>The families, in the order they are tried.</summary>
    public IReadOnlyList<string> Families => _families;

    /// <inheritdoc/>
    public IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic) => _families;
}
