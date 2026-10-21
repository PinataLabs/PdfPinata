using System;
using System.Runtime.CompilerServices;
using PdfPinata.Fonts;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The one piece of setup this assembly needs, and a small one: constructing a
///   <see cref="Document"/> builds its standard styles, and the Normal style asks
///   <c>GlobalFontSettings.FontResolver.DefaultFontName</c> what to call its font. Without a
///   resolver set that throws, and every test that touches a document fails on a font it never
///   asked for.
///   <para>
///   What it wants is a <em>name</em>, not a font: nothing in the DOM measures a glyph, so nothing
///   ever calls <see cref="ResolveTypeface"/> or <see cref="GetFont"/>. Answering with a name and
///   refusing the rest is therefore both sufficient and honest - it keeps this project free of a
///   backend, of font files and of the machine's fonts, and if the DOM ever does try to resolve a
///   face the exception below will say so rather than a layout quietly coming out wrong.
///   </para>
/// </summary>
internal sealed class NamedFontsOnly : IFontResolver
{
    /// <summary>
    ///   The name the standard styles are built with. Any name will do - it is written into the
    ///   model and read back out, never looked up - so a made-up one makes the point that it is
    ///   never resolved.
    /// </summary>
    public const string FontName = "Unresolved Test Face";

    public string DefaultFontName => FontName;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        throw new NotSupportedException(
            $"The document object model asked to resolve the typeface '{familyName}'. It holds "
            + "what a document says rather than what it looks like, so nothing in it should need a "
            + "font face. A test that needs one belongs in PinataLayout.Rendering.Tests.");

    public byte[] GetFont(string faceName) =>
        throw new NotSupportedException(
            $"The document object model asked for the bytes of the font '{faceName}'. See "
            + nameof(ResolveTypeface) + " above.");

    /// <summary>
    ///   Sets this as the resolver. Called once, by the module initializer in
    ///   <see cref="NamedFontsOnlyRegistration"/>: registered for the whole assembly rather than
    ///   per test, because the resolver may only be set before the first font is created and
    ///   nothing guarantees an order that would let a fixture get there first - the same
    ///   reasoning as <c>PdfPinata.Test</c>'s <c>TestBackendSetup</c>.
    /// </summary>
    internal static void Register()
    {
        GlobalFontSettings.FontResolver = new NamedFontsOnly();
    }
}

/// <summary>
///   Calls <see cref="NamedFontsOnly.Register"/> when the module loads, except in the
///   <c>--coverlet</c> controller. A class of its own because <see cref="NamedFontsOnly"/>
///   implements a PdfPinata interface, so merely reaching a static method on it loads PdfPinata.dll,
///   and in the controller that locks the file against instrumentation on Windows. See
///   <see cref="PdfPinata.Test.Helpers.CoverageController"/>.
/// </summary>
internal static class NamedFontsOnlyRegistration
{
    // A lambda rather than the method group: taking NamedFontsOnly.Register's address loads
    // NamedFontsOnly, and with it PdfPinata.dll, whether or not the delegate is ever called.
    [ModuleInitializer]
    internal static void Initialize() =>
        PdfPinata.Test.Helpers.CoverageController.OutsideController(() => NamedFontsOnly.Register());
}
