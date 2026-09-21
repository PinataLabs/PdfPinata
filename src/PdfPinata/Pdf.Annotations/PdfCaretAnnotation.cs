using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// A caret - the mark a proofreader puts where text is to be inserted - drawn on the page as an
/// annotation. ISO 32000-1 section 12.5.6.11.
/// </summary>
/// <remarks>
/// <para>
/// The rectangle is the geometry, as it is for <see cref="PdfSquareAnnotation"/>: the caret is
/// drawn to fill it, apex at the top, and is redrawn whenever the rectangle, the colour or the
/// opacity changes. Readers that draw carets at all draw them from the appearance stream, which is
/// why one is built here.
/// </para>
/// <para>
/// <see cref="Symbol"/> is recorded in <c>/Sy</c> but the drawing is a caret either way: a pilcrow
/// is text, and drawing text would oblige every caller to have a font resolver registered for a
/// mark that is a triangle. A reader regenerating the appearance may draw one.
/// </para>
/// </remarks>
public sealed class PdfCaretAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCaretAnnotation"/> class.
    /// </summary>
    public PdfCaretAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCaretAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfCaretAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfCaretAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Caret");

        // Blue, which is what a proofreader's caret is in every reader that has a tool for one.
        Color = XColors.Blue;
    }

    /// <summary>
    /// What the caret stands for - <c>/Sy</c>.
    /// </summary>
    public PdfCaretSymbol Symbol
    {
        get => Elements.GetName(Keys.Sy) == "/P" ? PdfCaretSymbol.Paragraph : PdfCaretSymbol.None;
        set
        {
            // /None is the default, so it is written by leaving the entry out.
            if (value == PdfCaretSymbol.Paragraph)
                Elements.SetName(Keys.Sy, "/P");
            else
                Elements.Remove(Keys.Sy);

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

    void RebuildAppearance()
    {
        // Until it is on a page there is no document to make a form in. OnAddedToPage calls this
        // again once there is, so nothing set beforehand is lost.
        if (Owner == null)
            return;

        var rect = Elements.GetRectangle(PdfAnnotation.Keys.Rect);
        var width = rect.Width;
        var height = rect.Height;

        // XForm's floor of a point in either direction.
        if (width < 1 || height < 1)
        {
            RemoveAppearance();
            return;
        }

        // A chevron with its apex at the top middle, notched from below a little over halfway up
        // so that it reads as a caret rather than as a filled triangle.
        var caret = new[]
        {
            new XPoint(0, height),
            new XPoint(width / 2, 0),
            new XPoint(width, height),
            new XPoint(width / 2, height * 0.55),
        };

        var form = new XForm(Owner, new XSize(width, height));
        using (var gfx = XGraphics.FromForm(form))
            gfx.DrawPolygon(new XSolidBrush(Color), caret, XFillMode.Winding);

        SetAppearance(form);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfMarkupAnnotation.Keys
    {
        /// <summary>
        /// (Optional; PDF 1.5) A set of four numbers describing the numerical differences between
        /// two rectangles: the Rect entry of the annotation and the actual boundaries of the
        /// underlying caret.
        /// </summary>
        [KeyInfo("1.5", KeyType.Array | KeyType.Optional)]
        public const string RD = "/RD";

        /// <summary>
        /// (Optional) A name specifying a symbol associated with the caret: P (a new paragraph
        /// symbol) or None. Default value: None.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Sy = "/Sy";

        public new static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
