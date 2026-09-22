#region Copyright
//
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
using System.Collections;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a collection of outlines.
/// </summary>
public class PdfOutlineCollection : PdfObject, IList<PdfOutline>
{
    /// <summary>
    /// Can only be created as part of PdfOutline.
    /// </summary>
    internal PdfOutlineCollection(PdfDocument document, PdfOutline parent)
        : base(document)
    {
        _parent = parent;
    }

    /// <summary>
    /// The document of the entry this collection belongs to. Asked of the entry rather than
    /// recorded here, because a collection can be made before its entry is in a document - by
    /// reading <see cref="PdfOutline.Outlines"/> of an entry not yet added anywhere - and it has
    /// to follow the entry into the document it is added to.
    /// </summary>
    public override PdfDocument Owner => _parent.Owner;

    /// <summary>
    /// Removes the first occurrence of a specific item from the collection.
    /// </summary>
    public bool Remove(PdfOutline item)
    {
        if (_outlines.Remove(item))
        {
            RemoveFromOutlinesTree(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the number of entries in this collection.
    /// </summary>
    public int Count => _outlines.Count;

    /// <summary>
    /// Returns false.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds the specified outline.
    /// </summary>
    public void Add(PdfOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        // DestinationPage is optional. PDFsharp does not yet support outlines with action ("/A") instead of destination page ("/DEST")
        if (outline.DestinationPage != null && !ReferenceEquals(Owner, outline.DestinationPage.Owner))
            throw new ArgumentException("Destination page must belong to this document.");

        AddToOutlinesTree(outline);
        _outlines.Add(outline);

        // Nothing is counted here any more. This used to walk the new entry's ancestors putting
        // its Opened state into a running total, which recorded whichever value the entry happened
        // to carry at the moment it was added: an Opened set afterwards was never seen, and a
        // removal never took its contribution back. PdfOutline.VisibleDescendants reads the tree
        // at save time instead.
    }

    /// <summary>
    /// Removes all elements form the collection.
    /// </summary>
    public void Clear()
    {
        if (Count > 0)
        {
            var array = new PdfOutline[Count];
            _outlines.CopyTo(array);
            _outlines.Clear();
            foreach (var item in array)
            {
                RemoveFromOutlinesTree(item);
            }
        }
    }

    /// <summary>
    /// Determines whether the specified element is in the collection.
    /// </summary>
    public bool Contains(PdfOutline item)
    {
        return _outlines.Contains(item);
    }

    /// <summary>
    /// Copies the collection to an array, starting at the specified index of the target array.
    /// </summary>
    public void CopyTo(PdfOutline[] array, int arrayIndex)
    {
        _outlines.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Adds the specified outline entry.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    /// <param name="style">The font style used to draw the outline text.</param>
    /// <param name="textColor">The color used to draw the outline text.</param>
    public PdfOutline Add(string title, PdfPage destinationPage, bool opened, PdfOutlineStyle style, XColor textColor)
    {
        var outline = new PdfOutline(title, destinationPage, opened, style, textColor);
        Add(outline);
        return outline;
    }

    /// <summary>
    /// Adds the specified outline entry.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    /// <param name="style">The font style used to draw the outline text.</param>
    public PdfOutline Add(string title, PdfPage destinationPage, bool opened, PdfOutlineStyle style)
    {
        var outline = new PdfOutline(title, destinationPage, opened, style);
        Add(outline);
        return outline;
    }

    /// <summary>
    /// Adds the specified outline entry.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    public PdfOutline Add(string title, PdfPage destinationPage, bool opened)
    {
        var outline = new PdfOutline(title, destinationPage, opened);
        Add(outline);
        return outline;
    }

    /// <summary>
    /// Creates a PdfOutline and adds it into the outline collection.
    /// </summary>
    public PdfOutline Add(string title, PdfPage destinationPage)
    {
        var outline = new PdfOutline(title, destinationPage);
        Add(outline);
        return outline;
    }

    /// <summary>
    /// Gets the index of the specified item.
    /// </summary>
    public int IndexOf(PdfOutline item)
    {
        return _outlines.IndexOf(item);
    }

    /// <summary>
    /// Inserts the item at the specified index.
    /// </summary>
    public void Insert(int index, PdfOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);
        if (index < 0 || index >= _outlines.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.OutlineIndexOutOfRange);

        AddToOutlinesTree(outline);
        _outlines.Insert(index, outline);
    }

    /// <summary>
    /// Removes the outline item at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        var outline = _outlines[index];
        _outlines.RemoveAt(index);
        RemoveFromOutlinesTree(outline);
    }

    /// <summary>
    /// Gets the <see cref="PdfPinata.Pdf.PdfOutline"/> at the specified index.
    /// </summary>
    public PdfOutline this[int index]
    {
        get
        {
            if (index < 0 || index >= _outlines.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.OutlineIndexOutOfRange);
            return _outlines[index];
        }
        set
        {
            if (index < 0 || index >= _outlines.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.OutlineIndexOutOfRange);
            if (value == null)
                throw new ArgumentOutOfRangeException(nameof(value), null, PSSR.SetValueMustNotBeNull);

            var replaced = _outlines[index];
            if (ReferenceEquals(replaced, value))
                return;

            AddToOutlinesTree(value);
            _outlines[index] = value;
            // The entry replaced is out of the list as surely as a removed one, and has to be
            // free to be added somewhere else.
            RemoveFromOutlinesTree(replaced);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the outline collection.
    /// </summary>
    public IEnumerator<PdfOutline> GetEnumerator()
    {
        return _outlines.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Makes <paramref name="outline"/> an entry of this collection's parent, refusing everything
    /// that would give the tree an entry it cannot write. Nothing is changed until every check has
    /// passed.
    /// </summary>
    void AddToOutlinesTree(PdfOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        // An entry is written as a numbered object of the document, and an entry nobody has added
        // to a document has no document to be numbered in. This used to fail on a null reference.
        if (Owner == null)
            throw new InvalidOperationException(PSSR.OutlineParentNotInDocument);

        // DestinationPage is optional. PDFsharp does not yet support outlines with action ("/A") instead of destination page ("/DEST")
        if (outline.DestinationPage != null && !ReferenceEquals(Owner, outline.DestinationPage.Owner))
            throw new ArgumentException("Destination page must belong to this document.");

        if (outline.Owner != null && !ReferenceEquals(outline.Owner, Owner))
            throw new ArgumentException(PSSR.OutlineOfAnotherDocument, nameof(outline));

        // An entry has one /Parent and one place among its siblings. Added a second time it used
        // to end up in two lists - or twice in one, which wrote an entry whose /Next was itself -
        // with /Parent naming whichever came last, as PdfPages refuses a page already placed.
        if (outline.PlacedIn != null)
            throw new InvalidOperationException(PSSR.OutlineAlreadyPlaced);

        // Nor can an entry be put under itself or under anything below it: the tree would then be
        // a loop that the save walked down until the stack ran out. The walk goes by PlacedIn,
        // which only this class sets and which this check keeps free of loops.
        for (var ancestor = _parent; ancestor != null; ancestor = ancestor.PlacedIn?._parent)
        {
            if (ReferenceEquals(ancestor, outline))
                throw new InvalidOperationException(PSSR.OutlinePlacedUnderItself);
        }

        outline.Document = Owner;
        outline.Parent = _parent;
        outline.PlacedIn = this;

        // An entry removed and added again may have been away across a save, which numbers the
        // objects from one again and can have given its number to another object.
        Owner._irefTable.Readmit(outline);
    }

    /// <summary>
    /// Takes <paramref name="outline"/> out of the tree, leaving it free to be added again here or
    /// anywhere else in the document. The entries under it stay under it and are written again if
    /// it is.
    /// </summary>
    void RemoveFromOutlinesTree(PdfOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        outline.Parent = null;
        outline.PlacedIn = null;

        // Its links to the entries beside it are rewritten when it is next saved in a list, and
        // the entries beside it have theirs rewritten now it is gone - PdfOutline.PrepareForSave
        // writes every one of them or removes it.
        Owner._irefTable.Remove(outline.Reference);
    }

    /// <summary>
    /// The parent outline of this collection.
    /// </summary>
    readonly PdfOutline _parent;

    readonly List<PdfOutline> _outlines = new();
}
