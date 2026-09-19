using System.IO;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Produces the detached signature that goes into a signature dictionary's <c>/Contents</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that keeps cryptography out of the core package. Everything a PDF signature
/// needs that is <em>about PDF</em> — the field, the widget, the placeholder, the byte range and the
/// patching — lives in <see cref="PdfSigner"/>; everything that is about certificates and CMS lives
/// behind this interface, in <c>PdfPinata.Signing</c> or in an implementation of your own.
/// </para>
/// <para>
/// An implementation that talks to a smart card, an HSM or a remote signing service is the reason
/// <see cref="Sign"/> takes a <see cref="Stream"/> rather than a byte array: the content to be signed
/// is most of the file, and a signer able to hash it in chunks should not be made to hold a second
/// copy of it.
/// </para>
/// </remarks>
public interface IPdfSigner
{
    /// <summary>
    /// The value written to the signature dictionary's <c>/SubFilter</c>, naming the format of what
    /// <see cref="Sign"/> returns — <c>/ETSI.CAdES.detached</c> for PAdES, or
    /// <c>/adbe.pkcs7.detached</c> for the older form.
    /// </summary>
    string SubFilter { get; }

    /// <summary>
    /// How many bytes to reserve for the signature, before it is known how long it will be.
    /// </summary>
    /// <remarks>
    /// The reservation is unavoidable: the signature covers the file it sits in, so the space it will
    /// occupy has to be committed to before the bytes being signed exist. Any surplus is padded with
    /// zeros, so an estimate that is too generous costs only file size, while one that is too small
    /// makes <see cref="PdfSigner"/> throw rather than produce a truncated signature. Estimate high.
    /// </remarks>
    int EstimatedSignatureSize { get; }

    /// <summary>
    /// Signs the content of the stream and answers the detached signature, ready to be written into
    /// <c>/Contents</c> as a hexadecimal string.
    /// </summary>
    /// <param name="content">
    /// The bytes the signature covers: the whole file apart from the hole the signature itself will
    /// be written into. The stream is positioned at the start and is read-only.
    /// </param>
    byte[] Sign(Stream content);
}
