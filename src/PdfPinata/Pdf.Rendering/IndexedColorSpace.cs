using System;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// <c>[/Indexed base hival lookup]</c> (ISO 32000-1 8.6.6.3): a colour is one number, an index
/// into a table of colours of the base space.
/// </summary>
/// <remarks>
/// An index is rounded to the nearest whole number and held between 0 and the highest index, and
/// what a table that is too short lacks is zeros, so every index has an answer. An image reads
/// its samples through <see cref="Lookup"/>, which takes the integer a sample already is.
/// </remarks>
internal sealed class IndexedColorSpace : RenderColorSpace
{
    /// <summary>The most entries a table can have: an index is a byte.</summary>
    internal const int MaximumHighValue = 255;

    private readonly RenderColorSpace _base;
    private readonly byte[] _table;
    private PagePaint[] _palette;

    internal IndexedColorSpace(RenderColorSpace baseSpace, int highValue, byte[] table)
    {
        _base = baseSpace;
        HighValue = highValue;
        _table = table;
    }

    /// <summary>The highest index there is an entry for, 0 to 255.</summary>
    internal int HighValue { get; }

    internal override int ComponentCount => 1;

    internal override PagePaint ToRgb(ReadOnlySpan<double> components)
    {
        var index = components[0];
        return Lookup(double.IsNaN(index) ? 0 : (int)Math.Round(Math.Clamp(index, 0, HighValue), MidpointRounding.AwayFromZero));
    }

    /// <summary>The colour at an index, which may be any integer: one outside the table is held to its ends.</summary>
    internal PagePaint Lookup(int index)
    {
        _palette ??= BuildPalette();
        return _palette[Math.Clamp(index, 0, HighValue)];
    }

    private PagePaint[] BuildPalette()
    {
        var size = _base.ComponentCount;
        var palette = new PagePaint[HighValue + 1];
        var components = new double[size];
        for (var entry = 0; entry < palette.Length; entry++)
        {
            for (var component = 0; component < size; component++)
            {
                var at = entry * size + component;
                components[component] = at < _table.Length ? _table[at] / 255.0 : 0;
            }

            palette[entry] = _base.ToRgb(components);
        }

        return palette;
    }
}
