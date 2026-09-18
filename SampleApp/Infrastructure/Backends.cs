using System;
using System.Threading;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Skia;
using PdfPinata.Utils;

namespace SampleApp.Infrastructure;

/// <summary>
///   The five static seams PdfPinata leaves for a host to fill: a font resolver, an image
///   source, a glyph outline provider, a text shaper and a font fallback. The core package carries
///   neither an imaging nor a font dependency of its own.
/// </summary>
/// <remarks>
///   <para>
///     Called from exactly one place - the runner, which only <c>Program.Main</c> reaches. No demo
///     calls it, and nothing a demo touches calls it either.
///   </para>
///   <para>
///     That rule is not tidiness. <c>GlobalFontSettings.FontResolver</c> throws
///     "Must not change font resolver after is was once used" as soon as a font has been created,
///     and its getter throws when nothing has been set at all. The test assembly installs its own
///     resolver for everything in it, including these demos once the smoke test runs them. A demo
///     that registered a backend would either throw - and throw only when some other test had
///     already made a font, so passing alone and failing in the suite - or win the race and quietly
///     move every other test in the assembly onto different font metrics.
///   </para>
/// </remarks>
public static class Backends
{
    static int _registered;

    /// <summary>
    ///   Installs the backends, once, and leaves alone any that a host has already chosen.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        if (!FontResolverIsSet())
            GlobalFontSettings.FontResolver = new BundledFontResolver();

        ImageSource.ImageSourceImpl ??= new SkiaImageSource();

        // Wanted by XGraphicsPath.AddString alone, which the Magazine demo uses for its title.
        if (!GlyphOutlineProviderIsSet())
            GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();

        // The two seams whose unset state is not an error: read either before it is set and the
        // answer is null, which means "do what this library always did". They are registered here
        // rather than by the demo that wants them for the reason in the remarks above - the smoke
        // test runs demos inside a host shared with every other test in the assembly, and these are
        // application-wide settings. Nothing calls this method from there, so under test the
        // International demo draws its Arabic unshaped and .notdef, which is exactly what a caller
        // who takes no shaper gets and is why its page count does not depend on either of these.
        GlobalFontSettings.TextShaper ??= new HarfBuzzTextShaper();

        // Liberation Sans has no Arabic in it at all, so a document that names the sans and then
        // writes Arabic gets empty boxes unless something says where else to look.
        GlobalFontSettings.FontFallback ??= new FontFallbackList(BundledFontResolver.ArabicFamily);
    }

    /// <summary>
    ///   Whether a resolver is already installed. There is no "is it set" to ask, and the getter
    ///   answers the question by throwing, so the exception is the test. Ugly, and better than the
    ///   alternative: assigning over a host's resolver, which the setter would refuse anyway the
    ///   moment any font had been made.
    /// </summary>
    static bool FontResolverIsSet()
    {
        try
        {
            _ = GlobalFontSettings.FontResolver;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    ///   Whether an outline provider is already installed. Asked the same way, and for the same
    ///   reason: the getter reports its absence by throwing, so that a caller who never set one
    ///   is told which property to set rather than handed an empty path.
    /// </summary>
    static bool GlyphOutlineProviderIsSet()
    {
        try
        {
            _ = GlobalFontSettings.GlyphOutlineProvider;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
