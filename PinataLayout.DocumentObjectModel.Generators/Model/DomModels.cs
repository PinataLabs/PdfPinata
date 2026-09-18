using System;

namespace PinataLayout.DocumentObjectModel.Generators.Model;

/// <summary>
/// Which <c>ValueKind</c> the emitted descriptor gets. Mirrors the runtime enum by name.
/// </summary>
internal enum MemberKind
{
    Leaf,
    NullableValue,
    PlainValue,
    DocumentObject,
    Collection,
}

/// <summary>
/// One [DV] member, reduced to strings and enums.
/// </summary>
/// <remarks>
/// Nothing here is a Roslyn type. ISymbol, ITypeSymbol, SyntaxNode and Compilation all hold the
/// compilation alive and compare by reference, so putting one in a pipeline model both leaks memory
/// across IDE keystrokes and guarantees a cache miss on every edit.
/// </remarks>
internal sealed record DomMemberModel(
    string Name,
    string DeclaringTypeFqn,
    string MemberTypeFqn,
    string ValueTypeFqn,
    MemberKind Kind,
    bool IsRefOnly,
    bool IsField,
    bool IsWritable,
    bool CanConstruct,
    bool IsEnum,
    string? BoxedDefaultExpression) : IEquatable<DomMemberModel>;

/// <summary>
/// One DOM class and the members its value model contains, its own and its bases'.
/// </summary>
internal sealed record DomTypeModel(
    string Namespace,
    string Name,
    string HintName,
    EquatableArray<DomMemberModel> Members) : IEquatable<DomTypeModel>;

/// <summary>
/// A member as parsed, before the base chain is closed.
/// </summary>
/// <param name="Location">
/// Where the member is declared, kept so that a diagnostic raised once the base chain is closed -
/// MDG004 is the only one - can point at it. Grouping is the first stage that can see a collision,
/// and by then the symbol is long gone. This costs nothing in cache terms that
/// <paramref name="DeclarationOrder"/> does not already cost: both change when the declaration
/// moves, and neither holds a syntax tree.
/// </param>
internal sealed record ParsedMember(
    DomMemberModel Member,
    string TypeFqn,
    int DeclarationOrder,
    LocationInfo? Location) : IEquatable<ParsedMember>;

/// <summary>
/// A DocumentObject class, whether or not it declares any [DV] members of its own.
/// </summary>
/// <remarks>
/// Collected separately from the members, because a great many DOM types - the collections and the
/// field classes, 15 of them - declare nothing themselves and inherit only DocumentObject.parent.
/// They still need a table, so an attribute-driven provider alone would leave them abstract-member
/// errors. This also carries the base chain, so grouping can close inheritance without going back
/// to the compilation.
/// </remarks>
/// <param name="SerializeLiterals">
/// The string literals, upper-invariant, written inside every method named Serialize declared
/// directly in this type - null if it declares no such method. MDG007 checks a [DV] member's name
/// against this set; see <see cref="Diagnostics.MemberMissingFromSerialize"/>.
/// </param>
internal sealed record ParsedType(
    string Fqn,
    string Namespace,
    string Name,
    string? BaseFqn,
    bool IsAbstract,
    bool IsPartial,
    EquatableArray<string>? SerializeLiterals) : IEquatable<ParsedType>;
