using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>What a configuration says of a group it does not list (ISO 32000-1 Table 101).</summary>
[Experimental("PPEXP001")]
public enum OptionalContentBaseState
{
    /// <summary>Every group starts on, and <c>/OFF</c> lists those that are not.</summary>
    On,

    /// <summary>Every group starts off, and <c>/ON</c> lists those that are not.</summary>
    Off,

    /// <summary>Groups start as the default configuration has them, and the lists change those.</summary>
    Unchanged
}

/// <summary>
/// The default configuration, <c>/D</c>, or one of the <c>/Configs</c> of a document's
/// <c>/OCProperties</c>, as <see cref="OptionalContentLayers"/> reads it. A snapshot: what it holds
/// does not change when the document does.
/// </summary>
[Experimental("PPEXP001")]
public sealed class OptionalContentConfiguration
{
    private readonly HashSet<OptionalContentGroup> _on;

    internal OptionalContentConfiguration(PdfDictionary dictionary, string name, string creator, OptionalContentBaseState baseState,
        IReadOnlyList<string> intents, HashSet<OptionalContentGroup> on, IReadOnlyList<OptionalContentOrderNode> order,
        IReadOnlyList<IReadOnlyList<OptionalContentGroup>> radioButtonGroups)
    {
        Dictionary = dictionary;
        Name = name;
        Creator = creator;
        BaseState = baseState;
        Intents = intents;
        _on = on;
        Order = order;
        RadioButtonGroups = radioButtonGroups;
    }

    /// <summary>The configuration's dictionary, null for a default configuration a document does not have.</summary>
    internal PdfDictionary Dictionary { get; }

    /// <summary>
    /// The <c>/Name</c>, which is what <see cref="PageOptionalContent.Configuration"/> takes, or
    /// null where there is none. The default configuration usually has none.
    /// </summary>
    public string Name { get; }

    /// <summary>The <c>/Creator</c>, or null.</summary>
    public string Creator { get; }

    /// <summary>The <c>/BaseState</c>, <see cref="OptionalContentBaseState.On"/> where there is none.</summary>
    public OptionalContentBaseState BaseState { get; }

    /// <summary>
    /// The <c>/Intent</c> without the slash, <c>View</c> where there is none, and <c>All</c> where
    /// it says every intent. A group counts only where one of its intents is among these.
    /// </summary>
    public IReadOnlyList<string> Intents { get; }

    /// <summary>The <c>/Order</c> tree: the groups, nested and labelled as a layer panel shows them. Empty where there is none.</summary>
    public IReadOnlyList<OptionalContentOrderNode> Order { get; }

    /// <summary>
    /// The <c>/RBGroups</c>: sets of groups of which at most one is on. Only groups of
    /// <see cref="OptionalContentLayers.Groups"/> are kept, and an empty set is not.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<OptionalContentGroup>> RadioButtonGroups { get; }

    /// <summary>
    /// Whether the configuration has the group on, as <c>/BaseState</c>, <c>/ON</c> and
    /// <c>/OFF</c> say. Whether the group's intent counts is a separate matter, and false is
    /// answered for a group that is not from the same <see cref="OptionalContentLayers"/>.
    /// </summary>
    public bool IsOn(OptionalContentGroup group) => group != null && _on.Contains(group);
}
