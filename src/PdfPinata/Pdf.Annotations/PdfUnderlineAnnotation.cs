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

using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents an underline annotation, which rules a line beneath a run of text.
/// </summary>
public sealed class PdfUnderlineAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUnderlineAnnotation"/> class.
    /// </summary>
    public PdfUnderlineAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUnderlineAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfUnderlineAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    void Initialize()
    {
        Elements.SetName(Keys.Subtype, "/Underline");
        Color = XColors.Black;
    }

    /// <summary>
    /// Rules a bar across the foot of the quadrilateral, one fourteenth of its height thick and
    /// sitting that far above the bottom, which is about where the descenders of the line end.
    /// </summary>
    protected override void DrawQuad(StringBuilder content, PdfRectangle quad)
    {
        var thickness = TextMarkupGeometry.RuleThickness(quad);
        content.Append(PdfEncoders.Format("{0:0.###} {1:0.###} {2:0.###} {3:0.###} re f\n",
            quad.X1, quad.Y1 + thickness, quad.X2 - quad.X1, thickness));
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfTextMarkupAnnotation.Keys
    {
        public static new DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
