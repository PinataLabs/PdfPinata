using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Turns what content names as a colour space into a <see cref="RenderColorSpace"/>. A new kind
/// of colour space is added here and nowhere in the interpreter.
/// </summary>
internal static partial class ColorSpaces
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

        return ResolveNested(definition, sink, []);
    }

    /// <summary>
    /// The space a definition describes: the value of a <c>/ColorSpace</c> resource or of an
    /// image's <c>/ColorSpace</c>, which is a name or an array that opens with the family's name.
    /// </summary>
    /// <returns>
    /// The space. Never null: a definition this version cannot resolve is reported and answered
    /// as an <see cref="UnsupportedColorSpace"/>.
    /// </returns>
    internal static RenderColorSpace Resolve(PdfItem definition, DiagnosticSink sink) =>
        ResolveNested(definition, sink, []);

    /// <param name="definition">The definition.</param>
    /// <param name="sink">Where a problem is reported.</param>
    /// <param name="open">
    /// The arrays being resolved, outermost first: an Indexed space resolves its base and an
    /// ICCBased one its alternate, so a definition that names itself would otherwise never end.
    /// </param>
    /// <param name="consequence">What an unsupported space is reported to cost the colour it stands for.</param>
    private static RenderColorSpace ResolveNested(PdfItem definition, DiagnosticSink sink, List<PdfItem> open,
        string consequence = DrawnInBlack)
    {
        definition = PdfReference.Dereference(definition);
        if (PdfItemValues.TryGetName(definition, out var name))
            return DeviceSpace(name) ?? Unsupported(name, -1, sink, consequence);

        if (definition is not PdfArray array || array.Elements.Count == 0
            || !PdfItemValues.TryGetName(array.Elements[0], out var family))
        {
            sink.Report(PageRenderProblem.UnsupportedColorSpace, "",
                "A colour space is neither a name nor an array that opens with one.");
            return new UnsupportedColorSpace(-1, Fallback);
        }

        if (open.Count >= MaximumNesting || open.Exists(item => ReferenceEquals(item, array)))
        {
            sink.Report(PageRenderProblem.InvalidValue, family,
                "A colour space is defined in terms of itself, or nested too deeply to follow. It is drawn in black.");
            return new UnsupportedColorSpace(ComponentCountOf(family, array), Fallback);
        }

        open.Add(array);
        try
        {
            return ResolveFamily(family, array, sink, open) ?? DeviceSpace(family)
                   ?? Unsupported(family, ComponentCountOf(family, array), sink, consequence);
        }
        finally
        {
            open.RemoveAt(open.Count - 1);
        }
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

    private const string DrawnInBlack = "What it colours is drawn in black.";

    private static UnsupportedColorSpace Unsupported(string family, int componentCount, DiagnosticSink sink,
        string consequence = DrawnInBlack)
    {
        sink.Report(PageRenderProblem.UnsupportedColorSpace, family,
            "This colour space is not resolved yet. " + consequence);
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
