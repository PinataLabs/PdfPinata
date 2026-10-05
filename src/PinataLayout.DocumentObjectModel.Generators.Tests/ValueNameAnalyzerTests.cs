using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Generators.Tests;

/// <summary>
/// MDG008: a value name handed to <c>IsNull</c>, <c>SetNull</c>, <c>HasValue</c>, <c>GetValue</c> or
/// <c>SetValue</c> that the receiver's value model does not have. At run time each of those throws
/// <c>InvalidValueName</c> - or, for <c>HasValue</c>, answers false - but only on the path that
/// reaches the line.
/// </summary>
public class ValueNameAnalyzerTests
{
    private const string Ns = "using PinataLayout.DocumentObjectModel;\nusing PinataLayout.DocumentObjectModel.Internals;\nnamespace Probe;\n";

    /// <summary>
    /// A small DOM: a format reached through a path, a font below it, an abstract base declaring a
    /// member its descendants inherit, and one descendant with a member of its own.
    /// </summary>
    private const string Dom = """
        public partial class Font : DocumentObject
        {
            [DV] internal bool? bold;
            [DV] internal string name;
        }

        public partial class Format : DocumentObject
        {
            [DV] internal Font font;
            [DV] internal Unit leftIndent;
        }

        public abstract partial class Shape : DocumentObject
        {
            [DV] internal Unit width;
            [DV] internal Format format;
        }

        public partial class Box : Shape
        {
            [DV] internal bool? visible;
        }

        public partial class Picture : Shape
        {
            [DV] internal string path;
        }
        """;

    private static Diagnostic[] Mdg008(string source) =>
        [..GeneratorHarness.Analyze(Ns + Dom + source).Where(d => d.Id == "MDG008")];

    [Fact]
    public void AMisspelledNameIsReportedAtTheName()
    {
        var reported = Mdg008("""
            public static class Use
            {
                public static bool M(Box box) => box.IsNull("Visibel");
            }
            """);

        reported.Should().ContainSingle();
        reported[0].GetMessage().Should().Contain("'Box' has no [DV] member named 'Visibel'");
        reported[0].Location.GetLineSpan().Path.Should().Be(GeneratorHarness.SnippetPath);
        reported[0].Location.SourceTree!.ToString()
            .Substring(reported[0].Location.SourceSpan.Start, reported[0].Location.SourceSpan.Length)
            .Should().Be("\"Visibel\"", "the diagnostic points at the name, not the whole call");
    }

    [Theory]
    [InlineData("box.IsNull(\"Visible\")")]
    [InlineData("box.IsNull(\"visible\")")]
    [InlineData("box.IsNull(\"VISIBLE\")")]
    [InlineData("box.GetValue(\"Visible\")")]
    [InlineData("box.HasValue(\"Visible\")")]
    [InlineData("box.SetNull(\"Visible\")")]
    [InlineData("box.SetValue(\"Visible\", null)")]
    public void AValidNameIsNotReportedWhateverItsCase(string call)
    {
        // Meta keys its descriptors case-insensitively, so the PascalCase property name the callers
        // write and the camelCase field the [DV] sits on are the same name.
        Mdg008($$"""
            public static class Use
            {
                public static object M(Box box) { {{call}}; return null; }
            }
            """).Should().BeEmpty();
    }

    [Theory]
    [InlineData("IsNull")]
    [InlineData("SetNull")]
    [InlineData("HasValue")]
    [InlineData("GetValue")]
    public void EveryNameTakingMemberIsChecked(string member)
    {
        Mdg008($$"""
            public static class Use
            {
                public static void M(Box box) { box.{{member}}("Widht"); }
            }
            """).Should().ContainSingle();
    }

    [Fact]
    public void SetValueIsChecked()
    {
        Mdg008("""
            public static class Use
            {
                public static void M(Box box) { box.SetValue("Widht", null); }
            }
            """).Should().ContainSingle();
    }

