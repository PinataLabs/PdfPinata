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

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// The appearance of an interactive form field on a page - ISO 32000-1 section 12.5.6.19.
/// </summary>
/// <remarks>
/// <para>
/// A field says what it is and what it holds; a widget says where on a page it is drawn and what
/// it looks like there. They are separate objects because one field may appear in several places,
/// which is what a radio group is: one field, one value, and a widget for each button.
/// </para>
/// <para>
/// This was <c>internal</c>, which meant nothing outside the assembly could put a form field on a
/// page - the last of the four things that made <c>PdfPinata.Pdf.AcroForms</c> a read-only API.
/// <see cref="AcroForms.PdfAcroField.AddWidget"/> is what makes one; it is public so that a caller
/// can go on to give it an appearance through
/// <see cref="PdfAnnotation.SetAppearance(string, PdfPinata.Drawing.XForm)"/>, which is how a
/// check box or a radio button carries the drawing for each of its states.
/// </para>
/// </remarks>
public sealed class PdfWidgetAnnotation : PdfAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfWidgetAnnotation"/> class.
    /// </summary>
    public PdfWidgetAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfWidgetAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfWidgetAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfWidgetAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Widget");
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        /// <summary>
        /// (Optional) The annotation’s highlighting mode, the visual effect to be used when
        /// the mouse button is pressed or held down inside its active area:
        ///   N (None) No highlighting.
        ///   I (Invert) Invert the contents of the annotation rectangle.
        ///   O (Outline) Invert the annotation’s border.
        ///   P (Push) Display the annotation’s down appearance, if any. If no down appearance is defined,
        ///     offset the contents of the annotation rectangle to appear as if it were being pushed below
        ///     the surface of the page.
        ///   T (Toggle) Same as P (which is preferred).
        /// A highlighting mode other than P overrides any down appearance defined for the annotation.
        /// Default value: I.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string H = "/H";

        /// <summary>
        /// (Optional) An appearance characteristics dictionary to be used in constructing a dynamic
        /// appearance stream specifying the annotation’s visual presentation on the page.
        /// The name MK for this entry is of historical significance only and has no direct meaning.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string MK = "/MK";

        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
