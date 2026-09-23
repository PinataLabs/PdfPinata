using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PinataLayout.DocumentObjectModel.Generators.Model;

/// <summary>
/// A source location reduced to values, so it can travel through the pipeline.
/// </summary>
/// <remarks>
/// <see cref="Location"/> itself cannot: a source location holds the <see cref="SyntaxTree"/> it
/// came from, which roots that tree - and everything it references - in the generator's cache for as
/// long as the model containing it is held. <see cref="TextSpan"/> and <see cref="LinePositionSpan"/>
/// are the exception to "no Roslyn types in a pipeline model": both are structs of ints that
/// reference nothing, and comparing by value is what they already do.
/// </remarks>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>
    /// The location of <paramref name="location"/>, or null if it did not come from source.
    /// </summary>
    public static LocationInfo? From(Location? location) =>
        location is { SourceTree: not null }
            ? new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : null;
}

/// <summary>
/// A diagnostic reduced to values, for the same reason as <see cref="LocationInfo"/>.
/// </summary>
/// <remarks>
/// A <see cref="Diagnostic"/> carries a <see cref="Location"/>, so returning one from a pipeline
/// stage puts a syntax tree in the cache. Parsing produces one of these instead, and
/// <see cref="ToDiagnostic"/> turns it back at the point it is reported - inside the source-output
/// stage, which is downstream of every cache and free to hold whatever it likes.
///
/// <see cref="DiagnosticDescriptor"/> is safe to hold: it compares by value, references no
/// compilation, and every one used here is a static readonly singleton on
/// <see cref="Diagnostics"/> anyway.
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        LocationInfo? location,
        params string[] messageArgs) =>
        new(descriptor, location, new EquatableArray<string>(messageArgs));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), [..MessageArgs.Cast<object>()]);
}
