using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PinataLayout.DocumentObjectModel.Generators.Tests;

/// <summary>
/// Runs the generator twice over two separately parsed compilations and reports why each pipeline
/// step ran the second time.
/// </summary>
/// <remarks>
/// The models exist to be compared by value - <c>DomMemberModel</c>, <c>ParsedMember</c>,
/// <c>ParsedType</c> and <c>DiagnosticInfo</c> are all records of strings and enums, and
/// <c>EquatableArray</c> exists because ImmutableArray would compare by reference. None of that is
/// observable from outside the assembly: every one of those types is internal, and this repository
/// has no InternalsVisibleTo. What *is* observable is the consequence - whether the driver reports
/// a step as cached when it is handed an equivalent compilation - so that is what these tests
/// assert, through the public <see cref="DomValueModelGenerator"/> and Roslyn's own step tracking.
/// </remarks>
static class IncrementalCachingProbe
{
    /// <summary>
    /// Runs <paramref name="source"/>, then runs the same generator again over a freshly parsed
    /// compilation of <paramref name="secondSource"/> (the same text unless given otherwise), and
    /// returns the run result of the second pass.
    /// </summary>
    public static GeneratorRunResult RunTwice(string source, string? secondSource = null)
    {
        var first = GeneratorHarness.CreateCompilation(source);
        var second = GeneratorHarness.CreateCompilation(secondSource ?? source);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new DomValueModelGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(first);
        driver = driver.RunGenerators(second);

        return driver.GetRunResult().Results.Single();
    }

    /// <summary>
    /// Why each output step ran, as "stepName=Reason" pairs, sorted - a single string so that one
    /// assertion covers the whole pipeline and a failure names what actually happened.
    /// </summary>
    public static string OutputReasons(GeneratorRunResult result) =>
        Describe(result.TrackedOutputSteps.SelectMany(entry =>
            entry.Value.SelectMany(step => step.Outputs.Select(output => (entry.Key, output.Reason)))));

    /// <summary>
    /// Why each tracked step ran, including the intermediate ones.
    /// </summary>
    public static string AllReasons(GeneratorRunResult result) =>
        Describe(result.TrackedSteps.SelectMany(entry =>
            entry.Value.SelectMany(step => step.Outputs.Select(output => (entry.Key, output.Reason)))));

    static string Describe(IEnumerable<(string Name, IncrementalStepRunReason Reason)> pairs) =>
        string.Join(", ", pairs
            .GroupBy(pair => pair.Name + "=" + pair.Reason)
            .OrderBy(group => group.Key)
            .Select(group => group.Count() == 1 ? group.Key : group.Key + "x" + group.Count()));
}
