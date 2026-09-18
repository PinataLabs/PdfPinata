namespace PdfPinata.Signing;

/// <summary>
/// Which flavour of detached CMS signature to produce, and so which <c>/SubFilter</c> to declare.
/// </summary>
public enum PdfSignatureFormat
{
    /// <summary>
    /// PAdES: a CAdES-BES signature carrying the signing certificate's own identity as a signed
    /// attribute, declared as <c>/ETSI.CAdES.detached</c>.
    /// </summary>
    /// <remarks>
    /// The one to use. Binding the certificate into what is signed is what stops an attacker
    /// substituting a different certificate for the same signature, and it is required by the
    /// European regulation on electronic signatures. This is PAdES baseline level <b>B-B</b>: B-T
    /// adds a trusted timestamp and B-LT the revocation data to validate it long after the fact,
    /// and neither is implemented here.
    /// </remarks>
    Pades = 0,

    /// <summary>
    /// The older Adobe form, declared as <c>/adbe.pkcs7.detached</c>.
    /// </summary>
    /// <remarks>
    /// For readers too old to know what a CAdES signature is. It is a plain PKCS#7 detached
    /// signature with no signing-certificate attribute, so prefer <see cref="Pades"/> unless
    /// something specifically requires this.
    /// </remarks>
    Pkcs7 = 1
}
