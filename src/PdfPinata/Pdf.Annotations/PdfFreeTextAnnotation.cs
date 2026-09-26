using System;
using System.Globalization;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Fonts;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Text displayed on the page itself rather than in a note a reader opens - PDFKit's
/// <c>textAnnotation</c>, and ISO 32000-1 section 12.5.6.6.
/// </summary>
/// <remarks>
/// <para>
/// The one annotation whose <c>/Contents</c> are not a description of what is drawn but the thing
/// drawn, which is why <see cref="Contents"/> is overridden here to redraw. It is also the one
/// whose appearance needs a font, so a <c>/FreeText</c> reaching a page with no
/// <see cref="GlobalFontSettings.FontResolver"/> registered fails the way every other piece of
/// text in this library fails.
/// </para>
/// <para>
/// Like <c>/Square</c> and <c>/Line</c> it is drawn from <c>/AP</c> and from nothing else, so the
/// appearance is built here and rebuilt whenever the text, the rectangle, the font, the colours,
/// the border, the alignment or the opacity changes.
/// </para>
/// <para>
/// <c>/C</c> - the entry <see cref="PdfAnnotation.Color"/> writes - is this subtype's background
/// rather than its ink, and the background is read from the dictionary rather than through that
/// property, because the property answers black for an annotation that has no <c>/C</c> at all and
/// a <c>/FreeText</c> with no background should be transparent rather than a black box. Setting
/// <see cref="PdfAnnotation.Color"/> gives it a background; removing <c>/C</c> takes it away
/// again. The ink is <see cref="TextColor"/>, which is also what the border is stroked with, as
/// ISO 32000-1 has it: a free text annotation's border takes its colour from <c>/DA</c>.
/// </para>
/// </remarks>
public sealed class PdfFreeTextAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFreeTextAnnotation"/> class.
    /// </summary>
    public PdfFreeTextAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFreeTextAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfFreeTextAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfFreeTextAnnotation(PdfDictionary dict)
        : base(dict)
    {
        // The ink and the size live in /DA, which is the one place a file says them. The face
        // cannot be recovered - /DA names a resource of the form's, not a font file - so the
        // default face is drawn at the size the file asked for if the text is ever redrawn.
        ReadDefaultAppearance(Elements.GetString(Keys.DA));
    }

    /// <summary>
    /// Takes the colour and the size back out of a <c>/DA</c> string - the last <c>rg</c>,
    /// <c>g</c> or <c>k</c> operator and the last <c>Tf</c> - leaving the defaults where it says
    /// nothing that can be read.
    /// </summary>
    private void ReadDefaultAppearance(string appearance)
    {
        if (string.IsNullOrEmpty(appearance))
            return;

        var tokens = appearance.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (TryReadFontSize(tokens, index, out var size))
                _readFontSize = size;
            else if (TryReadColor(tokens, index, out var color))
                _textColor = color;
        }
    }

    /// <summary>
    /// Whether the token at <paramref name="index"/> is a <c>Tf</c> whose size operand reads as a
    /// positive number.
    /// </summary>
    private static bool TryReadFontSize(string[] tokens, int index, out double size)
    {
        size = 0;
        if (tokens[index] != "Tf" || !TryOperands(tokens, index, 1, out var operands))
            return false;

        size = operands[0];
        return size > 0;
    }

    /// <summary>
    /// Whether the token at <paramref name="index"/> is a <c>g</c>, <c>rg</c> or <c>k</c> whose
    /// operands all read as numbers, and the colour they make.
    /// </summary>
    private static bool TryReadColor(string[] tokens, int index, out XColor color)
    {
        color = default;
        double[] operands;
        switch (tokens[index])
        {
            case "g" when TryOperands(tokens, index, 1, out operands):
                color = XColor.FromUnitRgb(operands[0], operands[0], operands[0]);
                return true;

            case "rg" when TryOperands(tokens, index, 3, out operands):
                color = XColor.FromUnitRgb(operands[0], operands[1], operands[2]);
                return true;

            case "k" when TryOperands(tokens, index, 4, out operands):
                color = XColor.FromCmyk(operands[0], operands[1], operands[2], operands[3]);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// The <paramref name="count"/> tokens before the operator at <paramref name="index"/>, read
    /// as numbers - or false when there are not that many, or one of them is not a number.
    /// </summary>
    private static bool TryOperands(string[] tokens, int index, int count, out double[] operands)
    {
        operands = null;
        if (index < count)
            return false;

        var values = new double[count];
        for (var operand = 0; operand < count; operand++)
        {
            if (!double.TryParse(tokens[index - count + operand], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out values[operand]))
                return false;
        }

        operands = values;
        return true;
    }

    private void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/FreeText");

        // /DA is required, and is written before anything can have changed so that an annotation
        // nobody configures is still well formed. No /C, so the default background is nothing at
        // all - a caption laid over a picture should not blank out a rectangle of it.
        BorderWidth = 1;
        WriteDefaultAppearance();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The text drawn in the box. Overridden because for this subtype the contents are the
    /// annotation rather than a description of it, so changing them has to redraw.
    /// </remarks>
    public override string Contents
    {
        get => base.Contents;
        set
        {
            base.Contents = value;
            OnAppearanceInvalidated();
        }
    }

    /// <summary>
    /// The face and size the text is drawn in.
    /// </summary>
    /// <remarks>
    /// Resolved lazily rather than in a field initializer, so that constructing the annotation
    /// does not oblige a caller who never draws it to have registered a font resolver.
    /// </remarks>
    public XFont Font
    {
        get => _font ??= new XFont(GlobalFontSettings.FontResolver.DefaultFontName, _readFontSize);
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            WriteDefaultAppearance();
            Touch();
        }
    }
    private XFont _font;

    /// <summary>
    /// The size the default face is drawn at - 10, or what <c>/DA</c> said of an annotation read
    /// from a file.
    /// </summary>
    private double _readFontSize = 10;

    /// <summary>
    /// The colour the text and the border are drawn in. Black by default.
    /// </summary>
    public XColor TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            WriteDefaultAppearance();
            Touch();
        }
    }
    private XColor _textColor = XColors.Black;

    /// <summary>
    /// The width of the box's border, in points. Zero draws no border, leaving the text and the
    /// background alone.
    /// </summary>
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

            Touch();
        }
    }

    /// <summary>
    /// How the text is quadded within the box - <c>/Q</c>, which knows three of the four
    /// alignments an <see cref="XParagraphAlignment"/> can name.
    /// </summary>
    /// <remarks>
    /// <see cref="XParagraphAlignment.Justify"/> has no <c>/Q</c> code, so it is written as
    /// left-justified and drawn justified: the drawing is ours and the entry is a reader's, and
    /// left is what a reader regenerating the appearance would make of it anyway.
    /// </remarks>
    public XParagraphAlignment Alignment
    {
        get
        {
            return Elements.GetInteger(Keys.Q) switch
            {
                1 => XParagraphAlignment.Center,
                2 => XParagraphAlignment.Right,
                _ => _justified ? XParagraphAlignment.Justify : XParagraphAlignment.Left
            };
        }
        set
        {
            _justified = value == XParagraphAlignment.Justify;

            var quadding =
                value == XParagraphAlignment.Center ? 1 :
                value == XParagraphAlignment.Right ? 2 : 0;

            Elements.SetInteger(Keys.Q, quadding);
            Touch();
        }
    }
    private bool _justified;

    /// <summary>
    /// Writes <c>/DA</c>, which ISO 32000-1 requires of this subtype and which a reader uses when
    /// it regenerates the appearance for itself.
    /// </summary>
    /// <remarks>
    /// The font is named <c>/Helv</c> rather than after the face actually used, because a name in
    /// <c>/DA</c> is looked up in the interactive form's <c>/DR</c> and a document that has no
    /// form has no such dictionary to look in. The appearance built below carries the real face in
    /// its own resources, so what a reader draws from <c>/AP</c> is the face asked for either way.
    /// </remarks>
    private void WriteDefaultAppearance()
    {
        // Size read off the font rather than stored, so that /DA and the drawing cannot disagree.
        var size = _font?.Size ?? _readFontSize;

        var appearance = string.Format(CultureInfo.InvariantCulture,
            "/Helv {0:0.###} Tf {1:0.###} {2:0.###} {3:0.###} rg",
            size, _textColor.R / 255.0, _textColor.G / 255.0, _textColor.B / 255.0);

        Elements.SetString(Keys.DA, appearance);
    }

    private protected override void RebuildAppearance()
    {
        var rect = Elements.GetRectangle(PdfAnnotation.Keys.Rect);
        var width = rect.X2 - rect.X1;
        var height = rect.Y2 - rect.Y1;

        var border = BorderWidth;
        var text = Contents ?? "";

        // Read from the dictionary rather than through Color, which answers black for an
        // annotation carrying no /C - so a box nobody gave a background to would get a black one.
        var hasBackground = Elements.GetArray(PdfAnnotation.Keys.C)?.Elements.Count == 3;

        // Nothing to draw: no room to draw it in, or nothing asked for. The appearance already
        // there has to go, or the annotation keeps showing what it was last asked for rather than
        // what it is being asked for now - text cleared away would stay on the page.
        // Measured against 1 rather than against 0 because that is XForm's floor: a rectangle
        // under a point in either direction is one no appearance can be made of.
        var tooSmallForAnAppearance = width < 1 || height < 1;
        var nothingAskedFor = text.Length == 0 && border <= 0 && !hasBackground;
        if (tooSmallForAnAppearance || nothingAskedFor)
        {
            // /AS goes with /AP, as SetAppearance clears it for the same reason, and /RD describes
            // the layout of an appearance that is no longer there.
            RemoveAppearance();
            Elements.Remove(Keys.RD);
            return;
        }

        // The border is drawn inside /Rect rather than centred on its edge, so that a wide one is
        // not clipped in half by the annotation's own bounds - and the text is inset from the
        // border by its own width again, so that it does not touch it.
        var inset = border + Math.Max(border, 2);

        var form = new XForm(Owner, new XSize(width, height));
        using (var gfx = XGraphics.FromForm(form))
        {
            if (hasBackground)
                gfx.DrawRectangle(new XSolidBrush(Color), new XRect(0, 0, width, height));

            DrawBorder(gfx, width, height, border);
            DrawText(gfx, text, inset, width - 2 * inset, height - 2 * inset);
        }

        SetAppearance(form);

        // What /Rect gives up before the text starts, as the specification asks for it: the
        // difference at the left, top, right and bottom between /Rect and the box laid out in.
        Elements[Keys.RD] = new PdfArray(Owner,
            new PdfReal(inset), new PdfReal(inset), new PdfReal(inset), new PdfReal(inset));
    }

    /// <summary>
    /// Strokes the border inside the box, when there is one and the box is wider and taller than it.
    /// </summary>
    private void DrawBorder(XGraphics gfx, double width, double height, double border)
    {
        var borderFits = border > 0 && width > border && height > border;
        if (!borderFits)
            return;

        gfx.DrawRectangle(new XPen(_textColor, border),
            new XRect(border / 2, border / 2, width - border, height - border));
    }

    /// <summary>
    /// Lays the text out in the box left inside the inset, when there is text and room for it.
    /// </summary>
    private void DrawText(XGraphics gfx, string text, double inset, double textWidth, double textHeight)
    {
        var textFits = text.Length > 0 && textWidth > 0 && textHeight > 0;
        if (!textFits)
            return;

        var formatter = new XTextFormatter(gfx)
        {
            Alignment = Alignment
        };

        formatter.DrawString(text, Font, new XSolidBrush(_textColor),
            new XRect(inset, inset, textWidth, textHeight));
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        // /BS, the border style dictionary this draws its width from, is inherited: every
        // annotation may carry one.

        /// <summary>
        /// (Required) The default appearance string to be used in formatting the text.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string DA = "/DA";

        /// <summary>
        /// (Optional; PDF 1.4) A code specifying the form of quadding (justification) to be used
        /// in displaying the annotation's text: 0 left-justified, 1 centred, 2 right-justified.
        /// Default value: 0.
        /// </summary>
        [KeyInfo("1.4", KeyType.Integer | KeyType.Optional)]
        public const string Q = "/Q";

        /// <summary>
        /// (Optional; PDF 1.6) An array of four numbers that shall describe the numerical
        /// differences between the Rect entry of the annotation and the rectangle the text is
        /// laid out in.
        /// </summary>
        [KeyInfo("1.6", KeyType.Array | KeyType.Optional)]
        public const string RD = "/RD";

        /// <summary>
        /// (Optional; PDF 1.6) An array of four or six numbers specifying a callout line
        /// attached to the free text annotation.
        /// </summary>
        [KeyInfo("1.6", KeyType.Array | KeyType.Optional)]
        public const string CL = "/CL";

        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
