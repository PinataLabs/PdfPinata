using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Form XObjects: content drawn once and placed many times, and pages of one PDF drawn onto
///   another.
/// </summary>
internal sealed class ImpositionDemo : PdfDemo
{
    public ImpositionDemo() : base() { }

    public override string Name => "Imposition";

    public override string Summary => "XForm and XPdfForm - stamps, watermarks, two-up and a booklet sheet.";

    public override IReadOnlyList<string> Shows => [
        "XForm - a device drawn once into a form XObject and placed twenty times, at one copy's cost",
        "The same form scaled, rotated and stretched, all from the one definition",
        "XPdfForm - a page of another PDF treated as something drawable",
        "A watermark under the content and one over it, and why the order matters",
        "Two pages imposed on one sheet, landscape, with a fold line",
        "A four-page booklet sheet: pages 4 and 1 on the front, 2 and 3 on the back"
    ];

    public override int PageCount => 5;

    #region example
    protected override PdfDocument Build(DemoContext context)
    {
        var document = new PdfDocument();
        document.Info.Title = "Imposition";

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var body = new XFont("Liberation Sans", 9);
        var note = new XFont("Liberation Sans", 7.5);
        var huge = new XFont("Liberation Sans", 60, XFontStyle.Bold);

        // ----- page 1: a form drawn once and placed many times -----

        var page1 = document.AddPage();
        var gfx1 = XGraphics.FromPdfPage(page1);
        var prose1 = new XTextFormatter(gfx1);

        gfx1.DrawString("A form drawn once", heading, XBrushes.Black, new XPoint(50, 60));

        prose1.DrawString(
            "An XForm is a piece of content stored in the document once and referred to wherever it "
            + "is placed. The rosette below is defined a single time and drawn twenty times at "
            + "different sizes and angles; the file carries one copy of it however many times it "
            + "appears. Draw into it through XGraphics.FromForm, then place it with DrawImage - an "
            + "XForm is an XImage, which is why the drawing call is the one for images.",
            body, XBrushes.Black, new XRect(50, 80, 495, 70));

        // docs:begin create-form
        // The form has to belong to a document from the moment it is created: it is stored in that
        // document's resources, and there would be nowhere else to put it.
        var rosette = new XForm(document, new XSize(60, 60));
        using (var inside = XGraphics.FromForm(rosette))
        {
            // Ordinary drawing, in the form's own coordinates - its view box, not the page's.
            for (var spoke = 0; spoke < 12; spoke++)
            {
                var state = inside.Save();
                inside.TranslateTransform(30, 30);
                inside.RotateTransform(spoke * 30);
                inside.DrawEllipse(new XPen(XColors.MidnightBlue, 0.6),
                    new XSolidBrush(XColor.FromArgb(40, 70, 130, 180)), -6, -26, 12, 26);
                inside.Restore(state);
            }

            inside.DrawEllipse(new XSolidBrush(XColors.Firebrick), 26, 26, 8, 8);
        }

        // DrawingFinished is called for you the first time the form is placed. Calling it by hand
        // is how a form is closed off before then - after it, the form cannot be drawn on again.
        rosette.DrawingFinished();
        // docs:end create-form

        // docs:begin place-form
        for (var index = 0; index < 20; index++)
        {
            var scale = 0.4 + index % 5 * 0.25;
            var state = gfx1.Save();
            // ReSharper disable once PossibleLossOfFraction
            gfx1.TranslateTransform(80 + index % 5 * 110, 220 + index / 5 * 110);
            gfx1.RotateTransform(index * 17);
            gfx1.ScaleTransform(scale, scale);
            gfx1.DrawImage(rosette, -30, -30, 60, 60);
            gfx1.Restore(state);
        }
        // docs:end place-form

        var drawnLongHand = WithoutTheForm();

        prose1.DrawString(
            "Twenty placements, one definition. Drawing the same twenty rosettes straight onto a "
            + $"page instead - the same picture, no form - takes {drawnLongHand:N0} bytes for that "
            + "page alone, because each of the two hundred and forty petals is written into the "
            + "content stream where it is drawn. The same trick is what a page number, a rule, a "
            + "logo or a repeating background should be built from: content put into the file once "
            + "costs once, content drawn onto every page costs every time.",
            body, XBrushes.Black, new XRect(50, 660, 495, 80));

        // ----- a source document to impose -----

        // Four numbered pages, built in memory. Everything below draws these pages onto sheets
        // rather than copying them as pages, which is the difference between imposing and merging.
        byte[] sourceBytes;
        using (var buffer = new MemoryStream())
        {
            var source = new PdfDocument();
            XColor[] colours =
            [
                XColor.FromArgb(70, 130, 180), XColor.FromArgb(178, 34, 34),
                XColor.FromArgb(46, 139, 87), XColor.FromArgb(218, 165, 32)
            ];

            for (var index = 0; index < 4; index++)
            {
                var page = source.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawRectangle(new XSolidBrush(colours[index]),
                    20, 20, page.Width.Point - 40, page.Height.Point - 40);
                gfx.DrawString((index + 1).ToString(), huge, XBrushes.White,
                    new XRect(0, 0, page.Width.Point, page.Height.Point), XStringFormats.Center);
                gfx.DrawString($"Source page {index + 1} of 4", body, XBrushes.White,
                    new XRect(0, page.Height.Point - 60, page.Width.Point, 20),
                    XStringFormats.TopCenter);
            }

            source.Save(buffer, false);
            sourceBytes = buffer.ToArray();
        }

        // docs:begin pdf-form
        // An XPdfForm is a page of an existing PDF, made drawable. PageNumber selects which - it
        // is one-based, where PageIndex beside it is not, and mixing them up is the usual reason
        // the wrong page turns up on the sheet.
        XPdfForm Page(int number)
        {
            var form = XPdfForm.FromStream(new MemoryStream(sourceBytes));
            form.PageNumber = number;
            return form;
        }
        // docs:end pdf-form

        // ----- page 2: watermarks, under and over -----

        var page2 = document.AddPage();
        var gfx2 = XGraphics.FromPdfPage(page2);

        gfx2.DrawString("Watermarks", heading, XBrushes.Black, new XPoint(50, 60));
        new XTextFormatter(gfx2).DrawString(
            "The same mark drawn before the content and after it. Under the content it is a tint "
            + "the page sits on and anything opaque hides it; over the content it is visible "
            + "everywhere and has to be translucent not to bury what it marks. Neither is more "
            + "correct - a draft stamp wants to be over, a background tint wants to be under.",
            body, XBrushes.Black, new XRect(50, 80, 495, 60));

        // docs:begin watermark
        // The two marks differ in nothing but when they are drawn, so the colour is the same for
        // both - black, which reads over either of the source pages' panels.
        void Watermark(XGraphics gfx, XRect where, string text, double alpha)
        {
            var state = gfx.Save();
            gfx.TranslateTransform(where.X + where.Width / 2, where.Y + where.Height / 2);
            gfx.RotateTransform(-35);
            gfx.DrawString(text, new XFont("Liberation Sans", 40, XFontStyle.Bold),
                new XSolidBrush(XColor.FromArgb((int)(alpha * 255), 0, 0, 0)),
                new XPoint(0, 0), XStringFormats.Center);
            gfx.Restore(state);
        }
        // docs:end watermark

        // A4 proportions, so the imposed pages are not stretched. 230 wide gives about 325 tall.
        var under = new XRect(50, 165, 230, 230 * 297 / 210.0);
        var over = new XRect(315, 165, 230, 230 * 297 / 210.0);

        // docs:begin under-over
        // Under: the mark first, the page on top of it. The page's own coloured panel is opaque,
        // so it covers the mark completely - which is the thing to see.
        Watermark(gfx2, under, "DRAFT", 1.0);
        using (var first = Page(1))
            gfx2.DrawImage(first, under);

        // Over: the page first, the mark on top. Translucent, or it would bury the content.
        using (var second = Page(2))
            gfx2.DrawImage(second, over);
        Watermark(gfx2, over, "DRAFT", 0.45);
        // docs:end under-over

        gfx2.DrawString("Drawn under the page - hidden by it", note, XBrushes.DimGray,
            new XRect(under.X, under.Bottom + 6, under.Width, 12), XStringFormats.TopCenter);
        gfx2.DrawString("Drawn over the page, at 45% alpha", note, XBrushes.DimGray,
            new XRect(over.X, over.Bottom + 6, over.Width, 12), XStringFormats.TopCenter);

        // ----- page 3: two up -----

        var page3 = document.AddPage();
        page3.Orientation = PageOrientation.Landscape;
        var gfx3 = XGraphics.FromPdfPage(page3);

        var sheetWidth = page3.Width.Point;
        var sheetHeight = page3.Height.Point;

        gfx3.DrawString("Two up", heading, XBrushes.Black, new XPoint(40, 40));
        gfx3.DrawString(
            "Source pages 1 and 2, each drawn at half width onto one landscape sheet. Nothing was "
            + "copied as a page: the sheet's content stream refers to two form XObjects.",
            note, XBrushes.DimGray, new XPoint(40, 56));

        var slotWidth = (sheetWidth - 60) / 2;
        var slotHeight = sheetHeight - 110;

        // docs:begin two-up
        for (var index = 0; index < 2; index++)
        {
            using var form = Page(index + 1);

            // Fit the page into the slot, keeping its proportions and centring what is left over.
            var scale = Math.Min(slotWidth / form.PointWidth, slotHeight / form.PointHeight);
            var width = form.PointWidth * scale;
            var height = form.PointHeight * scale;
            var x = 20 + index * (slotWidth + 20) + (slotWidth - width) / 2;
            var y = 75 + (slotHeight - height) / 2;

            gfx3.DrawImage(form, x, y, width, height);
        }
        // docs:end two-up

        // The fold, drawn down the middle of the sheet rather than between the two slots, because
        // the fold is where the paper bends and not where the artwork happens to stop.
        gfx3.DrawLine(new XPen(XColors.Gray, 0.5) { DashStyle = XDashStyle.Dash },
            sheetWidth / 2, 70, sheetWidth / 2, sheetHeight - 30);
        gfx3.DrawString("fold", note, XBrushes.Gray,
            new XPoint(sheetWidth / 2 + 4, sheetHeight - 34));

        // ----- pages 4 and 5: a booklet -----

        // Four pages folded once give two sheets printed on both sides. The outer sheet carries
        // the last page and the first; the inner one carries the second and the third. Getting
        // that order right is the whole of booklet imposition, and it is arithmetic rather than
        // an API: for n pages, sheet i holds n-i on the left and i+1 on the right.
        // docs:begin booklet-sheets
        (int Left, int Right, string Caption)[] sheets =
        [
            (4, 1, "Front of the sheet: page 4 on the left, page 1 on the right"),
            (2, 3, "Back of the sheet: page 2 on the left, page 3 on the right")
        ];
        // docs:end booklet-sheets

        foreach (var sheet in sheets)
        {
            var side = document.AddPage();
            side.Orientation = PageOrientation.Landscape;
            using var gfx = XGraphics.FromPdfPage(side);

            var width = side.Width.Point;
            var height = side.Height.Point;

            gfx.DrawString("Booklet", heading, XBrushes.Black, new XPoint(40, 40));
            gfx.DrawString(sheet.Caption, note, XBrushes.DimGray, new XPoint(40, 56));

            var slot = (width - 60) / 2;
            var tall = height - 110;

            // docs:begin booklet-place
            foreach ((int Number, int Position) placement in new[]
            {
                (sheet.Left, 0), (sheet.Right, 1)
            })
            {
                using var form = Page(placement.Number);
                var scale = Math.Min(slot / form.PointWidth, tall / form.PointHeight);
                var w = form.PointWidth * scale;
                var h = form.PointHeight * scale;

                gfx.DrawImage(form,
                    20 + placement.Position * (slot + 20) + (slot - w) / 2,
                    75 + (tall - h) / 2, w, h);
            }
            // docs:end booklet-place

            gfx.DrawLine(new XPen(XColors.Gray, 0.5) { DashStyle = XDashStyle.Dash },
                width / 2, 70, width / 2, height - 30);
            gfx.DrawString("fold", note, XBrushes.Gray, new XPoint(width / 2 + 4, height - 34));
        }

        return document;
    }

