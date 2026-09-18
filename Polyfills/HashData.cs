// The static one-shot hash, for the netstandard2.1 leg only. See README.md beside this file.

namespace System.Security.Cryptography
{
    internal static class Sha256Polyfill
    {
        extension(SHA256)
        {
            /// <summary>The SHA-256 hash of <paramref name="source"/>.</summary>
            public static byte[] HashData(byte[] source)
            {
                if (source is null)
                    throw new ArgumentNullException(nameof(source));
                using var sha256 = SHA256.Create();
                return sha256.ComputeHash(source);
            }
        }
    }
}
