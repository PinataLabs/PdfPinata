using System;
using PdfPinata.Internal;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Reads the dictionary of an inline image, written as text between <c>BI</c> and <c>ID</c>, into
/// the entries an image XObject has, so that one path decodes both.
/// </summary>
/// <remarks>
/// An inline image may abbreviate its keys, its device colour spaces, <c>/Indexed</c> and its
/// filters (ISO 32000-1 Tables 93 and 94). Each is written out in full here, so nothing after
/// this, the colour spaces included, knows abbreviations exist.
/// </remarks>
internal static class InlineImageDictionary
{
    /// <summary>
    /// How deep arrays and dictionaries may be nested inside an inline image's dictionary. The
    /// document parser reads each by a call inside the one that reads what holds it, so this
    /// bounds the depth of its stack. An image needs two: a <c>/DecodeParms</c> array of
    /// dictionaries.
    /// </summary>
    private const int MaximumDepth = 32;

    /// <summary>The entries with every abbreviation written out, or null when the text is no dictionary.</summary>
    internal static PdfDictionary Read(string entries)
    {
        PdfDictionary written;
        try
        {
            written = Parser.ReadDictionary(entries, MaximumDepth);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            return null;
        }

        if (written == null)
            return null;

        var full = new PdfDictionary();
        foreach (var key in written.Elements.Keys)
        {
            var fullKey = FullKey(key);
            full.Elements[fullKey] = fullKey switch
            {
                "/ColorSpace" => FullColorSpace(written.Elements[key]),
                "/Filter" => FullFilter(written.Elements[key]),
                _ => written.Elements[key]
            };
        }

        return full;
    }

    private static string FullKey(string key) => key switch
    {
        "/BPC" => "/BitsPerComponent",
        "/CS" => "/ColorSpace",
        "/D" => "/Decode",
        "/DP" => "/DecodeParms",
        "/F" => "/Filter",
        "/H" => "/Height",
        "/IM" => "/ImageMask",
        "/I" => "/Interpolate",
        "/L" => "/Length",
        "/W" => "/Width",
        _ => key
    };

    private static PdfItem FullColorSpace(PdfItem space)
    {
        if (PdfItemValues.TryGetName(space, out var name))
            return new PdfName(FullColorSpaceName(name));

        if (space is not PdfArray array)
            return space;

        // Only an indexed space's base may be abbreviated: a /Separation may name a colorant G.
        var full = new PdfArray();
        foreach (var element in array.Elements)
            full.Elements.Add(element);
        if (full.Elements.Count > 0 && PdfItemValues.TryGetName(full.Elements[0], out var family)
                                    && family is "/I" or "/Indexed")
        {
            full.Elements[0] = new PdfName("/Indexed");
            if (full.Elements.Count > 1 && PdfItemValues.TryGetName(full.Elements[1], out var baseSpace))
                full.Elements[1] = new PdfName(FullColorSpaceName(baseSpace));
        }

        return full;
    }

    private static string FullColorSpaceName(string name) => name switch
    {
        "/G" => "/DeviceGray",
        "/RGB" => "/DeviceRGB",
        "/CMYK" => "/DeviceCMYK",
        "/I" => "/Indexed",
        _ => name
    };

    private static PdfItem FullFilter(PdfItem filter)
    {
        if (PdfItemValues.TryGetName(filter, out var name))
            return new PdfName(FullFilterName(name));

        if (filter is not PdfArray array)
            return filter;

        var full = new PdfArray();
        foreach (var element in array.Elements)
            full.Elements.Add(PdfItemValues.TryGetName(element, out var each) ? new PdfName(FullFilterName(each)) : element);
        return full;
    }

    private static string FullFilterName(string name) => name switch
    {
        "/AHx" => "/ASCIIHexDecode",
        "/A85" => "/ASCII85Decode",
        "/LZW" => "/LZWDecode",
        "/Fl" => "/FlateDecode",
        "/RL" => "/RunLengthDecode",
        "/CCF" => "/CCITTFaxDecode",
        "/DCT" => "/DCTDecode",
        _ => name
    };
}
