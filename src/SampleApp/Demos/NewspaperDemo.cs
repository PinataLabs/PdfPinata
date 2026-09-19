using System;
using System.Collections.Generic;
using PdfPinata;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A broadsheet front page, set in columns.
/// </summary>
/// <remarks>
///   Hand drawn rather than flowed through PinataLayout, and not by preference: PinataLayout's PageSetup
///   has no columns at all, so the only multi-column engine in the library is
///   <see cref="XTextFormatter.Columns"/>. A newspaper laid out through PinataLayout would be text
///   frames positioned by arithmetic, which is more work and less honest.
/// </remarks>
internal sealed class NewspaperDemo : PdfDemo
{
    public NewspaperDemo() : base() { }

    public override string Name => "Newspaper";

    public override string Summary => "A broadsheet front page in five columns.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Five justified columns with rules drawn down the gutters",
        "Text run round a photograph by flowing it twice, above and below",
        "A masthead letterspaced with CharacterSpacing",
        "A sidebar clipped with IntersectClip inside Save and Restore",
    };

    public override int PageCount => 1;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Serif = "Liberation Serif";
        const string Sans = "Liberation Sans";

        const string Copy =
            "Readers of this page will notice that the text is set in five columns of equal "
            + "width, justified, with a rule down each gutter. None of that is automatic. The "
            + "formatter is given one rectangle and told how many columns to divide it into, "
            + "and it fills each in turn before moving on to the next. Where the columns meet "
            + "is arithmetic the caller has to repeat if it wants to draw anything there. "
            + "The photograph below is not flowed round either. Nothing in the library wraps "
            + "text about an object, so the story is drawn twice - once into the space above "
            + "the picture and once into the space below it - and the break between them is "
            + "chosen by measuring rather than by counting words. It is more work than a "
            + "single call, and it is the honest amount of work for what is being asked. ";

        PdfDocument document = new PdfDocument();
        document.Info.Title = "The Daily Broadsheet";

        PdfPage page = document.AddPage();
        page.Size = PageSize.A3;

        XGraphics gfx = XGraphics.FromPdfPage(page);
        XTextFormatter formatter = new XTextFormatter(gfx);

        double width = page.Width.Point;
        const double margin = 40;
        double measure = width - margin * 2;

        // ---- Masthead ------------------------------------------------------------------
        // Letterspacing a masthead is what CharacterSpacing is for. At display sizes the
        // default fit is too tight, and the gap has to be opened by hand.
        gfx.DrawString("THE DAILY BROADSHEET", new XFont(Serif, 46, XFontStyle.Bold),
            XBrushes.Black, new XRect(margin, 46, measure, 56),
            new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                CharacterSpacing = 2.5,
            });

        gfx.DrawLine(new XPen(XColors.Black, 2.4), margin, 106, width - margin, 106);
        gfx.DrawLine(new XPen(XColors.Black, 0.6), margin, 111, width - margin, 111);

        XFont folio = new XFont(Sans, 8);
        gfx.DrawString("Wednesday 12 August 2026", folio, XBrushes.Black,
            new XRect(margin, 118, measure, 12), XStringFormats.TopLeft);
        gfx.DrawString("No. 41,208", folio, XBrushes.Black,
            new XRect(margin, 118, measure, 12), XStringFormats.TopCenter);
        gfx.DrawString("Two pounds", folio, XBrushes.Black,
            new XRect(margin, 118, measure, 12), XStringFormats.TopRight);

        gfx.DrawLine(new XPen(XColors.Black, 0.6), margin, 134, width - margin, 134);

        // ---- Headline ------------------------------------------------------------------
        formatter.Alignment = XParagraphAlignment.Center;
        formatter.DrawString("Library gains columns, wraps nothing round anything",
            new XFont(Serif, 32, XFontStyle.Bold), XBrushes.Black,
            new XRect(margin, 152, measure, 80));

        formatter.DrawString(
            "Five columns, a rule in every gutter, and a photograph the story declines to "
            + "flow around",
            new XFont(Serif, 13, XFontStyle.Italic), XBrushes.Black,
            new XRect(margin, 224, measure, 40));

        formatter.Alignment = XParagraphAlignment.Left;

        gfx.DrawLine(new XPen(XColors.Black, 0.6), margin, 268, width - margin, 268);

        gfx.DrawString("By a Staff Reporter", new XFont(Sans, 9, XFontStyle.Bold),
            XBrushes.Black, new XPoint(margin, 288));

        // ---- The body, in columns ------------------------------------------------------
        // docs:begin columns
        const int columnCount = 5;
        const double columnGap = 14;
        double columnWidth = (measure - columnGap * (columnCount - 1)) / columnCount;

        XFont body = new XFont(Serif, 9.5);
        formatter.Columns = columnCount;
        formatter.ColumnGap = columnGap;
        formatter.Alignment = XParagraphAlignment.Justify;
        // docs:end columns

        // The story is flowed twice: once above the photograph, once below it. There is no
        // wrap-around-object anywhere in the library, so the space beside a picture has to
        // be given to the formatter as a rectangle that does not include the picture.
        const double upperTop = 300;
        const double upperHeight = 250;
        double pictureTop = upperTop + upperHeight + 16;

        // docs:begin columns-draw
        // Enough copy to fill five columns twice over. A story that runs out halfway leaves
        // empty columns, which says nothing about how the formatter fills them.
        string story = string.Concat(Copy, Copy, Copy, Copy, Copy, Copy, Copy, Copy);

        formatter.DrawString(story, body, XBrushes.Black,
            new XRect(margin, upperTop, measure, upperHeight));
        // docs:end columns-draw

        // ---- Photograph, spanning the middle columns -----------------------------------
        using XImage photograph = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));

        // The picture takes columns two to four of its band, leaving the first column of
        // that band for the side story. Both sit between the two blocks of body text
        // rather than over them, which is what keeps them from being drawn on top of.
        double pictureLeft = margin + (columnWidth + columnGap);
        double pictureWidth = columnWidth * 3 + columnGap * 2;
        double pictureHeight = pictureWidth * photograph.PointHeight / photograph.PointWidth;

        gfx.DrawImage(photograph, pictureLeft, pictureTop, pictureWidth, pictureHeight);

        XFont caption = new XFont(Sans, 7.5, XFontStyle.Italic);
        formatter.Columns = 1;
        formatter.Alignment = XParagraphAlignment.Left;
        formatter.DrawString(
            "Two readers consider the gutter rules, which are drawn rather than provided.",
            caption, XBrushes.DimGray,
            new XRect(pictureLeft, pictureTop + pictureHeight + 4, pictureWidth, 24));

        // ---- The rest of the story, below the photograph -------------------------------
        double lowerTop = pictureTop + pictureHeight + 30;

        // Floored at zero. Everything above is computed from the photograph's own aspect
        // ratio, so a taller picture pushes this down the page, and a rectangle with a
        // negative height throws rather than drawing nothing. Swapping the image should
        // give a worse looking page, not an exception.
        double lowerHeight = Math.Max(0, page.Height.Point - lowerTop - margin - 24);

        formatter.Columns = columnCount;
        formatter.Alignment = XParagraphAlignment.Justify;
        formatter.DrawString(story, body, XBrushes.Black,
            new XRect(margin, lowerTop, measure, lowerHeight));

        formatter.Columns = 1;
        formatter.Alignment = XParagraphAlignment.Left;

        // ---- Gutter rules ---------------------------------------------------------------
        // docs:begin gutter-rules
        // The formatter draws no rules, so the gutter centres are worked out with the same
        // arithmetic it used to place the columns. Getting this wrong is how a rule ends up
        // through the middle of a column rather than between two.
        XPen gutter = new XPen(XColors.LightGray, 0.5);
        for (int index = 1; index < columnCount; index++)
        {
            double x = margin + index * (columnWidth + columnGap) - columnGap / 2;
            gfx.DrawLine(gutter, x, upperTop, x, upperTop + upperHeight);
            gfx.DrawLine(gutter, x, lowerTop, x, lowerTop + lowerHeight);
        }
        // docs:end gutter-rules

        // ---- A boxed side story, clipped ------------------------------------------------
        // docs:begin clip-sidebar
        // IntersectClip has no counterpart to undo it - there is no ResetClip - so the only
        // way back is to restore a state saved before it was narrowed.
        XRect sidebar = new XRect(margin, pictureTop, columnWidth, pictureHeight);

        XGraphicsState state = gfx.Save();
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 250, 235)), sidebar);
        gfx.DrawRectangle(new XPen(XColors.Black, 0.8), sidebar);
        gfx.IntersectClip(sidebar);

        XTextFormatter boxed = new XTextFormatter(gfx);
        boxed.DrawString("ALSO INSIDE", new XFont(Sans, 8, XFontStyle.Bold), XBrushes.Black,
            new XRect(sidebar.X + 8, sidebar.Y + 8, sidebar.Width - 16, 14));
        boxed.DrawString(
            "This box is clipped to its own rectangle, so the long paragraph inside it is "
            + "cut off at the edge rather than running over the column beside it. The clip "
            + "is undone by restoring the graphics state, because there is nothing else "
            + "that will undo it.",
            new XFont(Serif, 8.5), XBrushes.Black,
            new XRect(sidebar.X + 8, sidebar.Y + 26, sidebar.Width - 16, sidebar.Height));

        gfx.Restore(state);
        // docs:end clip-sidebar

        // ---- Foot ------------------------------------------------------------------------
        gfx.DrawLine(new XPen(XColors.Black, 0.6), margin, page.Height.Point - margin - 14,
            width - margin, page.Height.Point - margin - 14);
        gfx.DrawString("The Daily Broadsheet · Wednesday 12 August 2026 · Page 1", folio,
            XBrushes.Black,
            new XRect(margin, page.Height.Point - margin - 10, measure, 12),
            XStringFormats.TopCenter);
        #endregion

        return document;
    }
}
