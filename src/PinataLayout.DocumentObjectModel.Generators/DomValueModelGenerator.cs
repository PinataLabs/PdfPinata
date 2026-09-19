using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PinataLayout.DocumentObjectModel.Generators.Model;

namespace PinataLayout.DocumentObjectModel.Generators;

/// <summary>
/// Emits the DOM's name-addressable value model from the [DV] attributes, replacing the reflection
/// that used to build it at run time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DomValueModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var members = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Parser.DvAttribute,
                predicate: static (node, _) => node is VariableDeclaratorSyntax or PropertyDeclarationSyntax,
                transform: static (ctx, _) => Parser.Parse(ctx, Order(ctx)));

        // Every DocumentObject needs a table, including the 15 that declare no [DV] member of their
        // own and inherit only parent. An attribute-driven provider cannot see those.
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => Parser.ParseType(ctx))
            .Where(static t => t is not null);

        var combined = types.Collect().Combine(members.Collect());

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            // Back to a Diagnostic only here. Everything upstream of this point is cached, and a
            // Diagnostic holds a Location, which holds the syntax tree it came from.
            foreach (var error in pair.Right.Select(m => m.Error).OfType<DiagnosticInfo>())
                spc.ReportDiagnostic(error.ToDiagnostic());

            var allTypes = pair.Left.OfType<ParsedType>();
            var allMembers = pair.Right.Select(m => m.Member).OfType<ParsedMember>();

            foreach (var type in Parser.GroupByTypeClosingInheritance(allTypes, allMembers, spc))
                spc.AddSource($"{type.HintName}.g.cs", Emitter.Emit(type));
        });
    }

    /// <summary>
    /// Source position, so members come out in declaration order rather than in whatever order the
    /// pipeline happens to hand them over. Reflection's order was never specified either, but the
    /// generated model should at least be reproducible build to build.
    /// </summary>
    static int Order(GeneratorAttributeSyntaxContext context) =>
        context.TargetNode.GetLocation().SourceSpan.Start;
}
