using System;
using System.Collections.Generic;

namespace SampleApp.Infrastructure;

/// <param name="Demos">The demos to run, in the order they should run.</param>
/// <param name="OutputDirectory">Where the PDFs go.</param>
/// <param name="ShowCode">Whether to print each demo's source above what it wrote.</param>
public sealed record RunOptions(
    IReadOnlyList<PdfDemo> Demos,
    string OutputDirectory,
    bool ShowCode);

public static class DemoRunner
{
    /// <summary>Runs the demos and returns the process exit code.</summary>
    /// <remarks>
    ///   A demo that throws is reported and the rest still run. Stopping at the first failure tells
    ///   you about one break per run, and the whole point of the app is to see everything at once.
    /// </remarks>
    public static int Run(RunOptions options)
    {
        Backends.EnsureRegistered();

        DemoContext context = new DemoContext(options.OutputDirectory);
        int succeeded = 0;
        int failed = 0;

        foreach (PdfDemo demo in options.Demos)
        {
            Ui.WriteDemoHeading(demo);

            if (options.ShowCode)
                Ui.WriteSource(demo);

            try
            {
                DemoResult result = demo.Run(context);
                Ui.WriteResult(result);
                succeeded++;
            }
            catch (Exception exception)
            {
                Ui.WriteFailure(demo, exception);
                failed++;
            }
        }

        Ui.WriteSummary(succeeded, failed, context.OutputDirectory);
        return failed == 0 ? 0 : 1;
    }

    public static int List()
    {
        Ui.WriteDemoList(DemoRegistry.All);
        return 0;
    }
}