    // Measured rather than asserted. The same twenty rosettes, drawn straight onto a page
    // instead of through a form, into a throwaway document that is never saved to disk.
    private static long WithoutTheForm()
    {
        using var plain = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(plain.AddPage());

        for (var index = 0; index < 20; index++)
        {
            var scale = 0.4 + index % 5 * 0.25;
            var state = gfx.Save();
            // ReSharper disable once PossibleLossOfFraction
            gfx.TranslateTransform(80 + index % 5 * 110, 220 + index / 5 * 110);
            gfx.RotateTransform(index * 17);
            gfx.ScaleTransform(scale, scale);

            for (var spoke = 0; spoke < 12; spoke++)
            {
                var turn = gfx.Save();
                gfx.TranslateTransform(0, 0);
                gfx.RotateTransform(spoke * 30);
                gfx.DrawEllipse(new XPen(XColors.MidnightBlue, 0.6),
                    new XSolidBrush(XColor.FromArgb(40, 70, 130, 180)), -6, -26, 12, 26);
                gfx.Restore(turn);
            }

            gfx.DrawEllipse(new XSolidBrush(XColors.Firebrick), -4, -4, 8, 8);
            gfx.Restore(state);
        }

        using var buffer = new MemoryStream();
        plain.Save(buffer, false);
        return buffer.Length;
    }
    #endregion
}
