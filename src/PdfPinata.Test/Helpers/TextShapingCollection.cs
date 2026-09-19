using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// The collection every test that registers a text shaper belongs to.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.Fonts.GlobalFontSettings.TextShaper"/> is one setting for the whole
/// application domain, so two tests installing shapers at the same time are one test: whichever
/// clears it first takes the other's shaper away, and the other then measures and draws unshaped
/// without anything saying so. Tests in one collection do not run alongside one another, which is
/// enough to keep them out of each other's way.
/// <para>
/// Parallelization is <b>not</b> disabled, unlike <see cref="RasterizingCollection"/>. The rest of
/// the suite is unaffected by these tests, because every shaper they install answers for one
/// sentinel string of its own and declines every other run - and a declined run is shaped exactly
/// as it would have been with no shaper at all.
/// </para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class TextShapingCollection
{
    public const string Name = "Text shaping";
}
