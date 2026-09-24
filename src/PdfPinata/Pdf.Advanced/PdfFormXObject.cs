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
using PdfPinata.Drawing;
using PdfPinata.Pdf.Filters;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents an external form object (e.g. an imported page).
/// </summary>
public sealed class PdfFormXObject : PdfXObject, IContentStream
{
    internal PdfFormXObject(PdfDocument thisDocument)
        : base(thisDocument)
    {
        Elements.SetName(Keys.Type, "/XObject");
        Elements.SetName(Keys.Subtype, "/Form");
    }

    /// <summary>
    /// The key marking a form as one a page resize wrapped a page's content in. Private to
    /// PdfPinata; a reader that does not know it ignores it, as PDF requires of any key it
    /// does not recognise.
    /// <para>
    /// The rectangle the content occupied when it was wrapped is not recorded separately: it is
    /// the /BBox, by construction. A second resize of the same page reads it from there and works
    /// out a fresh transform against it, rather than compounding one transform onto another.
    /// </para>
    /// </summary>
    internal const string ResizeWrapperKey = "/PdfPinataResizeWrapper";

    /// <summary>
    /// Initializes a form holding the content of a page of the <b>same</b> document, which is how
    /// a page resize gets a transform in front of everything the page draws.
    /// <para>
    /// Nothing is imported and nothing is copied. The page's resources are handed straight over -
    /// they may be shared with other pages, and moving the reference leaves what is shared
    /// untouched - and the content stream is moved as it stands, filter and all, so that a
    /// compressed page is not decompressed and recompressed just to be moved.
    /// </para>
    /// <para>
    /// The caller is expected to give the page a resource dictionary naming this form, and a
    /// content stream that draws it, immediately afterwards. Until it does, the page has content
    /// that is no longer reachable from it.
    /// </para>
    /// </summary>
    /// <param name="thisDocument">The document that owns both the page and this form.</param>
    /// <param name="page">The page whose content is to be moved into this form.</param>
    /// <param name="boundingBox">
    /// The rectangle the content occupies, which becomes the form's /BBox. A form clips to its
    /// bounding box, so this is also what keeps a page that drew outside its own box from
    /// suddenly showing that content once the page around it changes size.
    /// </param>
    internal PdfFormXObject(PdfDocument thisDocument, PdfPage page, PdfRectangle boundingBox)
        : base(thisDocument)
    {
        Debug.Assert(page != null);
        Debug.Assert(ReferenceEquals(thisDocument, page.Owner));

        Elements.SetName(Keys.Type, "/XObject");
        Elements.SetName(Keys.Subtype, "/Form");
        Elements.SetRectangle("/BBox", boundingBox);
        Elements[ResizeWrapperKey] = new PdfBoolean(true);

        // The same document, so the resources need no importing. Handing the reference over
        // leaves a dictionary shared with other pages exactly as it was.
        var resources = page.Elements[PdfPage.InheritablePageKeys.Resources];
        if (resources != null)
            Elements[Keys.Resources] = resources;

        // A transparency group left behind on the page would no longer wrap the content that
        // needed it, so it travels with the content.
        var group = page.Elements[PdfPage.Keys.Group];
        if (group != null)
            Elements["/Group"] = group;

        TakeContentOf(page);
    }

    /// <summary>
    /// Moves the content of the page into this form.
    /// <para>
    /// A page with one content stream - which is nearly every page - hands its bytes over exactly
    /// as they are, filter included, so nothing is decoded and nothing is re-encoded. A page with
    /// several has to have them run together, and that cannot be done without decoding them, so
    /// the result is compressed again only if the document is set to compress content.
    /// </para>
    /// </summary>
    private void TakeContentOf(PdfPage page)
    {
        var item = Resolve(page.Elements[PdfPage.Keys.Contents]);
        if (item is PdfArray { Elements.Count: > 1 })
        {
            TakeRunTogetherContentOf(page);
            return;
        }

        var single = item switch
        {
            PdfArray { Elements.Count: 1 } array => Resolve(array.Elements[0]) as PdfDictionary,
            _ => item as PdfDictionary
        };

        if (single?.Stream == null)
        {
            // A page with nothing on it. The form is empty rather than absent, so that the page
            // still draws something well formed.
            Stream = new PdfStream([], this);
            Elements.SetInteger("/Length", 0);
            return;
        }

        // Verbatim: the bytes as they are held, with whatever filter is undoing them.
        var filter = single.Elements["/Filter"];
        if (filter != null)
            Elements["/Filter"] = filter.Clone();

        var decodeParms = single.Elements["/DecodeParms"];
        if (decodeParms != null)
            Elements["/DecodeParms"] = decodeParms.Clone();

        Stream = new PdfStream(single.Stream.Value, this);
        Elements.SetInteger("/Length", single.Stream.Value.Length);
    }

