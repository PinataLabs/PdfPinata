namespace PdfPinata.Fonts;

/// <summary>
/// States how a global seam on <see cref="GlobalFontSettings"/> or on <c>ImageSource</c> may be
/// written after it has already been set, so that the rule is read from a seam's own
/// <c>*Lifecycle</c> property rather than learned by triggering the exception it throws when
/// broken.
/// </summary>
public enum SeamLifecycle
{
    /// <summary>
    /// The seam refuses a second write once it has been acted on - because what that first write
    /// decided is already cached elsewhere, and changing it underneath that cache would produce a
    /// document that disagrees with itself.
    /// </summary>
    SetOnce,

    /// <summary>
    /// The seam may be set, replaced or cleared at any time. Nothing depends on it having stayed
    /// the same.
    /// </summary>
    SetAnytime,
}
