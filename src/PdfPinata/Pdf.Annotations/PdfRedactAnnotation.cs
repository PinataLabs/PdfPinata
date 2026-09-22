using System;
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Marks a region of the page for redaction - ISO 32000-1 section 12.5.6.23.
/// </summary>
/// <remarks>
/// <para>
/// <b>This marks content; it removes nothing.</b> A redaction annotation is a proposal: a reader
/// with redaction tools shows the marked region, and <em>applying</em> it is what deletes the text,
/// images and paths underneath and paints <see cref="Interior"/> and <see cref="OverlayText"/> in
/// their place. This library does not apply redactions, so a document saved with one of these
/// still carries everything under it, readable by anyone who deletes the annotation.
/// </para>
/// <para>
/// The region is either the quadrilaterals added through <see cref="AddQuad(PdfRectangle)"/> or,
/// with none, the annotation's own rectangle - the same rule
/// <see cref="PdfTextMarkupAnnotation"/> follows. The appearance built here is the marked state:
/// an outline of each region in <see cref="PdfAnnotation.Color"/>, redrawn whenever the regions,
/// the rectangle, the colour or the opacity change.
/// </para>
/// </remarks>
public sealed class PdfRedactAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRedactAnnotation"/> class.
    /// </summary>
    public PdfRedactAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRedactAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfRedactAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfRedactAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Redact");

        // Red, the colour every reader with redaction tools marks a pending one in.
        Color = XColors.Red;
    }

    /// <summary>
    /// The quadrilaterals marked, in default user space. Empty when none have been added, in which
    /// case the annotation's rectangle is the region.
    /// </summary>
    public IReadOnlyList<PdfRectangle> Quads => QuadPoints.Read(Elements.GetArray(Keys.QuadPoints));

    /// <summary>
    /// Adds a region to be redacted, in default user space. The rectangle becomes the box
    /// enclosing every region.
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
        Elements.SetRectangle(PdfAnnotation.Keys.Rect, QuadPoints.Enclosing(Quads));
        Touch();
    }

    /// <summary>
    /// Removes every region added, leaving the annotation's rectangle to be the region.
    /// </summary>
    public void ClearQuads()
    {
        Elements.Remove(Keys.QuadPoints);
        Touch();
    }

    /// <summary>
    /// The colour the region is filled with once the redaction is applied - <c>/IC</c>.
    /// <see cref="XColor.Empty"/>, the default, leaves it unfilled.
    /// </summary>
    public XColor Interior
    {
        get => ColorFrom(Elements.GetArray(Keys.IC), XColor.Empty);
        set
        {
            Elements[Keys.IC] = ColorArray(value);
            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Text drawn over the region once the redaction is applied - <c>/OverlayText</c> - or null.
    /// </summary>
    public string OverlayText
    {
        get => Elements.ContainsKey(Keys.OverlayText) ? Elements.GetString(Keys.OverlayText) : null;
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.OverlayText);
            }
            else
            {
                Elements.SetString(Keys.OverlayText, value);

                // Overlay text requires /DA, which says how to set it. A caller who wrote one
                // keeps theirs.
                if (!Elements.ContainsKey(Keys.DA))
                    Elements.SetString(Keys.DA, "/Helv 12 Tf 0 g");
            }

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Whether <see cref="OverlayText"/> is repeated to fill the region - <c>/Repeat</c>.
    /// </summary>
    public bool RepeatOverlayText
    {
        get => Elements.GetBoolean(Keys.Repeat);
        set
        {
            if (value)
                Elements.SetBoolean(Keys.Repeat, true);
            else
                Elements.Remove(Keys.Repeat);

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
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

    void Touch()
    {
        Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        RebuildAppearance();
    }

    void RebuildAppearance()
    {
        // Until it is on a page there is no document to make a form in. OnAddedToPage calls this
        // again once there is, so nothing set beforehand is lost.
        if (Owner == null)
            return;

        var box = Elements.GetRectangle(PdfAnnotation.Keys.Rect);
        if (box.Width < 1 || box.Height < 1)
        {
            RemoveAppearance();
            return;
        }

        var quads = Quads;
        if (quads.Count == 0)
            quads = [box];

        // An outline a point wide, drawn inside each region rather than centred on its edge, so
        // that the part of it on the outermost edges is not clipped by the appearance's bounds.
        const double width = 1;
        var pen = new XPen(Color, width);

        var form = new XForm(Owner, new XSize(box.Width, box.Height));
        using (var gfx = XGraphics.FromForm(form))
        {
            foreach (var quad in quads)
            {
                var topLeft = PointArrays.IntoForm(new XPoint(quad.X1, quad.Y2), box);
                if (quad.Width > width && quad.Height > width)
                {
                    gfx.DrawRectangle(pen, topLeft.X + width / 2, topLeft.Y + width / 2,
                        quad.Width - width, quad.Height - width);
                }
            }
        }

        SetAppearance(form);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfMarkupAnnotation.Keys
    {
        /// <summary>
        /// (Optional) An array of 8 x n numbers specifying the coordinates of n quadrilaterals in
        /// default user space. If not present, the Rect entry denotes the content region.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string QuadPoints = "/QuadPoints";

        /// <summary>
        /// (Optional) An array of three numbers in the range 0.0 to 1.0 specifying the colour used
        /// to fill the region after the affected content has been removed.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string IC = "/IC";

        /// <summary>
        /// (Optional) A form XObject specifying the overlay appearance for this annotation, drawn
        /// after the affected content has been removed.
        /// </summary>
        [KeyInfo(KeyType.Stream | KeyType.Optional)]
        public const string RO = "/RO";

        /// <summary>
        /// (Optional) A text string specifying the overlay text drawn over the region after the
        /// affected content has been removed.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string OverlayText = "/OverlayText";

        /// <summary>
        /// (Optional) If true, the overlay text is repeated to fill the region. Default value:
        /// false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string Repeat = "/Repeat";

        /// <summary>
        /// (Required if OverlayText is present) The appearance string used to format the overlay
        /// text.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Optional)]
        public const string DA = "/DA";

        public new static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
