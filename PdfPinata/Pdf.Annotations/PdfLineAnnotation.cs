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
public sealed class PdfLineAnnotation : PdfAnnotation
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

    void Initialize()
    {
        Elements.SetName(Keys.Subtype, "/Line");

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
        get
        {
            PdfDictionary border = Elements.GetDictionary(Keys.BS);
            return border == null ? 1 : border.Elements.GetReal("/W");
        }
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "A line cannot be narrower than nothing.");

            // A direct dictionary, so that it needs no owner - the width can be set before the
            // annotation has been added to a page.
            PdfDictionary border = new PdfDictionary();
            border.Elements.SetName("/Type", "/Border");
            border.Elements.SetReal("/W", value);
            border.Elements.SetName("/S", "/S");
            Elements[Keys.BS] = border;

            Touch();
        }
    }

    /// <summary>
    /// The colour the line endings are filled with. <see cref="XColor.Empty"/>, which is the
    /// default, leaves them unfilled - an outline rather than a solid arrowhead.
    /// </summary>
    public XColor Interior
    {
        get
        {
            PdfArray colour = Elements.GetArray(Keys.IC);
            if (colour == null || colour.Elements.Count < 3)
                return XColor.Empty;

            // Rounded rather than truncated. A component is written as a fraction of 255 to the
            // seven decimal places PdfWriter gives a real, so 127 goes out as 0.4980392 and comes
            // back as 126.999996 - and truncating that loses a value the file all but said.
            return XColor.FromArgb(
                (int)Math.Round(colour.Elements.GetReal(0) * 255),
                (int)Math.Round(colour.Elements.GetReal(1) * 255),
                (int)Math.Round(colour.Elements.GetReal(2) * 255));
        }
        set
        {
            // An empty array is how the specification says "no interior colour", and is not the
            // same as the entry being absent - which means the same thing, but says nothing
            // about intent.
            PdfArray colour = new PdfArray();
            if (value != XColor.Empty)
            {
                colour.Elements.Add(new PdfReal(value.R / 255.0));
                colour.Elements.Add(new PdfReal(value.G / 255.0));
                colour.Elements.Add(new PdfReal(value.B / 255.0));
            }

            Elements[Keys.IC] = colour;
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

    internal override void OnAddedToPage()
    {
        RebuildAppearance();
    }

    internal override void OnAppearanceInvalidated()
    {
        RebuildAppearance();
    }

    XPoint EndpointAt(int first)
    {
        PdfArray line = Elements.GetArray(Keys.L);
        if (line == null || line.Elements.Count < 4)
            return new XPoint();

        return new XPoint(line.Elements.GetReal(first), line.Elements.GetReal(first + 1));
    }

    void WriteLine(XPoint start, XPoint end)
    {
        Elements[Keys.L] = new PdfArray(Owner,
            new PdfReal(start.X), new PdfReal(start.Y), new PdfReal(end.X), new PdfReal(end.Y));

        Touch();
    }

    PdfLineEnding EndingAt(int index)
    {
        PdfArray endings = Elements.GetArray(Keys.LE);
        if (endings == null || endings.Elements.Count <= index)
            return PdfLineEnding.None;

        string name = endings.Elements.GetName(index);
        if (name.Length > 0 && name[0] == '/')
            name = name.Substring(1);

        return Enum.IsDefined(typeof(PdfLineEnding), name)
            ? Enum.Parse<PdfLineEnding>(name, false)
            : PdfLineEnding.None;
    }

    void WriteEndings(PdfLineEnding start, PdfLineEnding end)
    {
        Elements[Keys.LE] = new PdfArray(Owner,
            new PdfName("/" + start), new PdfName("/" + end));

        Touch();
    }

    /// <summary>
    /// Records a change somebody made and redraws what follows from it.
    /// </summary>
    void Touch()
    {
        Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        RebuildAppearance();
    }

    /// <summary>
    /// The length an arrowhead runs back along the line, and the width of every other ending.
    /// </summary>
    /// <remarks>
    /// Scaled from the line's own width, floored at one point so that a hairline still gets a
    /// visible head rather than one four hundredths of a point across.
    /// </remarks>
    double EndingSize => Math.Max(BorderWidth, 1) * 4;

    void RebuildAppearance()
    {
        // Until it is on a page there is no document to make a form in. OnAddedToPage calls this
        // again once there is, so nothing set beforehand is lost.
        if (Owner == null)
            return;

        XPoint start = Start;
        XPoint end = End;
        double width = BorderWidth;

        bool anyEnding = StartEnding != PdfLineEnding.None || EndEnding != PdfLineEnding.None;
        double reach = width / 2 + (anyEnding ? EndingSize : 0);

        // /Rect has to enclose everything drawn, and what is drawn is the line plus whatever sits
        // at its ends. Written even when nothing will be drawn, because /Rect is required.
        double x1 = Math.Min(start.X, end.X) - reach;
        double y1 = Math.Min(start.Y, end.Y) - reach;
        double x2 = Math.Max(start.X, end.X) + reach;
        double y2 = Math.Max(start.Y, end.Y) + reach;
        Elements.SetRectangle(Keys.Rect, new PdfRectangle(new XPoint(x1, y1), new XPoint(x2, y2)));

        double boxWidth = x2 - x1;
        double boxHeight = y2 - y1;

        // Nothing to draw: no width to draw it with, no line to draw, or a box too small to draw
        // it in. That last is XForm's floor of a point in each direction, which a hairline lying
        // flat can fall under - half its width either side of a horizontal line is all the height
        // the box has. The appearance already there has to go, or the annotation keeps showing
        // what it was last asked for rather than what it is being asked for now - a width set back
        // to nothing would stay on the page.
        #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
        if (width <= 0 || (start.X == end.X && start.Y == end.Y)
            || boxWidth < 1 || boxHeight < 1)
        #pragma warning restore S1244
        {
            Elements.Remove(Keys.AP);

            // /AS names one of a set of appearances, so leaving it behind would point at a state
            // in an /AP that is no longer there. SetAppearance clears it for the same reason.
            Elements.Remove(Keys.AS);
            return;
        }

        // The form is drawn on with the origin at its top left and y running down, as every other
        // XGraphics surface is, while the endpoints above are default user space with y running
        // up. That flip is the whole of the conversion.
        XPoint from = new XPoint(start.X - x1, y2 - start.Y);
        XPoint to = new XPoint(end.X - x1, y2 - end.Y);

        XPen pen = new XPen(Color, width);
        XBrush brush = Interior == XColor.Empty ? null : new XSolidBrush(Interior);

        XForm form = new XForm(Owner, new XSize(boxWidth, boxHeight));
        using (XGraphics gfx = XGraphics.FromForm(form))
        {
            gfx.DrawLine(pen, from, to);

            // Each ending points away from the other end, which is what makes an arrow at the far
            // end of a line point forwards and one at the near end point back.
            DrawEnding(gfx, StartEnding, from, Direction(to, from), pen, brush);
            DrawEnding(gfx, EndEnding, to, Direction(from, to), pen, brush);
        }

        SetAppearance(form);
    }

    /// <summary>
    /// The unit vector from one point towards another, or the x axis when the two coincide.
    /// </summary>
    static XVector Direction(XPoint from, XPoint to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        return length == 0 ? new XVector(1, 0) : new XVector(dx / length, dy / length);
    }

    void DrawEnding(XGraphics gfx, PdfLineEnding ending, XPoint at, XVector outward, XPen pen, XBrush brush)
    {
        if (ending == PdfLineEnding.None)
            return;

        double size = EndingSize;
        double half = size / 2;

        // Reversed arrowheads are the same triangle turned round, which is the only thing the
        // R-prefixed members of Table 176 change.
        if (ending == PdfLineEnding.ROpenArrow || ending == PdfLineEnding.RClosedArrow)
            outward = new XVector(-outward.X, -outward.Y);

        XVector across = new XVector(-outward.Y, outward.X);

        switch (ending)
        {
            case PdfLineEnding.Square:
                DrawShape(gfx, pen, brush, new[]
                {
                    new XPoint(at.X - half, at.Y - half), new XPoint(at.X + half, at.Y - half),
                    new XPoint(at.X + half, at.Y + half), new XPoint(at.X - half, at.Y + half),
                });
                break;

            case PdfLineEnding.Circle:
                XRect circle = new XRect(at.X - half, at.Y - half, size, size);
                if (brush == null)
                    gfx.DrawEllipse(pen, circle);
                else
                    gfx.DrawEllipse(pen, brush, circle);
                break;

            case PdfLineEnding.Diamond:
                DrawShape(gfx, pen, brush, new[]
                {
                    new XPoint(at.X, at.Y - half), new XPoint(at.X + half, at.Y),
                    new XPoint(at.X, at.Y + half), new XPoint(at.X - half, at.Y),
                });
                break;

            case PdfLineEnding.OpenArrow:
            case PdfLineEnding.ROpenArrow:
                // Two segments meeting at the tip, drawn as one polyline so that the join is
                // mitred rather than two strokes crossing at a point.
                gfx.DrawLines(pen, new[] { Barb(at, outward, across, size, half, 1), at, Barb(at, outward, across, size, half, -1) });
                break;

            case PdfLineEnding.ClosedArrow:
            case PdfLineEnding.RClosedArrow:
                DrawShape(gfx, pen, brush, new[]
                {
                    at, Barb(at, outward, across, size, half, 1), Barb(at, outward, across, size, half, -1),
                });
                break;

            case PdfLineEnding.Butt:
                gfx.DrawLine(pen,
                    new XPoint(at.X - across.X * half, at.Y - across.Y * half),
                    new XPoint(at.X + across.X * half, at.Y + across.Y * half));
                break;

            case PdfLineEnding.Slash:
                // "Approximately thirty degrees clockwise from perpendicular", which is what the
                // specification asks for and how precisely it asks for it.
                double cos = Math.Cos(Math.PI / 6);
                double sin = Math.Sin(Math.PI / 6);
                XVector slash = new XVector(
                    across.X * cos - across.Y * sin,
                    across.X * sin + across.Y * cos);
                gfx.DrawLine(pen,
                    new XPoint(at.X - slash.X * half, at.Y - slash.Y * half),
                    new XPoint(at.X + slash.X * half, at.Y + slash.Y * half));
                break;
        }
    }

    /// <summary>
    /// One of the two back corners of an arrowhead whose tip is at <paramref name="at"/>.
    /// </summary>
    static XPoint Barb(XPoint at, XVector outward, XVector across, double size, double half, int side)
    {
        return new XPoint(
            at.X - outward.X * size + across.X * half * side,
            at.Y - outward.Y * size + across.Y * half * side);
    }

    /// <summary>
    /// Fills a closed shape when there is an interior colour and outlines it either way, which is
    /// what an absent <c>/IC</c> means: the ending is drawn, and is not filled in.
    /// </summary>
    static void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XPoint[] points)
    {
        if (brush == null)
            gfx.DrawPolygon(pen, points);
        else
            gfx.DrawPolygon(pen, brush, points, XFillMode.Winding);
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

        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
