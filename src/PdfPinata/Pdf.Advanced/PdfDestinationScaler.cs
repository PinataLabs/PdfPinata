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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Moves the destinations that point at a resized page, so that a link that went to the middle
/// of a page still goes to the middle of it afterwards.
/// <para>
/// This is the expensive half of a resize, and the reason a resize is a document-wide operation
/// rather than a page-wide one. A destination that names a page is held wherever somebody wanted
/// to link from: the annotations of any page, the outline tree, the name tree of the catalog,
/// the /Dests dictionary that PDF 1.1 used, or the action the document opens with. All of them
/// have to be looked at, because the page itself carries no list of who points at it.
/// </para>
/// <para>
/// The zoom of an /XYZ destination is deliberately not touched. It is a magnification the reader
/// asked for, not a promise about how big the text will be: a document scaled up should show
/// larger text when a link is followed, which is the whole reason for scaling it up, and scaling
/// the zoom the other way to hold the apparent size steady would undo that at the very moment
/// the reader arrives.
/// </para>
/// </summary>
internal static class PdfDestinationScaler
{
    /// <summary>
    /// Moves every destination of the document that points at one of the pages given.
    /// </summary>
    /// <param name="document">The document to sweep.</param>
    /// <param name="matrices">
    /// The transform each resized page moved under, by page. Pages absent from this are not
    /// resized and the destinations pointing at them are left exactly as they are.
    /// </param>
    internal static void Scale(PdfDocument document, IDictionary<PdfPage, XMatrix> matrices)
    {
        if (document == null || matrices == null || matrices.Count == 0)
            return;

        var byObjectId = ByObjectId(matrices);
        if (byObjectId.Count == 0)
            return;

        // A destination array can be indirect and pointed at from more than one link. Moving one
        // twice would move it twice as far, so each is done once however often it is found.
        var sweep = new Sweep(byObjectId);

        foreach (var page in document.Pages)
            sweep.VisitAnnotationsOf(page);

        var catalog = document.Catalog;
        if (catalog != null)
            VisitCatalog(sweep, catalog);
    }

    /// <summary>
    /// The matrices keyed by the object ID of their page, leaving out a page that is not an
    /// indirect object and so cannot be a destination.
    /// </summary>
    private static Dictionary<PdfObjectID, XMatrix> ByObjectId(IDictionary<PdfPage, XMatrix> matrices)
    {
        var byObjectId = new Dictionary<PdfObjectID, XMatrix>();
        foreach (var pair in matrices)
        {
            if (pair.Key?.Reference != null)
                byObjectId[pair.Key.Reference.ObjectID] = pair.Value;
        }

        return byObjectId;
    }

    /// <summary>
    /// Every place the catalog holds a destination: the outline, the name tree, the PDF 1.1
    /// /Dests dictionary and the action the document opens with.
    /// </summary>
    private static void VisitCatalog(Sweep sweep, PdfCatalog catalog)
    {
        sweep.VisitOutline(catalog.Elements.GetDictionary(PdfCatalog.Keys.Outlines), 0);

        var names = catalog.Elements.GetDictionary(PdfCatalog.Keys.Names);
        if (names != null)
            sweep.VisitNameTree(names.Elements.GetDictionary("/Dests"));

        var dests = catalog.Elements.GetDictionary(PdfCatalog.Keys.Dests);
        if (dests != null)
        {
            foreach (var key in dests.Elements.KeyNames)
                sweep.VisitDestinationHolder(dests, key.Value);
        }

        sweep.VisitDestinationHolder(catalog, PdfCatalog.Keys.OpenAction);
    }

    private sealed class Sweep
    {
        private readonly Dictionary<PdfObjectID, XMatrix> _matrices;
        private readonly HashSet<PdfArray> _done = new(ByIdentity.Instance);
        private readonly HashSet<PdfDictionary> _outlineItemsSeen = [];

        internal Sweep(Dictionary<PdfObjectID, XMatrix> matrices)
        {
            _matrices = matrices;
        }

        /// <summary>
        /// The destinations of every annotation of a page. A link either carries its destination
        /// under /Dest or performs an action that holds one under /D.
        /// </summary>
        internal void VisitAnnotationsOf(PdfPage page)
        {
            // The element rather than the property: reading page.Annotations would give a page
            // without any an empty array to hold.
            var item = Resolve(page.Elements[PdfPage.Keys.Annots]);
            if (item is not PdfArray annotations)
                return;

            foreach (var element in annotations.Elements)
            {
                if (Resolve(element) is PdfDictionary annotation)
                    VisitHolderAndItsAction(annotation, "/Dest");
            }
        }

