using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Generators.Tests;

/// <summary>
/// The generator's models are records of strings and enums, and <c>EquatableArray</c> exists inside
/// them, so that an incremental pipeline can tell a rebuild of the same thing from a change to it.
/// Nothing had ever tested that it works.
/// </summary>
/// <remarks>
/// <para>
/// It matters in both directions. Compare too coarsely and every keystroke re-runs every downstream
/// stage and re-emits identical source, which is the failure <c>EquatableArray</c>'s own summary
/// describes and which is invisible except as an IDE that has become slow. Compare too eagerly and
/// the generator serves stale source after a real edit, which is worse.
/// </para>
/// <para>
/// None of those types can be reached directly: <c>EquatableArray</c>, <c>DomMemberModel</c>,
/// <c>ParsedMember</c>, <c>ParsedType</c> and <c>DiagnosticInfo</c> are all internal, and this
/// repository has no <c>InternalsVisibleTo</c>. So these assert the consequence instead, through the
/// public <see cref="DomValueModelGenerator"/> and Roslyn's own step tracking: run the generator
/// over one compilation, run it again over a second compilation parsed separately from the same or
/// different text, and ask the driver why the source-output step ran. The two compilations share no
/// syntax tree and no symbol, so a driver that reports <c>Cached</c> can only have got there by
/// comparing the models by value.
/// </para>
/// </remarks>
public class IncrementalCachingTests
{
    const string Ns = "using PinataLayout.DocumentObjectModel;\nusing PinataLayout.DocumentObjectModel.Internals;\nnamespace Probe;\n";

    const string Widget = """
        public partial class Widget : DocumentObject
        {
            [DV] internal bool? visible;
            [DV] internal string caption;
            [DV] internal Unit width;
        }
        """;

    /// <summary>A DocumentObject that is not partial, which is MDG003.</summary>
    static string NotPartial(string name) => $$"""
        public class {{name}} : DocumentObject
        {
            [DV] internal bool? visible;
        }
        """;

    [Fact]
    public void RecompilingTheSameSourceIsServedFromTheCacheRatherThanRegenerated()
    {
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(Ns + Widget);

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Cached",
            "the second compilation is parsed separately, so only value equality on the models can "
            + "make the driver reuse the first run's output");
    }

    [Fact]
    public void RenamingAMemberIsNotServedFromTheCache()
    {
        // The complement of the test above, and the reason it means anything: a pipeline that
        // cached everything unconditionally would pass that one and fail this.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + Widget.Replace("caption", "heading"));

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void ChangingAMembersTypeIsNotServedFromTheCache()
    {
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + Widget.Replace("internal string caption", "internal Unit caption"));

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void ReorderingMembersIsNotServedFromTheCache()
    {
        // Declaration order reaches the model deliberately, so that the generated table is
        // reproducible build to build. Moving a member is therefore a real change.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + """
                public partial class Widget : DocumentObject
                {
                    [DV] internal string caption;
                    [DV] internal bool? visible;
                    [DV] internal Unit width;
                }
                """);

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    // -------------------------------------------------------------------------------------------
    // The diagnostic path, which is the one place an EquatableArray is compared for caching.
    //
    // DomTypeModel also holds one - EquatableArray<DomMemberModel> - but it is built inside the
    // source-output callback, downstream of every cache, so nothing ever compares it. DiagnosticInfo
    // is the model that actually travels through the pipeline holding an EquatableArray<string>,
    // and these three reach its Equals: equal contents, differing contents, differing lengths.
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void RaisingTheSameDiagnosticTwiceIsStillServedFromTheCache()
    {
        // Equal contents: the loop runs to the end and answers true.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(Ns + NotPartial("Broken"));

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Cached",
            "a diagnostic that has not changed is not a reason to regenerate");
    }

    [Fact]
    public void ADiagnosticNamingSomethingElseIsNotServedFromTheCache()
    {
        // Differing contents: the message argument is the offending type's name, so renaming it
        // changes one string inside the EquatableArray and nothing else about the shape.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + NotPartial("Broken"),
            Ns + NotPartial("AlsoBroken"));

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void ASecondDiagnosticIsNotServedFromTheCache()
    {
        // Differing lengths, at the level of the collected pipeline output.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + NotPartial("Broken"),
            Ns + NotPartial("Broken") + "\n" + NotPartial("AlsoBroken"));

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void AWhollyUnrelatedTypeStillCountsAsAChange()
    {
        // Worth pinning rather than assuming. The pipeline collects every DocumentObject, so a new
        // one is a new element in the collected array whether or not it affects any existing table.
        // This is the cost of the Collect() that the 15 member-less DOM types make necessary.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + Widget + "\npublic partial class Other : DocumentObject { }");

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void AnEditAboveADeclarationIsNotServedFromTheCacheEvenWhenItChangesNothing()
    {
        // Reducing the models to strings and enums keeps trivia out of them, so the natural
        // expectation is that adding a comment regenerates nothing. It does regenerate, and this
        // pins that rather than the expectation, because the reason is structural.
        //
        // Two members of the model are absolute source positions: ParsedMember.DeclarationOrder is
        // TargetNode.GetLocation().SourceSpan.Start, and LocationInfo carries the TextSpan and
        // LineSpan. Anything inserted above a declaration moves both, so the model differs and the
        // whole emit re-runs - for a comment, a blank line, or an unrelated using directive.
        //
        // DomModels.cs already says of Location that it "costs nothing in cache terms that
        // DeclarationOrder does not already cost: both change when the declaration moves". That is
        // true and is the point: the cost was already there. It is recorded here rather than fixed
        // because the cheap fix - an ordinal index within the type instead of a source position -
        // is not available to ForAttributeWithMetadataName, which sees one member at a time and
        // never its siblings. See the backlog spec's batch 16 for the argument.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + "// a remark that changes nothing\n" + Widget);

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Modified");
    }

    [Fact]
    public void AnEditBelowEveryDeclarationIsServedFromTheCache()
    {
        // The other side of it, and what makes the test above a statement about position rather
        // than about trivia: the same comment added after everything moves no declaration, so the
        // models are equal and the output is reused.
        GeneratorRunResult result = IncrementalCachingProbe.RunTwice(
            Ns + Widget,
            Ns + Widget + "\n// a remark that changes nothing");

        IncrementalCachingProbe.OutputReasons(result).Should().Be("SourceOutput=Cached");
    }
}
