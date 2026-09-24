using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PinataLayout.DocumentObjectModel.Generators.Model;

namespace PinataLayout.DocumentObjectModel.Generators;

/// <summary>
/// Turns symbols into models. The only Roslyn-aware part of the generator beyond the entry point.
/// </summary>
internal static class Parser
{
    public const string DvAttribute = "PinataLayout.DocumentObjectModel.Internals.DVAttribute";
    private const string SuppressSerializeCheckAttribute = "PinataLayout.DocumentObjectModel.Internals.SuppressSerializeCheckAttribute";
    private const string DocumentObject = "PinataLayout.DocumentObjectModel.DocumentObject";
    private const string DocumentObjectCollection = "PinataLayout.DocumentObjectModel.DocumentObjectCollection";
    private const string NullableValue = "PinataLayout.DocumentObjectModel.Internals.INullableValue";

    private static readonly SymbolDisplayFormat Fqn = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// A [DV] member, or null with a diagnostic if it is one the model cannot describe.
    /// </summary>
    /// <remarks>
    /// The diagnostic comes back as a <see cref="Model.DiagnosticInfo"/> rather than a
    /// <see cref="Diagnostic"/> because this return value goes into the pipeline, and a Diagnostic
    /// carries a Location, which carries the SyntaxTree it came from.
    /// </remarks>
    public static (ParsedMember? Member, DiagnosticInfo? Error) Parse(GeneratorAttributeSyntaxContext context, int order)
    {
        var symbol = context.TargetSymbol;
        var owner = symbol.ContainingType;
        if (owner is null)
            return (null, null);

        var ownerFqn = owner.ToDisplayString(Fqn);
        var location = LocationInfo.From(symbol.Locations.FirstOrDefault());

        if (DeclarationError(symbol, owner, location) is { } declarationError)
            return (null, declarationError);

        var shape = ReadShape(symbol);
        if (shape is null)
            return (null, null);

        (var memberType, var isField, var isWritable) = shape.Value;

        (MemberKind kind, ITypeSymbol valueType)? classified = Classify(memberType);
        if (classified is null)
        {
            return (null, DiagnosticInfo.Create(Diagnostics.UnsupportedMemberType, location,
                owner.Name, symbol.Name, memberType.ToDisplayString()));
        }

        (var kind, var valueTypeSymbol) = classified.Value;
        var isRefOnly = ReadRefOnly(context.Attributes);

        if (isRefOnly && memberType.IsValueType)
        {
            return (null, DiagnosticInfo.Create(Diagnostics.RefOnlyOnValueType, location,
                owner.Name, symbol.Name));
        }

        var holdsObject = HoldsDocumentObject(kind);

        // A DocumentObject member is only assignable through the model when it is a field. The
        // descriptor class this replaces threw "This value cannot be set." for every property.
        var settable = holdsObject ? isField : isWritable;

        var member = new DomMemberModel(
            Name: symbol.Name,
            DeclaringTypeFqn: ownerFqn,
            MemberTypeFqn: memberType.ToDisplayString(Fqn),
            ValueTypeFqn: valueTypeSymbol.ToDisplayString(Fqn),
            Kind: kind,
            IsRefOnly: isRefOnly,
            IsField: isField,
            IsWritable: settable,
            CanConstruct: holdsObject && HasAccessibleParameterlessConstructor(memberType),
            IsEnum: valueTypeSymbol.TypeKind == TypeKind.Enum,
            BoxedDefaultExpression: kind == MemberKind.Leaf
                ? BoxedDefault(valueTypeSymbol)
                : null);

        return (new ParsedMember(member, ownerFqn, order, location), null);
    }

    /// <summary>
    /// Why a [DV] member cannot be in the model for where it is declared, or null if it can be:
    /// it must be an instance member that is not private, of a partial DocumentObject class.
    /// </summary>
    private static DiagnosticInfo? DeclarationError(ISymbol symbol, INamedTypeSymbol owner, LocationInfo? location)
    {
        if (symbol.IsStatic)
        {
            return DiagnosticInfo.Create(Diagnostics.NotAnInstanceMember, location,
                owner.Name, symbol.Name, "static");
        }

        if (symbol.DeclaredAccessibility == Accessibility.Private)
        {
            return DiagnosticInfo.Create(Diagnostics.NotAnInstanceMember, location,
                owner.Name, symbol.Name, "private");
        }

        if (!DerivesFrom(owner, DocumentObject))
        {
            return DiagnosticInfo.Create(Diagnostics.NotADocumentObject, location,
                owner.Name, symbol.Name);
        }

        if (!IsPartial(owner))
            return DiagnosticInfo.Create(Diagnostics.NotPartial, location, owner.Name);

        return null;
    }

