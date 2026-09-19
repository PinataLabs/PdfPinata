using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Says whether an XObject about to be drawn on a page paints with transparency, so that a page
/// which needs a transparency group is given one and a page whose content is opaque throughout is
/// left without.
/// <para>
/// What is read is what an XObject has available to paint with rather than what it can be proved
/// to paint with: an alpha graphics state named in a resource dictionary but never set by the
/// content counts as transparency here. Settling it exactly would mean decoding the content
/// stream of every form drawn on the page, and of every form drawn within those, to decide a
/// single flag. The error only ever falls one way - a page is given a group it did not strictly
/// need, which is what every page used to get - so the cost of being approximate is a few bytes
/// and the cost of decoding everything is paid on every save.
/// </para>
/// </summary>
static class PdfTransparencyDetector
{
    /// <summary>
    /// How deep forms may be drawn within one another before this stops looking. Well past
    /// anything a real document does, and there to stop a malformed one running away.
    /// </summary>
    const int MaximumDepth = 32;

    /// <summary>
    /// Whether the XObject paints with transparency.
    /// </summary>
    internal static bool UsesTransparency(PdfDictionary xObject)
    {
        return xObject != null && Paints(xObject, new Dictionary<string, object>(), 0);
    }

    static bool Paints(PdfDictionary xObject, Dictionary<string, object> seen, int depth)
    {
        // Deeper than this is a document that is either malformed or beyond understanding, and
        // the safe answer to "does this use transparency" is yes: a group that was not needed
        // costs a few bytes, and a missing one costs the appearance of the page.
        if (depth > MaximumDepth)
            return true;

        if (!MarkAsSeen(xObject, seen))
        {
            // Either a form drawn within itself, or one reached twice by different routes. The
            // first visit is the one that answers for it, and a yes there would have stopped the
            // search before it came back round to here.
            return false;
        }

        if (xObject.Elements.GetName("/Subtype") == "/Image")
            return ImagePaints(xObject);

        // A form declaring a transparency group of its own says so outright.
        var group = xObject.Elements.GetDictionary(PdfPage.Keys.Group);
        if (group != null && group.Elements.GetName("/S") == "/Transparency")
            return true;

        return ResourcesPaint(xObject.Elements.GetDictionary(PdfPage.Keys.Resources), seen, depth);
    }

    /// <summary>
    /// Whether an image carries per-pixel alpha.
    /// <para>
    /// A stencil or colour-key <c>/Mask</c> is deliberately not counted. It leaves pixels
    /// unpainted rather than blending them, predates transparency altogether, and needs no group
    /// to render the way it was meant to.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Internal rather than private: <c>Metadata.PdfResourceConformanceRules</c> asks the same
    /// question of an image a page-resource walk found, and answering it twice would let the two
    /// definitions of "transparent" drift apart.
    /// </remarks>
    internal static bool ImagePaints(PdfDictionary image)
    {
        if (IsSomethingOtherThanNone(image.Elements["/SMask"]))
            return true;

        // A JPEG 2000 image can carry its alpha within the image data instead of beside it.
        return image.Elements.GetInteger("/SMaskInData") != 0;
    }

    static bool ResourcesPaint(PdfDictionary resources, Dictionary<string, object> seen, int depth)
    {
        if (depth > MaximumDepth)
            return true;

        if (resources == null)
            return false;

        var states = resources.Elements.GetDictionary("/ExtGState");
        if (states != null)
        {
            foreach (var name in states.Elements.KeyNames)
            {
                if (StatePaints(states.Elements.GetDictionary(name.Value)))
                    return true;
            }
        }

        var xObjects = resources.Elements.GetDictionary("/XObject");
        if (xObjects != null)
        {
            foreach (var name in xObjects.Elements.KeyNames)
            {
                var nested = xObjects.Elements.GetDictionary(name.Value);
                if (nested != null && Paints(nested, seen, depth + 1))
                    return true;
            }
        }

        // A tiling pattern paints from a content stream of its own and carries its own resources.
        // A shading pattern has none, so there is nothing under it to look at.
        var patterns = resources.Elements.GetDictionary("/Pattern");
        if (patterns != null)
        {
            foreach (var name in patterns.Elements.KeyNames)
            {
                var pattern = patterns.Elements.GetDictionary(name.Value);
                if (pattern == null)
                    continue;

                if (ResourcesPaint(pattern.Elements.GetDictionary(PdfPage.Keys.Resources), seen, depth + 1))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a graphics state paints anything other than opaquely: less than full alpha, a
    /// blend mode that reads what is underneath, or a soft mask.
    /// </summary>
    /// <remarks>
    /// Internal for the same reason <see cref="ImagePaints"/> is — shared with
    /// <c>Metadata.PdfResourceConformanceRules</c> rather than answered a second way.
    /// </remarks>
    internal static bool StatePaints(PdfDictionary state)
    {
        if (state == null)
            return false;

        if (AlphaOf(state, PdfExtGState.Keys.ca) < 1 || AlphaOf(state, PdfExtGState.Keys.CA) < 1)
            return true;

        if (BlendsWithTheBackdrop(state.Elements[PdfExtGState.Keys.BM]))
            return true;

        return IsSomethingOtherThanNone(state.Elements[PdfExtGState.Keys.SMask]);
    }

    /// <summary>
    /// The alpha a graphics state sets, or 1 where it sets none - a state that says nothing about
    /// alpha leaves it as it was, and what it was is opaque until something says otherwise.
    /// </summary>
    static double AlphaOf(PdfDictionary state, string key)
    {
        // Not GetReal: it answers 0 for a key that is absent, which would read every graphics
        // state in the document as fully transparent. TryNumber also follows a reference, which
        // a number in a dictionary is allowed to be.
        return PdfPageResizer.TryNumber(state.Elements[key], out var alpha) ? alpha : 1;
    }

    /// <summary>
    /// Whether the blend mode composites with what is already on the page. Normal and its PDF 1.3
    /// spelling Compatible paint over it, which needs no group; everything else reads it.
    /// </summary>
    static bool BlendsWithTheBackdrop(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfName name)
            return name.Value != "/Normal" && name.Value != "/Compatible";

        // An array is a list of blend modes in order of preference, the first one the reader
        // knows winning. Any of them being a real blend is enough to want a group.
        if (item is PdfArray array)
        {
            foreach (var element in array.Elements)
            {
                if (BlendsWithTheBackdrop(element))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the item is present and is not the name <c>/None</c>. Both soft mask entries read
    /// the same way: absent leaves the mask as it was, <c>/None</c> turns it off, and anything
    /// else is a mask.
    /// </summary>
    static bool IsSomethingOtherThanNone(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        // A PDF null is how a writer says a key holds nothing, whether it is written into the
        // dictionary itself or reached through a reference, and it reads as an absent key does.
        if (item == null || item is PdfNull || item is PdfNullObject)
            return false;

        return !(item is PdfName name && name.Value == "/None");
    }

    /// <summary>
    /// Notes that the XObject has been looked at, and says whether it had not been looked at
    /// already.
    /// </summary>
    static bool MarkAsSeen(PdfDictionary xObject, Dictionary<string, object> seen)
    {
        // A direct object cannot be shared and so cannot be drawn within itself.
        if (!xObject.IsIndirect)
            return true;

        var id = xObject.ObjectID.ToString();
        if (seen.ContainsKey(id))
            return false;

        seen[id] = null;
        return true;
    }
}
