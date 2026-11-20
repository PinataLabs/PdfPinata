using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>The families that are resolved through another colour space.</summary>
internal static partial class ColorSpaces
{
    private const int MaximumNesting = 8;

    /// <summary>
    /// The space of an array that opens with the name of a family this file resolves, or null for
    /// any other family.
    /// </summary>
    /// <remarks>
    /// <c>/CalGray</c> and <c>/CalRGB</c> are DeviceGray and DeviceRGB: their white point, gamma
    /// and matrix are not applied, in the way a profile is not applied to an ICCBased space.
    /// Nothing is reported, because the colour is close and no more can be said of it.
    /// </remarks>
    private static RenderColorSpace ResolveFamily(string family, PdfArray array, DiagnosticSink sink,
        List<PdfItem> open) => family switch
    {
        "/CalGray" => DeviceGray,
        "/CalRGB" => DeviceRgb,
        "/Indexed" => ResolveIndexed(array, sink, open),
        "/ICCBased" => ResolveIccBased(array, sink, open),
        _ => null
    };

    private static RenderColorSpace ResolveIndexed(PdfArray array, DiagnosticSink sink, List<PdfItem> open)
    {
        if (array.Elements.Count < 4)
        {
            Invalid(sink, "/Indexed", "An Indexed space is [/Indexed base hival lookup]. This one has too few entries.");
            return new UnsupportedColorSpace(1, Fallback);
        }

        var baseSpace = ResolveNested(array.Elements[1], sink, open);

        // An unsupported base has been reported already, and what it cannot colour no table can.
        if (baseSpace is UnsupportedColorSpace)
            return new UnsupportedColorSpace(1, Fallback);

        if (baseSpace is IndexedColorSpace || baseSpace.ComponentCount < 1)
        {
            Invalid(sink, "/Indexed", "An Indexed space cannot be based on an Indexed or a Pattern space.");
            return new UnsupportedColorSpace(1, Fallback);
        }

        var table = TableOf(PdfReference.Dereference(array.Elements[3]));
        if (table == null)
        {
            Invalid(sink, "/Indexed", "The lookup table is neither a string nor a stream that can be read. It is taken as empty.");
            table = [];
        }

        var entrySize = baseSpace.ComponentCount;
        int highValue;
        if (PdfItemValues.TryGetNumber(array.Elements[2], out var number) && !double.IsNaN(number))
        {
            highValue = (int)Math.Clamp(number, 0, IndexedColorSpace.MaximumHighValue);
        }
        else
        {
            Invalid(sink, "/Indexed", "The highest index is not a number. It is taken from the length of the lookup table.");
            highValue = Math.Clamp(table.Length / entrySize - 1, 0, IndexedColorSpace.MaximumHighValue);
        }

        if (table.Length < (highValue + 1) * entrySize)
        {
            Invalid(sink, "/Indexed", "The lookup table is shorter than the highest index needs. What it lacks is taken as zeros.");
        }

        return new IndexedColorSpace(baseSpace, highValue, table);
    }

    private static byte[] TableOf(PdfItem lookup)
    {
        switch (lookup)
        {
            case PdfString text:
                return text.EncryptionValue;

            case PdfStringObject textObject:
                return textObject.EncryptionValue;

            case PdfDictionary stream when PdfContentStreams.TryGetContent(stream, out var content):
                return content;

            default:
                return null;
        }
    }

    private static RenderColorSpace ResolveIccBased(PdfArray array, DiagnosticSink sink, List<PdfItem> open)
    {
        var profile = array.Elements.Count > 1 ? PdfReference.Dereference(array.Elements[1]) as PdfDictionary : null;
        if (profile == null)
        {
            Invalid(sink, "/ICCBased", "An ICCBased space is [/ICCBased profile], and the profile is a stream. This one has none.");
            return new UnsupportedColorSpace(-1, Fallback);
        }

        var componentCount = ComponentCountOf("/ICCBased", array);

        // The alternate is preferred, as the issue says, so long as it can stand in for the profile.
        var alternate = profile.Elements["/Alternate"];
        if (!PdfItemValues.IsNull(alternate))
        {
            var space = ResolveNested(alternate, sink, open);
            if (space is not UnsupportedColorSpace)
            {
                if (space is IndexedColorSpace || componentCount > 0 && space.ComponentCount != componentCount)
                    Invalid(sink, "/ICCBased", "The alternate space cannot stand in for the profile. It is ignored.");
                else
                    return new IccBasedColorSpace(space);
            }
        }

        switch (componentCount)
        {
            case 1: return new IccBasedColorSpace(DeviceGray);
            case 3: return new IccBasedColorSpace(DeviceRgb);
            case 4: return new IccBasedColorSpace(DeviceCmyk);
            default:
                Invalid(sink, "/ICCBased", "The profile's /N is not 1, 3 or 4, and it names no alternate that can be used.");
                return new UnsupportedColorSpace(-1, Fallback);
        }
    }

    private static void Invalid(DiagnosticSink sink, string family, string message) =>
        sink.Report(PageRenderProblem.InvalidValue, family, message);
}
