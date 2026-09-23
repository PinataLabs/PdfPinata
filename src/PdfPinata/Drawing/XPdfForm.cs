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
using System.IO;
using PdfPinata.Internal;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents a so called 'PDF form external object', which is typically an imported page of an external
/// PDF document. XPdfForm objects are used like images to draw an existing PDF page of an external
/// document in the current document. XPdfForm objects can only be placed in PDF documents. If you try
/// to draw them using a XGraphics based on an GDI+ context no action is taken if no placeholder image
/// is specified. Otherwise, the placeholder is drawn.
/// </summary>
public class XPdfForm : XForm
{
    /// <summary>
    /// Initializes a new instance of the XPdfForm class from the specified path to an external PDF document.
    /// Although PDFsharp internally caches XPdfForm objects it is recommended to reuse XPdfForm objects
    /// in your code and change the PageNumber property if more than one page is needed form the external
    /// document. Furthermore, because XPdfForm can occupy very much memory, it is recommended to
    /// dispose XPdfForm objects if not needed anymore.
    /// </summary>
    internal XPdfForm(string path, PdfReadAccuracy accuracy)
    {
        path = ExtractPageNumber(path, out var pageNumber);

        path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException(PSSR.FileNotFound(path));

        if (PdfReader.TestPdfFile(path) == 0)
            throw new ArgumentException("The specified file has no valid PDF file header.", nameof(path));

        Path = path;
        _pathReadAccuracy = accuracy;
        if (pageNumber != 0)
            PageNumber = pageNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XPdfForm"/> class from a stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="accuracy">Moderate allows for broken references.</param>
    internal XPdfForm(Stream stream, PdfReadAccuracy accuracy)
    {
        // Create a dummy unique path
        Path = "*" + Guid.NewGuid().ToString("B");

        if (PdfReader.TestPdfFile(stream) == 0)
            throw new ArgumentException("The specified stream has no valid PDF file header.", nameof(stream));

        _externalDocument = PdfReader.Open(stream, accuracy);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XPdfForm"/> class from a stream and password.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password.</param>
    /// <param name="accuracy">Moderate allows for broken references.</param>
    internal XPdfForm(Stream stream, string password, PdfReadAccuracy accuracy)
    {
        // Create a dummy unique path
        Path = "*" + Guid.NewGuid().ToString("B");

        if (PdfReader.TestPdfFile(stream) == 0)
            throw new ArgumentException("The specified stream has no valid PDF file header.", nameof(stream));

        _externalDocument = PdfReader.Open(stream, password, PdfDocumentOpenMode.ReadOnly, accuracy);
    }

    /// <summary>
    /// Creates an XPdfForm from a file.
    /// </summary>
    public static XPdfForm FromFile(string path)
    {
        return FromFile(path, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Creates an XPdfForm from a file.
    /// </summary>
    public new static XPdfForm FromFile(string path, PdfReadAccuracy accuracy)
    {
        return new XPdfForm(path, accuracy);
    }

    /// <summary>
    /// Creates an XPdfForm from a stream.
    /// </summary>
    public static XPdfForm FromStream(Stream stream)
    {
        return FromStream(stream, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Creates an XPdfForm from a stream.
    /// </summary>
    public static XPdfForm FromStream(Stream stream, PdfReadAccuracy accuracy)
    {
        return new XPdfForm(stream, accuracy);
    }

    /// <summary>
    /// Creates an XPdfForm from a stream and a password.
    /// </summary>
    public static XPdfForm FromStream(Stream stream, string password)
    {
        return FromStream(stream, password, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Creates an XPdfForm from a stream and a password.
    /// </summary>
    public static XPdfForm FromStream(Stream stream, string password, PdfReadAccuracy accuracy)
    {
        return new XPdfForm(stream, password, accuracy);
    }

    /// <summary>
    /// Sets the form in the state FormState.Finished.
    /// </summary>
    internal override void Finish()
    {
        if (_formState is FormState.NotATemplate or FormState.Finished)
            return;

        base.Finish();
    }

    /// <summary>
    /// Frees the memory occupied by the underlying imported PDF document, even if other XPdfForm objects
    /// refer to this document. A reuse of this object doesn't fail, because the underlying PDF document
    /// is re-imported if necessary.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            if (_externalDocument != null)
                PdfDocument.Tls.DetachDocument(_externalDocument.Handle);
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private bool _disposed;

    /// <summary>
    /// Gets or sets an image that is used for drawing if the current XGraphics object cannot handle
    /// PDF forms. A place holder is useful for showing a preview of a page on the display, because
    /// PDFsharp cannot render native PDF objects.
    /// </summary>
    public XImage PlaceHolder { get; set; }

    /// <summary>
    /// Gets the underlying PdfPage (if one exists).
    /// </summary>
    public PdfPage Page
    {
        get
        {
            if (IsTemplate)
                return null;
            var page = ExternalDocument.Pages[_pageNumber - 1];
            return page;
        }
    }

    /// <summary>
    /// Gets the number of pages in the PDF form.
    /// </summary>
    public int PageCount
    {
        get
        {
            if (IsTemplate)
                return 1;
            if (field == -1)
                field = ExternalDocument.Pages.Count;
            return field;
        }
    } = -1;

    /// <summary>
    /// Gets the width in point of the page identified by the property PageNumber.
    /// </summary>
    public override double PointWidth
    {
        get
        {
            var page = ExternalDocument.Pages[_pageNumber - 1];
            return page.Width;
        }
    }

    /// <summary>
    /// Gets the height in point of the page identified by the property PageNumber.
    /// </summary>
    public override double PointHeight
    {
        get
        {
            var page = ExternalDocument.Pages[_pageNumber - 1];
            return page.Height;
        }
    }

    /// <summary>
    /// Gets the width in point of the page identified by the property PageNumber.
    /// </summary>
    public override int PixelWidth => DoubleUtil.DoubleToInt(PointWidth);

    /// <summary>
    /// Gets the height in point of the page identified by the property PageNumber.
    /// </summary>
    public override int PixelHeight => DoubleUtil.DoubleToInt(PointHeight);

    /// <summary>
    /// Get the size of the page identified by the property PageNumber.
    /// </summary>
    public override XSize Size
    {
        get
        {
            var page = ExternalDocument.Pages[_pageNumber - 1];
            return new XSize(page.Width, page.Height);
        }
    }

    /// <summary>
    /// Gets or sets the transformation matrix.
    /// </summary>
    public override XMatrix Transform
    {
        get => _transform;
        set
        {
            if (_transform == value)
                return;

            // discard PdfFromXObject when Transform changed
            _pdfForm = null;
            _transform = value;
        }
    }

    /// <summary>
    /// Gets or sets the page number in the external PDF document this object refers to. The page number
    /// is one-based, i.e. it is in the range from 1 to PageCount. The default value is 1.
    /// </summary>
    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (IsTemplate)
                throw new InvalidOperationException("The page number of an XPdfForm template cannot be modified.");

            if (_pageNumber == value)
                return;

            _pageNumber = value;
            // dispose PdfFromXObject when number has changed
            _pdfForm = null;
        }
    }

    private int _pageNumber = 1;

    /// <summary>
    /// Gets or sets the page index in the external PDF document this object refers to. The page index
    /// is zero-based, i.e. it is in the range from 0 to PageCount - 1. The default value is 0.
    /// </summary>
    public int PageIndex
    {
        get => PageNumber - 1;
        set => PageNumber = value + 1;
    }

    /// <summary>
    /// Gets the underlying document from which pages are imported.
    /// </summary>
    internal PdfDocument ExternalDocument
    {
        // The problem is that you can ask an XPdfForm about the number of its pages before it was
        // drawn the first time. At this moment the XPdfForm doesn't know the document where it will
        // be later draw on one of its pages. To prevent the import of the same document more than
        // once, all imported documents of a thread are cached. The cache is local to the current
        // thread and not to the appdomain, because I won't get problems in a multi-thread environment
        // that I don't understand.
        get
        {
            if (IsTemplate)
                throw new InvalidOperationException(
                    "This XPdfForm is a template and not an imported PDF page; therefore it has no external document.");

            _externalDocument ??= PdfDocument.Tls.GetDocument(Path, _pathReadAccuracy);
            return _externalDocument;
        }
    }

    internal PdfDocument _externalDocument;

    private readonly PdfReadAccuracy _pathReadAccuracy;

    /// <summary>
    /// Extracts the page number if the path has the form 'MyFile.pdf#123' and returns
    /// the actual path without the number sign and the following digits.
    /// </summary>
    public static string ExtractPageNumber(string path, out int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(path);

        pageNumber = 0;
        var length = path.Length;
        if (length == 0)
            return path;

        length--;
        if (!char.IsDigit(path, length))
            return path;

        // Bound first: the old order asked whether path[-1] was a digit when every
        // character was one, and char.IsDigit threw rather than the loop ending.
        // Duplicated in PinataLayout's ImageHelper.
        while (length >= 0 && char.IsDigit(path, length))
            length--;
        if (length <= 0 || path[length] != '#')
            return path;

        // Must have at least one dot left of colon to distinguish from e.g. '#123'
        if (!path.Contains('.'))
            return path;

        pageNumber = int.Parse(path[(length + 1)..]);
        path = path[..length];

        return path;
    }
}
