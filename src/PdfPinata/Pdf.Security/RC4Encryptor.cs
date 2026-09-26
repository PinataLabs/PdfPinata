using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Security;

internal class RC4Encryptor : EncryptorBase, IEncryptor
{
    /// <summary>
    /// The cipher the reader decrypts with.
    /// </summary>
    private readonly Rc4 rc4 = new();

    /// <summary>
    /// Makes the file encryption key from <paramref name="password"/>, taken as the user password.
    /// </summary>
    public virtual void InitEncryptionKey(string password)
    {
        encryptionKey = StandardSecurityAlgorithms.FileKey(md5, StandardSecurityAlgorithms.PadPassword(password),
            ownerValue, pValue, documentId, rValue, keyLength, encryptMetadata);
    }

    public bool ValidatePassword(string password)
    {
        // AESEncryptor sets these
        if (HaveOwnerPermission || PasswordValid)
            return true;

        ValidateOwnerPassword(password);
        if (!PasswordValid)
            ValidateUserPassword(password);
        return PasswordValid;

    }

    private void ValidateUserPassword(string password)
    {
        CreateUserKey(password);
        PasswordValid = CompareArrays(computedUserValue, userValue, 16);
    }

    /// <summary>
    /// Takes <paramref name="password"/> for the owner password, recovers the user password from
    /// the /O entry with it, and validates that.
    /// </summary>
    private void ValidateOwnerPassword(string password)
    {
        var ov = StandardSecurityAlgorithms.UserPasswordFromOwnerValue(md5, rc4,
            StandardSecurityAlgorithms.PadPassword(password), ownerValue, rValue, keyLength);

        var userPass = PdfEncoders.RawEncoding.GetString(ov);
        ValidateUserPassword(userPass);
        if (PasswordValid)
            HaveOwnerPermission = true;
    }

    /// <summary>
    /// Makes the file encryption key from <paramref name="password"/>, and the /U entry that key
    /// would have written.
    /// </summary>
    public void CreateUserKey(string password)
    {
        InitEncryptionKey(password);
        computedUserValue = StandardSecurityAlgorithms.UserValue(md5, rc4, encryptionKey, documentId, rValue);
    }

    /// <summary>
    /// Makes the key the object <paramref name="id"/> is decrypted with.
    /// </summary>
    public virtual void CreateHashKey(PdfObjectID id)
    {
        key = StandardSecurityAlgorithms.ObjectKey(md5, encryptionKey, id, aes: false, out keySize);
    }

    public virtual byte[] Encrypt(byte[] bytes)
    {
        rc4.SetKey(key, 0, keySize);
        rc4.Apply(bytes);
        return bytes;
    }
}
