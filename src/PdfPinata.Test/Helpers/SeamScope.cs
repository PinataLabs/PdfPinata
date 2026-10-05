using System;
using System.Threading;
using PdfPinata.Fonts;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Installs a <see cref="GlobalFontSettings.TextShaper"/> or <see cref="GlobalFontSettings.FontFallback"/>
/// for as long as a test needs it, and puts back <b>whatever was there before</b> when disposed.
/// </summary>
/// <remarks>
/// Both seams are one setting for the whole application domain and may be changed at any time, so
/// a test installs its own and has to leave the seam as it found it. Clearing it to null instead is
/// right only for as long as nothing installs one for the whole assembly - nothing does today, and
/// the first thing that did would have every scope that cleared the seam quietly take it away from
/// whatever ran next.
/// <para>
/// The previous value is read through the public getter. For <see cref="GlobalFontSettings.FontFallback"/>
/// that answers the registered font resolver when no fallback was set and the resolver implements
/// <see cref="IFontFallback"/> itself, so restoring then sets that resolver explicitly - which is
/// what the getter answered anyway.
/// </para>
/// <para>
/// Restoring does not keep two tests installing at once out of each other's way; that is what
/// <see cref="TextShapingCollection"/> is for.
/// </para>
/// </remarks>
internal sealed class SeamScope : IDisposable
{
    private Action _restore;

    private SeamScope(Action restore) => _restore = restore;

    /// <summary>Sets <see cref="GlobalFontSettings.TextShaper"/> until the scope is disposed.</summary>
    internal static SeamScope TextShaper(ITextShaper shaper)
    {
        var previous = GlobalFontSettings.TextShaper;
        GlobalFontSettings.TextShaper = shaper;
        return new SeamScope(() => GlobalFontSettings.TextShaper = previous);
    }

    /// <summary>Sets <see cref="GlobalFontSettings.FontFallback"/> until the scope is disposed.</summary>
    internal static SeamScope FontFallback(IFontFallback fallback)
    {
        var previous = GlobalFontSettings.FontFallback;
        GlobalFontSettings.FontFallback = fallback;
        return new SeamScope(() => GlobalFontSettings.FontFallback = previous);
    }

    /// <summary>Puts the previous value back. Disposing a second time does nothing.</summary>
    public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
}