        internal void VisitOutline(PdfDictionary node, int depth)
        {
            // Each item is entered once. The two caps bound how deep and how wide the walk goes, but
            // not their product: an item that is its own first child and its own next sibling is
            // MaxSiblings visits at each of MaxDepth levels, one inside another. The caps stay for
            // the honest outline that is merely absurdly large.
            if (node == null || depth > MaxDepth || !_outlineItemsSeen.Add(node))
                return;

            VisitHolderAndItsAction(node, "/Dest");

            var child = node.Elements.GetDictionary("/First");
            var guard = 0;
            // A /Next chain that comes back round stops at the first item it has already entered.
            while (child != null && guard++ <= MaxSiblings && !_outlineItemsSeen.Contains(child))
            {
                VisitOutline(child, depth + 1);
                child = child.Elements.GetDictionary("/Next");
            }
        }

        /// <summary>
        /// Every destination held in a name tree, which is where PDF 1.2 onwards keeps the ones
        /// that are named rather than stated.
        /// </summary>
        /// <remarks>
        /// Walked by <see cref="PdfNameTree.Walk"/>, which enters each node once. A depth cap alone
        /// does not stop a node that names itself twice among its /Kids from doubling the walk at
        /// every level, and this ran during a page resize.
        /// </remarks>
        internal void VisitNameTree(PdfDictionary root)
        {
            foreach (var entry in PdfNameTree.Walk(root, 0, []))
                VisitDestination(entry.Value);
        }

        /// <summary>
        /// A destination held directly under the key, and one held by the action under /A.
        /// </summary>
        internal void VisitHolderAndItsAction(PdfDictionary holder, string key)
        {
            VisitDestinationHolder(holder, key);

            var action = holder.Elements.GetDictionary("/A");
            if (action == null)
                return;

            // Only a go-to action goes to a page of this document. A /GoToR names a page of
            // another file, where the numbers mean what they say and this resize has no business
            // touching them. An action that does not say what it is is taken to be a go-to,
            // which is how the import path treats one too.
            var subtype = action.Elements.GetName("/S");
            if (subtype.Length == 0 || subtype == "/GoTo")
                VisitDestinationHolder(action, "/D");
        }

        internal void VisitDestinationHolder(PdfDictionary holder, string key)
        {
            if (holder == null)
                return;

            VisitDestination(holder.Elements[key]);
        }

        /// <summary>
        /// Moves one destination, wherever it was found and however it was written down. A named
        /// destination is not followed from here: the name tree and the /Dests dictionary that
        /// hold what the names stand for are swept in their own right, so following the name too
        /// would find the same array a second time.
        /// </summary>
        private void VisitDestination(PdfItem item)
        {
            item = Resolve(item);

            // A destination is either the array or a dictionary holding it under /D.
            if (item is PdfDictionary dictionary)
                item = Resolve(dictionary.Elements["/D"]);

            if (item is not PdfArray destination || destination.Elements.Count < 2)
                return;

            if (!_done.Add(destination))
                return;

            // The page it goes to, which has to be one that moved.
            if (destination.Elements[0] is not PdfReference page)
                return;

            if (!_matrices.TryGetValue(page.ObjectID, out var matrix))
                return;

            Move(destination, matrix);
        }

        private static void Move(PdfArray destination, XMatrix matrix)
        {
            switch (destination.Elements.GetName(1))
            {
                case "/XYZ":
                    MoveXyz(destination, matrix);
                    break;

                case "/FitR":
                    MoveRectangle(destination, matrix);
                    break;

                case "/FitH":
                case "/FitBH":
                    MoveHorizontalLine(destination, matrix);
                    break;

                case "/FitV":
                case "/FitBV":
                    MoveVerticalLine(destination, matrix);
                    break;

                // /Fit and /FitB say "show the whole page" and carry no coordinates, so there is
                // nothing in them to move. Anything else is not a destination form this knows,
                // and leaving it alone is better than guessing at what its numbers mean.
            }
        }

        /// <summary>
        /// [page /XYZ left top zoom]: a corner to put at the top left of the window, and a
        /// magnification. The corner moves; the magnification does not.
        /// </summary>
        private static void MoveXyz(PdfArray destination, XMatrix matrix)
        {
            if (destination.Elements.Count < 4)
                return;

            var hasLeft = PdfItemValues.TryGetNumber(destination.Elements[2], out var left);
            var hasTop = PdfItemValues.TryGetNumber(destination.Elements[3], out var top);

            if (hasLeft && hasTop)
            {
                var moved = matrix.Transform(new XPoint(left, top));
                destination.Elements[2] = new PdfReal(moved.X);
                destination.Elements[3] = new PdfReal(moved.Y);
                return;
            }

            // Either coordinate may be null, meaning "leave this one where the reader has it".
            // One on its own can only be moved when the axes have not been swapped around, which
            // is to say when the other coordinate makes no difference to it.
            if (hasLeft && IsAxisAligned(matrix))
                destination.Elements[2] = new PdfReal(TransformX(left, matrix));

            if (hasTop && IsAxisAligned(matrix))
                destination.Elements[3] = new PdfReal(TransformY(top, matrix));
        }

