#region Copyright
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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

using PdfPinata.Drawing;

namespace PdfPinata;

/// <summary>
/// How a page is to be mapped into a page of a different size.
/// </summary>
public sealed class PageResizeOptions
{
    /// <summary>
    /// How the content is scaled into the new box. Fit by default, which loses nothing and
    /// distorts nothing.
    /// </summary>
    public PageFitMode Fit { get; set; } = PageFitMode.Fit;

    /// <summary>
    /// Where the content sits when the new box leaves it slack, and which part of it is kept
    /// when the new box is too small to hold it. Centred by default.
    /// </summary>
    public PageAlignment Alignment { get; set; } = PageAlignment.MiddleCenter;

    /// <summary>
    /// An empty border to leave on all four sides, taken off the new box before the content is
    /// fitted into what is left. Zero by default.
    /// </summary>
    public XUnit Margin { get; set; } = 0;

    /// <summary>
    /// Whether to turn the content a quarter when the new box is of the opposite shape to the
    /// old one - landscape against portrait - rather than shrink it to fit and leave slack down
    /// the sides. Off by default.
    /// <para>
    /// This is the setting to turn on when normalising a batch of pages of mixed size and
    /// orientation onto one paper size, which is the case where it is nearly always wanted.
    /// </para>
    /// </summary>
    public bool AutoRotate { get; set; }

    /// <summary>
    /// Whether the annotations of the page move with the content. On by default. Turning it off
    /// leaves every annotation where it was, which is to say in the wrong place.
    /// </summary>
    public bool ScaleAnnotations { get; set; } = true;

    /// <summary>
    /// Whether the destinations that point at the page are found and moved with the content. On
    /// by default.
    /// <para>
    /// This is the expensive part of a resize: a destination can be held on any page of the
    /// document, in the outline tree or in the name tree of the catalog, so the whole document
    /// has to be walked to find them. Turn it off when resizing every page - or better, use
    /// <see cref="Pdf.PdfDocument.ResizePages(PageSize, PageOrientation, PageResizeOptions)"/>,
    /// which makes one pass for the document rather than one per page.
    /// </para>
    /// </summary>
    public bool ScaleDestinations { get; set; } = true;

    /// <summary>
    /// Fit the whole page into the new box, centred, with no margin. What a resize does when it
    /// is not told otherwise.
    /// </summary>
    /// <remarks>
    /// A fresh instance every time, so that a caller who changes one setting on it does not
    /// change it for everybody else.
    /// </remarks>
    public static PageResizeOptions Default => new();

    /// <summary>
    /// Crop to the new box rather than scale into it: the content keeps its size and whatever
    /// falls outside the new box is lost.
    /// <para>
    /// Anchored <b>top left</b>, so what is cropped away is the foot of the page. Note this is
    /// deliberately not what the <see cref="Pdf.PdfPage.Size"/> setter used to do before it
    /// began to refuse a page with content on it: that wrote a new media box at the origin, and
    /// the origin of a PDF page is its bottom left corner, so it kept the foot of the page and
    /// cropped the heading away. That was an artefact of the coordinate system rather than
    /// anybody's intent, and it is not reproduced here. A caller who really wants it can ask for
    /// <see cref="PageAlignment.BottomLeft"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A fresh instance every time, so that a caller who changes one setting on it does not
    /// change it for everybody else.
    /// </remarks>
    public static PageResizeOptions Crop => new()
    {
        Fit = PageFitMode.None,
        Alignment = PageAlignment.TopLeft
    };

    /// <summary>
    /// A copy of these options, so that a resize cannot be changed under way by a caller holding
    /// the same instance.
    /// </summary>
    internal PageResizeOptions Clone()
    {
        return new PageResizeOptions
        {
            Fit = Fit,
            Alignment = Alignment,
            Margin = Margin,
            AutoRotate = AutoRotate,
            ScaleAnnotations = ScaleAnnotations,
            ScaleDestinations = ScaleDestinations
        };
    }
}
