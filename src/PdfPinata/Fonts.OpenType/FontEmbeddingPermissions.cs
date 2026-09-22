using System;
using System.Globalization;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// What a face's OS/2 <c>fsType</c> says about putting it in a document: whether it may be
/// embedded at all, and whether it may be cut down to the glyphs a document draws.
/// </summary>
/// <remarks>
/// <para>
/// Bits 0-3 are the usage permissions and are read as one value: none set is Installable,
/// <c>0x0002</c> Restricted License, <c>0x0004</c> Preview &amp; Print and <c>0x0008</c> Editable.
/// Only Restricted License forbids embedding. The current OpenType specification allows at most one
/// of them, but versions 0 to 2 of the table did not, and the specification says a font setting
/// several is read by the least restrictive — so an old font setting both Restricted License and
/// Editable is Editable, and may be embedded.
/// </para>
/// <para>
/// Bit 9, <c>0x0200</c>, is Bitmap Embedding Only: only the font's bitmaps may be embedded. This
/// library embeds outlines and nothing else, so for it the bit forbids embedding as surely as
/// Restricted License does. Bit 8, <c>0x0100</c>, is No Subsetting: the font may be embedded, but
/// only whole. Both were reserved and zero before version 2 of the table, so reading them whatever
/// the version is safe.
/// </para>
/// <para>
/// A face with no OS/2 table says nothing, and nothing said is read as Installable: refusing a font
/// for lacking a table would refuse fonts no license restricts.
/// </para>
/// </remarks>
internal readonly struct FontEmbeddingPermissions
{
    private const ushort _restrictedLicense = 0x0002;
    private const ushort _previewAndPrint = 0x0004;
    private const ushort _editable = 0x0008;
    private const ushort _noSubsetting = 0x0100;
    private const ushort _bitmapEmbeddingOnly = 0x0200;

    private readonly ushort _fsType;

    private FontEmbeddingPermissions(ushort fsType) => _fsType = fsType;

    /// <summary>
    /// The permissions <paramref name="face"/> declares.
    /// </summary>
    public static FontEmbeddingPermissions Of(OpenTypeFontface face)
        => new(face.os2?.fsType ?? 0);

    /// <summary>
    /// Whether the face may be embedded only whole.
    /// </summary>
    public bool ForbidsSubsetting => (_fsType & _noSubsetting) != 0;

    /// <summary>
    /// Why the face may not be embedded, or null when it may.
    /// </summary>
    public string Refusal
    {
        get
        {
            // The least restrictive usage bit wins, and either of these permits embedding whatever
            // else is set beside it.
            var permitsEmbedding = (_fsType & (_editable | _previewAndPrint)) != 0;

            if (!permitsEmbedding && (_fsType & _restrictedLicense) != 0)
            {
                return "its OS/2 fsType is 0x" + Hex + ", Restricted License embedding: the font must "
                       + "not be embedded without its legal owner's permission";
            }

            if ((_fsType & _bitmapEmbeddingOnly) != 0)
            {
                return "its OS/2 fsType is 0x" + Hex + ", Bitmap Embedding Only: only the font's "
                       + "bitmaps may be embedded, and PdfPinata embeds outlines";
            }

            return null;
        }
    }

    private string Hex => _fsType.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Throws when <paramref name="face"/> may not be embedded, naming the face and the restriction.
    /// </summary>
    public static void EnsureEmbeddable(OpenTypeFontface face)
    {
        var refusal = Of(face).Refusal;
        if (refusal == null)
            return;

        throw new InvalidOperationException(
            "The font '" + face.FullFaceName + "' cannot be embedded, because " + refusal + ". "
            + "PdfDocumentOptions.RespectFontEmbeddingRestrictions is set, so the document refuses the "
            + "font rather than embed it. Use a font whose licence permits embedding.");
    }
}
