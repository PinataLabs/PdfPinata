#region Copyright
//
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

using System;
using System.Globalization;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf;

/// <summary>
/// The sheet a page is printed on, and everything that only a page going to a press needs: the
/// bleed the artwork runs into, the room outside it for printer's marks, the five boxes that say
/// which part of the sheet is what, and the crop marks that tell the trimmer where to cut.
/// </summary>
/// <remarks>
/// <para>
/// Owned by the <see cref="PdfPage"/> it is constructed with, one per page, and reached only
/// through that page: <see cref="PdfPage.TrimMargins"/>, <see cref="PdfPage.MarkMargins"/> and
/// <see cref="PdfPage.DrawCropMarks"/> are the public surface, and they forward here. Nothing
/// about the page's public API says this type exists.
/// </para>
/// <para>
/// It holds no copy of anything the page knows. The width, the height, the elements and the
/// content streams are read from <see cref="PdfPage"/> whenever they are wanted, so the two
/// cannot come to disagree about what the page is.
/// </para>
/// </remarks>
internal sealed class PdfPageSheet
{
    internal PdfPageSheet(PdfPage page) => _page = page;

    private readonly PdfPage _page;

    /// <summary>
    /// The bleed: how much sheet there is outside the page. See
    /// <see cref="PdfPage.TrimMargins"/>, which is the public surface for this and documents
    /// what setting it does - including that assignment copies the four values rather than
    /// keeping the reference.
    /// </summary>
    internal TrimMargins TrimMargins
    {
        get
        {
            if (_trimMargins == null)
                _trimMargins = new TrimMargins();
            return _trimMargins;
        }
        set
        {
            if (_trimMargins == null)
                _trimMargins = new TrimMargins();
            if (value != null)
            {
                _trimMargins.Left = value.Left;
                _trimMargins.Right = value.Right;
                _trimMargins.Top = value.Top;
                _trimMargins.Bottom = value.Bottom;
            }
            else
                _trimMargins.All = 0;
        }
    }
    private TrimMargins _trimMargins = new();

    /// <summary>
    /// The room outside the bleed for printer's marks. See <see cref="PdfPage.MarkMargins"/>,
    /// which is the public surface for this and documents what it is for - including that
    /// assignment copies the four values rather than keeping the reference.
    /// </summary>
    internal TrimMargins MarkMargins
    {
        get => _markMargins;
        set
        {
            if (value != null)
            {
                _markMargins.Left = value.Left;
                _markMargins.Right = value.Right;
                _markMargins.Top = value.Top;
                _markMargins.Bottom = value.Bottom;
            }
            else
                _markMargins.All = 0;
        }
    }
    private readonly TrimMargins _markMargins = new() { All = XUnit.FromMillimeter(5) };

    /// <summary>
    /// The distance from the corner of the sheet to the corner of the trimmed page: the bleed,
    /// plus the room left outside it for printer's marks.
    /// </summary>
    /// <remarks>
    /// Read through <see cref="PdfPage.SheetOffset"/> by both the things that place the drawing
    /// origin - <c>XGraphics.Initialize</c> and <c>XGraphicsPdfRenderer.BeginPage</c> - so that
    /// the two cannot come to different answers.
    /// </remarks>
    internal XPoint Offset => new XPoint(
        _markMargins.Left.Point + _trimMargins.Left.Point,
        _markMargins.Top.Point + _trimMargins.Top.Point);

    /// <summary>
    /// How much taller the sheet is than the page that will be cut out of it.
    /// </summary>
    internal double ExtraHeight =>
        _markMargins.Top.Point + _trimMargins.Top.Point +
        _markMargins.Bottom.Point + _trimMargins.Bottom.Point;

    /// <summary>
    /// The size the page was asked to be, remembered before saving grows the media box into the
    /// sheet. Null until then, and null again whenever the page is resized.
    /// </summary>
    /// <remarks>
    /// <see cref="PdfPage.Width"/> reads the media box, and saving overwrites the media box with
    /// the sheet. Without this the page would report the sheet ever afterwards, and - worse - a
    /// second save would take the sheet for the page and add the margins to it all over again.
    /// That is why the two members of the page that need it - <see cref="PdfPage.Width"/> and
    /// <see cref="PdfPage.Height"/> to answer with the page rather than the sheet, and
    /// <see cref="PdfPage.MediaBox"/>'s setter to forget it - reach in here for it.
    /// </remarks>
    internal XSize? TrimmedSize { get; set; }

    /// <summary>
    /// Writes the sheet boxes if the page has a bleed, and does nothing if it has not.
    /// </summary>
    /// <remarks>
    /// Whether the boxes are wanted is decided here rather than at the call site in
    /// <c>PdfPage.PrepareForSave</c>, because it is a decision about the margins this type owns.
    /// </remarks>
    internal void WriteBoxesIfTrimmed()
    {
        if (_trimMargins.AreSet)
            WriteSheetBoxes();
    }