    [Fact]
    public void AnInheritedMemberIsAValue()
    {
        // width is declared on Shape, the abstract base; Box's value model includes it.
        Mdg008("""
            public static class Use
            {
                public static bool M(Box box) => box.IsNull("Width");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void AMemberOfADescendantIsAcceptedOnTheBase()
    {
        // The value model consulted at run time is the runtime type's, so on a Shape a name that
        // only Box declares may well be found. That is not something the static type can rule out.
        Mdg008("""
            public static class Use
            {
                public static bool M(Shape shape) => shape.IsNull("Visible") || shape.IsNull("Path");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void ANameNoDescendantDeclaresIsReportedOnTheBase()
    {
        Mdg008("""
            public static class Use
            {
                public static bool M(Shape shape) => shape.IsNull("Height");
            }
            """).Should().ContainSingle();
    }

    [Fact]
    public void AValidDottedPathIsFollowedStepByStep()
    {
        Mdg008("""
            public static class Use
            {
                public static bool M(Box box) =>
                    box.IsNull("Format.Font.Bold") || box.IsNull("Format.LeftIndent") || box.GetValue("format.font") == null;
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void AMisspelledLaterStepIsReportedAgainstThatStepsType()
    {
        var reported = Mdg008("""
            public static class Use
            {
                public static bool M(Box box) => box.IsNull("Format.Font.Bodl");
            }
            """);

        reported.Should().ContainSingle();
        reported[0].GetMessage().Should().Contain("'Font' has no [DV] member named 'Bodl'");
    }

    [Fact]
    public void APathCannotGoOnPastASimpleValue()
    {
        // Meta.IsNull throws for a trail after a simple value, and GetValue for a trail after
        // anything that is not a DocumentObject.
        var reported = Mdg008("""
            public static class Use
            {
                public static bool M(Box box) => box.IsNull("Width.Point");
            }
            """);

        reported.Should().ContainSingle();
        reported[0].GetMessage().Should().Contain("simple value");
    }

    [Theory]
    [InlineData("SetNull")]
    [InlineData("HasValue")]
    public void AMemberThatDoesNotFollowPathsIsReportedForADottedName(string member)
    {
        // Both look the whole string up as a single name, so a path can never match.
        var reported = Mdg008($$"""
            public static class Use
            {
                public static void M(Box box) { box.{{member}}("Format.Font"); }
            }
            """);

        reported.Should().ContainSingle();
        reported[0].GetMessage().Should().Contain("does not follow a dotted path");
    }

    [Fact]
    public void ACallOnTheImplicitThisIsChecked()
    {
        // The DOM's own Serialize methods ask IsNull("Format") of themselves, with no receiver.
        Mdg008("""
            public partial class Card : DocumentObject
            {
                [DV] internal Format format;

                internal bool Probe() => IsNull("Format") || IsNull("Fromat");
            }
            """).Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'Fromat'");
    }

    [Fact]
    public void ANameBuiltAtRunTimeIsNotChecked()
    {
        Mdg008("""
            public static class Use
            {
                public static bool M(Box box, string name) => box.IsNull(name) || box.IsNull(name + "x");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void AConstantIsCheckedLikeALiteral()
    {
        Mdg008("""
            public static class Use
            {
                private const string Name = "Visibel";
                public static bool M(Box box) => box.IsNull(Name);
            }
            """).Should().ContainSingle();
    }

    [Fact]
    public void AReceiverWhoseTypeOverridesTheMemberIsNotChecked()
    {
        // Style.GetValue sends every name beginning "font" to its paragraph format, so what an
        // override does with a name is not something the [DV] members say.
        Mdg008("""
            public partial class Routing : DocumentObject
            {
                [DV] internal Format format;

                public override object GetValue(string name) => format.GetValue(name);
            }

            public static class Use
            {
                public static object M(Routing r) => r.GetValue("LeftIndent");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void AReceiverTypedAsDocumentObjectAcceptsAnyValueOfAnyType()
    {
        Mdg008("""
            public static class Use
            {
                public static bool M(DocumentObject o) => o.IsNull("Path") || o.IsNull("Bold");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void AMethodOfTheSameNameOutsideTheDomIsNotChecked()
    {
        Mdg008("""
            public sealed class Lookalike
            {
                public bool IsNull(string name) => false;
            }

            public static class Use
            {
                public static bool M(Lookalike l) => l.IsNull("Anything");
            }
            """).Should().BeEmpty();
    }

    [Fact]
    public void ItSaysNothingAcrossAnAssemblyBoundary()
    {
        // The [DV] members are internal, and a compilation referencing the DOM is not shown them -
        // so from there every name would look unknown. PinataLayout.Rendering is that compilation,
        // and its calls name a public property through nameof instead.
        var dom = GeneratorHarness.CompileToReference(Ns + Dom);
        var consumer = GeneratorHarness.CreateConsumerCompilation("""
            using Probe;
            public static class Use
            {
                public static bool M(Box box) => box.IsNull("Visible") || box.IsNull("Visibel");
            }
            """, dom);

        consumer.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the consumer must bind, or the analyzer would have nothing to look at");
        GeneratorHarness.Analyze(consumer).Where(d => d.Id == "MDG008").Should().BeEmpty();
    }
}
