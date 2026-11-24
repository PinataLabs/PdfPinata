using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// One entry of a configuration's <c>/Order</c>, the layer panel's tree (ISO 32000-1 Table 101).
/// </summary>
/// <remarks>
/// A node is a group, with the groups nested under it; or a label, which is not a group, over the
/// nodes of its array. An array with neither is not kept.
/// </remarks>
[Experimental("PPEXP001")]
public sealed class OptionalContentOrderNode
{
    internal OptionalContentOrderNode(string label, OptionalContentGroup group, IReadOnlyList<OptionalContentOrderNode> children)
    {
        Label = label;
        Group = group;
        Children = children;
    }

    /// <summary>The text the node is shown as where it is not a group, or null.</summary>
    public string Label { get; }

    /// <summary>The group the node is, or null for a label.</summary>
    public OptionalContentGroup Group { get; }

    /// <summary>The nodes nested under this one, which may be none.</summary>
    public IReadOnlyList<OptionalContentOrderNode> Children { get; }
}
