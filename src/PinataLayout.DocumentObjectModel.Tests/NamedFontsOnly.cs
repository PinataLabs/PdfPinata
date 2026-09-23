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
    ///   Registered for the whole assembly rather than per test, because the resolver may only be
    ///   set before the first font is created and xUnit gives no ordering guarantee that would let
    ///   a fixture get there first. The same reasoning as <c>PdfPinata.Test</c>'s
    ///   <c>TestBackendSetup</c>, which is a module initializer for the same reason.
    /// </summary>
    [ModuleInitializer]
    internal static void Register()
    {
        GlobalFontSettings.FontResolver = new NamedFontsOnly();
    }
}
