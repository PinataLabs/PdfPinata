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

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents a interactive form (or AcroForm), a collection of fields for
/// gathering information interactively from the user.
/// </summary>
public sealed class PdfAcroForm : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of AcroForm.
    /// </summary>
    /// <param name="document">The document the form belongs to.</param>
    /// <remarks>
    /// Constructing one does not attach it to anything.
    /// <see cref="PdfDocument.GetOrCreateAcroForm"/> is what a caller authoring a form wants: it
    /// makes the form, makes it indirect and puts it in the catalogue, and answers the one the
    /// document already has when it has one.
    /// </remarks>
    public PdfAcroForm(PdfDocument document)
        : base(document)
    {
        _document = document;
    }

    internal PdfAcroForm(PdfDictionary dictionary)
        : base(dictionary)
    { }

    /// <summary>
    /// Whether a reader should build the appearance streams of this form's fields for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rarely what a form built here wants. Text fields, combo boxes and list boxes draw their own
    /// appearance, and a caller draws a button's. The flag asks a reader to throw all of those
    /// away and build its own from each widget's <c>/MK</c>, <see cref="DefaultAppearance"/> and
    /// <see cref="DefaultResources"/> - and PDFium, the engine in Chrome and Edge, does exactly
    /// that for every field, buttons included, so a push button whose <c>/MK</c> names only a
    /// caption shows there as bare text (issue #153).
    /// </para>
    /// <para>
    /// Other readers ignore it: Ghostscript, print pipelines and most previewers draw the
    /// appearance streams whatever it says. ISO 32000-2 deprecates it, and PDF/A-2 and PDF/A-3
    /// forbid it to be true. Since text fields wrap, centre and mask their values as a reader
    /// does (issue #155), there is little left for it to do.
    /// </para>
    /// </remarks>
    public bool NeedAppearances
    {
        get => Elements.GetBoolean(Keys.NeedAppearances);
        set => Elements.SetBoolean(Keys.NeedAppearances, value);
    }

    /// <summary>
    /// The default appearance string every field of this form falls back on - <c>/DA</c>, a
    /// fragment of content stream naming the font, the size and the colour a value is drawn in.
    /// </summary>
    /// <remarks>
    /// The font is named by the key it has in <see cref="DefaultResources"/>, so
    /// <c>"/Helv 9 Tf 0 g"</c> wants a <c>/Helv</c> there - which
    /// <see cref="AddStandardFont"/> puts in.
    /// <para>
    /// Zero means auto-size. The appearance a text, combo box or list box field draws for itself
    /// sizes its text to each widget's box (issue #354), but a reader that builds its own drawing
    /// from <c>/DA</c> decides for itself what that means on a multiline field: Ghostscript scales
    /// the first line to the height of the whole box, so a two-line value fills the page. Name a
    /// real size where every reader has to agree.
    /// </para>
    /// </remarks>
    public string DefaultAppearance
    {
        get => Elements.GetString(Keys.DA);
        set => Elements.SetString(Keys.DA, value);
    }

    /// <summary>
    /// The resource dictionary <see cref="DefaultAppearance"/> and every field's own <c>/DA</c>
    /// look their font up in - <c>/DR</c>, made on first use.
    /// </summary>
    /// <remarks>
    /// Indirect, because it is shared: the form refers to it and so may every appearance stream in
    /// the document, and a direct dictionary hung off several parents is written out once per
    /// parent.
    /// </remarks>
    public PdfDictionary DefaultResources
    {
        get
        {
            var resources = Elements.GetDictionary(Keys.DR);
            if (resources == null)
            {
                resources = new PdfDictionary(Owner);
                Owner.Internals.AddObject(resources);
                Elements.SetReference(Keys.DR, resources);
            }
            return resources;
        }
    }

    /// <summary>
    /// Names one of the fourteen standard faces in <see cref="DefaultResources"/>, so that a
    /// default appearance string may refer to it.
    /// </summary>
    /// <param name="resourceName">
    /// The name a <c>/DA</c> string calls it by - <c>/Helv</c> by convention for Helvetica,
    /// <c>/ZaDb</c> for ZapfDingbats. A leading solidus is added if it is left off.
    /// </param>
    /// <param name="baseFont">
    /// The face, as ISO 32000-1 Table 109 names it: <c>/Helvetica</c>, <c>/Times-Roman</c>,
    /// <c>/Courier</c>, <c>/Symbol</c>, <c>/ZapfDingbats</c> and the rest. A leading solidus is
    /// added if it is left off.
    /// </param>
    /// <remarks>
    /// Not embedded, and not required to be: the fourteen standard faces are the ones a reader
    /// already has. The encoding is WinAnsi for the text faces and left alone for Symbol and
    /// ZapfDingbats, which carry a built-in encoding of their own that WinAnsi would override -
    /// and ZapfDingbats is how a check box draws its tick.
    /// </remarks>
    public void AddStandardFont(string resourceName, string baseFont)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("A font in the default resources has to be named.", nameof(resourceName));
        if (string.IsNullOrWhiteSpace(baseFont))
            throw new ArgumentException("A standard font has to be named.", nameof(baseFont));

        var key = PdfName.WithSolidus(resourceName);
        var face = PdfName.WithSolidus(baseFont);

        var font = new PdfDictionary(Owner);
        Owner.Internals.AddObject(font);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", face);

        if (face != "/Symbol" && face != "/ZapfDingbats")
            font.Elements.SetName("/Encoding", "/WinAnsiEncoding");

        var resources = DefaultResources;
        var fonts = resources.Elements.GetDictionary("/Font");
        if (fonts == null)
        {
            fonts = new PdfDictionary(Owner);
            resources.Elements["/Font"] = fonts;
        }

        fonts.Elements.SetReference(key, font);
    }

    /// <summary>
    /// Turns the filled form into page content: draws each widget's appearance into the page it is
    /// on, takes the widgets off every page, and takes this form out of the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each widget is drawn <em>as it is</em>: its normal appearance, <c>/AP /N</c>, or for a check
    /// box or radio button the state <c>/AS</c> names. No field redraws on save, so a form read from
    /// a file shows what its producer drew, and that is what is drawn. <see cref="Flatten(bool)"/>
    /// redraws the fields that draw their own appearance first.
    /// </para>
    /// <para>
    /// The appearance is placed as ISO 32000-1 12.5.5 places it: the form's <c>/BBox</c> through its
    /// <c>/Matrix</c>, then onto the widget's <c>/Rect</c>. A widget flagged Hidden or NoView, and a
    /// widget with no appearance to show, is not drawn. Every widget of every field leaves every
    /// page's <c>/Annots</c>, whether it was drawn or not; annotations that are not widgets of this
    /// form are left alone. Afterwards <see cref="PdfDocument.AcroForm"/> answers null, and this
    /// object is detached from the document.
    /// </para>
    /// <para>
    /// <strong>A refusal changes nothing.</strong> The document is refused when it cannot be
    /// modified, when it is tagged (flattening a tagged form is tracked as issue #357), and when
    /// any signature field in it has been signed, because saving it would invalidate the signature.
    /// </para>
    /// <para>
    /// <strong>A widget with no appearance draws nothing.</strong> A form whose producer left the
    /// drawing to the reader and set <see cref="NeedAppearances"/> has widgets like that, and
    /// flattened as they are their values are gone. <see cref="Flatten(bool)"/> with
    /// <c>true</c> draws a text field, combo box or list box first, which is what such a form
    /// wants.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The document was not opened for modification, a certifying signature forbids changing its
    /// structure, it is tagged, a signature field has been signed, or this is not the form of its
    /// document.
    /// </exception>
    public void Flatten() => Flatten(redrawAppearances: false);

    /// <summary>
    /// Turns the filled form into page content, as <see cref="Flatten()"/> does, optionally drawing
    /// the fields that draw their own appearance again first.
    /// </summary>
    /// <param name="redrawAppearances">
    /// True to call <see cref="PdfAcroField.RedrawAppearance"/> on every text field, combo box and
    /// list box before drawing - for a form changed in a way no field noticed, such as an
    /// ancestor's <c>/V</c> a kid inherits. A read-only field is redrawn too, since flattening does
    /// not fill it. Other fields draw no appearance of their own and are drawn as they are.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// As for <see cref="Flatten()"/>; and, with <paramref name="redrawAppearances"/>, a field that
    /// cannot be redrawn because it has no <see cref="PdfTextField.Font"/> and no font resolver is
    /// registered.
    /// </exception>
    public void Flatten(bool redrawAppearances) => PdfFormFlattener.Flatten(this, redrawAppearances);

    /// <summary>
    /// Gets the fields collection of this form.
    /// </summary>
    /// <remarks>
    /// Reading it makes <c>/Fields</c> if the form has none, since ISO 32000-1 Table 218 requires
    /// the entry of every form.
    /// </remarks>
    public PdfAcroField.PdfAcroFieldCollection Fields
    {
        get
        {
            if (_fields == null)
            {
                _fields = new PdfAcroField.PdfAcroFieldCollection(this, Keys.Fields, parent: null);
                _fields.GetOrCreateEntries();
            }
            return _fields;
        }
    }

    private PdfAcroField.PdfAcroFieldCollection _fields;

    /// <summary>
    /// A copy is a form of its own, so it does not keep the view of <c>/Fields</c> this form made,
    /// which reads and writes this form's array.
    /// </summary>
    protected override object Copy()
    {
        var copy = (PdfAcroForm)base.Copy();
        copy._fields = null;
        return copy;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Required) An array of references to the document’s root fields (those with
        /// no ancestors in the field hierarchy).
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string Fields = "/Fields";

        /// <summary>
        /// (Optional) A flag specifying whether to construct appearance streams and
        /// appearance dictionaries for all widget annotations in the document.
        /// Default value: false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string NeedAppearances = "/NeedAppearances";

        /// <summary>
        /// (Optional; PDF 1.3) A set of flags specifying various document-level characteristics
        /// related to signature fields.
        /// Default value: 0.
        /// </summary>
        [KeyInfo("1.3", KeyType.Integer | KeyType.Optional)]
        public const string SigFlags = "/SigFlags";

        /// <summary>
        /// (Required if any fields in the document have additional-actions dictionaries
        /// containing a C entry; PDF 1.3) An array of indirect references to field dictionaries
        /// with calculation actions, defining the calculation order in which their values will
        /// be recalculated when the value of any field changes.
        /// </summary>
        [KeyInfo(KeyType.Array)]
        public const string CO = "/CO";

        /// <summary>
        /// (Optional) A document-wide default value for the DR attribute of variable text fields.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string DR = "/DR";

        /// <summary>
        /// (Optional) A document-wide default value for the DA attribute of variable text fields.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Optional)]
        public const string DA = "/DA";

        /// <summary>
        /// (Optional) A document-wide default value for the Q attribute of variable text fields.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Q = "/Q";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta
        {
            get
            {
                field ??= CreateMeta(typeof(Keys));
                return field;
            }
        }

        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
