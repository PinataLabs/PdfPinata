using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A feature opener and its continuation: a full bleed photograph, a drop cap, and a pull quote.
/// </summary>
/// <remarks>
///   <para>
///     The drop cap is a property now. It used to be thirty lines that drew the letter separately
///     and then added one word at a time to a probe rectangle until the answer stopped fitting - a
///     workaround this demo was teaching as though it were a technique.
///   </para>
///   <para>
///     The pull quote is a property now too. It used to be two text blocks with a gap arithmetic'd
///     between them, and this demo's own notes said it was going to stay that way - that the
///     formatter had no notion of a shape to flow around, and that PinataLayout's side wrap was the
///     nearest thing there would ever be. That turned out to be a fact about the formatter's API
///     rather than about the formatter: it already flowed text beside a drop cap, and what was
///     missing was a way for a caller to say where something else was.
///   </para>
///   <para>
///     The two engines are still separate and still deliberately so. PinataLayout wraps a
///     <i>shape in a document tree</i>; this wraps a rectangle a caller drew and knows the position
///     of. Same idea, different level, and neither is the other's implementation.
///   </para>
/// </remarks>
internal sealed class MagazineDemo : PdfDemo
{
    public MagazineDemo() : base() { }

    public override string Name => "Magazine";

    public override string Summary => "A feature opener with a drop cap and a pull quote.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "A photograph bled off three edges, clipped to its band",
        "A scrim that fades out as well as down, from a gradient with alpha in its colours",
        "A drop cap from XTextFormatter.DropCap - one property, three lines deep, set by its ink",
        "A title turned into a path by AddString and filled with a gradient",
        "A pull quote slanted with ObliqueAngle, straddling the gutter",
        "Copy flowing down both sides of it from one DrawString, via XTextFormatter.Obstacles",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Serif = "Liberation Serif";
        const string Sans = "Liberation Sans";

        // The text begins with the letter the cap is made from. The formatter takes the first
        // character for the cap and lays out the rest, so the word reads whole on the page and
        // the caller does not split it by hand.
        const string Opening =
            "There is a drop cap in this library now, and it is the property set below rather "
            + "than the thirty lines this demo used to carry. The pull quote on the next page used "
            + "to be two text blocks with a gap measured out between them; it is one block and one "
            + "obstacle now, and the copy finds its own way down both sides of it. The SideWrap "
            + "demo does the same thing a level up, for a shape in a PinataLayout document. ";

        const string Body =
            "The letter beside these lines is not drawn separately. DropCap says how many lines "
            + "deep it sits; the formatter scales it so its foot rests on the baseline of the "
            + "third one, reserves the room, and shortens the lines that stand against it. It is "
            + "placed by the outline of the glyph rather than by its advance, so the ink sits "
            + "flush with the margin instead of a side bearing's width inside it. ";

        PdfDocument document = new PdfDocument();
        document.Info.Title = "Feature";

