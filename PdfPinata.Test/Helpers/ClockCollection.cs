using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// The collection every test that moves the clock belongs to.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.GlobalTimeSettings.Clock"/> is one static for the whole application
/// domain, and testing it means setting it and putting it back. Any test that creates a document
/// while it is set would be stamped with the fixed time and could not tell why, so the tests that
/// move it are kept out of everything else's way here.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ClockCollection
{
    /// <summary>The name of the collection.</summary>
    public const string Name = "Clock";
}
