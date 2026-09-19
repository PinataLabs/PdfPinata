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
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents a squiggly-underline annotation, which rules a wavy line beneath a run of text.
/// </summary>
public sealed class PdfSquigglyAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquigglyAnnotation"/> class.
    /// </summary>
    public PdfSquigglyAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquigglyAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfSquigglyAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Squiggly");
        Color = XColors.Green;
    }

    /// <summary>
    /// Rules a zigzag along the foot of the quadrilateral, its peaks a tenth of the height of
    /// the line apart, so that the wave stays inside the space the descenders occupy.
    /// </summary>
    protected override void DrawQuad(StringBuilder content, PdfRectangle quad)
    {
        var amplitude = TextMarkupGeometry.RuleThickness(quad) * 1.4;
        var thickness = amplitude / 2;
        var bottom = quad.Y1 + thickness;
        var top = bottom + amplitude;

        content.Append(PdfEncoders.Format("{0:0.###} w\n", thickness));
        content.Append(PdfEncoders.Format("{0:0.###} {1:0.###} m\n", quad.X1, bottom));

        // Half a period per segment, alternating between the trough and the crest. The last one
        // is clipped to the end of the quadrilateral rather than allowed to overhang it.
        var x = quad.X1;
        var up = true;
        while (x < quad.X2)
        {
            x = Math.Min(x + amplitude, quad.X2);
            content.Append(PdfEncoders.Format("{0:0.###} {1:0.###} l\n", x, up ? top : bottom));
            up = !up;
        }
        content.Append("S\n");
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
