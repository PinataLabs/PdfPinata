using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Turns what content names as a colour space into a <see cref="RenderColorSpace"/>. A new kind
/// of colour space is added here and nowhere in the interpreter.
/// </summary>
internal static class ColorSpaces
{
    internal static readonly RenderColorSpace DeviceGray = new DeviceGrayColorSpace();
    internal static readonly RenderColorSpace DeviceRgb = new DeviceRgbColorSpace();
    internal static readonly RenderColorSpace DeviceCmyk = new DeviceCmykColorSpace();

    /// <summary>
    /// The space the operand of <c>cs</c> or <c>CS</c> names: one of the device spaces,
    /// <c>/Pattern</c>, or an entry of the <c>/ColorSpace</c> resources.
    /// </summary>
    /// <returns>The space, or null when the resources hold nothing by that name, which is reported.</returns>
    internal static RenderColorSpace ResolveName(string name, ResourceScope resources, DiagnosticSink sink)
    {
        var device = DeviceSpace(name);
        if (device != null)
            return device;

        if (name == "/Pattern")
            return Unsupported(name, -1, sink);

        var definition = resources.LookupItem("/ColorSpace", name);
        if (definition == null)
        {
            sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no colour space of that name.");
            return null;
        }

        return Resolve(definition, sink);
    }

    /// <summary>
    /// The space a definition describes: the value of a <c>/ColorSpace</c> resource or of an
    /// image's <c>/ColorSpace</c>, which is a name or an array that opens with the family's name.
    /// </summary>
    /// <returns>
    /// The space. Never null: a definition this version cannot resolve is reported and answered
    /// as an <see cref="UnsupportedColorSpace"/>.
    /// </returns>
    internal static RenderColorSpace Resolve(PdfItem definition, DiagnosticSink sink)
    {
        definition = PdfReference.Dereference(definition);
        if (PdfItemValues.TryGetName(definition, out var name))
            return DeviceSpace(name) ?? Unsupported(name, -1, sink);

        if (definition is not PdfArray array || array.Elements.Count == 0
            || !PdfItemValues.TryGetName(array.Elements[0], out var family))
        {
            sink.Report(PageRenderProblem.UnsupportedColorSpace, "",
                "A colour space is neither a name nor an array that opens with one.");
            return new UnsupportedColorSpace(-1, Fallback);
        }

        // Indexed and ICCBased are resolved here once they are read: Indexed through its base
        // space, ICCBased through its /Alternate or the device space its /N names. Either
        // resolves another definition, so it needs a guard against one that names itself.
        return DeviceSpace(family) ?? Unsupported(family, ComponentCountOf(family, array), sink);
    }

    private static RenderColorSpace DeviceSpace(string name) => name switch
    {
        "/DeviceGray" => DeviceGray,
        "/DeviceRGB" => DeviceRgb,
        "/DeviceCMYK" => DeviceCmyk,
        _ => null
    };

    /// <summary>What an unsupported space draws in: black, which is what colour starts as.</summary>
    private static PagePaint Fallback => new(0, 0, 0);

    private static UnsupportedColorSpace Unsupported(string family, int componentCount, DiagnosticSink sink)
    {
        sink.Report(PageRenderProblem.UnsupportedColorSpace, family,
            "This colour space is not resolved yet. What it colours is drawn in black.");
        return new UnsupportedColorSpace(componentCount, Fallback);
    }

    /// <summary>
    /// How many numbers a colour of a family takes, so that an <c>sc</c> in a space that cannot
    /// be resolved is still read as a colour of that space and not reported as malformed.
    /// </summary>
    private static int ComponentCountOf(string family, PdfArray definition)
    {
        switch (family)
        {
            case "/Indexed":
            case "/Separation":
            case "/CalGray":
                return 1;

            case "/Lab":
            case "/CalRGB":
                return 3;

            case "/ICCBased":
                var profile = definition.Elements.Count > 1
                    ? PdfReference.Dereference(definition.Elements[1]) as PdfDictionary
                    : null;
                return profile != null && PdfItemValues.TryGetInteger(profile.Elements["/N"], out var n)
                                       && n is 1 or 3 or 4
                    ? n
                    : -1;

            case "/DeviceN":
                return definition.Elements.Count > 1
                       && PdfReference.Dereference(definition.Elements[1]) is PdfArray names
                    ? names.Elements.Count
                    : -1;

            default:
                return -1;
        }
    }
}
