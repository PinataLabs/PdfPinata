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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;
using PdfPinata.Pdf.IO;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a page in a PDF document.
/// </summary>
public sealed class PdfPage : PdfDictionary, IContentStream
{
    /// <summary>
    /// Initializes a new page. The page must be added to a document before it can be used.
    /// Depending of the IsMetric property of the current region the page size is set to
    /// A4 or Letter respectively. If this size is not appropriate it should be changed before
    /// any drawing operations are performed on the page.
    /// </summary>
    public PdfPage()
    {
        _sheet = new PdfPageSheet(this);
        Elements.SetName(Keys.Type, "/Page");
        Initialize();
    }

    /// <summary>
    /// Initializes a new page owned by the specified document but <b>not yet placed in it</b>:
    /// the page count does not change and the page does not appear in the output until it is
    /// placed.
    /// <para>
    /// This is the way to build a page before deciding where it goes. The page is drawable
    /// straight away - <c>XGraphics.FromPdfPage</c> accepts it, which it does not accept for a
    /// page created by the parameterless constructor - so the order is: construct, draw, then
    /// place with <see cref="PdfDocument.PlacePage"/>.
    /// </para>
    /// <para>
    /// Use this rather than <see cref="PdfDocument.AddPage()"/> whenever the position is not
    /// simply the end of the document. <c>AddPage()</c> appends the page immediately, so
    /// passing its result to a later Insert call places the same page twice and throws.
    /// </para>
    /// </summary>
    /// <param name="document">The document that is to own the page.</param>
    public PdfPage(PdfDocument document)
        : base(document)
    {
        _sheet = new PdfPageSheet(this);
        Elements.SetName(Keys.Type, "/Page");
        Elements[Keys.Parent] = document.Pages.Reference;
        Initialize();
    }

    internal PdfPage(PdfDictionary dict)
        : base(dict)
    {
        _sheet = new PdfPageSheet(this);

        // Set Orientation depending on /Rotate.
        var rotate = Elements.GetInteger(InheritablePageKeys.Rotate);
        if (Math.Abs((rotate / 90)) % 2 == 1)
            _orientation = PageOrientation.Landscape;
    }

    private void Initialize()
    {

        try
        {
            Size = RegionInfo.CurrentRegion.IsMetric ? PageSize.A4 : PageSize.Letter;
        }
        catch (NotImplementedException)
        {
            // https://github.com/ststeiger/PdfSharpCore/issues/46
            // { System.NotImplementedException: Neutral region info
            // at System.Globalization.RegionInfo..ctor
            Size = PageSize.A4;
        }
    }

    /// <summary>
    /// Gets or sets a user defined object that contains arbitrary information associated with this PDF page.
    /// The tag is not used by PdfPinata.
    /// </summary>
    public object Tag
    {
        get => _tag;
        set => _tag = value;
    }
    private object _tag;

    /// <summary>
    /// Closes the page. A closes page cannot be modified anymore and it is not possible to
    /// get an XGraphics object for a closed page. Closing a page is not required, but may saves
    /// resources if the document has many pages.
    /// </summary>
    public void Close()
    {
        _closed = true;
    }
    private bool _closed;

    /// <summary>
    /// Gets a value indicating whether the page is closed.
    /// </summary>
    internal bool IsClosed => _closed;

    /// <summary>
    /// Gets or sets the PdfDocument this page belongs to.
    /// </summary>
    internal override PdfDocument Document
    {
        set
        {
            if (!ReferenceEquals(_document, value))
            {
                if (_document != null)
                    throw new InvalidOperationException("Cannot change document.");
                _document = value;
                if (Reference != null)
                    Reference.Document = value;
                Elements[Keys.Parent] = _document.Pages.Reference;
            }
        }
    }

    /// <summary>
    /// Gets or sets the orientation of the page. The default value PageOrientation.Portrait.
    /// If an imported page has a /Rotate value that matches the formula 90 + n * 180 the
    /// orientation is set to PageOrientation.Landscape.
    /// </summary>
    public PageOrientation Orientation
    {
        get => _orientation;
        set => _orientation = value;
    }
    private PageOrientation _orientation;

