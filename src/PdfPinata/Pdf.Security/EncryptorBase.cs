using PdfPinata.Pdf.Internal;
using System;

namespace PdfPinata.Pdf.Security;

internal abstract class EncryptorBase
{
    /// <summary>
    /// The MD5 implementation the standard security handler is built on. It is created on
    /// first use, because revision 5 and 6 encryption does not need it at all.
    /// </summary>
    protected MD5Managed md5 => field ??= new MD5Managed();

    /// <summary>
    /// The encryption key for the owner.
    /// </summary>
    protected byte[] ownerKey = new byte[32];

    /// <summary>
    /// The encryption key for the user.
    /// </summary>
    protected byte[] userKey = new byte[32];

    /// <summary>
    /// The encryption key for a particular object/generation.
    /// </summary>
    protected byte[] key;

    /// <summary>
    /// The global encryption key.
    /// </summary>
    protected byte[] encryptionKey;

    /// <summary>
    /// The file encryption key. A document has a single one, even when its strings and
    /// streams are covered by different crypt filters.
    /// </summary>
    public byte[] EncryptionKey
    {
        get => encryptionKey;
        set => encryptionKey = value;
    }

    /// <summary>
    /// The /O value as read from the input document
    /// </summary>
    protected byte[] ownerValue;

    /// <summary>
    /// The /U value as read from the input document
    /// </summary>
    protected byte[] userValue;

    protected byte[] computedOwnerValue;

    protected byte[] computedUserValue;

    protected byte[] documentId;

    protected byte[] oeValue;

    protected byte[] ueValue;

    protected byte[] permsValue;

    protected int pValue;

    protected int rValue;

    protected int vValue;

    protected bool encryptMetadata;

    protected PdfDictionary cf;

    protected string stmF;

    protected string strF;

    protected int keyLength;

    protected static readonly byte[] passwordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];

    /// <summary>
    /// The encryption key length for a particular object/generation.
    /// </summary>
    protected int keySize;

    protected PdfDocument doc;

    protected PdfDictionary encryptionDict;

    public bool PasswordValid { get; protected set; }

    public bool HaveOwnerPermission { get; protected set; }

    public void Initialize(PdfDocument document, PdfDictionary encryptionDictionary)
    {
        doc = document;
        encryptionDict = encryptionDictionary;

        documentId = PdfEncoders.RawEncoding.GetBytes(doc.Internals.FirstDocumentID);
        ownerValue = PdfEncoders.RawEncoding.GetBytes(encryptionDict.Elements.GetString(PdfStandardSecurityHandler.Keys.O));
        userValue = PdfEncoders.RawEncoding.GetBytes(encryptionDict.Elements.GetString(PdfStandardSecurityHandler.Keys.U));
        oeValue = PdfEncoders.RawEncoding.GetBytes(encryptionDict.Elements.GetString(PdfStandardSecurityHandler.Keys.OE));
        ueValue = PdfEncoders.RawEncoding.GetBytes(encryptionDict.Elements.GetString(PdfStandardSecurityHandler.Keys.UE));
        permsValue = PdfEncoders.RawEncoding.GetBytes(encryptionDict.Elements.GetString(PdfStandardSecurityHandler.Keys.Perms));
        pValue = encryptionDict.Elements.GetInteger(PdfStandardSecurityHandler.Keys.P);
        rValue = encryptionDict.Elements.GetInteger(PdfStandardSecurityHandler.Keys.R);
        vValue = encryptionDict.Elements.GetInteger(PdfSecurityHandler.Keys.V);
        encryptMetadata = !encryptionDict.Elements.ContainsKey(PdfStandardSecurityHandler.Keys.EncryptMetadata) || encryptionDict.Elements.GetBoolean(PdfStandardSecurityHandler.Keys.EncryptMetadata);
        cf = encryptionDict.Elements.GetDictionary(PdfSecurityHandler.Keys.CF);
        stmF = encryptionDict.Elements.GetString(PdfSecurityHandler.Keys.StmF);
        strF = encryptionDict.Elements.GetString(PdfSecurityHandler.Keys.StrF);
        keyLength = encryptionDict.Elements.GetInteger(PdfSecurityHandler.Keys.Length) / 8;     // specified in Bits
        // Length may be absent, use default of 40 bits (see 7.6.1 Table 20, "V" entry)
        if (keyLength <= 0)
            keyLength = 5;
        // Up to revision 4 the key is cut from a 16-byte MD5 digest, so ISO 32000-1 Table 20 allows
        // /Length 40 to 128. A larger one would have every rehash read past the digest. qpdf opens
        // such a file as 128-bit, and so does this. Revisions 5 and 6 are AES-256 and never read it.
        if (rValue <= 4 && keyLength > 16)
            keyLength = 16;
        keySize = keyLength;
    }

    /// <summary>
    /// Pads a password to a 32 byte array.
    /// </summary>
    protected static byte[] PadPassword(string password)
    {
        var padded = new byte[32];
        if (password == null)
        {
            Array.Copy(passwordPadding, 0, padded, 0, 32);
        }
        else
        {
            var length = password.Length;
            Array.Copy(PdfEncoders.RawEncoding.GetBytes(password), 0, padded, 0, Math.Min(length, 32));
            if (length < 32)
                Array.Copy(passwordPadding, 0, padded, length, 32 - length);
        }
        return padded;
    }

    /// <summary>
    /// Compares the first 'length' bytes of two byte-arrays
    /// </summary>
    /// <returns></returns>
    protected static bool CompareArrays(byte[] left, byte[] right, int length)
    {
        for (var i = 0; i < length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }
}
