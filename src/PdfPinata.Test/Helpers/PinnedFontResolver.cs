using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using PdfPinata.Drawing;
using PdfPinata.Fonts;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Answers every request with the fonts shipped alongside the tests, rather than with
///   whatever the machine happens to have installed.
/// </summary>
/// <remarks>
///   The resolver the library ships scans the font directories of the platform, so a document
///   built by the tests is laid out with Arial here and with whatever stands in for it there.
///   That decides more than the shape of the glyphs: their widths decide where a line wraps,
///   and the height of a line against the height of its characters decides whether the line
///   fits at all. Tests that assert either of those cannot pass on both machines while the
///   font is chosen for them. Liberation Sans is used because it carries the metrics of Arial,
///   which the assertions and the reference images were written against.
///
///   Linked, not copied, into <c>PinataLayout.Rendering.Tests</c> and
///   <c>PdfPinata.Charting.Tests</c> - the same way this project's content-stream readers are
///   shared with those two projects - so that the one fact deciding every layout assertion in
///   three suites has one file to edit. Compiling it needs no project reference, which is why it
///   resolves its own path through the executing assembly rather than through
///   <see cref="PathHelper"/>: a type that, unlike this one, is not linked anywhere.
/// </remarks>
internal sealed class PinnedFontResolver : IFontResolver
{
    private static readonly ConcurrentDictionary<string, byte[]> Fonts =
        new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   The one family here that is not Liberation. Its outlines are PostScript (CFF) rather
    ///   than TrueType, which is a different embedding path entirely, and nothing else shipped
    ///   with the tests exercises it.
    /// </summary>
    public const string CffFamilyName = "Source Code Pro";

    private const string CffFaceName = "SourceCodePro-Regular.otf";

    /// <summary>
    ///   The one family here with a glyph Liberation Sans has not got, which is what makes it the
    ///   family every fallback and shaping test falls back <em>to</em>.
    /// </summary>
    /// <remarks>
    ///   Answered here rather than registered by whichever test gets there first, and that is the
    ///   whole point of it being here. Every family this resolver does not know is answered with
    ///   Liberation Sans, so a family that is registered lazily means something different depending
    ///   on whether the registration or the first draw won the race - and the library caches the
    ///   first answer, so the loser is stuck with it for the rest of the run. That is not
    ///   hypothetical: it turned four Arabic tests into four <c>.notdef</c> boxes the day a demo in
    ///   the sample app started naming this family too, and it did it on some runs and not others.
    ///   A family the resolver knows from the start cannot be raced for.
    /// </remarks>
    public const string ArabicFamilyName = "Noto Sans Arabic";

    /// <summary>
    ///   The family the Devanagari face answers to. Served from here for the same reason the Arabic
    ///   one is: a family registered on first use means whatever the first caller in the assembly
    ///   made it mean, and this resolver answers every family it does not know with Liberation Sans.
    /// </summary>
    public const string DevanagariFamilyName = "Noto Sans Devanagari";

    private const string ArabicFaceName = "NotoSansArabic-Regular.ttf";

    private const string DevanagariFaceName = "NotoSansDevanagari-Regular.ttf";

    /// <summary>
    ///   Families a test builds the bytes for itself. The resolver is installed once for the
    ///   whole assembly, so a test that needs a font of its own adds one here rather than
    ///   replacing the resolver under every other test running beside it.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> Registered =
        new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   Serves <paramref name="fontBytes"/> to anything asking for <paramref name="familyName"/>.
    ///   Registering the same family twice with different bytes is a test colliding with
    ///   another, so the first registration wins and the caller is told.
    /// </summary>
    public static void Register(string familyName, byte[] fontBytes)
    {
        string faceName = familyName + ".test";
        Fonts.TryAdd(faceName, fontBytes);
        Registered.TryAdd(familyName, faceName);
    }

    public string DefaultFontName => "Arial";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (Registered.TryGetValue(familyName, out string testFaceName))
            return new FontResolverInfo(testFaceName);

        if (string.Equals(familyName, ArabicFamilyName, StringComparison.OrdinalIgnoreCase))
        {
            // No style simulation. A stroked or skewed Arabic letter is not a bold or an italic
            // one, and only a regular face ships.
            return new FontResolverInfo(ArabicFaceName);
        }

        if (string.Equals(familyName, DevanagariFamilyName, StringComparison.OrdinalIgnoreCase))
        {
            // No style simulation, for the same reason as the Arabic face above.
            return new FontResolverInfo(DevanagariFaceName);
        }

        if (string.Equals(familyName, CffFamilyName, StringComparison.OrdinalIgnoreCase))
        {
            // A regular face is all that is shipped for this family, so a bold or an italic has
            // to be drawn on. That also gives the style-simulation tests a family to work with:
            // the same file answers every request, so only the simulation differs.
            XStyleSimulations simulations =
                (isBold ? XStyleSimulations.BoldSimulation : XStyleSimulations.None)
                | (isItalic ? XStyleSimulations.ItalicSimulation : XStyleSimulations.None);

            return new FontResolverInfo(CffFaceName, simulations);
        }

        // Every other family is answered, so that a document asking for a font that is not
        // shipped is laid out the same way everywhere instead of falling back to the machine.
        return new FontResolverInfo(FaceNameOf(isBold, isItalic));
    }

    public byte[] GetFont(string faceName)
    {
        return Fonts.GetOrAdd(faceName, name => File.ReadAllBytes(AssetPathOf(name)));
    }

    /// <summary>
    ///   Where a face name's file is. The two named faces are files in their own right; every
    ///   other name is one of Liberation Sans's four styles.
    /// </summary>
    /// <remarks>
    ///   Resolved through the executing assembly's own location rather than through
    ///   <see cref="PathHelper"/>, because this class is linked into two other test projects that
    ///   do not reference this one and so cannot see that type. Every project this file is linked
    ///   into copies "Assets/Fonts" from this one's into its own output directory.
    /// </remarks>
    private static string AssetPathOf(string faceName)
    {
        var directory = Path.GetDirectoryName(typeof(PinnedFontResolver).GetTypeInfo().Assembly.Location);

        if (faceName == CffFaceName || faceName == ArabicFaceName
            || faceName == DevanagariFaceName)
            return Path.Combine(directory, "Assets", "Fonts", faceName);

        return Path.Combine(directory, "Assets", "Fonts", "LiberationSans-" + faceName + ".ttf");
    }

    private static string FaceNameOf(bool isBold, bool isItalic)
    {
        if (isBold && isItalic)
            return "BoldItalic";
        if (isBold)
            return "Bold";
        if (isItalic)
            return "Italic";
        return "Regular";
    }
}
