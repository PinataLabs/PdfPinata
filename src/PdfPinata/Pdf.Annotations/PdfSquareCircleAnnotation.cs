using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// What a <c>/Square</c> and a <c>/Circle</c> annotation share, which by ISO 32000-1 section
/// 12.5.6.8 is everything except the shape drawn: both are a rectangle, an optional interior
/// colour, and a border.
/// </summary>
/// <remarks>
/// <para>
/// A reader draws a <c>/Square</c> from its appearance stream and from nothing else. An annotation
/// of this subtype carrying only a <c>/Rect</c>, a colour and a border width is well formed, and
/// every reader paints nothing for it - which is why this class builds the appearance itself rather
/// than leaving the caller to, and rebuilds it whenever something it is drawn from changes.
/// </para>
/// <para>
/// The drawing is the library's own: an <see cref="XForm"/> filled in through
/// <see cref="XGraphics"/>, handed over with <see cref="PdfAnnotation.SetAppearance(XForm)"/>.
/// Nothing here writes content-stream operators by hand.
/// </para>
/// <para>
/// Being an annotation rather than a rectangle drawn onto the page, it can be hidden, printed or
/// not printed, moved, given a tooltip through <see cref="PdfAnnotation.Contents"/>, and edited by
/// a reader - none of which <c>XGraphics.DrawRectangle</c> can offer, because that is ink.
/// </para>
/// </remarks>
public abstract class PdfSquareCircleAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareCircleAnnotation"/> class.
    /// </summary>
    /// <param name="subtype">The value of <c>/Subtype</c>: <c>/Square</c> or <c>/Circle</c>.</param>
    protected PdfSquareCircleAnnotation(string subtype)
    {
        Initialize(subtype);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareCircleAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="subtype">The value of <c>/Subtype</c>: <c>/Square</c> or <c>/Circle</c>.</param>
    protected PdfSquareCircleAnnotation(PdfDocument document, string subtype)
        : base(document)
    {
        Initialize(subtype);
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has - its
    /// appearance above all - and writing none of the defaults a new one is given.
    /// </summary>
    private protected PdfSquareCircleAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Draws the shape into the appearance stream, inside the box the border has left of the
    /// annotation's rectangle.
    /// </summary>
    /// <param name="gfx">The surface the appearance is drawn on.</param>
    /// <param name="pen">The border, or null when none was asked for.</param>
    /// <param name="brush">The interior, or null when the shape is unfilled.</param>
    /// <param name="box">Where to draw, in the appearance stream's own coordinates.</param>
    protected abstract void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XRect box);

    // Takes the subtype rather than reading it from an abstract member, so that nothing virtual
    // is called while the derived constructor has yet to run.
    private void Initialize(string subtype)
    {
        // Validated even though the two subclasses here pass constants: the constructors are
        // protected on a public class, so a subtype can come from outside this assembly.
        Elements.SetName(PdfAnnotation.Keys.Subtype, SubtypeName(subtype));

        // A visible default, so that an annotation given nothing but a rectangle still appears.
        // Nothing else about this class has a sensible zero: a border of no width and no interior
        // is an annotation that draws nothing, which is the very thing it exists to avoid.
        //
        // Set through the properties rather than the fields, so that the entries they write are
        // in the dictionary from the start. Rebuilding is a no-op until there is an owner.
        Color = XColors.Black;
        BorderWidth = 1;
        Interior = XColor.Empty;
    }

    /// <summary>
    /// The colour the rectangle is filled with. <see cref="XColor.Empty"/>, which is the default,
    /// leaves it unfilled - the <c>/IC</c> entry of an empty array.
    /// </summary>
    /// <remarks>
    /// Read back out of the dictionary rather than kept in a field, so that an annotation read
    /// from a file reports what the file says rather than what a new one would have.
    /// </remarks>
    public XColor Interior
    {
        get => ColorFrom(Elements.GetArray(Keys.IC), XColor.Empty);
        set
        {
            WriteInteriorColor(value);
            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
            OnAppearanceInvalidated();
        }
    }

    /// <summary>
    /// The width of the border, in points. Zero draws no border, leaving the interior alone.
    /// </summary>
    /// <remarks>
    /// The border is drawn inside <see cref="PdfAnnotation.Rectangle"/> rather than centred on its
    /// edge, so that a wide one is not clipped in half by the annotation's own bounds. The amount
    /// given up to it is recorded in <c>/RD</c>, which is what that entry is for.
    /// </remarks>
    public double BorderWidth
    {
        get => BorderWidthFrom(Elements.GetDictionary(PdfAnnotation.Keys.BS));
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "A border cannot be narrower than nothing.");

            // A direct dictionary, so that it needs no owner - the width can be set before the
            // annotation has been added to a page.
            var border = new PdfDictionary();
            border.Elements.SetName("/Type", "/Border");
            border.Elements.SetReal("/W", value);
            border.Elements.SetName("/S", "/S");
            Elements[PdfAnnotation.Keys.BS] = border;

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
            OnAppearanceInvalidated();
        }
    }
    private void WriteInteriorColor(XColor interior)
    {
        // An empty array is how the specification says "no interior colour", and is not the same
        // as the entry being absent - which means the same thing, but says nothing about intent.
        Elements[Keys.IC] = ColorArray(interior);
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
    /// Draws the shape into a form and gives it to this annotation as its appearance.
    /// </summary>
    /// <remarks>
    /// Does not stamp <c>/M</c>. The properties above do, as the ones on
    /// <see cref="PdfAnnotation"/> do, because a modification date records a change somebody made
    /// rather than the redrawing that follows from it - and this runs from every one of them.
    /// </remarks>
    private void RebuildAppearance()
    {
        // Until it is on a page there is no document to make a form in. OnAddedToPage calls this
        // again once there is, so nothing set beforehand is lost.
        if (Owner == null)
            return;

        var rect = Elements.GetRectangle(PdfAnnotation.Keys.Rect);
        var width = rect.X2 - rect.X1;
        var height = rect.Y2 - rect.Y1;

        var borderWidth = BorderWidth;
        var interior = Interior;

        var inset = borderWidth / 2;
        var drawnWidth = width - borderWidth;
        var drawnHeight = height - borderWidth;

        var pen = borderWidth > 0 ? new XPen(Color, borderWidth) : null;
        XBrush brush = interior == XColor.Empty ? null : new XSolidBrush(interior);

        // Nothing to draw: no rectangle yet, none left once the border has taken its half from
        // each side, or neither a border nor a fill asked for. The appearance already there has
        // to go, or the annotation keeps showing what it was last asked for rather than what it
        // is being asked for now - a border set back to nothing would stay on the page.
        //
        // Measured before any XRect is made of it, because XRect refuses a negative width. The
        // outer pair are measured against 1 rather than against 0 because that is XForm's floor:
        // a rectangle under a point in either direction is one no appearance can be made of.
        if (width < 1 || height < 1 || drawnWidth <= 0 || drawnHeight <= 0
            || (pen == null && brush == null))
        {
            Elements.Remove(PdfAnnotation.Keys.AP);
            Elements.Remove(Keys.RD);

            // /AS names one of a set of appearances, so leaving it behind would point at a state
            // in an /AP that is no longer there. SetAppearance clears it for the same reason.
            Elements.Remove(PdfAnnotation.Keys.AS);
            return;
        }

        var drawn = new XRect(inset, inset, drawnWidth, drawnHeight);
        var form = new XForm(Owner, new XSize(width, height));
        using (var gfx = XGraphics.FromForm(form))
        {
            DrawShape(gfx, pen, brush, drawn);
        }

        SetAppearance(form);

        // What /Rect gives up to the border, as the specification asks for it: the difference at
        // the left, top, right and bottom between /Rect and the square actually drawn.
        Elements[Keys.RD] = new PdfArray(Owner,
            new PdfReal(inset), new PdfReal(inset), new PdfReal(inset), new PdfReal(inset));
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        // /BS, the border style dictionary this draws its width from, is inherited: every
        // annotation may carry one.

        /// <summary>
        /// (Optional; PDF 1.4) An array of numbers in the range 0.0 to 1.0 specifying the
        /// interior colour with which to fill the annotation's rectangle. An empty array
        /// specifies no colour, which leaves the rectangle unfilled.
        /// </summary>
        [KeyInfo("1.4", KeyType.Array | KeyType.Optional)]
        public const string IC = "/IC";

        /// <summary>
        /// (Optional; PDF 1.5) A border effect dictionary describing an effect applied to the
        /// border described by the BS entry.
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string BE = "/BE";

        /// <summary>
        /// (Optional; PDF 1.5) An array of four numbers that shall describe the numerical
        /// differences between two rectangles: the Rect entry of the annotation and the actual
        /// boundaries of the underlying square or circle.
        /// </summary>
        [KeyInfo("1.5", KeyType.Array | KeyType.Optional)]
        public const string RD = "/RD";

        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
