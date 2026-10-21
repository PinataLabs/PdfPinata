using System;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Says whether this process is the one that instruments the assemblies for <c>--coverlet</c>
/// rather than the one that runs the tests. Shared: linked into every test project whose module
/// initializer reaches a PdfPinata assembly.
/// </summary>
/// <remarks>
/// <para>
/// A test project is an executable that Microsoft.Testing.Platform starts twice when coverage is
/// on. The first process is the test host controller, where coverlet's extension rewrites each
/// assembly on disk; it then starts the same executable again, with the same arguments plus
/// <c>--internal-testhostcontroller-pid</c>, and that second process runs the tests. A module
/// initializer runs in both, because both run the test assembly's entry point.
/// </para>
/// <para>
/// A module initializer that registers a backend loads PdfPinata.dll and its backend into the
/// controller before coverlet gets to them. Windows refuses to open a loaded assembly for writing,
/// so coverlet logged "Skipping module PdfPinata.dll: Locked" and the report had no rows for the
/// core library, nor for whatever else the initializer loaded. Linux lets a mapped file be
/// rewritten, which is why CI never saw it. The controller runs no test, so it needs no backend:
/// an initializer asks this first and does nothing there. Issue #345.
/// </para>
/// <para>
/// The check reads the command line rather than an environment variable because the controller
/// sets its variables on the child alone; nothing in the controller's own environment says what it
/// is. It reads arguments as given, so a <c>--coverlet</c> inside a response file is not seen and the
/// lock comes back.
/// </para>
/// </remarks>
internal static class CoverageController
{
    /// <summary>True in the controller of a <c>--coverlet</c> run, and in no other process.</summary>
    public static bool IsThisProcess { get; } = Determine(Environment.GetCommandLineArgs());

    private static bool Determine(string[] arguments)
    {
        var coverage = false;
        foreach (var argument in arguments)
        {
            var option = OptionName(argument);
            if (string.Equals(option, "internal-testhostcontroller-pid", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(option, "coverlet", StringComparison.OrdinalIgnoreCase))
                coverage = true;
        }
        return coverage;
    }

    /// <summary>
    /// The option an argument names, as the platform's parser reads it: one dash or two, and
    /// anything after a ':' or '=' is its value. Null for an argument that is not an option.
    /// </summary>
    private static string OptionName(string argument)
    {
        if (!argument.StartsWith("-", StringComparison.Ordinal))
            return null;
        var name = argument.TrimStart('-');
        var separator = name.IndexOfAny([':', '=']);
        return separator < 0 ? name : name.Substring(0, separator);
    }
}
