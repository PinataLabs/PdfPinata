using System;
using System.IO;

namespace SampleApp.Infrastructure;

/// <summary>
///   Everything a demo is allowed to know about the world outside it: where to write, and how to
///   reach the assets the app carries.
/// </summary>
/// <remarks>
///   Deliberately not a place to register a backend. <see cref="Backends"/> does that, once, from
///   the composition root, because <c>GlobalFontSettings.FontResolver</c> throws once a font has
///   been used and a demo running inside a host that installed its own resolver - the test
///   assembly, for one - must not disturb it.
/// </remarks>
public sealed class DemoContext
{
    public DemoContext(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));

        OutputDirectory = Path.GetFullPath(outputDirectory);
    }

    /// <summary>The directory every demo writes into, as an absolute path.</summary>
    public string OutputDirectory { get; }

    /// <summary>
    ///   Creates the output directory if it is not there, removes any file left by a previous run,
    ///   and hands back the path the demo should write to.
    /// </summary>
    /// <remarks>
    ///   The old file is deleted rather than written over. Writing over it leaves the tail of the
    ///   previous run behind if the new one is shorter or throws halfway, and a stale PDF that
    ///   still opens is worse than no PDF at all.
    /// </remarks>
    public string PrepareOutputPath(string demoName)
    {
        Directory.CreateDirectory(OutputDirectory);

        string path = Path.Combine(OutputDirectory, demoName + ".pdf");
        if (File.Exists(path))
            File.Delete(path);

        return path;
    }
}
