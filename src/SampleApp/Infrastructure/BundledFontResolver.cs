using System;
using System.Collections.Concurrent;
using PdfPinata.Drawing;
using PdfPinata.Fonts;

namespace SampleApp.Infrastructure;

/// <summary>
///   Answers every font request with one of the three families the app carries, rather than with
///   whatever the machine happens to have installed.
/// </summary>
/// <remarks>
///   <para>
///     The resolver the library ships scans the platform's font directories, which is right for the
///     library and wrong for a demonstration: Arial on Windows, something else on macOS, and on a
///     bare Linux container frequently nothing at all. A demo of fonts that renders with whatever
///     is lying around is not a demo of anything.
///   </para>
///   <para>
///     This differs from the test suite's <c>PinnedFontResolver</c> in the one way that matters
///     here. That one answers <em>every</em> family with Liberation Sans, because a test wants the
///     same metrics whatever it asks for. A demo wants the opposite - three families that look like
///     three families - so each name is mapped to its own files, and only an unrecognised name
///     falls back to the sans.
///   </para>
/// </remarks>
public sealed class BundledFontResolver : IFontResolver
{
    /// <summary>The sans family, carrying the metrics of Arial.</summary>
    public const string SansFamily = "Liberation Sans";

    /// <summary>The serif family, carrying the metrics of Times New Roman.</summary>
    public const string SerifFamily = "Liberation Serif";

    /// <summary>
    ///   The monospaced family, and the only one here whose outlines are PostScript (CFF) rather
    ///   than TrueType. Only a regular face ships, so bold and italic come back simulated.
    /// </summary>
    public const string MonoFamily = "Source Code Pro";

    /// <summary>
    ///   The Arabic family, and the only one here that none of the other three can stand in for.
    /// </summary>
    /// <remarks>
    ///   Liberation and Source Code Pro between them have no Arabic glyph at all, so this is what
    ///   the International demo falls back <em>to</em> - and what it shapes, because Arabic is the
    ///   script where the difference between shaped and unshaped is a different set of letters
    ///   rather than a different fit. Only a regular face ships.
    /// </remarks>
    public const string ArabicFamily = "Noto Sans Arabic";

    const string MonoFace = "SourceCodePro-Regular.otf";

    const string ArabicFace = "NotoSansArabic-Regular.ttf";

    static readonly ConcurrentDictionary<string, byte[]> Loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   What a document gets when it names a family that is not carried here - including the
    ///   "Arial" that so much sample code asks for.
    /// </summary>
    public string DefaultFontName => SansFamily;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (Matches(familyName, ArabicFamily))
        {
            // One face, and no simulation asked for either. A stroked or skewed Arabic letter is
            // not a bold or an italic one - those are not distinctions this script makes, and
            // faking them would draw something no reader of it would recognise.
            return new FontResolverInfo(ArabicFace);
        }

        if (Matches(familyName, MonoFamily))
        {
            // One face for the whole family, so a bold or an italic has to be drawn on rather than
            // chosen. The Fonts demo puts these beside Liberation's real four so the difference
            // between a designed weight and a stroked one can be seen rather than described.
            var simulations =
                (isBold ? XStyleSimulations.BoldSimulation : XStyleSimulations.None)
                | (isItalic ? XStyleSimulations.ItalicSimulation : XStyleSimulations.None);

            return new FontResolverInfo(MonoFace, simulations);
        }

        var family = Matches(familyName, SerifFamily) ? "LiberationSerif" : "LiberationSans";
        return new FontResolverInfo($"{family}-{FaceOf(isBold, isItalic)}.ttf");
    }

    public byte[] GetFont(string faceName) =>
        Loaded.GetOrAdd(faceName, name => Assets.Bytes(Assets.FontPrefix + name));

    static bool Matches(string familyName, string family) =>
        string.Equals(familyName, family, StringComparison.OrdinalIgnoreCase);

    static string FaceOf(bool isBold, bool isItalic)
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
