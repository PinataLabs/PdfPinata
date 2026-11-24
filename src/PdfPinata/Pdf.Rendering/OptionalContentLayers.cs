using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// A document's optional content groups and configurations, read for a layer panel: what
/// <c>/OCProperties</c> says, as a model (ISO 32000-1 8.11.4).
/// </summary>
/// <remarks>
/// <para>
/// A snapshot that reads and never writes. Each group's <see cref="OptionalContentGroup.Dictionary"/>
/// is what <see cref="PageOptionalContent.On"/> and <see cref="PageOptionalContent.Off"/> take, and
/// each configuration's <see cref="OptionalContentConfiguration.Name"/> is what
/// <see cref="PageOptionalContent.Configuration"/> takes, so a layer a reader switches is passed
/// straight back.
/// </para>
/// <para>
/// A document with no <c>/OCProperties</c> reads as no groups and a default configuration that
/// is empty. Entries that are not what they should be are left out and nothing throws: a
/// dictionary in <c>/OCGs</c> that is not a group, a group named twice, a configuration that is
/// not a dictionary, an <c>/Order</c> or <c>/RBGroups</c> entry that names no listed group. An
/// <c>/Order</c> that holds itself is read once, and one nested more than 32 deep, or of more than
/// 10,000 entries, is read as far as that.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public sealed class OptionalContentLayers
{
    private const int MaximumOrderDepth = 32;
    private const int MaximumOrderNodes = 10_000;

    private OptionalContentLayers(IReadOnlyList<OptionalContentGroup> groups, OptionalContentConfiguration defaultConfiguration,
        IReadOnlyList<OptionalContentConfiguration> configurations)
    {
        Groups = groups;
        Default = defaultConfiguration;
        Configurations = configurations;
    }

    /// <summary>The groups of <c>/OCGs</c>, in the order it lists them.</summary>
    public IReadOnlyList<OptionalContentGroup> Groups { get; }

    /// <summary>The <c>/D</c> configuration, which a render uses where no other is chosen. Never null.</summary>
    public OptionalContentConfiguration Default { get; }

    /// <summary>The <c>/Configs</c>, in order. A name that two have is the first's to a render.</summary>
    public IReadOnlyList<OptionalContentConfiguration> Configurations { get; }

    /// <summary>Reads the optional content of a document, which may state none.</summary>
    public static OptionalContentLayers Read(PdfDocument document)
    {
        var properties = document?.Catalog.Elements.GetDictionary(PdfCatalog.Keys.OCProperties);
        var groups = new List<OptionalContentGroup>();
        var byKey = new Dictionary<object, OptionalContentGroup>(ReferenceIdentity.Instance);

        var listed = properties?.Elements.GetArray("/OCGs");
        for (var index = 0; listed != null && index < listed.Elements.Count; index++)
        {
            if (listed.Elements.GetDictionary(index) is not { } dictionary
                || dictionary.Elements.GetNameOrEmpty("/Type") is not ("/OCG" or ""))
            {
                continue;
            }

            var key = ReferenceIdentity.KeyOf(dictionary);
            if (byKey.ContainsKey(key))
                continue;

            var group = new OptionalContentGroup(dictionary,
                dictionary.Elements.TryGetString("/Name", out var name) ? name : null, IntentsOf(dictionary.Elements["/Intent"]));
            byKey[key] = group;
            groups.Add(group);
        }

        var defaults = properties?.Elements.GetDictionary("/D");
        var configurations = new List<OptionalContentConfiguration>();
        var configs = properties?.Elements.GetArray("/Configs");
        for (var index = 0; configs != null && index < configs.Elements.Count; index++)
        {
            if (configs.Elements.GetDictionary(index) is { } configuration)
                configurations.Add(ReadConfiguration(configuration, defaults, groups, byKey));
        }

        return new OptionalContentLayers(groups, ReadConfiguration(defaults, defaults, groups, byKey), configurations);
    }

    private static OptionalContentConfiguration ReadConfiguration(PdfDictionary configuration, PdfDictionary defaults,
        List<OptionalContentGroup> groups, Dictionary<object, OptionalContentGroup> byKey)
    {
        var state = OptionalContent.For(configuration, defaults);
        var on = new HashSet<OptionalContentGroup>();
        foreach (var group in groups)
        {
            if (state.IsOn(group.Dictionary))
                on.Add(group);
        }

        var baseState = configuration?.Elements.GetNameOrEmpty("/BaseState") switch
        {
            "/OFF" => OptionalContentBaseState.Off,
            "/Unchanged" when !ReferenceEquals(configuration, defaults) => OptionalContentBaseState.Unchanged,
            _ => OptionalContentBaseState.On
        };

        string creator = null;
        string name = null;
        configuration?.Elements.TryGetString("/Creator", out creator);
        configuration?.Elements.TryGetString("/Name", out name);

        var radio = new List<IReadOnlyList<OptionalContentGroup>>();
        var sets = configuration?.Elements.GetArray("/RBGroups");
        for (var index = 0; sets != null && index < sets.Elements.Count; index++)
        {
            if (sets.Elements.GetArray(index) is not { } set)
                continue;

            var members = new List<OptionalContentGroup>();
            for (var member = 0; member < set.Elements.Count; member++)
            {
                if (set.Elements.GetDictionary(member) is { } dictionary
                    && byKey.TryGetValue(ReferenceIdentity.KeyOf(dictionary), out var group) && !members.Contains(group))
                {
                    members.Add(group);
                }
            }

            if (members.Count > 0)
                radio.Add(members);
        }

        var order = configuration?.Elements.GetArray("/Order") is { } array
            ? new OrderReader(byKey).Read(array)
            : new List<OptionalContentOrderNode>();

        return new OptionalContentConfiguration(name, creator, baseState, IntentsOf(configuration?.Elements["/Intent"]), on, order, radio);
    }

    private static List<string> IntentsOf(PdfItem intent)
    {
        var intents = new List<string>();
        switch (PdfReference.Dereference(intent))
        {
            case PdfArray names:
                foreach (var item in names.Elements)
                {
                    if (PdfItemValues.TryGetName(item, out var value))
                        intents.Add(value.TrimStart('/'));
                }

                break;

            case var single when PdfItemValues.TryGetName(single, out var value):
                intents.Add(value.TrimStart('/'));
                break;

            default:
                intents.Add("View");
                break;
        }

        return intents;
    }

    /// <summary>
    /// Reads an <c>/Order</c>: an array after a group holds the groups nested under it, and an array
    /// that starts with a string is a label over the rest of it (ISO 32000-1 Table 101).
    /// </summary>
    private sealed class OrderReader(Dictionary<object, OptionalContentGroup> groups)
    {
        private readonly HashSet<object> _open = new(ReferenceIdentity.Instance);
        private int _nodesLeft = MaximumOrderNodes;

        internal List<OptionalContentOrderNode> Read(PdfArray array) => Nodes(array, 0, 0);

        private List<OptionalContentOrderNode> Nodes(PdfArray array, int start, int depth)
        {
            var nodes = new List<OptionalContentOrderNode>();
            var key = ReferenceIdentity.KeyOf(array);
            if (depth >= MaximumOrderDepth || !_open.Add(key))
                return nodes;

            for (var index = start; index < array.Elements.Count && _nodesLeft > 0; index++)
            {
                if (array.Elements.GetDictionary(index) is { } dictionary)
                {
                    if (!groups.TryGetValue(ReferenceIdentity.KeyOf(dictionary), out var group))
                        continue;

                    _nodesLeft--;
                    var children = new List<OptionalContentOrderNode>();
                    if (index + 1 < array.Elements.Count && array.Elements.GetArray(index + 1) is { } nested)
                    {
                        index++;
                        children = Nodes(nested, 0, depth + 1);
                    }

                    nodes.Add(new OptionalContentOrderNode(null, group, children));
                }
                else if (array.Elements.GetArray(index) is { } subarray)
                {
                    string label = null;
                    if (subarray.Elements.Count > 0)
                        PdfItemValues.TryGetText(subarray.Elements[0], allowName: false, out label);

                    var members = Nodes(subarray, label != null ? 1 : 0, depth + 1);
                    if (label != null || members.Count > 0)
                    {
                        _nodesLeft--;
                        nodes.Add(new OptionalContentOrderNode(label, null, members));
                    }
                }
            }

            _open.Remove(key);
            return nodes;
        }
    }
}
