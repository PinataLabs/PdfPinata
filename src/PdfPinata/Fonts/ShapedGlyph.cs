namespace PdfPinata.Fonts;

/// <summary>
/// One glyph as a shaper positioned it: which glyph, how far it advances, how far it is nudged off
/// the pen position, and which characters of the source text it came from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Advances and offsets are in font design units</b>, the same units
/// <see cref="PdfPinata.Fonts.OpenType.OpenTypeDescriptor.GlyphIndexToWidth"/> answers in, and
/// they are read against <see cref="ShapedRun.UnitsPerEm"/>. Points were the other candidate and
/// are worse: a shaped run in points is only good for the one size it was shaped at, while a run
/// in design units can be measured, cached and drawn at every size, which is what the measuring
/// paths actually do with it.
/// </para>
/// <para>
/// <see cref="Cluster"/> is the load-bearing member. It is an index into the text that was
/// shaped - the first character of the group this glyph belongs to - and consecutive glyphs
/// sharing a cluster are one indivisible unit. It is what says that the two characters "f" and
/// "i" became the single glyph <c>fi</c>, and three separate things need to know that: line
/// breaking, which may only break at a cluster boundary; <c>/ToUnicode</c>, which has to map that
/// one glyph back to two characters; and <c>/ActualText</c> for tagged output, which has to say
/// the same thing to a screen reader. A shaping result without clusters can drive a screen and
/// cannot drive a PDF writer.
/// </para>
/// </remarks>
public readonly struct ShapedGlyph
{
    /// <summary>
    /// Initializes a new <see cref="ShapedGlyph"/>.
    /// </summary>
    /// <param name="glyphId">The glyph index within the face the run was shaped against.</param>
    /// <param name="cluster">The index into the shaped text of the first character this glyph came from.</param>
    /// <param name="advance">How far the pen moves after drawing it, in font design units.</param>
    /// <param name="offsetX">How far the glyph is drawn right of the pen position, in font design units.</param>
    /// <param name="offsetY">How far the glyph is drawn above the pen position, in font design units.</param>
    public ShapedGlyph(ushort glyphId, int cluster, double advance, double offsetX = 0, double offsetY = 0)
    {
        GlyphId = glyphId;
        Cluster = cluster;
        Advance = advance;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// The glyph index within the face this run was shaped against.
    /// </summary>
    public ushort GlyphId { get; }

    /// <summary>
    /// The index into the shaped text of the first character this glyph came from. Several glyphs
    /// may share one cluster (a character that decomposed into several marks), and one glyph may
    /// stand for several characters (a ligature), in which case the characters between this
    /// cluster and the next one are all its.
    /// </summary>
    public int Cluster { get; }

    /// <summary>
    /// How far the pen moves along the run after drawing this glyph, in font design units.
    /// </summary>
    public double Advance { get; }

    /// <summary>
    /// How far right of the pen position the glyph is drawn, in font design units. Nonzero for
    /// attached marks; zero for nearly everything else.
    /// </summary>
    public double OffsetX { get; }

    /// <summary>
    /// How far above the pen position the glyph is drawn, in font design units. Nonzero for
    /// attached marks; zero for nearly everything else.
    /// </summary>
    public double OffsetY { get; }

    /// <summary>
    /// Whether the glyph sits exactly on the pen position, which is to say whether a renderer can
    /// draw it without emitting a displacement first.
    /// </summary>
    public bool IsOnBaselineOrigin => OffsetX == 0 && OffsetY == 0;

    /// <inheritdoc/>
    public override string ToString()
        => $"glyph {GlyphId} cluster {Cluster} advance {Advance}";
}
