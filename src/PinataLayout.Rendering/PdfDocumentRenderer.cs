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
        this.unicode = unicode;
    }

    /// <summary>
    /// Gets a value indicating whether the text is rendered as Unicode.
    /// </summary>
    public bool Unicode => unicode;

    bool unicode;

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
        get => language;
        set => language = value;
    }
    string language = String.Empty;

    /// <summary>
    /// Gets or sets whether the rendered PDF carries a structure tree describing it. The default is
    /// <c>true</c>. See <see cref="PinataLayout.Rendering.DocumentRenderer.TagContent"/>.
    /// </summary>
    public bool TagContent
    {
        get => tagContent;
        set
        {
            tagContent = value;
            if (documentRenderer != null)
                documentRenderer.TagContent = value;
        }
    }
    bool tagContent = true;

    /// <summary>
    /// Set the PinataLayout document to be rendered by this printer.
    /// </summary>
    public Document Document
    {
        set
        {
            document = null;
            value.BindToRenderer(this);
            document = value;
        }
    }
    Document document;

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
            if (documentRenderer == null)
                PrepareDocumentRenderer();
            return documentRenderer;
        }
        set => documentRenderer = value;
    }
    DocumentRenderer documentRenderer;

    void PrepareDocumentRenderer()
    {
        PrepareDocumentRenderer(false);
    }

    void PrepareDocumentRenderer(bool prepareCompletely)
    {
        if (document == null)
            throw new InvalidOperationException(string.Format(AppResources.PropertyNotSetBefore, "DocumentRenderer", nameof(PrepareDocumentRenderer)));

        if (documentRenderer == null)
        {
            documentRenderer = new DocumentRenderer(document);
            documentRenderer.WorkingDirectory = workingDirectory;
        }

        documentRenderer.TagContent = tagContent;

        // The document's language belongs on the catalog and in the structure tree both, and it is
        // set here rather than in CreatePdfDocument because a caller supplying their own PdfDocument
        // never goes through that.
        if (!string.IsNullOrEmpty(language))
            documentRenderer.Tagger.Language = language;
        if (prepareCompletely && documentRenderer.formattedDocument == null)
        {
            documentRenderer.PrepareDocument();
        }
    }

    /// <summary>
    /// Renders the document into a PdfDocument containing all pages of the document.
    /// </summary>
    public void RenderDocument()
    {
        PrepareRenderPages();
        RenderPages(1, documentRenderer.FormattedDocument.PageCount);
    }

    /// <summary>
    /// Renders the document into a PdfDocument containing all pages of the document.
    /// </summary>
    public void PrepareRenderPages()
    {
        //if (this.documentRenderer == null)
        PrepareDocumentRenderer(true);

        if (pdfDocument == null)
        {
            pdfDocument = CreatePdfDocument();
            if (document.UseCmykColor)
                pdfDocument.Options.ColorMode = PdfColorMode.Cmyk;
        }

        WriteDocumentInformation();
        //RenderPages(1, this.documentRenderer.FormattedDocument.PageCount);
    }

    /// <summary>
    /// Gets the count of pages.
    /// </summary>
    public int PageCount => documentRenderer.FormattedDocument.PageCount;

    /// <summary>
    /// Saves the PdfDocument to the specified path. If a file already exists, it will be overwritten.
    /// </summary>
    public void Save(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        else if (path == "")
            throw new ArgumentException("PDF file Path must not be empty");

        if (workingDirectory != null)
            Path.Combine(workingDirectory, path);

        pdfDocument.Save(path);
    }

    /// <summary>
    /// Saves the PDF document to the specified stream.
    /// </summary>
    public void Save(Stream stream, bool closeStream)
    {
        pdfDocument.Save(stream, closeStream);
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
        if (documentRenderer?.FormattedDocument == null)
            PrepareRenderPages();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(endPage, documentRenderer.FormattedDocument.PageCount);

        if (pdfDocument == null)
            pdfDocument = CreatePdfDocument();

        documentRenderer.printDate = GlobalTimeSettings.Now;
        for (int pageNr = startPage; pageNr <= endPage; ++pageNr)
        {
            PdfPage pdfPage = pdfDocument.AddPage();
            PageInfo pageInfo = documentRenderer.FormattedDocument.GetPageInfo(pageNr);
            pdfPage.Width = pageInfo.Width;
            pdfPage.Height = pageInfo.Height;
            pdfPage.Orientation = pageInfo.Orientation;

            using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
            {
                gfx.MUH = unicode ? PdfFontEncoding.Unicode : PdfFontEncoding.WinAnsi;
                documentRenderer.RenderPage(gfx, pageNr);
            }
        }
    }

    /// <summary>
    /// Gets or sets a working directory for the printing process.
    /// </summary>
    public string WorkingDirectory
    {
        get => workingDirectory;
        set => workingDirectory = value;
    }
    string workingDirectory;

    /// <summary>
    /// Gets or sets the PDF document to render on.
    /// </summary>
    /// <remarks>A PDF document in memory is automatically created when printing before this property was set.</remarks>
    public PdfDocument PdfDocument
    {
        get => pdfDocument;
        set => pdfDocument = value;
    }
    PdfDocument pdfDocument;

    /// <summary>
    /// Writes document information like author and subject to the PDF document.
    /// </summary>
    public void WriteDocumentInformation()
    {
        if (!document.IsNull("Info"))
        {
            DocumentInfo docInfo = document.Info;
            PdfDocumentInformation pdfInfo = pdfDocument.Info;

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
    PdfDocument CreatePdfDocument()
    {
        PdfDocument document = new PdfDocument();
        document.Info.Creator = "PinataLayout " + typeof(PdfDocumentRenderer).GetTypeInfo().Assembly.GetName().Version;
        if (language != null && language.Length != 0)
            document.Language = language;
        return document;
    }
}
