using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   One photograph, placed every way there is to place it.
/// </summary>
internal sealed class ImagesDemo : PdfDemo
{
    public ImagesDemo() : base() { }

    public override string Name => "Images";

    public override string Summary => "Sizing, stretching, fitting, cropping and rotating one image.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Natural size, and what PointWidth means next to PixelWidth",
        "Fit and fill worked out by hand - the library has no helper",
        "Cropping through the source-rectangle overload of DrawImage",
        "Rotation about a point, inside Save and Restore",
        "A PNG with an alpha channel, over a chequer and over a colour",
        "XImage.Interpolate - whether an upscaled image is smoothed or blocky",
        "Palette-with-alpha beside truecolour-with-alpha, which are two different PNGs",
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();

        XFont label = new XFont("Liberation Sans", 8);
        XFont heading = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        XPen boxPen = new XPen(XColors.Crimson, 0.5) { DashStyle = XDashStyle.Dot };

        // The image is embedded in this assembly rather than read from disk, so it is found
        // wherever the app runs. FromStream takes a factory rather than a stream: the
        // library opens it when it needs it and may do so more than once.
        using XImage photograph = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));

        // ---- Page one: sizing ---------------------------------------------------------
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        void Caption(string text, double x, double y) =>
            gfx.DrawString(text, label, XBrushes.DimGray, new XPoint(x, y));

        void Heading(string text, double y)
        {
            gfx.DrawString(text.ToUpperInvariant(), heading, XBrushes.SteelBlue, new XPoint(48, y));
            gfx.DrawLine(XPens.LightGray, 48, y + 5, 548, y + 5);
        }

        Heading("Natural size, and scaled", 56);

        // PointWidth is the pixel count converted at 96 dpi, which is what the image is
        // worth on the page if nothing scales it. The pixels themselves are unchanged
        // whatever rectangle it is drawn into - drawing it smaller does not resample it.
        Caption($"{photograph.PixelWidth} x {photograph.PixelHeight} pixels, "
              + $"{photograph.PointWidth:0.#} x {photograph.PointHeight:0.#} points at 96 dpi",
            48, 78);

        double naturalWidth = photograph.PointWidth;
        double naturalHeight = photograph.PointHeight;

        // Natural size gets a row to itself: at 96 dpi this photograph is most of the width
        // of an A4 page, which is the point worth making about drawing one unscaled.
        double y = 92;
        gfx.DrawImage(photograph, 48, y, naturalWidth, naturalHeight);
        Caption("natural size, from PointWidth and PointHeight", 48, y + naturalHeight + 12);

        // Everything below is placed from the sizes above rather than from numbers typed in,
        // so changing the photograph cannot silently make the page overlap itself.
        y += naturalHeight + 34;
        gfx.DrawImage(photograph, 48, y, naturalWidth / 2, naturalHeight / 2);
        Caption("half", 48, y + naturalHeight / 2 + 12);

        double quarterX = 48 + naturalWidth / 2 + 24;
        gfx.DrawImage(photograph, quarterX, y, naturalWidth / 4, naturalHeight / 4);
        Caption("quarter", quarterX, y + naturalHeight / 4 + 12);

        y += naturalHeight / 2 + 40;
        Heading("Stretched out of proportion", y);
        y += 20;
        gfx.DrawImage(photograph, 48, y, 300, 84);
        Caption("a rectangle the image does not share the shape of", 48, y + 96);

        // ---- Page two: fitting, cropping and turning -----------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);

        Heading("Fit and fill", 56);
        Caption("There is no fit or cover helper. The arithmetic below is the whole of it.",
            48, 78);

        XRect box = new XRect(48, 92, 200, 200);

        // Fit, or "contain": the largest scale at which the whole image is inside the box,
        // so the box shows through on two sides. Min of the two ratios.
        double fit = Math.Min(box.Width / naturalWidth, box.Height / naturalHeight);
        XRect fitted = new XRect(
            box.X + (box.Width - naturalWidth * fit) / 2,
            box.Y + (box.Height - naturalHeight * fit) / 2,
            naturalWidth * fit,
            naturalHeight * fit);

        gfx.DrawImage(photograph, fitted);
        gfx.DrawRectangle(boxPen, box);
        Caption("fit: Math.Min, the whole image, letterboxed", 48, 306);

        // Fill, or "cover": the smallest scale at which the image covers the box, so the
        // overflow has to be cut off. Max of the two ratios, and then the part of the
        // image to keep is given as a source rectangle in the image's own pixels.
        XRect coverBox = new XRect(300, 92, 200, 200);
        double cover = Math.Max(coverBox.Width / naturalWidth, coverBox.Height / naturalHeight);
        double sourceWidth = coverBox.Width / cover * photograph.PixelWidth / naturalWidth;
        double sourceHeight = coverBox.Height / cover * photograph.PixelHeight / naturalHeight;

        gfx.DrawImage(photograph, coverBox,
            new XRect(
                (photograph.PixelWidth - sourceWidth) / 2,
                (photograph.PixelHeight - sourceHeight) / 2,
                sourceWidth,
                sourceHeight),
            XGraphicsUnit.Point);

        gfx.DrawRectangle(boxPen, coverBox);
        Caption("fill: Math.Max, centre kept, edges cropped away", 300, 306);

        Heading("Turned", 340);

        // Every transform is undone by restoring the state that was saved before it. There
        // is no ResetTransform, so a Save that is not Restored leaks into everything drawn
        // afterwards.
        double[] angles = { 0, 15, 30, 45 };
        double x = 110;
        foreach (double angle in angles)
        {
            XGraphicsState state = gfx.Save();
            gfx.RotateAtTransform(angle, new XPoint(x, 430));
            gfx.DrawImage(photograph, x - 45, 430 - 30, 90, 60);
            gfx.Restore(state);

            Caption($"{angle:0}°", x - 6, 500);
            x += 130;
        }

        Caption("RotateAtTransform turns the page about a point, then the image is drawn "
              + "square onto it.", 48, 520);

        // ---- Page three: transparency and interpolation --------------------------------
        page = document.AddPage();
        gfx = XGraphics.FromPdfPage(page);

        Heading("A PNG with an alpha channel", 60);

        // Two PNGs, and they are not the same kind of file. The badge is a palette image with an
        // alpha channel; the disc is truecolour with one. Both arrive through the same seam and
        // the same call, which is the point - the backend decodes whatever the format is.
        using XImage badge = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "alpha-badge.png"));
        using XImage disc = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "soft-disc.png"));

        // A chequer, so that transparency reads as transparency rather than as a colour. Over a
        // white page a transparent pixel and a white one look identical.
        for (int cx = 0; cx < 10; cx++)
        {
            for (int cy = 0; cy < 8; cy++)
            {
                gfx.DrawRectangle((cx + cy) % 2 == 0 ? XBrushes.WhiteSmoke : XBrushes.Gainsboro,
                    48 + cx * 12, 80 + cy * 12, 12, 12);
            }
        }

        gfx.DrawImage(badge, 48, 80, 120, 120);
        Caption("Over a chequer", 48, 216);

        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(46, 139, 87)), 200, 80, 120, 120);
        gfx.DrawImage(badge, 200, 80, 120, 120);
        Caption("Over a solid colour", 200, 216);

        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(218, 165, 32)), 352, 80, 120, 120);
        gfx.DrawImage(disc, 352, 80, 120, 120);
        Caption("A truecolour PNG, fading out", 352, 216);

        Caption("The badge is a palette PNG with an alpha channel; the disc is truecolour with "
              + "one. Neither needed anything of the caller: XImage.FromStream took both.",
            48, 232);

        Heading("Interpolate", 270);

        // Whether a reader smooths an image scaled up beyond its own resolution. It is a request
        // written into the image dictionary rather than something the library does, so what the
        // two panels below look like depends on the reader - and some ignore it entirely.
        using XImage blocky = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));
        blocky.Interpolate = false;

        using XImage smooth = XImage.FromStream(
            () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg"));
        smooth.Interpolate = true;

        // A small piece of the photograph, blown up far past its own pixels, which is the only
        // arrangement in which the setting is visible at all.
        gfx.DrawImage(blocky, new XRect(48, 290, 230, 170), new XRect(60, 40, 40, 30),
            XGraphicsUnit.Point);
        Caption("Interpolate = false", 48, 474);

        gfx.DrawImage(smooth, new XRect(300, 290, 230, 170), new XRect(60, 40, 40, 30),
            XGraphicsUnit.Point);
        Caption("Interpolate = true", 300, 474);

        Caption("Forty by thirty points of the photograph, drawn at two hundred and thirty wide. "
              + "Interpolate writes /Interpolate true into the image dictionary and asks the "
              + "reader to smooth it; the reader decides, and several ignore the request.",
            48, 492);

        Heading("When an image will not load", 530);

        Caption("XImage.FromStream throws, and the exception says what went wrong - this fork "
              + "swallows nothing. PinataLayout is the one place that does not throw: a failed image "
              + "becomes a grey box, and DocumentRenderer.ImageFailed is the event that says why. "
              + "Without a handler the reason is dropped. See image-failure-reporting.md.",
            48, 550);
        #endregion

        return document;
    }
}
