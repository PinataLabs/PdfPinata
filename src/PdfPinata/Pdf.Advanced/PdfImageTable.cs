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
/// Contains all used images of a document.
/// </summary>
internal sealed class PdfImageTable : PdfResourceTable
{
    /// <summary>
    /// Initializes a new instance of this class, which is a singleton for each document.
    /// </summary>
    public PdfImageTable(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Gets a PdfImage from an XImage. If no PdfImage already exists, a new one is created.
    /// </summary>
    public PdfImage GetImage(XImage image)
    {
        var selector = image.Selector;
        if (selector == null)
        {
            selector = new ImageSelector(image);
            image.Selector = selector;
        }
        if (!_images.TryGetValue(selector, out var pdfImage))
        {
            pdfImage = new PdfImage(Owner, image);
            //pdfImage.Document = _document;
            Debug.Assert(pdfImage.Owner == Owner);
            _images[selector] = pdfImage;
        }
        return pdfImage;
    }

    /// <summary>
    /// Map from ImageSelector to PdfImage.
    /// </summary>
    readonly Dictionary<ImageSelector, PdfImage> _images = new();

    /// <summary>
    /// A collection of information that uniquely identifies a particular PdfImage.
    /// </summary>
    public class ImageSelector
    {
        /// <summary>
        /// Initializes a new instance of ImageSelector from an XImage.
        /// </summary>
        public ImageSelector(XImage image)
        {
            // Images are matched by path here; identical content drawn from different sources is
            // merged at save time when PdfDocumentOptions.DeduplicateResources is set.
            if (image.Path == null)
                image.Path = "*" + Guid.NewGuid().ToString("B");

            _path = image.Path.ToLowerInvariant();
        }

        public string Path
        {
            get => _path;
            set => _path = value;
        }
        string _path;

        public override bool Equals(object obj)
        {
            var selector = obj as ImageSelector;
            if (selector == null)
                return false;
            return _path == selector._path;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return _path.GetHashCode();
        }
    }
}
