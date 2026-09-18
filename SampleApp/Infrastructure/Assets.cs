using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SampleApp.Infrastructure;

/// <summary>
///   The fonts, images and demo sources the app carries, read out of the assembly itself.
/// </summary>
/// <remarks>
///   Embedded rather than copied beside the binary. A referenced project's content items do not
///   reach the referencing project's output directory, and PdfPinata.Test references this
///   project to run the demos in its smoke test - so a file on disk would be found when the app
///   runs and missing when the test does. Embedding also survives publishing and has no opinion
///   about the working directory.
///   <para>
///     Names are the <c>LogicalName</c> values set in the project file: <c>Fonts/…</c>,
///     <c>Images/…</c> and <c>Sources/…</c>.
///   </para>
/// </remarks>
public static class Assets
{
    static readonly Assembly Self = typeof(Assets).Assembly;

    public const string FontPrefix = "Fonts/";
    public const string ImagePrefix = "Images/";
    public const string SourcePrefix = "Sources/";

    /// <summary>Opens an embedded asset, or throws naming what is actually there.</summary>
    public static Stream Open(string name)
    {
        Stream? stream = Self.GetManifestResourceStream(name);
        if (stream is null)
        {
            throw new FileNotFoundException(
                $"'{name}' is not embedded in {Self.GetName().Name}. Embedded: "
                + string.Join(", ", Names()), name);
        }

        return stream;
    }

    public static byte[] Bytes(string name)
    {
        using Stream stream = Open(name);
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>The text of an embedded asset, or null if it is not embedded at all.</summary>
    public static string? TryReadText(string name)
    {
        using Stream? stream = Self.GetManifestResourceStream(name);
        if (stream is null)
            return null;

        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static bool Exists(string name) => Names().Contains(name, StringComparer.Ordinal);

    public static IReadOnlyList<string> Names() => Self.GetManifestResourceNames();
}
