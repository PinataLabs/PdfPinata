using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Generators.Tests;

/// <summary>
/// MDG001 to MDG006 each replaced a condition that used to fail at run time or not at all. Until
/// now none had a test proving it fires, which made them decorative - MDG002 in particular, since
/// it replaced a Debug.Assert(false) that is a no-op in Release.
/// </summary>
public class DiagnosticTests
{
    const string Ns = "using PinataLayout.DocumentObjectModel;\nusing PinataLayout.DocumentObjectModel.Internals;\nnamespace Probe;\n";

    [Fact]
    public void AValidTypeProducesATableAndNoDiagnostics()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;
                [DV] internal string caption;
                [DV] internal Unit width;
            }
            """);

        result.Ids.Should().BeEmpty();
        result.AllGenerated.Should().Contain("partial class Widget");
        result.AllGenerated.Should().Contain("\"visible\"").And.Contain("\"caption\"").And.Contain("\"width\"");
        result.AllGenerated.Should().Contain("\"parent\"", "inherited [DV] members are included");
    }

    [Fact]
    public void TheGeneratedTableCompiles()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;
            }
            """);

        result.CompilationErrors.Should().BeEmpty(
            "the emitted source must bind, not merely be produced");
    }

    [Fact]
    public void EachKindIsClassifiedAsTheModelExpects()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Child : DocumentObject { }
            public partial class Bag : DocumentObjectCollection { }
            public enum Shade { None, Some }

            public partial class Widget : DocumentObject
            {
                [DV] internal bool? nullableValueType;
                [DV] internal string text;
                [DV] internal Shade? nullableEnum;
                [DV] internal Unit tracksItsOwnNull;
                [DV] public bool PlainBool { get; set; }
                [DV] internal Child child;
                [DV] internal Bag bag;
            }
            """);

        result.Ids.Should().BeEmpty();
        string generated = result.AllGenerated;

        generated.Should().Contain("valueName: \"nullableValueType\"").And.Contain("ValueKind.Leaf");
        generated.Should().Contain("valueName: \"tracksItsOwnNull\"");
        generated.Should().Contain("ValueKind.NullableValue", "Unit implements INullableValue");
        generated.Should().Contain("ValueKind.PlainValue", "a plain bool has no null of its own");
        generated.Should().Contain("ValueKind.DocumentObject");
        generated.Should().Contain("ValueKind.Collection");
    }

    [Fact]
    public void MDG001_TypeWithDvMembersMustBePartial()
    {
        var result = GeneratorHarness.Run(Ns + """
            public class Widget : DocumentObject
            {
                [DV] internal bool? visible;
            }
            """);

        result.Ids.Should().Contain("MDG001");
    }

    [Fact]
    public void MDG002_MemberTypeTheModelCannotDescribe()
    {
        // Replaces Debug.Assert(false, type.FullName) followed by a null descriptor - nothing at
        // all in Release, and a NullReferenceException in the DDL parser much later.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal System.Collections.Generic.List<int> unsupported;
            }
            """);

        result.Ids.Should().Contain("MDG002");
    }

    [Fact]
    public void MDG003_StaticMember()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal static bool? visible;
            }
            """);

        result.Ids.Should().Contain("MDG003");
    }

    [Fact]
    public void MDG003_PrivateMember()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] private bool? visible;
            }
            """);

        result.Ids.Should().Contain("MDG003");
    }

    [Fact]
    public void MDG004_TwoMembersCollidingUnderCaseInsensitiveLookup()
    {
        // The name table is case-insensitive. Before the generator this threw from Hashtable.Add
        // at first use of the type, far from the declarations that caused it.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? caption;
                [DV] public bool? Caption { get; set; }
            }
            """);

        result.Ids.Should().Contain("MDG004");
    }

    [Fact]
    public void MDG005_DvOutsideADocumentObject()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class NotADomType
            {
                [DV] internal bool? visible;
            }
            """);

        result.Ids.Should().Contain("MDG005");
    }

    [Fact]
    public void MDG006_RefOnlyOnAValueMember()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV(RefOnly = true)] internal bool? visible;
            }
            """);

        result.Ids.Should().Contain("MDG006");
    }

    [Fact]
    public void MDG007_MemberNamedInNoStringLiteralWithinSerialize()
    {
        // The check this task's spec asks for: a member the generator drives correctly, but the
        // hand-written Serialize this type declares never mentions by name - exactly the shape of
        // Font.strikethrough going missing from FlattenFont, aimed at Serialize instead.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;

                internal void Serialize(object serializer) { }
            }
            """);

        result.Ids.Should().Contain("MDG007");
    }

    [Fact]
    public void MDG007_SaysNothingWhenTheMemberIsNamed()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;

                internal void Serialize(object serializer)
                {
                    string attributeName = "Visible";
                }
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }

    [Fact]
    public void MDG007_MatchesCaseInsensitively()
    {
        // A [DV] field is conventionally camelCase and the DDL attribute name Serialize writes it
        // under is the PascalCase property - "visible" against "Visible" - so the match cannot be
        // case-sensitive without flagging every well-behaved type in the DOM.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;

                internal void Serialize(object serializer)
                {
                    string attributeName = "VISIBLE";
                }
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }

    [Fact]
    public void MDG007_SaysNothingAboutATypeWithNoSerializeOfItsOwn()
    {
        // No Serialize method means this type is not the one responsible for getting its members
        // into DDL - a base class's Serialize, or none at all. Nothing to check.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }

    [Fact]
    public void MDG007_SaysNothingAboutARefOnlyMember()
    {
        // RefOnly exists so parent isn't walked forever by IsNull and SetNull; the same reason
        // excludes it from a value's DDL, so it should not be flagged for missing from Serialize.
        var result = GeneratorHarness.Run(Ns + """
            public partial class Widget : DocumentObject
            {
                [DV(RefOnly = true)] internal Widget owner;

                internal void Serialize(object serializer) { }
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }

    /// <summary>
    /// A diagnostic with no location is reported against the project rather than the code, so the
    /// build says what is wrong without saying where. Every one of these used to be located except
    /// MDG004, which had no way to be: the collision is only visible once the base chain is closed,
    /// by which point the symbol that caused it is gone. Carrying a value-equatable LocationInfo
    /// through the pipeline is what fixed it, and this is the test that keeps it fixed.
    /// </summary>
    [Theory]
    [InlineData("MDG001", "public class Widget : DocumentObject { [DV] internal bool? visible; }")]
    [InlineData("MDG002", "public partial class Widget : DocumentObject { [DV] internal System.Collections.Generic.List<int> bad; }")]
    [InlineData("MDG003", "public partial class Widget : DocumentObject { [DV] internal static bool? visible; }")]
    [InlineData("MDG004", "public partial class Widget : DocumentObject { [DV] internal bool? caption; [DV] public bool? Caption { get; set; } }")]
    [InlineData("MDG005", "public partial class NotADomType { [DV] internal bool? visible; }")]
    [InlineData("MDG006", "public partial class Widget : DocumentObject { [DV(RefOnly = true)] internal bool? visible; }")]
    [InlineData("MDG007", "public partial class Widget : DocumentObject { [DV] internal bool? visible; internal void Serialize(object serializer) { } }")]
    public void EveryDiagnosticPointsAtSource(string id, string snippet)
    {
        var result = GeneratorHarness.Run(Ns + snippet);

        Diagnostic reported = result.Diagnostics.Single(d => d.Id == id);

        reported.Location.Should().NotBe(Location.None, "a diagnostic with no location cannot be navigated to");
        reported.Location.GetLineSpan().Path.Should().Be(GeneratorHarness.SnippetPath);
    }

    [Fact]
    public void MDG004_PointsAtTheLaterOfTheTwoDeclarations()
    {
        // Base chain is walked base first, so the second declaration seen is the one the collision
        // is attributable to - and the one a reader would delete or rename.
        string snippet = """
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? caption;
                [DV] public bool? Caption { get; set; }
            }
            """;
        var result = GeneratorHarness.Run(Ns + snippet);

        Diagnostic collision = result.Diagnostics.Single(d => d.Id == "MDG004");

        int expected = LineOf(Ns + snippet, "public bool? Caption");
        collision.Location.GetLineSpan().StartLinePosition.Line.Should().Be(expected);
    }

    static int LineOf(string source, string needle) =>
        source.Replace("\r\n", "\n").Split('\n').ToList().FindIndex(line => line.Contains(needle));

    /// <summary>
    ///   A type declaring no [DV] member of its own still needs a table - it inherits parent, and
    ///   without one it fails to implement DocumentObject.Meta. Fifteen real DOM types are in this
    ///   position, and an attribute-driven provider alone cannot see them.
    /// </summary>
    [Fact]
    public void ATypeWithNoDvMembersOfItsOwnStillGetsATable()
    {
        var result = GeneratorHarness.Run(Ns + """
            public partial class Empty : DocumentObject { }
            """);

        result.Ids.Should().BeEmpty();
        result.AllGenerated.Should().Contain("partial class Empty");
        result.AllGenerated.Should().Contain("\"parent\"");
        result.CompilationErrors.Should().BeEmpty();
    }

    /// <summary>
    ///   An abstract class needs no table: it cannot be instantiated, and its members are picked up
    ///   by every concrete type below it.
    /// </summary>
    [Fact]
    public void AnAbstractTypeGetsNoTableButItsMembersAreInherited()
    {
        var result = GeneratorHarness.Run(Ns + """
            public abstract partial class Shape : DocumentObject
            {
                [DV] internal Unit width;
            }

            public partial class Box : Shape
            {
                [DV] internal bool? visible;
            }
            """);

        result.Ids.Should().BeEmpty();
        result.GeneratedSources.Should().NotContain(s => s.Contains("partial class Shape"));
        result.AllGenerated.Should().Contain("partial class Box");
        result.AllGenerated.Should().Contain("\"width\"", "Box inherits Shape's [DV] members");
        result.AllGenerated.Should().Contain("\"visible\"");
    }

    [Fact]
    public void MDG007_APragmaDoesNotSuppressIt()
    {
        // Checked against a real build of the DOM, not guessed: a plain #pragma warning disable
        // MDG007 compiles cleanly but the warning still appears. A generator's own diagnostics do
        // not go through the same in-source suppression path an ordinary analyzer's do, which is
        // why SuppressSerializeCheckAttribute exists instead of relying on this. Pinned here so a
        // future refactor that moves the check into a real DiagnosticAnalyzer - where a pragma
        // would start working - does not silently change what a suppressed type looks like.
        var result = GeneratorHarness.Run(Ns + """
            #pragma warning disable MDG007
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;

                internal void Serialize(object serializer) { }
            }
            #pragma warning restore MDG007
            """);

        result.Ids.Should().Contain("MDG007");
    }

    [Fact]
    public void MDG007_SuppressedByAttributeOnTheClass()
    {
        // The mechanism this task's spec actually asks for: "suppress it once, in source, with a
        // reason" - answered before the diagnostic is ever created, since a #pragma cannot do it.
        var result = GeneratorHarness.Run(Ns + """
            [SuppressSerializeCheck("this type builds its DDL keyword by concatenation")]
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;

                internal void Serialize(object serializer) { }
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }

    [Fact]
    public void MDG007_TheAttributeSuppressesEveryMemberOfTheType()
    {
        var result = GeneratorHarness.Run(Ns + """
            [SuppressSerializeCheck("keyword-writer")]
            public partial class Widget : DocumentObject
            {
                [DV] internal bool? visible;
                [DV] internal bool? alsoUnmentioned;

                internal void Serialize(object serializer) { }
            }
            """);

        result.Ids.Should().NotContain("MDG007");
    }
}
