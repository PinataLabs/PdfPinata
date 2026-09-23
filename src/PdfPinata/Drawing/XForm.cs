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
using System.Diagnostics;
using PdfPinata.Drawing.Pdf;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Filters;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents a graphical object that can be used to render retained graphics on it.
/// In GDI+ it is represented by a Metafile, in WPF by a DrawingVisual, and in PDF by a Form XObjects.
/// </summary>
public class XForm : XImage, IContentStream
{
    internal enum FormState
    {
        /// <summary>
        /// The form is an imported PDF page.
        /// </summary>
        NotATemplate,

        /// <summary>
        /// The template is just created.
        /// </summary>
        Created,

        /// <summary>
        /// XGraphics.FromForm() was called.
        /// </summary>
        UnderConstruction,

        /// <summary>
        /// The form was drawn at least once and is 'frozen' now.
        /// </summary>
        Finished
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XForm"/> class.
    /// </summary>
    protected XForm()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="XForm"/> class that represents a page of a PDF document.
    /// </summary>
    /// <param name="document">The PDF document.</param>
    /// <param name="viewBox">The view box of the page.</param>
    public XForm(PdfDocument document, XRect viewBox)
    {
        if (viewBox.Width < 1 || viewBox.Height < 1)
            throw new ArgumentNullException(nameof(viewBox), "The size of the XPdfForm is to small.");
        // I must tie the XPdfForm to a document immediately, because otherwise I would have no place where
        // to store the resources.

        _formState = FormState.Created;
        Owner = document ?? throw new ArgumentNullException(nameof(document), "An XPdfForm template must be associated with a document at creation time.");
        _pdfForm = new PdfFormXObject(document);
        _viewBox = viewBox;
        var rect = new PdfRectangle(viewBox);
        _pdfForm.Elements.SetRectangle(PdfFormXObject.Keys.BBox, rect);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XForm"/> class that represents a page of a PDF document.
    /// </summary>
    /// <param name="document">The PDF document.</param>
    /// <param name="size">The size of the page.</param>
    public XForm(PdfDocument document, XSize size)
        : this(document, new XRect(0, 0, size.Width, size.Height))
    {
        ////if (size.width < 1 || size.height < 1)
        ////  throw new ArgumentNullException("size", "The size of the XPdfForm is to small.");
        ////// I must tie the XPdfForm to a document immediately, because otherwise I would have no place where
        ////// to store the resources.
        ////if (document == null)
        ////  throw new ArgumentNullException("document", "An XPdfForm template must be associated with a document.");

        ////_formState = FormState.Created;
        ////_document = document;
        ////pdfForm = new PdfFormXObject(document, this);
        ////templateSize = size;
        ////PdfRectangle rect = new PdfRectangle(new XPoint(), size);
        ////pdfForm.Elements.SetRectangle(PdfFormXObject.Keys.BBox, rect);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XForm"/> class that represents a page of a PDF document.
    /// </summary>
    /// <param name="document">The PDF document.</param>
    /// <param name="width">The width of the page.</param>
    /// <param name="height">The height of the page</param>
    public XForm(PdfDocument document, XUnit width, XUnit height)
        : this(document, new XRect(0, 0, width, height))
    { }

    /// <summary>
    /// This function should be called when drawing the content of this form is finished.
    /// The XGraphics object used for drawing the content is disposed by this function and
    /// cannot be used for any further drawing operations.
    /// PdfPinata automatically calls this function when this form was used the first time
    /// in a DrawImage function.
    /// </summary>
    public void DrawingFinished()
    {
        if (_formState == FormState.Finished)
            return;

        if (_formState == FormState.NotATemplate)
            throw new InvalidOperationException("This object is an imported PDF page and you cannot finish drawing on it because you must not draw on it at all.");

        Finish();
    }

    /// <summary>
    /// Called from XGraphics constructor that creates an instance that work on this form.
    /// </summary>
    internal void AssociateGraphics(XGraphics gfx)
    {
        if (_formState == FormState.NotATemplate)
            throw new NotImplementedException("The current version of PdfPinata cannot draw on an imported page.");

        if (_formState == FormState.UnderConstruction)
            throw new InvalidOperationException("An XGraphics object already exists for this form.");

        if (_formState == FormState.Finished)
            throw new InvalidOperationException("After drawing a form it cannot be modified anymore.");

        Debug.Assert(_formState == FormState.Created);
        _formState = FormState.UnderConstruction;
        Gfx = gfx;
    }
    internal XGraphics Gfx;

    /// <summary>
    /// Sets the form in the state FormState.Finished.
    /// </summary>
    internal virtual void Finish()
    {
        if (_formState == FormState.NotATemplate || _formState == FormState.Finished)
            return;

        if (!(_formState == FormState.Created || _formState == FormState.UnderConstruction))
        {
            throw new InvalidOperationException("Expected the form to be Created or UnderConstruction");
        }

        _formState = FormState.Finished;

        // A form that was never drawn on has no XGraphics to dispose. That is not a mistake: an
        // empty form is an empty content stream, which is exactly what the "off" appearance of a
        // check box or a radio button is. Finishing one threw a NullReferenceException.
        Gfx?.Dispose();
        Gfx = null;

        if (PdfRenderer == null)
            return;

        PdfRenderer.Close();

        if (Owner.Options.CompressContentStreams)
        {
            _pdfForm.Stream.Value = Filtering.FlateDecode.Encode(_pdfForm.Stream.Value, Owner.Options.FlateEncodeMode);
            _pdfForm.Elements["/Filter"] = new PdfName("/FlateDecode");
        }
        var length = _pdfForm.Stream.Length;
        _pdfForm.Elements.SetInteger("/Length", length);
    }

    /// <summary>
    /// Gets the owning document.
    /// </summary>
    internal PdfDocument Owner { get; }

    /// <summary>
    /// Gets the color model used in the underlying PDF document.
    /// </summary>
    internal PdfColorMode ColorMode
    {
        get
        {
            return Owner == null ? PdfColorMode.Undefined : Owner.Options.ColorMode;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is a template.
    /// </summary>
    internal bool IsTemplate => _formState != FormState.NotATemplate;

    internal FormState _formState;

    /// <summary>
    /// Get the width in point of this image.
    /// </summary>
    public override double PointWidth => _viewBox.Width;

    /// <summary>
    /// Get the height in point of this image.
    /// </summary>
    public override double PointHeight => _viewBox.Height;

    /// <summary>
    /// Get the width of the page identified by the property PageNumber.
    /// </summary>
    public override int PixelWidth => (int)_viewBox.Width;

    /// <summary>
    /// Get the height of the page identified by the property PageNumber.
    /// </summary>
    public override int PixelHeight => (int)_viewBox.Height;

    /// <summary>
    /// Get the size of the page identified by the property PageNumber.
    /// </summary>
    public override XSize Size => _viewBox.Size;

    /// <summary>
    /// Gets the view box of the form.
    /// </summary>
    public XRect ViewBox => _viewBox;

    private XRect _viewBox;

    /// <summary>
    /// Gets 72, the horizontal resolution by design of a form object.
    /// </summary>
    public override double HorizontalResolution => 72;

    /// <summary>
    /// Gets 72 always, the vertical resolution by design of a form object.
    /// </summary>
    public override double VerticalResolution => 72;

    /// <summary>
    /// Gets or sets the bounding box.
    /// </summary>
    public XRect BoundingBox { get; set; }

    /// <summary>
    /// Gets or sets the transformation matrix.
    /// </summary>
    public virtual XMatrix Transform
    {
        get => _transform;
        set
        {
            if (_formState == FormState.Finished)
                throw new InvalidOperationException("After a XPdfForm was once drawn it must not be modified.");
            _transform = value;
        }
    }
    internal XMatrix _transform;

    internal PdfResources Resources
    {
        get
        {
            Debug.Assert(IsTemplate, "This function is for form templates only.");
            return PdfForm.Resources;
        }
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    PdfResources IContentStream.Resources => Resources;

    /// <summary>
    /// Gets the resource name of the specified font within this form.
    /// </summary>
    internal string GetFontName(XFont font, out PdfFont pdfFont)
    {
        Debug.Assert(IsTemplate, "This function is for form templates only.");
        pdfFont = Owner.FontTable.GetFont(font);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(XFont font, out PdfFont pdfFont)
    {
        return GetFontName(font, out pdfFont);
    }

    /// <summary>
    /// Tries to get the resource name of the specified font data within this form.
    /// Returns null if no such font exists.
    /// </summary>
    internal string TryGetFontName(string idName, out PdfFont pdfFont)
    {
        Debug.Assert(IsTemplate, "This function is for form templates only.");
        pdfFont = Owner.FontTable.TryGetFont(idName);
        string name = null;
        if (pdfFont != null)
            name = Resources.AddFont(pdfFont);
        return name;
    }

    /// <summary>
    /// Gets the resource name of the specified font data within this form.
    /// </summary>
    internal string GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        Debug.Assert(IsTemplate, "This function is for form templates only.");
        pdfFont = Owner.FontTable.GetFont(idName, fontData);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        return GetFontName(idName, fontData, out pdfFont);
    }

    /// <summary>
    /// Gets the resource name of the specified image within this form.
    /// </summary>
    internal string GetImageName(XImage image)
    {
        Debug.Assert(IsTemplate, "This function is for form templates only.");
        var pdfImage = Owner.ImageTable.GetImage(image);
        Debug.Assert(pdfImage != null);
        var name = Resources.AddImage(pdfImage);
        return name;
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    string IContentStream.GetImageName(XImage image)
    {
        return GetImageName(image);
    }

    internal PdfFormXObject PdfForm
    {
        get
        {
            Debug.Assert(IsTemplate, "This function is for form templates only.");
            if (_pdfForm.Reference == null)
                Owner._irefTable.Add(_pdfForm);
            return _pdfForm;
        }
    }

    /// <summary>
    /// Gets the resource name of the specified form within this form.
    /// </summary>
    internal string GetFormName(XForm form)
    {
        Debug.Assert(IsTemplate, "This function is for form templates only.");
        var pdfForm = Owner.FormTable.GetForm(form);
        Debug.Assert(pdfForm != null);
        var name = Resources.AddForm(pdfForm);
        return name;
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    string IContentStream.GetFormName(XForm form)
    {
        return GetFormName(form);
    }

    /// <summary>
    /// The PdfFormXObject gets invalid when PageNumber or transform changed. This is because a modification
    /// of an XPdfForm must not change objects that are already been drawn.
    /// </summary>
    internal PdfFormXObject _pdfForm;

    internal XGraphicsPdfRenderer PdfRenderer;

}
