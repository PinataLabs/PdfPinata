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
using System.Diagnostics;
using System.Collections;
using JetBrains.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents the pages of the document.
/// </summary>
[DebuggerDisplay("(PageCount={Count})")]
public sealed class PdfPages : PdfDictionary, IEnumerable<PdfPage>
{
    internal PdfPages(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Pages");
        Elements[Keys.Count] = new PdfInteger(0);
    }

    internal PdfPages(PdfDictionary dictionary)
        : base(dictionary)
    { }

    /// <summary>
    /// Gets the number of pages.
    /// </summary>
    public int Count => PagesArray.Elements.Count;

    /// <summary>
    /// Gets the page with the specified index.
    /// </summary>
    public PdfPage this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, PSSR.PageIndexOutOfRange);

            var dict = (PdfDictionary)((PdfReference)PagesArray.Elements[index]).Value;
            if (!(dict is PdfPage))
                dict = new PdfPage(dict);
            return (PdfPage)dict;
        }
    }

    /// <summary>
    /// Refuses a mutation of the page tree when the document it belongs to is not open for
    /// modification.
    /// <para>
    /// This is the only place the check is made for the page tree. <see cref="PdfDocument"/>'s
    /// page methods are one-line forwarders into the methods below, so the check they used to
    /// repeat each for themselves lives here instead - once per operation rather than once per
    /// forwarder, and now covering the callers who reach the page tree through
    /// <see cref="PdfDocument.Pages"/> rather than through those forwarders.
    /// </para>
    /// <para>
    /// The methods that delegate - <see cref="Add()"/> and <see cref="Insert(int)"/> and their
    /// overloads - ask again before delegating, which is redundant as a check and is not there as
    /// one. It is there so that a caller who wrote <c>Add</c> is told that <em>adding</em> a page
    /// was refused, rather than being told about the <c>Insert</c> they did not write.
    /// </para>
    /// </summary>
    /// <param name="operation">
    /// What the caller was trying to do, as a gerund phrase - "adding a page". It is read as the
    /// middle of a sentence naming the mode the document was opened with.
    /// </param>
    void EnsureCanModify(string operation)
    {
        Owner.EnsureCanModify(operation);
    }

    /// <summary>
    /// Creates a new PdfPage, adds it to the end of this document, and returns it.
    /// </summary>
    [MustUseReturnValue]
    public PdfPage Add()
    {
        EnsureCanModify("adding a page");
        var page = new PdfPage();
        return Insert(Count, page);
    }

    /// <summary>
    /// Adds the specified PdfPage to the end of this document and maybe returns a new PdfPage object.
    /// The value returned is a new object if the added page comes from a foreign document.
    /// </summary>
    [MustUseReturnValue]
    public PdfPage Add(PdfPage page, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        EnsureCanModify("adding a page");
        return Insert(Count, page, annotationCopying);
    }

    /// <summary>
    /// Creates a new PdfPage, inserts it at the specified position into this document, and returns it.
    /// </summary>
    [MustUseReturnValue]
    public PdfPage Insert(int index)
    {
        EnsureCanModify("inserting a page");
        var page = new PdfPage();
        return Insert(index, page);
    }

    /// <summary>
    /// Inserts the specified PdfPage at the specified position to this document and maybe returns a new PdfPage object.
    /// The value returned is a new object if the inserted page comes from a foreign document.
    /// </summary>
    [MustUseReturnValue]
    public PdfPage Insert(int index, PdfPage page, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        EnsureCanModify("inserting a page");
        ArgumentNullException.ThrowIfNull(page);

        // Is the page already owned by this document?
        if (page.Owner == Owner)
        {
            // Case: Page is first removed and than inserted again, maybe at another position.
            var count = Count;
            // Check if page is not already part of the document.
            for (var idx = 0; idx < count; idx++)
            {
                if (ReferenceEquals(this[idx], page))
                    throw new InvalidOperationException(PSSR.PageAlreadyPlaced(idx, index));
            }

            // The page was part of this document and was removed from its page tree, or was made
            // with 'new PdfPage(document)' and never placed. Removing a page leaves it in the
            // object table until a save drops it and numbers the rest from one again; Readmit puts
            // it back under a number of its own either way, and the save that follows does the same
            // for whatever the page reaches that was dropped with it. Its /Parent is written by
            // PrepareForSave, which walks the page tree, so it is right however long it was away.
            Owner._irefTable.Readmit(page);
            Debug.Assert(page.Owner == Owner);

            // Insert page in array.
            PagesArray.Elements.Insert(index, page.Reference);

            // Update page count.
            Elements.SetInteger(Keys.Count, PagesArray.Elements.Count);

            Owner.OnPageAdded(page, index);
            return page;
        }

        // All new page insertions come here.
        if (page.Owner == null)
        {
            // Case: New page was newly created and inserted now.
            page.Document = Owner;

            Owner._irefTable.Add(page);
            Debug.Assert(page.Owner == Owner);
            PagesArray.Elements.Insert(index, page.Reference);
            Elements.SetInteger(Keys.Count, PagesArray.Elements.Count);
        }
        else
        {
            // Case: Page is from an external document -> import it.
            var importPage = page;
            page = ImportExternalPage(importPage, annotationCopying);
            Owner._irefTable.Add(page);

            // Add page substitute to importedObjectTable.
            var importedObjectTable = Owner.FormTable.GetImportedObjectTable(importPage);
            importedObjectTable.Add(importPage.ObjectID, page.Reference);

            PagesArray.Elements.Insert(index, page.Reference);
            Elements.SetInteger(Keys.Count, PagesArray.Elements.Count);
            PdfAnnotations.FixImportedAnnotation(page);
            DetachImportedDestinations(page, importPage, importedObjectTable);
        }
        if (Owner.Settings.TrimMargins.AreSet)
            page.TrimMargins = Owner.Settings.TrimMargins;

        Owner.OnPageAdded(page, index);
        return page;
    }

    /// <summary>
    /// The keys copied when a page is duplicated within one document. Annotations are
    /// deliberately excluded: an annotation carries a /P back-reference to the page that owns
    /// it, so sharing one between two pages would leave that reference pointing at the wrong page.
    /// </summary>
    static readonly string[] DuplicatedPageKeys =
    [
        PdfPage.InheritablePageKeys.Resources,
        PdfPage.Keys.Contents,
        PdfPage.InheritablePageKeys.MediaBox,
        PdfPage.InheritablePageKeys.CropBox,
        PdfPage.InheritablePageKeys.Rotate,
        PdfPage.Keys.BleedBox,
        PdfPage.Keys.TrimBox,
        PdfPage.Keys.ArtBox
    ];

    /// <summary>
    /// Returns the index of the specified page in this document, or -1 if the page is not
    /// placed in it. Use this to tell an unplaced page apart from a placed one before calling
    /// <see cref="Place"/>.
    /// </summary>
    public int IndexOf(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var count = Count;
        for (var idx = 0; idx < count; idx++)
        {
            if (ReferenceEquals(this[idx], page))
                return idx;
        }
        return -1;
    }

    /// <summary>
    /// Places a page this document already owns but has not yet added to the page tree, and
    /// returns that same page object. This is the counterpart of <c>new PdfPage(document)</c>,
    /// which creates a drawable page without placing it.
    /// <para>
    /// Unlike <see cref="Insert(int, PdfPage, AnnotationCopyingType)"/> this never copies: the
    /// page returned is always the page passed in. A page from another document is rejected
    /// rather than silently imported.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The page belongs to another document, or is already placed in this one.
    /// </exception>
    public PdfPage Place(int index, PdfPage page)
    {
        EnsureCanModify("placing a page");
        ArgumentNullException.ThrowIfNull(page);
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Argument 'index' out of range.");
        if (page.Owner != null && page.Owner != Owner)
            throw new InvalidOperationException(PSSR.PageBelongsToAnotherDocument);

        // Insert rejects an already placed page with a message naming the remedy.
        var placed = Insert(index, page);
        Debug.Assert(ReferenceEquals(placed, page), "Place must never copy the page.");
        return placed;
    }

    /// <summary>
    /// Imports a page from another document and returns the imported copy, which is always a
    /// different object from the page passed in.
    /// <para>
    /// Unlike <see cref="Insert(int, PdfPage, AnnotationCopyingType)"/> this always copies: a
    /// page this document already owns is rejected rather than silently placed, so the return
    /// value never aliases the argument.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The page has no owner, or already belongs to this document.
    /// </exception>
    [MustUseReturnValue]
    public PdfPage Import(int index, PdfPage page, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        EnsureCanModify("importing a page");
        ArgumentNullException.ThrowIfNull(page);
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Argument 'index' out of range.");
        if (page.Owner == null || page.Owner == Owner)
            throw new InvalidOperationException(PSSR.PageBelongsToThisDocument);

        var imported = Insert(index, page, annotationCopying);
        Debug.Assert(!ReferenceEquals(imported, page), "Import must always copy the page.");
        return imported;
    }

    /// <summary>
    /// Adds a second page showing the content of an existing page of this document, and
    /// returns the new page.
    /// <para>
    /// The duplicate shares the content stream of the source rather than copying its bytes, so
    /// it is cheap and the file does not grow by the size of the page. It gets a resource
    /// dictionary of its own, so the two pages are independent: drawing on either through
    /// XGraphics gives that page a content stream of its own and adds resources only to it.
    /// Annotations are not carried over - see <see cref="DuplicatedPageKeys"/>.
    /// </para>
    /// </summary>
    /// <param name="sourceIndex">The index of the page to duplicate.</param>
    /// <param name="index">The index to place the duplicate at.</param>
    [MustUseReturnValue]
    public PdfPage Duplicate(int sourceIndex, int index)
    {
        EnsureCanModify("duplicating a page");
        if (sourceIndex < 0 || sourceIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(sourceIndex), "Argument 'sourceIndex' out of range.");
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Argument 'index' out of range.");

        var source = this[sourceIndex];
        var duplicate = new PdfPage(Owner);
        foreach (var key in DuplicatedPageKeys)
        {
            var item = source.Elements[key];
            if (item == null)
                continue;

            // The content stream is shared, which is the point: the duplicate costs a page
            // object rather than a copy of the page. The resource dictionary cannot be shared,
            // because drawing on either page writes into it.
            duplicate.Elements[key] = key == PdfPage.InheritablePageKeys.Resources
                ? CloneResources(item)
                : item;
        }
        return Insert(index, duplicate);
    }

    /// <summary>
    /// Gives a duplicated page a resource dictionary of its own, so that drawing on either page
    /// afterwards does not add entries to the other.
    /// <para>
    /// Only the dictionaries are copied. The objects they name - fonts, images, graphics states -
    /// are held by indirect reference, and <see cref="PdfDictionary.Clone"/> leaves those alone,
    /// so the two pages go on sharing the things that carry the bytes.
    /// </para>
    /// </summary>
    PdfItem CloneResources(PdfItem resources)
    {
        var dictionary = ResolveDictionary(resources);
        if (dictionary == null)
            return resources;

        var clone = dictionary.Clone();
        clone.Document = Owner;
        return clone;
    }

    /// <summary>
    /// Returns the dictionary an item holds, following an indirect reference, or null if the
    /// item is not a dictionary.
    /// </summary>
    static PdfDictionary ResolveDictionary(PdfItem item)
    {
        var reference = item as PdfReference;
        if (reference != null)
            item = reference.Value;
        return item as PdfDictionary;
    }

    /// <summary>
    /// Inserts  pages of the specified document into this document.
    /// </summary>
    /// <param name="index">The index in this document where to insert the page .</param>
    /// <param name="document">The document to be inserted.</param>
    /// <param name="startIndex">The index of the first page to be inserted.</param>
    /// <param name="pageCount">The number of pages to be inserted.</param>
    /// <param name="annotationCopying">Annotation copying action, by default annotations are copied shallowly.</param>
    public void InsertRange(int index, PdfDocument document, int startIndex, int pageCount, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        EnsureCanModify("inserting a range of pages");
        ArgumentNullException.ThrowIfNull(document);

        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Argument 'index' out of range.");

        var importDocumentPageCount = document.PageCount;

        if (startIndex < 0 || startIndex + pageCount > importDocumentPageCount)
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Argument 'startIndex' out of range.");

        if (pageCount > importDocumentPageCount)
            throw new ArgumentOutOfRangeException(nameof(pageCount), "Argument 'pageCount' out of range.");

        var inserted = new List<PdfPage>(pageCount);
        for (int insertIndex = index, importIndex = startIndex;
             importIndex < startIndex + pageCount;
             insertIndex++, importIndex++)
        {
            var importPage = document.Pages[importIndex];
            var page = ImportExternalPage(importPage, annotationCopying);

            Owner._irefTable.Add(page);

            // Add page substitute to importedObjectTable.
            var importedObjectTable = Owner.FormTable.GetImportedObjectTable(importPage);
            importedObjectTable.Add(importPage.ObjectID, page.Reference);

            PagesArray.Elements.Insert(insertIndex, page.Reference);
            inserted.Add(page);

            PdfAnnotations.FixImportedAnnotation(page);
            DetachImportedDestinations(page, importPage, importedObjectTable);

            if (Owner.Settings.TrimMargins.AreSet)
                page.TrimMargins = Owner.Settings.TrimMargins;
        }
        Elements.SetInteger(Keys.Count, PagesArray.Elements.Count);

        // Raised once the whole range is in and the count is right, so a handler looking at the
        // document never sees it half inserted. The pages are the ones inserted rather than looked
        // up again by position, because a handler may insert pages of its own and move the rest.
        for (var offset = 0; offset < inserted.Count; offset++)
            Owner.OnPageAdded(inserted[offset], index + offset);
    }

    /// <summary>
    /// Inserts all pages of the specified document into this document.
    /// </summary>
    /// <param name="index">The index in this document where to insert the page .</param>
    /// <param name="document">The document to be inserted.</param>
    /// <param name="annotationCopying">Annotation copying action, by default annotations are copied shallowly.</param>
    public void InsertRange(int index, PdfDocument document, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        ArgumentNullException.ThrowIfNull(document);

        InsertRange(index, document, 0, document.PageCount, annotationCopying);
    }

    /// <summary>
    /// Inserts all pages of the specified document into this document.
    /// </summary>
    /// <param name="index">The index in this document where to insert the page .</param>
    /// <param name="document">The document to be inserted.</param>
    /// <param name="startIndex">The index of the first page to be inserted.</param>
    /// <param name="annotationCopying">Annotation copying action, by default annotations are copied shallowly.</param>
    public void InsertRange(int index, PdfDocument document, int startIndex, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        ArgumentNullException.ThrowIfNull(document);

        InsertRange(index, document, startIndex, document.PageCount - startIndex, annotationCopying);
    }

    /// <summary>
    /// Removes the specified page from the document.
    /// </summary>
    public void Remove(PdfPage page)
    {
        EnsureCanModify("removing a page");
        var index = PagesArray.Elements.IndexOf(page.Reference);
        if (index < 0)
            return;

        RemoveAt(index);
    }

    /// <summary>
    /// Removes the specified page from the document.
    /// </summary>
    public void RemoveAt(int index)
    {
        EnsureCanModify("removing a page");

        // Only a handler needs the page, and reading it resolves the reference, so it is asked for
        // only when somebody is listening.
        var page = Owner.HasPageRemovedHandlers ? this[index] : null;

        PagesArray.Elements.RemoveAt(index);
        Elements.SetInteger(Keys.Count, PagesArray.Elements.Count);

        if (page != null)
            Owner.OnPageRemoved(page, index);
    }

    /// <summary>
    /// Moves a page within the page sequence.
    /// </summary>
    /// <param name="oldIndex">The page index before this operation.</param>
    /// <param name="newIndex">The page index after this operation.</param>
    public void MovePage(int oldIndex, int newIndex)
    {
        EnsureCanModify("moving a page");
        if (oldIndex < 0 || oldIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex)
            return;

        var page = (PdfReference)_pagesArray.Elements[oldIndex];
        _pagesArray.Elements.RemoveAt(oldIndex);
        _pagesArray.Elements.Insert(newIndex, page);
    }

    /// <summary>
    /// Imports an external page. The elements of the imported page are cloned and added to this document.
    /// Important: In contrast to PdfFormXObject adding an external page always make a deep copy
    /// of their transitive closure. Any reuse of already imported objects is not intended because
    /// any modification of an imported page must not change another page.
    /// </summary>
    PdfPage ImportExternalPage(PdfPage importPage, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        if (importPage.Owner._openMode != PdfDocumentOpenMode.Import)
            throw new InvalidOperationException("A PDF document must be opened with PdfDocumentOpenMode.Import to import pages from it.");

        var page = new PdfPage(_document);

        // ReSharper disable AccessToStaticMemberViaDerivedType for a better code readability.
        CloneElement(page, importPage, PdfPage.Keys.Resources, false);
        CloneElement(page, importPage, PdfPage.Keys.Contents, false);
        CloneElement(page, importPage, PdfPage.Keys.MediaBox, true);
        CloneElement(page, importPage, PdfPage.Keys.CropBox, true);
        CloneElement(page, importPage, PdfPage.Keys.Rotate, true);
        CloneElement(page, importPage, PdfPage.Keys.BleedBox, true);
        CloneElement(page, importPage, PdfPage.Keys.TrimBox, true);
        CloneElement(page, importPage, PdfPage.Keys.ArtBox, true);

        if (annotationCopying == AnnotationCopyingType.ShallowCopy)
            CloneElement(page, importPage, PdfPage.Keys.Annots, false);
        else if (annotationCopying == AnnotationCopyingType.DeepCopy)
            CloneElement(page, importPage, PdfPage.Keys.Annots, true);

        // The page group says how the page is composited against its backdrop, so leaving it
        // behind changes what a transparent page looks like. It goes through the imported-object
        // table like the resources, because its /CS may be the very colour space the resources
        // name, and imported twice it would be written twice. The page keeps the group it
        // brings: WriteObject makes one for a page drawn on with transparency only when the
        // page has none.
        CloneElement(page, importPage, PdfPage.Keys.Group, false);
        // A user unit scales default user space, so without it the page comes out a different
        // physical size.
        CloneElement(page, importPage, PdfPage.Keys.UserUnit, true);
        // /UserUnit arrives in PDF 1.6 and /Tabs in PDF 1.5, so a page bringing either raises
        // the document to that version. Raising never lowers, and a PDF/A-1 claim, which is held
        // to PDF 1.4, refuses the raised version rather than writing a header it forbids.
        if (page.Elements.ContainsKey(PdfPage.Keys.UserUnit))
            PdfVersionRequirements.Require(_document, 16);

        CloneElement(page, importPage, PdfPage.Keys.Tabs, true);
        // Structure order, /S, orders the annotations by the structure tree, and the tree is not
        // imported: no import path carries it or the annotations' /StructParent mappings over.
        // So an /S could only point at a structure this document does not have. It is dropped,
        // and the page falls back to the default order; if this document is tagged later, the
        // structure builder writes an /S of its own. Row and column order stand on their own.
        if (page.Elements.GetName(PdfPage.Keys.Tabs) == "/S")
            page.Elements.Remove(PdfPage.Keys.Tabs);
        if (page.Elements.ContainsKey(PdfPage.Keys.Tabs))
            PdfVersionRequirements.Require(_document, 15);
        // The presentation entries: a transition dictionary holds only names and numbers, and
        // the display duration is a number.
        CloneElement(page, importPage, PdfPage.Keys.Trans, true);
        CloneElement(page, importPage, PdfPage.Keys.Dur, true);

        // Deliberately not copied: /StructParents, /B, /AA, /Metadata, /PieceInfo,
        // /SeparationInfo, /Thumb, /ID, /PZ, /PresSteps, /TemplateInstantiated and /VP. Most
        // name a place in a structure that belongs to the other document - its structure tree's
        // parent tree, its article threads, its Web Capture content sets, its navigation nodes -
        // which importing a page does not bring along, so here the entry would point at nothing
        // or at the wrong thing. The rest are actions, private data or advisory data written for
        // the page as it stood in that document, not as it stands in this one.
        // ReSharper restore AccessToStaticMemberViaDerivedType
        return page;
    }

    /// <summary>
    /// Helper function for ImportExternalPage.
    /// </summary>
    void CloneElement(PdfPage page, PdfPage importPage, string key, bool deepcopy)
    {
        Debug.Assert(page != null);
        Debug.Assert(page.Owner == _document);
        Debug.Assert(importPage.Owner != null);
        Debug.Assert(importPage.Owner != _document);

        var item = importPage.Elements[key];
        if (item != null)
        {
            PdfImportedObjectTable importedObjectTable = null;
            if (!deepcopy)
                importedObjectTable = Owner.FormTable.GetImportedObjectTable(importPage);

            // The item can be indirect. If so, replace it by its value.
            if (item is PdfReference)
                item = ((PdfReference)item).Value;
            if (item is PdfObject)
            {
                var root = (PdfObject)item;
                if (deepcopy)
                {
                    Debug.Assert(root.Owner != null, "See 'else' case for details");
                    root = DeepCopyClosure(_document, root);
                }
                else
                {
                    // The owner can be null if the item is not a reference.
                    if (root.Owner == null)
                        root.Document = importPage.Owner;
                    root = ImportClosure(importedObjectTable, page.Owner, root);
                }

                if (root.Reference == null)
                    page.Elements[key] = root;
                else
                    page.Elements[key] = root.Reference;
            }
            else
            {
                // Simple items are just cloned.
                page.Elements[key] = item.Clone();
            }
        }
    }

    /// <summary>
    /// Takes the pages of the external document out of the destinations of the annotations that
    /// were just imported, and remembers where each of those destinations wanted to go.
    /// A destination names its page by an indirect reference, so importing it copies that page,
    /// and with it everything the page reaches, up to and including the whole page tree. That is
    /// why splitting a document used to yield files as large as the document they came from.
    /// </summary>
    void DetachImportedDestinations(PdfPage page, PdfPage importPage, PdfImportedObjectTable importedObjectTable)
    {
        var importedAnnotations = page.Elements.GetArray(PdfPage.Keys.Annots);
        var externalAnnotations = importPage.Elements.GetArray(PdfPage.Keys.Annots);
        if (importedAnnotations == null || externalAnnotations == null)
            return;

        // The document the page is being taken out of, which is the one that holds what the
        // destinations naming rather than stating where they go stand for.
        var externalDocument = importPage.Owner;

        // The annotations were copied one by one, so the two arrays run in parallel.
        var count = Math.Min(importedAnnotations.Elements.Count, externalAnnotations.Elements.Count);
        for (var idx = 0; idx < count; idx++)
        {
            var imported = importedAnnotations.Elements.GetDictionary(idx);
            var external = externalAnnotations.Elements.GetDictionary(idx);
            if (imported == null || external == null)
                continue;

            // A link either carries its destination directly or performs a go-to action.
            DetachDestination(imported, imported, external, PdfLinkAnnotation.Keys.Dest,
                importedObjectTable, externalDocument);

            var importedAction = imported.Elements.GetDictionary(PdfAnnotation.Keys.A);
            var externalAction = external.Elements.GetDictionary(PdfAnnotation.Keys.A);
            if (importedAction == null || externalAction == null)
                continue;

            // Only a go-to action goes to a page of the document the annotation is part of.
            // /GoToR and friends go into another file, where a page number means what it says
            // and a name is for that file to resolve, so their destination is left alone. An
            // action that does not say what it is is taken to be a go-to, which is what it was
            // taken to be before any of them were told apart.
            var subtype = externalAction.Elements.GetName("/S");
            if (subtype.Length == 0 || subtype == "/GoTo")
                DetachDestination(imported, importedAction, externalAction, "/D",
                    importedObjectTable, externalDocument);
        }
    }

    /// <summary>
    /// Helper function for DetachImportedDestinations. Empties the page out of a single
    /// destination, which is an array whose first element is the page to go to.
    /// <para>
    /// A destination written as a string or a name is one the catalog of the external document
    /// holds under that name. The catalog is not imported along with a page, so the name would
    /// arrive standing for nothing and the link would go nowhere. What it stands for is looked
    /// up and written in its place, which leaves the link with a destination that carries
    /// itself and needs no catalog to be understood.
    /// </para>
    /// </summary>
    void DetachDestination(PdfDictionary annotation, PdfDictionary holder, PdfDictionary externalHolder,
        string key, PdfImportedObjectTable importedObjectTable, PdfDocument externalDocument)
    {
        var externalDestination = externalHolder.Elements.GetArray(key);
        var named = externalDestination == null;
        if (named)
            externalDestination = PdfNamedDestinations.Lookup(externalDocument, externalHolder.Elements[key]);

        if (externalDestination == null || externalDestination.Elements.Count == 0)
            return;

        // A destination going into another file names its page by number rather than holding a
        // reference to it, and there is nothing here to detach.
        var externalPage = externalDestination.Elements[0] as PdfReference;
        if (externalPage == null)
            return;

        PdfArray destination;
        if (named)
        {
            destination = ExplicitDestination(externalDestination);
            if (destination == null)
                return;
            holder.Elements[key] = destination;
        }
        else
        {
            destination = holder.Elements.GetArray(key);
            if (destination == null || destination.Elements.Count == 0)
                return;
        }

        destination.Elements[0] = PdfNull.Value;
        _importedDestinations.Add(new ImportedDestination(annotation, holder, key, destination,
            importedObjectTable, externalPage.ObjectID));
    }

    /// <summary>
    /// A destination of this document saying what the one given says beyond which page it goes
    /// to, that page being left to be filled in once it is known what became of it.
    /// <para>
    /// Returns null when there is an element that cannot be carried over, so that a destination
    /// which cannot be written faithfully is left alone rather than written wrongly.
    /// </para>
    /// </summary>
    PdfArray ExplicitDestination(PdfArray externalDestination)
    {
        var destination = new PdfArray(_document);
        destination.Elements.Add(PdfNull.Value);

        var count = externalDestination.Elements.Count;
        for (var idx = 1; idx < count; idx++)
        {
            var item = externalDestination.Elements[idx];

            // Where on the page to go is written with names and numbers. Anything else is an
            // object of the other document, which cloning would not bring across.
            if (item is PdfReference || item is PdfObject)
                return null;

            destination.Elements.Add(item.Clone());
        }
        return destination;
    }

    /// <summary>
    /// Points the destinations detached by DetachImportedDestinations at their page again. Which
    /// pages of an external document made it into this one is not known before it is saved, so a
    /// destination whose page was left behind can only be dropped here.
    /// </summary>
    void ResolveImportedDestinations()
    {
        if (_importedDestinations.Count == 0)
            return;

        var ownPages = new Dictionary<PdfReference, object>();
        foreach (var item in PagesArray.Elements)
        {
            var iref = item as PdfReference;
            if (iref != null)
                ownPages[iref] = null;
        }

        foreach (var destination in _importedDestinations)
        {
            // The page substitute overwrites whatever the import left under this identifier, so
            // the entry is the imported page itself as soon as the page was imported as a page.
            var page = destination.ImportedObjectTable.Contains(destination.ExternalPageID)
                ? destination.ImportedObjectTable[destination.ExternalPageID]
                : null;

            if (page != null && ownPages.ContainsKey(page))
            {
                destination.Destination.Elements[0] = page;
            }
            else
            {
                // There is no page in this document to go to, so the link stays without an aim.
                destination.Holder.Elements.Remove(destination.Key);
                if (!ReferenceEquals(destination.Holder, destination.Annotation))
                    destination.Annotation.Elements.Remove(PdfAnnotation.Keys.A);
            }
        }

        _importedDestinations.Clear();
    }

    /// <summary>
    /// The destinations of imported annotations, waiting for the page of the external document
    /// they name to be imported as well.
    /// </summary>
    readonly List<ImportedDestination> _importedDestinations = [];

    sealed class ImportedDestination
    {
        internal ImportedDestination(PdfDictionary annotation, PdfDictionary holder, string key,
            PdfArray destination, PdfImportedObjectTable importedObjectTable, PdfObjectID externalPageID)
        {
            Annotation = annotation;
            Holder = holder;
            Key = key;
            Destination = destination;
            ImportedObjectTable = importedObjectTable;
            ExternalPageID = externalPageID;
        }

        /// <summary>The annotation the destination belongs to.</summary>
        internal readonly PdfDictionary Annotation;

        /// <summary>The annotation itself or, for a go-to action, the action dictionary.</summary>
        internal readonly PdfDictionary Holder;

        /// <summary>The key the destination is held under by Holder.</summary>
        internal readonly string Key;

        /// <summary>The destination array, whose first element is the page to go to.</summary>
        internal readonly PdfArray Destination;

        internal readonly PdfImportedObjectTable ImportedObjectTable;

        /// <summary>The page of the external document the destination named.</summary>
        internal readonly PdfObjectID ExternalPageID;
    }

    /// <summary>
    /// Gets a PdfArray containing all pages of this document. The array must not be modified.
    /// </summary>
    public PdfArray PagesArray
    {
        get
        {
            if (_pagesArray == null)
                _pagesArray = (PdfArray)Elements.GetValue(Keys.Kids, VCF.Create);
            return _pagesArray;
        }
    }
    PdfArray _pagesArray;

    /// <summary>
    /// Replaces the page tree by a flat array of indirect references to the pages objects.
    /// </summary>
    internal void FlattenPageTree()
    {
        // Acrobat creates a balanced tree if the number of pages is rougly more than ten. This is
        // not difficult but obviously also not necessary. I created a document with 50000 pages with
        // PDF4NET and Acrobat opened it in less than 2 seconds.

        // Promote inheritable values down the page tree
        var values = new PdfPage.InheritedValues();
        PdfPage.InheritValues(this, ref values);
        var pages = GetKids(Reference, values, Owner._irefTable.ObjectTable.Count);

        var array = new PdfArray(Owner);
        foreach (var page in pages)
        {
            // Fix the parent
            page.Elements[PdfPage.Keys.Parent] = Reference;
            array.Elements.Add(page.Reference);
        }

        Elements.SetName(Keys.Type, "/Pages");
        // direct array
        Elements.SetValue(Keys.Kids, array);

        Elements.SetInteger(Keys.Count, array.Elements.Count);
    }

    /// <summary>
    /// Recursively converts the page tree into a flat array.
    /// </summary>
    static PdfDictionary[] GetKids(PdfReference iref, PdfPage.InheritedValues values, int objectCount)
    {
        // A tree enters each of its nodes once, so the walk of a file holding n objects enters at
        // most n nodes. A node two parents list is entered twice, which is read rather than refused -
        // but a chain of nodes each listing the next one twice doubles at every level, and forty
        // levels are a trillion nodes in a file of forty objects: no loop, nothing deep, and a walk
        // that never ends in practice. Twice the objects in the file is room for every node to be
        // listed twice and no room for that.
        var budget = 2 * objectCount + 1;
        return GetKids(iref, values, [], ref budget);
    }

    /// <summary>
    /// How deep the page tree of a document may be nested before the file is read as malformed.
    /// </summary>
    /// <remarks>
    /// Not a limit on how many pages a document may have: a tree this deep branching two ways at
    /// every level holds more pages than a file could name. The number it is really about is the
    /// stack, which this walk overflowed at about 1,800 frames — and a stack overflow cannot be
    /// caught, so the process goes, and the file was only being opened.
    /// </remarks>
    const int MaxPageTreeDepth = 256;

    /// <summary>
    /// The same walk, carrying the nodes it is currently inside — which is both the loop detector
    /// and the depth, since a node appears on the path at most once.
    /// </summary>
    static PdfDictionary[] GetKids(PdfReference iref, PdfPage.InheritedValues values,
        HashSet<PdfObjectID> ancestors, ref int budget)
    {
        if (--budget < 0)
        {
            throw new PdfReaderException(
                $"The page tree has been entered more than twice as many times as the file has " +
                $"objects, reaching node {iref.ObjectID}, so it lists the same subtrees over and over " +
                "rather than each once as a tree does.");
        }

        if (iref.Value is not PdfDictionary kid)
        {
            throw new PdfReaderException(
                $"Object {iref.ObjectID} stands in a page tree but is a " +
                $"{TypeNameOf(iref.Value)} rather than a dictionary.");
        }

        var type = kid.Elements.GetName(Keys.Type);
        if (type == "/Page")
        {
            PdfPage.InheritValues(kid, values);
            return [kid];
        }

        if (string.IsNullOrEmpty(type))
        {
            // Type is required. If type is missing, assume it is "/Page" and hope it will work.
            PdfPage.InheritValues(kid, values);
            return [kid];
        }

        Debug.Assert(kid.Elements.GetName(Keys.Type) == "/Pages");
        PdfPage.InheritValues(kid, ref values);

        // The array itself, or an indirect reference to it. Both are well-formed; a reference is
        // resolved here so that what is reported below is what the entry came to rather than the
        // reference that got there.
        var entry = kid.Elements[Keys.Kids];
        if (entry is PdfReference reference)
            entry = reference.Value;

        // A node that lists no children is read as a node with no children, which is the tolerant
        // reading already taken a few lines above for a node with no /Type. /Kids is required of a
        // page tree node by ISO 32000-1 Table 29, so this is already a file that does not say what it
        // should. A reference the file never defines is the null object by 7.3.9 and a null entry is
        // the same as no entry, which is how the rest of a page reads one - see DanglingReferenceTests.
        if (entry == null || entry is PdfNull)
            return [];

        // Anything else - a number, a name, a dictionary - is a page tree this method cannot walk, and
        // saying which node it gave up on is worth more than the NullReferenceException it used to
        // raise two lines later.
        if (entry is not PdfArray kids)
        {
            throw new PdfReaderException(
                $"The /Kids entry of page tree node {iref.ObjectID} is a {TypeNameOf(entry)} " +
                "rather than an array.");
        }

        // Descending is the one thing that can fail to end, so this is where the walk is asked
        // whether it is going anywhere new. A node that stands among its own ancestors is a loop
        // rather than a deeper tree, and a tree deeper than any document builds is one the stack
        // cannot hold. Both used to run until the stack ran out, which takes the process with it —
        // an untrusted file killed the program that merely opened it. ISO 32000-1 7.7.3.2 has the
        // pages of a document in a *tree*; empira/PDFsharp#361 is a file where they are not.
        if (!ancestors.Add(iref.ObjectID))
        {
            throw new PdfReaderException(
                $"Page tree node {iref.ObjectID} stands among its own descendants, so the /Kids " +
                "of this document form a loop rather than a tree.");
        }

        if (ancestors.Count > MaxPageTreeDepth)
        {
            throw new PdfReaderException(
                $"The page tree is nested more than {MaxPageTreeDepth} levels deep at node " +
                $"{iref.ObjectID}, which is deeper than a tree of pages is ever built and deeper " +
                "than this walk can go.");
        }

        var list = new List<PdfDictionary>();
        foreach (var item in kids)
        {
            if (item is not PdfReference xref2)
            {
                throw new PdfReaderException(
                    $"The /Kids array of page tree node {iref.ObjectID} holds a " +
                    $"{TypeNameOf(item)} where an indirect reference to a page or a page tree " +
                    "node was expected.");
            }

            list.AddRange(GetKids(xref2, values, ancestors, ref budget));
        }

        // Only while the walk is inside it. A node listed by two parents is a page counted twice,
        // which is malformed but ends; it is a node listed by itself that does not, and that is
        // what the set above is asked about.
        ancestors.Remove(iref.ObjectID);

        // /Count is not asserted against what was found. FlattenPageTree overwrites it with the real
        // count the moment this returns, and an empty node tolerated above legitimately leaves the
        // two disagreeing - so the assertion that used to stand here could only fire on the files
        // this method now reads on purpose.
        return [.. list];
    }

    /// <summary>
    /// What to call an object in a message about a page tree that does not hold what it should.
    /// </summary>
    static string TypeNameOf(PdfItem item) => item == null ? "null" : item.GetType().Name;

    /// <summary>
    /// Prepares the document for saving.
    /// </summary>
    internal override void PrepareForSave()
    {
        ResolveImportedDestinations();

        // The pages are written as one flat /Kids array rather than a balanced tree. The
        // implementation limit of 8,191 array elements in ISO 32000-1 Annex C is a reader's, not
        // the format's, and documents of 50,000 pages written this way have opened without
        // complaint, so a tree is not built until a reader is found that needs one.

        // Through the property, not the field. The field is filled in lazily, and reading it here
        // only worked because every path that reached this point happened to have touched the
        // property first — an incremental save does not, and got a null reference for it.
        var count = PagesArray.Elements.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var page = this[idx];
            page.PrepareForSave();
        }
    }

    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    public new IEnumerator<PdfPage> GetEnumerator()
    {
        return new PdfPagesEnumerator(this);
    }

    class PdfPagesEnumerator : IEnumerator<PdfPage>
    {
        internal PdfPagesEnumerator(PdfPages list)
        {
            _list = list;
            _index = -1;
        }

        public bool MoveNext()
        {
            if (_index < _list.Count - 1)
            {
                _index++;
                _currentElement = _list[_index];
                return true;
            }
            _index = _list.Count;
            return false;
        }

        public void Reset()
        {
            _currentElement = null;
            _index = -1;
        }

        object IEnumerator.Current => Current;

        public PdfPage Current
        {
            get
            {
                if (_index == -1 || _index >= _list.Count)
                    throw new InvalidOperationException(PSSR.ListEnumCurrentOutOfRange);
                return _currentElement;
            }
        }

        public void Dispose()
        {
            // Nothing to do.
        }

        PdfPage _currentElement;
        int _index;
        readonly PdfPages _list;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed class Keys : PdfPage.InheritablePageKeys
    {
        /// <summary>
        /// (Required) The type of PDF object that this dictionary describes;
        /// must be Pages for a page tree node.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "Pages")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required except in root node; must be an indirect reference)
        /// The page tree node that is the immediate parent of this one.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Parent = "/Parent";

        /// <summary>
        /// (Required) An array of indirect references to the immediate children of this node.
        /// The children may be page objects or other page tree nodes.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string Kids = "/Kids";

        /// <summary>
        /// (Required) The number of leaf nodes (page objects) that are descendants of this node
        /// within the page tree.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string Count = "/Count";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
