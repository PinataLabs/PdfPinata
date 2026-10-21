using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Security;

internal class EncryptorFactory
{
    public static void Create(PdfDocument doc, PdfDictionary dict, out IEncryptor stringEncryptor, out IEncryptor streamEncryptor)
    {
        var filter = dict.Elements.GetName(PdfSecurityHandler.Keys.Filter);
        var v = dict.Elements.GetInteger(PdfSecurityHandler.Keys.V);
        var maxSupportedVersion = 5;
        if (filter != "/Standard" || !(v >= 1 && v <= maxSupportedVersion))
            throw new PdfReaderException(PSSR.UnknownEncryption);

        stringEncryptor = CreateFor(doc, dict, v, "/StrF");
        streamEncryptor = CreateFor(doc, dict, v, "/StmF");
    }

    /// <summary>
    /// Creates and initializes the encryptor for strings or for streams, as the crypt filter the
    /// entry <paramref name="keyName"/> names asks, defaulting to RC4.
    /// </summary>
    private static IEncryptor CreateFor(PdfDocument doc, PdfDictionary dict, int v, string keyName)
    {
        IEncryptor encryptor;
        if (UsesIdentityCryptFilter(dict, v, keyName))
        {
            // Never an entry of /CF, so asking it for a /CFM would answer RC4 and garble plain data.
            // The encryptor inside still derives the key and validates the password, and up to
            // revision 4 that is RC4's whatever the crypt filters say.
            var revision = dict.Elements.GetInteger(PdfStandardSecurityHandler.Keys.R);
            encryptor = new IdentityEncryptor(revision >= 5 ? new AESEncryptor() : new RC4Encryptor());
        }
        else
            encryptor = v >= 4 && IsAes(MethodOf(dict, dict.Elements.GetName(keyName))) ? new AESEncryptor() : new RC4Encryptor();
        encryptor.Initialize(doc, dict);
        return encryptor;
    }

    /// <summary>
    /// The encryptor for the crypt filter a stream names in a <c>/Crypt</c> filter of its own
    /// (ISO 32000-1 7.4.10), given the file key the document's other encryptors already hold; null
    /// when that filter leaves the data as it is. A name <c>/CF</c> does not define is decrypted by
    /// <paramref name="fallback"/>, the stream encryptor, as every stream was before.
    /// </summary>
    internal static IEncryptor CreateForStreamFilter(PdfDocument doc, PdfDictionary dict, string filterName,
        IEncryptor fallback)
    {
        if (filterName == IdentityFilter)
            return null;

        IEncryptor encryptor;
        switch (MethodOf(dict, filterName))
        {
            case "/V2":
                encryptor = new RC4Encryptor();
                break;

            case var method when IsAes(method):
                encryptor = new AESEncryptor();
                break;

            case "/None":
                // "The application shall not decrypt data": what is there is what is read.
                return null;

            default:
                return fallback;
        }

        encryptor.Initialize(doc, dict);
        encryptor.EncryptionKey = fallback.EncryptionKey;
        return encryptor;
    }

    /// <summary>
    /// Whether the entry <paramref name="keyName"/> names the crypt filter <c>/Identity</c>, which
    /// leaves the data it covers as it is (ISO 32000-1 7.6.5) - or is missing, which at <c>/V</c> 4
    /// and later means the same (Table 20).
    /// </summary>
    private static bool UsesIdentityCryptFilter(PdfDictionary dict, int v, string keyName) =>
        v >= 4 && dict.Elements.GetName(keyName) is "" or IdentityFilter;

    /// <summary>The name of the crypt filter that does nothing.</summary>
    internal const string IdentityFilter = "/Identity";

    /// <summary>
    /// The <c>/CFM</c> of the crypt filter <paramref name="filterName"/> in <c>/CF</c>, or null
    /// when it defines no such filter.
    /// </summary>
    /// <remarks>
    /// Pdf Reference 1.7 Chapter 7.6.5 (Crypt Filters)
    /// <list type="bullet">
    /// <item>None: The application shall not decrypt data but shall direct the input stream to the
    /// security handler for decryption.</item>
    /// <item>V2: decrypt with "Algorithm 1: Encryption of data using the RC4 or AES algorithms",
    /// using the RC4 algorithm.</item>
    /// <item>AESV2 (PDF 1.6): the same, using AES in CBC mode with a 16-byte block size and an
    /// initialization vector as the first 16 bytes of the stream or string.</item>
    /// <item>AESV3 (PDF 1.7, ExtensionLevel 3): Algorithm 3.1a, AES-256 in CBC mode, the
    /// initialization vector likewise first. The key size is 256 bits.</item>
    /// </list>
    /// </remarks>
    private static string MethodOf(PdfDictionary dict, string filterName)
    {
        if (string.IsNullOrEmpty(filterName))
            return null;

        var method = dict.Elements.GetDictionary(PdfSecurityHandler.Keys.CF)?.Elements.GetDictionary(filterName)?
            .Elements.GetName(PdfSecurityHandler.Keys.CFM);
        return string.IsNullOrEmpty(method) ? null : method;
    }

    /// <summary>AESV2 (PDF 1.6) or AESV3 (PDF 1.7, ExtensionLevel 3).</summary>
    private static bool IsAes(string method) => method != null && method.StartsWith("/AESV");
}
