using System.Collections.Generic;
using PdfPinata;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A document whose pages are different sizes and both orientations, each captioned with the
///   dimensions it actually has.
/// </summary>
internal sealed class OrientationDemo : PdfDemo
{
    public OrientationDemo() : base() { }

    public override string Name => "Orientation";

    public override string Summary => "Page sizes, portrait and landscape, and /Rotate.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Six page sizes from the ISO and North American sets",
        "PageOrientation.Landscape swapping the sides",
        "XUnit converting one size into points, millimetres and inches",
        "page.Rotate turning what a reader shows without moving the drawing",
    };

    public override int PageCount => 6;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();

        XFont title = new XFont("Liberation Sans", 22, XFontStyle.Bold);
        XFont label = new XFont("Liberation Sans", 10);
        XFont small = new XFont("Liberation Sans", 8);

        (PageSize Size, PageOrientation Orientation, string Note)[] pages =
        {
            (PageSize.A4, PageOrientation.Portrait, "the ISO default"),
            (PageSize.A4, PageOrientation.Landscape, "the same page, turned"),
            (PageSize.A3, PageOrientation.Portrait, "twice A4"),
            (PageSize.A6, PageOrientation.Portrait, "an eighth of A3"),
            (PageSize.Letter, PageOrientation.Portrait, "North American"),
            (PageSize.Legal, PageOrientation.Landscape, "North American, turned"),
        };

        foreach ((PageSize size, PageOrientation orientation, string note) in pages)
        {
            // docs:begin page-size
            PdfPage page = document.AddPage();

            // Size and Orientation are set before anything is drawn. The Size setter throws
            // on a page that already has content, because writing a new media box would
            // crop what is there rather than scale it - see the PageResize demo for what
            // to do when the page has already been drawn on.
            page.Size = size;
            page.Orientation = orientation;

            XGraphics gfx = XGraphics.FromPdfPage(page);
            double width = page.Width.Point;
            double height = page.Height.Point;
            // docs:end page-size

            // A frame, corner ticks and a diagonal, so the shape of the page and which way
            // up it is can be read at a glance.
            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 1), 24, 24, width - 48, height - 48);
            gfx.DrawLine(new XPen(XColors.WhiteSmoke, 1), 24, 24, width - 24, height - 24);
            foreach (XPoint corner in new[]
                     {
                         new XPoint(24, 24), new XPoint(width - 24, 24),
                         new XPoint(24, height - 24), new XPoint(width - 24, height - 24),
                     })
            {
                gfx.DrawRectangle(XBrushes.SteelBlue, corner.X - 3, corner.Y - 3, 6, 6);
            }

            gfx.DrawString($"{size} {orientation}", title, XBrushes.Black,
                new XPoint(56, 90));
            gfx.DrawString(note, label, XBrushes.DimGray, new XPoint(56, 112));

            // XUnit is what the page's Width and Height are, and it converts rather than
            // being converted: the same measurement read three ways.
            // docs:begin units
            XUnit pageWidth = page.Width;
            XUnit pageHeight = page.Height;

            string[] lines =
            {
                $"{pageWidth.Point:0.#} x {pageHeight.Point:0.#} points",
                $"{pageWidth.Millimeter:0.#} x {pageHeight.Millimeter:0.#} mm",
                $"{pageWidth.Inch:0.00} x {pageHeight.Inch:0.00} inches",
                $"PageSizeConverter.ToSize({size}) = {PageSizeConverter.ToSize(size)}",
            };
            // docs:end units

            double y = 148;
            foreach (string line in lines)
            {
                gfx.DrawString(line, label, XBrushes.Black, new XPoint(56, y));
                y += 16;
            }

            // Rotate asks the reader to turn the page when it displays it. The drawing is
            // untouched - the words below are laid down the same way as every other page
            // here, and it is the viewer that turns them.
            // docs:begin rotate
            if (size == PageSize.A6)
            {
                page.Rotate = 90;

                // Broken over two lines by hand. A6 is 298pt wide and this is the one page
                // here where the measure is tight enough that a single line of it would run
                // to the frame - DrawString does not wrap, so nothing would break it.
                gfx.DrawString("page.Rotate = 90: the reader turns this,", small,
                    XBrushes.Crimson, new XPoint(56, y + 8));
                gfx.DrawString("the drawing did not.", small,
                    XBrushes.Crimson, new XPoint(56, y + 20));
            }
            // docs:end rotate
        }
        #endregion

        return document;
    }
}
