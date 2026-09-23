using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// The questions the four unenforced PDF/A rules ask of a page-resource walk. The walk itself —
/// <see cref="PdfPageResourceUsage"/> — only reports what a page reaches; every judgment about what
/// that means for a claimed profile lives here, beside the rest of the conformance rules, rather
/// than inside the walk. A walk that knew about PDF/A would have to change for every future rule.
/// </summary>
internal static class PdfResourceConformanceRules
{
    /// <summary>
    /// Whether the page paints with transparency, reached through an image's soft mask, a graphics
    /// state's alpha, blend mode or soft mask, or a form declaring a transparency group of its own —
    /// however deep the form or soft mask that carries it is nested.
    /// </summary>
    internal static bool UsesTransparency(PdfPageResourceUsage usage)
    {
        if (usage.Images.Any(PdfTransparencyDetector.ImagePaints))
            return true;

        if (usage.GraphicsStates.Any(PdfTransparencyDetector.StatePaints))
            return true;

        return usage.Forms.Any(DeclaresTransparencyGroup);
    }

    /// <summary>
    /// Whether a form or a page declares a transparency group. A page carries one of its own when
    /// it is imported from a document that composited it as a group, and PDF/A-1 forbids that
    /// just as it forbids a form's.
    /// </summary>
    internal static bool DeclaresTransparencyGroup(PdfDictionary formOrPage)
    {
        var group = formOrPage.Elements.GetDictionary(PdfPage.Keys.Group);
        return group != null && group.Elements.GetName("/S") == "/Transparency";
    }

    /// <summary>Whether any image the page reaches is filtered with <c>/JPXDecode</c>.</summary>
    internal static bool UsesJpxImage(PdfPageResourceUsage usage) => usage.Images.Any(HasJpxFilter);

    private static bool HasJpxFilter(PdfDictionary image)
    {
        var filter = Resolve(image.Elements["/Filter"]);

        if (filter is PdfName single)
            return single.Value == "/JPXDecode";

        if (filter is PdfArray array)
        {
            foreach (var item in array.Elements)
            {
                if (Resolve(item) is PdfName name && name.Value == "/JPXDecode")
                    return true;
            }
        }

        return false;
    }

    /// <summary>Whether any image the page reaches is set to interpolate.</summary>
    internal static bool UsesInterpolatedImage(PdfPageResourceUsage usage) =>
        usage.Images.Any(image => image.Elements.GetBoolean("/Interpolate"));

    /// <summary>
    /// Adds the component count of every <em>device</em> colour family the page paints with — 1 for
    /// grey, 3 for RGB, 4 for CMYK, and whatever an indexed, separation, device-N or uncoloured
    /// pattern space resolves to underneath — to <paramref name="families"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reported as component counts rather than as colour spaces, because that is the one fact
    /// <see cref="PdfConformanceWriter"/> can compare against an output intent's own <c>/N</c> — the
    /// two do not have to be the same kind of colour space to agree, only the same size.
    /// </para>
    /// <para>
    /// Device colour alone, because the output intent exists to say what those four uncalibrated
    /// numbers mean and nothing else needs saying: an <c>/ICCBased</c>, <c>/CalGray</c>,
    /// <c>/CalRGB</c> or <c>/Lab</c> space already carries its own mapping to a
    /// device-independent model, so PDF/A lets it stand whatever the output intent describes.
    /// Counting those would refuse a CMYK ICC-based image in an sRGB document, which is exactly
    /// the arrangement PDF/A permits.
    /// </para>
    /// </remarks>
    internal static void CollectDeviceColorFamilies(PdfPageResourceUsage usage, HashSet<int> families)
    {
        if (usage.UsesDeviceGray)
            families.Add(1);
        if (usage.UsesDeviceRgb)
            families.Add(3);
        if (usage.UsesDeviceCmyk)
            families.Add(4);

        foreach (var image in usage.Images)
        {
            var components = DeviceComponentsOf(image.Elements["/ColorSpace"], 0);
            if (components != null)
                families.Add(components.Value);
        }

        foreach (var colorSpace in usage.NamedColorSpaces)
        {
            var components = DeviceComponentsOf(colorSpace, 0);
            if (components != null)
                families.Add(components.Value);
        }
    }

    /// <summary>
    /// How many components the device colour space underneath this one has, following an indexed,
    /// separation, device-N or uncoloured pattern space down to the space it is really painted in.
    /// Null for anything that is not device colour — a calibrated or ICC-based space, an
    /// unrecognised name, or nesting deep enough that this has given up understanding it, which is
    /// the same defensive limit the walk itself uses.
    /// </summary>
    private static int? DeviceComponentsOf(PdfItem colorSpace, int depth)
    {
        if (depth > 8)
            return null;

        var item = Resolve(colorSpace);

        switch (item)
        {
            case PdfName name:
                return DeviceFamilyOf(name.Value);

            case PdfArray array when array.Elements.Count > 0:
                return DeviceComponentsOfArray(array, depth);

            default:
                // An ICC profile stream referenced directly rather than through the usual
                // [/ICCBased ref] array included: device-independent either way.
                return null;
        }
    }

    private static int? DeviceComponentsOfArray(PdfArray array, int depth)
    {
        var head = Resolve(array.Elements[0]) as PdfName;
        switch (head?.Value)
        {
            case "/Indexed":
                return array.Elements.Count > 1 ? DeviceComponentsOf(array.Elements[1], depth + 1) : null;

            case "/Separation":
            case "/DeviceN":
                // [/Separation name alternateSpace tintTransform] and
                // [/DeviceN names alternateSpace tintTransform ...] agree on where the space
                // a reader without the separation ink actually paints in sits. A separation is not
                // itself device colour, but its alternate may be, and a reader that has to fall
                // back on the alternate paints those numbers for real — so it is held to the
                // output intent exactly as painting them outright would be.
                return array.Elements.Count > 2 ? DeviceComponentsOf(array.Elements[2], depth + 1) : null;

            case "/Pattern":
                // An uncoloured tiling pattern names the space its colour operands are given in;
                // a coloured one and a shading pattern carry no colour of their own to ask about.
                return array.Elements.Count > 1 ? DeviceComponentsOf(array.Elements[1], depth + 1) : null;

            default:
                // /ICCBased, /CalGray, /CalRGB and /Lab all reach here, and all say for themselves
                // what their numbers mean. So does a name this does not know.
                return null;
        }
    }

    /// <summary>
    /// The component count of a device colour space named outright. The three short spellings are
    /// only legal inside an inline image dictionary, which defeats the walk before this is ever
    /// asked — they are here because they mean the same three spaces, not because they are reached.
    /// </summary>
    private static int? DeviceFamilyOf(string name) => name switch
    {
        "/DeviceGray" or "/G" => 1,
        "/DeviceRGB" or "/RGB" => 3,
        "/DeviceCMYK" or "/CMYK" => 4,
        _ => null
    };

    private static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;
}
