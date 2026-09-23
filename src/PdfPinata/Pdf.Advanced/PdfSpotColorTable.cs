using System;
using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// The <c>/Separation</c> colour spaces a document paints its spot colours in, one per colorant name.
/// </summary>
/// <remarks>
/// <para>
/// One object per name rather than per <see cref="XSpotColor"/> instance, so every page and form
/// that paints a colorant refers to the same indirect array. That is what keeps a file with a
/// thousand pages of one ink one colour space large, and what PDF/A-2 asks for besides: clause
/// 6.2.4.4 requires every Separation sharing a name to share its alternate space and tint transform.
/// </para>
/// <para>
/// So a second definition under a name already in use is refused if it disagrees about the
/// alternate. A RIP separates by name alone and would put both on one plate, with a screen proof
/// showing two different colours for it - there is no reading of that which is what the caller meant.
/// </para>
/// </remarks>
internal sealed class PdfSpotColorTable : PdfResourceTable
{
    public PdfSpotColorTable(PdfDocument document)
        : base(document)
    { }

    private readonly Dictionary<string, (XSpotColor Spot, PdfArray ColorSpace)> _colorSpaces = new(StringComparer.Ordinal);

    /// <summary>
    /// The colour space the spot colour is painted in, built the first time the colorant is used.
    /// </summary>
    public PdfArray GetColorSpace(XSpotColor spot)
    {
        if (_colorSpaces.TryGetValue(spot.Name, out var known))
        {
            if (!known.Spot.Equals(spot))
                throw new InvalidOperationException(
                    "The spot colour '" + spot.Name + "' is already painted in this document with the "
                    + "alternate " + known.Spot + ", and is now asked for with " + spot + ". A press "
                    + "separates by name, so both would print from one plate while every screen showed "
                    + "two colours, and PDF/A forbids it outright. Define the colorant once and share "
                    + "the XSpotColor, or give the second one a name of its own.");

            return known.ColorSpace;
        }

        var colorSpace = Build(spot);
        _colorSpaces.Add(spot.Name, (spot, colorSpace));
        return colorSpace;
    }

    /// <summary>
    /// <c>[/Separation name alternateSpace tintTransform]</c>, ISO 32000-1 8.6.6.4, with the tint
    /// transform a Type 2 function running from the alternate's white at tint 0 to the alternate
    /// itself at tint 1.
    /// </summary>
    private PdfArray Build(XSpotColor spot)
    {
        var alternate = spot.AlternateComponents;
        var (space, white) = spot.Alternate.ColorSpace switch
        {
            XColorSpace.Cmyk => ("/DeviceCMYK", 0.0),
            XColorSpace.GrayScale => ("/DeviceGray", 1.0),
            _ => ("/DeviceRGB", 1.0)
        };

        var c0 = new PdfArray();
        var c1 = new PdfArray();
        foreach (var component in alternate)
        {
            c0.Elements.Add(new PdfReal(white));
            c1.Elements.Add(new PdfReal(component));
        }

        var domain = new PdfArray();
        domain.Elements.Add(new PdfInteger(0));
        domain.Elements.Add(new PdfInteger(1));

        var function = new PdfDictionary
        {
            Elements =
            {
                ["/FunctionType"] = new PdfInteger(2),
                ["/Domain"] = domain,
                ["/C0"] = c0,
                ["/C1"] = c1,
                ["/N"] = new PdfInteger(1)
            }
        };

        return new PdfArray(Owner,
            new PdfName("/Separation"),
            new PdfName("/" + ColorantName(spot.Name)),
            new PdfName(space),
            function);
    }

    /// <summary>
    /// The name as the bytes PDF 2.0 says a name means - UTF-8 - held one byte to a character, which
    /// is how every name in this library is held and how the writer expects to find it. ASCII, which
    /// is what nearly every colorant is called, comes through unchanged.
    /// </summary>
    private static string ColorantName(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        var chars = new char[bytes.Length];
        for (var idx = 0; idx < bytes.Length; idx++)
            chars[idx] = (char)bytes[idx];
        return new string(chars);
    }
}
