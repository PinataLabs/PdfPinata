using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// What a <c>/Polygon</c> and a <c>/PolyLine</c> annotation share, which by ISO 32000-1 section
/// 12.5.6.9 is everything except whether the last vertex joins the first: a run of vertices, a
/// width, a colour and an interior colour.
/// </summary>
/// <remarks>
/// <para>
/// Drawn from the appearance stream this class builds, and rebuilt whenever the vertices, the
/// colour, the interior, the width or the opacity change, as <see cref="PdfLineAnnotation"/> is.
/// Asked for nothing - fewer than two vertices, or no width - it removes the appearance it had.
/// </para>
/// <para>
/// The vertices are the geometry and the rectangle follows from them, so <c>/Rect</c> is computed
/// each time they change and assigning <see cref="PdfAnnotation.Rectangle"/> is overwritten. They
/// are in default user space - up from the bottom left of the page - and not the world space
/// <see cref="XGraphics"/> draws in; <c>gfx.Transformer.WorldToDefaultPage</c> converts a point.
/// </para>
/// </remarks>
public abstract class PdfPolyAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyAnnotation"/> class.
    /// </summary>
    /// <param name="subtype">The value of <c>/Subtype</c>: <c>/Polygon</c> or <c>/PolyLine</c>.</param>
    private protected PdfPolyAnnotation(string subtype)
    {
        Initialize(subtype);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="subtype">The value of <c>/Subtype</c>: <c>/Polygon</c> or <c>/PolyLine</c>.</param>
    private protected PdfPolyAnnotation(PdfDocument document, string subtype)
        : base(document)
    {
        Initialize(subtype);
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document.
    /// </summary>
    private protected PdfPolyAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    private void Initialize(string subtype)
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, subtype);

        // /Vertices is required, so it is there from the start even while it holds nothing.
        Elements[Keys.Vertices] = new PdfArray();
        Color = XColors.Black;
        BorderWidth = 1;
    }

    /// <summary>
    /// The vertices, in order, in default user space.
    /// </summary>
    public IReadOnlyList<XPoint> Vertices => PointArrays.Read(Elements.GetArray(Keys.Vertices));

    /// <summary>
    /// Replaces the vertices, in default user space.
    /// </summary>
    public void SetVertices(params XPoint[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        Elements[Keys.Vertices] = PointArrays.Write(vertices);
        Touch();
    }

    /// <summary>
    /// The width of the line, in points - <c>/BS</c>'s <c>/W</c>. Zero draws nothing at all.
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

    /// <summary>
    /// The interior colour - <c>/IC</c>. A polygon is filled with it and a polyline's closed line
    /// endings are; <see cref="XColor.Empty"/>, the default, fills nothing.
    /// </summary>
    public XColor Interior
    {
        get => ColorFrom(Elements.GetArray(Keys.IC), XColor.Empty);
        set
        {
            Elements[Keys.IC] = ColorArray(value);
            Touch();
        }
    }

    /// <summary>
    /// How far beyond the vertices what is drawn reaches, which is how much bigger than the box
    /// enclosing them <c>/Rect</c> has to be.
    /// </summary>
    private protected virtual double Reach(double width) => width / 2;

    /// <summary>
    /// Draws the shape through the vertices, already mapped into the appearance's own space.
    /// </summary>
    private protected abstract void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XPoint[] vertices);

    internal override void OnAddedToPage()
    {
        RebuildAppearance();
    }

    internal override void OnAppearanceInvalidated()
    {
        RebuildAppearance();
    }

    private protected void Touch()
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

        var vertices = Vertices;
        var width = BorderWidth;

        if (vertices.Count < 2 || !PointArrays.TryEnclose(vertices, Reach(width), out var box))
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

        // Round joins, so that a sharp corner does not mitre out past the reach /Rect allowed.
        var pen = new XPen(Color, width) { LineJoin = XLineJoin.Round };
        var interior = Interior;
        XBrush brush = interior == XColor.Empty ? null : new XSolidBrush(interior);

        var form = new XForm(Owner, new XSize(box.Width, box.Height));
        using (var gfx = XGraphics.FromForm(form))
            DrawShape(gfx, pen, brush, [..vertices.Select(point => PointArrays.IntoForm(point, box))]);

        SetAppearance(form);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfMarkupAnnotation.Keys
    {
        /// <summary>
        /// (Required) An array of numbers specifying the alternating horizontal and vertical
        /// coordinates, respectively, of each vertex, in default user space.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string Vertices = "/Vertices";

        /// <summary>
        /// (Optional; PDF 1.4) For /PolyLine only: an array of two names specifying the line
        /// ending styles. Default value: [ /None /None ].
        /// </summary>
        [KeyInfo("1.4", KeyType.Array | KeyType.Optional)]
        public const string LE = "/LE";

        /// <summary>
        /// (Optional; PDF 1.4) An array of numbers in the range 0.0 to 1.0 specifying the
        /// interior colour: of the polygon, or of a polyline's line endings.
        /// </summary>
        [KeyInfo("1.4", KeyType.Array | KeyType.Optional)]
        public const string IC = "/IC";

        /// <summary>
        /// (Optional; PDF 1.5) A border effect dictionary describing an effect applied to the
        /// border described by the BS entry.
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string BE = "/BE";

        public new static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
