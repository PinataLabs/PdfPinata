using System;

namespace PdfPinata.Pdf;

/// <summary>
/// Reads a PDF name as a member of an enumeration whose members are spelt as the names are.
/// </summary>
internal static class PdfNameEnum
{
    /// <summary>
    /// The member of <typeparamref name="T"/> that <paramref name="name"/> stands for, or
    /// <paramref name="fallback"/> when the name is absent, empty, or names something this
    /// enumeration does not have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by everything that reads a name as an enumeration member - the icon of a text, stamp
    /// or file attachment annotation, and the line endings of <c>/LE</c> - rather than copied into
    /// each of them. It was copied, and the copies drifted: two stripped the solidus and checked the
    /// member existed, and the third did neither, so <c>PdfFileAttachmentAnnotation.Icon</c> threw
    /// on every read it was ever given.
    /// </para>
    /// <para>
    /// The solidus is the trap. <c>Elements.GetName</c> hands back <c>PdfName.Value</c>, which
    /// carries it, so the first character of the returned string is never part of the member name -
    /// and <c>GetName</c> answers a missing key with <see cref="string.Empty"/> rather than
    /// <c>null</c>, so a null check for "no name" never fires.
    /// </para>
    /// <para>
    /// <c>Enum.IsDefined</c> rather than <c>Enum.TryParse</c>: given a string of digits
    /// <c>TryParse</c> succeeds and hands back that number as the enumeration value, so a document
    /// naming its icon <c>/3</c> would read back as whichever member happens to be 3.
    /// </para>
    /// </remarks>
    public static T Parse<T>(string name, T fallback) where T : struct, Enum
    {
        var member = PdfName.WithoutSolidus(name);

        return !string.IsNullOrEmpty(member) && Enum.IsDefined(typeof(T), member)
            ? Enum.Parse<T>(member, false)
            : fallback;
    }
}