    /// <summary>
    /// A member's type, whether it is a field, and whether it can be written to; or null if the
    /// symbol is neither a field nor a property.
    /// </summary>
    private static (ITypeSymbol Type, bool IsField, bool IsWritable)? ReadShape(ISymbol symbol) =>
        symbol switch
        {
            IFieldSymbol field => (field.Type, true, !field.IsReadOnly),
            IPropertySymbol property => (property.Type, false, property.SetMethod is not null),
            _ => null
        };

    /// <summary>
    /// Whether a member of this kind holds a DocumentObject, a collection being one.
    /// </summary>
    private static bool HoldsDocumentObject(MemberKind kind) =>
        kind is MemberKind.DocumentObject or MemberKind.Collection;

    /// <summary>
    /// A DocumentObject class declaration, or null if the syntax node is not one.
    /// </summary>
    public static ParsedType? ParseType(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
            return null;
        if (!DerivesFrom(symbol, DocumentObject))
            return null;

        var suppressed = symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString(Fqn) == "global::" + SuppressSerializeCheckAttribute);

        return new ParsedType(
            Fqn: symbol.ToDisplayString(Fqn),
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString(),
            Name: symbol.Name,
            BaseFqn: symbol.BaseType?.ToDisplayString(Fqn),
            IsAbstract: symbol.IsAbstract,
            IsPartial: IsPartial(symbol),
            SerializeLiterals: suppressed ? null : SerializeMentions(classDecl));
    }

    /// <summary>
    /// Every string literal and identifier written inside a method named Serialize declared
    /// directly in <paramref name="classDecl"/>, upper-invariant so MDG007's match is
    /// case-insensitive - a [DV] field is typically camelCase, and the name it is mentioned under
    /// in Serialize is either the PascalCase property (<c>Columns.Serialize(serializer)</c>,
    /// <c>WriteComment(comment)</c>) or a literal DDL attribute name
    /// (<c>WriteSimpleAttribute("Style", Style)</c>) - the check does not care which, only that the
    /// member's name shows up somewhere. Null if the class declares no such method, which means
    /// MDG007 has nothing to check: a type with no Serialize of its own is not the type responsible
    /// for its members reaching DDL.
    /// </summary>
    /// <remarks>
    /// Scans <paramref name="classDecl"/> alone, not every partial declaration of the type: a
    /// hand-written DOM class lives in exactly one file today (only the generator's own emitted
    /// partial, which has no base list and so is never seen here at all, adds a second), so there is
    /// nothing yet to aggregate. A type split across two hand-written files with the [DV] member in
    /// one and Serialize in the other would misfire - MDG002-style, a real gap this scan cannot see
    /// rather than one that has happened.
    /// </remarks>
    private static EquatableArray<string>? SerializeMentions(ClassDeclarationSyntax classDecl)
    {
        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == "Serialize")
            .ToList();
        if (methods.Count == 0)
            return null;

        var nodes = methods.SelectMany(m => m.DescendantNodes()).ToList();

        var literals = nodes
            .OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            .Select(l => l.Token.ValueText);

        var identifiers = nodes
            .OfType<IdentifierNameSyntax>()
            .Select(i => i.Identifier.Text);

        return new EquatableArray<string>(literals.Concat(identifiers).Select(s => s.ToUpperInvariant()));
    }

    /// <summary>
    /// The same order of tests the reflection-built model applied, with its single value-type
    /// branch split in two: a struct that implements INullableValue tracks its own null, a plain
    /// bool or enum does not.
    /// </summary>
    private static (MemberKind, ITypeSymbol)? Classify(ITypeSymbol type)
    {
        // Nullable<T> first - it is a struct, so the value-type test below would swallow it.
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return (MemberKind.Leaf, nullable.TypeArguments[0]);

        if (type.SpecialType == SpecialType.System_String)
            return (MemberKind.Leaf, type);

        if (type.IsValueType)
        {
            return type.AllInterfaces.Any(i => i.ToDisplayString(Fqn) == "global::" + NullableValue)
                ? (MemberKind.NullableValue, type)
                : (MemberKind.PlainValue, type);
        }

        if (DerivesFrom(type, DocumentObjectCollection))
            return (MemberKind.Collection, type);

        if (DerivesFrom(type, DocumentObject))
            return (MemberKind.DocumentObject, type);

        return null;
    }

    /// <summary>
    /// What GetValue hands back for a Leaf that was never set. The model has always answered with
    /// the type's default rather than with null unless GV.GetNull was asked for, and with "" rather
    /// than null for a string.
    /// </summary>
    private static string BoxedDefault(ITypeSymbol valueType) =>
        valueType.SpecialType == SpecialType.System_String
            ? "\"\""
            : $"default({valueType.ToDisplayString(Fqn)})";

    private static bool ReadRefOnly(System.Collections.Immutable.ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            foreach (var named in attribute.NamedArguments)
            {
                if (named is { Key: "RefOnly", Value.Value: bool value })
                    return value;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether the member's type can be constructed on demand, for GV.ReadWrite auto-creation.
    /// DocumentObject.parent is the case that matters: it is typed as the abstract base, so it has
    /// constructors but none that can be called.
    /// </summary>
    private static bool HasAccessibleParameterlessConstructor(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsAbstract: false } named
        && named.InstanceConstructors.Any(c =>
            c.Parameters.Length == 0
            && c.DeclaredAccessibility is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal);

    private static bool DerivesFrom(ITypeSymbol type, string baseFqn)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.ToDisplayString(Fqn) == "global::" + baseFqn)
                return true;
        }
        return false;
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(d => d.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));

    /// <summary>
    /// Groups members by declaring type and closes the inheritance chain: a type's value model
    /// contains its own [DV] members and every one its bases declare.
    /// </summary>
    /// <remarks>
    /// This is why the pipeline has a Collect() barrier. A member cannot know what derives from the
    /// type that declares it, so the base chain can only be closed once every member is in hand.
    /// The models are value-equatable, so an edit that changes no [DV] member still produces
    /// identical output and the emit stage's cache holds.
    /// </remarks>
    public static IEnumerable<DomTypeModel> GroupByTypeClosingInheritance(
        IEnumerable<ParsedType> types,
        IEnumerable<ParsedMember> members,
        SourceProductionContext context)
    {
        var byType = members
            .GroupBy(m => m.TypeFqn)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.DeclarationOrder).ToList());

        var declarations = new Dictionary<string, ParsedType>(System.StringComparer.Ordinal);
        foreach (var type in types)
            declarations[type.Fqn] = type;

        foreach (var type in declarations.Values.OrderBy(t => t.Fqn, System.StringComparer.Ordinal))
        {
            // MDG007 checks a type's own [DV] members against its own Serialize - independent of
            // whether the type is abstract, and independent of the base chain the table below
            // closes, since a member declared here is this type's responsibility to serialize,
            // not a descendant's.
            ReportMembersMissingFromSerialize(type, byType, context);

            // An abstract class needs no table of its own - it cannot be instantiated, and its
            // members are picked up by every concrete type below it.
            if (type.IsAbstract)
                continue;

            var collected = CollectMembers(type, BaseFirstChain(type.Fqn, declarations), byType, context);

            yield return new DomTypeModel(
                Namespace: type.Namespace,
                Name: type.Name,
                HintName: type.Fqn.Replace("global::", "").Replace('.', '_'),
                Members: new EquatableArray<DomMemberModel>(collected));
        }
    }

    /// <summary>
    /// Reports MDG007 for each of a type's own [DV] members that is not RefOnly and is not
    /// mentioned in the type's own Serialize. Reports nothing for a type with no Serialize.
    /// </summary>
    private static void ReportMembersMissingFromSerialize(
        ParsedType type,
        Dictionary<string, List<ParsedMember>> byType,
        SourceProductionContext context)
    {
        if (type.SerializeLiterals is not { } literals || !byType.TryGetValue(type.Fqn, out var ownMembers))
            return;

        var written = new HashSet<string>(literals, System.StringComparer.OrdinalIgnoreCase);
        foreach (var member in ownMembers)
        {
            if (!member.Member.IsRefOnly && !written.Contains(member.Member.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.MemberMissingFromSerialize, member.Location?.ToLocation(),
                    type.Name, member.Member.Name));
            }
        }
    }

    /// <summary>
    /// The type and every base of it declared in this compilation, base first, so the order is
    /// deterministic. Reflection's own order never was.
    /// </summary>
    private static List<string> BaseFirstChain(string fqn, Dictionary<string, ParsedType> declarations)
    {
        var chain = new List<string>();
        for (var t = fqn; t is not null; )
        {
            chain.Insert(0, t);
            t = declarations.TryGetValue(t, out var found) ? found.BaseFqn : null;
        }
        return chain;
    }

    /// <summary>
    /// The [DV] members the types of the chain declare, in chain order, reporting MDG004 for each
    /// that has the name of one already collected and leaving it out.
    /// </summary>
    private static List<DomMemberModel> CollectMembers(
        ParsedType type,
        List<string> chain,
        Dictionary<string, List<ParsedMember>> byType,
        SourceProductionContext context)
    {
        var collected = new List<DomMemberModel>();
        var seen = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var owner in chain)
        {
            if (!byType.TryGetValue(owner, out var declared))
                continue;
            foreach (var member in declared)
            {
                if (seen.ContainsKey(member.Member.Name))
                {
                    // Points at the later of the two declarations - the base chain is walked
                    // base first, so that is the one the collision is attributable to. Raising
                    // a real Diagnostic here rather than a DiagnosticInfo is fine: grouping
                    // runs inside the source-output stage, downstream of every cache.
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.DuplicateValueName, member.Location?.ToLocation(),
                        type.Name, member.Member.Name));
                    continue;
                }
                seen.Add(member.Member.Name, owner);
                collected.Add(member.Member);
            }
        }
        return collected;
    }
}
