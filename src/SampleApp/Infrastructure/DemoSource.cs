using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SampleApp.Infrastructure;

/// <summary>
///   Reads a demo's own source back, so what is printed is what ran.
/// </summary>
/// <remarks>
///   The alternative - a description of the code written beside the code - is a copy, and a copy
///   drifts. Nothing here is transcribed: the file is read, either from where it was compiled from
///   if that path still exists, or from the copy embedded in the assembly.
/// </remarks>
public static class DemoSource
{
    /// <summary>Marks the beginning of the part of a demo worth showing.</summary>
    public const string BeginMarker = "#region example";

    /// <summary>Marks the end of it.</summary>
    public const string EndMarker = "#endregion";

    /// <summary>
    ///   The whole file a demo is written in, or null if it can be found neither on disk nor in the
    ///   assembly.
    /// </summary>
    /// <remarks>
    ///   Disk first, so that editing a demo and running it again shows the edit. That path is the
    ///   build machine's and will not exist anywhere else, which is what the embedded copy is for.
    /// </remarks>
    public static string? Read(PdfDemo demo)
    {
        if (demo.SourceFilePath.Length > 0 && File.Exists(demo.SourceFilePath))
            return File.ReadAllText(demo.SourceFilePath);

        return Assets.TryReadText(Assets.SourcePrefix + demo.SourceFileName);
    }

    /// <summary>
    ///   The lines between <c>#region example</c> and its <c>#endregion</c>, dedented, or null if
    ///   the file has no such region.
    /// </summary>
    /// <remarks>
    ///   Markers rather than picking a method body out by matching braces. A scan for two literal
    ///   strings cannot be thrown off by a brace inside a string literal or a comment, and the
    ///   region is something the IDE understands too. Nested regions are not handled, because a
    ///   demo that needs one is a demo that is too long.
    /// </remarks>
    public static string? Example(PdfDemo demo)
    {
        string? text = Read(demo);
        return text is null ? null : ExampleFrom(text);
    }

    /// <summary>The extraction itself, split out so it can be tested against a string.</summary>
    public static string? ExampleFrom(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Split('\n');

        int begin = Array.FindIndex(lines, line => line.Trim() == BeginMarker);
        if (begin < 0)
            return null;

        int end = Array.FindIndex(lines, begin + 1, line => line.Trim().StartsWith(EndMarker, StringComparison.Ordinal));
        if (end < 0)
            return null;

        List<string> body = lines.Skip(begin + 1).Take(end - begin - 1).ToList();

        // Trim blank lines from both ends before measuring the indent, so a stray one does not
        // report an indent of zero and defeat the dedent.
        while (body.Count > 0 && body[0].Trim().Length == 0)
            body.RemoveAt(0);
        while (body.Count > 0 && body[body.Count - 1].Trim().Length == 0)
            body.RemoveAt(body.Count - 1);

        if (body.Count == 0)
            return null;

        int indent = body
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Length - line.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        return string.Join(
            Environment.NewLine,
            body.Select(line => line.Length >= indent ? line.Substring(indent) : line.TrimStart()));
    }
}
