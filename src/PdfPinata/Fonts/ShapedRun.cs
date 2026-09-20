using System;
using System.Collections.Generic;

namespace PdfPinata.Fonts;

/// <summary>
/// The result of shaping one run of text: the glyphs to draw, in the order they are drawn.
/// </summary>
/// <remarks>
/// The glyphs are in <b>visual order</b> - left to right on the page - whichever direction the run
/// is written in. A right-to-left run has already been reversed by the time it gets here, so a
/// renderer draws every run the same way and only the <see cref="Direction"/> records what happened.
/// The <see cref="ShapedGlyph.Cluster"/> indices of a right-to-left run therefore descend.
/// </remarks>
public sealed class ShapedRun
{
    /// <summary>
    /// Initializes a new <see cref="ShapedRun"/>.
    /// </summary>
    /// <param name="glyphs">The glyphs, in the order they are drawn.</param>
    /// <param name="unitsPerEm">The design units per em of the face the run was shaped against.</param>
    /// <param name="direction">The direction the run is written in.</param>
    public ShapedRun(IReadOnlyList<ShapedGlyph> glyphs, int unitsPerEm,
        XTextDirection direction = XTextDirection.LeftToRight)
    {
        Glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));

        if (unitsPerEm <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsPerEm),
                "A face has a positive number of design units per em; advances are read against it.");

        UnitsPerEm = unitsPerEm;
        Direction = direction;

        double width = 0;
        for (var idx = 0; idx < glyphs.Count; idx++)
            width += glyphs[idx].Advance;
        Width = width;
    }

    /// <summary>
    /// An empty run against the given face. Shaping nothing yields this rather than null.
    /// </summary>
    public static ShapedRun Empty(int unitsPerEm, XTextDirection direction = XTextDirection.LeftToRight)
        => new ShapedRun(Array.Empty<ShapedGlyph>(), unitsPerEm, direction);

    /// <summary>
    /// The glyphs, in the order they are drawn.
    /// </summary>
    public IReadOnlyList<ShapedGlyph> Glyphs { get; }

    /// <summary>
    /// The design units per em of the face this run was shaped against.
    /// <see cref="ShapedGlyph.Advance"/> and the offsets are in those units.
    /// </summary>
    public int UnitsPerEm { get; }

    /// <summary>
    /// The direction the run is written in. The glyphs are in visual order either way; this says
    /// what the order means, not what to do with it.
    /// </summary>
    public XTextDirection Direction { get; }

    /// <summary>
    /// The sum of the glyph advances, in font design units. Multiply by the em size and divide by
    /// <see cref="UnitsPerEm"/> for a width in points.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// The width of the run in points at the given em size.
    /// </summary>
    public double WidthAt(double emSize) => Width * emSize / UnitsPerEm;

    /// <inheritdoc/>
    public override string ToString()
        => $"{Glyphs.Count} glyph(s), {Direction}, width {Width} / {UnitsPerEm} em";
}
