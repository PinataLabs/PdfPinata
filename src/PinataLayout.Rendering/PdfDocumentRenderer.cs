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
using System.Reflection;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using PdfPinata.Drawing;
using PinataLayout.Rendering.Resources;

using PdfPinata;

namespace PinataLayout.Rendering;

/// <summary>
/// Provides the functionality to convert a PinataLayout document into PDF.
/// </summary>
public class PdfDocumentRenderer
{
    /// <summary>
    /// Initializes a new instance of the PdfDocumentRenderer class.
    /// </summary>
    public PdfDocumentRenderer()
    {
    }

    /// <summary>
    /// Initializes a new instance of the PdfDocumentRenderer class.
    /// </summary>
    /// <param name="unicode">If true Unicode encoding is used for all text. If false, WinAnsi encoding is used.</param>
    public PdfDocumentRenderer(bool unicode)
    {
        this._unicode = unicode;
    }

    /// <summary>
    /// Gets a value indicating whether the text is rendered as Unicode.
    /// </summary>
    public bool Unicode => _unicode;

    private readonly bool _unicode;

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    /// <value>The language.</value>
    /// <remarks>
    /// An RFC 3066 tag such as "en-GB". Written to the catalog, and required by PDF/UA — a reader
    /// that does not know what language a document is in cannot choose a voice to read it in.
    /// </remarks>
    public string Language
    {
        get => _language;
        set => _language = value;
    }

    private string _language = String.Empty;

    /// <summary>
    /// Gets or sets whether the rendered PDF carries a structure tree describing it. The default is
    /// <c>true</c>. See <see cref="PinataLayout.Rendering.DocumentRenderer.TagContent"/>.
    /// </summary>
    public bool TagContent
    {
        get => _tagContent;
        set
        {
            _tagContent = value;
            if (_documentRenderer != null)
                _documentRenderer.TagContent = value;
        }
    }

    private bool _tagContent = true;

    /// <summary>
    /// Set the PinataLayout document to be rendered by this printer.
    /// </summary>
    public Document Document
    {
        set
        {
            _document = null;
            value.BindToRenderer(this);
            _document = value;
        }
    }

    private Document _document;

    /// <summary>
    /// Gets or sets a document renderer.
    /// </summary>
    /// <remarks>
    /// A document renderer is automatically created and prepared
    /// when printing before this property was set.
    /// </remarks>
    public DocumentRenderer DocumentRenderer
    {
        get
        {
            if (_documentRenderer == null)
                PrepareDocumentRenderer();
            return _documentRenderer;
        }
        set => _documentRenderer = value;
    }

    private DocumentRenderer _documentRenderer;

    private void PrepareDocumentRenderer()
    {
        PrepareDocumentRenderer(false);
    }

    private void PrepareDocumentRenderer(bool prepareCompletely)
    {
        if (_document == null)
            throw new InvalidOperationException(string.Format(AppResources.PropertyNotSetBefore, "DocumentRenderer", nameof(PrepareDocumentRenderer)));

        if (_documentRenderer == null)
        {
            _documentRenderer = new DocumentRenderer(_document);
            _documentRenderer.WorkingDirectory = _workingDirectory;
        }

        _documentRenderer.TagContent = _tagContent;

        // The document's language belongs on the catalog and in the structure tree both, and it is
        // set here rather than in CreatePdfDocument because a caller supplying their own PdfDocument
        // never goes through that.
        if (!string.IsNullOrEmpty(_language))
            _documentRenderer.Tagger.Language = _language;
        if (prepareCompletely && _documentRenderer.formattedDocument == null)
        {
            _documentRenderer.PrepareDocument();
        }
    }

    /// <summary>
    /// Renders the document into a PdfDocument containing all pages of the document.
    /// </summary>
    public void RenderDocument()
    {
        PrepareRenderPages();
        RenderPages(1, _documentRenderer.FormattedDocument.PageCount);
    }

    /// <summary>
    /// Renders the document into a PdfDocument containing all pages of the document.
    /// </summary>
    public void PrepareRenderPages()
    {
        PrepareDocumentRenderer(true);

        if (_pdfDocument == null)
        {
            _pdfDocument = CreatePdfDocument();
            if (_document.UseCmykColor)
                _pdfDocument.Options.ColorMode = PdfColorMode.Cmyk;
        }

        WriteDocumentInformation();
    }

