using System;
using System.Collections.Generic;
using System.IO;

using PdfPinata.Drawing;
using PdfPinata.Fonts;


namespace PdfPinata.Utils;

/// <summary>
/// Serves fonts from bytes the app supplies, and never touches the file system.
/// </summary>
/// <remarks>
/// <para>
/// This is the resolver for a platform with no font files to find - Blazor WebAssembly above
/// all, where <see cref="FontResolverBase"/> has nothing to scan - and for an app that would
/// rather ship its fonts than depend on what a server happens to have installed. It needs no
/// imaging backend, so it works with PdfPinata.Skia, with PdfPinata.ImageSharp or with neither.
/// </para>
/// <para>
/// Each font names its own family and style: <see cref="AddFont(byte[])"/> reads them from the
/// font, and a TrueType collection adds every face it holds. A face is chosen for a request by the
/// same rule <see cref="FontResolverBase"/> uses - the nearest face first, then only the missing
/// bold or italic simulated.
/// </para>
/// <code>
/// var fonts = new InMemoryFontResolver();
/// fonts.AddFont(await http.GetByteArrayAsync("fonts/LiberationSans-Regular.ttf"));
/// fonts.AddFont(await http.GetByteArrayAsync("fonts/LiberationSans-Bold.ttf"));
/// fonts.AddAlias("Arial", "Liberation Sans");
/// GlobalFontSettings.FontResolver = fonts;
/// </code>
/// <para>
/// Add every font before the first document uses one. The library asks the resolver about each
/// family and style once and caches the answer for the life of the process, so a font added
/// after its family was asked for does not change what that family resolves to. Adding is safe
/// from several threads at once, so the resolver can serve a server as well as a browser.
/// </para>
/// </remarks>
public sealed class InMemoryFontResolver : IFontResolver
{
    private readonly object _lock = new();

    /// <summary>The faces of each family, by style, as the face names handed out.</summary>
    private readonly Dictionary<string, Dictionary<XFontStyle, string>> _families = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The bytes of each face, standalone, by the face name handed out.</summary>
    private readonly Dictionary<string, byte[]> _faces = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The family of the first face added; the default unless one is set.</summary>
    private string _firstFamily;

    private string _defaultFontName;

    /// <summary>
    /// Gets or sets the family name used when a document asks for no font in particular, and the
    /// family an unknown family falls back to. Unless set, the family of the first font added.
    /// </summary>
    /// <remarks>
    /// It may name an alias. If it names neither a family nor an alias, an unknown family falls
    /// back to the first family added instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Read when it was never set and no font has been added.
    /// </exception>
    /// <exception cref="ArgumentException">Set to null or an empty string.</exception>
    public string DefaultFontName
    {
        get
        {
            lock (_lock)
            {
                if (_defaultFontName != null)
                    return _defaultFontName;

                return _firstFamily ?? throw NoFontsAdded();
            }
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("A default font name must name a family.", nameof(value));

            lock (_lock)
                _defaultFontName = value;
        }
    }

    /// <summary>
    /// Gets or sets whether an unknown family answers null instead of falling back to the default
    /// family. Null lets the caller decide what to do; the fallback keeps the document renderable.
    /// </summary>
    public bool NullIfFontNotFound { get; set; }

    /// <summary>
    /// Gets or sets how bold is simulated for a family with no bold face of its own.
    /// <see cref="XBoldSimulation.Default"/> unless set; <see cref="XBoldSimulation.WordCompatible"/>
    /// draws a simulated bold at the regular face's advances, as Microsoft Word does.
    /// </summary>
    /// <remarks>
    /// Read when a typeface is first resolved, and cached with the rest of the answer - so set it
    /// before the resolver is registered.
    /// </remarks>
    public XBoldSimulation BoldSimulation
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = XBoldSimulation.Default;

    /// <summary>
    /// Adds a font, or every face of a TrueType collection, under the family name and style the
    /// font itself declares.
    /// </summary>
    /// <param name="data">The whole font file, as it would be on disk. The resolver keeps a copy.</param>
    /// <remarks>
    /// Where a face of the same family and style was added already, the first one is kept, as
    /// <see cref="FontResolverBase"/> keeps the first file it finds.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The bytes are not a font whose family name can be read.
    /// </exception>
    public void AddFont(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // A collection's faces are extracted into new arrays; a single font would be kept as the
        // caller's own array, which the caller is free to reuse.
        Add(TrueTypeCollection.IsCollection(data) ? data : (byte[])data.Clone());
    }

