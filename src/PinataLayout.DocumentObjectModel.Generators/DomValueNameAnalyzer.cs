using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PinataLayout.DocumentObjectModel.Generators;

/// <summary>
/// Checks a value name handed to <c>DocumentObject</c>'s name-taking members - <c>IsNull</c>,
/// <c>SetNull</c>, <c>HasValue</c>, <c>GetValue</c> and <c>SetValue</c> - against the [DV] members
/// of the type it is called on, so that a misspelled or renamed member fails the build rather than
/// throwing <c>InvalidValueName</c> on the one path that reaches it (MDG008).
/// </summary>
/// <remarks>
/// <para>
/// It runs only in the compilation that declares <c>DocumentObject</c> - the DOM itself - and says
/// nothing anywhere else, because nowhere else can it see what it checks against. Almost every [DV]
/// member is an <c>internal</c> field, and a project referencing the DOM compiles against its
/// reference assembly, which carries no internal member at all: the repository has no
/// <c>InternalsVisibleTo</c>. Even the implementation assembly would not do, since the compiler
/// imports a referenced assembly's public and protected members only. So from PinataLayout.Rendering
/// every name would look unknown. The calls there name a public property through <c>nameof</c>
/// instead, bound to the receiver expression so it is checked against the receiver's own type; that
/// catches a misspelling or a rename, which is what this does here.
/// </para>
/// <para>
/// Only a name the compiler knows is checked - a literal, a <c>nameof</c>, a constant. A name built
/// at run time is not a mistake this can see, and is not reported.
/// </para>
/// <para>
/// It errs towards silence wherever the receiver's static type does not settle the question. A name
/// is accepted if any type derived from the receiver's declares it, because the value model consulted
/// at run time is the runtime type's; and a receiver whose type, or a type below it, overrides the
/// member being called is not checked at all, because the override can route a name anywhere -
/// <c>Style.GetValue</c> sends every name beginning "font" to its paragraph format.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomValueNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The members that take a value name, and whether each follows a dotted path.</summary>
    /// <remarks>
    /// <c>Meta.IsNull</c>, <c>GetValue</c> and <c>SetValue</c> split a name at its first period and
    /// hand the rest to the member it names. <c>SetNull</c> and <c>HasValue</c> look the whole string
    /// up as one name, so a dotted path given to either can never match.
    /// </remarks>
    private static readonly ImmutableDictionary<string, bool> NameTakingMembers =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["IsNull"] = true,
            ["GetValue"] = true,
            ["SetValue"] = true,
            ["SetNull"] = false,
            ["HasValue"] = false,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Diagnostics.UnknownValueName];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // Not the DOM. Either nothing here can be a value name, or the [DV] members are internal
            // to a referenced assembly and invisible, so every name would look unknown.
            if (start.Compilation.GetTypeByMetadataName(Parser.DocumentObject) is not { } documentObject
                || !SymbolEqualityComparer.Default.Equals(documentObject.ContainingAssembly, start.Compilation.Assembly))
                return;

            var model = new ValueModel(documentObject);
            start.RegisterOperationAction(
                operation => AnalyzeInvocation(operation, model),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ValueModel model)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!NameTakingMembers.TryGetValue(method.Name, out var followsPath))
            return;
        if (method.IsStatic || method.Parameters.Length == 0
            || method.Parameters[0] is not { Name: "name", Type.SpecialType: SpecialType.System_String })
            return;
        if (!model.IsDocumentObject(method.ContainingType))
            return;

        // A type parameter or an interface tells us nothing about which value model answers.
        if (invocation.Instance?.Type is not INamedTypeSymbol receiver || !model.IsDocumentObject(receiver))
            return;

        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0);
        if (argument?.Value.ConstantValue is not { HasValue: true, Value: string name })
            return;

        if (model.IsOverriddenAtOrBelow(receiver, method))
            return;

        if (Problem(name, receiver, followsPath, method.Name, model) is { } problem)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UnknownValueName, argument.Syntax.GetLocation(),
                name, receiver.Name, problem));
        }
    }

    /// <summary>
    /// Why <paramref name="name"/> names no value of <paramref name="receiver"/>, or null if it does
    /// - or if a step of it leads somewhere the static types cannot follow.
    /// </summary>
    private static string? Problem(string name, INamedTypeSymbol receiver, bool followsPath, string methodName, ValueModel model)
    {
        if (!followsPath && name.IndexOf('.') >= 0)
            return $"{methodName} looks the whole string up as one name and does not follow a dotted path";

        var steps = followsPath ? name.Split('.') : [name];
        var current = receiver;
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (!model.ValuesOf(current).TryGetValue(step, out var memberType))
                return $"'{current.Name}' has no [DV] member named '{step}'";

            if (i == steps.Length - 1)
                return null;

            // Two types below the receiver declare the step with different types, or it is typed as
            // something the static types cannot see past. Either way, nothing more can be said.
            if (memberType is not INamedTypeSymbol next)
                return null;

            if (!model.IsDocumentObject(next))
                return $"'{current.Name}.{step}' is a simple value, so nothing can follow it in a path";

            current = next;
        }
        return null;
    }

    /// <summary>
    /// The [DV] members of the DOM's types, read off their symbols and cached for the compilation.
    /// </summary>
    private sealed class ValueModel(INamedTypeSymbol documentObject)
    {
        private readonly ConcurrentDictionary<INamedTypeSymbol, IReadOnlyDictionary<string, ITypeSymbol?>> values =
            new(SymbolEqualityComparer.Default);

        private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> descendants =
            new(SymbolEqualityComparer.Default);

        private readonly Lazy<ImmutableArray<INamedTypeSymbol>> domTypes = new(() =>
            [..AllTypes(documentObject.ContainingAssembly.GlobalNamespace)]);

        public bool IsDocumentObject(ITypeSymbol? type)
        {
            for (var t = type; t is not null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, documentObject))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Every [DV] member the value model of <paramref name="type"/>, or of any type below it,
        /// can answer to, keyed case-insensitively as <c>Meta</c> keys them, with the member's type -
        /// or null where two types below declare the same name with different types.
        /// </summary>
        public IReadOnlyDictionary<string, ITypeSymbol?> ValuesOf(INamedTypeSymbol type) =>
            values.GetOrAdd(type, t =>
            {
                var result = new Dictionary<string, ITypeSymbol?>(StringComparer.OrdinalIgnoreCase);
                foreach (var member in DescendantsAndSelf(t).SelectMany(OwnAndInheritedValues))
                {
                    var memberType = TypeOf(member);
                    if (!result.TryGetValue(member.Name, out var seen))
                        result.Add(member.Name, memberType);
                    else if (!SymbolEqualityComparer.Default.Equals(seen, memberType))
                        result[member.Name] = null;
                }
                return result;
            });

        /// <summary>
        /// Whether <paramref name="receiver"/>, a type between it and the member's declaring type, or
        /// a type below it overrides <paramref name="method"/>.
        /// </summary>
        public bool IsOverriddenAtOrBelow(INamedTypeSymbol receiver, IMethodSymbol method)
        {
            // The call may be bound to an override already, so start from the virtual it overrides.
            var declared = method.OriginalDefinition;
            while (declared.OverriddenMethod is { } overridden)
                declared = overridden.OriginalDefinition;

            for (var t = receiver; t is not null && !SymbolEqualityComparer.Default.Equals(t, declared.ContainingType); t = t.BaseType)
            {
                if (Overrides(t, declared))
                    return true;
            }
            return DescendantsAndSelf(receiver).Any(t => Overrides(t, declared));
        }

        private static bool Overrides(INamedTypeSymbol type, IMethodSymbol declared) =>
            type.GetMembers(declared.Name).OfType<IMethodSymbol>().Any(m =>
            {
                for (var o = m.OverriddenMethod; o is not null; o = o.OverriddenMethod)
                {
                    if (SymbolEqualityComparer.Default.Equals(o.OriginalDefinition, declared))
                        return true;
                }
                return false;
            });

        private ImmutableArray<INamedTypeSymbol> DescendantsAndSelf(INamedTypeSymbol type) =>
            descendants.GetOrAdd(type, t =>
                [t, ..domTypes.Value.Where(d => !SymbolEqualityComparer.Default.Equals(d, t) && DerivesFrom(d, t))]);

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var t = type.BaseType; t is not null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, baseType))
                    return true;
            }
            return false;
        }

        private static IEnumerable<ISymbol> OwnAndInheritedValues(INamedTypeSymbol type)
        {
            for (var t = type; t is not null; t = t.BaseType)
            {
                foreach (var member in t.GetMembers())
                {
                    if (member is IFieldSymbol or IPropertySymbol && !member.IsStatic && HasDv(member))
                        yield return member;
                }
            }
        }

        private static bool HasDv(ISymbol member) =>
            member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == Parser.DvAttribute);

        private static ITypeSymbol? TypeOf(ISymbol member) => member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };

        private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                foreach (var nested in AllTypesIn(type))
                    yield return nested;
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                foreach (var type in AllTypes(child))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> AllTypesIn(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers().SelectMany(AllTypesIn))
                yield return nested;
        }
    }
}
