using System.Collections.Generic;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Keeps a page whose form XObjects draw one another from running for ever: a form is entered at
/// most once at a time, at most <see cref="MaximumDepth"/> deep and at most <see cref="Limit"/>
/// times in all.
/// </summary>
/// <remarks>
/// The limits are those of the text extractor's own guard, which stays where it is until
/// extraction is moved onto this interpreter.
/// </remarks>
internal sealed class FormVisits
{
    /// <summary>
    /// How many forms one page may enter in all. A tree of forms that each draw the next twice
    /// is never deep and never draws itself, and still doubles at every level.
    /// </summary>
    internal const int Limit = 4096;

    /// <summary>How deep forms may nest. A stack overflow cannot be caught.</summary>
    internal const int MaximumDepth = 32;

    private readonly HashSet<PdfDictionary> _active = [];
    private int _entered;

    /// <summary>Whether a form may be entered now. If so, it has been.</summary>
    internal bool TryEnter(PdfDictionary form)
    {
        if (_entered >= Limit || _active.Count >= MaximumDepth || !_active.Add(form))
            return false;

        _entered++;
        return true;
    }

    internal void Leave(PdfDictionary form) => _active.Remove(form);
}
