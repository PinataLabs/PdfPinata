using System;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// What a path or a stencil is painted with: a colour, and a constant alpha.
/// </summary>
/// <remarks>
/// The colour is RGB whatever colour space the content named. No profile is applied on the way,
/// so it means what DeviceRGB means: whatever the device takes those three numbers to be.
/// </remarks>
[Experimental("PPEXP001")]
public readonly struct PagePaint : IEquatable<PagePaint>
{
    /// <summary>
    /// Initializes a paint. Each component is held between 0 and 1, and one that is not a number
    /// is 0.
    /// </summary>
    /// <param name="red">Red, from 0 to 1.</param>
    /// <param name="green">Green, from 0 to 1.</param>
    /// <param name="blue">Blue, from 0 to 1.</param>
    /// <param name="alpha">Alpha, from 0, which paints nothing, to 1, which is opaque.</param>
    public PagePaint(double red, double green, double blue, double alpha = 1)
    {
        Red = Unit(red);
        Green = Unit(green);
        Blue = Unit(blue);
        Alpha = Unit(alpha);
    }

    /// <summary>Red, from 0 to 1.</summary>
    public double Red { get; }

    /// <summary>Green, from 0 to 1.</summary>
    public double Green { get; }

    /// <summary>Blue, from 0 to 1.</summary>
    public double Blue { get; }

    /// <summary>
    /// The constant alpha, from 0 to 1: the graphics state's <c>/ca</c> for a fill, a filled glyph
    /// and an image, and its <c>/CA</c> for a stroke.
    /// </summary>
    public double Alpha { get; }

    /// <summary>The same colour with another alpha.</summary>
    /// <param name="alpha">Alpha, from 0 to 1.</param>
    public PagePaint WithAlpha(double alpha) => new(Red, Green, Blue, alpha);

    /// <inheritdoc />
    #pragma warning disable S1244 // Exact on purpose: two paints are equal when they are the same numbers.
    public bool Equals(PagePaint other) =>
        Red.Equals(other.Red) && Green.Equals(other.Green) && Blue.Equals(other.Blue) && Alpha.Equals(other.Alpha);
    #pragma warning restore S1244

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is PagePaint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Red.GetHashCode();
            hash = hash * 397 ^ Green.GetHashCode();
            hash = hash * 397 ^ Blue.GetHashCode();
            return hash * 397 ^ Alpha.GetHashCode();
        }
    }

    /// <summary>Whether two paints are the same colour and alpha.</summary>
    public static bool operator ==(PagePaint left, PagePaint right) => left.Equals(right);

    /// <summary>Whether two paints differ in colour or alpha.</summary>
    public static bool operator !=(PagePaint left, PagePaint right) => !left.Equals(right);

    // The comparison is written so that NaN, which compares false with everything, falls to 0.
    private static double Unit(double value) => value > 0 ? Math.Min(value, 1) : 0;
}
