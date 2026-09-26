using System;
using System.Globalization;

namespace PdfPinata.Pdf;

/// <summary>
/// The two forms a date is written in - the PDF date string of ISO 32000-1 7.9.4, for
/// <c>/CreationDate</c>, <c>/ModDate</c> and every other date entry, and the ISO 8601 form XMP wants -
/// both from one reading of the offset, so that <c>/Info</c> and the metadata packet describe the
/// same instant.
/// </summary>
/// <remarks>
/// <para>
/// Both are formatted in the invariant culture. A culture whose default calendar is not the
/// Gregorian one writes its own year otherwise: th-TH wrote 2569 for 2026.
/// </para>
/// <para>
/// A date of <see cref="DateTimeKind.Unspecified"/> kind is taken to be local time, which is what
/// the PDF writer has always assumed, and what a date built from its parts with no kind usually means.
/// The XMP writer used to write it with no offset at all, leaving the reader to guess, while
/// <c>/Info</c> said the local one - two descriptions of one instant that PDF/A requires to agree.
/// </para>
/// </remarks>
internal static class PdfDateFormat
{
    /// <summary>
    /// The PDF date string, <c>D:YYYYMMDDHHmmSS+HH'mm'</c>. Universal time is written with an offset of
    /// <c>+00'00'</c> rather than <c>Z</c>, as it always has been.
    /// </summary>
    public static string Pdf(DateTime value)
        => "D:" + value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
           + SignedOffset(value, '\'') + "'";

    /// <summary>
    /// The ISO 8601 form XMP wants, <c>YYYY-MM-DDThh:mm:ss</c> and an offset, <c>Z</c> for
    /// universal time.
    /// </summary>
    public static string Xmp(DateTime value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
           + (value.Kind == DateTimeKind.Utc ? "Z" : SignedOffset(value, ':'));

    /// <summary>
    /// How far <paramref name="value"/> is ahead of universal time: none for a universal time, and the
    /// local zone's offset at that moment otherwise.
    /// </summary>
    public static TimeSpan Offset(DateTime value)
        => value.Kind == DateTimeKind.Utc ? TimeSpan.Zero : TimeZoneInfo.Local.GetUtcOffset(value);

    private static string SignedOffset(DateTime value, char separator)
    {
        var offset = Offset(value);
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var magnitude = offset.Duration();
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:00}{2}{3:00}", sign, magnitude.Hours, separator,
            magnitude.Minutes);
    }
}
