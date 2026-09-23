#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Diagnostics;
using System.Collections;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Signatures;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents the annotations array of a page.
/// </summary>
public sealed class PdfAnnotations : PdfArray
{
    internal PdfAnnotations(PdfDocument document)
        : base(document)
    { }

    internal PdfAnnotations(PdfArray array)
        : base(array)
    { }

    /// <summary>
    /// Adds the specified annotation.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    public void Add(PdfAnnotation annotation)
    {
        Owner.EnsureCanModify("adding an annotation", PdfChangeKind.Annotations);

        annotation.Document = Owner;
        Owner._irefTable.Add(annotation);
        Elements.Add(annotation.Reference);
        annotation.OnAddedToPage();
    }

    /// <summary>
    /// Removes an annotation from the document.
    /// </summary>
    public void Remove(PdfAnnotation annotation)
    {
        if (annotation.Owner != Owner)
            throw new InvalidOperationException("The annotation does not belong to this document.");

        Owner.EnsureCanModify("removing an annotation", PdfChangeKind.Annotations);

        Owner.Internals.RemoveObject(annotation);
        Elements.Remove(annotation.Reference);
    }

    /// <summary>
    /// Removes all the annotations from the current page.
    /// </summary>
    public void Clear()
    {
        for (var idx = Count - 1; idx >= 0; idx--)
            Page.Annotations.Remove(_page.Annotations[idx]);
    }

    /// <summary>
    /// Gets the number of annotations in this collection.
    /// </summary>
    public int Count => Elements.Count;

    /// <summary>
    /// Gets the <see cref="PdfPinata.Pdf.Annotations.PdfAnnotation"/> at the specified index.
    /// </summary>
    public PdfAnnotation this[int index]
    {
        get
        {
            PdfReference iref;
            PdfDictionary dict;
            var item = Elements[index];
            if ((iref = item as PdfReference) != null)
            {
                Debug.Assert(iref.Value is PdfDictionary, "Reference to dictionary expected.");
                dict = (PdfDictionary)iref.Value;
            }
            else
            {
                Debug.Assert(item is PdfDictionary, "Dictionary expected.");
                dict = (PdfDictionary)item;
            }
            // Given the class its subtype names. Wrapping an indirect dictionary points its
            // reference at the wrapper, so the next read finds the same object; a direct one has
            // to be put back in the array for the same to be true.
            var annotation = PdfAnnotation.FromDictionary(dict);
            if (!ReferenceEquals(annotation, dict) && iref == null)
                Elements[index] = annotation;
            return annotation;
        }
    }

    /// <summary>
    /// Gets the page the annotations belongs to.
    /// </summary>
    internal PdfPage Page
    {
        get => _page;
        set => _page = value;
    }
    private PdfPage _page;

    /// <summary>
    /// Fixes the /P element in imported annotation.
    /// </summary>
    internal static void FixImportedAnnotation(PdfPage page)
    {
        var annots = page.Elements.GetArray(PdfPage.Keys.Annots);
        if (annots != null)
        {
            var count = annots.Elements.Count;
            for (var idx = 0; idx < count; idx++)
            {
                var annot = annots.Elements.GetDictionary(idx);
                if (annot != null && annot.Elements.ContainsKey("/P"))
                    annot.Elements["/P"] = page.Reference;
            }
        }
    }

    /// <summary>
    /// Returns an enumerator over the annotations in this collection, each given the class its
    /// subtype names, exactly as <see cref="this[int]"/> gives it.
    /// </summary>
    /// <remarks>
    /// Hides <see cref="PdfArray.GetEnumerator"/> rather than overriding it, so that
    /// <c>foreach (var annotation in page.Annotations)</c> is typed as <see cref="PdfAnnotation"/>.
    /// Enumerated as a <see cref="PdfArray"/>, or through <see cref="IEnumerable{T}"/> of
    /// <see cref="PdfItem"/> or plain <see cref="IEnumerable"/>, it yields the same annotations.
    /// </remarks>
    public new IEnumerator<PdfAnnotation> GetEnumerator()
    {
        return new AnnotationsIterator(this);
    }

    private protected override IEnumerator<PdfItem> EnumerateItems()
    {
        return GetEnumerator();
    }

    private sealed class AnnotationsIterator : IEnumerator<PdfAnnotation>
    {
        public AnnotationsIterator(PdfAnnotations annotations)
        {
            _annotations = annotations;
            _index = -1;
        }

        public PdfAnnotation Current => _annotations[_index];

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return ++_index < _annotations.Count;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
            // Holds nothing to release.
        }

        private readonly PdfAnnotations _annotations;
        private int _index;
    }
}
