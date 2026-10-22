using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PdfPinata.Test.Security;

/// <summary>
///   The standard security handler at revision 6 (AES-256, ISO 32000-2 7.6.4.3), written from the
///   standard for building test documents rather than taken from the reader, so that the two can
///   disagree. Every salt is fixed, so a document built twice is the same bytes.
/// </summary>
internal static class Aes256Security
{
    private static readonly byte[] UserValidationSalt = Salt(0x11);
    private static readonly byte[] UserKeySalt = Salt(0x22);
    private static readonly byte[] OwnerValidationSalt = Salt(0x33);
    private static readonly byte[] OwnerKeySalt = Salt(0x44);

    /// <summary>The 48-byte <c>/U</c> entry for <paramref name="userPassword"/> (Algorithm 8).</summary>
    public static byte[] UserEntry(string userPassword) =>
        [..Hash(userPassword, UserValidationSalt, []), ..UserValidationSalt, ..UserKeySalt];

    /// <summary>The 32-byte <c>/UE</c> entry: the file key under the user password (Algorithm 8).</summary>
    public static byte[] UserKeyEntry(string userPassword, byte[] fileKey) =>
        WrapKey(Hash(userPassword, UserKeySalt, []), fileKey);

    /// <summary>The 48-byte <c>/O</c> entry, which hashes the whole <c>/U</c> in (Algorithm 9).</summary>
    public static byte[] OwnerEntry(string ownerPassword, byte[] userEntry) =>
        [..Hash(ownerPassword, OwnerValidationSalt, userEntry), ..OwnerValidationSalt, ..OwnerKeySalt];

    /// <summary>The 32-byte <c>/OE</c> entry: the file key under the owner password (Algorithm 9).</summary>
    public static byte[] OwnerKeyEntry(string ownerPassword, byte[] userEntry, byte[] fileKey) =>
        WrapKey(Hash(ownerPassword, OwnerKeySalt, userEntry), fileKey);

    /// <summary>The 16-byte <c>/Perms</c> entry (Algorithm 10).</summary>
    public static byte[] PermsEntry(int permissions, bool encryptMetadata, byte[] fileKey)
    {
        byte[] plain =
        [
            ..BitConverter.GetBytes(permissions), 0xFF, 0xFF, 0xFF, 0xFF,
            (byte)(encryptMetadata ? 'T' : 'F'), (byte)'a', (byte)'d', (byte)'b', 0, 0, 0, 0
        ];
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return aes.EncryptEcb(plain, PaddingMode.None);
    }

    /// <summary>
    ///   A string or stream under <c>/AESV3</c>: AES-256 in CBC mode with the file key itself, the
    ///   initialization vector ahead of the data (7.6.3.2).
    /// </summary>
    public static byte[] Encrypt(byte[] fileKey, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return [..iv, ..aes.EncryptCbc(data, iv, PaddingMode.PKCS7)];
    }

    /// <summary>Algorithm 2.B: the hash a revision 6 password is checked and a key unwrapped with.</summary>
    internal static byte[] Hash(string password, byte[] salt, byte[] userEntry)
    {
        var pwd = Encoding.UTF8.GetBytes(password);
        var k = SHA256.HashData([..pwd, ..salt, ..userEntry]);
        for (var round = 1; ; round++)
        {
            byte[] once = [..pwd, ..k, ..userEntry];
            var k1 = Enumerable.Repeat(once, 64).SelectMany(b => b).ToArray();

            using var aes = Aes.Create();
            aes.Key = k[..16];
            var e = aes.EncryptCbc(k1, k[16..32], PaddingMode.None);

            // The first 16 bytes of E as one big-endian number, modulo 3; 256 is 1 modulo 3, so
            // that is the sum of the bytes modulo 3.
            k = (e.Take(16).Sum(b => b) % 3) switch
            {
                0 => SHA256.HashData(e),
                1 => SHA384.HashData(e),
                _ => SHA512.HashData(e)
            };

            if (round >= 64 && e[^1] <= round - 32)
                return k[..32];
        }
    }

    private static byte[] WrapKey(byte[] key, byte[] fileKey)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(fileKey, new byte[16], PaddingMode.None);
    }

    private static byte[] Salt(byte seed) => Enumerable.Range(0, 8).Select(i => (byte)(seed + i)).ToArray();
}
