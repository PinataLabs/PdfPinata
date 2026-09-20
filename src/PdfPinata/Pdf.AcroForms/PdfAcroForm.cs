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
    /// It is how a text or choice field gets drawn without the author laying out its glyphs: the
    /// reader renders the value using <see cref="DefaultAppearance"/> and the resources in
    /// <see cref="DefaultResources"/>. Buttons get no help from it, because what a check box or a
    /// radio button shows <em>is</em> its value rather than a rendering of one, so their
    /// appearance streams are the field and have to be written.
    /// </para>
    /// <para>
    /// Not every reader honours it, and one that does may draw the field only once it has been
    /// clicked into. A form that must look right unopened carries its own appearances.
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
    /// Name a real size. Zero means auto-size, and what a reader makes of that on a multiline
    /// field is its own business: Ghostscript scales the first line to the height of the whole
    /// box, so a two-line value fills the page. It is the natural thing to write at form level
    /// and the reason a form can look right in one reader and wrong in another.
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

        var key = resourceName[0] == '/' ? resourceName : "/" + resourceName;
        var face = baseFont[0] == '/' ? baseFont : "/" + baseFont;

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
    /// Gets the fields collection of this form.
    /// </summary>
    public PdfAcroField.PdfAcroFieldCollection Fields
    {
        get
        {
            if (_fields == null)
            {
                object o = Elements.GetValue(Keys.Fields, VCF.CreateIndirect);
                _fields = (PdfAcroField.PdfAcroFieldCollection)o;
            }
            return _fields;
        }
    }
    PdfAcroField.PdfAcroFieldCollection _fields;

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
        [KeyInfo(KeyType.Array | KeyType.Required, typeof(PdfAcroField.PdfAcroFieldCollection))]
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
                if (s_meta == null)
                    s_meta = CreateMeta(typeof(Keys));
                return s_meta;
            }
        }
        static DictionaryMeta s_meta;

        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
