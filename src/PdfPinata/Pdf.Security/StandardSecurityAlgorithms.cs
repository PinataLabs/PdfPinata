using System;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Security;

/// <summary>
/// The algorithms of ISO 32000-1 7.6.3 that the standard security handler is built on up to
/// revision 4, shared by what reads a document (<see cref="RC4Encryptor"/> and
/// <see cref="AESEncryptor"/>) and what writes one (<see cref="PdfStandardSecurityHandler"/>).
/// </summary>
/// <remarks>
/// The two sides do not ask for the same things. The writer produces revision 2 at 40 bits and
/// revision 3 at 128 bits and nothing else, while the reader takes revisions 2 to 4 at any key
/// length from 40 to 128 bits, so each algorithm takes the revision and the key length rather
/// than a choice between two security levels. What only a reader can meet - unencrypted metadata
/// at revision 4, and a revision 3 key shorter than 128 bits - is a branch the writer never takes,
/// not a branch it lacks. Revisions 5 and 6 are AES-256 and hash with SHA-2, so they do not come
/// through here.
/// </remarks>
internal static class StandardSecurityAlgorithms
{
    /// <summary>
    /// The 32 bytes Algorithm 2 step (a) pads a password with.
    /// </summary>
    internal static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];

    /// <summary>
    /// What Algorithm 1 step (b) appends to the object number and generation for AES.
    /// </summary>
    private static readonly byte[] AesSalt = "sAlT"u8.ToArray();

    /// <summary>
    /// Pads a password to 32 bytes: its first 32 bytes, then as much of the padding string as
    /// makes up the rest. A null password is the padding string alone.
    /// </summary>
    public static byte[] PadPassword(string password)
    {
        var padded = new byte[32];
        if (password == null)
        {
            Array.Copy(PasswordPadding, 0, padded, 0, 32);
        }
        else
        {
            var length = password.Length;
            Array.Copy(PdfEncoders.RawEncoding.GetBytes(password), 0, padded, 0, Math.Min(length, 32));
            if (length < 32)
                Array.Copy(PasswordPadding, 0, padded, length, 32 - length);
        }
        return padded;
    }

    /// <summary>
    /// The file encryption key, which Algorithm 2 makes from the padded user password.
    /// </summary>
    /// <remarks>
    /// From revision 3 on the digest is hashed 50 more times, each time over its first
    /// <paramref name="keyLength"/> bytes, as step (h) says. Below 128 bits that is not the same as
    /// hashing the whole digest, and Ghostscript and qpdf both read it this way.
    /// </remarks>
    public static byte[] FileKey(MD5Managed md5, byte[] paddedUserPassword, byte[] ownerValue, int permissions,
        byte[] documentId, int revision, int keyLength, bool encryptMetadata)
    {
        md5.Initialize();
        md5.TransformBlock(paddedUserPassword, 0, paddedUserPassword.Length, paddedUserPassword, 0);
        md5.TransformBlock(ownerValue, 0, ownerValue.Length, ownerValue, 0);
        var permission = new byte[4];
        permission[0] = (byte)permissions;
        permission[1] = (byte)(permissions >> 8);
        permission[2] = (byte)(permissions >> 16);
        permission[3] = (byte)(permissions >> 24);
        md5.TransformBlock(permission, 0, 4, permission, 0);
        md5.TransformBlock(documentId, 0, documentId.Length, documentId, 0);
        // Step (f). Only a reader meets it: the writer produces nothing past revision 3.
        if (revision >= 4 && !encryptMetadata)
        {
            var ff = new byte[] { 0xff, 0xff, 0xff, 0xff };
            md5.TransformBlock(ff, 0, ff.Length, ff, 0);
        }
        md5.TransformFinalBlock(permission, 0, 0);
        var hash = md5.Hash;
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                md5.Initialize();
                // ReSharper disable once AssignNullToNotNullAttribute
                hash = md5.ComputeHash(hash, 0, keyLength);
            }
        }
        var fileKey = new byte[keyLength];
        // ReSharper disable once AssignNullToNotNullAttribute
        Array.Copy(hash, fileKey, keyLength);
        return fileKey;
    }

    /// <summary>
    /// The /O entry, which Algorithm 3 makes by encrypting the padded user password with a key
    /// made from the padded owner password.
    /// </summary>
    public static byte[] OwnerValue(MD5Managed md5, Rc4 rc4, byte[] paddedOwnerPassword, byte[] paddedUserPassword,
        int revision, int keyLength)
    {
        var key = OwnerPasswordKey(md5, paddedOwnerPassword, revision, keyLength);
        var value = new byte[paddedUserPassword.Length];
        Array.Copy(paddedUserPassword, value, value.Length);
        Rc4Rounds(rc4, key, value, value.Length, revision < 3 ? 1 : 20, descending: false);
        return value;
    }

    /// <summary>
    /// The padded user password an /O entry holds, which Algorithm 7 step (b) recovers by
    /// decrypting it with the key made from the padded owner password. Whether that was the owner
    /// password is for the user password it gives back to say.
    /// </summary>
    public static byte[] UserPasswordFromOwnerValue(MD5Managed md5, Rc4 rc4, byte[] paddedOwnerPassword,
        byte[] ownerValue, int revision, int keyLength)
    {
        var key = OwnerPasswordKey(md5, paddedOwnerPassword, revision, keyLength);
        var value = new byte[ownerValue.Length];
        Array.Copy(ownerValue, value, value.Length);
        Rc4Rounds(rc4, key, value, value.Length, revision < 3 ? 1 : 20, descending: true);
        return value;
    }

    /// <summary>
    /// The /U entry: at revision 2 the padding string encrypted with the file key (Algorithm 4),
    /// and from revision 3 on the digest of the padding string and the document identifier
    /// encrypted 20 times (Algorithm 5). Only the first 16 bytes of the second are defined, and
    /// the 16 after them are zeros.
    /// </summary>
    public static byte[] UserValue(MD5Managed md5, Rc4 rc4, byte[] fileKey, byte[] documentId, int revision)
    {
        var value = new byte[32];
        if (revision == 2)
        {
            Array.Copy(PasswordPadding, value, value.Length);
            Rc4Rounds(rc4, fileKey, value, value.Length, 1, descending: false);
            return value;
        }

        md5.Initialize();
        md5.TransformBlock(PasswordPadding, 0, PasswordPadding.Length, PasswordPadding, 0);
        md5.TransformFinalBlock(documentId, 0, documentId.Length);
        // ReSharper disable once AssignNullToNotNullAttribute
        Array.Copy(md5.Hash, value, 16);
        // Each round's key is the file key XORed with the round number, so it is as long as the
        // file key, which is /Length / 8 bytes. Only at 128 bits does that match the 16-byte
        // digest above, and a shorter key once read past the end of the file key.
        Rc4Rounds(rc4, fileKey, value, 16, 20, descending: false);
        return value;
    }

    /// <summary>
    /// The RC4 key made from the padded owner password: its MD5 digest, rehashed 50 times from
    /// revision 3 on, and cut to 5 bytes at revision 2 and to the key length after. Algorithm 3,
    /// steps (a) to (d).
    /// </summary>
    /// <remarks>
    /// Each rehash reads only the first key-length bytes of the digest before it, as Algorithm 2
    /// step (h) does for the file key. Step (c) reads as though it wants the whole 16-byte digest,
    /// but Ghostscript and qpdf both write /O this way, and qpdf rejects the owner password of a
    /// file whose /O was made from the whole digest. The two readings only differ below 128 bits,
    /// and at revision 2 there is no rehash at all.
    /// </remarks>
    private static byte[] OwnerPasswordKey(MD5Managed md5, byte[] paddedOwnerPassword, int revision, int keyLength)
    {
        md5.Initialize();
        var hash = md5.ComputeHash(paddedOwnerPassword);
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
                hash = md5.ComputeHash(hash, 0, keyLength);
        }
        var n = revision <= 2 ? 5 : keyLength;
        var key = new byte[n];
        Array.Copy(hash, key, n);
        return key;
    }

    /// <summary>
    /// Encrypts the first <paramref name="length"/> bytes of <paramref name="data"/> in place,
    /// <paramref name="rounds"/> times, each time with <paramref name="key"/> XORed with the round
    /// number: 0 up to 19 to encrypt, as Algorithm 3 step (f) and Algorithm 5 step (e) do, and 19
    /// down to 0 to decrypt, as Algorithm 7 step (b) does. A single round is the key itself.
    /// </summary>
    /// <remarks>
    /// Each round XORs one more keystream into the data, so the order makes no difference to the
    /// result. It is kept as each algorithm states it, so that the code reads against the spec.
    /// </remarks>
    private static void Rc4Rounds(Rc4 rc4, byte[] key, byte[] data, int length, int rounds, bool descending)
    {
        var roundKey = new byte[key.Length];
        for (var i = 0; i < rounds; i++)
        {
            var round = descending ? rounds - 1 - i : i;
            for (var j = 0; j < key.Length; j++)
                roundKey[j] = (byte)(key[j] ^ round);
            rc4.SetKey(roundKey);
            rc4.Apply(data, 0, length, data);
        }
    }

    /// <summary>
    /// The key an individual object is encrypted with, which Algorithm 1 makes from the file key
    /// and the object's number and generation. Only the first <paramref name="keySize"/> bytes of
    /// the digest are the key for RC4: the length of the file key plus 5, and 16 at most.
    /// </summary>
    public static byte[] ObjectKey(MD5Managed md5, byte[] fileKey, PdfObjectID id, bool aes, out int keySize)
    {
        var objectId = new byte[5];
        // The low three bytes of the object number and the low two of the generation, low first.
        objectId[0] = (byte)id.ObjectNumber;
        objectId[1] = (byte)(id.ObjectNumber >> 8);
        objectId[2] = (byte)(id.ObjectNumber >> 16);
        objectId[3] = (byte)id.GenerationNumber;
        objectId[4] = (byte)(id.GenerationNumber >> 8);

        md5.Initialize();
        md5.TransformBlock(fileKey, 0, fileKey.Length, fileKey, 0);
        if (aes)
        {
            md5.TransformBlock(objectId, 0, objectId.Length, objectId, 0);
            md5.TransformFinalBlock(AesSalt, 0, AesSalt.Length);
        }
        else
        {
            md5.TransformFinalBlock(objectId, 0, objectId.Length);
        }
        var key = md5.Hash;
        md5.Initialize();

        keySize = Math.Min(fileKey.Length + 5, 16);
        return key;
    }
}
