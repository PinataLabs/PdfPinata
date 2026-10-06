using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Marks a test, or a class of them, that adds text to a path.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.Fonts.GlobalFontSettings.GlyphOutlineProvider"/> is one static for the
/// whole application domain, and the behaviour when it is unset is part of what has to be tested -
/// which means clearing it and putting it back. A test doing that alongside a test adding text to
/// a path would fail the second one for the first one's reasons, so a
/// <see cref="NotInParallelAttribute"/> with no key runs each of these alone, as the xUnit
/// collection this replaces did by disabling parallelization.
/// </remarks>
public sealed class GlyphOutlineSensitiveAttribute : NotInParallelAttribute;
