using System;
using System.IO;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The glyphs of a TrueType font program embedded in the file as <c>/FontFile2</c>, chosen by
/// the PDF's rules for a simple TrueType font or a <c>CIDFontType2</c> font.
/// </summary>
/// <remarks>
/// <para>
/// The font program is read here, glyph by glyph, and never handed to a platform's font engine:
/// DirectWrite and FreeType draw a broken subset differently from each other, and a page has to
/// look the same everywhere.
/// </para>
/// <para>
/// Nothing is read until the first outline is asked for, so a font used only for invisible text
/// costs nothing, and a font program that cannot be read is reported against each glyph shown in
/// it, by <see cref="RenderFont"/>, rather than thrown where the font is selected.
/// </para>
/// </remarks>
internal sealed class EmbeddedTrueTypeGlyphSource : PageGlyphSource
{
    private readonly PdfDictionary _font;
    private readonly PdfDictionary _descendant;
    private readonly PdfDictionary _descriptor;
    private readonly PdfDictionary _fontFile;

    private TrueTypeGlyphs _glyphs;
    private string _unreadable;
    private SimpleFontGlyphs _simple;
    private bool _cidMapRead;
    private string _cidMapUnreadable;

    /// <summary>The <c>/CIDToGIDMap</c> stream, or null for <c>/Identity</c>.</summary>
    private byte[] _cidToGid;

    /// <summary>Reads nothing: the font program is read when the first outline is asked for.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    /// <param name="descriptor">The font descriptor that holds <paramref name="fontFile"/>.</param>
    /// <param name="fontFile">The <c>/FontFile2</c> stream.</param>
    internal EmbeddedTrueTypeGlyphSource(PdfDictionary font, PdfDictionary descendant, PdfDictionary descriptor,
        PdfDictionary fontFile)
    {
        _font = font;
        _descendant = descendant;
        _descriptor = descriptor;
        _fontFile = fontFile;
    }

    public override PagePath GetOutline(int code)
    {
        var glyph = GlyphOf(code);
        return glyph < 0 ? null : OutlineOfGlyph(glyph);
    }

    internal override int GlyphOf(int code)
    {
        var glyphs = Glyphs();
        return _descendant != null
            ? GlyphOfCid(code, glyphs)
            : (_simple ??= new SimpleFontGlyphs(glyphs, _font, _descriptor)).GlyphOf(code);
    }

    internal override PagePath OutlineOfGlyph(int glyph)
    {
        var glyphs = Glyphs();
        var outline = glyphs.GetOutline(glyph);
        return outline == null ? null : TrueTypeOutlinePaths.ToPagePath(outline, glyphs.UnitsPerEm);
    }

    internal override long HeldBytes => (_glyphs?.Length ?? 0) + (_cidToGid?.Length ?? 0);

    internal override void Release()
    {
        _glyphs = null;
        _simple = null;
        _cidToGid = null;
        _cidMapRead = false;
    }

    /// <summary>
    /// The font program, read the first time it is needed. One that cannot be read throws for
    /// every glyph asked for, with the same message, and is not read again.
    /// </summary>
    private TrueTypeGlyphs Glyphs()
    {
        if (_glyphs != null)
            return _glyphs;
        if (_unreadable != null)
            throw new InvalidDataException(_unreadable);

        try
        {
            if (!PdfContentStreams.TryGetContent(_fontFile, out var bytes))
                throw new InvalidDataException("The embedded font program's stream cannot be decoded.");
            return _glyphs = TrueTypeGlyphs.Read(bytes);
        }
        catch (InvalidDataException ex)
        {
            _unreadable = ex.Message;
            throw;
        }
    }

    /// <summary>
    /// The glyph of a CID, through <c>/CIDToGIDMap</c>: <c>/Identity</c>, which is what an absent
    /// entry means, or a stream of two bytes for each CID. A CID past the end of the stream, or
    /// mapped to a glyph the font has not, has none. A CID mapped to glyph 0 draws glyph 0, as
    /// viewers do: some files keep a real glyph there, such as a check box's tick.
    /// </summary>
    private int GlyphOfCid(int cid, TrueTypeGlyphs glyphs)
    {
        if (!_cidMapRead)
        {
            _cidMapRead = true;
            var map = PdfReference.Dereference(_descendant.Elements["/CIDToGIDMap"]) as PdfDictionary;
            if (map != null && !PdfContentStreams.TryGetContent(map, out _cidToGid))
                _cidMapUnreadable = "The font's /CIDToGIDMap stream cannot be decoded.";
        }

        if (_cidMapUnreadable != null)
            throw new InvalidDataException(_cidMapUnreadable);

        var glyph = cid;
        if (_cidToGid != null)
        {
            var at = 2 * cid;
            glyph = at + 1 < _cidToGid.Length ? _cidToGid[at] << 8 | _cidToGid[at + 1] : -1;
        }

        return glyph < glyphs.GlyphCount ? glyph : -1;
    }

