using System;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Security;

/// <summary>
/// The algorithms of ISO 32000-1 7.6.3 that the standard security handler is built on up to
/// revision 4, shared by what reads a document (<see cref="RC4Encryptor"/> and
/// <see cref="AESEncryptor"/>) and what writes one (<see cref="PdfStandardSecurityHandler"/>).
/// </summary>
/// <remarks>
/// Revisions 5 and 6 are AES-256 and hash with SHA-2, so they do not come through here.
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