    /// <summary>
    /// Runs the several content streams of a page together into this form's single stream.
    /// </summary>
    private void TakeRunTogetherContentOf(PdfPage page)
    {
        // CreateSingleContent decodes as it concatenates, so what comes back is unfiltered.
        var joined = page.Contents.CreateSingleContent();
        var bytes = joined.Stream.Value;

        if (Owner.Options.CompressContentStreams)
        {
            bytes = Filtering.FlateDecode.Encode(bytes, Owner.Options.FlateEncodeMode);
            Elements.SetName("/Filter", "/FlateDecode");
        }

        Stream = new PdfStream(bytes, this);
        Elements.SetInteger("/Length", bytes.Length);
    }

    internal double DpiX { get; set; } = 72;

    internal double DpiY { get; set; } = 72;

    internal PdfFormXObject(PdfDocument thisDocument, PdfImportedObjectTable importedObjectTable, XPdfForm form)
        : base(thisDocument)
    {
        Debug.Assert(ReferenceEquals(thisDocument, importedObjectTable.Owner));
        Elements.SetName(Keys.Type, "/XObject");
        Elements.SetName(Keys.Subtype, "/Form");

        if (form.IsTemplate)
        {
            Debug.Assert(importedObjectTable == null);
            return;
        }
        Debug.Assert(importedObjectTable != null);

        var pdfForm = form;
        // Get import page
        var importPages = importedObjectTable.ExternalDocument.Pages;
        if (pdfForm.PageNumber < 1 || pdfForm.PageNumber > importPages.Count)
            PSSR.ImportPageNumberOutOfRange(pdfForm.PageNumber, importPages.Count, form.Path);
        var importPage = importPages[pdfForm.PageNumber - 1];

        // Import resources
        var res = importPage.Elements["/Resources"];
        if (res != null) // unlikely but possible
        {
            // Get root object
            var root = res is PdfReference resourcesReference ? resourcesReference.Value : (PdfDictionary)res;
            Elements["/Resources"] = ImportIndirect(importedObjectTable, thisDocument, root);
        }

        // A transparency group belongs to the content it wraps, and the content is being moved
        // into this form. Leaving it behind on the page in the other document would mean the
        // content arrives composited against the wrong backdrop - which is the whole of what a
        // group says - so it is imported along with everything else.
        // A /Group entry that is not a dictionary describes no group. A PDF null is the way a
        // writer says a key is not there, and a page that says nothing has nothing to bring.
        if (Resolve(importPage.Elements[PdfPage.Keys.Group]) is PdfDictionary groupDictionary)
            Elements["/Group"] = ImportIndirect(importedObjectTable, thisDocument, groupDictionary);

        SetBoundingBoxOf(importPage);

        // Preserve filter because the content keeps unmodified
        var content = importPage.Contents.CreateSingleContent();
        var filter = content.Elements["/Filter"];
        if (filter != null)
            Elements["/Filter"] = filter.Clone();

        // (no cloning needed because the bytes keep untouched)
        Stream = content.Stream; // new PdfStream(bytes, this);
        Elements.SetInteger("/Length", content.Stream.Value.Length);
    }

    /// <summary>
    /// Imports an object and everything it reaches, and answers a reference to the copy.
    /// </summary>
    private static PdfReference ImportIndirect(PdfImportedObjectTable importedObjectTable, PdfDocument thisDocument, PdfObject root)
    {
        root = ImportClosure(importedObjectTable, thisDocument, root);
        // If the root was a direct object - a group written straight into the page dictionary,
        // for one - make it indirect.
        if (root.Reference == null)
            thisDocument._irefTable.Add(root);

        Debug.Assert(root.Reference != null);
        return root.Reference;
    }

    /// <summary>
    /// Sets the bounding box to the imported page's media box, taking /Rotate into account.
    /// </summary>
    private void SetBoundingBoxOf(PdfPage importPage)
    {
        var rect = importPage.Elements.GetRectangle(PdfPage.InheritablePageKeys.MediaBox);
        var rotate = importPage.Elements.GetInteger(PdfPage.InheritablePageKeys.Rotate);
        Elements["/BBox"] = rect;
        if (rotate == 0)
            return;

        // Rotate the image such that it is upright
        var matrix = new XMatrix();
        var width = rect.Width;
        var height = rect.Height;
        matrix.RotateAtPrepend(-rotate, new XPoint(width / 2, height / 2));

        // Translate the image such that its center lies on the center of the rotated bounding box
        var offset = (height - width) / 2;
        if (rotate == 90)
            matrix.TranslatePrepend(offset, offset);
        else if (rotate == -90)
            matrix.TranslatePrepend(-offset, -offset);

        Elements.SetMatrix(Keys.Matrix, matrix);
    }

    private static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    /// <summary>
    /// Gets the PdfResources object of this form.
    /// </summary>
    public PdfResources Resources
    {
        get
        {
            field ??= (PdfResources)Elements.GetValue(Keys.Resources, VCF.Create);
            return field;
        }
    }

