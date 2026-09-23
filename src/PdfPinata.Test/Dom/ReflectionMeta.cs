using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PinataLayout.DocumentObjectModel;

namespace PdfPinata.Test.Dom;

/// <summary>
///   An independent reimplementation of the member discovery in Meta.AddValueDescriptors, kept in
///   the test project so the value model has something to be checked against.
/// </summary>
/// <remarks>
///   <para>
///     This exists to make the move to a generated value model verifiable. While Meta still
///     reflects, this asserts that the rules below are the rules Meta actually follows. Once Meta is
///     generated, the same assertions prove the generator produces the same member set - which is
///     the only thing standing between "the generator compiles" and "the generator is correct".
///   </para>
///   <para>
///     Deliberately written against the public surface only. DVAttribute is internal, so it is
///     matched by name rather than by type, which means this file cannot drift into simply calling
///     the code it is supposed to be checking.
///   </para>
/// </remarks>
public static class ReflectionMeta
{
    public sealed record Member(
        string Name,
        Type MemberType,
        Type ValueType,
        bool IsRefOnly,
        string ExpectedKind);

    /// <summary>
    ///   Every concrete DocumentObject in the DOM assembly. The hierarchy is closed - DocumentObject
    ///   declares Meta as internal abstract, so no other assembly can add to it - which is what
    ///   makes an exhaustive sweep possible.
    /// </summary>
    public static IEnumerable<Type> AllDocumentObjectTypes() =>
        typeof(Document).Assembly
            .GetTypes()
            .Where(t => typeof(DocumentObject).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    /// <summary>
    ///   The member set Meta builds for a type, by the same rules: instance fields and properties,
    ///   inherited ones included, that carry [DV].
    /// </summary>
    public static List<Member> Build(Type type)
    {
        var members = new List<Member>();

        foreach (var field in type.GetRuntimeFields())
        {
            var dv = FindDv(field);
            if (dv != null)
                members.Add(Describe(field.Name, field.FieldType, dv));
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var dv = FindDv(property);
            if (dv != null)
                members.Add(Describe(property.Name, property.PropertyType, dv));
        }

        return members;
    }

    /// <summary>
    ///   Exactly one [DV], matched by attribute type name because the attribute is internal.
    ///   Meta requires exactly one too - it tests dvs.Count() == 1 - so more than one means the
    ///   member is not in the model at all.
    /// </summary>
    private static object FindDv(MemberInfo member)
    {
        var found = member.GetCustomAttributes(false)
            .Where(a => a.GetType().Name == "DVAttribute")
            .ToArray();
        return found.Length == 1 ? found[0] : null;
    }

    private static Member Describe(string name, Type memberType, object dv)
    {
        // ReSharper disable once PossibleNullReferenceException
        var refOnly = (bool)dv.GetType().GetField("RefOnly").GetValue(dv);

        // Nullable<T> first: it is a struct, so the value-type test below would swallow it.
        var underlying = Nullable.GetUnderlyingType(memberType);
        if (underlying != null)
            return new Member(name, memberType, underlying, refOnly, "Leaf");

        if (memberType == typeof(string))
            return new Member(name, memberType, typeof(string), refOnly, "Leaf");

        if (memberType.IsSubclassOf(typeof(ValueType)))
        {
            // INullableValue is internal, so it is matched by name like DVAttribute is. A struct
            // that implements it tracks its own null; a plain bool or enum has none to track.
            var tracksItsOwnNull = memberType.GetInterfaces().Any(i => i.Name == "INullableValue");
            return new Member(name, memberType, memberType, refOnly,
                tracksItsOwnNull ? "NullableValue" : "PlainValue");
        }

        if (IsAssignableToNamed(memberType, "DocumentObjectCollection"))
            return new Member(name, memberType, memberType, refOnly, "Collection");

        return typeof(DocumentObject).IsAssignableFrom(memberType)
            ? new Member(name, memberType, memberType, refOnly, "DocumentObject")
            : new Member(name, memberType, memberType, refOnly, "UNSUPPORTED");
    }

    private static bool IsAssignableToNamed(Type type, string baseName)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.Name == baseName)
                return true;
        return false;
    }
}
