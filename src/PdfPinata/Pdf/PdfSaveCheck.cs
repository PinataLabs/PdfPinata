namespace PdfPinata.Pdf;

/// <summary>
/// Whether a document can be saved as it stands, and if not, why.
/// </summary>
/// <remarks>
/// <see cref="CanSave"/> is worked out from <see cref="Reason"/> rather than stored beside it, so a
/// refusal always says why, and <c>default</c> is a check that passed rather than a third state.
/// </remarks>
public readonly struct PdfSaveCheck
{
    private PdfSaveCheck(string reason)
    {
        Reason = reason;
    }

    /// <summary>Whether the document can be saved.</summary>
    public bool CanSave => Reason == null;

    /// <summary>Why the document cannot be saved, or null when it can.</summary>
    public string Reason { get; }

    /// <summary>A check that passed.</summary>
    internal static PdfSaveCheck Allowed => default;

    /// <summary>A check that failed, for the given reason.</summary>
    internal static PdfSaveCheck Refused(string reason)
    {
        return new PdfSaveCheck(reason);
    }
}
