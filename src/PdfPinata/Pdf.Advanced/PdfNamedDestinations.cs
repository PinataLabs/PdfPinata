#region Copyright
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Looks up the destinations a document holds by name.
/// <para>
/// A link can name where it goes rather than say it outright, leaving the document catalog to
/// hold what the name stands for. There are two places to look: the name tree under the /Names
/// entry of the catalog, which is where PDF 1.2 onwards puts them and where the name is written
/// as a string, and the /Dests dictionary of the catalog, which is where PDF 1.1 put them and
/// where the name is written as a name.
/// </para>
/// </summary>
internal static class PdfNamedDestinations
{
    /// <summary>
    /// The destination the document holds under the name given, or null if it holds none. The
    /// array returned belongs to the document searched, so a caller importing it has to copy it.
    /// </summary>
    internal static PdfArray Lookup(PdfDocument document, PdfItem name)
    {
        if (document == null || name == null)
            return null;

        var text = TextOf(name);
        if (text == null)
            return null;

        var catalog = document.Catalog;
        if (catalog == null)
            return null;

        PdfItem found = null;

        var names = catalog.Elements.GetDictionary("/Names");
        if (names != null)
            found = Search(names.Elements.GetDictionary("/Dests"), text, 0, []);

        if (found == null)
        {
            // The keys of a dictionary carry the slash a name is written with.
            var dests = catalog.Elements.GetDictionary("/Dests");
            if (dests != null)
                found = dests.Elements["/" + text];
        }

        return DestinationOf(found);
    }

    /// <summary>
    /// Searches a node of a name tree, and the nodes below it, for the name given.
    /// </summary>
    /// <remarks>
    /// <paramref name="seen"/> is what bounds the work, not <paramref name="depth"/>: a node listing
    /// itself twice among its kids doubles the search at every level, so the depth cap alone lets a
    /// tiny malformed document cost billions of visits. A name tree is a tree, so a node reached twice
    /// holds nothing the first visit did not already look at. See <see cref="PdfNameTree.MaxDepth"/>.
    /// </remarks>
    private static PdfItem Search(PdfDictionary node, string name, int depth, HashSet<PdfDictionary> seen)
    {
        if (node == null || depth > MaxDepth || !seen.Add(node))
            return null;

        // A node says which names lie below it, so one the name is outside can be passed over.
        // Anything unreadable about the bounds leaves the node to be searched rather than
        // skipped: searching one node too many costs time, skipping one loses the destination.
        var limits = node.Elements.GetArray("/Limits");
        if (limits != null && limits.Elements.Count == 2)
        {
            var low = TextOf(limits.Elements[0]);
            var high = TextOf(limits.Elements[1]);
            if (low != null && high != null &&
                (string.CompareOrdinal(name, low) < 0 || string.CompareOrdinal(name, high) > 0))
                return null;
        }

        // A leaf holds the names themselves, alternating with what each one stands for.
        var leaves = node.Elements.GetArray("/Names");
        if (leaves != null)
        {
            var count = leaves.Elements.Count;
            for (var idx = 0; idx + 1 < count; idx += 2)
            {
                if (TextOf(leaves.Elements[idx]) == name)
                    return leaves.Elements[idx + 1];
            }
        }

        var kids = node.Elements.GetArray("/Kids");
        if (kids != null)
        {
            var count = kids.Elements.Count;
            for (var idx = 0; idx < count; idx++)
            {
                var found = Search(kids.Elements.GetDictionary(idx), name, depth + 1, seen);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Every name the document holds in its /Names /Dests tree, with what it stands for, as it is
    /// written rather than resolved. The legacy /Dests dictionary is not walked: a document being
    /// rewritten gets a name tree, and putting the old dictionary's names into it would move them.
    /// </summary>
    internal static IEnumerable<KeyValuePair<string, PdfItem>> Enumerate(PdfDocument document)
    {
        return PdfNameTree.Enumerate(document, "/Dests");
    }

    /// <summary>
    /// The destination an entry of the catalog stands for. It is either the array itself or a
    /// dictionary holding it under /D, and either of them can be held indirectly.
    /// </summary>
    private static PdfArray DestinationOf(PdfItem item)
    {
        if (item is PdfReference iref)
            item = iref.Value;

        if (item is PdfArray array)
            return array;

        return item is not PdfDictionary dictionary ? null : dictionary.Elements.GetArray("/D");
    }

    /// <summary>
    /// The text of a name written either as a string or as a name, without the slash a name
    /// carries, or null if the item is neither.
    /// </summary>
    private static string TextOf(PdfItem item) => PdfNameTree.TextOf(item);

    /// <summary>
    /// How far down a name tree to go before giving up on it. The same cap the shared walk uses,
    /// and for the same reason.
    /// </summary>
    private const int MaxDepth = PdfNameTree.MaxDepth;
}
