using System;

namespace PdfPinata.Drawing;

/// <summary>
/// How a face with no bold of its own is made to look bold: how thick a stroke is drawn round
/// each glyph on top of its fill, and how much further apart the glyphs are set to make room for
/// it. Both are fractions of the font's em size.
/// </summary>
/// <remarks>
/// A font resolver chooses it per face, on the <see cref="Fonts.FontResolverInfo"/> it answers
/// with, and it applies only where that answer also says
/// <see cref="Fonts.FontResolverInfo.MustSimulateBold"/>. A resolver derived from
/// <see cref="Utils.FontResolverBase"/> takes it from
/// <see cref="Utils.FontResolverBase.BoldSimulation"/>.
/// <para>
/// The two are separate because producers disagree about the second. The XPS specification widens
/// every glyph's advance by the same 2% of the em the stroke is, and that is <see cref="Default"/>;
/// Microsoft Word strokes a little more heavily and sets the glyphs at their regular advances, and
/// that is <see cref="WordCompatible"/>. A document that has to line up with one produced by Word
/// needs the second, because the spacing is a constant per glyph rather than a proportion of it -
/// no change of font size makes a line of narrow capitals and a line of wide ones both come out
/// right.
/// </para>
/// </remarks>
public sealed class XBoldSimulation : IEquatable<XBoldSimulation>
{
    /// <summary>
    /// Describes a bold simulation by its stroke width and the character spacing that goes with it.
    /// </summary>
    /// <param name="strokeWidth">
    /// The width of the stroke drawn round each glyph, as a fraction of the em size. Greater than
    /// zero: a PDF line of width zero is the thinnest line the device can draw, not no line.
    /// </param>
    /// <param name="characterSpacing">
    /// The space added after every glyph, as a fraction of the em size. Zero or more; zero sets the
    /// glyphs at the advances the face gives them.
    /// </param>
    public XBoldSimulation(double strokeWidth, double characterSpacing)
    {
        if (!(strokeWidth > 0) || double.IsInfinity(strokeWidth))
            throw new ArgumentOutOfRangeException(nameof(strokeWidth), strokeWidth,
                "The stroke width must be a finite fraction of the em greater than zero.");
        if (!(characterSpacing >= 0) || double.IsInfinity(characterSpacing))
            throw new ArgumentOutOfRangeException(nameof(characterSpacing), characterSpacing,
                "The character spacing must be a finite fraction of the em of zero or more.");

        StrokeWidth = strokeWidth;
        CharacterSpacing = characterSpacing;
    }

    /// <summary>
    /// A stroke of 2% of the em, and the same 2% added after every glyph - the figure in the XPS 1.0
    /// specification, and what this library has always drawn.
    /// </summary>
    public static XBoldSimulation Default { get; } = new(0.02, 0.02);

    /// <summary>
    /// A stroke of 1/35 of the em and no added spacing, which is how Microsoft Word draws a bold it
    /// has to simulate: glyphs land where the regular face puts them.
    /// </summary>
    public static XBoldSimulation WordCompatible { get; } = new(1.0 / 35, 0);

    /// <summary>
    /// The width of the stroke drawn round each glyph, as a fraction of the em size.
    /// </summary>
    public double StrokeWidth { get; }

    /// <summary>
    /// The space added after every glyph, as a fraction of the em size.
    /// </summary>
    public double CharacterSpacing { get; }

    /// <inheritdoc />
    #pragma warning disable S1244 // Exact on purpose: two simulations are the same only if they draw the same.
    public bool Equals(XBoldSimulation other)
        => other is not null && StrokeWidth == other.StrokeWidth && CharacterSpacing == other.CharacterSpacing;
    #pragma warning restore S1244

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as XBoldSimulation);

    /// <inheritdoc />
    public override int GetHashCode() => (StrokeWidth.GetHashCode() * 397) ^ CharacterSpacing.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"stroke {StrokeWidth:0.####} em, spacing {CharacterSpacing:0.####} em";
}