    /// <summary>
    /// Grows the page into the sheet it will be printed on and writes the five boxes that say
    /// which part of that sheet is what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three areas nest, outermost first: the <b>sheet</b> is what goes through the press;
    /// the <b>bleed</b> is how far the artwork may run, and everything between it and the sheet
    /// edge is room for printer's marks; the <b>trim</b> is where the guillotine cuts, and is the
    /// page the caller asked for. So <c>/MediaBox ⊇ /BleedBox ⊇ /TrimBox</c>, which is the
    /// nesting the PDF specification describes and which this library did not used to produce -
    /// it wrote <c>/BleedBox</c> equal to <c>/MediaBox</c>, leaving nowhere for a mark to go.
    /// </para>
    /// <para>
    /// Y1 is the <i>bottom</i> edge of a PDF rectangle. The bottom margins therefore go into Y1
    /// and the top margins come off Y2, which is the way round the arithmetic here did not use to
    /// have them. No page with an even margin could show the difference.
    /// </para>
    /// </remarks>
    private void WriteSheetBoxes()
    {
        // Remembered before the media box is overwritten, because Width reads the media box and
        // there would otherwise be nothing left to derive the sheet from - a second save would
        // take the sheet for the page and add the margins to it again.
        TrimmedSize ??= new XSize(_page.Width.Point, _page.Height.Point);

        double bleedLeft = _trimMargins.Left.Point, bleedRight = _trimMargins.Right.Point;
        double bleedTop = _trimMargins.Top.Point, bleedBottom = _trimMargins.Bottom.Point;
        double markLeft = _markMargins.Left.Point, markRight = _markMargins.Right.Point;
        double markTop = _markMargins.Top.Point, markBottom = _markMargins.Bottom.Point;

        var width = markLeft + bleedLeft + TrimmedSize.Value.Width + bleedRight + markRight;
        var height = markTop + bleedTop + TrimmedSize.Value.Height + bleedBottom + markBottom;

        // Written through the elements rather than through the properties, which would throw the
        // remembered size away as any other resize does.
        SetBox(PdfPage.InheritablePageKeys.MediaBox, new PdfRectangle(0, 0, width, height));
        SetBox(PdfPage.InheritablePageKeys.CropBox, new PdfRectangle(0, 0, width, height));
        SetBox(PdfPage.Keys.BleedBox, new PdfRectangle(markLeft, markBottom, width - markRight, height - markTop));

        var trim = new PdfRectangle(
            markLeft + bleedLeft, markBottom + bleedBottom,
            width - markRight - bleedRight, height - markTop - bleedTop);
        SetBox(PdfPage.Keys.TrimBox, trim);
        SetBox(PdfPage.Keys.ArtBox, trim.Clone());

        if (_markMargins.AreSet)
            DrawCropMarks();
    }

    private void SetBox(string key, PdfRectangle box) => _page.Elements.SetRectangle(key, box);

    /// <summary>
    /// Draws the eight standard crop marks in the room <see cref="MarkMargins"/> leaves outside
    /// the bleed, telling the trimmer where to cut. See <see cref="PdfPage.DrawCropMarks"/>,
    /// which is the public surface for this.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The page has no trim margin, or no room outside it to put the marks in.
    /// </exception>
    internal void DrawCropMarks()
    {
        if (!_trimMargins.AreSet)
        {
            throw new InvalidOperationException(
                "Crop marks say where a page is to be cut out of a larger sheet, and this page " +
                "has no TrimMargins, so there is no sheet and no cut. Set PdfPage.TrimMargins to " +
                "the bleed the artwork runs into.");
        }

        if (!_markMargins.AreSet)
        {
            throw new InvalidOperationException(
                "Crop marks are drawn outside the bleed, and PdfPage.MarkMargins is zero, so " +
                "there is no room on the sheet to put them. Set MarkMargins to the space the " +
                "press needs around the bleed - five millimetres is the default and is enough.");
        }

        if (_cropMarksDrawn)
            return;
        _cropMarksDrawn = true;

        double bleedLeft = _trimMargins.Left.Point, bleedRight = _trimMargins.Right.Point;
        double bleedTop = _trimMargins.Top.Point, bleedBottom = _trimMargins.Bottom.Point;
        double markLeft = _markMargins.Left.Point, markRight = _markMargins.Right.Point;
        double markTop = _markMargins.Top.Point, markBottom = _markMargins.Bottom.Point;

        var width = markLeft + bleedLeft + _page.Width.Point + bleedRight + markRight;
        var height = markTop + bleedTop + _page.Height.Point + bleedBottom + markBottom;

        // The four lines the guillotine follows, in the sheet's own coordinates.
        var left = markLeft + bleedLeft;
        var right = width - markRight - bleedRight;
        var top = height - markTop - bleedTop;
        var bottom = markBottom + bleedBottom;

        var marks = new StringBuilder();

        // Black, and thin enough that the mark itself does not tell the trimmer a lie about
        // where the cut is. Both are what every other producer writes.
        marks.Append("q\n0 G\n0.25 w\n");

        // Horizontal marks lie on the top and bottom cuts and reach out past the left and right
        // bleed; vertical marks lie on the left and right cuts and reach out past the top and
        // bottom bleed. Eight in all, two meeting at each corner.
        Mark(marks, 0, top, markLeft, top);
        Mark(marks, width - markRight, top, width, top);
        Mark(marks, 0, bottom, markLeft, bottom);
        Mark(marks, width - markRight, bottom, width, bottom);

        Mark(marks, left, height - markTop, left, height);
        Mark(marks, right, height - markTop, right, height);
        Mark(marks, left, 0, left, markBottom);
        Mark(marks, right, 0, right, markBottom);

        marks.Append("Q\n");

        var content = _page.Contents.AppendContent();
        content.CreateStream(PdfEncoders.RawEncoding.GetBytes(marks.ToString()));
    }

    private bool _cropMarksDrawn;

    private static void Mark(StringBuilder marks, double x1, double y1, double x2, double y2)
    {
        marks.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} m {2:0.###} {3:0.###} l S\n",
            x1, y1, x2, y2);
    }
}
