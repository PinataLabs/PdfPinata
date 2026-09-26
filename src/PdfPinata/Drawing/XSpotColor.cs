using System;
using System.Globalization;

namespace PdfPinata.Drawing;

/// <summary>
/// A named colorant — a spot colour such as a Pantone ink, a varnish or a white underprint — and
/// the process colour a device without that ink paints instead.
/// </summary>
/// <remarks>
/// <para>
/// Written as a <c>/Separation</c> colour space (ISO 32000-1 8.6.6.4). A press or RIP that has a
/// plate called <see cref="Name"/> puts the ink on it; anything else — a screen, an office
/// printer, a proof — paints <see cref="Alternate"/> scaled by the tint. Paint with it through
/// <see cref="XColor.FromSpot(XSpotColor, double)"/>, which gives an <see cref="XColor"/> usable
/// wherever one is: an <see cref="XSolidBrush"/>, an <see cref="XPen"/>, text.
/// </para>
/// <para>
/// The alternate is the colour at full tint, and its <see cref="XColor.ColorSpace"/> decides the
/// space the fallback is painted in: <c>DeviceRGB</c>, <c>DeviceCMYK</c> or <c>DeviceGray</c>. Its
/// alpha is ignored — transparency belongs to the colour painted with, not to the ink.
/// </para>
/// <para>
/// A document writes one colour space per name, however many instances describe it. Two
/// definitions sharing a name and disagreeing about the alternate are refused when the second is
/// drawn with, because a RIP separates by name and would put both on one plate.
/// </para>
/// </remarks>
public sealed class XSpotColor : IEquatable<XSpotColor>
{
    /// <summary>
    /// Defines a spot colour.
    /// </summary>
    /// <param name="name">
    /// The colorant's name as the press knows it, e.g. <c>"PANTONE 185 C"</c> or <c>"White"</c>.
    /// Written as UTF-8, which is what PDF 2.0 says a name means.
    /// </param>
    /// <param name="alternate">The process colour a device without the ink paints at full tint.</param>
    public XSpotColor(string name, XColor alternate)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
            throw new ArgumentException("A spot colour needs a name: it is the name a press separates by.", nameof(name));

        Name = name;
        alternate.A = 1;
        Alternate = alternate.ColorSpace == XColorSpace.GrayScale ? ConsistentGray(alternate) : alternate;
    }

    /// <summary>
    /// A grey alternate whose <see cref="XColor.GS"/> means lightness, as the tint transform reads it.
    /// </summary>
    /// <remarks>
    /// A colour whose <see cref="XColor.ColorSpace"/> was set to grey afterwards keeps the
    /// <c>GS</c> it was built with. Built from RGB, that is the lightness of its channels; built
    /// from CMYK, it is a weighing of the inks that can land away from what its RGB channels say,
    /// and a colour set from <see cref="XColor.RgbCmykG"/> can carry anything. The RGB components
    /// are what every other reader paints, so a GS that disagrees with them is replaced by theirs.
    /// </remarks>
    private static XColor ConsistentGray(XColor alternate)
    {
        double fromRgb = XColor.LightnessOf(alternate.R, alternate.G, alternate.B);

        return Math.Abs(alternate.GS - fromRgb) <= 1 / 255.0 ? alternate : XColor.FromGrayScale(fromRgb);
    }

    /// <summary>The colorant's name.</summary>
    public string Name { get; }

    /// <summary>
    /// The process colour painted at full tint where the colorant is not available. Always opaque.
    /// </summary>
    public XColor Alternate { get; }

    /// <summary>
    /// The alternate at the given tint: the Type 2 function the colour space is written with,
    /// evaluated here, so that anything reading an <see cref="XColor"/>'s process components —
    /// a gradient, a caller — sees what a device without the ink would paint.
    /// </summary>
    internal XColor Tinted(double tint)
    {
        return Alternate.ColorSpace switch
        {
            XColorSpace.Cmyk => XColor.FromCmyk(Alternate.C * tint, Alternate.M * tint, Alternate.Y * tint, Alternate.K * tint),
            XColorSpace.GrayScale => XColor.FromGrayScale(1 - tint * (1 - Alternate.GS)),
            _ => XColor.FromArgb(TowardWhite(Alternate.R, tint), TowardWhite(Alternate.G, tint), TowardWhite(Alternate.B, tint))
        };
    }

    private static int TowardWhite(byte component, double tint) =>
        (int)Math.Round(255 - tint * (255 - component), MidpointRounding.AwayFromZero);

    /// <summary>
    /// The alternate's components as the numbers written into the tint transform's <c>/C1</c>,
    /// which is also what decides whether two definitions agree.
    /// </summary>
    internal double[] AlternateComponents => Alternate.ColorSpace switch
    {
        XColorSpace.Cmyk => [Alternate.C, Alternate.M, Alternate.Y, Alternate.K],
        XColorSpace.GrayScale => [Alternate.GS],
        _ => [Alternate.R / 255.0, Alternate.G / 255.0, Alternate.B / 255.0]
    };

    /// <summary>
    /// Whether the other definition names the same colorant with the same alternate — the test
    /// a document applies before letting the two share one colour space.
    /// </summary>
    public bool Equals(XSpotColor other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal) || Alternate.ColorSpace != other.Alternate.ColorSpace)
            return false;

        var mine = AlternateComponents;
        var theirs = other.AlternateComponents;
        for (var idx = 0; idx < mine.Length; idx++)
        {
            #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
            if (mine[idx] != theirs[idx])
                return false;
            #pragma warning restore S1244
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as XSpotColor);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <inheritdoc />
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0} ({1} {2})", Name, Alternate.ColorSpace,
            string.Join(" ", Array.ConvertAll(AlternateComponents, c => c.ToString("0.###", CultureInfo.InvariantCulture))));
}
