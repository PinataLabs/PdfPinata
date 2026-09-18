using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// The collection every test that adds text to a path belongs to.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.Fonts.GlobalFontSettings.GlyphOutlineProvider"/> is one static for the
/// whole application domain, and the behaviour when it is unset is part of what has to be tested -
/// which means clearing it and putting it back. A test doing that alongside a test adding text to
/// a path would fail the second one for the first one's reasons, so they are kept out of each
/// other's way here.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlyphOutlineCollection
{
    public const string Name = "GlyphOutlines";
}
