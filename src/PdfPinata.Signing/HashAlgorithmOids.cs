using System;
using System.Security.Cryptography;

namespace PdfPinata.Signing;

/// <summary>
/// The object identifiers for the hash algorithms this package signs with. One place for the mapping
/// so <see cref="Pkcs7Signer"/>, <see cref="LocalTimestampAuthority"/> and anything else that has to
/// name a digest in ASN.1 agree with each other.
/// </summary>
internal static class HashAlgorithmOids
{
    public static string Of(HashAlgorithmName algorithm)
    {
        if (algorithm == HashAlgorithmName.SHA256)
            return "2.16.840.1.101.3.4.2.1";
        if (algorithm == HashAlgorithmName.SHA384)
            return "2.16.840.1.101.3.4.2.2";
        if (algorithm == HashAlgorithmName.SHA512)
            return "2.16.840.1.101.3.4.2.3";

        throw new ArgumentException(
            "Only SHA-256, SHA-384 and SHA-512 are supported.", nameof(algorithm));
    }
}
