using System.Collections.Generic;
using System.CommandLine;
using System.IO;

namespace SampleApp.Infrastructure;

public static class CommandLineApp
{
    public static RootCommand Build()
    {
        var example = new Option<string[]>("--example", "-e")
        {
            Description = "Run only the demos named. Case-insensitive. Give several after one flag, "
                        + "or repeat the flag. Omit to run every demo.",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        // Not AcceptOnlyFromAmong, which compares ordinally and would turn "--example fonts" into
        // an error over one capital letter. This says the same thing case-insensitively, and names
        // the demos that do exist - which is what somebody who mistyped one needs to read.
        example.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                if (!DemoRegistry.TryGet(token.Value, out _))
                {
                    result.AddError(
                        $"Unknown demo '{token.Value}'. "
                        + $"Known demos: {string.Join(", ", DemoRegistry.Names)}.");
                }
            }
        });

        // Relative to the working directory rather than to the binary, because PDFs buried in
        // bin/Debug/net8.0/output are PDFs nobody finds. The path written to the terminal is
        // absolute, so where they went is never in doubt.
        var output = new Option<DirectoryInfo>("--output", "-o")
        {
            Description = "Directory the PDFs are written to. Defaults to ./output.",
            DefaultValueFactory = _ => new DirectoryInfo(
                Path.Combine(Directory.GetCurrentDirectory(), "output"))
        };

        var noCode = new Option<bool>("--no-code")
        {
            Description = "Do not print the source of each demo, only what it wrote."
        };

        var run = new Command("run", "Build the demonstration PDFs, printing the source of each.");
        run.Options.Add(example);
        run.Options.Add(output);
        run.Options.Add(noCode);
        run.SetAction(parseResult => DemoRunner.Run(new RunOptions(
            Demos: Select(parseResult.GetValue(example)),
            OutputDirectory: parseResult.GetValue(output)!.FullName,
            ShowCode: !parseResult.GetValue(noCode))));

        var list = new Command("list", "List the demos and what each one shows.");
        list.SetAction(_ => DemoRunner.List());

        // No action on the root: with subcommands and no action, a bare invocation prints help and
        // returns non-zero, which is the right answer to being run with no arguments.
        var root = new RootCommand(
            "PdfPinata demonstrations - runnable examples and the PDFs they produce.");
        root.Subcommands.Add(run);
        root.Subcommands.Add(list);
        return root;
    }

    private static IReadOnlyList<PdfDemo> Select(string[]? names)
    {
        if (names is null || names.Length == 0)
            return DemoRegistry.All;

        var chosen = new List<PdfDemo>(names.Length);
        foreach (var name in names)
        {
            // The validator has already rejected anything unknown, so a miss here cannot happen.
            if (DemoRegistry.TryGet(name, out var demo) && !chosen.Contains(demo))
                chosen.Add(demo);
        }

        return chosen;
    }
}
