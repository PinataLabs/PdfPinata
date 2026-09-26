using PdfPinata.Pdf.Internal;
using System;

namespace PdfPinata.Pdf.Security;

internal class RC4Encryptor : EncryptorBase, IEncryptor
{
    /// <summary>
    /// The cipher the reader decrypts with.
    /// </summary>
    private readonly Rc4 rc4 = new();

    /// <summary>
    /// Creates the encryption Key.
    /// Based on algorithm #2 (3.2 in Extension Level 3) in the Pdf 1.7 reference (7.6.3.3)
    /// </summary>
    public virtual void InitEncryptionKey(string password)
    {
        var userPad = StandardSecurityAlgorithms.PadPassword(password);
        md5.Initialize();
        md5.TransformBlock(userPad, 0, userPad.Length, userPad, 0);
        md5.TransformBlock(ownerValue, 0, ownerValue.Length, ownerValue, 0);
        var permission = new byte[4];
        permission[0] = (byte)pValue;
        permission[1] = (byte)(pValue >> 8);
        permission[2] = (byte)(pValue >> 16);
        permission[3] = (byte)(pValue >> 24);
        md5.TransformBlock(permission, 0, 4, permission, 0);
        md5.TransformBlock(documentId, 0, documentId.Length, documentId, 0);
        if (rValue >= 4 && !encryptMetadata)
        {
            var ff = new byte[] { 0xff, 0xff, 0xff, 0xff };
            md5.TransformBlock(ff, 0, ff.Length, ff, 0);
        }
        md5.TransformFinalBlock(permission, 0, 0);
        var hash = md5.Hash;
        if (rValue >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                md5.Initialize();
                // ReSharper disable once AssignNullToNotNullAttribute
                hash = md5.ComputeHash(hash, 0, keyLength);
            }
        }
        encryptionKey = new byte[keyLength];
        // ReSharper disable once AssignNullToNotNullAttribute
        Array.Copy(hash, encryptionKey, keyLength);
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

    private void ValidateOwnerPassword(string password)
    {
        var pwdPad = StandardSecurityAlgorithms.PadPassword(password);
        var rc4Input = OwnerPasswordKey(pwdPad);

        var ov = new byte[ownerValue.Length];
        Array.Copy(ownerValue, ov, ov.Length);
        DecryptOwnerValue(ov, rc4Input);

        var userPass = PdfEncoders.RawEncoding.GetString(ov);
        ValidateUserPassword(userPass);
        if (PasswordValid)
            HaveOwnerPermission = true;
    }

    /// <summary>
    /// Decrypts a copy of the /O value with the key made from the owner password, which gives back
    /// the padded user password.
    /// </summary>
    private void DecryptOwnerValue(byte[] ov, byte[] rc4Input)
    {
        var n = rc4Input.Length;
        if (rValue < 3)
        {
            rc4.SetKey(rc4Input, 0, n);
            rc4.Apply(ov);
            return;
        }

        var xor = new byte[n];
        for (var i = 0; i < 20; i++)
        {
            for (var j = 0; j < n; j++)
                xor[j] = (byte)(rc4Input[j] ^ (19 - i));
            rc4.SetKey(xor, 0, n);
            rc4.Apply(ov);
        }
    }

    /// <summary>
    /// The RC4 key made from the padded owner password: its MD5 hash, rehashed 50 times from
    /// revision 3 on, and cut to the key length. ISO 32000-1 7.6.3.4, Algorithm 3, steps (a)-(d).
    /// </summary>
    /// <remarks>
    /// Each rehash reads only the first key-length bytes of the hash before it, as Algorithm 2
    /// step (h) does for the file key. Algorithm 3 step (c) reads as though it wants the whole
    /// 16-byte digest, but Ghostscript and qpdf both write /O this way, and qpdf rejects the owner
    /// password of a file whose /O was made from the whole digest. The two readings only differ
    /// below 128 bits, and at revision 2 there is no rehash at all.
    /// </remarks>
    private byte[] OwnerPasswordKey(byte[] pwdPad)
    {
        md5.Initialize();
        var pwdKey = md5.ComputeHash(pwdPad);
        if (rValue >= 3)
        {
            for (var i = 0; i < 50; i++)
                pwdKey = md5.ComputeHash(pwdKey, 0, keyLength);
        }
        var n = rValue <= 2 ? 5 : keyLength;
        var rc4Input = new byte[n];
        Array.Copy(pwdKey, rc4Input, n);
        return rc4Input;
    }

    /// <summary>
    /// Pdf Reference 1.7, Chapter 7.6.3.4, Algorithm #4 and #5
    /// </summary>
    public void CreateUserKey(string password)
    {
        InitEncryptionKey(password);
        if (rValue == 2)
        {
            var data = new byte[StandardSecurityAlgorithms.PasswordPadding.Length];
            Array.Copy(StandardSecurityAlgorithms.PasswordPadding, data, data.Length);
            rc4.SetKey(encryptionKey, 0, keySize);
            rc4.Apply(data);
            computedUserValue = new byte[data.Length];
            Array.Copy(data, computedUserValue, data.Length);
        }
        else
        {
            computedUserValue = new byte[32];
            md5.Initialize();
            md5.TransformBlock(StandardSecurityAlgorithms.PasswordPadding, 0, StandardSecurityAlgorithms.PasswordPadding.Length, StandardSecurityAlgorithms.PasswordPadding, 0);
            md5.TransformFinalBlock(documentId, 0, documentId.Length);
            var mkey = md5.Hash;
            // ReSharper disable once AssignNullToNotNullAttribute
            // ReSharper disable once PossibleNullReferenceException
            Array.Copy(mkey, computedUserValue, mkey.Length);
            // Each round's key is the file key XORed with the round number, so it is as long as
            // the file key, which is /Length / 8 bytes. Only at 128 bits does that match the
            // 16-byte digest above, and a shorter key read past the end of the file key.
            var roundKey = new byte[encryptionKey.Length];
            for (var i = 0; i < 20; i++)
            {
                for (var j = 0; j < roundKey.Length; j++)
                    roundKey[j] = (byte)(encryptionKey[j] ^ i);
                rc4.SetKey(roundKey);
                rc4.Apply(computedUserValue, 0, 16, computedUserValue);
            }
            for (var i = 16; i < 32; i++)
                computedUserValue[i] = 0;
        }
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
