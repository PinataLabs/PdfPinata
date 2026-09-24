
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using PdfPinata.Internal;
using PdfPinata.Drawing;
using PdfPinata.Fonts;


namespace PdfPinata.Utils;

/// <summary>
/// Family name and style read from a font file.
/// </summary>
public readonly struct FontMetadata
{
    /// <summary>Initializes a new instance of the <see cref="FontMetadata"/> structure.</summary>
    public FontMetadata(string familyName, XFontStyle style)
    {
        FamilyName = familyName;
        Style = style;
    }

    /// <summary>Gets the family name read from the font file.</summary>
    public string FamilyName { get; }

    /// <summary>Gets the style read from the font file.</summary>
    public XFontStyle Style { get; }
}


/// <summary>
/// Locates the fonts installed on the current platform and resolves typefaces against them.
/// Reading the family name and style out of a font file is left to a derived class, so that
/// PdfPinata itself does not depend on any particular font library.
/// Use the resolver from PdfPinata.Skia or PdfPinata.ImageSharp, or derive your own.
/// </summary>
public abstract class FontResolverBase
    : IFontResolver
{
    /// <summary>Gets the family name used when a document asks for no font in particular.</summary>
    public virtual string DefaultFontName => "Arial";

    // Per instance rather than static: two backends in one process must not inherit each
    // other's font mappings, and reading metadata is the derived class's job.
    private readonly object _initLock = new();

    // Volatile because EnsureInitialized reads this outside the lock. Without it a thread can
    // see the reference before the writes that filled the dictionary, on any architecture with
    // a weaker memory model than x86.
    private volatile Dictionary<string, FontFamilyModel> _installedFonts;

    /// <summary>
    /// Maps the face name handed out by <see cref="ResolveTypeface"/> to where it was read
    /// from. Published before <see cref="_installedFonts"/>, whose volatile write orders it.
    /// </summary>
    private Dictionary<string, FaceLocation> _facePaths;


    /// <summary>
    /// Where a face was found: the file, and which member of it when that file is a collection.
    /// </summary>
    private readonly struct FaceLocation
    {
        public FaceLocation(string path, int faceIndex)
        {
            Path = path;
            FaceIndex = faceIndex;
        }

        public string Path { get; }

        /// <summary>
        /// The index of the face within a collection, or -1 when the file holds a single font.
        /// </summary>
        public int FaceIndex { get; }
    }


    /// <summary>
    /// Reads the family name and style out of the given font file.
    /// </summary>
    protected abstract FontMetadata ReadFontMetadata(string fontFilePath);


    /// <summary>
    /// Reads the family name and style of one face of the given font file.
    /// </summary>
    /// <param name="fontFilePath">The font file.</param>
    /// <param name="faceIndex">
    /// The index of the face within a collection file, or -1 when the file holds a single font.
    /// </param>
    /// <remarks>
    /// Overriding this is what lets a backend see the faces of a collection past the first.
    /// One that does not override it still resolves every single-font file, and reports each
    /// further face of a collection as unreadable - which discovery logs and skips - because
    /// <see cref="ReadFontMetadata(string)"/> has no way to say which face it described.
    /// </remarks>
    protected virtual FontMetadata ReadFontMetadata(string fontFilePath, int faceIndex)
    {
        if (faceIndex < 0)
            return ReadFontMetadata(fontFilePath);

        throw new System.NotSupportedException(
            "This font resolver reads single-font files only; face " + faceIndex + " of the collection '"
            + fontFilePath + "' cannot be described.");
    }


    /// <summary>
    /// Reads the family name and style of every face of a collection file, in order.
    /// </summary>
    /// <remarks>
    /// Overriding this as well as <see cref="ReadFontMetadata(string,int)"/> lets a backend open
    /// the file once instead of once per face. The default reads it once per face, which for
    /// the dozen-face collections a system font directory holds is most of the cost of
    /// discovering one. A backend that cannot do better need not override it.
    /// </remarks>
    protected virtual FontMetadata[] ReadCollectionMetadata(string fontFilePath, int faceCount)
    {
        var metadata = new FontMetadata[faceCount];

        for (var face = 0; face < faceCount; face++)
            metadata[face] = ReadFontMetadata(fontFilePath, face);

        return metadata;
    }


    /// <summary>
    /// Reports a font file that could not be used. Written to the trace listeners rather than to
    /// the console, so that a host that wants to hear about unreadable fonts can listen for them
    /// and one that does not is not made to read them.
    /// </summary>
    private static void LogError(string message)
    {
        Trace.WriteLine(message);
    }


    /// <summary>
    /// Scans the platform font directories on first use. Deferred rather than done in the
    /// constructor so that <see cref="ReadFontMetadata(string)"/> is never called on a half-built
    /// derived instance.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_installedFonts != null)
            return;

        lock (_initLock)
        {
            if (_installedFonts == null)
                SetupFontsFiles(GetPlatformFontFiles());
        }
    }


    private static string[] GetPlatformFontFiles()
    {
        string fontDir;

        var isOSX = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
        if (isOSX)
        {
            fontDir = "/Library/Fonts/";
            return System.IO.Directory.Exists(fontDir) ? [..FontFileTypes.In(fontDir)] : [];
        }

        var isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        if (isLinux)
        {
            return LinuxSystemFontResolver.Resolve();
        }

        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        if (isWindows)
        {
            fontDir = System.Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Fonts");
            var fontPaths = new List<string>();
            if (System.IO.Directory.Exists(fontDir))
                fontPaths.AddRange(FontFileTypes.In(fontDir));

            var appdataFontDir = System.Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Windows\Fonts");
            if (System.IO.Directory.Exists(appdataFontDir))
                fontPaths.AddRange(FontFileTypes.In(appdataFontDir));

            return [..fontPaths];
        }

        throw new System.NotImplementedException("FontResolver not implemented for this platform (PdfPinata.Utils.FontResolverBase.cs).");
    }


    private readonly struct FontFileInfo
    {
        public FontFileInfo(string faceName, FontMetadata metadata)
        {
            FaceName = faceName;
            Metadata = metadata;
        }

        /// <summary>
        /// The name this face is handed out under, and the key into the face-to-path map.
        /// </summary>
        public string FaceName { get; }

        public FontMetadata Metadata { get; }

        public string FamilyName => Metadata.FamilyName;

        public XFontStyle GuessFontStyle() => Metadata.Style;
    }


    /// <summary>
    /// Builds the family lookup from an explicit set of font files, replacing anything
    /// discovered previously.
    /// </summary>
    public void SetupFontsFiles(string[] sSupportedFonts)
    {
        var facePaths = new Dictionary<string, FaceLocation>(System.StringComparer.OrdinalIgnoreCase);
        var tempFontInfoList = new List<FontFileInfo>();

        foreach (var fontPathFile in sSupportedFonts)
            AddFacesOfFile(fontPathFile, facePaths, tempFontInfoList);

        var installedFonts = BuildFamilies(tempFontInfoList);

        lock (_initLock)
        {
            _facePaths = facePaths;
            // Written last: its volatile write publishes _facePaths to EnsureInitialized,
            // which reads that field to decide whether both are ready.
            _installedFonts = installedFonts;
        }
    }


    /// <summary>
    /// Reads every face of one font file into the lookups being built, logging and skipping
    /// whatever cannot be read.
    /// </summary>
    private void AddFacesOfFile(string fontPathFile, Dictionary<string, FaceLocation> facePaths,
        List<FontFileInfo> fontInfoList)
    {
        var fileName = System.IO.Path.GetFileName(fontPathFile);

        if (!TryCountFaces(fontPathFile, out var faceCount, out var isCollection))
            return;

        Debug.WriteLine(fontPathFile);

        var collectionMetadata = isCollection ? TryReadCollectionMetadata(fontPathFile, faceCount) : null;

        for (var face = 0; face < faceCount; face++)
        {
            var (faceIndex, faceName) = FaceIndexAndName(fileName, face, isCollection);

            // Two font directories habitually hold a file of the same name - on Windows the
            // system one and the per-user one. The first found wins, so that the face name a
            // family points at and the file it is read from cannot drift apart. Checked
            // before the metadata is read, so that a file the derived class cannot parse
            // does not reserve a name a readable one could have used.
            if (facePaths.ContainsKey(faceName))
                continue;

            if (!TryReadFaceMetadata(fontPathFile, face, faceIndex, collectionMetadata, out var metadata))
                continue;

            facePaths.Add(faceName, new FaceLocation(fontPathFile, faceIndex));
            fontInfoList.Add(new FontFileInfo(faceName, metadata));
        }
    }

    /// <summary>
    /// How many faces a font file holds and whether it is a collection. A file that cannot be read
    /// is logged and answers false.
    /// </summary>
    private static bool TryCountFaces(string fontPathFile, out int faceCount, out bool isCollection)
    {
        try
        {
            isCollection = TrueTypeCollection.TryGetFaceCount(fontPathFile, out faceCount);
            return true;
        }
        catch (System.Exception e) when (!Unrecoverable.Is(e))
        {
            LogError(e.ToString());
            faceCount = 0;
            isCollection = false;
            return false;
        }
    }

    /// <summary>
    /// The index a face is read at and the name it is known by.
    /// </summary>
    private static (int FaceIndex, string FaceName) FaceIndexAndName(string fileName, int face, bool isCollection)
    {
        // Only a member of a collection carries an index; a single font keeps the plain
        // file name it has always been known by.
        return isCollection
            ? (face, TrueTypeCollection.FaceName(fileName, face))
            : (-1, fileName);
    }


    /// <summary>
    /// Reads a collection in one go where the backend can, so that a file holding a dozen faces
    /// is opened once rather than a dozen times. Answers null when that fails.
    /// </summary>
    private FontMetadata[] TryReadCollectionMetadata(string fontPathFile, int faceCount)
    {
        try
        {
            return ReadCollectionMetadata(fontPathFile, faceCount);
        }
        catch (System.Exception e) when (!Unrecoverable.Is(e))
        {
            // One unreadable face must not cost the rest of the collection, so fall back to
            // reading them one at a time, where a failure stays with the face that caused it.
            LogError(e.ToString());
            return null;
        }
    }


    /// <summary>
    /// One face's metadata: taken from the collection's where that was read in one go, and read
    /// on its own otherwise. A face that cannot be read is logged and answers false.
    /// </summary>
    private bool TryReadFaceMetadata(string fontPathFile, int face, int faceIndex,
        FontMetadata[] collectionMetadata, out FontMetadata metadata)
    {
        try
        {
            metadata = collectionMetadata != null
                ? collectionMetadata[face]
                : ReadFontMetadata(fontPathFile, faceIndex);
            return true;
        }
        catch (System.Exception e) when (!Unrecoverable.Is(e))
        {
            LogError(e.ToString());
            metadata = default;
            return false;
        }
    }


    /// <summary>
    /// Groups the faces found into families, keyed by lower-cased family name. A family that
    /// cannot be built is logged and left out.
    /// </summary>
    private static Dictionary<string, FontFamilyModel> BuildFamilies(List<FontFileInfo> fontInfoList)
    {
        var installedFonts = new Dictionary<string, FontFamilyModel>();

        // Deserialize all font families
        foreach (var familyGroup in fontInfoList.GroupBy(info => info.FamilyName))
            try
            {
                var familyName = familyGroup.Key;
                var family = DeserializeFontFamily(familyName, familyGroup);
                installedFonts.Add(familyName.ToLower(), family);
            }
            catch (System.Exception e) when (!Unrecoverable.Is(e))
            {
                LogError(e.ToString());
            }

        return installedFonts;
    }


    /// <summary>
    /// Files a family's faces under the style each one reports.
    /// </summary>
    /// <remarks>
    /// A family shipping a single file used to be filed under Regular whatever that file
    /// actually was. It made resolution succeed, since Regular is what the fallback looked for
    /// last, but it also meant a family with nothing but a bold face answered a request for
    /// bold with a face it believed was regular - and now that the missing weight is drawn on
    /// rather than ignored, that would stroke a bold face bolder still. The candidate list in
    /// <see cref="Candidates"/> reaches every style, so filing by the truth still resolves.
    /// </remarks>
    private static FontFamilyModel DeserializeFontFamily(string fontFamilyName, IEnumerable<FontFileInfo> fontList)
    {
        var font = new FontFamilyModel { Name = fontFamilyName };

        foreach (var info in fontList)
        {
            var style = info.GuessFontStyle();
            if (!font.FontFiles.ContainsKey(style))
                font.FontFiles.Add(style, info.FaceName);
        }

        return font;
    }

    /// <param name="faceName">A face name handed out by <see cref="ResolveTypeface"/>.</param>
    public virtual byte[] GetFont(string faceName)
    {
        EnsureInitialized();

        if (!_facePaths.TryGetValue(faceName, out var location))
            throw new System.IO.FileNotFoundException(
                "No font file was discovered for the face name '" + faceName + "'.", faceName);

        var bytes = System.IO.File.ReadAllBytes(location.Path);

        // A collection is taken apart here, because nothing below this point understands one.
        return location.FaceIndex < 0
            ? bytes
            : TrueTypeCollection.ExtractFace(bytes, location.FaceIndex);
    }

    /// <summary>
    /// Gets or sets whether an unresolvable family answers null instead of falling back to another
    /// face. Null lets the caller decide what to do; the fallback keeps the document renderable.
    /// </summary>
    public bool NullIfFontNotFound { get; set; } = false;

    /// <summary>
    /// Resolves a family name and style to an installed face, falling back to a face of the same
    /// family in another style, and then to the default family, unless
    /// <see cref="NullIfFontNotFound"/> says to answer null instead.
    /// </summary>
    /// <exception cref="System.IO.FileNotFoundException">No fonts are installed on this device.</exception>
    public virtual FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        EnsureInitialized();

        if (_installedFonts.Count == 0)
            throw new System.IO.FileNotFoundException("No Fonts installed on this device!");

        if (_installedFonts.TryGetValue(familyName.ToLower(), out var family))
            return Resolve(family, isBold, isItalic);

        return NullIfFontNotFound ? null : new FontResolverInfo(_installedFonts.First().Value.FontFiles.First().Value);
    }


    /// <summary>
    /// Picks the closest face the family ships a file for, and asks the renderer to supply what
    /// is missing.
    /// </summary>
    /// <remarks>
    /// Falling back to the regular face and saying nothing, as this used to, renders bold text
    /// as regular text without a word about it. PdfPinata can draw a bold or an italic that
    /// a family has no file for - stroking the glyphs, and skewing them - but only if it is
    /// told to, and this is where it is told.
    /// </remarks>
    private static FontResolverInfo Resolve(FontFamilyModel family, bool isBold, bool isItalic)
    {
        foreach (var candidate in Candidates(isBold, isItalic))
        {
            if (family.FontFiles.TryGetValue(candidate.Key, out var faceName))
                return new FontResolverInfo(faceName, candidate.Value);
        }

        // The family is keyed by the four styles the candidates cover, so this is unreachable
        // for a family that has any file at all. Kept so that a family which somehow has one
        // under another key still resolves to something.
        return new FontResolverInfo(family.FontFiles.First().Value);
    }


    /// <summary>
    /// The faces to try for a requested style, nearest first, each with what the renderer would
    /// have to simulate if that face were used.
    /// </summary>
    /// <remarks>
    /// Only the axis that is missing gets simulated. Asked for bold italic by a family shipping
    /// a bold, the bold file is used and only the slant is drawn on - a simulated italic over a
    /// real bold beats simulating both over the regular face.
    /// <para>
    /// Neither weight nor slant can be taken away, so a request for a plainer face than the
    /// family ships is answered with the nearest one and no simulation at all.
    /// </para>
    /// </remarks>
    private static IEnumerable<KeyValuePair<XFontStyle, XStyleSimulations>> Candidates(bool isBold, bool isItalic)
    {
        const XStyleSimulations none = XStyleSimulations.None;
        const XStyleSimulations bold = XStyleSimulations.BoldSimulation;
        const XStyleSimulations italic = XStyleSimulations.ItalicSimulation;
        const XStyleSimulations both = XStyleSimulations.BoldItalicSimulation;

        if (isBold && isItalic)
        {
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Bold, italic);
            yield return Candidate(XFontStyle.Italic, bold);
            yield return Candidate(XFontStyle.Regular, both);
        }
        else if (isBold)
        {
            yield return Candidate(XFontStyle.Bold, none);
            yield return Candidate(XFontStyle.Regular, bold);
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Italic, bold);
        }
        else if (isItalic)
        {
            yield return Candidate(XFontStyle.Italic, none);
            yield return Candidate(XFontStyle.Regular, italic);
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Bold, italic);
        }
        else
        {
            yield return Candidate(XFontStyle.Regular, none);
            yield return Candidate(XFontStyle.Italic, none);
            yield return Candidate(XFontStyle.Bold, none);
            yield return Candidate(XFontStyle.BoldItalic, none);
        }
    }


    private static KeyValuePair<XFontStyle, XStyleSimulations> Candidate(XFontStyle style, XStyleSimulations simulations)
    {
        return new KeyValuePair<XFontStyle, XStyleSimulations>(style, simulations);
    }
}
