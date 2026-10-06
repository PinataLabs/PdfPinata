using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Marks a test, or a class of them, that registers a text shaper.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.Fonts.GlobalFontSettings.TextShaper"/> is one setting for the whole
/// application domain, so two tests installing shapers at the same time are one test: whichever
/// clears it first takes the other's shaper away, and the other then measures and draws unshaped
/// without anything saying so. Tests sharing this attribute's key never run alongside one another,
/// which is enough to keep them out of each other's way.
/// <para>
/// The key is what sets this apart from <see cref="RasterizingAttribute"/>, which has none and so
/// runs alone. The rest of the suite is unaffected by these tests, because every shaper they
/// install answers for one sentinel string of its own and declines every other run - and a declined
/// run is shaped exactly as it would have been with no shaper at all.
/// </para>
/// </remarks>
public sealed class TextShaperSensitiveAttribute() : NotInParallelAttribute("Text shaping");
