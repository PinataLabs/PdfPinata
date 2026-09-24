using System.Collections.Generic;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// What a page's resources actually contain, found by walking its content the way
/// <see cref="PdfResourcePruner"/> always has: every image and form the page reaches, however deep
/// the forms and soft masks nest, every graphics state it sets, every colour space its content names
/// or paints with directly.
/// </summary>
/// <remarks>
/// This is the walk's other caller — built once in <see cref="PdfPageWalk"/> and asked two different
/// questions rather than written twice. It only reports what a page uses; the PDF/A rules that need
/// this are asked in <c>Metadata.PdfResourceConformanceRules</c> and
/// <c>Metadata.PdfConformanceWriter</c>, not here — a walk that knew about PDF/A would have to change
/// for every future rule.
/// </remarks>
internal sealed class PdfPageResourceUsage : PdfPageWalk
{
    /// <summary>Every image XObject the page reaches.</summary>
    internal List<PdfDictionary> Images { get; } = [];

    /// <summary>Every form XObject the page reaches.</summary>
    internal List<PdfDictionary> Forms { get; } = [];

    /// <summary>Every graphics state the page sets.</summary>
    internal List<PdfDictionary> GraphicsStates { get; } = [];

    /// <summary>
    /// Every colour space the page names through a resource dictionary — a stream (an ICCBased
    /// profile), an array (<c>Indexed</c>, <c>Separation</c>, <c>DeviceN</c>, <c>CalRGB</c> and
    /// their kin) or a bare name, whatever the resource dictionary held.
    /// </summary>
    internal List<PdfItem> NamedColorSpaces { get; } = [];

    /// <summary>
    /// Whether the page's content sets grey directly — with <c>g</c> or <c>G</c>, or by selecting
    /// <c>/DeviceGray</c> with <c>cs</c> or <c>CS</c>.
    /// </summary>
    internal bool UsesDeviceGray { get; private set; }

    /// <summary>
    /// Whether the page's content sets RGB directly — with <c>rg</c> or <c>RG</c>, or by selecting
    /// <c>/DeviceRGB</c> with <c>cs</c> or <c>CS</c>.
    /// </summary>
    internal bool UsesDeviceRgb { get; private set; }

    /// <summary>
    /// Whether the page's content sets CMYK directly — with <c>k</c> or <c>K</c>, or by selecting
    /// <c>/DeviceCMYK</c> with <c>cs</c> or <c>CS</c>.
    /// </summary>
    internal bool UsesDeviceCmyk { get; private set; }

    /// <summary>
    /// Walks the page. <see cref="PdfPageWalk.Understood"/> says whether the whole of it was —
    /// a page whose content defeats the walk answers with none of its resources found, and the
    /// caller is expected to leave it unchecked rather than judge it on a guess.
    /// </summary>
    internal static PdfPageResourceUsage Walk(PdfPage page)
    {
        // A page with no resource dictionary still has to be walked. It can name nothing to draw
        // with, but it can paint a device colour outright — "0 0 0 1 k" names no resource — and its
        // annotations can carry appearance streams with resources of their own. Skipping it would
        // hand the caller an empty answer that says Understood, which is a page judged on a guess.
        var resources = page.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources) ?? new PdfDictionary();
        var usage = new PdfPageResourceUsage(resources);
        usage.ReadPage(page);

        return usage;
    }

    private PdfPageResourceUsage(PdfDictionary resources) : base(resources)
    {
    }

    protected override void Observe(COperator op, PdfDictionary scope, int depth)
    {
        switch (op.OpCode.OpCodeName)
        {
            case OpCodeName.g:
            case OpCodeName.G:
                UsesDeviceGray = true;
                break;

            case OpCodeName.rg:
            case OpCodeName.RG:
                UsesDeviceRgb = true;
                break;

            case OpCodeName.k:
            case OpCodeName.K:
                UsesDeviceCmyk = true;
                break;

            case OpCodeName.cs:
            case OpCodeName.CS:
                // The other way to paint a device colour: select the space by name and set the
                // components afterwards with sc or scn. The walk deliberately does not look these
                // three names up — no resource dictionary ever holds them — so they would otherwise
                // go unrecorded, and a page painting "/DeviceCMYK cs 0 0 0 1 sc" would read as
                // painting nothing at all.
                switch (NameOfFirstOperand(op))
                {
                    case "/DeviceGray":
                        UsesDeviceGray = true;
                        break;
                    case "/DeviceRGB":
                        UsesDeviceRgb = true;
                        break;
                    case "/DeviceCMYK":
                        UsesDeviceCmyk = true;
                        break;
                }
                break;
        }
    }

    private static string NameOfFirstOperand(COperator op) =>
        op.Operands.Count > 0 && op.Operands[0] is CName name ? name.Name : null;

    protected override void RecordResolved(string category, string name, PdfItem resolved)
    {
        if (resolved == null)
            return;

        switch (category)
        {
            case "/XObject":
                if (resolved is PdfDictionary xObject)
                    RecordXObject(xObject);
                break;

            case "/ExtGState":
                if (resolved is PdfDictionary state)
                    GraphicsStates.Add(state);
                break;

            case "/ColorSpace":
                NamedColorSpaces.Add(resolved);
                break;
        }
    }

    private void RecordXObject(PdfDictionary xObject)
    {
        var subtype = xObject.Elements.GetName("/Subtype");
        if (subtype == "/Image")
            Images.Add(xObject);
        else if (subtype == "/Form")
            Forms.Add(xObject);
    }
}