    /// <summary>
    /// Adds a font, or every face of a TrueType collection, read synchronously from a stream -
    /// meant for a font embedded as a resource.
    /// </summary>
    /// <param name="stream">
    /// The font file, read from its current position to its end. The stream is not closed.
    /// </param>
    /// <remarks>
    /// In a browser, a stream from <c>HttpClient</c> cannot be read synchronously, and this throws
    /// when given one. Fetch a font from <c>wwwroot</c> with
    /// <c>await http.GetByteArrayAsync(...)</c> and add the bytes with <see cref="AddFont(byte[])"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The bytes are not a font whose family name can be read.
    /// </exception>
    public void AddFont(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        Add(buffer.ToArray());
    }

    /// <summary>
    /// Answers a request for one family with another - <c>"Arial"</c> with
    /// <c>"Liberation Sans"</c>, say - so that documents and code asking for a font the app does
    /// not ship still get one of the same metrics.
    /// </summary>
    /// <param name="alias">The family name asked for. Matched whatever its case.</param>
    /// <param name="familyName">
    /// The family to answer with. It need not have been added yet, but an alias is not followed
    /// through another alias.
    /// </param>
    /// <remarks>
    /// A family actually added under the alias's name is answered in preference to the alias.
    /// Adding an alias that exists already replaces it.
    /// </remarks>
    /// <exception cref="ArgumentException">Either name is null or empty.</exception>
    public void AddAlias(string alias, string familyName)
    {
        if (string.IsNullOrEmpty(alias))
            throw new ArgumentException("An alias must name a family.", nameof(alias));
        if (string.IsNullOrEmpty(familyName))
            throw new ArgumentException("An alias must name the family it stands for.", nameof(familyName));

        lock (_lock)
            _aliases[alias] = familyName;
    }

    /// <summary>
    /// Resolves a family name and style to the nearest face added, simulating only the bold or
    /// italic it lacks. An alias stands for the family it names. An unknown family falls back to
    /// the default family in the style asked for, unless <see cref="NullIfFontNotFound"/> says to
    /// answer null.
    /// </summary>
    /// <exception cref="InvalidOperationException">No font has been added.</exception>
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        lock (_lock)
        {
            if (_firstFamily == null)
                throw NoFontsAdded();

            var family = Find(familyName);
            if (family == null)
            {
                if (NullIfFontNotFound)
                    return null;

                family = (_defaultFontName == null ? null : Find(_defaultFontName)) ?? _families[_firstFamily];
            }

            return FontFaceMatching.Resolve(family, isBold, isItalic, BoldSimulation);
        }
    }

    /// <summary>Gets the bytes of a face, as a standalone font even where it was added in a collection.</summary>
    /// <param name="faceName">A face name handed out by <see cref="ResolveTypeface"/>.</param>
    /// <exception cref="ArgumentException">This resolver never handed out <paramref name="faceName"/>.</exception>
    public byte[] GetFont(string faceName)
    {
        lock (_lock)
        {
            if (faceName != null && _faces.TryGetValue(faceName, out var bytes))
                return bytes;
        }

        throw new ArgumentException(
            "No font was added to this InMemoryFontResolver under the face name '" + faceName + "'.", nameof(faceName));
    }

    /// <summary>Adds a font or collection whose bytes the resolver owns.</summary>
    private void Add(byte[] data)
    {
        if (!TrueTypeCollection.IsCollection(data))
        {
            AddFace(OpenTypeFontMetadata.Read(data), data);
            return;
        }

        var faceCount = TrueTypeCollection.FaceCount(data);
        var metadata = OpenTypeFontMetadata.ReadAll(data, faceCount);
        for (var face = 0; face < faceCount; face++)
            AddFace(metadata[face], TrueTypeCollection.ExtractFace(data, face));
    }

    private void AddFace(FontMetadata metadata, byte[] face)
    {
        lock (_lock)
        {
            if (!_families.TryGetValue(metadata.FamilyName, out var family))
            {
                family = new Dictionary<XFontStyle, string>();
                _families.Add(metadata.FamilyName, family);
                _firstFamily ??= metadata.FamilyName;
            }

            if (family.ContainsKey(metadata.Style))
                return;

            // The library keys font sources by face name for the life of the process, so a name is
            // never given to other bytes: a family has one face per style, and the first one stays.
            var faceName = "InMemory:" + metadata.FamilyName + ":" + metadata.Style;
            family.Add(metadata.Style, faceName);
            _faces.Add(faceName, face);
        }
    }

    /// <summary>The family a name stands for, directly or through an alias, or null.</summary>
    private Dictionary<XFontStyle, string> Find(string familyName)
    {
        if (familyName == null)
            return null;

        if (_families.TryGetValue(familyName, out var family))
            return family;

        return _aliases.TryGetValue(familyName, out var target) && _families.TryGetValue(target, out family)
            ? family
            : null;
    }

    private static InvalidOperationException NoFontsAdded()
    {
        return new InvalidOperationException(
            "No fonts have been added to the InMemoryFontResolver. Call AddFont with at least one font " +
            "before a document uses one - in a browser there are no system fonts to fall back on.");
    }
}
