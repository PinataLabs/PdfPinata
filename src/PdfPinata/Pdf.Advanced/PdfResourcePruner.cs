using System;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Works out which entries of a page's resource dictionary the page draws with, by reading the
/// content of the page and of everything the content draws.
/// <para>
/// A page carries the resources it was given, which is not the same as the resources it uses:
/// pages of a document commonly share one dictionary naming every font and image in it. Copying
/// such a page copies them all, which is what made a document split into one file per page give
/// every file the weight of the whole document.
/// </para>
/// <para>
/// A page that comes out heavier than it needs to be is a nuisance; a page that comes out
/// missing a font is broken. So anything about the content that is not understood in full
/// leaves the page exactly as it was.
/// </para>
/// <para>
/// The reading itself — following forms, soft masks, Type 3 char procs and annotation appearances —
/// is <see cref="PdfPageWalk"/>'s, shared with <see cref="PdfPageResourceUsage"/>, which asks the
/// same walk what a page uses rather than what a shared dictionary can be pruned to. What is this
/// class's own is deciding which names survive in the page's own resource dictionary.
/// </para>
/// </summary>
internal sealed class PdfResourcePruner : PdfPageWalk
{
    /// <summary>The categories of a resource dictionary whose entries content names.</summary>
    static readonly string[] Categories =
    [
        "/XObject", "/Font", "/ExtGState", "/Shading", "/Pattern", "/ColorSpace", "/Properties"
    ];

    /// <summary>
    /// Drops the entries of the page's resource dictionary that the page does not draw with.
    /// </summary>
    internal static void Prune(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var resources = page.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources);
        if (resources == null)
            return;

        var pruner = new PdfResourcePruner(resources);
        pruner.ReadPage(page);
        if (!pruner.Understood)
            return;

        pruner.Rewrite(page, resources);
    }

    PdfResourcePruner(PdfDictionary pageResources) : base(pageResources)
    {
    }

    /// <summary>The names the page draws with, by category.</summary>
    readonly Dictionary<string, Dictionary<string, object>> _used = new();

    protected override void RecordUse(string category, string name, PdfDictionary scope)
    {
        if (ReferenceEquals(scope, PageResources))
            Record(category, name);
    }

    void Record(string category, string name)
    {
        Dictionary<string, object> names;
        if (!_used.TryGetValue(category, out names))
            _used[category] = names = new Dictionary<string, object>();

        names[name] = null;
    }

    bool IsUsed(string category, string name)
    {
        Dictionary<string, object> names;
        return _used.TryGetValue(category, out names) && names.ContainsKey(name);
    }

    #region Rewriting

    /// <summary>
    /// Puts a resource dictionary on the page holding only what it draws with. The dictionary it
    /// had is left untouched, being in all likelihood the one the other pages carry as well.
    /// </summary>
    void Rewrite(PdfPage page, PdfDictionary resources)
    {
        var pruned = new PdfResources(page.Owner);
        var anythingDropped = false;

        foreach (var key in resources.Elements.KeyNames)
        {
            var entries = Array.IndexOf(Categories, key.Value) < 0
                ? null
                : resources.Elements.GetDictionary(key.Value);

            if (entries == null)
            {
                // Not a category of named resources, or not written as one. Kept as it stands.
                pruned.Elements[key.Value] = resources.Elements[key.Value];
                continue;
            }

            var kept = new PdfDictionary(page.Owner);
            foreach (var name in entries.Elements.KeyNames)
            {
                if (IsUsed(key.Value, name.Value))
                    kept.Elements[name.Value] = entries.Elements[name.Value];
                else
                    anythingDropped = true;
            }

            if (kept.Elements.Count > 0)
                pruned.Elements[key.Value] = kept;
        }

        // Leaving the page alone where there was nothing to drop keeps a document that gains
        // nothing from this unchanged by it.
        if (!anythingDropped)
            return;

        // An object of its own, as the dictionary it stands in for is in all likelihood one the
        // other pages carry as well, and must be left as it is for them.
        page.Owner._irefTable.Add(pruned);
        page.ReplaceResources(pruned);
    }

    #endregion
}
