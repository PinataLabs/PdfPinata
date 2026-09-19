using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace SampleApp.Infrastructure;

/// <summary>Everything written to the terminal, in one place.</summary>
public static class Ui
{
    public static void WriteDemoList(IReadOnlyList<PdfDemo> demos)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey35)
            .AddColumn("[bold]Demo[/]")
            .AddColumn("[bold]Pages[/]", column => column.RightAligned())
            .AddColumn("[bold]Shows[/]");

        foreach (PdfDemo demo in demos)
        {
            table.AddRow(
                new Markup($"[bold]{Markup.Escape(demo.Name)}[/]\n[grey]{Markup.Escape(demo.Summary)}[/]"),
                new Markup(demo.PageCount.ToString()),
                new Markup(string.Join("\n", Bulleted(demo.Shows))));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            "\n[grey]Run them all with[/] [bold]run[/][grey], or some of them with[/] "
            + "[bold]run --example <name> <name>[/][grey].[/]");
    }

    public static void WriteDemoHeading(PdfDemo demo)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(demo.Name)}[/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("grey35"),
        });
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(demo.Summary)}[/]");
    }

    /// <summary>
    ///   Prints the demo's own source - the code that is about to run, read back from the file it
    ///   was compiled from or the copy embedded in this assembly.
    /// </summary>
    public static void WriteSource(PdfDemo demo)
    {
        string? example = DemoSource.Example(demo);
        if (example is null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No '{DemoSource.BeginMarker}' region found in "
                + $"{Markup.Escape(demo.SourceFileName)}.[/]");
            return;
        }

        string[] lines = example.Split('\n');
        StringBuilder markup = new StringBuilder();
        for (int index = 0; index < lines.Length; index++)
        {
            if (index > 0)
                markup.Append('\n');

            markup.Append($"[grey30]{index + 1,3}[/] ")
                  .Append(CSharpHighlighter.Highlight(lines[index].TrimEnd('\r')));
        }

        AnsiConsole.Write(new Panel(new Markup(markup.ToString()))
        {
            Header = new PanelHeader($" {Markup.Escape(demo.SourceFileName)} "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey35"),
            Expand = true,
        });
    }

    public static void WriteResult(DemoResult result)
    {
        AnsiConsole.MarkupLine(
            $"[green]wrote[/] {Markup.Escape(result.OutputPath)}  "
            + $"[grey]{result.PageCount} page{(result.PageCount == 1 ? "" : "s")} · "
            + $"{Kilobytes(result.Bytes)} · {result.Elapsed.TotalMilliseconds:F0} ms[/]");

        // A file that will not open is a failed demo as far as anybody looking at the output
        // directory is concerned, so the one demo that encrypts its own output says so here.
        if (result.OpenPassword is not null)
        {
            AnsiConsole.MarkupLine(
                $"      [yellow]encrypted[/] [grey]· opens with the password[/] "
                + $"[bold]{Markup.Escape(result.OpenPassword)}[/]");
        }
    }

    public static void WriteFailure(PdfDemo demo, Exception exception)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(demo.Name)} failed[/]");
        AnsiConsole.WriteException(exception, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
    }

    public static void WriteSummary(int succeeded, int failed, string outputDirectory)
    {
        AnsiConsole.WriteLine();

        string wrote = $"{succeeded} PDF{(succeeded == 1 ? "" : "s")} in {Markup.Escape(outputDirectory)}";
        AnsiConsole.MarkupLine(failed == 0
            ? $"[green]Done.[/] [grey]{wrote}[/]"
            : $"[red]{failed} demo{(failed == 1 ? "" : "s")} failed.[/] [grey]{wrote}[/]");
    }

    static IEnumerable<string> Bulleted(IReadOnlyList<string> items)
    {
        foreach (string item in items)
            yield return "[grey]·[/] " + Markup.Escape(item);
    }

    static string Kilobytes(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:F0} KB";
}
