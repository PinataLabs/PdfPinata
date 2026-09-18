namespace PdfPinata.Pdf.Security;

internal interface IEncryptor
{
    bool PasswordValid { get; }

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

    byte[] Encrypt(byte[] bytes);
}
