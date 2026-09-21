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
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Contains all external PDF files from which PdfFormXObjects are imported into the current document.
/// </summary>
internal sealed class PdfFormXObjectTable : PdfResourceTable
{
    // The name PdfFormXObjectTable is technically not correct, because in contrast to PdfFontTable
    // or PdfImageTable this class holds no PdfFormXObject objects. Actually it holds instances of
    // the class ImportedObjectTable, one for each external document. The PdfFormXObject instances
    // are not cached, because they hold a transformation matrix that make them unique. If the user
    // wants to use a particular page of a PdfFormXObject more than once, he must reuse the object
    // before he changes the PageNumber or the transformation matrix. In other words this class
    // caches the indirect objects of an external form, not the form itself.

    /// <summary>
    /// Initializes a new instance of this class, which is a singleton for each document.
    /// </summary>
    public PdfFormXObjectTable(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Gets a PdfFormXObject from an XPdfForm. Because the returned objects must be unique, always
    /// a new instance of PdfFormXObject is created if none exists for the specified form.
    /// </summary>
    public PdfFormXObject GetForm(XForm form)
    {
        // If the form already has a PdfFormXObject, return it.
        if (form._pdfForm != null)
        {
            Debug.Assert(form.IsTemplate, "An XPdfForm must not have a PdfFormXObject.");
            if (ReferenceEquals(form._pdfForm.Owner, Owner))
                return form._pdfForm;

            // Dispose PdfFromXObject when document has changed
            form._pdfForm = null;
        }

        if (form is XPdfForm pdfForm)
        {
            // Is the external PDF file from which is imported already known for the current document?
            var selector = new Selector(form);
            PdfImportedObjectTable importedObjectTable;
            if (!_forms.TryGetValue(selector, out importedObjectTable))
            {
                // No: Get the external document from the form and create ImportedObjectTable.
                var doc = pdfForm.ExternalDocument;
                importedObjectTable = new PdfImportedObjectTable(Owner, doc);
                _forms[selector] = importedObjectTable;
            }

            var xObject = importedObjectTable.GetXObject(pdfForm.PageNumber);
            if (xObject == null)
            {
                xObject = new PdfFormXObject(Owner, importedObjectTable, pdfForm);
                importedObjectTable.SetXObject(pdfForm.PageNumber, xObject);
            }
            return xObject;
        }
        Debug.Assert(form.GetType() == typeof(XForm));
        form._pdfForm = new PdfFormXObject(Owner);
        return form._pdfForm;
    }

    /// <summary>
    /// Gets the imported object table.
    /// </summary>
    public PdfImportedObjectTable GetImportedObjectTable(PdfPage page)
    {
        // Is the external PDF file from which is imported already known for the current document?
        var selector = new Selector(page);
        PdfImportedObjectTable importedObjectTable;
        if (!_forms.TryGetValue(selector, out importedObjectTable))
        {
            importedObjectTable = new PdfImportedObjectTable(Owner, page.Owner);
            _forms[selector] = importedObjectTable;
        }
        return importedObjectTable;
    }

    /// <summary>
    /// Gets the imported object table.
    /// </summary>
    public PdfImportedObjectTable GetImportedObjectTable(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Is the external PDF file from which is imported already known for the current document?
        var selector = new Selector(document);
        if (!_forms.TryGetValue(selector, out var importedObjectTable))
        {
            // Create new table for document.
            importedObjectTable = new PdfImportedObjectTable(Owner, document);
            _forms[selector] = importedObjectTable;
        }
        return importedObjectTable;
    }

    public void DetachDocument(PdfDocument.DocumentHandle handle)
    {
        if (handle.IsAlive)
        {
            foreach (var selector in _forms.Keys)
            {
                var table = _forms[selector];
                if (table.ExternalDocument != null && table.ExternalDocument.Handle == handle)
                {
                    _forms.Remove(selector);
                    break;
                }
            }
        }

        // Clean table
        var itemRemoved = true;
        while (itemRemoved)
        {
            itemRemoved = false;
            foreach (var selector in _forms.Keys)
            {
                var table = _forms[selector];
                if (table.ExternalDocument == null)
                {
                    _forms.Remove(selector);
                    itemRemoved = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Points every imported object that has been merged into another at the one it was merged
    /// into, so that importing more from the same document reuses what is still in this one.
    /// </summary>
    internal void RedirectImportedObjects(IReadOnlyDictionary<PdfReference, PdfReference> replacements)
    {
        foreach (var table in _forms.Values)
            table.Redirect(replacements);
    }

    /// <summary>
    /// Map from Selector to PdfImportedObjectTable.
    /// </summary>
    readonly Dictionary<Selector, PdfImportedObjectTable> _forms = new();

    /// <summary>
    /// A collection of information that uniquely identifies a particular ImportedObjectTable.
    /// </summary>
    public class Selector
    {
        /// <summary>
        /// Initializes a new instance of FormSelector from an XPdfForm.
        /// </summary>
        public Selector(XForm form)
        {
            // HACK: just use full path to identify
            _path = form._path.ToLowerInvariant();
        }

        /// <summary>
        /// Initializes a new instance of FormSelector from a PdfPage.
        /// </summary>
        public Selector(PdfPage page)
        {
            var owner = page.Owner;
            _path = "*" + owner.Guid.ToString("B");
            _path = _path.ToLowerInvariant();
        }

        public Selector(PdfDocument document)
        {
            _path = "*" + document.Guid.ToString("B");
            _path = _path.ToLowerInvariant();
        }

        public string Path
        {
            get => _path;
            set => _path = value;
        }
        string _path;

        public override bool Equals(object obj)
        {
            var selector = obj as Selector;
            if (selector == null)
                return false;
            // ReSharper disable once PossibleNullReferenceException
            return _path == selector._path;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return _path.GetHashCode();
        }
    }
}
