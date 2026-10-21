namespace PdfPinata.Pdf.Security;

/// <summary>
/// The crypt filter <c>/Identity</c>: what it covers is not encrypted, and is read as it stands.
/// </summary>
/// <remarks>
/// It still holds an encryptor of the document's own kind, because the string encryptor is also what
/// validates the password and derives the file key, and a document whose strings are left in plain
/// still has a password and a key for its streams. Only <see cref="Encrypt"/> differs.
/// </remarks>
internal sealed class IdentityEncryptor(IEncryptor keys) : IEncryptor
{
    public bool PasswordValid => keys.PasswordValid;

    public bool HaveOwnerPermission => keys.HaveOwnerPermission;

    public byte[] EncryptionKey
    {
        get => keys.EncryptionKey;
        set => keys.EncryptionKey = value;
    }

    public void Initialize(PdfDocument document, PdfDictionary encryptionDict) =>
        keys.Initialize(document, encryptionDict);

    public void InitEncryptionKey(string password) => keys.InitEncryptionKey(password);

    public bool ValidatePassword(string password) => keys.ValidatePassword(password);

    public void CreateHashKey(PdfObjectID objectId)
    { }

    public byte[] Encrypt(byte[] bytes) => bytes;
}