    /// <summary>
    /// The glyph of each code of a simple TrueType font, by ISO 32000-1 9.6.6.4 where a font has an
    /// <c>/Encoding</c>, and by the code itself where it has none or is symbolic, as viewers read them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>By name</b>: the code's glyph name, from <c>/Differences</c> or the base encoding, to a
    /// character, to a glyph in the Unicode <c>cmap</c>, then in the (1,0) <c>cmap</c> by the
    /// character's MacRomanEncoding code, and only then in <c>post</c>. <b>By code</b>: the code
    /// looked up in a <c>cmap</c> as it is.
    /// </para>
    /// <para>
    /// A nonsymbolic font with no <c>/Encoding</c> at all is read by code, though ISO 32000-1 gives
    /// it StandardEncoding: pdf.js and MuPDF read it by code, and a page is judged against them.
    /// </para>
    /// <para>
    /// Real files often say a font is symbolic when it is not, or the reverse, and embed subsets
    /// with one <c>cmap</c> subtable or none, so the other way is tried where the first finds
    /// nothing. Except that where the name gave a character the font has no glyph for, its code is
    /// not looked up in the Unicode or (1,0) <c>cmap</c>, where it means another character: a
    /// euro the font lacks would be drawn as whatever the font keeps at 0x80. Only the (3,0)
    /// <c>cmap</c>, which the name could not be looked up in, is tried. The same holds for a
    /// <c>/Differences</c> name that is no character at all.
    /// </para>
    /// </remarks>
    private sealed class SimpleFontGlyphs
    {
        private readonly TrueTypeGlyphs _glyphs;
        private readonly bool _symbolic;
        private readonly bool _byCode;
        private readonly string _baseEncoding;
        private readonly string[] _differences = new string[256];
        private readonly TrueTypeCharacterMap _unicode;
        private readonly TrueTypeCharacterMap _fullUnicode;
        private readonly TrueTypeCharacterMap _symbol;
        private readonly TrueTypeCharacterMap _macintosh;
        private readonly bool _hasNoCharacterMap;
        private readonly bool _hasNoUsableCharacterMap;
        private readonly int[] _chosen = new int[256];
        private string _unreadableSubtable;
        private bool _subtableFailed;

        public SimpleFontGlyphs(TrueTypeGlyphs glyphs, PdfDictionary font, PdfDictionary descriptor)
        {
            _glyphs = glyphs;
            var encoding = PdfReference.Dereference(font.Elements["/Encoding"]);
            if (PdfItemValues.TryGetName(encoding, out var name))
            {
                _baseEncoding = name;
            }
            else if (encoding is PdfDictionary dictionary)
            {
                PdfItemValues.TryGetName(dictionary.Elements["/BaseEncoding"], out _baseEncoding);
                ReadDifferences(dictionary.Elements.GetArray("/Differences"));
            }

            // ISO 32000-1 9.6.6.4: a font that names WinAnsiEncoding or MacRomanEncoding is read
            // by glyph name whatever its flags say, as one whose Nonsymbolic flag is set is.
            var flags = PdfItemValues.TryGetNumber(descriptor.Elements["/Flags"], out var stated) ? (int)stated : 0;
            _symbolic = (flags & SymbolicFlag) != 0 && (flags & NonsymbolicFlag) == 0
                && _baseEncoding is not ("/WinAnsiEncoding" or "/MacRomanEncoding");
            _byCode = _symbolic || encoding == null;

            // A nonsymbolic font whose /Encoding names no base encoding has the standard one.
            if (_baseEncoding == null && !_symbolic)
                _baseEncoding = "/StandardEncoding";

            // A font may map the basic multilingual plane in one subtable and every character in
            // another. Both are kept, because a name such as u1F600 is only in the second.
            _fullUnicode = glyphs.CharacterMap(3, 10) ?? glyphs.CharacterMap(0, 4) ?? glyphs.CharacterMap(0, 6);
            _unicode = glyphs.CharacterMap(3, 1) ?? glyphs.CharacterMap(0, 3) ?? _fullUnicode;
            _symbol = glyphs.CharacterMap(3, 0);
            _macintosh = glyphs.CharacterMap(1, 0);
            _hasNoUsableCharacterMap = _unicode == null && _symbol == null && _macintosh == null;
            _hasNoCharacterMap = !glyphs.HasCharacterMaps;

            Array.Fill(_chosen, NotChosen);
        }

        /// <summary>The glyph of a code, or -1 where the font has none for it.</summary>
        /// <exception cref="InvalidDataException">
        /// No subtable had a glyph for the code, and one that was asked could not be read.
        /// </exception>
        public int GlyphOf(int code)
        {
            if ((uint)code > 255)
                return -1;
            if (_chosen[code] == NotChosen)
            {
                _subtableFailed = false;
                var glyph = Choose(code);
                if (glyph == 0 && code == 0 && _differences[0] != ".notdef" && NotdefDrawsSomething())
                    _chosen[code] = 0;
                else
                    _chosen[code] = glyph != 0 ? glyph : _subtableFailed ? Unreadable : -1;
            }

            return _chosen[code] == Unreadable ? throw new InvalidDataException(_unreadableSubtable) : _chosen[code];
        }