        /// <summary>
        /// [page /FitR left bottom right top]: a rectangle to fit the window to.
        /// </summary>
        private static void MoveRectangle(PdfArray destination, XMatrix matrix)
        {
            if (destination.Elements.Count < 6)
                return;

            var corners = new double[4];
            for (var index = 0; index < 4; index++)
            {
                if (!PdfItemValues.TryGetNumber(destination.Elements[index + 2], out corners[index]))
                    return;
            }

            var left = corners[0];
            var bottom = corners[1];
            var right = corners[2];
            var top = corners[3];

            var moved = PdfPageResizer.Transformed(
                new XRect(Math.Min(left, right), Math.Min(bottom, top),
                    Math.Abs(right - left), Math.Abs(top - bottom)),
                matrix);

            destination.Elements[2] = new PdfReal(moved.X);
            destination.Elements[3] = new PdfReal(moved.Y);
            destination.Elements[4] = new PdfReal(moved.X + moved.Width);
            destination.Elements[5] = new PdfReal(moved.Y + moved.Height);
        }

        /// <summary>
        /// [page /FitH top]: a horizontal line to bring to the top of the window. A quarter turn
        /// makes it a vertical line, and the destination has to change form to say so.
        /// </summary>
        private static void MoveHorizontalLine(PdfArray destination, XMatrix matrix)
        {
            if (destination.Elements.Count < 3 ||
                !PdfItemValues.TryGetNumber(destination.Elements[2], out var value))
                return;

            if (IsAxisAligned(matrix))
            {
                destination.Elements[2] = new PdfReal(TransformY(value, matrix));
                return;
            }

            // Turned: the line y = value becomes the line x = M21 * value + OffsetX, which no
            // longer depends on y at all, so it is exactly a /FitV.
            destination.Elements[1] = new PdfName(Turned(destination.Elements.GetName(1)));
            destination.Elements[2] = new PdfReal(value * matrix.M21 + matrix.OffsetX);
        }

        /// <summary>
        /// [page /FitV left]: a vertical line to bring to the left of the window.
        /// </summary>
        private static void MoveVerticalLine(PdfArray destination, XMatrix matrix)
        {
            if (destination.Elements.Count < 3 ||
                !PdfItemValues.TryGetNumber(destination.Elements[2], out var value))
                return;

            if (IsAxisAligned(matrix))
            {
                destination.Elements[2] = new PdfReal(TransformX(value, matrix));
                return;
            }

            destination.Elements[1] = new PdfName(Turned(destination.Elements.GetName(1)));
            destination.Elements[2] = new PdfReal(value * matrix.M12 + matrix.OffsetY);
        }

        /// <summary>
        /// The destination form that means the same thing about the other axis.
        /// </summary>
        private static string Turned(string form)
        {
            return form switch
            {
                "/FitH" => "/FitV",
                "/FitV" => "/FitH",
                "/FitBH" => "/FitBV",
                "/FitBV" => "/FitBH",
                _ => form
            };
        }

        /// <summary>
        /// Whether the transform leaves the axes where they were, so that an x depends only on
        /// an x and a y only on a y. True of every resize but a turned one.
        /// </summary>
        private static bool IsAxisAligned(XMatrix matrix)
        {
            return Math.Abs(matrix.M12) < 1e-9 && Math.Abs(matrix.M21) < 1e-9;
        }

        private static double TransformX(double x, XMatrix matrix)
        {
            return x * matrix.M11 + matrix.OffsetX;
        }

        private static double TransformY(double y, XMatrix matrix)
        {
            return y * matrix.M22 + matrix.OffsetY;
        }

        private static PdfItem Resolve(PdfItem item)
        {
            return item is PdfReference reference ? reference.Value : item;
        }

        private const int MaxDepth = 32;

        /// <summary>
        /// How many outline entries to follow along one level before giving up. A circle of /Next
        /// entries is stopped sooner, at the first entry already entered.
        /// </summary>
        private const int MaxSiblings = 100000;
    }

    /// <summary>
    /// Tells two destination arrays apart by which object they are rather than by what they hold,
    /// so that two links going to the same place are not mistaken for the same array.
    /// </summary>
    private sealed class ByIdentity : IEqualityComparer<PdfArray>
    {
        internal static readonly ByIdentity Instance = new();

        public bool Equals(PdfArray x, PdfArray y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(PdfArray obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
