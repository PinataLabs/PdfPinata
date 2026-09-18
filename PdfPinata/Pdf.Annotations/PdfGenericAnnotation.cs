#region PDFsharp - A .NET library for processing PDF
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
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// An annotation of any subtype: the ones this library has a class for, and the ones it does not.
/// </summary>
/// <remarks>
/// <para>
/// Read back out of a document, this is what an annotation dictionary PdfPinata has no class
/// for becomes. Written, it is the way to put one into a document - <c>/Square</c>, <c>/Circle</c>,
/// <c>/Line</c>, <c>/FreeText</c>, or anything else a reader understands - by naming the subtype
/// and filling in the entries it calls for.
/// </para>
/// <para>
/// It was <c>internal</c>, which with <see cref="PdfAnnotation"/> being abstract with no public way
/// to set <c>/Subtype</c> meant a subtype without a class of its own could not be added to a page
/// at all: <see cref="PdfAnnotations.Add"/> takes a <see cref="PdfAnnotation"/>, and there was no
/// way to make one.
/// </para>
/// <para>
/// Nothing here writes an appearance. Subtypes that a reader draws from <c>/AP</c> and nothing else
/// need <see cref="PdfAnnotation.SetAppearance(XForm)"/>, or they occupy their rectangle and paint
/// none of it.
/// </para>
/// </remarks>
public sealed class PdfGenericAnnotation : PdfAnnotation
{
    //DMH 6/7/06
    //Make this public so we can use it in PdfAnnotations to
    //get the Meta data from existings annotations.
    /// <summary>
    /// Wraps an existing annotation dictionary. Public so that <see cref="PdfAnnotations"/> can reach
    /// the metadata of annotations read from a document.
    /// </summary>
    public PdfGenericAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Creates an annotation of the named subtype.
    /// </summary>
    /// <param name="subtype">
    /// The value of <c>/Subtype</c>, as ISO 32000-1 Table 169 names it - <c>/Square</c>,
    /// <c>/FreeText</c> and so on. A leading solidus is added if it is left off.
    /// </param>
    public PdfGenericAnnotation(string subtype)
    {
        Elements.SetName(Keys.Subtype, SubtypeName(subtype));
    }

    /// <summary>
    /// Creates an annotation of the named subtype, owned by the given document.
    /// </summary>
    /// <param name="document">The document the annotation belongs to.</param>
    /// <param name="subtype">
    /// The value of <c>/Subtype</c>. A leading solidus is added if it is left off.
    /// </param>
    public PdfGenericAnnotation(PdfDocument document, string subtype)
        : base(document)
    {
        Elements.SetName(Keys.Subtype, SubtypeName(subtype));
    }

    /// <summary>
    /// The subtype this annotation names itself with, including its solidus.
    /// </summary>
    public string Subtype => Elements.GetName(Keys.Subtype);

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
