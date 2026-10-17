using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SkiaSharp;
using TUnit.Core;
using static PdfPinata.Test.Helpers.GlyphOutlineGeometry;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The ImageSharp backend's glyph outlines, glyph by glyph, against Skia's for the same font file:
///   the same figures, the same extent and the same area, to within a thousandth of a point, or
///   further where Skia's own outline is rounded (<see cref="SkiaRounding"/>).
///   <see cref="GlyphOutlineTests"/> compares the two backends on where a whole string lands, to a
///   quarter of a point; this is the comparison that would notice a glyph drawn differently.
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it holds both ImageSharp packages to
///     Skia, and so to each other, which is what spec #305 asks of them. Fonts 3 lays a glyph out
///     differently from Fonts 1 in two ways that each moved an outline by more than this allows: it
///     rounds TrueType points to a 64th of a pixel, and it finds the first baseline its own way when
///     a glyph rises above the ascender. Measured directly, on this text, the two packages now agree
///     to a few millionths of a point. See #305. On Linux the PostScript face is held to Skia less
///     closely, so there it is Windows that holds the two packages to each other at this precision.
///   </para>
/// </summary>
public class ImageSharpGlyphOutlineParityTests
{
    private const double EmSize = 48;

    /// <summary>A thousandth of a point, which is a fiftieth of a font unit at this size.</summary>
    private const double Tolerance = 0.001;

    /// <summary>
    ///   The grid Skia's outlines are rounded to where it reads them through FreeType, in steps to
    ///   the em: Skia asks for every path at 64 pixels to the em and scales it to the size wanted,
    ///   and FreeType answers in 26.6 fixed point, a 64th of a pixel.
    /// </summary>
    private const int FreeTypeGridStepsPerEm = 64 * 64;

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

        // An edge of Skia's outline may be rounded by up to SkiaRounding, and a width or a height
        // is one edge less another, each rounded on its own.
        var rounding = SkiaRounding(family);
        var edge = Tolerance + rounding;
        var span = Tolerance + 2 * rounding;

        for (var glyph = 0; glyph < skia.Count; glyph++)
        {
            var expected = Bounds(skia[glyph]);
            var actual = Bounds(imageSharp[glyph]);

            actual.X.Should().BeApproximately(expected.X, edge, "the left of glyph {0}", glyph);
            actual.Y.Should().BeApproximately(expected.Y, edge, "the foot of glyph {0}", glyph);
            actual.Width.Should().BeApproximately(expected.Width, span, "the width of glyph {0}", glyph);
            actual.Height.Should().BeApproximately(expected.Height, span, "the height of glyph {0}", glyph);
        }
    }

    [Test]
    [Arguments("Arial")]
    [Arguments(PinnedFontResolver.CffFamilyName)]
    public void EachGlyphEnclosesTheAreaSkiaGivesIt(string family)
    {
        var (imageSharp, skia) = Outlines(family);
        var rounding = SkiaRounding(family);

        // Signed, so a figure wound the other way, which fills differently, would not pass either.
        for (var glyph = 0; glyph < skia.Count; glyph++)
        {
            var figures = Flattened(skia[glyph]);
            var expected = figures.Sum(SignedArea);

            // Moving every point of a figure by up to r across and r up changes its area by at most
            // r times the sum of each edge's run and rise: the most a rounded reference can lose or
            // gain. Measured on Linux, Source Code Pro's rounding alone used up to 89% of the
            // relative allowance, which is too close to stand on.
            var rounded = rounding * figures.Sum(RunAndRise);
            Flattened(imageSharp[glyph]).Sum(SignedArea)
                .Should().BeApproximately(expected, Math.Max(1, Math.Abs(expected)) * Tolerance + rounded,
                    "the area of glyph {0}", glyph);
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

    /// <summary>
    ///   How far Skia's own outline of <paramref name="family"/> may lie from the exact one, which
    ///   is nothing except where FreeType reads it and the font's units do not fall on FreeType's
    ///   grid.
    /// </summary>
    /// <remarks>
    ///   Skia reads outlines through DirectWrite on Windows and Core Text on Apple's systems, which
    ///   answer in floating point, and through FreeType everywhere else, which rounds each point to
    ///   a 4096th of the em (<see cref="FreeTypeGridStepsPerEm"/>). Liberation Sans has 2048 units
    ///   to the em, so every point it holds is on that grid and Skia's outline is exact on Linux too.
    ///   Source Code Pro has 1000, so a point lands up to half a step away: about 0.006 pt here, six
    ///   times <see cref="Tolerance"/>, which is what failed on Linux CI and never on Windows. The
    ///   ImageSharp outline is the exact one; only the reference moved.
    ///   <para>
    ///     Half a step and no more, with <see cref="Tolerance"/> on top: measured on Linux, the worst
    ///     point of Source Code Pro in this text is 0.54 of a step from ImageSharp's, a little over
    ///     half because FreeType's CFF engine works in fixed point before it rounds. Allowing this
    ///     only where the reference is rounded keeps the TrueType comparison, where Fonts 3's
    ///     rounding was found, at a thousandth of a point on every platform CI runs on.
    ///   </para>
    /// </remarks>
    private static double SkiaRounding(string family)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
            OperatingSystem.IsMacCatalyst())
            return 0;

        var resolver = GlobalFontSettings.FontResolver;
        var face = resolver.ResolveTypeface(family, false, false)!;
        using var typeface = SKTypeface.FromStream(new MemoryStream(resolver.GetFont(face.FaceName)));

        return FreeTypeGridStepsPerEm % typeface.UnitsPerEm == 0 ? 0 : EmSize / FreeTypeGridStepsPerEm / 2;
    }

    /// <summary>The sum of each edge's horizontal and vertical length, around a figure taken as closed.</summary>
    private static double RunAndRise(List<XPoint> figure)
    {
        double sum = 0;
        for (var i = 0; i < figure.Count; i++)
        {
            var a = figure[i];
            var b = figure[(i + 1) % figure.Count];
            sum += Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
        }

        return sum;
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