    /// <summary>
    /// Gets the count of pages.
    /// </summary>
    public int PageCount => _documentRenderer.FormattedDocument.PageCount;

    /// <summary>
    /// Saves the PdfDocument to the specified path. If a file already exists, it will be overwritten.
    /// <para>
    /// A relative path is resolved against <see cref="WorkingDirectory"/> when one is set, and
    /// against the process's current directory otherwise. An absolute path is used as it stands.
    /// </para>
    /// </summary>
    public void Save(string path)
    {
        switch (path)
        {
            case null:
                throw new ArgumentNullException(nameof(path));
            case "":
                throw new ArgumentException("PDF file Path must not be empty");
        }

        // The combined path, which is what this line was for. It used to throw the result away, so
        // the working directory decided nothing and every relative path was written against whatever
        // the process's current directory happened to be. A caller passing an absolute path is
        // unaffected either way: Path.Combine answers one with itself.
        if (_workingDirectory != null)
            path = Path.Combine(_workingDirectory, path);

        _pdfDocument.Save(path);
    }

    /// <summary>
    /// Saves the PDF document to the specified stream.
    /// </summary>
    public void Save(Stream stream, bool closeStream)
    {
        _pdfDocument.Save(stream, closeStream);
    }

    /// <summary>
    /// Renders the spcified page range.
    /// </summary>
    /// <param name="startPage">The first page to print.</param>
    /// <param name="endPage">The last page to print</param>
    public void RenderPages(int startPage, int endPage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startPage, 1);

        // Formatting is what produces the page count, so a renderer nobody prepared is prepared
        // here rather than failing on a null formatted document.
        if (_documentRenderer?.FormattedDocument == null)
            PrepareRenderPages();

        // ReSharper disable PossibleNullReferenceException
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endPage, _documentRenderer.FormattedDocument.PageCount);
        // ReSharper restore PossibleNullReferenceException

        _pdfDocument ??= CreatePdfDocument();

        _documentRenderer.printDate = GlobalTimeSettings.Now;
        for (var pageNr = startPage; pageNr <= endPage; ++pageNr)
        {
            var pdfPage = _pdfDocument.AddPage();
            var pageInfo = _documentRenderer.FormattedDocument.GetPageInfo(pageNr);
            pdfPage.Width = pageInfo.Width;
            pdfPage.Height = pageInfo.Height;
            pdfPage.Orientation = pageInfo.Orientation;

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            gfx.MUH = _unicode ? PdfFontEncoding.Unicode : PdfFontEncoding.WinAnsi;
            _documentRenderer.RenderPage(gfx, pageNr);
        }
    }

    /// <summary>
    /// Gets or sets the directory a relative path given to <see cref="Save(string)"/> is resolved
    /// against. Unset, a relative path is resolved against the process's current directory.
    /// </summary>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => _workingDirectory = value;
    }

    private string _workingDirectory;

    /// <summary>
    /// Gets or sets the PDF document to render on.
    /// </summary>
    /// <remarks>A PDF document in memory is automatically created when printing before this property was set.</remarks>
    public PdfDocument PdfDocument
    {
        get => _pdfDocument;
        set => _pdfDocument = value;
    }

    private PdfDocument _pdfDocument;

    /// <summary>
    /// Writes document information like author and subject to the PDF document.
    /// </summary>
    public void WriteDocumentInformation()
    {
        if (!_document.IsNull("Info"))
        {
            var docInfo = _document.Info;
            var pdfInfo = _pdfDocument.Info;

            if (!docInfo.IsNull("Author"))
                pdfInfo.Author = docInfo.Author;

            if (!docInfo.IsNull("Keywords"))
                pdfInfo.Keywords = docInfo.Keywords;

            if (!docInfo.IsNull("Subject"))
                pdfInfo.Subject = docInfo.Subject;

            if (!docInfo.IsNull("Title"))
                pdfInfo.Title = docInfo.Title;
        }
    }

    /// <summary>
    /// Creates a new PDF document.
    /// </summary>
    private PdfDocument CreatePdfDocument()
    {
        var pdfDocument = new PdfDocument();
        pdfDocument.Info.Creator = "PinataLayout " + typeof(PdfDocumentRenderer).GetTypeInfo().Assembly.GetName().Version;
        if (_language != null && _language.Length != 0)
            pdfDocument.Language = _language;
        return pdfDocument;
    }
}
