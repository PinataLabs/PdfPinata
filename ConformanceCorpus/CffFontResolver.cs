using System;
using System.IO;
using System.Reflection;
using PdfPinata.Fonts;
using PdfPinata.Utils;

namespace ConformanceCorpus;

/// <summary>
/// Serves one face with PostScript outlines by name, and leaves every other family to the backend.
/// </summary>
/// <remarks>
/// <para>
/// The corpus needs a CFF font on purpose, and the machine it runs on cannot be relied on to have
/// one — a runner has whatever its base image ships. So the face travels with the corpus, and this
/// answers for that one family and delegates the rest, because <c>Arial</c> is what every other
/// document here draws with and there is only one resolver seam to install into.
/// </para>
/// <para>
/// Source Code Pro is the OpenType/CFF face the assets already carry. It is linked rather than
/// copied: a third copy of the same 128 kB in the repository would be three things to keep in step
/// for no gain.
/// </para>
/// </remarks>
sealed class CffFontResolver : IFontResolver
{
    internal const string Family = "Source Code Pro";

    const string Resource = "ConformanceCorpus.SourceCodePro-Regular.otf";

    readonly SkiaFontResolver _backend = new SkiaFontResolver();

    public string DefaultFontName => _backend.DefaultFontName;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => IsMine(familyName)
            ? new FontResolverInfo(Family)
            : _backend.ResolveTypeface(familyName, isBold, isItalic);

    public byte[] GetFont(string faceName)
    {
        if (!IsMine(faceName))
            return _backend.GetFont(faceName);

        using var stream = typeof(CffFontResolver).GetTypeInfo().Assembly
            .GetManifestResourceStream(Resource);

        if (stream == null)
            throw new InvalidOperationException(
                "The CFF face '" + Resource + "' is not embedded in this assembly, so the document "
                + "meant to exercise PostScript outlines cannot be drawn.");

        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    static bool IsMine(string name)
        => string.Equals(name, Family, StringComparison.OrdinalIgnoreCase);
}
