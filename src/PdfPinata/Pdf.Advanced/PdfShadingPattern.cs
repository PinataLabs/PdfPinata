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
using PdfPinata.Drawing;
using PdfPinata.Drawing.Pdf;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a shading pattern dictionary.
/// </summary>
public sealed class PdfShadingPattern : PdfDictionaryWithContentStream
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingPattern"/> class.
    /// </summary>
    public PdfShadingPattern(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Pattern");
        Elements[Keys.PatternType] = new PdfInteger(2);
    }

    /// <summary>
    /// Sets the shading pattern up from a gradient brush, linear or radial.
    /// </summary>
    /// <param name="brush">The gradient.</param>
    /// <param name="matrix">
    /// The matrix from the renderer's view space to the default space of the page.
    /// </param>
    /// <param name="renderer">The renderer painting the gradient.</param>
    /// <param name="channel">Whether the shading carries the colours or the alpha.</param>
    /// <remarks>
    /// A shading whose coordinates cannot be written in view space - a linear gradient with a
    /// transform of its own, or a radial gradient whose transform stretches its circles into
    /// ellipses - is written in the brush's space instead, and the mapping from there to view space goes in
    /// front of the matrix here. For every other shading that mapping is the identity, and the
    /// matrix is written as it always was.
    /// </remarks>
    internal void SetupFromBrush(XBaseGradientBrush brush, XMatrix matrix, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var shading = new PdfShading(_document);
        var shadingToView = shading.SetupFromBrush(brush, renderer, channel);
        Elements[Keys.Shading] = shading;

        if (!shadingToView.IsIdentity)
            matrix.Prepend(shadingToView);
        Elements.SetMatrix(Keys.Matrix, matrix);
    }

    /// <summary>
    /// Common keys for all streams.
    /// </summary>
    internal sealed new class Keys : PdfDictionaryWithContentStream.Keys
    {
        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be Pattern for a pattern dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) A code identifying the type of pattern that this dictionary describes;
        /// must be 2 for a shading pattern.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string PatternType = "/PatternType";

        /// <summary>
        /// (Required) A shading object (see below) defining the shading pattern's gradient fill.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Shading = "/Shading";

        /// <summary>
        /// (Optional) An array of six numbers specifying the pattern matrix.
        /// Default value: the identity matrix [1 0 0 1 0 0].
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Matrix = "/Matrix";

        /// <summary>
        /// (Optional) A graphics state parameter dictionary containing graphics state parameters
        /// to be put into effect temporarily while the shading pattern is painted. Any parameters
        /// that are not so specified are inherited from the graphics state that was in effect
        /// at the beginning of the content stream in which the pattern is defined as a resource.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string ExtGState = "/ExtGState";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
