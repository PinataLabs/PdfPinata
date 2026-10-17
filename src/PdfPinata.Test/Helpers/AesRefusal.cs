using System;
using System.Reflection;
using System.Security.Cryptography;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Makes every AES instance the security handler asks for, in this flow alone, refuse the way a
/// runtime with no AES does, until disposed.
/// </summary>
/// <remarks>
/// <para>
/// There are two ways to refuse, and the WebAssembly runtime uses the second. <see cref="Begin"/>
/// has <c>Aes.Create()</c> itself throw. <see cref="BeginAtTransform"/> hands out an instance, as
/// .NET 10 in the browser does, and throws only when an encryptor or decryptor is asked of it:
/// <c>AesImplementation.CreateTransformCore</c> is where the browser's
/// <see cref="PlatformNotSupportedException"/> comes from.
/// </para>
/// <para>
/// The factory is internal and this repository carries no <c>InternalsVisibleTo</c>, so its
/// override is set through a method reached by name, the way the other probes in this suite reach
/// internals. The method is looked up when a refusal begins rather than when this class is first
/// touched, so a rename fails the test that needed it, saying what to look for, instead of
/// surfacing as a <see cref="TypeInitializationException"/>. The override lives in an
/// <c>AsyncLocal</c>, so the other encrypted-document tests running beside the one that begins a
/// refusal are untouched by it.
/// </para>
/// </remarks>
internal sealed class AesRefusal : IDisposable
{
    private const string RuntimeMessage = "Algorithm 'Aes' is not supported on this platform.";

    private const string Factory = "PdfPinata.Pdf.Security.AesProvider";

    private const string Seam = "SetCreateOverride";

    private readonly Func<Aes> _previous;

    private AesRefusal(Func<Aes> create)
    {
        _previous = SetOverride(create);
    }

    /// <summary><c>Aes.Create()</c> throws.</summary>
    public static AesRefusal Begin() => new(static () => throw new PlatformNotSupportedException(RuntimeMessage));

    /// <summary><c>Aes.Create()</c> answers an instance that throws when asked for a transform.</summary>
    public static AesRefusal BeginAtTransform() => new(static () => new TransformlessAes());

    public void Dispose() => SetOverride(_previous);

    /// <summary>Sets the factory's override for this flow, and answers the one it replaced.</summary>
    private static Func<Aes> SetOverride(Func<Aes> create)
    {
        var seam = typeof(PdfDocument).Assembly.GetType(Factory)
            ?.GetMethod(Seam, BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(Func<Aes>)], null);

        if (seam == null)
            throw new InvalidOperationException(
                $"AesRefusal simulates a runtime with no AES through {Factory}.{Seam}(Func<Aes>), which is not " +
                "there: it was renamed or removed. Point AesRefusal at whatever replaced it.");

        return (Func<Aes>)seam.Invoke(null, [create]);
    }

    private sealed class TransformlessAes : Aes
    {
        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) =>
            throw new PlatformNotSupportedException(RuntimeMessage);

        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) =>
            throw new PlatformNotSupportedException(RuntimeMessage);

        public override void GenerateIV() => IVValue = new byte[BlockSizeValue / 8];

        public override void GenerateKey() => KeyValue = new byte[KeySizeValue / 8];
    }
}
