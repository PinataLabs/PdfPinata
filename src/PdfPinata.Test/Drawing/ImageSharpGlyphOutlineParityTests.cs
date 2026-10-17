using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using TUnit.Core;
using static PdfPinata.Test.Helpers.GlyphOutlineGeometry;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The ImageSharp backend's glyph outlines, glyph by glyph, against Skia's for the same font file:
///   the same figures, the same extent and the same area, to within a thousandth of a point.
///   <see cref="GlyphOutlineTests"/> compares the two backends on where a whole string lands, to a
///   quarter of a point; this is the comparison that would notice a glyph drawn differently.
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it holds both ImageSharp packages to
///     Skia, and so to each other, which is what spec #305 asks of them. Fonts 3 lays a glyph out
///     differently from Fonts 1 in two ways that each moved an outline by more than this allows: it
///     rounds TrueType points to a 64th of a pixel, and it finds the first baseline its own way when
///     a glyph rises above the ascender. Measured directly, on this text, the two packages now agree
///     to a few millionths of a point. See #305.
///   </para>
/// </summary>
public class ImageSharpGlyphOutlineParityTests
{
    private const double EmSize = 48;

    /// <summary>A thousandth of a point, which is a fiftieth of a font unit at this size.</summary>
    private const double Tolerance = 0.001;

    /// <summary>
    ///   Capitals, ascenders and round letters; capitals carrying accents, which rise above the
    ///   ascender; and a space, which draws nothing.
    /// </summary>
    private const string Text = "Handles ÄÖÜ fi";

    [Test]
    [Arguments("Arial")]
    [Arguments(PinnedFontResolver.CffFamilyName)]
    public void EachGlyphHasTheFiguresSkiaGivesIt(string family)
    {
        var (imageSharp, skia) = Outlines(family);

        imageSharp.Select(outline => Flattened(outline).Count)
            .Should().Equal(skia.Select(outline => Flattened(outline).Count));
    }

    [Test]
    [Arguments("Arial")]
    [Arguments(PinnedFontResolver.CffFamilyName)]
    public void EachGlyphCoversTheExtentSkiaGivesIt(string family)
    {
        var (imageSharp, skia) = Outlines(family);

        for (var glyph = 0; glyph < skia.Count; glyph++)
        {
            var expected = Bounds(skia[glyph]);
            var actual = Bounds(imageSharp[glyph]);

            actual.X.Should().BeApproximately(expected.X, Tolerance, "the left of glyph {0}", glyph);
            actual.Y.Should().BeApproximately(expected.Y, Tolerance, "the foot of glyph {0}", glyph);
            actual.Width.Should().BeApproximately(expected.Width, Tolerance, "the width of glyph {0}", glyph);
            actual.Height.Should().BeApproximately(expected.Height, Tolerance, "the height of glyph {0}", glyph);
        }
    }

    [Test]
    [Arguments("Arial")]
    [Arguments(PinnedFontResolver.CffFamilyName)]
    public void EachGlyphEnclosesTheAreaSkiaGivesIt(string family)
    {
        var (imageSharp, skia) = Outlines(family);

        // Signed, so a figure wound the other way, which fills differently, would not pass either.
        for (var glyph = 0; glyph < skia.Count; glyph++)
        {
            var expected = Flattened(skia[glyph]).Sum(SignedArea);
            Flattened(imageSharp[glyph]).Sum(SignedArea)
                .Should().BeApproximately(expected, Math.Max(1, Math.Abs(expected)) * Tolerance, "the area of glyph {0}", glyph);
        }
    }

    // ----- arrangements -----

    private static (IReadOnlyList<XGlyphOutline> ImageSharp, IReadOnlyList<XGlyphOutline> Skia) Outlines(string family)
    {
        var imageSharp = new ImageSharpGlyphOutlineProvider().GetOutlines(Text, family, false, false, EmSize).ToList();
        var skia = new SkiaGlyphOutlineProvider().GetOutlines(Text, family, false, false, EmSize).ToList();

        imageSharp.Should().HaveCount(skia.Count, "one outline per glyph");
        return (imageSharp, skia);
    }

    private static XRect Bounds(XGlyphOutline outline)
    {
        var points = Flattened(outline).SelectMany(figure => figure).ToList();
        if (points.Count == 0)
            return new XRect(0, 0, 0, 0); // a space; Empty's infinities compare to nothing

        var left = points.Min(p => p.X);
        var bottom = points.Min(p => p.Y);
        return new XRect(left, bottom, points.Max(p => p.X) - left, points.Max(p => p.Y) - bottom);
    }
}
