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
using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents the base class of the text markup annotations, which mark up a run of text on the
/// page: highlight, underline, strike out and squiggly.
/// </summary>
/// <remarks>
/// What distinguishes these from the other annotations is <see cref="Quads"/>. A run of text is
/// not a rectangle — it wraps, and a line of it can be a different height from the line above —
/// so the marked-up region is given as a list of quadrilaterals, one per line or per word, and
/// the annotation rectangle is merely the box that encloses them. PDF 32000-1 section 12.5.6.10
/// makes /QuadPoints <em>required</em> for these subtypes, and an annotation carrying only a
/// rectangle draws nothing at all in any viewer.
/// <para>
/// So that the simple case does not fall into that trap, an annotation with no quads of its own
/// is marked up over the one quad its <see cref="PdfAnnotation.Rectangle"/> describes. Adding a
/// quad takes over: the rectangle is then recomputed as the box enclosing the quads, which is
/// what the specification asks of it.
/// </para>
/// </remarks>
public abstract class PdfTextMarkupAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextMarkupAnnotation"/> class.
    /// </summary>
    protected PdfTextMarkupAnnotation()
    {
        Elements.SetDateTime(PdfAnnotation.Keys.CreationDate, GlobalTimeSettings.Now);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextMarkupAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    protected PdfTextMarkupAnnotation(PdfDocument document)
        : base(document)
    {
        Elements.SetDateTime(PdfAnnotation.Keys.CreationDate, GlobalTimeSettings.Now);
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    private protected PdfTextMarkupAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Gets the quadrilaterals the markup covers, in default page space.
    /// </summary>
    /// <remarks>
    /// Empty when none have been added, in which case the markup covers
    /// <see cref="PdfAnnotation.Rectangle"/> alone.
    /// </remarks>
    public IReadOnlyList<PdfRectangle> Quads => QuadPoints.Read(Elements.GetArray(Keys.QuadPoints));

    /// <summary>
    /// Adds a quadrilateral to be marked up, in default page space — the space
    /// XGraphics.Transformer.WorldToDefaultPage returns.
    /// </summary>
    public void AddQuad(XRect rect)
    {
        AddQuad(new PdfRectangle(rect));
    }

    /// <summary>
    /// Adds a quadrilateral to be marked up, in default page space.
    /// </summary>
    public void AddQuad(PdfRectangle rect)
    {
        ArgumentNullException.ThrowIfNull(rect);

        var array = Elements.GetArray(Keys.QuadPoints);
        if (array == null)
        {
            array = new PdfArray(Owner);
            Elements[Keys.QuadPoints] = array;
        }

        QuadPoints.Append(array, rect);

        UpdateRectangle();
        Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        RebuildAppearance();
    }

    /// <summary>
    /// Removes every quadrilateral, leaving the markup to cover
    /// <see cref="PdfAnnotation.Rectangle"/> alone.
    /// </summary>
    public void ClearQuads()
    {
        Elements.Remove(Keys.QuadPoints);
        Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        RebuildAppearance();
    }

    /// <summary>
    /// The quadrilaterals actually marked up: those added, or the one the annotation rectangle
    /// describes when none have been.
    /// </summary>
    private IReadOnlyList<PdfRectangle> EffectiveQuads
    {
        get
        {
            var quads = Quads;
            if (quads.Count > 0)
                return quads;

            var rect = Rectangle;
            return rect == null || rect.IsEmpty
                ? new List<PdfRectangle>()
                : new List<PdfRectangle> { rect };
        }
    }

    /// <summary>
    /// Sets the annotation rectangle to the box enclosing the quadrilaterals, which is what
    /// the specification asks of it and what a viewer regenerating the appearance assumes.
    /// </summary>
    private void UpdateRectangle()
    {
        var enclosing = QuadPoints.Enclosing(Quads);
        if (enclosing != null)
            Elements.SetRectangle(PdfAnnotation.Keys.Rect, enclosing);
    }

    internal override void OnAddedToPage()
    {
        RebuildAppearance();
    }

    internal override void OnAppearanceInvalidated()
    {
        RebuildAppearance();
    }

    /// <summary>
    /// Draws one quadrilateral into the appearance stream, in the coordinates of the page.
    /// </summary>
    /// <param name="content">The content stream being built.</param>
    /// <param name="quad">The quadrilateral to mark up.</param>
    protected abstract void DrawQuad(StringBuilder content, PdfRectangle quad);

    /// <summary>
    /// Writes the normal appearance of the annotation.
    /// </summary>
    /// <remarks>
    /// Not every viewer builds an appearance of its own from /QuadPoints — Ghostscript and
    /// Acrobat do, others show nothing — so one is written here rather than left to the reader.
    /// It also has to be, for opacity to be honoured at all: /CA on the annotation is ignored
    /// once an appearance is present, so the value is carried into the graphics state instead.
    /// <para>
    /// The form object is made once and rewritten in place afterwards, so that changing a
    /// colour does not leave the stream it replaces behind in the document. There is nothing to
    /// do until the annotation has an owner, because the form has to be added to it; adding the
    /// annotation to a page calls back here once it has one.
    /// </para>
    /// </remarks>
    internal void RebuildAppearance()
    {
        if (Owner == null)
            return;

        // Nothing to mark. The appearance already there has to go, or the annotation keeps showing
        // what it was last asked for; the form is kept, to be hung back up and rewritten when
        // there is something to mark again.
        var quads = EffectiveQuads;
        if (quads.Count == 0)
        {
            RemoveAppearance();
            return;
        }

        var box = Elements.GetRectangle(PdfAnnotation.Keys.Rect);

        var content = new StringBuilder();
        content.Append("/GS0 gs\n");
        var color = Color;
        content.Append(PdfEncoders.Format("{0:0.###} {1:0.###} {2:0.###} rg\n",
            color.R / 255.0, color.G / 255.0, color.B / 255.0));
        content.Append(PdfEncoders.Format("{0:0.###} {1:0.###} {2:0.###} RG\n",
            color.R / 255.0, color.G / 255.0, color.B / 255.0));
        foreach (var quad in quads)
            DrawQuad(content, quad);

        var form = _appearanceForm;
        var made = form == null;
        if (made)
        {
            form = new PdfDictionary(Owner);
            form.Elements.SetName("/Type", "/XObject");
            form.Elements.SetName("/Subtype", "/Form");
            form.Elements.SetInteger("/FormType", 1);
            Owner.Internals.AddObject(form);
            _appearanceForm = form;
        }

        // Hung up when it is made - over whatever appearance a file gave the annotation - and
        // again after nothing to mark took it down.
        if (made || !Elements.ContainsKey(PdfAnnotation.Keys.AP))
        {
            var appearance = new PdfDictionary(Owner) { Elements = { ["/N"] = form.Reference } };
            Elements[PdfAnnotation.Keys.AP] = appearance;
        }

        form.Elements["/BBox"] = new PdfArray(Owner,
            new PdfReal(box.X1), new PdfReal(box.Y1), new PdfReal(box.X2), new PdfReal(box.Y2));
        form.Elements["/Resources"] = BuildResources();

        // A stream cannot be created twice on one dictionary, so the second time round the
        // bytes are set on the stream that is already there.
        var bytes = new RawEncoding().GetBytes(content.ToString());
        if (form.Stream == null)
            form.CreateStream(bytes);
        else
            form.Stream.Value = bytes;
    }
    private PdfDictionary _appearanceForm;

    /// <summary>
    /// The graphics state the appearance is drawn under. Multiply is what keeps a highlight from
    /// painting over the text it marks; the opacity of the annotation rides along here because
    /// /CA on the annotation itself no longer applies once there is an appearance to apply it to.
    /// </summary>
    private PdfDictionary BuildResources()
    {
        var state = new PdfDictionary(Owner);
        state.Elements.SetName("/Type", "/ExtGState");
        state.Elements.SetName("/BM", BlendMode);
        state.Elements.SetReal("/ca", Opacity);
        state.Elements.SetReal("/CA", Opacity);

        var states = new PdfDictionary(Owner) { Elements = { ["/GS0"] = state } };

        var resources = new PdfDictionary(Owner) { Elements = { ["/ExtGState"] = states } };
        return resources;
    }

    /// <summary>
    /// The blend mode the markup is drawn with.
    /// </summary>
    protected virtual string BlendMode => "/Multiply";

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        /// <summary>
        /// (Required) An array of 8 x n numbers specifying the coordinates of n quadrilaterals
        /// in default user space. Each quadrilateral encompasses a word or group of contiguous
        /// words in the text underlying the annotation.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string QuadPoints = "/QuadPoints";

        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
