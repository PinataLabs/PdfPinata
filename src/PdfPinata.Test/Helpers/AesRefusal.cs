using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
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
/// override is reached by name, the way the other probes in this suite reach internals. It is an
/// <see cref="AsyncLocal{T}"/>, so the other encrypted-document tests running beside the one that
/// begins a refusal are untouched by it.
/// </para>
/// </remarks>
internal sealed class AesRefusal : IDisposable
{
    private const string RuntimeMessage = "Algorithm 'Aes' is not supported on this platform.";

    private static readonly AsyncLocal<Func<Aes>> Override = (AsyncLocal<Func<Aes>>)
        typeof(PdfDocument).Assembly
            .GetType("PdfPinata.Pdf.Security.AesProvider", throwOnError: true)!
            .GetField("CreateOverride", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private readonly Func<Aes> _previous;

    private AesRefusal(Func<Aes> create)
    {
        _previous = Override.Value;
        Override.Value = create;
    }

    /// <summary><c>Aes.Create()</c> throws.</summary>
    public static AesRefusal Begin() => new(static () => throw new PlatformNotSupportedException(RuntimeMessage));

    /// <summary><c>Aes.Create()</c> answers an instance that throws when asked for a transform.</summary>
    public static AesRefusal BeginAtTransform() => new(static () => new TransformlessAes());

    public void Dispose() => Override.Value = _previous;

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
