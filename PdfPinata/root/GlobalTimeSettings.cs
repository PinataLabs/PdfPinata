using System;

namespace PdfPinata;

/// <summary>
/// Where PdfPinata reads the current time. Everything the library stamps a document with — a
/// creation date, a modification date, an annotation's date, a signing time, the value a MigraDoc
/// date field renders to — is read through here rather than from <see cref="DateTime.Now"/>
/// directly, so that a caller who needs a document to come out the same twice can say what the
/// time is.
/// </summary>
/// <remarks>
/// The default reads <see cref="DateTime.Now"/>, which is local time. That is deliberate: a PDF
/// date string carries its own UTC offset, so local time is what the format expects, and switching
/// to UTC would change what every document says.
/// <para>
/// Like <see cref="PdfPinata.Fonts.GlobalFontSettings.FontResolver"/> this is one setting for
/// the whole application domain, and it is not synchronised. Set it once during start-up, before
/// any document is created, rather than swapping it around while documents are being written. A
/// test suite that runs in parallel should prefer asserting a date falls between two readings of
/// the clock over fixing this and racing every other test that reads it.
/// </para>
/// </remarks>
public static class GlobalTimeSettings
{
    /// <summary>
    /// Gets or sets the clock the library reads the time from. Setting it to null restores the
    /// default, which is <see cref="DateTime.Now"/>.
    /// </summary>
    public static Func<DateTime> Clock
    {
        get => _clock;
        set => _clock = value ?? DefaultClock;
    }
    static Func<DateTime> _clock = DefaultClock;

    /// <summary>
    /// Gets the current time, as <see cref="Clock"/> reports it. Reads the same as
    /// <see cref="DateTime.Now"/> until a clock is set.
    /// </summary>
    public static DateTime Now => _clock();

    static DateTime DefaultClock() => DateTime.Now;
}
