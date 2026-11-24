using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// One optional content group of a document, a layer, as <see cref="OptionalContentLayers"/> lists
/// it (ISO 32000-1 8.11.2).
/// </summary>
[Experimental("PPEXP001")]
public sealed class OptionalContentGroup
{
    internal OptionalContentGroup(PdfDictionary dictionary, string name, IReadOnlyList<string> intents)
    {
        Dictionary = dictionary;
        Name = name;
        Intents = intents;
    }

    /// <summary>
    /// The group's dictionary, the one that <see cref="PageOptionalContent.On"/> and
    /// <see cref="PageOptionalContent.Off"/> take.
    /// </summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>The group's <c>/Name</c>, or null where it has none that is a string.</summary>
    public string Name { get; }

    /// <summary>
    /// The group's <c>/Intent</c> without the slash, such as <c>View</c> or <c>Design</c>, and
    /// <c>View</c> alone where it names none.
    /// </summary>
    public IReadOnlyList<string> Intents { get; }
}