    /// <summary>
    /// Gets or sets one of the predefined standard sizes like.
    /// </summary>
    public PageSize Size
    {
        get => _pageSize;
        set
        {
            if (!Enum.IsDefined(value))
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(PageSize));

            RefuseToResizeByReboxing(nameof(Size));

            var size = PageSizeConverter.ToSize(value);
            // MediaBox is always in Portrait mode (see Height, Width).
            // So take Orientation NOT into account.
            MediaBox = new PdfRectangle(0, 0, size.Width, size.Height);
            _pageSize = value;
        }
    }
    private PageSize _pageSize;

    /// <summary>
    /// Gets a value indicating whether the page holds any content, without disturbing it.
    /// <para>
    /// Reading <see cref="Contents"/> to find out would not do: its getter rewrites /Contents into
    /// an array as a side effect of being read, so asking the question would change the answer.
    /// </para>
    /// </summary>
    internal bool HasContent
    {
        get
        {
            var item = Elements[Keys.Contents];
            if (item is PdfReference reference)
                item = reference.Value;

            return item switch
            {
                null => false,
                // An array of content streams holds nothing when none of the streams in it do.
                // Counting the entries would not answer the question: XGraphics appends a stream
                // to the array before anything is drawn, so a page that was opened for drawing
                // and then left alone has an entry that holds no bytes.
                PdfArray array => HoldsBytes(array),
                // A single content stream holds nothing when it has no bytes.
                PdfDictionary dictionary => dictionary.Stream is { Length: > 0 },
                _ => true
            };
        }
    }

    /// <summary>
    /// Whether any stream of a content array has bytes in it. An entry that cannot be resolved
    /// to a stream dictionary is counted as content: it is not understood, and treating what is
    /// not understood as empty is the answer that loses a page.
    /// </summary>
    private static bool HoldsBytes(PdfArray array)
    {
        foreach (var element in array.Elements)
        {
            var item = element;
            if (item is PdfReference reference)
                item = reference.Value;

            if (item is PdfDictionary dictionary)
            {
                if (dictionary.Stream is { Length: > 0 })
                    return true;
            }
            else if (item != null && item is not PdfNull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Throws when the page has content, because setting its size writes a new media box and
    /// nothing else, which crops the content rather than resizing it.
    /// </summary>
    private void RefuseToResizeByReboxing(string property)
    {
        if (!HasContent)
            return;

        throw new InvalidOperationException(
            $"{property} cannot be set on a page that already has content on it: it writes a new " +
            "media box and leaves the content where it was, which crops the page rather than " +
            "resizing it. Use PdfPage.Resize to scale the content into the new size, or " +
            "PdfPage.Resize with PageResizeOptions.Crop to crop it on purpose.");
    }

    /// <summary>
    /// Changes the size of this page, moving what is drawn on it into the new size rather than
    /// cropping it, and moving the annotations of the page and the destinations that point at it
    /// along with the content.
    /// </summary>
    /// <param name="size">The size the page is to become.</param>
    /// <param name="orientation">Which way round that size goes.</param>
    /// <param name="options">
    /// How the content is to be fitted into the new size. Null fits the whole page in, centred,
    /// which loses nothing and distorts nothing.
    /// </param>
    /// <remarks>
    /// <para>
    /// A destination is held wherever somebody linked from - the annotations of any page, the
    /// outline tree, the name tree of the catalog - and a page carries no list of what points at
    /// it, so this walks the whole document to find them. Resizing every page one at a time
    /// therefore walks the document once per page: use
    /// <see cref="PdfDocument.ResizePages(PageSize, PageOrientation, PageResizeOptions)"/>, which
    /// walks it once altogether.
    /// </para>
    /// <para>
    /// Refused on a document that is encrypted, signed, tagged, or not open for modification.
    /// </para>
    /// </remarks>
    public void Resize(PageSize size, PageOrientation orientation = PageOrientation.Portrait,
        PageResizeOptions options = null)
    {
        if (!Enum.IsDefined(size))
            throw new InvalidEnumArgumentException(nameof(size), (int)size, typeof(PageSize));

        PdfPageResizer.Resize(this, SizeInPoints(size, orientation), size, options);
    }

    /// <summary>
    /// Changes the size of this page to a size in points, moving what is drawn on it into the
    /// new size rather than cropping it.
    /// </summary>
    /// <param name="size">The size the page is to become, in points, as the reader will see it.</param>
    /// <param name="options">
    /// How the content is to be fitted into the new size. Null fits the whole page in, centred.
    /// </param>
    /// <remarks>
    /// See <see cref="Resize(PageSize, PageOrientation, PageResizeOptions)"/> for what this costs
    /// and when it is refused.
    /// </remarks>
    public void Resize(XSize size, PageResizeOptions options = null)
    {
        PdfPageResizer.Resize(this, size, PageSize.Undefined, options);
    }

    /// <summary>
    /// The size in points that a page size and an orientation come to, as the reader sees it.
    /// </summary>
    internal static XSize SizeInPoints(PageSize size, PageOrientation orientation)
    {
        // Anything other than landscape would otherwise be taken for portrait, so a value that
        // is not an orientation at all would quietly produce a portrait page rather than say
        // that it was not understood.
        if (!Enum.IsDefined(orientation))
        {
            throw new InvalidEnumArgumentException(nameof(orientation), (int)orientation,
                typeof(PageOrientation));
        }

        var points = PageSizeConverter.ToSize(size);
        return orientation == PageOrientation.Landscape
            ? new XSize(points.Height, points.Width)
            : points;
    }

    /// <summary>
    /// Gets or sets the trim margins: how much sheet there is outside the page, for artwork that
    /// has to run past the edge of the paper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting a margin does three things. The origin an <see cref="Drawing.XGraphics"/> draws in
    /// moves to the top-left corner of the <b>trimmed</b> page, so a caller who never draws outside
    /// it writes exactly the same code as on a page with no margin at all, and a caller who wants a
    /// bleed reaches past the origin with negative coordinates. The sheet written to the file grows
    /// by the margins - <see cref="Width"/> and <see cref="Height"/> go on reporting the trimmed
    /// page while the document is being built. And saving writes all five page boxes rather than
    /// <c>/MediaBox</c> alone: <c>/MediaBox</c>, <c>/CropBox</c> and <c>/BleedBox</c> are the whole
    /// sheet, and <c>/TrimBox</c> and <c>/ArtBox</c> are the sheet inset by the margins, which is
    /// where a guillotine cuts.
    /// </para>
    /// <para>
    /// A page with no margin set is untouched: none of the extra boxes is written, so the whole
    /// feature is invisible to every document that does not ask for it.
    /// </para>
    /// <para>
    /// <b>The page must be drawn in points.</b> An <see cref="Drawing.XGraphics"/> opened on a
    /// trimmed page with any other <see cref="Drawing.XGraphics.PageUnit"/> asserts in a debug
    /// build and places the origin wrongly in a release one; the offset is applied after the unit
    /// scale rather than through it.
    /// </para>
    /// <para>
    /// <b>Save the document once.</b> The sheet is derived from <see cref="Width"/>, which is the
    /// media box that saving then overwrites, so saving a second time adds the margins again. This
    /// is a known defect rather than an intention.
    /// </para>
    /// <para>
    /// <b>Assigning copies the four values rather than keeping the reference.</b> The page holds a
    /// <see cref="TrimMargins"/> of its own from the moment it is constructed, and
    /// <c>page.TrimMargins = other</c> copies <c>other</c>'s left, right, top and bottom into it -
    /// so mutating <c>other</c> afterwards does not move this page's margins, and assigning the
    /// same instance to several pages gives each of them margins of its own. That is what makes
    /// <see cref="PdfDocumentSettings.TrimMargins"/> usable as a document-wide default: every page
    /// added to the document is given the default's values, not the default itself. Assigning null
    /// sets all four to zero.
    /// </para>
    /// </remarks>
    /// <example>
    /// A photograph bled 3mm off the top-left corner of an A5 page:
    /// <code>
    /// page.Size = PageSize.A5;
    /// page.TrimMargins.All = XUnit.FromMillimeter(3);
    ///
    /// XGraphics gfx = XGraphics.FromPdfPage(page);
    /// double bleed = XUnit.FromMillimeter(3).Point;
    /// gfx.DrawImage(photograph, -bleed, -bleed, page.Width.Point + 2 * bleed, 300);
    /// </code>
    /// </example>
    public TrimMargins TrimMargins
    {
        get => _sheet.TrimMargins;
        set => _sheet.TrimMargins = value;
    }

    /// <summary>
    /// Gets or sets the margins outside the bleed: the room on the sheet for printer's marks, and
    /// the switch that decides whether crop marks are drawn there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five millimetres on each edge by default, which is the room a press wants and is enough for
    /// the eight crop marks the library draws into it. It applies only to a page that has
    /// <see cref="TrimMargins"/> set - a page with no bleed is a page going nowhere near a
    /// guillotine, and nothing about it changes.
    /// </para>
    /// <para>
    /// Setting it to zero takes the room away and, with it, the marks. That is also the setting
    /// that reproduces the boxes this library wrote before crop marks existed, for a caller whose
    /// downstream tooling expects them.
    /// </para>
    /// <para>
    /// <b>Assigning copies the four values rather than keeping the reference</b>, exactly as
    /// <see cref="TrimMargins"/> does and for the same reason. Assigning null sets all four to
    /// zero, which is the setting that takes the marks away.
    /// </para>
    /// </remarks>
    /// <seealso cref="DrawCropMarks()"/>
    public TrimMargins MarkMargins
    {
        get => _sheet.MarkMargins;
        set => _sheet.MarkMargins = value;
    }

    /// <summary>
    /// The distance from the corner of the sheet to the corner of the trimmed page: the bleed,
    /// plus the room left outside it for printer's marks.
    /// </summary>
    /// <remarks>
    /// Read by both the things that place the drawing origin - <c>XGraphics.Initialize</c> and
    /// <c>XGraphicsPdfRenderer.BeginPage</c> - so that the two cannot come to different answers.
    /// </remarks>
    internal XPoint SheetOffset => _sheet.Offset;

    /// <summary>
    /// How much taller the sheet is than the page that will be cut out of it.
    /// </summary>
    internal double SheetExtraHeight => _sheet.ExtraHeight;

    /// <summary>
    /// The sheet this page is printed on: the bleed, the room for printer's marks, the boxes that
    /// say which part of the sheet is what, and the crop marks. Everything a page going to a press
    /// needs and no other page does, kept in a type of its own.
    /// </summary>
    /// <remarks>
    /// Built in every constructor rather than lazily on first read, because
    /// <see cref="PrepareForSave"/> and the public <see cref="DrawCropMarks"/> both reach it
    /// without going through a getter first, and because the margins it holds have to exist for an
    /// imported page too - which is built from an existing dictionary and never runs
    /// <c>Initialize</c>.
    /// </remarks>
    private readonly PdfPageSheet _sheet;

    /// <summary>
    /// Gets or sets the media box directly: the whole sheet the page is printed on. XGrahics is
    /// not prepared to work with a media box with an origin other than (0,0).
    /// </summary>
    /// <remarks>
    /// Reading it never changes the page. A page that states no media box - which ISO 32000-1
    /// requires, but which a file read from elsewhere can still leave out - answers the empty
    /// rectangle and is left without one; <see cref="HasMediaBox"/> tells the two apart. A media
    /// box stated on a node of the page tree is copied onto the page when the document is read,
    /// so an inherited one counts as the page's own.
    /// </remarks>
    public PdfRectangle MediaBox
    {
        get => BoxOrEmpty(InheritablePageKeys.MediaBox);
        set
        {
            // Whatever the page was asked to be, it is not that any more.
            _sheet.TrimmedSize = null;
            Elements.SetRectangle(InheritablePageKeys.MediaBox, value);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the page states a media box, either itself or through
    /// the page tree it was read from.
    /// </summary>
    public bool HasMediaBox => StatedBox(InheritablePageKeys.MediaBox) != null;

    /// <summary>
    /// Gets or sets the crop box: the part of the page a reader shows and prints.
    /// </summary>
    /// <remarks>
    /// Reading it never changes the page. A page that states no crop box answers the empty
    /// rectangle, exactly the value it always answered, but is no longer given
    /// <c>/CropBox [0 0 0 0]</c> for having been asked - a crop box of no size, which crops the
    /// page to nothing. Use <see cref="HasCropBox"/> to ask whether there is one, and
    /// <see cref="EffectiveCropBox"/> for the box a reader will actually use. A crop box stated
    /// on a node of the page tree is copied onto the page when the document is read, so an
    /// inherited one counts as the page's own.
    /// </remarks>
    public PdfRectangle CropBox
    {
        get => BoxOrEmpty(InheritablePageKeys.CropBox);
        set => Elements.SetRectangle(InheritablePageKeys.CropBox, value);
    }

    /// <summary>
    /// Gets a value indicating whether the page states a crop box, either itself or through the
    /// page tree it was read from.
    /// </summary>
    public bool HasCropBox => StatedBox(InheritablePageKeys.CropBox) != null;

    /// <summary>
    /// Gets the crop box a reader uses: the one the page states, or its media box when it states
    /// none (ISO 32000-1 14.11.2), reduced to the part that lies within the media box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading it never changes the page. The result is always written lower-left corner first,
    /// however the page wrote it, and is the empty rectangle when the box lies wholly outside the
    /// media box, or when the page states neither it nor a media box. A page with no media box is
    /// not clipped at all: the crop box it states is answered as it is.
    /// </para>
    /// <para>
    /// The media box is taken as it is written to the file. A page built here as
    /// <see cref="PageOrientation.Landscape"/> holds its media box upright while it is drawn on
    /// and turns it over on the way out, and the other boxes are not turned; so for such a page
    /// this is the media box turned over, not <see cref="MediaBox"/>.
    /// </para>
    /// <para>
    /// The same holds for <see cref="EffectiveBleedBox"/>, <see cref="EffectiveTrimBox"/> and
    /// <see cref="EffectiveArtBox"/>.
    /// </para>
    /// </remarks>
    public PdfRectangle EffectiveCropBox => EffectiveBox(InheritablePageKeys.CropBox);

    /// <summary>
    /// Gets or sets the bleed box: how far the artwork runs past the trim, for a page going to a
    /// press.
    /// </summary>
    /// <remarks>
    /// Reading it never changes the page. A page that states no bleed box answers the empty
    /// rectangle and is left without one; see <see cref="HasBleedBox"/> and
    /// <see cref="EffectiveBleedBox"/>.
    /// </remarks>
    public PdfRectangle BleedBox
    {
        get => BoxOrEmpty(Keys.BleedBox);
        set => Elements.SetRectangle(Keys.BleedBox, value);
    }

    /// <summary>
    /// Gets a value indicating whether the page states a bleed box.
    /// </summary>
    public bool HasBleedBox => StatedBox(Keys.BleedBox) != null;

    /// <summary>
    /// Gets the bleed box a reader uses: the one the page states, or the
    /// <see cref="EffectiveCropBox"/> when it states none (ISO 32000-1 14.11.2), reduced to the
    /// part that lies within the media box.
    /// </summary>
    /// <remarks>
    /// Shaped as <see cref="EffectiveCropBox"/> is, and like it never changes the page.
    /// </remarks>
    public PdfRectangle EffectiveBleedBox => EffectiveBox(Keys.BleedBox);

    /// <summary>
    /// Gets or sets the art box: the extent of the page's meaningful content.
    /// </summary>
    /// <remarks>
    /// Reading it never changes the page. A page that states no art box answers the empty
    /// rectangle and is left without one; see <see cref="HasArtBox"/> and
    /// <see cref="EffectiveArtBox"/>.
    /// </remarks>
    public PdfRectangle ArtBox
    {
        get => BoxOrEmpty(Keys.ArtBox);
        set => Elements.SetRectangle(Keys.ArtBox, value);
    }

    /// <summary>
    /// Gets a value indicating whether the page states an art box.
    /// </summary>
    public bool HasArtBox => StatedBox(Keys.ArtBox) != null;

    /// <summary>
    /// Gets the art box a reader uses: the one the page states, or the
    /// <see cref="EffectiveCropBox"/> when it states none (ISO 32000-1 14.11.2), reduced to the
    /// part that lies within the media box.
    /// </summary>
    /// <remarks>
    /// Shaped as <see cref="EffectiveCropBox"/> is, and like it never changes the page.
    /// </remarks>
    public PdfRectangle EffectiveArtBox => EffectiveBox(Keys.ArtBox);

    /// <summary>
    /// Gets or sets the trim box: the finished page, where the sheet is cut.
    /// </summary>
    /// <remarks>
    /// Reading it never changes the page. A page that states no trim box answers the empty
    /// rectangle and is left without one; see <see cref="HasTrimBox"/> and
    /// <see cref="EffectiveTrimBox"/>.
    /// </remarks>
    public PdfRectangle TrimBox
    {
        get => BoxOrEmpty(Keys.TrimBox);
        set => Elements.SetRectangle(Keys.TrimBox, value);
    }

    /// <summary>
    /// Gets a value indicating whether the page states a trim box.
    /// </summary>
    public bool HasTrimBox => StatedBox(Keys.TrimBox) != null;

    /// <summary>
    /// Gets the trim box a reader uses: the one the page states, or the
    /// <see cref="EffectiveCropBox"/> when it states none (ISO 32000-1 14.11.2), reduced to the
    /// part that lies within the media box.
    /// </summary>
    /// <remarks>
    /// Shaped as <see cref="EffectiveCropBox"/> is, and like it never changes the page.
    /// </remarks>
    public PdfRectangle EffectiveTrimBox => EffectiveBox(Keys.TrimBox);

    /// <summary>
    /// The box the page states under <paramref name="key"/>, or null when it states none. An
    /// entry whose value is null, directly or by reference, or is not an array of four numbers,
    /// is no box at all. Nothing is written to the page.
    /// </summary>
    private PdfRectangle StatedBox(string key) => PdfPageResizer.RectangleOf(this, key);

    private PdfRectangle BoxOrEmpty(string key) => StatedBox(key) ?? new PdfRectangle();

    /// <summary>
    /// The box under <paramref name="key"/> as a reader applies it, following ISO 32000-1
    /// 14.11.2.
    /// </summary>
    /// <remarks>
    /// A box the page does not state defaults to the crop box, and a crop box it does not state
    /// defaults to the media box. The result is then reduced to its intersection with the media
    /// box, because the standard says a box reaching beyond the media box is effectively reduced
    /// to that. <see cref="EffectiveCropBox"/> says what the caller sees.
    /// </remarks>
    private PdfRectangle EffectiveBox(string key)
    {
        var media = StatedBox(InheritablePageKeys.MediaBox);
        if (media != null)
            media = LowerLeftFirst(MediaBoxIsTurnedWhenWritten ? TurnedOver(media) : media);

        var box = StatedBox(key);
        if (box == null && key != InheritablePageKeys.CropBox)
            box = StatedBox(InheritablePageKeys.CropBox);

        if (box == null)
            return media ?? new PdfRectangle();

        box = LowerLeftFirst(box);
        if (media == null)
            return box;

        var x1 = Math.Max(box.X1, media.X1);
        var y1 = Math.Max(box.Y1, media.Y1);
        var x2 = Math.Min(box.X2, media.X2);
        var y2 = Math.Min(box.Y2, media.Y2);
        return x1 <= x2 && y1 <= y2 ? new PdfRectangle(x1, y1, x2, y2) : new PdfRectangle();
    }

    private static PdfRectangle LowerLeftFirst(PdfRectangle box) =>
        new(Math.Min(box.X1, box.X2), Math.Min(box.Y1, box.Y2),
            Math.Max(box.X1, box.X2), Math.Max(box.Y1, box.Y2));

    /// <summary>
    /// The media box the way <see cref="WriteObject"/> turns it over for a landscape page.
    /// </summary>
    private static PdfRectangle TurnedOver(PdfRectangle mediaBox) =>
        new(mediaBox.X1, mediaBox.Y1, mediaBox.Y2, mediaBox.X2);

    /// <summary>
    /// Gets or sets the height of the page. If orientation is Landscape, this function applies to
    /// the width.
    /// </summary>
    public XUnit Height
    {
        get
        {
            // The page a caller asked for, where saving has since grown the media box into a
            // sheet larger than it. Trimming does not resize the page; it puts more paper
            // around it.
            if (_sheet.TrimmedSize is { } trimmed)
                return trimmed.Height;

            var rect = MediaBox;
            return VisibleSizeIsTurned ? rect.Width : rect.Height;
        }
        set
        {
            RefuseToResizeByReboxing(nameof(Height));

            var rect = MediaBox;
            if (VisibleSizeIsTurned)
                MediaBox = new PdfRectangle(0, rect.Y1, value, rect.Y2);
            else
                MediaBox = new PdfRectangle(rect.X1, 0, rect.X2, value);
            _pageSize = PageSize.Undefined;
        }
    }

    /// <summary>
    /// Gets or sets the width of the page. If orientation is Landscape, this function applies to
    /// the height.
    /// </summary>
    public XUnit Width
    {
        get
        {
            if (_sheet.TrimmedSize is { } trimmed)
                return trimmed.Width;

            var rect = MediaBox;
            return VisibleSizeIsTurned ? rect.Height : rect.Width;
        }
        set
        {
            RefuseToResizeByReboxing(nameof(Width));

            var rect = MediaBox;
            if (VisibleSizeIsTurned)
                MediaBox = new PdfRectangle(rect.X1, 0, rect.X2, value);
            else
                MediaBox = new PdfRectangle(0, rect.Y1, value, rect.Y2);
            _pageSize = PageSize.Undefined;
        }
    }

    /// <summary>
    /// Gets or sets the /Rotate entry of the PDF page. The value is the number of degrees by which the page
    /// should be rotated clockwise when displayed or printed. The value must be a multiple of 90, and 0
    /// is answered when the page has no entry. A page read from a file carries the value it inherits
    /// from the page tree when it has none of its own. Setting it writes /Rotate on the page itself.
    /// </summary>
    public int Rotate
    {
        get => _elements.GetInteger(InheritablePageKeys.Rotate);
        set
        {
            if (value / 90 * 90 != value)
                throw new ArgumentException("Value must be a multiple of 90.");
            _elements.SetInteger(InheritablePageKeys.Rotate, value);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the /Rotate entry turns the page by a quarter, which
    /// makes the viewer show the width of the page where its height is stored.
    /// </summary>
    internal bool IsTurnedByAQuarter => Math.Abs(Rotate / 90) % 2 == 1;

    /// <summary>
    /// Gets a value indicating whether the media box is turned when the page is written. It is
    /// always held in portrait, so a landscape page is turned on the way out - unless the
    /// /Rotate entry already turns it, which the viewer does on its own.
    /// </summary>
    internal bool MediaBoxIsTurnedWhenWritten => _orientation == PageOrientation.Landscape && !IsTurnedByAQuarter;

    /// <summary>
    /// Gets a value indicating whether the page the viewer shows is as wide as the media box
    /// held here is high, which is the case when either of the two turns applies to it.
    /// </summary>
    private bool VisibleSizeIsTurned => MediaBoxIsTurnedWhenWritten || IsTurnedByAQuarter;

    /// <summary>
    /// Gets the size of the media box of this page as it is written to the file. It is the
    /// area drawing on the page ends up in, before the viewer turns it.
    /// </summary>
    internal XSize StoredSize
    {
        get
        {
            var rect = MediaBox;
            return MediaBoxIsTurnedWhenWritten
                ? new XSize(rect.Height, rect.Width)
                : new XSize(rect.Width, rect.Height);
        }
    }

    /// <summary>
    /// Gives the page the media box a resize has worked out for it, and forgets the authoring
    /// orientation while doing so.
    /// <para>
    /// A page marked <see cref="PageOrientation.Landscape"/> keeps its media box the other way
    /// round in memory and turns it over on the way out (see <c>WriteObject</c>). A resize has
    /// just written the content into a box of a known shape, so the box it hands over here is the
    /// one that belongs in the file, and any further turning would undo the work. The orientation
    /// therefore goes back to <see cref="PageOrientation.Portrait"/>, which is the setting that
    /// means "the media box is already right". Any /Rotate entry is left alone: that turn belongs
    /// to the viewer and the resize took it into account.
    /// </para>
    /// </summary>
    internal void ApplyResizedBox(PdfRectangle box, PageSize size)
    {
        _orientation = PageOrientation.Portrait;
        MediaBox = box;
        _pageSize = size;
    }

    /// <summary>
    /// The content stream currently used by an XGraphics object for rendering.
    /// </summary>
    internal PdfContent RenderContent;

    /// <summary>
    /// Gets the array of content streams of the page.
    /// </summary>
    public PdfContents Contents
    {
        get
        {
            if (_contents == null)
            {
                if (true) // || Document.IsImported)
                {
                    var item = Elements[Keys.Contents];
                    if (item == null)
                    {
                        _contents = new PdfContents(Owner);
                    }
                    else
                    {
                        if (item is PdfReference)
                            item = ((PdfReference)item).Value;

                        var array = item as PdfArray;
                        if (array != null)
                        {
                            // It is already an array of content streams.
                            if (array.IsIndirect)
                            {
                                // Make it a direct array
                                array = array.Clone();
                                array.Document = Owner;
                            }
                            _contents = new PdfContents(array);
                        }
                        else
                        {
                            // Only one content stream -> create array
                            _contents = new PdfContents(Owner);
                            var content = new PdfContent((PdfDictionary)item);
                            _contents.Elements.Add(content.Reference);
                        }
                    }
                }
                Debug.Assert(_contents.Reference == null);
                Elements[Keys.Contents] = _contents;
            }
            return _contents;
        }
    }
    private PdfContents _contents;

    #region Annotations

    /// <summary>
    /// Gets a value indicating whether this page has an annotations array.
    /// </summary>
    /// <remarks>
    ///   Asking does not give the page one, so a page with no annotations is left without the
    ///   empty array that reading <see cref="Annotations"/> would add. A page whose annotations
    ///   are a reference to an object the file never defines has none either.
    /// </remarks>
    public bool HasAnnotations
    {
        get
        {
            if (_annotations == null)
            {
                // Get annotations array if exists.
                _annotations = Elements.GetValue(Keys.Annots) as PdfAnnotations;
                if (_annotations != null)
                    _annotations.Page = this;
            }
            return _annotations != null;
        }
    }

    /// <summary>
    /// Gets the annotations array of this page.
    /// </summary>
    public PdfAnnotations Annotations
    {
        get
        {
            if (_annotations == null)
            {
                // Get or create annotations array.
                _annotations = (PdfAnnotations)Elements.GetValue(Keys.Annots, VCF.Create);
                _annotations.Page = this;
            }
            return _annotations;
        }
    }
    private PdfAnnotations _annotations;

    /// <summary>
    /// Adds an intra document link.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <param name="destinationPage">The destination page.</param>
    public PdfLinkAnnotation AddDocumentLink(PdfRectangle rect, int destinationPage)
    {
        var annotation = PdfLinkAnnotation.CreateDocumentLink(rect, destinationPage);
        Annotations.Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Adds an intra document link to a place on a page.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="destinationTop">
    /// How far up the destination page to place the top of the window, in default page
    /// coordinates. NaN lands the reader wherever the page is already scrolled to.
    /// </param>
    public PdfLinkAnnotation AddDocumentLink(PdfRectangle rect, int destinationPage, double destinationTop)
    {
        var annotation = PdfLinkAnnotation.CreateDocumentLink(rect, destinationPage, destinationTop);
        Annotations.Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Adds a link to the Web.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <param name="url">The URL.</param>
    public PdfLinkAnnotation AddWebLink(PdfRectangle rect, string url)
    {
        var annotation = PdfLinkAnnotation.CreateWebLink(rect, url);
        Annotations.Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Adds a link to a named destination of this document.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <param name="destinationName">The name, as given to <see cref="PdfDocument.NamedDestinations"/>.</param>
    public PdfLinkAnnotation AddNamedLink(PdfRectangle rect, string destinationName)
    {
        var annotation = PdfLinkAnnotation.CreateNamedLink(rect, destinationName);
        Annotations.Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Adds a link to a file.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <param name="fileName">Name of the file.</param>
    public PdfLinkAnnotation AddFileLink(PdfRectangle rect, string fileName)
    {
        var annotation = PdfLinkAnnotation.CreateFileLink(rect, fileName);
        Annotations.Add(annotation);
        return annotation;
    }

    #endregion

    /// <summary>
    /// Gets or sets the custom values.
    /// </summary>
    public PdfCustomValues CustomValues
    {
        get
        {
            if (_customValues == null)
                _customValues = PdfCustomValues.Get(Elements);
            return _customValues;
        }
        set
        {
            if (value != null)
                throw new ArgumentException("Only null is allowed to clear all custom values.");
            PdfCustomValues.Remove(Elements);
            _customValues = null;
        }
    }
    private PdfCustomValues _customValues;

    /// <summary>
    /// Gets the PdfResources object of this page.
    /// </summary>
    public PdfResources Resources
    {
        get
        {
            if (_resources == null)
                _resources = (PdfResources)Elements.GetValue(InheritablePageKeys.Resources, VCF.Create); //VCF.CreateIndirect
            return _resources;
        }
    }
    private PdfResources _resources;

    /// <summary>
    /// Gives the page a resource dictionary in place of the one it has. The page reads its
    /// resources but once and keeps them, so the two have to be replaced together.
    /// </summary>
    internal void ReplaceResources(PdfResources resources)
    {
        Elements[InheritablePageKeys.Resources] = resources;
        _resources = null;
    }

    /// <summary>
    /// Gives the page a single content stream in place of whatever it had. The page keeps the
    /// content it was first asked for, in the same way it keeps its resources, so the two have to
    /// be replaced together or the page goes on handing out the streams it no longer holds.
    /// </summary>
    internal void ReplaceContents(PdfContent content)
    {
        Debug.Assert(content.Reference != null, "The content has to be indirect to be referred to.");

        // The array itself stays direct, which is how the Contents property builds one and what
        // the writer expects: PdfArray.WriteObject writes no brackets round an indirect array, so
        // making this one indirect writes a page whose /Contents is a bare reference and an
        // object that is not an object, and the file will not open again.
        var contents = new PdfContents(Owner);
        contents.Elements.Add(content.Reference);

        Elements[Keys.Contents] = contents;
        _contents = contents;
    }

    /// <summary>
    /// Reads the content of the page and returns the images it draws, each with the transform
    /// it is drawn under, in the order the content draws them.
    /// <para>
    /// An image is stored with its first row of samples at the top, and the page decides which
    /// way up that ends up: a writer of PDF may store an image upside down and turn it back
    /// over with a negative vertical scale as it draws it. Code that pulls the stream out of an
    /// image XObject and saves it therefore gets an image the wrong way up without being told,
    /// which is what <see cref="PdfImagePlacement.Orientation"/> is for.
    /// </para>
    /// <para>
    /// The images drawn by the forms the content draws are included, under the transforms those
    /// forms are drawn under. Inline images are not: the end of one can only be guessed at, and
    /// a transform read out of the middle of image data would say the wrong thing about the
    /// images after it, so the rest of that stream is left unread. Images drawn by the
    /// appearance of an annotation are not included either, being no part of the content of
    /// the page.
    /// </para>
    /// </summary>
    public IList<PdfImagePlacement> GetImagePlacements()
    {
        return PdfImagePlacementReader.Read(this);
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    PdfResources IContentStream.Resources => Resources;

    /// <summary>
    /// Gets the resource name of the specified font within this page.
    /// </summary>
    internal string GetFontName(XFont font, out PdfFont pdfFont)
    {
        pdfFont = _document.FontTable.GetFont(font);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(XFont font, out PdfFont pdfFont)
    {
        return GetFontName(font, out pdfFont);
    }

    /// <summary>
    /// Embeds the given font program in the document under a name of the caller's choosing and adds
    /// it to this page's resources, returning the resource name a content stream refers to it by.
    /// <para>
    /// The program is embedded as a composite font with Identity-H encoding, so a show-text operator
    /// written against it gives glyph indices rather than character codes. It is for a caller writing
    /// its own content stream; text drawn through <see cref="Drawing.XGraphics"/> names its font with
    /// an <see cref="Drawing.XFont"/> and needs none of this.
    /// </para>
    /// <para>
    /// The name identifies the program within the document. Asking twice under the same name embeds
    /// it once and answers the same resource, whatever bytes the second call was handed - which is
    /// also what lets <see cref="TryGetFontProgramName"/> find it again.
    /// </para>
    /// </summary>
    /// <param name="idName">The caller's name for the font program. Not a PDF name; no slash.</param>
    /// <param name="fontData">The font program itself, as an OpenType or TrueType file.</param>
    public string AddFontProgram(string idName, byte[] fontData)
    {
        if (idName == null)
            throw new ArgumentNullException(nameof(idName));
        if (idName.Length == 0)
            throw new ArgumentException(@"The name of a font program must not be empty.", nameof(idName));
        if (fontData == null)
            throw new ArgumentNullException(nameof(fontData));

        return GetFontName(idName, fontData, out _);
    }

    /// <summary>
    /// Gets the resource name of a font program already embedded in the document under this name and
    /// added to this page, or null if the document has none by that name.
    /// </summary>
    public string TryGetFontProgramName(string idName)
    {
        if (idName == null)
            throw new ArgumentNullException(nameof(idName));

        return TryGetFontName(idName, out _);
    }

    /// <summary>
    /// Tries to get the resource name of the specified font data within this page.
    /// Returns null if no such font exists.
    /// </summary>
    internal string TryGetFontName(string idName, out PdfFont pdfFont)
    {
        pdfFont = _document.FontTable.TryGetFont(idName);
        string name = null;
        if (pdfFont != null)
            name = Resources.AddFont(pdfFont);
        return name;
    }

    /// <summary>
    /// Gets the resource name of the specified font data within this page.
    /// </summary>
    internal string GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        pdfFont = _document.FontTable.GetFont(idName, fontData);
        Debug.Assert(pdfFont != null);
        var name = Resources.AddFont(pdfFont);
        return name;
    }

    string IContentStream.GetFontName(string idName, byte[] fontData, out PdfFont pdfFont)
    {
        return GetFontName(idName, fontData, out pdfFont);
    }

    /// <summary>
    /// Gets the resource name of the specified image within this page.
    /// </summary>
    internal string GetImageName(XImage image)
    {
        var pdfImage = _document.ImageTable.GetImage(image);
        Debug.Assert(pdfImage != null);
        NoteTransparencyOf(pdfImage);
        var name = Resources.AddImage(pdfImage);
        return name;
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    string IContentStream.GetImageName(XImage image)
    {
        return GetImageName(image);
    }

    /// <summary>
    /// Gets the resource name of the specified form within this page.
    /// </summary>
    internal string GetFormName(XForm form)
    {
        var pdfForm = _document.FormTable.GetForm(form);
        Debug.Assert(pdfForm != null);
        NoteTransparencyOf(pdfForm);
        var name = Resources.AddForm(pdfForm);
        return name;
    }

    /// <summary>
    /// Implements the interface because the primary function is internal.
    /// </summary>
    string IContentStream.GetFormName(XForm form)
    {
        return GetFormName(form);
    }

    internal override void WriteObject(PdfWriter writer)
    {
        // HACK: temporarily flip media box if Landscape. A page with no media box has nothing to
        // turn, and turning the empty rectangle would write one.
        var mediaBox = MediaBox;
        var turned = MediaBoxIsTurnedWhenWritten && HasMediaBox;
        if (turned)
            MediaBox = TurnedOver(mediaBox);

        // A page that paints with transparency is given a transparency group, without which
        // Adobe's viewer renders it wrongly. A page that does not paint with any is left as it
        // is: a group is not free, it tells a reader to composite the page as a unit against the
        // backdrop, and stamping one onto a page that came in without it means a document that
        // was only read and written back does not come out the way it went in.
        //
        // Rgb is the default for the ColorMode, but a caller who sets it to Undefined is asking
        // for no colour space to be imposed, and that is respected by skipping the group.
        if (TransparencyUsed && !Elements.ContainsKey(Keys.Group) &&
            _document.Options.ColorMode != PdfColorMode.Undefined)
        {
            var group = new PdfDictionary();
            _elements["/Group"] = group;
            if (_document.Options.ColorMode != PdfColorMode.Cmyk)
                group.Elements.SetName("/CS", "/DeviceRGB");
            else
                group.Elements.SetName("/CS", "/DeviceCMYK");
            group.Elements.SetName("/S", "/Transparency");
            // /I and /K are left out: both default to false.
        }
        base.WriteObject(writer);

        if (turned)
            MediaBox = mediaBox;
    }

    /// <summary>
    /// Whether anything drawn on this page paints with transparency, and so whether the page has
    /// to be given a transparency group when it is written.
    /// <para>
    /// Set from two places: the renderer, when it realizes a colour that is not fully opaque, and
    /// <see cref="NoteTransparencyOf"/>, when an image or a form that paints with transparency is
    /// drawn on the page. Content that was already on an imported page does not set it - what
    /// that page needed, it already carries.
    /// </para>
    /// </summary>
    internal bool TransparencyUsed;

    /// <summary>
    /// Notes that the page will need a transparency group if the XObject being drawn on it paints
    /// with transparency.
    /// </summary>
    /// <remarks>
    /// Nothing is examined once the page is known to need a group. The answer cannot turn back,
    /// and working it out means walking the resources of a form and of every form within it.
    /// </remarks>
    private void NoteTransparencyOf(PdfDictionary xObject)
    {
        if (!TransparencyUsed && PdfTransparencyDetector.UsesTransparency(xObject))
            TransparencyUsed = true;
    }

    /// <summary>
    /// The item an inheritable entry holds, or null when there is no entry or the entry is null.
    /// </summary>
    /// <remarks>
    /// ISO 32000-1 7.3.7 makes an entry whose value is the null object the same as no entry at
    /// all, and a reference to null (7.3.10) is a null value too. So a page tree node that says
    /// null passes on nothing and leaves whatever a node above it stated in place, and a page
    /// that says null inherits as though it had said nothing.
    /// </remarks>
    private static PdfItem InheritableEntry(PdfDictionary dictionary, string key)
    {
        var item = dictionary.Elements[key];
        var value = item is PdfReference reference ? reference.Value : item;
        return value is null or PdfNull or PdfNullObject ? null : item;
    }

    /// <summary>
    /// Inherit values from parent node.
    /// </summary>
    internal static void InheritValues(PdfDictionary page, InheritedValues values)
    {
        // HACK: I'M ABSOLUTELY NOT SURE WHETHER THIS CODE COVERS ALL CASES.
        if (values.Resources != null)
        {
            PdfDictionary resources;
            var res = InheritableEntry(page, InheritablePageKeys.Resources);
            if (res is PdfReference)
            {
                resources = (PdfDictionary)((PdfReference)res).Value.Clone();
                resources.Document = page.Owner;
            }
            else
                resources = (PdfDictionary)res;

            if (resources == null)
            {
                resources = values.Resources.Clone();
                resources.Document = page.Owner;
                page.Elements.Add(InheritablePageKeys.Resources, resources);
            }
            else
            {
                foreach (var name in values.Resources.Elements.KeyNames)
                {
                    if (!resources.Elements.ContainsKey(name.Value))
                    {
                        var item = values.Resources.Elements[name];
                        if (item is PdfObject)
                            item = item.Clone();
                        resources.Elements.Add(name.ToString(), item);
                    }
                }
            }
        }

        if (values.MediaBox != null && InheritableEntry(page, InheritablePageKeys.MediaBox) == null)
            page.Elements[InheritablePageKeys.MediaBox] = values.MediaBox;

        if (values.CropBox != null && InheritableEntry(page, InheritablePageKeys.CropBox) == null)
            page.Elements[InheritablePageKeys.CropBox] = values.CropBox;

        if (values.Rotate != null && InheritableEntry(page, InheritablePageKeys.Rotate) == null)
            page.Elements[InheritablePageKeys.Rotate] = values.Rotate;
    }

    /// <summary>
    /// Add all inheritable values from the specified page to the specified values structure.
    /// </summary>
    internal static void InheritValues(PdfDictionary page, ref InheritedValues values)
    {
        // A null entry is skipped rather than recorded, so it neither reaches the pages below as
        // an empty box nor overrides what a node further up stated.
        var item = InheritableEntry(page, InheritablePageKeys.Resources);
        if (item != null)
        {
            var reference = item as PdfReference;
            if (reference != null)
                values.Resources = (PdfDictionary)(reference.Value);
            else
                values.Resources = (PdfDictionary)item;
        }

        item = InheritableEntry(page, InheritablePageKeys.MediaBox);
        if (item != null)
            values.MediaBox = new PdfRectangle(item);

        item = InheritableEntry(page, InheritablePageKeys.CropBox);
        if (item != null)
            values.CropBox = new PdfRectangle(item);

        item = InheritableEntry(page, InheritablePageKeys.Rotate);
        if (item != null)
        {
            if (item is PdfReference)
                item = ((PdfReference)item).Value;
            values.Rotate = (PdfInteger)item;
        }
    }

    internal override void PrepareForSave()
    {
        // An XGraphics that was never disposed still holds the content stream it has been
        // appending to. Close it here rather than leaving it to the writer, so that whatever it
        // wrote counts towards the decision below. This is the "close all open content streams"
        // the page tree has wanted for a long time.
        RenderContent?.PdfRenderer?.Close();

        RemoveEmptyContentStreams();

        _sheet.WriteBoxesIfTrimmed();
    }

    /// <summary>
    /// Draws the eight standard crop marks in the room <see cref="MarkMargins"/> leaves outside
    /// the bleed, telling the trimmer where to cut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two marks meet at each corner of the trimmed page, one on each of its edges, and each runs
    /// outwards from the bleed to the edge of the sheet. None crosses the bleed, so none can be
    /// mistaken for artwork or land on any part of the page that survives the cut.
    /// </para>
    /// <para>
    /// Called automatically when a page with both a bleed and a mark allowance is saved, so a
    /// caller normally never calls it. It is public for the caller who wants the marks somewhere
    /// else - on a form, say - and calling it twice draws them once.
    /// </para>
    /// <para>
    /// The marks are appended as a content stream of their own, in the sheet's coordinates, so
    /// they are unaffected by whatever transformation the page was drawn under and are painted
    /// after everything already on the page - whenever this is called.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The page has no trim margin, or no room outside it to put the marks in.
    /// </exception>
    public void DrawCropMarks() => _sheet.DrawCropMarks();

    /// <summary>
    /// Drops the content streams that hold nothing.
    /// </summary>
    /// <remarks>
    /// An XGraphics appends a content stream to the page the moment it is created, before
    /// anything has been asked of it, so one that is closed without a mark being made leaves that
    /// stream behind empty. Written out it is worse than useless: an empty stream compresses to
    /// eight bytes of zlib framing around nothing, and Acrobat reports that as an error on the
    /// page. A stream with nothing in it draws nothing either way, so there is no reason to keep
    /// one. The object itself is swept up by the unreachable-object pass that follows.
    /// </remarks>
    private void RemoveEmptyContentStreams()
    {
        // Only a page whose contents have already been read. Reading them here would give a page
        // that has no /Contents at all an empty array it never had.
        if (_contents == null)
            return;

        for (var idx = _contents.Elements.Count - 1; idx >= 0; idx--)
        {
            var content = _contents.Elements.GetDictionary(idx);
            if (content != null && (content.Stream == null || content.Stream.Length == 0))
                _contents.Elements.RemoveAt(idx);
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed class Keys : InheritablePageKeys
    {
        /// <summary>
        /// (Required) The type of PDF object that this dictionary describes;
        /// must be Page for a page object.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "Page")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required; must be an indirect reference)
        /// The page tree node that is the immediate parent of this page object.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required | KeyType.MustBeIndirect)]
        public const string Parent = "/Parent";

        /// <summary>
        /// (Required if PieceInfo is present; optional otherwise; PDF 1.3) The date and time
        /// when the page's contents were most recently modified. If a page-piece dictionary
        /// (PieceInfo) is present, the modification date is used to ascertain which of the
        /// application data dictionaries that it contains correspond to the current content
        /// of the page.
        /// </summary>
        [KeyInfo(KeyType.Date)]
        public const string LastModified = "/LastModified";

        /// <summary>
        /// (Optional; PDF 1.3) A rectangle, expressed in default user space units, defining the
        /// region to which the contents of the page should be clipped when output in a production
        /// environment. Default value: the value of CropBox.
        /// </summary>
        [KeyInfo("1.3", KeyType.Rectangle | KeyType.Optional)]
        public const string BleedBox = "/BleedBox";

        /// <summary>
        /// (Optional; PDF 1.3) A rectangle, expressed in default user space units, defining the
        /// intended dimensions of the finished page after trimming. Default value: the value of
        /// CropBox.
        /// </summary>
        [KeyInfo("1.3", KeyType.Rectangle | KeyType.Optional)]
        public const string TrimBox = "/TrimBox";

        /// <summary>
        /// (Optional; PDF 1.3) A rectangle, expressed in default user space units, defining the
        /// extent of the page's meaningful content (including potential white space) as intended
        /// by the page's creator. Default value: the value of CropBox.
        /// </summary>
        [KeyInfo("1.3", KeyType.Rectangle | KeyType.Optional)]
        public const string ArtBox = "/ArtBox";

        /// <summary>
        /// (Optional; PDF 1.4) A box color information dictionary specifying the colors and other
        /// visual characteristics to be used in displaying guidelines on the screen for the various
        /// page boundaries. If this entry is absent, the application should use its own current
        /// default settings.
        /// </summary>
        [KeyInfo("1.4", KeyType.Dictionary | KeyType.Optional)]
        public const string BoxColorInfo = "/BoxColorInfo";

        /// <summary>
        /// (Optional) A content stream describing the contents of this page. If this entry is absent,
        /// the page is empty. The value may be either a single stream or an array of streams. If the
        /// value is an array, the effect is as if all of the streams in the array were concatenated,
        /// in order, to form a single stream. This allows PDF producers to create image objects and
        /// other resources as they occur, even though they interrupt the content stream. The division
        /// between streams may occur only at the boundaries between lexical tokens but is unrelated
        /// to the page's logical content or organization. Applications that consume or produce PDF
        /// files are not required to preserve the existing structure of the Contents array.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Stream | KeyType.Optional)]
        public const string Contents = "/Contents";

        /// <summary>
        /// (Optional; PDF 1.4) A group attributes dictionary specifying the attributes of the page's
        /// page group for use in the transparent imaging model.
        /// </summary>
        [KeyInfo("1.4", KeyType.Dictionary | KeyType.Optional)]
        public const string Group = "/Group";

        /// <summary>
        /// (Optional) A stream object defining the page's thumbnail image.
        /// </summary>
        [KeyInfo(KeyType.Stream | KeyType.Optional)]
        public const string Thumb = "/Thumb";

        /// <summary>
        /// (Optional; PDF 1.1; recommended if the page contains article beads) An array of indirect
        /// references to article beads appearing on the page. The beads are listed in the array in
        /// natural reading order.
        /// </summary>
        [KeyInfo("1.1", KeyType.Array | KeyType.Optional)]
        public const string B = "/B";

        /// <summary>
        /// (Optional; PDF 1.1) The page's display duration (also called its advance timing): the
        /// maximum length of time, in seconds, that the page is displayed during presentations before
        /// the viewer application automatically advances to the next page. By default, the viewer does
        /// not advance automatically.
        /// </summary>
        [KeyInfo("1.1", KeyType.Real | KeyType.Optional)]
        public const string Dur = "/Dur";

        /// <summary>
        /// (Optional; PDF 1.1) A transition dictionary describing the transition effect to be used
        /// when displaying the page during presentations.
        /// </summary>
        [KeyInfo("1.1", KeyType.Dictionary | KeyType.Optional)]
        public const string Trans = "/Trans";

        /// <summary>
        /// (Optional) An array of annotation dictionaries representing annotations associated with
        /// the page.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional, typeof(PdfAnnotations))]
        public const string Annots = "/Annots";

        /// <summary>
        /// (Optional; PDF 1.2) An additional-actions dictionary defining actions to be performed
        /// when the page is opened or closed.
        /// </summary>
        [KeyInfo("1.2", KeyType.Dictionary | KeyType.Optional)]
        public const string AA = "/AA";

        /// <summary>
        /// (Optional; PDF 1.4) A metadata stream containing metadata for the page.
        /// </summary>
        [KeyInfo("1.4", KeyType.Stream | KeyType.Optional)]
        public const string Metadata = "/Metadata";

        /// <summary>
        /// (Optional; PDF 1.3) A page-piece dictionary associated with the page.
        /// </summary>
        [KeyInfo("1.3", KeyType.Dictionary | KeyType.Optional)]
        public const string PieceInfo = "/PieceInfo";

        /// <summary>
        /// (Required if the page contains structural content items; PDF 1.3)
        /// The integer key of the page's entry in the structural parent tree.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string StructParents = "/StructParents";

        /// <summary>
        /// (Optional; PDF 1.3; indirect reference preferred) The digital identifier of
        /// the page's parent Web Capture content set.
        /// </summary>
        [KeyInfo("1.3", KeyType.String | KeyType.Optional)]
        public const string ID = "/ID";

        /// <summary>
        /// (Optional; PDF 1.3) The page's preferred zoom (magnification) factor: the factor
        /// by which it should be scaled to achieve the natural display magnification.
        /// </summary>
        [KeyInfo("1.3", KeyType.Real | KeyType.Optional)]
        public const string PZ = "/PZ";

        /// <summary>
        /// (Optional; PDF 1.3) A separation dictionary containing information needed
        /// to generate color separations for the page.
        /// </summary>
        [KeyInfo("1.3", KeyType.Dictionary | KeyType.Optional)]
        public const string SeparationInfo = "/SeparationInfo";

        /// <summary>
        /// (Optional; PDF 1.5) A name specifying the tab order to be used for annotations
        /// on the page. The possible values are R (row order), C (column order),
        /// and S (structure order).
        /// </summary>
        [KeyInfo("1.5", KeyType.Name | KeyType.Optional)]
        public const string Tabs = "/Tabs";

        /// <summary>
        /// (Required if this page was created from a named page object; PDF 1.5)
        /// The name of the originating page object.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string TemplateInstantiated = "/TemplateInstantiated";

        /// <summary>
        /// (Optional; PDF 1.5) A navigation node dictionary representing the first node
        /// on the page.
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string PresSteps = "/PresSteps";

        /// <summary>
        /// (Optional; PDF 1.6) A positive number giving the size of default user space units,
        /// in multiples of 1/72 inch. The range of supported values is implementation-dependent.
        /// </summary>
        [KeyInfo("1.6", KeyType.Real | KeyType.Optional)]
        public const string UserUnit = "/UserUnit";

        /// <summary>
        /// (Optional; PDF 1.6) An array of viewport dictionaries specifying rectangular regions
        /// of the page.
        /// </summary>
        [KeyInfo("1.6", KeyType.Dictionary | KeyType.Optional)]
        public const string VP = "/VP";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;

    /// <summary>
    /// Predefined keys common to PdfPage and PdfPages.
    /// </summary>
    internal class InheritablePageKeys : KeysBase
    {
        /// <summary>
        /// (Required; inheritable) A dictionary containing any resources required by the page.
        /// If the page requires no resources, the value of this entry should be an empty dictionary.
        /// Omitting the entry entirely indicates that the resources are to be inherited from an
        /// ancestor node in the page tree.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required | KeyType.Inheritable, typeof(PdfResources))]
        public const string Resources = "/Resources";

        /// <summary>
        /// (Required; inheritable) A rectangle, expressed in default user space units, defining the
        /// boundaries of the physical medium on which the page is intended to be displayed or printed.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Required | KeyType.Inheritable)]
        public const string MediaBox = "/MediaBox";

        /// <summary>
        /// (Optional; inheritable) A rectangle, expressed in default user space units, defining the
        /// visible region of default user space. When the page is displayed or printed, its contents
        /// are to be clipped (cropped) to this rectangle and then imposed on the output medium in some
        /// implementation defined manner. Default value: the value of MediaBox.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Optional | KeyType.Inheritable)]
        public const string CropBox = "/CropBox";

        /// <summary>
        /// (Optional; inheritable) The number of degrees by which the page should be rotated clockwise
        /// when displayed or printed. The value must be a multiple of 90. Default value: 0.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Rotate = "/Rotate";
    }

    /// <summary>
    /// Values inherited from a parent in the parent chain of a page tree.
    /// </summary>
    internal struct InheritedValues
    {
        public PdfDictionary Resources;
        public PdfRectangle MediaBox;
        public PdfRectangle CropBox;
        public PdfInteger Rotate;
    }
}
