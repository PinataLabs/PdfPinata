using System;
using System.Reflection;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Runs <paramref name="register"/> - a module initializer's registration of its backend or
    /// resolver - in every process but the controller of a <c>--coverlet</c> run. In the
    /// controller it registers nothing, and on Windows ends the run if a PdfPinata or PinataLayout
    /// assembly is loaded there anyway.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registration is passed as a delegate so that compiling the initializer loads none of
    /// the types it names: making a delegate takes the method's address and leaves its body
    /// uncompiled until it is called, and here it is never called. A registration that is a
    /// static method of a class implementing a PdfPinata interface has to be wrapped in a lambda,
    /// because taking the address of a method loads the class it is declared on.
    /// </para>
    /// <para>
    /// Keeping the initializers out of the controller is not the whole of it: TUnit's generated
    /// test registration names every test class, and loading those loads PdfPinata and the DOM
    /// before coverlet runs. Only <c>-p:EnableTUnitSourceGeneration=false</c> keeps that out, and a
    /// run without it used to pass with a report that had no rows for the core library. A warning
    /// would not do: <c>dotnet test</c> shows a test application's standard error only when the
    /// application fails. So the controller watches for those assemblies and, on Windows, where a
    /// loaded assembly cannot be rewritten, exits with an error naming the property. Linux
    /// rewrites a loaded file, so a run there is left alone.
    /// </para>
    /// </remarks>
    public static void OutsideController(Action register)
    {
        if (!IsThisProcess)
        {
            register();
            return;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Both, because module initializers run in an order nobody here chooses: TUnit's may have
        // loaded them already, or may be about to.
        var domain = AppDomain.CurrentDomain;
        domain.AssemblyLoad += (_, loaded) => RefuseIfCoverletWouldSkip(loaded.LoadedAssembly);
        foreach (var assembly in domain.GetAssemblies())
            RefuseIfCoverletWouldSkip(assembly);
    }

    private static void RefuseIfCoverletWouldSkip(Assembly assembly)
    {
        // The test assembly itself is not instrumented; this class is compiled into each one.
        if (assembly == typeof(CoverageController).Assembly)
            return;

        var name = assembly.GetName().Name ?? "";
        if (!name.StartsWith("PdfPinata", StringComparison.Ordinal)
            && !name.StartsWith("PinataLayout", StringComparison.Ordinal))
        {
            return;
        }

        Console.Error.WriteLine(
            $"error: {name}.dll was loaded by coverlet's controller process before coverlet could " +
            "instrument it, and Windows will not let a loaded assembly be rewritten, so the coverage " +
            "report would have no rows for it. Run the tests with -p:EnableTUnitSourceGeneration=false " +
            "to have coverage on Windows. See CLAUDE.md and issue #345.");
        Environment.Exit(1);
    }

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
        if (!argument.StartsWith('-'))
            return null;
        var name = argument.TrimStart('-');
        var separator = name.IndexOfAny([':', '=']);
        return separator < 0 ? name : name.Substring(0, separator);
    }
}
