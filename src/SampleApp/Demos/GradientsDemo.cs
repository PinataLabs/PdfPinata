using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Linear and radial gradient brushes: where a gradient starts and stops, what is painted past
///   its ends, and how a transform of its own moves and stretches it.
/// </summary>
internal sealed class GradientsDemo : PdfDemo
{
    public GradientsDemo() : base() { }

    public override string Name => "Gradients";

    public override string Summary => "Radial and linear gradients, extended past their ends and transformed.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "XRadialGradientBrush with one centre, and with two for an off-centre highlight",
        "ExtendRight carrying the outer colour to the corners, ExtendLeft filling the inner circle",
        "The same ExtendLeft and ExtendRight on an XLinearGradientBrush",
        "A brush's own Transform: a radial gradient stretched to an ellipse, a linear one turned",
        "A radial gradient that fades to transparent over the drawing beneath it"
    };

    public override int PageCount => 1;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var document = new PdfDocument();
        document.Info.Title = "Gradients";

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var label = new XFont("Liberation Sans", 8);

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString("Gradients", heading, XBrushes.Black, new XPoint(50, 60));

        // Each panel is a titled box in a three-by-four grid, and the lambda draws inside it.
        void Panel(int column, int row, string title, Action<XRect> draw)
        {
            var cell = new XRect(50 + column * 165, 90 + row * 170, 155, 160);
            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 0.5), cell);
            draw(new XRect(cell.X + 8, cell.Y + 8, cell.Width - 16, cell.Height - 30));
            gfx.DrawString(title, label, XBrushes.Black,
                new XRect(cell.X, cell.Bottom - 20, cell.Width, 14), XStringFormats.Center);
        }

        XPoint Middle(XRect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

        // ----- radial gradients -----

        // docs:begin radial
        Panel(0, 0, "One centre", r =>
        {
            // The first colour at the first radius, the second at the second. With a first radius
            // of zero the first colour is a point at the centre.
            var brush = new XRadialGradientBrush(Middle(r), 0, 55, XColors.Gold, XColors.Firebrick);
            gfx.DrawEllipse(brush, Middle(r).X - 55, Middle(r).Y - 55, 110, 110);
        });
        // docs:end radial

        // docs:begin extend-right
        Panel(1, 0, "ExtendRight", r =>
        {
            // Nothing is painted beyond the outer circle unless ExtendRight says so. Here the
            // rectangle is larger than the circle, and its corners take the outer colour.
            var brush = new XRadialGradientBrush(Middle(r), 0, 50, XColors.White, XColors.SteelBlue)
            {
                ExtendRight = true
            };
            gfx.DrawRectangle(brush, r);
        });
        // docs:end extend-right

        Panel(2, 0, "No extend", r =>
        {
            // The same brush without ExtendRight: the corners are left as they were.
            var brush = new XRadialGradientBrush(Middle(r), 0, 50, XColors.White, XColors.SteelBlue);
            gfx.DrawRectangle(brush, r);
        });

        // docs:begin two-centres
        Panel(0, 1, "Two centres", r =>
        {
            // The first circle is a point up and to the left of the second circle's centre, so the
            // highlight sits off-centre, as on a lit sphere.
            var centre = Middle(r);
            var highlight = new XPoint(centre.X - 18, centre.Y - 18);
            var brush = new XRadialGradientBrush(highlight, centre, 0, 55, XColors.White, XColors.DarkGreen);
            gfx.DrawEllipse(brush, centre.X - 55, centre.Y - 55, 110, 110);
        });
        // docs:end two-centres

        // docs:begin extend-left
        Panel(1, 1, "ExtendLeft", r =>
        {
            // A first radius above zero leaves a hole inside the first circle. ExtendLeft fills
            // it with the first colour.
            var brush = new XRadialGradientBrush(Middle(r), 25, 55, XColors.Orange, XColors.Indigo)
            {
                ExtendLeft = true
            };
            gfx.DrawEllipse(brush, Middle(r).X - 55, Middle(r).Y - 55, 110, 110);
        });
        // docs:end extend-left

        Panel(2, 1, "A ring, no ExtendLeft", r =>
        {
            var brush = new XRadialGradientBrush(Middle(r), 25, 55, XColors.Orange, XColors.Indigo);
            gfx.DrawEllipse(brush, Middle(r).X - 55, Middle(r).Y - 55, 110, 110);
        });

        // ----- linear gradients -----

        // docs:begin linear-extend
        Panel(0, 2, "Linear, extended", r =>
        {
            // The blend runs across the middle third only. Extended at both ends, the rest of the
            // rectangle takes the colour of the nearer end.
            var start = new XPoint(r.X + r.Width / 3, r.Y);
            var end = new XPoint(r.X + 2 * r.Width / 3, r.Y);
            var brush = new XLinearGradientBrush(start, end, XColors.Teal, XColors.Coral)
            {
                ExtendLeft = true,
                ExtendRight = true
            };
            gfx.DrawRectangle(brush, r);
        });
        // docs:end linear-extend

        Panel(1, 2, "Linear, not extended", r =>
        {
            var start = new XPoint(r.X + r.Width / 3, r.Y);
            var end = new XPoint(r.X + 2 * r.Width / 3, r.Y);
            gfx.DrawRectangle(new XLinearGradientBrush(start, end, XColors.Teal, XColors.Coral), r);
        });

        // docs:begin linear-transform
        Panel(2, 2, "Linear, turned by Transform", r =>
        {
            // Left to right across the panel, then turned by 30 degrees about the panel's middle.
            // Turned, the axis no longer reaches two of the corners, so both ends are extended.
            var brush = new XLinearGradientBrush(new XPoint(r.X, r.Y), new XPoint(r.Right, r.Y),
                XColors.Navy, XColors.LightSkyBlue)
            {
                ExtendLeft = true,
                ExtendRight = true
            };
            brush.RotateTransform(30);
            brush.TranslateTransform(-Middle(r).X, -Middle(r).Y, XMatrixOrder.Prepend);
            brush.TranslateTransform(Middle(r).X, Middle(r).Y, XMatrixOrder.Append);
            gfx.DrawRectangle(brush, r);
        });
        // docs:end linear-transform

        // ----- transforms and transparency -----

        // docs:begin radial-transform
        Panel(0, 3, "Radial, stretched by Transform", r =>
        {
            // A circle about the origin, made twice as wide as it is tall, then moved to the
            // middle of the panel. The gradient's rings become ellipses.
            var brush = new XRadialGradientBrush(new XPoint(0, 0), 0, 30, XColors.Yellow, XColors.Purple)
            {
                ExtendRight = true,
                Transform = new XMatrix(2, 0, 0, 1, Middle(r).X, Middle(r).Y)
            };
            gfx.DrawRectangle(brush, r);
        });
        // docs:end radial-transform

        // docs:begin radial-fade
        Panel(1, 3, "Fading to transparent", r =>
        {
            for (var x = r.X; x < r.Right; x += 12)
                gfx.DrawRectangle(XBrushes.LightGray, x, r.Y, 6, r.Height);

            // A transparent second colour lets the stripes show through towards the rim.
            var brush = new XRadialGradientBrush(Middle(r), 0, 55,
                XColors.Crimson, XColor.FromArgb(0, XColors.Crimson));
            gfx.DrawRectangle(brush, r);
        });
        // docs:end radial-fade

        Panel(2, 3, "Under a graphics transform", r =>
        {
            // The graphics' own transform applies to the brush as it does to the shape.
            var state = gfx.Save();
            gfx.TranslateTransform(Middle(r).X, Middle(r).Y);
            gfx.RotateTransform(-20);
            gfx.ScaleTransform(1.6, 0.8);

            var brush = new XRadialGradientBrush(new XPoint(0, 0), 0, 40, XColors.LightYellow, XColors.DarkOrange);
            gfx.DrawEllipse(brush, -40, -40, 80, 80);
            gfx.Restore(state);
        });
        #endregion

        return document;
    }
}