    PdfResources IContentStream.Resources => Resources;

    internal string GetFontName(XFont font, out PdfFont pdfFont)
    {
        pdfFont = _document.FontTable.GetFont(font);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(XFont font, out PdfFont pdfFont)
    {
        return GetFontName(font, out pdfFont);
    }

    /// <summary>
    /// Gets the resource name of the specified font data within this form XObject.
    /// </summary>
    internal string GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        pdfFont = _document.FontTable.GetFont(idName, fontData);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        return GetFontName(idName, fontData, out pdfFont);
    }

    string IContentStream.GetImageName(XImage image)
    {
        throw new NotImplementedException();
    }

    string IContentStream.GetFormName(XForm form)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public sealed new class Keys : PdfXObject.Keys
    {
        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be XObject for a form XObject.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) The type of XObject that this dictionary describes; must be Form
        /// for a form XObject.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Subtype = "/Subtype";

        /// <summary>
        /// (Optional) A code identifying the type of form XObject that this dictionary
        /// describes. The only valid value defined at the time of publication is 1.
        /// Default value: 1.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string FormType = "/FormType";

        /// <summary>
        /// (Required) An array of four numbers in the form coordinate system, giving the
        /// coordinates of the left, bottom, right, and top edges, respectively, of the
        /// form XObject’s bounding box. These boundaries are used to clip the form XObject
        /// and to determine its size for caching.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Required)]
        public const string BBox = "/BBox";

        /// <summary>
        /// (Optional) An array of six numbers specifying the form matrix, which maps
        /// form space into user space.
        /// Default value: the identity matrix [1 0 0 1 0 0].
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Matrix = "/Matrix";

        /// <summary>
        /// (Optional but strongly recommended; PDF 1.2) A dictionary specifying any
        /// resources (such as fonts and images) required by the form XObject.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResources))]
        public const string Resources = "/Resources";

        /// <summary>
        /// (Optional; PDF 1.4) A group attributes dictionary indicating that the contents
        /// of the form XObject are to be treated as a group and specifying the attributes
        /// of that group (see Section 4.9.2, “Group XObjects”).
        /// Note: If a Ref entry (see below) is present, the group attributes also apply to the
        /// external page imported by that entry, which allows such an imported page to be
        /// treated as a group without further modification.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Group = "/Group";

        /// <summary>
        /// (Optional; PDF 1.4) A reference dictionary identifying a page to be imported from another
        /// PDF file, and for which the form XObject serves as a proxy
        /// (see ISO 32000-1 8.10.4, “Reference XObjects”).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Ref = "/Ref";

        /// <summary>
        /// (Optional; PDF 1.4) A metadata stream containing metadata for the form XObject
        /// (see ISO 32000-1 14.3.2, “Metadata Streams”).
        /// </summary>
        [KeyInfo(KeyType.Stream | KeyType.Optional)]
        public const string Metadata = "/Metadata";

        /// <summary>
        /// (Optional; PDF 1.3) A page-piece dictionary associated with the form XObject
        /// (see ISO 32000-1 14.5, “Page-Piece Dictionaries”).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string PieceInfo = "/PieceInfo";

        /// <summary>
        /// (Required if PieceInfo is present; optional otherwise; PDF 1.3) The date and time when
        /// the form XObject’s contents were most recently modified. If a page-piece dictionary
        /// (PieceInfo) is present, the modification date is used to ascertain which of the
        /// application data dictionaries it contains correspond to the current content of the form.
        /// </summary>
        [KeyInfo(KeyType.Date | KeyType.Optional)]
        public const string LastModified = "/LastModified";

        /// <summary>
        /// (Required if the form XObject is a structural content item; PDF 1.3) The integer key of
        /// the form XObject’s entry in the structural parent tree.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string StructParent = "/StructParent";

        /// <summary>
        /// (Required if the form XObject contains marked-content sequences that are structural
        /// content items; PDF 1.3) The integer key of the form XObject’s entry in the structural
        /// parent tree. At most one of StructParent and StructParents may be present.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string StructParents = "/StructParents";

        /// <summary>
        /// (Optional; PDF 1.2) An OPI version dictionary for the form XObject
        /// (see ISO 32000-1 14.11.7, “Open Prepress Interface (OPI)”).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string OPI = "/OPI";

        /// <summary>
        /// (Optional; PDF 1.5) An optional content group or optional content membership dictionary
        /// specifying the optional content properties for the form XObject
        /// (see ISO 32000-1 8.11, “Optional Content”).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string OC = "/OC";

        /// <summary>
        /// (Required in PDF 1.0; optional otherwise) The name by which this form XObject is
        /// referenced in the XObject subdictionary of the current resource dictionary.
        /// This entry is obsolescent and its use is no longer recommended.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Name = "/Name";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
