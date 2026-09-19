using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   The gate for replacing the DOM's reflection-built value model with a generated one.
///
///   Each test compares the live Meta for every concrete DocumentObject against ReflectionMeta,
///   which reimplements the discovery rules independently. Today both sides reflect, so these
///   assert that the rules are written down correctly. When Meta becomes generated they assert that
///   the generator produces the same model - unchanged, which is the point of writing them first.
/// </summary>
public class ValueModelParityTests
{
    /// <summary>
    ///   Most DOM types have internal or parameterised constructors, and some do real work in them.
    ///   Meta only ever depends on the type, so an uninitialised instance is enough to reach it.
    /// </summary>
    static Meta MetaFor(Type type) =>
        Meta.GetMeta((DocumentObject)RuntimeHelpers.GetUninitializedObject(type));

    static List<ValueDescriptor> Descriptors(Meta meta) => meta.ValueDescriptors.ToList();

    public static TheoryData<Type> DomTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in ReflectionMeta.AllDocumentObjectTypes())
            data.Add(type);
        return data;
    }

    [Fact]
    public void TheSweepCoversTheWholeDom()
    {
        // A guard on the harness itself. If this ever collapses to a handful of types, every other
        // test in this file passes vacuously.
        ReflectionMeta.AllDocumentObjectTypes().Should().HaveCountGreaterThan(60);
    }

    [Theory]
    [MemberData(nameof(DomTypes))]
    public void TheModelContainsExactlyTheExpectedMembers(Type type)
    {
        var expected = ReflectionMeta.Build(type).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal);
        var actual = Descriptors(MetaFor(type)).Select(d => d.ValueName).OrderBy(n => n, StringComparer.Ordinal);

        actual.Should().Equal(expected, $"{type.Name}'s value model must not gain or lose members");
    }

    [Theory]
    [MemberData(nameof(DomTypes))]
    public void EveryMemberKeepsItsTypesAndFlags(Type type)
    {
        var meta = MetaFor(type);

        foreach (var expected in ReflectionMeta.Build(type))
        {
            var actual = meta[expected.Name];
            actual.Should().NotBeNull($"{type.Name}.{expected.Name} is in the model");

            actual.ValueType.Should().Be(expected.ValueType, $"{type.Name}.{expected.Name} value type");
            actual.MemberType.Should().Be(expected.MemberType, $"{type.Name}.{expected.Name} member type");
            actual.IsRefOnly.Should().Be(expected.IsRefOnly, $"{type.Name}.{expected.Name} RefOnly");
            actual.Kind.ToString().Should().Be(expected.ExpectedKind, $"{type.Name}.{expected.Name} descriptor kind");
        }
    }

    [Theory]
    [MemberData(nameof(DomTypes))]
    public void NoMemberFallsThroughToAnUnsupportedShape(Type type)
    {
        // Meta answers an unhandled member type with Debug.Assert(false) and a null descriptor,
        // which is a NullReferenceException later and nothing at all in Release. The generator
        // turns this into a build error (MDG002); this is the check until then.
        ReflectionMeta.Build(type)
            .Where(m => m.ExpectedKind == "UNSUPPORTED")
            .Should().BeEmpty($"{type.Name} has a [DV] member no descriptor kind handles");
    }

    [Theory]
    [MemberData(nameof(DomTypes))]
    public void NameLookupIsCaseInsensitive(Type type)
    {
        var meta = MetaFor(type);

        foreach (var descriptor in Descriptors(meta))
        {
            meta[descriptor.ValueName.ToUpperInvariant()].Should().BeSameAs(descriptor);
            meta[descriptor.ValueName.ToLowerInvariant()].Should().BeSameAs(descriptor);
        }
    }

    [Theory]
    [MemberData(nameof(DomTypes))]
    public void NoTwoMembersCollideUnderCaseInsensitiveLookup(Type type)
    {
        // The name table is case-insensitive and built with Hashtable.Add, which throws on a
        // duplicate. Two [DV] members differing only in case would fail at first use of the type,
        // far from the declaration that caused it.
        var names = ReflectionMeta.Build(type).Select(m => m.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
        names.Select(n => n.ToUpperInvariant()).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    ///   Inherited [DV] members are included. This is the rule with the most riding on it - the
    ///   generator has to walk the base chain to match - so it is asserted directly rather than
    ///   left implicit in the sweep above.
    /// </summary>
    [Fact]
    public void InheritedMembersAreIncluded()
    {
        // parent is declared protected internal on DocumentObject and carries [DV(RefOnly = true)].
        foreach (var type in ReflectionMeta.AllDocumentObjectTypes())
        {
            var parent = MetaFor(type)["parent"];
            parent.Should().NotBeNull($"{type.Name} inherits parent from DocumentObject");
            parent.IsRefOnly.Should().BeTrue($"{type.Name}.parent must stay out of recursive walks");
        }

        // Image derives from Shape, whose [DV] fields are internal rather than protected.
        var image = MetaFor(typeof(PinataLayout.DocumentObjectModel.Shapes.Image));
        image["relativeHorizontal"].Should().NotBeNull("declared on the Shape base class");
        image["width"].Should().NotBeNull("declared on the Shape base class");
    }

    /// <summary>
    ///   RefOnly is what stops IsNull() and SetNull() walking up the parent chain forever. Exactly
    ///   one member in the whole DOM has it.
    /// </summary>
    [Fact]
    public void ParentIsTheOnlyRefOnlyMember()
    {
        foreach (var type in ReflectionMeta.AllDocumentObjectTypes())
        {
            var refOnly = Descriptors(MetaFor(type)).Where(d => d.IsRefOnly).Select(d => d.ValueName);
            refOnly.Should().Equal(new[] { "parent" }, $"{type.Name}");
        }
    }

    /// <summary>
    ///   A worked example, so a change to the model shows up as an obviously wrong list rather than
    ///   as a count.
    /// </summary>
    [Fact]
    public void BorderHasExactlyTheMembersItsDeclarationsSay()
    {
        Descriptors(MetaFor(typeof(Border)))
            .Select(d => d.ValueName)
            .Should().BeEquivalentTo(new[] { "parent", "visible", "style", "width", "color" });

        // fClear deliberately carries no [DV] - it is a serialization instruction, not a value.
        MetaFor(typeof(Border))["fClear"].Should().BeNull();
    }
}
