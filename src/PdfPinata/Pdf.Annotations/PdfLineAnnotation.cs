using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// A straight line drawn on the page as an annotation rather than as page content - PDFKit's
/// <c>lineAnnotation</c>, and ISO 32000-1 section 12.5.6.7.
/// </summary>
/// <remarks>
/// <para>
/// A <c>/Line</c> is drawn from its appearance stream and from nothing else, so this class builds
/// one and rebuilds it whenever something it is drawn from changes - the endpoints, the colour,
/// the width, the interior, the line endings or the opacity.
/// </para>
/// <para>
/// The rectangle is not the caller's to set. <c>/Rect</c> has to enclose the line and everything
/// drawn at its ends, and only this class knows how much the arrowheads take, so it is computed
/// from <see cref="Start"/> and <see cref="End"/> every time either moves. Assigning
/// <see cref="PdfAnnotation.Rectangle"/> is therefore overwritten rather than honoured, which is
/// the opposite of <see cref="PdfSquareCircleAnnotation"/>, where the rectangle is the geometry.
/// </para>
/// <para>
/// Both endpoints are in default user space - the space <c>/Rect</c> and <c>/L</c> are written in,
/// measured up from the bottom left of the page - and not the top-left world space
/// <see cref="XGraphics"/> draws in. <c>gfx.Transformer.WorldToDefaultPage</c> converts.
/// </para>
/// </remarks>
public sealed class PdfLineAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLineAnnotation"/> class.
    /// </summary>
    public PdfLineAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLineAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfLineAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfLineAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    private void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Line");

        // A visible default, for the same reason PdfSquareCircleAnnotation has one: a line of no
        // width is an annotation that draws nothing, which is the very thing this class exists to
        // avoid. /L is written from the start because it is required, even while it is degenerate.
        WriteLine(new XPoint(), new XPoint());
        Color = XColors.Black;
        BorderWidth = 1;
    }

    /// <summary>
    /// Where the line starts, in default user space.
    /// </summary>
    public XPoint Start
    {
        get => EndpointAt(0);
        set => WriteLine(value, End);
    }

    /// <summary>
    /// Where the line ends, in default user space.
    /// </summary>
    public XPoint End
    {
        get => EndpointAt(2);
        set => WriteLine(Start, value);
    }

    /// <summary>
    /// Moves both ends at once, which is a single rebuild where setting each in turn is two.
    /// </summary>
    /// <param name="start">Where the line starts, in default user space.</param>
    /// <param name="end">Where the line ends, in default user space.</param>
    public void SetLine(XPoint start, XPoint end)
    {
        WriteLine(start, end);
    }

    /// <summary>
    /// The width of the line, in points. Zero draws nothing at all.
    /// </summary>
    /// <remarks>
    /// It is <c>/BS</c>, the border style dictionary, because that is where ISO 32000-1 puts the
    /// width of a line annotation - the same entry <see cref="PdfSquareCircleAnnotation"/> draws
    /// its border from. The line endings are sized from it too, so a wider line gets a bigger
    /// arrowhead.
    /// </remarks>
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
    /// The colour the line endings are filled with. <see cref="XColor.Empty"/>, which is the
    /// default, leaves them unfilled - an outline rather than a solid arrowhead.
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
    /// What is drawn at <see cref="Start"/>.
    /// </summary>
    public PdfLineEnding StartEnding
    {
        get => EndingAt(0);
        set => WriteEndings(value, EndEnding);
    }

    /// <summary>
    /// What is drawn at <see cref="End"/>.
    /// </summary>
    public PdfLineEnding EndEnding
    {
        get => EndingAt(1);
        set => WriteEndings(StartEnding, value);
    }

    private XPoint EndpointAt(int first)
    {
        var line = Elements.GetArray(Keys.L);
        if (line == null || line.Elements.Count < 4)
            return new XPoint();

        return new XPoint(line.Elements.GetReal(first), line.Elements.GetReal(first + 1));
    }

    private void WriteLine(XPoint start, XPoint end)
    {
        Elements[Keys.L] = new PdfArray(Owner,
            new PdfReal(start.X), new PdfReal(start.Y), new PdfReal(end.X), new PdfReal(end.Y));

        Touch();
    }

    private PdfLineEnding EndingAt(int index) => LineEndings.Read(Elements.GetArray(Keys.LE), index);

    private void WriteEndings(PdfLineEnding start, PdfLineEnding end)
    {
        Elements[Keys.LE] = LineEndings.Write(Owner, start, end);

        Touch();
    }

    private protected override void RebuildAppearance()
    {
        var start = Start;
        var end = End;
        var width = BorderWidth;

        var anyEnding = StartEnding != PdfLineEnding.None || EndEnding != PdfLineEnding.None;
        var reach = width / 2 + (anyEnding ? LineEndings.Size(width) : 0);

        // /Rect has to enclose everything drawn, and what is drawn is the line plus whatever sits
        // at its ends. Written even when nothing will be drawn, because /Rect is required.
        var x1 = Math.Min(start.X, end.X) - reach;
        var y1 = Math.Min(start.Y, end.Y) - reach;
        var x2 = Math.Max(start.X, end.X) + reach;
        var y2 = Math.Max(start.Y, end.Y) + reach;
        Elements.SetRectangle(PdfAnnotation.Keys.Rect, new PdfRectangle(new XPoint(x1, y1), new XPoint(x2, y2)));

        var boxWidth = x2 - x1;
        var boxHeight = y2 - y1;

        // Nothing to draw: no width to draw it with, no line to draw, or a box too small to draw
        // it in. That last is XForm's floor of a point in each direction, which a hairline lying
        // flat can fall under - half its width either side of a horizontal line is all the height
        // the box has. The appearance already there has to go, or the annotation keeps showing
        // what it was last asked for rather than what it is being asked for now - a width set back
        // to nothing would stay on the page.
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (width <= 0 || (start.X == end.X && start.Y == end.Y)
            || boxWidth < 1 || boxHeight < 1)
        // ReSharper restore CompareOfFloatsByEqualityOperator
        #pragma warning restore S1244
        {
            RemoveAppearance();
            return;
        }

        // The form is drawn on with the origin at its top left and y running down, as every other
        // XGraphics surface is, while the endpoints above are default user space with y running
        // up. That flip is the whole of the conversion.
        var from = new XPoint(start.X - x1, y2 - start.Y);
        var to = new XPoint(end.X - x1, y2 - end.Y);

        var pen = new XPen(Color, width);
        XBrush brush = Interior == XColor.Empty ? null : new XSolidBrush(Interior);

        var form = new XForm(Owner, new XSize(boxWidth, boxHeight));
        using (var gfx = XGraphics.FromForm(form))
        {
            gfx.DrawLine(pen, from, to);

            // Each ending points away from the other end, which is what makes an arrow at the far
            // end of a line point forwards and one at the near end point back.
            var size = LineEndings.Size(width);
            LineEndings.Draw(gfx, StartEnding, from, LineEndings.Direction(to, from), pen, brush, size);
            LineEndings.Draw(gfx, EndEnding, to, LineEndings.Direction(from, to), pen, brush, size);
        }

        SetAppearance(form);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        // /BS, the border style dictionary this draws its width from, is inherited: every
        // annotation may carry one.

        /// <summary>
        /// (Required) An array of four numbers giving the coordinates of the starting and ending
        /// points of the line, in default user space.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string L = "/L";

        /// <summary>
        /// (Optional; PDF 1.4) An array of two names specifying the line ending styles to be used
        /// in drawing the line. The first names the style at the starting point, the second the
        /// style at the ending point. Default value: [ /None /None ].
        /// </summary>
        [KeyInfo("1.4", KeyType.Array | KeyType.Optional)]
        public const string LE = "/LE";

        /// <summary>
        /// (Optional; PDF 1.4) An array of numbers in the range 0.0 to 1.0 specifying the
        /// interior colour with which to fill the annotation's line endings. An empty array
        /// specifies no colour, which leaves them unfilled.
        /// </summary>
        [KeyInfo("1.4", KeyType.Array | KeyType.Optional)]
        public const string IC = "/IC";

        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
