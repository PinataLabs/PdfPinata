using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Freehand ink - one or more strokes of a pen - drawn on the page as an annotation rather than as
/// page content. ISO 32000-1 section 12.5.6.13.
/// </summary>
/// <remarks>
/// <para>
/// Every reader that draws an <c>/Ink</c> at all draws it from its appearance stream when there is
/// one, and several draw nothing without, so this class builds one and rebuilds it whenever the
/// strokes, the colour, the width or the opacity change - as <see cref="PdfLineAnnotation"/> does.
/// </para>
/// <para>
/// Like a line, the rectangle is not the caller's to set: <c>/Rect</c> has to enclose every stroke
/// and the half of the pen's width either side of it, so it is computed from the strokes each time
/// they change, and assigning <see cref="PdfAnnotation.Rectangle"/> is overwritten.
/// </para>
/// <para>
/// Points are in default user space - measured up from the bottom left of the page, like
/// <c>/Rect</c> - and not the top-left world space <see cref="XGraphics"/> draws in;
/// <c>gfx.Transformer.WorldToDefaultPage</c> has an <see cref="XPoint"/> overload for exactly this.
/// Each stroke is drawn as straight segments between its points, with round joins and caps, which
/// is what a pen does.
/// </para>
/// </remarks>
public sealed class PdfInkAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInkAnnotation"/> class.
    /// </summary>
    public PdfInkAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInkAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfInkAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfInkAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    private void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Ink");

        // /InkList is required, so it is there from the start even while it holds nothing; the
        // defaults are visible ones, for the reason PdfLineAnnotation gives.
        Elements[Keys.InkList] = new PdfArray();
        Color = XColors.Black;
        BorderWidth = 1;
    }

    /// <summary>
    /// The strokes, each the points the pen passed through in order, in default user space.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<XPoint>> Strokes
    {
        get
        {
            var strokes = new List<IReadOnlyList<XPoint>>();
            var list = Elements.GetArray(Keys.InkList);
            if (list == null)
                return strokes;

            for (var index = 0; index < list.Elements.Count; index++)
            {
                var stroke = list.Elements.GetArray(index);
                if (stroke != null)
                    strokes.Add(PointArrays.Read(stroke));
            }

            return strokes;
        }
    }

    /// <summary>
    /// Adds a stroke through the given points, in default user space.
    /// </summary>
    /// <param name="points">At least two points: a stroke has somewhere to go.</param>
    public void AddStroke(params XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException("A stroke needs at least two points.", nameof(points));

        var list = Elements.GetArray(Keys.InkList);
        if (list == null)
        {
            list = new PdfArray();
            Elements[Keys.InkList] = list;
        }

        list.Elements.Add(PointArrays.Write(points));
        Touch();
    }

    /// <summary>
    /// Takes every stroke away, which leaves nothing to draw and so no appearance.
    /// </summary>
    public void ClearStrokes()
    {
        Elements[Keys.InkList] = new PdfArray();
        Touch();
    }

    /// <summary>
    /// The width of the pen, in points - <c>/BS</c>'s <c>/W</c>. Zero draws nothing at all.
    /// </summary>
    public double BorderWidth
    {
        get => BorderWidthFrom(Elements.GetDictionary(PdfAnnotation.Keys.BS));
        set
        {
            Elements[PdfAnnotation.Keys.BS] = SolidBorder(value);
            Touch();
        }
    }

    internal override void OnAddedToPage()
    {
        RebuildAppearance();
    }

    internal override void OnAppearanceInvalidated()
    {
        RebuildAppearance();
    }

    private void Touch()
    {
        Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        RebuildAppearance();
    }

    private void RebuildAppearance()
    {
        // Until it is on a page there is no document to make a form in. OnAddedToPage calls this
        // again once there is, so nothing set beforehand is lost.
        if (Owner == null)
            return;

        var strokes = Strokes.Where(stroke => stroke.Count >= 2).ToList();
        var width = BorderWidth;

        if (!PointArrays.TryEnclose(strokes.SelectMany(stroke => stroke), width / 2, out var box))
        {
            RemoveAppearance();
            return;
        }

        // /Rect is required, so it is written even when what is inside it draws nothing.
        Elements.SetRectangle(PdfAnnotation.Keys.Rect, box);

        // Nothing to draw with, or a box under XForm's floor of a point in either direction.
        if (width <= 0 || box.Width < 1 || box.Height < 1)
        {
            RemoveAppearance();
            return;
        }

        var pen = new XPen(Color, width) { LineCap = XLineCap.Round, LineJoin = XLineJoin.Round };

        var form = new XForm(Owner, new XSize(box.Width, box.Height));
        using (var gfx = XGraphics.FromForm(form))
        {
            foreach (var stroke in strokes)
                gfx.DrawLines(pen, [..stroke.Select(point => PointArrays.IntoForm(point, box))]);
        }

        SetAppearance(form);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfMarkupAnnotation.Keys
    {
        /// <summary>
        /// (Required) An array of n arrays, each representing a stroked path. Each array shall be
        /// a series of alternating horizontal and vertical coordinates in default user space.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string InkList = "/InkList";

        public new static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