        private int Choose(int code)
        {
            // .notdef is a name for no glyph, and is not a reason to look the code up some other way.
            if (_differences[code] == ".notdef")
                return 0;

            int glyph;
            if (_byCode)
            {
                glyph = ByCode(code);
                if (glyph == 0 && (_symbolic || _hasNoUsableCharacterMap))
                    glyph = ByName(code, out _);
            }
            else
            {
                // A name from /Differences that is no character and no glyph in post draws
                // nothing, as in pdf.js: the code means another character in a Unicode cmap.
                glyph = ByName(code, out var named);
                if (glyph == 0)
                    glyph = named || _differences[code] != null ? InSymbolCmap(code) : ByCode(code);
            }

            // A subset with no cmap at all is one whose producer numbered its glyphs by the codes.
            if (glyph == 0 && _hasNoCharacterMap && code < _glyphs.GlyphCount)
                glyph = code;
            return glyph;
        }

        /// <summary>
        /// The code's glyph name, from <c>/Differences</c> or the base encoding, to a character,
        /// to a glyph: in the Unicode <c>cmap</c>, then the (1,0) one, then by <c>post</c> name.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="named">Whether the name stands for a character.</param>
        private int ByName(int code, out bool named)
        {
            var name = _differences[code];
            var character = name != null ? GlyphNames.CharacterOf(name) : GlyphNames.CharacterOf(_baseEncoding, code);
            named = character > 0;

            var glyph = named ? Look(_unicode, character) : 0;
            if (glyph == 0 && named && _fullUnicode != _unicode)
                glyph = Look(_fullUnicode, character);
            if (glyph == 0 && named && _macintosh != null)
            {
                var macCode = GlyphNames.MacRomanCodeOf(character);
                if (macCode >= 0)
                    glyph = Look(_macintosh, macCode);
            }
            if (glyph == 0 && name != null)
                glyph = Math.Max(0, _glyphs.GlyphNamed(name));
            if (glyph == 0 && name == null)
            {
                foreach (var alias in GlyphNames.NamesOf(character))
                {
                    glyph = Math.Max(0, _glyphs.GlyphNamed(alias));
                    if (glyph != 0)
                        break;
                }
            }

            return glyph;
        }

        /// <summary>
        /// The code itself, looked up in each <c>cmap</c>: for a symbolic font the (3,0) one first,
        /// then the (1,0) one, then the Unicode one; for any other the Unicode one first, then
        /// the (1,0) one, then the (3,0) one.
        /// </summary>
        private int ByCode(int code)
        {
            if (_symbolic)
            {
                var glyph = InSymbolCmap(code);
                if (glyph == 0)
                    glyph = Look(_macintosh, code);
                return glyph != 0 ? glyph : Look(_unicode, code);
            }

            var mapped = Look(_unicode, code);
            if (mapped == 0)
                mapped = Look(_macintosh, code);
            return mapped != 0 ? mapped : InSymbolCmap(code);
        }

        /// <summary>The code in the (3,0) subtable, where a symbol font's codes lie at 0xF000 and above as often as not.</summary>
        private int InSymbolCmap(int code)
        {
            if (_symbol == null)
                return 0;

            foreach (var high in SymbolPages)
            {
                var glyph = Look(_symbol, high | code);
                if (glyph != 0)
                    return glyph;
            }

            return 0;
        }

        /// <summary>
        /// The glyph a subtable maps a code to, or 0 where it maps it to none, to a glyph the font
        /// has not, or cannot be read: a malformed subtable is no reason not to try the next.
        /// </summary>
        private int Look(TrueTypeCharacterMap map, int code)
        {
            if (map == null)
                return 0;

            try
            {
                var glyph = map.GlyphOf(code);
                return (uint)glyph < (uint)_glyphs.GlyphCount ? glyph : 0;
            }
            catch (InvalidDataException ex)
            {
                _unreadableSubtable ??= ex.Message;
                _subtableFailed = true;
                return 0;
            }
        }

        /// <summary>
        /// Whether glyph 0 has contours. Code 0 that nothing else maps draws it where it does, as
        /// pdf.js does, because some files keep a real glyph there.
        /// </summary>
        private bool NotdefDrawsSomething()
        {
            try
            {
                return _glyphs.GetOutline(0) is { IsEmpty: false };
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private void ReadDifferences(PdfArray differences)
        {
            if (differences == null)
                return;

            var code = -1;
            foreach (var item in differences.Elements)
            {
                var value = PdfReference.Dereference(item);
                if (PdfItemValues.TryGetNumber(value, out var number))
                {
                    code = number is >= 0 and <= 255 ? (int)number : -1;
                }
                else if (PdfItemValues.TryGetName(value, out var glyphName) && code is >= 0 and <= 255)
                {
                    _differences[code++] = glyphName.Substring(1);
                }
            }
        }

        private static readonly int[] SymbolPages = [0x0000, 0xF000, 0xF100, 0xF200];

        private const int NotChosen = int.MinValue;
        private const int Unreadable = -2;
        private const int SymbolicFlag = 1 << 2;
        private const int NonsymbolicFlag = 1 << 5;
    }
}
