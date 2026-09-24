using PdfPinata.Pdf.Internal;
using System;

namespace PdfPinata.Pdf.Security;

internal class RC4Encryptor : EncryptorBase, IEncryptor
{
    /// <summary>
    /// Bytes used for RC4 encryption.
    /// </summary>
    private readonly byte[] state = new byte[256];

    /// <summary>
    /// Creates the encryption Key.
    /// Based on algorithm #2 (3.2 in Extension Level 3) in the Pdf 1.7 reference (7.6.3.3)
    /// </summary>
    public virtual void InitEncryptionKey(string password)
    {
        var userPad = PadPassword(password);
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
        var pwdPad = PadPassword(password);
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
            PrepareRC4Key(rc4Input, 0, n);
            EncryptRC4(ov);
            return;
        }

        var xor = new byte[n];
        for (var i = 0; i < 20; i++)
        {
            for (var j = 0; j < n; j++)
                xor[j] = (byte)(rc4Input[j] ^ (19 - i));
            PrepareRC4Key(xor, 0, n);
            EncryptRC4(ov);
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
            var data = new byte[passwordPadding.Length];
            Array.Copy(passwordPadding, data, data.Length);
            PrepareRC4Key(encryptionKey);
            EncryptRC4(data);
            computedUserValue = new byte[data.Length];
            Array.Copy(data, computedUserValue, data.Length);
        }
        else
        {
            computedUserValue = new byte[32];
            md5.Initialize();
            md5.TransformBlock(passwordPadding, 0, passwordPadding.Length, passwordPadding, 0);
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
                PrepareRC4Key(roundKey, 0, roundKey.Length);
                EncryptRC4(computedUserValue, 0, 16);
            }
            for (var i = 16; i < 32; i++)
                computedUserValue[i] = 0;
        }
    }

    /// <summary>
    /// Pdf Reference 1.7, Chapter 7.6.2, Algorithm #1
    /// </summary>
    /// <param name="id"></param>
    public virtual void CreateHashKey(PdfObjectID id)
    {
        var objectId = new byte[5];
        md5.Initialize();
        // Split the object number and generation
        objectId[0] = (byte)id.ObjectNumber;
        objectId[1] = (byte)(id.ObjectNumber >> 8);
        objectId[2] = (byte)(id.ObjectNumber >> 16);
        objectId[3] = (byte)id.GenerationNumber;
        objectId[4] = (byte)(id.GenerationNumber >> 8);
        md5.TransformBlock(encryptionKey, 0, encryptionKey.Length, encryptionKey, 0);   // ?? incomplete
        md5.TransformFinalBlock(objectId, 0, objectId.Length);
        key = md5.Hash;
        md5.Initialize();
        keySize = encryptionKey.Length + 5;
        if (keySize > 16)
            keySize = 16;
    }

    public virtual byte[] Encrypt(byte[] bytes)
    {
        PrepareRC4Key(key);
        EncryptRC4(bytes);
        return bytes;
    }

    /// <summary>
    /// Prepare the encryption key.
    /// </summary>
    protected void PrepareRC4Key(byte[] keyBytes)
    {
        PrepareRC4Key(keyBytes, 0, keySize);
    }

    /// <summary>
    /// Prepare the encryption key.
    /// </summary>
    protected void PrepareRC4Key(byte[] keyBytes, int offset, int length)
    {
        var idx1 = 0;
        var idx2 = 0;
        for (var idx = 0; idx < 256; idx++)
            state[idx] = (byte)idx;
        byte tmp;
        for (var idx = 0; idx < 256; idx++)
        {
            idx2 = (keyBytes[idx1 + offset] + state[idx] + idx2) & 255;
            tmp = state[idx];
            state[idx] = state[idx2];
            state[idx2] = tmp;
            idx1 = (idx1 + 1) % length;
        }
    }

    /// <summary>
    /// Encrypts the data.
    /// </summary>
    protected void EncryptRC4(byte[] data)
    {
        EncryptRC4(data, 0, data.Length, data);
    }

    /// <summary>
    /// Encrypts the data.
    /// </summary>
    protected void EncryptRC4(byte[] data, int offset, int length)
    {
        EncryptRC4(data, offset, length, data);
    }

    /// <summary>
    /// Encrypts the data.
    /// </summary>
    protected void EncryptRC4(byte[] inputData, byte[] outputData)
    {
        EncryptRC4(inputData, 0, inputData.Length, outputData);
    }

    /// <summary>
    /// Encrypts the data.
    /// </summary>
    protected void EncryptRC4(byte[] inputData, int offset, int length, byte[] outputData)
    {
        length += offset;
        int x = 0, y = 0;
        byte b;
        for (var idx = offset; idx < length; idx++)
        {
            x = (x + 1) & 255;
            y = (state[x] + y) & 255;
            b = state[x];
            state[x] = state[y];
            state[y] = b;
            outputData[idx] = (byte)(inputData[idx] ^ state[(state[x] + state[y]) & 255]);
        }
    }

}
