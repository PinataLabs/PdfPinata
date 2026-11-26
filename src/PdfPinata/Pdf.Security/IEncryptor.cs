namespace PdfPinata.Pdf.Security;

internal interface IEncryptor
{
    bool HaveOwnerPermission { get; }

    /// <summary>
    /// The file encryption key. A document has a single one, even when its strings and
    /// streams are covered by different crypt filters.
    /// </summary>
    byte[] EncryptionKey { get; set; }

    void Initialize(PdfDocument document, PdfDictionary encryptionDict);

    void InitEncryptionKey(string password);

    bool ValidatePassword(string password);

    void CreateHashKey(PdfObjectID objectId);

    /// <summary>
    /// Decrypts <paramref name="bytes"/>, under the key <see cref="CreateHashKey"/> made. Named for
    /// RC4, where the two directions are one.
    /// </summary>
    byte[] Encrypt(byte[] bytes);

    /// <summary>
    /// Encrypts <paramref name="bytes"/> for writing, under the key <see cref="CreateHashKey"/> made.
    /// </summary>
    byte[] EncryptForWriting(byte[] bytes);
}
