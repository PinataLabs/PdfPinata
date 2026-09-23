#region Copyright

//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using PinataLayout.DocumentObjectModel.Fields;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Field information used to fill fields when rendering or formatting.
/// </summary>
internal class FieldInfos
{
    internal FieldInfos(Dictionary<string, BookmarkInfo> bookmarks)
    {
        this.bookmarks = bookmarks;
    }

    internal struct BookmarkInfo
    {
        internal BookmarkInfo(int physicalPageNumber, int displayPageNumber, double top)
        {
            this.displayPageNumber = physicalPageNumber;
            shownPageNumber = displayPageNumber;
            this.top = top;
        }

        internal int displayPageNumber;
        internal int shownPageNumber;

        /// <summary>
        /// How far up the page the bookmark sits, in the coordinates a PDF page is measured in.
        /// NaN when the page height was not known and the position could not be worked out.
        /// </summary>
        internal double top;
    }

    internal void AddBookmark(string name, XUnit verticalPosition)
    {
        if (pyhsicalPageNr <= 0)
            return;

        bookmarks.Remove(name);

        // A document is laid out from the top of the page down and a PDF page is measured from the
        // bottom up, so the one has to be turned into the other before it can be a destination.
        var top = pageHeight.Point > 0 ? pageHeight.Point - verticalPosition.Point : double.NaN;
        bookmarks.Add(name, new BookmarkInfo(pyhsicalPageNr, displayPageNr, top));
    }

    /// <summary>
    /// How far up its page the named bookmark sits, in the coordinates a PDF page is measured in,
    /// or NaN when there is no such bookmark or its position is not known.
    /// </summary>
    internal double GetBookmarkTop(string bookmarkName)
    {
        return bookmarks.TryGetValue(bookmarkName, out var bi) ? bi.top : double.NaN;
    }

    internal int GetShownPageNumber(string bookmarkName)
    {
        if (bookmarks.TryGetValue(bookmarkName, out var bi))
            return bi.shownPageNumber;
        return -1;
    }

    /// <summary>
    /// The same facts, in the shape <see cref="FieldEvaluator"/> asks for them. The translation lives
    /// here, beside what is being translated: a count of zero is this class's way of saying the count
    /// is not known yet, and the evaluator would rather be told that in nulls than have to know it.
    /// </summary>
    internal FieldEvaluationContext ToEvaluationContext()
    {
        return new FieldEvaluationContext
        {
            DisplayPageNumber = displayPageNr,
            SectionNumber = section,
            NumberOfPages = numPages > 0 ? numPages : null,
            PagesInSection = sectionPages > 0 ? sectionPages : null,
            PrintDate = date,
            ResolveBookmarkPage = name =>
            {
                var shownPageNumber = GetShownPageNumber(name);
                return shownPageNumber > 0 ? shownPageNumber : null;
            }
        };
    }

    internal int GetPhysicalPageNumber(string bookmarkName)
    {
        if (bookmarks.TryGetValue(bookmarkName, out var bi))
            return bi.displayPageNumber;
        return -1;
    }

    private readonly Dictionary<string, BookmarkInfo> bookmarks;
    internal int displayPageNr;
    internal int pyhsicalPageNr;

    /// <summary>
    /// The height of the page these infos belong to, which is what turns a distance down the page
    /// into a distance up it. Zero when it has not been set, and then no bookmark carries a position.
    /// </summary>
    internal XUnit pageHeight;

    internal int section;
    internal int sectionPages;
    internal int numPages;
    internal DateTime date;
}
