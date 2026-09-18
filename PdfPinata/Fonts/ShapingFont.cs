using System;

namespace PdfPinata.Fonts;

/// <summary>
/// The face a run is shaped against, as an <see cref="ITextShaper"/> is shown it: which file
/// answered for the family, the bytes of it, and the size to shape at.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that a shaper never resolves a family for itself. It is handed the face this
/// library has already picked and will really embed and draw from, so the two cannot disagree
/// about which file a family means - and they must not, because the glyph identifiers a shaper
/// returns are indices into one particular file and nothing later checks which.
/// </para>
/// <para>
/// It is also why the seam does not simply pass an
/// <see cref="PdfPinata.Drawing.XFont"/>: everything a shaper needs from one - the typeface,
/// the face name, the font source - lives on types this assembly keeps internal.
/// </para>
/// </remarks>
public sealed class ShapingFont
{
    /// <summary>
    /// Describes a face to shape against. This library builds one for every
    /// <see cref="PdfPinata.Drawing.XFont"/> it shapes; the constructor is public so that
    /// somebody writing an <see cref="ITextShaper"/> can build one from a font file and test their
    /// shaper without a document, a page or a font resolver in the way.
    /// </summary>
    /// <param name="familyName">The family the face names itself as.</param>
    /// <param name="faceName">The name of the face.</param>
    /// <param name="key">A value distinct per face and stable for one; see <see cref="Key"/>.</param>
    /// <param name="isBold">Whether the face itself is bold.</param>
    /// <param name="isItalic">Whether the face itself is italic.</param>
    /// <param name="emSize">The size in points the text will be drawn at.</param>
    /// <param name="unitsPerEm">The design units per em of the face; must be positive.</param>
    /// <param name="bytes">The bytes of the font file.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unitsPerEm"/> is not positive.</exception>
    public ShapingFont(string familyName, string faceName, string key,
        bool isBold, bool isItalic, double emSize, int unitsPerEm, byte[] bytes)
    {
        if (unitsPerEm <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsPerEm),
                "A face has a positive number of design units per em; advances are read against it.");

        // Refused here rather than worked around by whoever reads it. A shaper caches its parsed
        // face under this key, so two faces sharing one - which is what every keyless face would
        // do - means the second is shaped with the first's glyph identifiers. Those identifiers
        // index a font file, nothing downstream checks which file, and the text comes out as
        // whatever the other face happens to have at those indices: wrong, silently, and only on
        // the pages that use the second font.
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "A shaping font needs a key that is distinct per face and stable for one, because "
                + "that is what a shaper caches its parsed face under.", nameof(key));

        FamilyName = familyName;
        FaceName = faceName;
        Key = key;
        IsBold = isBold;
        IsItalic = isItalic;
        EmSize = emSize;
        UnitsPerEm = unitsPerEm;
        Bytes = bytes ?? Array.Empty<byte>();
    }

    /// <summary>
    /// The family the face belongs to, as the face itself names it - which is not always the
    /// family that was asked for, a resolver being free to answer with something else.
    /// </summary>
    public string FamilyName { get; }

    /// <summary>
    /// The name of the resolved face.
    /// </summary>
    public string FaceName { get; }

    /// <summary>
    /// The key this library caches the face under. Never null or blank: distinct faces have
    /// distinct keys and the same face always has the same one, so a shaper wanting to keep a
    /// parsed face of its own between calls can key it on this and nothing else.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Whether the resolved face is bold. False for a face that is being emboldened by simulation,
    /// which is a drawing matter and none of a shaper's business.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    /// Whether the resolved face is italic. False for a face being slanted by simulation.
    /// </summary>
    public bool IsItalic { get; }

    /// <summary>
    /// The size in points the text will be drawn at. A shaper needs it only for the few features
    /// that depend on size - optical sizing, hinted positioning - because advances come back in
    /// design units and are scaled afterwards.
    /// </summary>
    public double EmSize { get; }

    /// <summary>
    /// The design units per em of the face. <see cref="ShapedGlyph.Advance"/> and the offsets are
    /// in these units, so a shaper working in any other scale has to convert before returning.
    /// </summary>
    public int UnitsPerEm { get; }

    /// <summary>
    /// The bytes of the font file. Read-only rather than an array, because this is the very buffer
    /// the library holds the face in and will write into the document - a copy handed out to be
    /// modified would embed something other than what was shaped.
    /// </summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{FaceName} at {EmSize}pt, {UnitsPerEm} units per em";
}