        // ---- Page one: the opener --------------------------------------------------------
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);
        XTextFormatter formatter = new XTextFormatter(gfx);

        double width = page.Width.Point;
        double height = page.Height.Point;
        const double margin = 46;

        void RunningFoot(XGraphics target, string folio) =>
            target.DrawString(folio, new XFont(Sans, 8), XBrushes.Gray,
                new XRect(margin, height - margin, width - margin * 2, 12),
                XStringFormats.TopCenter);

        using XImage photograph = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));

        // A full bleed is a rectangle that starts at the page edge and finishes past it.
        // The image is scaled to cover, exactly as in the Images demo - but covering means
        // one dimension overflows, and an overflow off the bottom lands on the article
        // rather than off the page. So it is clipped to the band it belongs in, and the
        // clip is undone by restoring the state, there being no ResetClip.
        double bleedHeight = height * 0.42;
        double cover = System.Math.Max(
            width / photograph.PointWidth, bleedHeight / photograph.PointHeight);

        XGraphicsState bleed = gfx.Save();
        gfx.IntersectClip(new XRect(0, 0, width, bleedHeight));
        gfx.DrawImage(photograph, 0, 0,
            photograph.PointWidth * cover, photograph.PointHeight * cover);

        // A scrim, so white type has something to sit on whatever the photograph happens to
        // be doing underneath it. One gradient, from nothing at the top to nearly opaque at
        // the foot of the band: a gradient honours the alpha of its colours, so this fades out
        // as well as down and the picture shows through the top of it.
        XRect scrim = new XRect(0, bleedHeight * 0.45, width, bleedHeight * 0.55);
        gfx.DrawRectangle(
            new XLinearGradientBrush(scrim,
                XColor.FromArgb(0, 12, 14, 10), XColor.FromArgb(190, 12, 14, 10),
                XLinearGradientMode.Vertical),
            scrim);

        gfx.Restore(bleed);

        gfx.DrawString("FEATURE", new XFont(Sans, 9, XFontStyle.Bold), XBrushes.White,
            new XRect(margin, bleedHeight - 128, width - margin * 2, 14),
            new XStringFormat { CharacterSpacing = 3 });

        formatter.DrawString("The arithmetic behind\na page that looks designed",
            new XFont(Serif, 30, XFontStyle.Bold), XBrushes.White,
            new XRect(margin, bleedHeight - 108, width - margin * 2, 84));

        gfx.DrawString("Photographs by the test suite · Words by nobody",
            new XFont(Sans, 8), new XSolidBrush(XColor.FromArgb(230, 255, 255, 255)),
            new XPoint(margin, bleedHeight - 20));

        // ---- The drop cap ----------------------------------------------------------------
        XFont body = new XFont(Serif, 10);
        double textTop = bleedHeight + 30;
        double measure = width - margin * 2;

        // The cap is one property. The formatter takes the first character of the text,
        // scales it so that its foot rests on the baseline of the third line, reserves the
        // room beside it and shortens the three lines that sit against it. The size is not
        // given here: a depth in lines is what the surrounding text is measured in, and a
        // size would imply a depth that is almost never a whole number of them.
        formatter.DropCap = new XDropCap(new XFont(Serif, 10, XFontStyle.Bold), lines: 3);
        formatter.Alignment = XParagraphAlignment.Justify;
        formatter.Columns = 2;
        formatter.ColumnGap = 18;

        // One call for the whole feature: the cap, the lines that clear it, and the two
        // columns the rest of it flows down.
        formatter.DrawString(string.Concat(Opening, Body, Body, Body, Body, Body), body,
            XBrushes.Black, new XRect(margin, textTop, measure, height - textTop - margin - 20));

        formatter.DropCap = null;
        formatter.Columns = 1;
        formatter.Alignment = XParagraphAlignment.Left;

        RunningFoot(gfx, "1");

        // ---- Page two: the continuation and the pull quote --------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);
        formatter = new XTextFormatter(gfx);

        gfx.DrawString("THE ARITHMETIC BEHIND A PAGE", new XFont(Sans, 8, XFontStyle.Bold),
            new XSolidBrush(XColor.FromArgb(140, 140, 140)),
            new XRect(margin, margin, measure, 12),
            new XStringFormat { CharacterSpacing = 2 });
        gfx.DrawLine(new XPen(XColors.Gainsboro, 0.6), margin, margin + 18, width - margin,
            margin + 18);

        // A title as geometry rather than as text. AddString puts the glyph outlines into a
        // path, which can then be filled with anything a shape can be filled with - here a
        // gradient across the word, which no DrawString overload can produce.
        //
        // It needs a glyph outline provider registered, which the runner does along with the
        // other backends. To stroke a title and nothing more you would not come here at all:
        // DrawString takes a pen as well as a brush, which is cheaper and stays searchable.
        XRect titleBox = new XRect(margin, margin + 30, measure, 44);
        XGraphicsPath title = new XGraphicsPath();
        title.AddString("Continued", new XFontFamily(Serif), XFontStyle.Bold, 34, titleBox,
            XStringFormats.TopLeft);

        gfx.DrawPath(
            new XLinearGradientBrush(titleBox, XColors.DarkSlateGray, XColors.CadetBlue,
                XLinearGradientMode.Horizontal),
            title);

        // ---- The pull quote, and the copy that flows around it -----------------------------
        // One text block for the whole page and one obstacle standing in it. The quote is
        // narrower than the measure and centred, so it straddles the gutter and takes the
        // right of the first column and the left of the second - which leaves a usable run
        // down each outside edge, and that is where the copy goes.
        //
        // Nothing here measures text. The two blocks with a gap arithmetic'd between them
        // that this used to be needed the quote's height, the gap either side of it and the
        // line height all kept in step by hand, and got them wrong whenever the font changed.
        double textTopOfPage = margin + 86;
        double textHeight = height - textTopOfPage - margin - 20;

        // Positioned relative to the layout rectangle, which is what an obstacle is measured
        // in - so the page coordinates the quote is drawn at are these plus the block's corner.
        XRect quoteInBlock = new XRect(140, 150, measure - 280, 108);
        XRect quote = new XRect(margin + quoteInBlock.X, textTopOfPage + quoteInBlock.Y,
            quoteInBlock.Width, quoteInBlock.Height);

        // Set on a tint, at a slight slant. ObliqueAngle skews the glyphs where a real
        // italic would redraw them, which is the honest tool for display type that has no
        // italic of its own to reach for.
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 248, 232)), quote);
        gfx.DrawLine(new XPen(XColors.DarkSlateGray, 2), quote.X, quote.Y,
            quote.X, quote.Y + quote.Height);

        formatter.Alignment = XParagraphAlignment.Justify;
        formatter.Columns = 2;
        formatter.ColumnGap = 18;

        // The padding is the obstacle's own, because how much air a thing wants around it is a
        // fact about that thing rather than about the text.
        formatter.Obstacles.Add(new RectangleObstacle(quoteInBlock, padding: 14));

        // Enough copy to fill both columns, because a column left empty would show nothing about
        // an obstacle standing in it.
        formatter.DrawString(
            string.Concat(Enumerable.Repeat(Body, 17)), body,
            XBrushes.Black, new XRect(margin, textTopOfPage, measure, textHeight));

        formatter.Obstacles.Clear();
        formatter.Columns = 1;
        formatter.Alignment = XParagraphAlignment.Left;

        // Drawn a line at a time with XGraphics rather than flowed, because the slant lives
        // on XStringFormat and XTextFormatter's own DrawString does not take one.
        XStringFormat slanted = new XStringFormat
        {
            ObliqueAngle = 8,
            LineAlignment = XLineAlignment.Near,
        };

        // Set to the width of the quote rather than of the page: it straddles the gutter and is
        // narrower than either, so the lines are broken to suit it.
        XFont quoteFont = new XFont(Serif, 14);
        string[] quoteLines =
        {
            "“Nothing decides",
            "that break except a",
            "loop that adds one",
            "word at a time.”",
        };

        for (int line = 0; line < quoteLines.Length; line++)
        {
            gfx.DrawString(quoteLines[line], quoteFont, XBrushes.DarkSlateGray,
                new XRect(quote.X + 18, quote.Y + 16 + line * 21, quote.Width - 32, 21),
                slanted);
        }

        gfx.DrawString("Set with ObliqueAngle, on a tint", new XFont(Sans, 7),
            XBrushes.Gray, new XPoint(quote.X + 20, quote.Y + quote.Height + 14));

        RunningFoot(gfx, "2");
        #endregion

        return document;
    }
}
