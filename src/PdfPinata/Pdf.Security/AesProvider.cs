using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace PdfPinata.Pdf.Security;

/// <summary>
/// Where the standard security handler gets every AES instance it uses, so that a runtime with no
/// AES is refused in one place and in words that say what was refused.
/// </summary>
/// <remarks>
/// <para>
/// .NET on WebAssembly provides no AES: <see cref="Aes.Create()"/> throws a
/// <see cref="PlatformNotSupportedException"/> whose message names neither the document's
/// encryption nor a way round it. That breaks reading a document encrypted with revision 4 of the
/// standard security handler (AES-128) or revisions 5 and 6 (AES-256). RC4, MD5 and the SHA-2
/// hashes all still work there, so a revision 2 or 3 document is unaffected.
/// </para>
/// <para>
/// Only reading uses AES. This library writes RC4 alone, so saving a document never reaches here.
/// </para>
/// </remarks>
internal static class AesProvider
{
    /// <summary>What revision 4 of the standard security handler encrypts with.</summary>
    internal const string Aes128 = "AES-128";

    /// <summary>What revisions 5 and 6 of the standard security handler encrypt with.</summary>
    internal const string Aes256 = "AES-256";

    /// <summary>
    /// Stands in for <see cref="Aes.Create()"/> in the current flow alone, and is null everywhere
    /// else. A test sets it to make the runtime refuse as WebAssembly's does; being an
    /// <see cref="AsyncLocal{T}"/>, it cannot reach a test running beside that one.
    /// </summary>
    private static readonly AsyncLocal<Func<Aes>> CreateOverride = new();

    /// <summary>
    /// Creates an AES instance for a document encrypted with <paramref name="encryption"/>.
    /// </summary>
    /// <param name="encryption"><see cref="Aes128"/> or <see cref="Aes256"/>: the document's
    /// encryption, which is what the message names - not the key size of this one instance,
    /// since revision 6 hashes its password with AES-128 on the way to an AES-256 key.</param>
    /// <exception cref="NotSupportedException">The runtime provides no AES.</exception>
    internal static Aes Create(string encryption)
    {
        try
        {
            return CreateOverride.Value?.Invoke() ?? Aes.Create();
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new NotSupportedException(RefusalMessage(encryption, PlatformName()), ex);
        }
    }

    /// <summary>
    /// What a document encrypted with <paramref name="encryption"/> is refused with on
    /// <paramref name="platform"/>.
    /// </summary>
    internal static string RefusalMessage(string encryption, string platform)
        => $"{encryption} encryption is not supported on this platform, {platform}: its .NET runtime "
           + $"provides no AES. This document is encrypted with {encryption}, so it cannot be opened "
           + "here. Open it on a server or desktop instead, or use a copy that is not encrypted or is "
           + "encrypted with RC4, which works on every platform.";

    private static string PlatformName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"))
            ? "the browser (.NET on WebAssembly)"
            : RuntimeInformation.OSDescription.Trim();
}
