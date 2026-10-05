using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinataLayout.DocumentObjectModel.Generators.Tests;

/// <summary>
/// Runs <see cref="DomValueModelGenerator"/> over a source snippet and returns what it produced.
/// </summary>
/// <remarks>
/// <para>
/// The snippet compiles against stand-ins for the handful of DOM types the generator looks for by
/// fully-qualified name, declared in <see cref="Preamble"/> rather than referenced from the real
/// assembly. That is not a shortcut: <c>DocumentObject.Meta</c> is <c>internal abstract</c>, so a
/// test compilation referencing the real assembly could not declare a DocumentObject at all - the
/// generated <c>internal override</c> cannot override an internal member from another assembly.
/// Declaring the types locally makes the test compilation self-contained and lets the generated
/// code actually bind.
/// </para>
/// <para>
/// The stand-ins mirror the real declarations in the ways the generator cares about, and one of
/// those is easy to get wrong: DocumentObject has no base list, so the type-collecting provider
/// never sees it, and its <c>parent</c> member reaches the model only through the attribute
/// provider. That is true of the real assembly too, and is why the base-chain walk tolerates a
/// missing entry.
/// </para>
/// </remarks>
internal static class GeneratorHarness
{
    /// <summary>
    /// Minimal stand-ins for the types the generator resolves by name. Prepended to every snippet.
    /// </summary>
    public const string Preamble = """
        namespace PinataLayout.DocumentObjectModel.Internals
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            internal sealed class DVAttribute : System.Attribute { public bool RefOnly; }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class SuppressSerializeCheckAttribute : System.Attribute
            {
                public SuppressSerializeCheckAttribute(string reason) { }
            }

            public interface INullableValue { bool IsNull { get; } void SetNull(); }

            public enum ValueKind { Leaf, NullableValue, PlainValue, DocumentObject, Collection }

            public enum GV { ReadOnly, ReadWrite, GetNull }

            public sealed class Meta
            {
                public Meta(params ValueDescriptor[] descriptors) { }
            }

            public sealed class ValueDescriptor
            {
                public ValueDescriptor(
                    string valueName,
                    System.Type valueType,
                    System.Type memberType,
                    ValueKind kind,
                    bool isRefOnly,
                    System.Func<global::PinataLayout.DocumentObjectModel.DocumentObject, object> getter,
                    System.Action<global::PinataLayout.DocumentObjectModel.DocumentObject, object> setter,
                    System.Func<global::PinataLayout.DocumentObjectModel.DocumentObject> factory = null,
                    object valueWhenNull = null,
                    bool isField = false) { }
            }
        }

        namespace PinataLayout.DocumentObjectModel
        {
            // No base list, exactly as in the real assembly - so the type provider never sees this
            // one and 'parent' arrives only through the attribute provider.
            public abstract partial class DocumentObject
            {
                internal abstract global::PinataLayout.DocumentObjectModel.Internals.Meta Meta { get; }

                [global::PinataLayout.DocumentObjectModel.Internals.DV(RefOnly = true)]
                protected internal DocumentObject parent;

                // The name-taking members MDG008 checks, with the real signatures and no bodies
                // worth the name - the analyzer reads the call, never runs it.
                public virtual object GetValue(string name) => GetValue(name, global::PinataLayout.DocumentObjectModel.Internals.GV.ReadWrite);
                public virtual object GetValue(string name, global::PinataLayout.DocumentObjectModel.Internals.GV flags) => null;
                public virtual void SetValue(string name, object val) { }
                public virtual bool HasValue(string name) => false;
                public virtual bool IsNull(string name) => false;
                public virtual void SetNull(string name) { }
                public virtual bool IsNull() => false;
            }

            public abstract partial class DocumentObjectCollection : DocumentObject { }

            public struct Unit : global::PinataLayout.DocumentObjectModel.Internals.INullableValue
            {
                public bool IsNull => false;
                public void SetNull() { }
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> References =
        [..((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];

    /// <param name="Diagnostics">
    /// What the generator itself reported. A `#pragma warning disable` in the snippet has no effect
    /// on this list - see <c>DiagnosticTests.MDG007_APragmaDoesNotSuppressIt</c>, which checked that
    /// against a real build rather than assuming it: a source generator's own diagnostics do not go
    /// through the same in-source suppression path an ordinary analyzer's do.
    /// </param>
    public sealed record Result(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        ImmutableArray<Diagnostic> CompilationErrors)
    {
        public IEnumerable<string> Ids => Diagnostics.Select(d => d.Id);

        public string AllGenerated => string.Concat(GeneratedSources);
    }

    /// <summary>The path the snippet is parsed under, so a test can assert where a diagnostic points.</summary>
    public const string SnippetPath = "Snippet.cs";

    /// <summary>
    /// The preamble and <paramref name="source"/> as one compilation. Every call parses afresh, so
    /// two calls with the same text produce structurally identical compilations that share no syntax
    /// tree or symbol - which is what a caching test needs in order to be about value equality
    /// rather than about reference equality.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            "GeneratorTests",
            [
                CSharpSyntaxTree.ParseText(Preamble, path: "Preamble.cs"),
                CSharpSyntaxTree.ParseText(source, path: SnippetPath)
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// What <see cref="DomValueNameAnalyzer"/> reports over the preamble and
    /// <paramref name="source"/>, after the generator has run - so the compilation it analyzes is
    /// the one a real build would hand it, generated tables included.
    /// </summary>
    public static ImmutableArray<Diagnostic> Analyze(string source) =>
        Analyze(CreateCompilation(source));

    /// <inheritdoc cref="Analyze(string)"/>
    public static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation)
    {
        CSharpGeneratorDriver.Create(new DomValueModelGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        return output
            .WithAnalyzers([new DomValueNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// The preamble and <paramref name="source"/>, generated tables included, compiled to an
    /// assembly another compilation can reference - a stand-in for the DOM as PinataLayout.Rendering
    /// sees it. Throws if it does not compile, since a test built on it would mean nothing.
    /// </summary>
    public static MetadataReference CompileToReference(string source)
    {
        CSharpGeneratorDriver.Create(new DomValueModelGenerator())
            .RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out var output, out _);

        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));

        return MetadataReference.CreateFromImage(image.ToArray());
    }

    /// <summary>A compilation of <paramref name="source"/> alone, referencing <paramref name="dom"/>.</summary>
    public static CSharpCompilation CreateConsumerCompilation(string source, MetadataReference dom) =>
        CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(source, path: SnippetPath)],
            [..References, dom],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static Result Run(string source)
    {
        var compilation = CreateCompilation(source);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new DomValueModelGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var run = driver.GetRunResult();

        return new Result(
            run.Diagnostics,
            [..run.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString())],
            [..output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)]);
    }
}
