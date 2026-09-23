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
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents a link annotation.
/// </summary>
public sealed class PdfLinkAnnotation : PdfAnnotation
{
    // Just a hack to make PinataLayout work with this code.
    private enum LinkType
    {
        None, Document, Web, File, Named
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLinkAnnotation"/> class.
    /// </summary>
    public PdfLinkAnnotation()
    {
        _linkType = LinkType.None;
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Link");
        Flags = PdfAnnotationFlags.Print;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLinkAnnotation"/> class.
    /// </summary>
    public PdfLinkAnnotation(PdfDocument document)
        : base(document)
    {
        _linkType = LinkType.None;
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Link");
        Flags = PdfAnnotationFlags.Print;
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfLinkAnnotation(PdfDictionary dict)
        : base(dict)
    {
        _readFromFile = true;
    }

    /// <summary>
    /// Whether this link was read from a file rather than made here, in which case its border is
    /// whatever the file says - including nothing, which ISO 32000-1 reads as a one-point border -
    /// and the zero-width default below is not this class's to add.
    /// </summary>
    private readonly bool _readFromFile;

    /// <summary>
    /// Creates a link within the current document.
    /// </summary>
    /// <param name="rect">The link area in default page coordinates.</param>
    /// <param name="destinationPage">The one-based destination page number.</param>
    public static PdfLinkAnnotation CreateDocumentLink(PdfRectangle rect, int destinationPage)
    {
        return CreateDocumentLink(rect, destinationPage, double.NaN);
    }

    /// <summary>
    /// Creates a link to a place on a page within the current document.
    /// </summary>
    /// <param name="rect">The link area in default page coordinates.</param>
    /// <param name="destinationPage">The one-based destination page number.</param>
    /// <param name="destinationTop">
    /// How far up the destination page to place the top of the window, in default page
    /// coordinates. NaN leaves the position alone, which lands the reader wherever on the page
    /// it happens to be scrolled to.
    /// </param>
    public static PdfLinkAnnotation CreateDocumentLink(PdfRectangle rect, int destinationPage, double destinationTop)
    {
        if (destinationPage < 1)
            throw new ArgumentException("Invalid destination page in call to CreateDocumentLink: page number is one-based and must be 1 or higher.", nameof(destinationPage));

        var link = new PdfLinkAnnotation
        {
            _linkType = LinkType.Document,
            Rectangle = rect,
            _destPage = destinationPage,
            _destTop = destinationTop
        };
        return link;
    }
    private int _destPage;
    private double _destTop = double.NaN;
    private LinkType _linkType;
    private string _url;

    /// <summary>
    /// Creates a link to the web.
    /// </summary>
    public static PdfLinkAnnotation CreateWebLink(PdfRectangle rect, string url)
    {
        var link = new PdfLinkAnnotation
        {
            _linkType = LinkType.Web,
            Rectangle = rect,
            _url = url
        };
        return link;
    }

    /// <summary>
    /// Creates a link to a named destination of the current document.
    /// </summary>
    /// <param name="rect">The link area in default page coordinates.</param>
    /// <param name="destinationName">
    /// The name, as given to <see cref="PdfDocument.NamedDestinations"/>. A link to a name that
    /// the document never names is written all the same and does nothing when followed - which is
    /// what a reader does with a broken link, and better than refusing to write the document.
    /// </param>
    public static PdfLinkAnnotation CreateNamedLink(PdfRectangle rect, string destinationName)
    {
        if (string.IsNullOrEmpty(destinationName))
            throw new ArgumentException("A named link must name something.", nameof(destinationName));

        var link = new PdfLinkAnnotation
        {
            _linkType = LinkType.Named,
            Rectangle = rect,
            _destName = destinationName
        };
        return link;
    }
    private string _destName;

    /// <summary>
    /// Creates a link to a file.
    /// </summary>
    public static PdfLinkAnnotation CreateFileLink(PdfRectangle rect, string fileName)
    {
        var link = new PdfLinkAnnotation
        {
            _linkType = LinkType.File,
            Rectangle = rect,
            _url = fileName
        };
        return link;
    }

    internal override void WriteObject(PdfWriter writer)
    {
        // Older Adobe Reader versions uses a border width of 0 as default value if neither Border nor BS are present.
        // But the PDF Reference specifies:
        // "If neither the Border nor the BS entry is present, the border is drawn as a solid line with a width of 1 point."
        // After this issue was fixed in newer Reader versions older PDFsharp created documents show an ugly solid border.
        // The following hack fixes this by specifying a 0 width border.
        if (!_readFromFile)
        {
            Elements[PdfAnnotation.Keys.BS] ??= new PdfLiteral("<</Type/Border/W 0>>");

            // May be superfluous. See comment above.
            Elements[PdfAnnotation.Keys.Border] ??= new PdfLiteral("[0 0 0]");
        }

        switch (_linkType)
        {
            case LinkType.None:
                break;

            case LinkType.Document:
                // destIndex > Owner.PageCount can happen when rendering pages using PDFsharp directly.
                var destIndex = _destPage;
                if (destIndex > Owner.PageCount)
                    destIndex = Owner.PageCount;
                destIndex--;
                var dest = Owner.Pages[destIndex];
                // A destination without a top lands the reader wherever the page is already
                // scrolled to, so a link to a place halfway down a page needs the coordinate.
                Elements[Keys.Dest] = double.IsNaN(_destTop)
                    ? new PdfLiteral("[{0} 0 R/XYZ null null 0]", dest.ObjectNumber)
                    : new PdfLiteral("[{0} 0 R/XYZ null {1} 0]", dest.ObjectNumber,
                        PdfEncoders.Format("{0:0.###}", _destTop));
                break;

            case LinkType.Named:
                // A destination written as a string is looked up in the /Names /Dests name tree,
                // which is where PDF 1.2 onwards puts them. Writing it as a name instead would
                // send the reader to the /Dests dictionary of PDF 1.1.
                //
                // A PdfString rather than a literal encoded here, so that the name is encoded the
                // way PdfNamedDestinationTable encodes the one it writes into the tree - which is
                // PdfString's own choice of raw or Unicode. Pinning this end to WinAnsi would turn
                // every character outside it into a question mark and leave the link pointing at a
                // name the document does not hold.
                Elements[Keys.Dest] = new PdfString(_destName);
                break;

            case LinkType.Web:
                Elements[PdfAnnotation.Keys.A] = new PdfLiteral("<</S/URI/URI{0}>>",
                    PdfEncoders.ToStringLiteral(_url, PdfStringEncoding.WinAnsiEncoding, writer.SecurityHandler));
                break;

            case LinkType.File:
                Elements[PdfAnnotation.Keys.A] = new PdfLiteral("<</Type/Action/S/Launch/F<</Type/Filespec/F{0}>> >>",
                    PdfEncoders.ToStringLiteral(_url, PdfStringEncoding.WinAnsiEncoding, writer.SecurityHandler));
                break;
        }
        base.WriteObject(writer);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        /// <summary>
        /// (Optional; not permitted if an A entry is present) A destination to be displayed
        /// when the annotation is activated.
        /// </summary>
        [KeyInfo(KeyType.ArrayOrNameOrString | KeyType.Optional)]
        public const string Dest = "/Dest";

        /// <summary>
        /// (Optional; PDF 1.2) The annotation�s highlighting mode, the visual effect to be
        /// used when the mouse button is pressed or held down inside its active area:
        /// N (None) No highlighting.
        /// I (Invert) Invert the contents of the annotation rectangle.
        /// O (Outline) Invert the annotation�s border.
        /// P (Push) Display the annotation as if it were being pushed below the surface of the page.
        /// Default value: I.
        /// Note: In PDF 1.1, highlighting is always done by inverting colors inside the annotation rectangle.
        /// </summary>
        [KeyInfo("1.2", KeyType.Name | KeyType.Optional)]
        public const string H = "/H";

        /// <summary>
        /// (Optional; PDF 1.3) A URI action formerly associated with this annotation. When Web
        /// Capture changes and annotation from a URI to a go-to action, it uses this entry to save
        /// the data from the original URI action so that it can be changed back in case the target page for
        /// the go-to action is subsequently deleted.
        /// </summary>
        [KeyInfo("1.3", KeyType.Dictionary | KeyType.Optional)]
        public const string PA = "/PA";

        // QuadPoints

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
