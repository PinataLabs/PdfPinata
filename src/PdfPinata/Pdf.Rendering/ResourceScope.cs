using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The resources the names in a content stream are looked up in: a page's, or a form XObject's.
/// </summary>
internal sealed class ResourceScope
{
    private readonly PdfDictionary _resources;

    private ResourceScope(PdfDictionary resources)
    {
        _resources = resources;
    }

    /// <summary>The resources of a page, which may be none.</summary>
    internal static ResourceScope OfPage(PdfPage page) =>
        new(page.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources));

    /// <summary>
    /// The resources of a form drawn from this scope: its own where it has them, and otherwise
    /// those of whatever drew it, which is how forms were written before they carried their own.
    /// </summary>
    internal ResourceScope ForForm(PdfDictionary form)
    {
        var own = form.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources);
        return own == null ? this : new ResourceScope(own);
    }

    /// <summary>The dictionary a name stands for in a category such as <c>/Font</c>, or null.</summary>
    internal PdfDictionary Lookup(string category, string name) =>
        _resources?.Elements.GetDictionary(category)?.Elements.GetDictionary(name);

    /// <summary>
    /// What a name stands for in a category whose entries need not be dictionaries, such as
    /// <c>/ColorSpace</c>, where one may be a name or an array. Null when there is none.
    /// </summary>
    internal PdfItem LookupItem(string category, string name)
    {
        var item = PdfReference.Dereference(_resources?.Elements.GetDictionary(category)?.Elements[name]);
        return PdfItemValues.IsNull(item) ? null : item;
    }
}
