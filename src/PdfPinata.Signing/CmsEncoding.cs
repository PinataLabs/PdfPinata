using System;
using System.Formats.Asn1;

namespace PdfPinata.Signing;

/// <summary>
/// The one piece of raw CMS byte-handling more than one class here needs.
/// </summary>
internal static class CmsEncoding
{
    /// <summary>
    /// The encoded signature without the zero padding reserved for it that follows it.
    /// </summary>
    /// <remarks>
    /// The room for a signature is reserved before its length is known, so what is written into
    /// <c>/Contents</c> is the signature followed by however many zeros are left over. Reading the
    /// first DER value out of it is what says where the signature actually ends — its own encoded
    /// length is the only thing that does. Needed both to verify a signature and to read the
    /// certificates it embeds for validation data, which is why this is shared rather than kept as a
    /// private copy in each.
    /// </remarks>
    public static byte[] Trimmed(byte[] contents)
    {
        if (contents == null || contents.Length == 0)
            throw new ArgumentException("The signature is empty.", nameof(contents));

        var reader = new AsnReader(contents, AsnEncodingRules.BER);
        return reader.PeekEncodedValue().ToArray();
    }
}
