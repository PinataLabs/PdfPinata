using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The drawing surface below text and images: shapes, curves, pens, brushes and transforms.
/// </summary>
internal sealed class VectorsDemo : PdfDemo
{
    public VectorsDemo() : base() { }

    public override string Name => "Vectors";

    public override string Summary => "Every shape XGraphics draws, and the pens, brushes and transforms behind them.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Closed shapes: ellipse, rounded rectangle, polygon, pie, closed curve, and rectangles in one call",
        "The two fill modes on the same self-intersecting star - Alternate hollows the middle, Winding fills it",
        "Open paths: lines, arcs with their start and sweep angles drawn, Beziers with their control points marked",
        "A cardinal spline at three tensions through identical points",
        "Pens: width, the three caps, the three joins, the miter limit, the dash styles and a custom pattern with an offset",
        "Brushes: solid, linear gradient, radial gradient, and a gradient that fades to transparent",
        "Transforms: translate, scale, rotate and a matrix multiplied on; Save/Restore against BeginContainer/EndContainer",
        "IntersectClip, scoped by the graphics state it was set in",
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();
        document.Info.Title = "Vectors";

        XFont heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        XFont label = new XFont("Liberation Sans", 8);
        XFont note = new XFont("Liberation Sans", 7, XFontStyle.Italic);

        // Every panel on every page is a titled box in a grid, so the drawing inside one can be
        // read against the label under it without any of them being positioned by hand.
        void Panel(XGraphics gfx, XRect cell, string title, Action<XGraphics, XRect> draw)
        {
            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 0.5), cell);

            XRect inside = new XRect(cell.X + 8, cell.Y + 8, cell.Width - 16, cell.Height - 30);
            draw(gfx, inside);

            gfx.DrawString(title, label, XBrushes.Black,
                new XRect(cell.X, cell.Bottom - 20, cell.Width, 14), XStringFormats.Center);
        }

        XRect Cell(int column, int row, int columns = 3, int rows = 4) =>
            new XRect(50 + column * (495.0 / columns), 90 + row * (680.0 / rows),
                495.0 / columns - 10, 680.0 / rows - 10);

        // ----- page 1: shapes that enclose an area -----

        PdfPage page1 = document.AddPage();
        XGraphics gfx1 = XGraphics.FromPdfPage(page1);
        gfx1.DrawString("Closed shapes", heading, XBrushes.Black, new XPoint(50, 60));

        XPen outline = new XPen(XColors.MidnightBlue, 1.2);
        XSolidBrush fill = new XSolidBrush(XColor.FromArgb(80, 100, 149, 237));

        Panel(gfx1, Cell(0, 0), "DrawEllipse", (gfx, r) =>
            gfx.DrawEllipse(outline, fill, r));

        Panel(gfx1, Cell(1, 0), "DrawRoundedRectangle", (gfx, r) =>
            // The last argument is the size of the ellipse the corner is a quarter of, not a
            // radius - so a corner as round as it can be is the full height, not half of it.
            gfx.DrawRoundedRectangle(outline, fill, r, new XSize(30, 30)));

        Panel(gfx1, Cell(2, 0), "DrawRectangles - one call", (gfx, r) =>
        {
            // One call, one path, one fill. Three DrawRectangle calls would be three paths, and
            // three times the operators in the content stream.
            double w = r.Width / 4;
            gfx.DrawRectangles(outline, fill, new[]
            {
                new XRect(r.X, r.Y, w, r.Height),
                new XRect(r.X + w * 1.5, r.Y, w, r.Height * 0.6),
                new XRect(r.X + w * 3, r.Y, w, r.Height * 0.3),
            });
        });

        // A five-pointed star drawn as one self-intersecting outline, which is the shape that
        // makes the two fill rules disagree.
        XPoint[] Star(XRect r)
        {
            XPoint centre = new XPoint(r.X + r.Width / 2, r.Y + r.Height / 2);
            double radius = Math.Min(r.Width, r.Height) / 2;
            XPoint[] points = new XPoint[5];
            for (int index = 0; index < 5; index++)
            {
                // Two fifths of a turn between consecutive points is what makes the outline cross
                // itself; one fifth would draw a pentagon.
                double angle = -Math.PI / 2 + index * 4 * Math.PI / 5;
                points[index] = new XPoint(
                    centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));
            }

            return points;
        }

        Panel(gfx1, Cell(0, 1), "DrawPolygon - Alternate", (gfx, r) =>
        {
            // The middle of the star is enclosed twice, so an even number of crossings reaches it
            // and the alternate rule calls it outside. This is the even-odd rule, PDF's f*.
            gfx.DrawPolygon(new XSolidBrush(XColors.Firebrick), Star(r), XFillMode.Alternate);
            gfx.DrawPolygon(outline, Star(r));
        });

        Panel(gfx1, Cell(1, 1), "DrawPolygon - Winding", (gfx, r) =>
        {
            // The same outline, wound consistently, so the non-zero rule fills the middle in.
            // PDF's f. Nothing about the points changed; only the rule reading them.
            gfx.DrawPolygon(new XSolidBrush(XColors.SeaGreen), Star(r), XFillMode.Winding);
            gfx.DrawPolygon(outline, Star(r));
        });

        Panel(gfx1, Cell(2, 1), "DrawPie", (gfx, r) =>
        {
            gfx.DrawPie(outline, new XSolidBrush(XColor.FromArgb(120, 70, 130, 180)),
                r, startAngle: -30, sweepAngle: 120);
            gfx.DrawPie(outline, new XSolidBrush(XColor.FromArgb(120, 205, 92, 92)),
                r, startAngle: 90, sweepAngle: 90);
        });

        XPoint[] Wave(XRect r, int count)
        {
            XPoint[] points = new XPoint[count];
            for (int index = 0; index < count; index++)
            {
                double t = (double)index / (count - 1);
                points[index] = new XPoint(
                    r.X + t * r.Width,
                    r.Y + r.Height / 2 - Math.Sin(t * Math.PI * 2) * r.Height * 0.35);
            }

            return points;
        }

        Panel(gfx1, Cell(0, 2), "DrawClosedCurve", (gfx, r) =>
        {
            // A closed cardinal spline: the last point is joined back to the first with the same
            // smoothing as every other join, which is what makes it a curve rather than a polygon.
            gfx.DrawClosedCurve(fill, Star(r), XFillMode.Winding, 0.5);
            gfx.DrawPolygon(new XPen(XColors.Silver, 0.4) { DashStyle = XDashStyle.Dot }, Star(r));
        });

        Panel(gfx1, Cell(1, 2), "DrawEllipse - no pen", (gfx, r) =>
            // Either argument may be null. A brush and no pen fills without an outline; a pen and
            // no brush outlines without a fill.
            gfx.DrawEllipse(null, new XSolidBrush(XColors.DarkOrange), r));

        Panel(gfx1, Cell(2, 2), "DrawEllipse - no brush", (gfx, r) =>
            gfx.DrawEllipse(new XPen(XColors.DarkOrange, 2), null, r));

        Panel(gfx1, Cell(0, 3), "XGraphicsPath - one shape", (gfx, r) =>
        {
            // A path collects segments and is drawn once, which is how a shape with a hole is made:
            // two figures in one path, filled under the alternate rule.
            XGraphicsPath path = new XGraphicsPath { FillMode = XFillMode.Alternate };
            path.AddEllipse(r);
            path.AddEllipse(new XRect(r.X + r.Width / 4, r.Y + r.Height / 4, r.Width / 2, r.Height / 2));
            gfx.DrawPath(outline, new XSolidBrush(XColors.SlateBlue), path);
        });

        Panel(gfx1, Cell(1, 3), "Path - Winding, same figures", (gfx, r) =>
        {
            // The identical path under the other rule: both ellipses wind the same way, so the
            // inner one does not cancel the outer and there is no hole.
            XGraphicsPath path = new XGraphicsPath { FillMode = XFillMode.Winding };
            path.AddEllipse(r);
            path.AddEllipse(new XRect(r.X + r.Width / 4, r.Y + r.Height / 4, r.Width / 2, r.Height / 2));
            gfx.DrawPath(outline, new XSolidBrush(XColors.SlateBlue), path);
        });

        Panel(gfx1, Cell(2, 3), "AddPie and AddRoundedRectangle", (gfx, r) =>
        {
            XGraphicsPath path = new XGraphicsPath();
            path.AddPie(r.X, r.Y, r.Width, r.Height * 0.9, 200, 140);
            path.AddRoundedRectangle(r.X + 6, r.Y + r.Height * 0.55, r.Width - 12, r.Height * 0.35, 8, 8);
            gfx.DrawPath(outline, fill, path);
        });

        // ----- page 2: open paths and curves -----

        PdfPage page2 = document.AddPage();
        XGraphics gfx2 = XGraphics.FromPdfPage(page2);
        gfx2.DrawString("Open paths and curves", heading, XBrushes.Black, new XPoint(50, 60));

        Panel(gfx2, Cell(0, 0), "DrawLines - one polyline", (gfx, r) =>
            gfx.DrawLines(new XPen(XColors.MidnightBlue, 1.5), Wave(r, 9)));

        Panel(gfx2, Cell(1, 0), "DrawCurve - tension 0", (gfx, r) =>
        {
            // Tension zero is straight between the points: the same polyline as its neighbour.
            gfx.DrawCurve(new XPen(XColors.MidnightBlue, 1.5), Wave(r, 5), 0);
            foreach (XPoint point in Wave(r, 5))
                gfx.DrawEllipse(XBrushes.Firebrick, point.X - 1.5, point.Y - 1.5, 3, 3);
        });

        Panel(gfx2, Cell(2, 0), "DrawCurve - tension 1", (gfx, r) =>
        {
            // The same five points. Only the smoothing changed, and it overshoots them.
            gfx.DrawCurve(new XPen(XColors.MidnightBlue, 1.5), Wave(r, 5), 1);
            foreach (XPoint point in Wave(r, 5))
                gfx.DrawEllipse(XBrushes.Firebrick, point.X - 1.5, point.Y - 1.5, 3, 3);
        });

        Panel(gfx2, Cell(0, 1), "DrawBezier - control points shown", (gfx, r) =>
        {
            XPoint p1 = new XPoint(r.X, r.Bottom);
            XPoint c1 = new XPoint(r.X + r.Width * 0.1, r.Y);
            XPoint c2 = new XPoint(r.X + r.Width * 0.9, r.Y);
            XPoint p2 = new XPoint(r.Right, r.Bottom);

            // The two middle arguments are not on the curve. They are where it is pulled towards,
            // which is the single thing about a Bezier worth drawing rather than describing.
            gfx.DrawBezier(new XPen(XColors.MidnightBlue, 1.5), p1, c1, c2, p2);

            XPen handle = new XPen(XColors.Silver, 0.6) { DashStyle = XDashStyle.Dash };
            gfx.DrawLine(handle, p1, c1);
            gfx.DrawLine(handle, p2, c2);
            foreach (XPoint point in new[] { c1, c2 })
                gfx.DrawEllipse(XBrushes.Firebrick, point.X - 2, point.Y - 2, 4, 4);
            foreach (XPoint point in new[] { p1, p2 })
                gfx.DrawEllipse(XBrushes.MidnightBlue, point.X - 2, point.Y - 2, 4, 4);
        });

        Panel(gfx2, Cell(1, 1), "DrawBeziers - chained", (gfx, r) =>
        {
            // Every curve after the first reuses the previous end point, so the array is
            // 1 + 3n long rather than 4n. Getting that wrong is the usual reason this throws.
            gfx.DrawBeziers(new XPen(XColors.MidnightBlue, 1.5), new[]
            {
                new XPoint(r.X, r.Bottom),
                new XPoint(r.X + r.Width * 0.15, r.Y),
                new XPoint(r.X + r.Width * 0.35, r.Y),
                new XPoint(r.X + r.Width * 0.5, r.Y + r.Height / 2),
                new XPoint(r.X + r.Width * 0.65, r.Bottom),
                new XPoint(r.X + r.Width * 0.85, r.Bottom),
                new XPoint(r.Right, r.Y + r.Height / 2),
            });
        });

        Panel(gfx2, Cell(2, 1), "DrawArc - 0 degrees is 3 o'clock", (gfx, r) =>
        {
            XRect box = new XRect(r.X + 5, r.Y + 5, r.Width - 10, r.Height - 10);
            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 0.4) { DashStyle = XDashStyle.Dot }, box);
            gfx.DrawEllipse(new XPen(XColors.Gainsboro, 0.4) { DashStyle = XDashStyle.Dot }, box);

            // The rectangle is the ellipse the arc is cut from, not a bounding box of the arc.
            // Angles are degrees from 3 o'clock, and a positive sweep turns clockwise.
            gfx.DrawArc(new XPen(XColors.Firebrick, 2), box.X, box.Y, box.Width, box.Height, 0, 90);
            gfx.DrawArc(new XPen(XColors.SeaGreen, 2), box.X, box.Y, box.Width, box.Height, 180, -60);

            gfx.DrawString("0 to +90", note, XBrushes.Firebrick,
                new XPoint(box.X + box.Width * 0.55, box.Bottom + 8));
            gfx.DrawString("180 to -60", note, XBrushes.SeaGreen,
                new XPoint(box.X, box.Y - 2));
        });

        Panel(gfx2, Cell(0, 2), "AddArc between two points", (gfx, r) =>
        {
            // The other AddArc, and the one SVG and WPF use: given where to start, where to finish
            // and the radii, it works out the centre for itself. Four arcs fit any such pair, and
            // the two flags choose between them - the long way round or the short, and which way
            // round. Radii too small for the chord are scaled up until an arc fits, so an arc that
            // came out far larger than asked for usually means radii that could not span the gap.
            XPoint from = new XPoint(r.X + 4, r.Y + r.Height * 0.55);
            XPoint to = new XPoint(r.Right - 4, r.Y + r.Height * 0.55);
            XSize radii = new XSize(r.Width / 2, r.Height * 0.45);

            XGraphicsPath shortWay = new XGraphicsPath();
            shortWay.AddArc(from, to, radii, 0, isLargeArg: false, XSweepDirection.Counterclockwise);
            gfx.DrawPath(new XPen(XColors.MidnightBlue, 1.5), shortWay);

            XGraphicsPath otherWay = new XGraphicsPath();
            otherWay.AddArc(from, to, radii, 0, isLargeArg: false, XSweepDirection.Clockwise);
            gfx.DrawPath(new XPen(XColors.Firebrick, 1.5) { DashStyle = XDashStyle.Dash }, otherWay);
        });

        Panel(gfx2, Cell(1, 2), "A path built segment by segment", (gfx, r) =>
        {
            XGraphicsPath path = new XGraphicsPath();
            path.AddLine(r.X, r.Bottom, r.X + r.Width * 0.25, r.Y);
            path.AddBezier(r.X + r.Width * 0.25, r.Y, r.X + r.Width * 0.4, r.Bottom,
                r.X + r.Width * 0.6, r.Y, r.X + r.Width * 0.75, r.Bottom);
            path.AddLine(r.X + r.Width * 0.75, r.Bottom, r.Right, r.Y);
            gfx.DrawPath(new XPen(XColors.MidnightBlue, 1.5), path);
        });

        Panel(gfx2, Cell(2, 2), "The same path, closed and filled", (gfx, r) =>
        {
            XGraphicsPath path = new XGraphicsPath();
            path.AddLine(r.X, r.Bottom, r.X + r.Width * 0.25, r.Y);
            path.AddBezier(r.X + r.Width * 0.25, r.Y, r.X + r.Width * 0.4, r.Bottom,
                r.X + r.Width * 0.6, r.Y, r.X + r.Width * 0.75, r.Bottom);
            path.AddLine(r.X + r.Width * 0.75, r.Bottom, r.Right, r.Y);
            path.CloseFigure();
            gfx.DrawPath(outline, fill, path);
        });

        Panel(gfx2, Cell(0, 3), "AddPath - connect: false", (gfx, r) =>
        {
            // Two figures kept apart. The appended path starts where it says it starts.
            XGraphicsPath arch = new XGraphicsPath();
            arch.AddArc(r.X, r.Y, r.Width, r.Height, 180, 180);

            XGraphicsPath whole = new XGraphicsPath();
            whole.AddLine(r.X, r.Bottom, r.Right, r.Bottom);
            whole.AddPath(arch, connect: false);
            gfx.DrawPath(new XPen(XColors.MidnightBlue, 1.5), whole);
        });

        Panel(gfx2, Cell(1, 3), "AddPath - connect: true", (gfx, r) =>
        {
            // The same two paths joined into one figure: the arch's first point becomes a line
            // from where the previous figure had got to, so the shape can be filled as one.
            XGraphicsPath arch = new XGraphicsPath();
            arch.AddArc(r.X, r.Y, r.Width, r.Height, 180, 180);

            XGraphicsPath whole = new XGraphicsPath();
            whole.AddLine(r.X, r.Bottom, r.Right, r.Bottom);
            whole.AddPath(arch, connect: true);
            gfx.DrawPath(outline, fill, whole);
        });

        Panel(gfx2, Cell(2, 3), "AddClosedCurve", (gfx, r) =>
        {
            // A closed spline joins the last point back to the first with the same smoothing as
            // every other join, where AddCurve plus CloseFigure would join them with a straight
            // line and leave a corner at the seam.
            XGraphicsPath path = new XGraphicsPath();
            path.AddClosedCurve(Star(r), 0.5);
            gfx.DrawPath(outline, fill, path);
        });

        // ----- page 3: pens and brushes -----

        PdfPage page3 = document.AddPage();
        XGraphics gfx3 = XGraphics.FromPdfPage(page3);
        gfx3.DrawString("Pens and brushes", heading, XBrushes.Black, new XPoint(50, 60));

        Panel(gfx3, Cell(0, 0), "Width", (gfx, r) =>
        {
            double y = r.Y + 6;
            #pragma warning disable CA1861 // The demo prints only its example region, so the values stay beside what they draw.
            foreach (double width in new[] { 0.25, 0.75, 2.0, 5.0 })
            {
                gfx.DrawLine(new XPen(XColors.MidnightBlue, width), r.X, y, r.Right, y);
                y += r.Height / 4;
            }
            #pragma warning restore CA1861
        });

        Panel(gfx3, Cell(1, 0), "LineCap - Flat, Round, Square", (gfx, r) =>
        {
            double y = r.Y + 10;
            foreach (XLineCap cap in new[] { XLineCap.Flat, XLineCap.Round, XLineCap.Square })
            {
                // Flat stops at the point. Round and Square both carry on past it by half the
                // pen's width, which is why a Flat line looks shorter than the other two.
                gfx.DrawLine(new XPen(XColors.MidnightBlue, 8) { LineCap = cap },
                    r.X + 15, y, r.Right - 15, y);
                gfx.DrawLine(new XPen(XColors.Firebrick, 0.4), r.X + 15, r.Y, r.X + 15, r.Bottom);
                gfx.DrawLine(new XPen(XColors.Firebrick, 0.4), r.Right - 15, r.Y, r.Right - 15, r.Bottom);
                y += r.Height / 3;
            }
        });

        Panel(gfx3, Cell(2, 0), "LineJoin - Miter, Round, Bevel", (gfx, r) =>
        {
            double y = r.Y + 6;
            foreach (XLineJoin join in new[] { XLineJoin.Miter, XLineJoin.Round, XLineJoin.Bevel })
            {
                gfx.DrawLines(new XPen(XColors.MidnightBlue, 7) { LineJoin = join }, new[]
                {
                    new XPoint(r.X + 5, y + 14),
                    new XPoint(r.X + r.Width / 2, y),
                    new XPoint(r.Right - 5, y + 14),
                });
                y += r.Height / 3;
            }
        });

        Panel(gfx3, Cell(0, 1), "MiterLimit", (gfx, r) =>
        {
            // A mitre on a sharp corner runs a long way past it - the spike below would reach
            // about six times the pen's width - so there is a limit past which the join is
            // bevelled off instead. These two differ in nothing but that limit, and the corner is
            // deliberately narrow, because a wide one mitres to barely more than the pen's width
            // and no limit worth setting would ever cut it.
            double y = r.Y + 6;
            #pragma warning disable CA1861 // The demo prints only its example region, so the values stay beside what they draw.
            foreach (double limit in new[] { 10.0, 2.0 })
            {
                gfx.DrawLines(new XPen(XColors.MidnightBlue, 6)
                {
                    LineJoin = XLineJoin.Miter,
                    MiterLimit = limit,
                }, new[]
                {
                    new XPoint(r.X + r.Width / 2 - 8, y + 40),
                    new XPoint(r.X + r.Width / 2, y),
                    new XPoint(r.X + r.Width / 2 + 8, y + 40),
                });

                gfx.DrawString($"MiterLimit = {limit:0}", note, XBrushes.Gray,
                    new XPoint(r.X, y + 38));
                y += r.Height / 2;
            }
            #pragma warning restore CA1861
        });

        Panel(gfx3, Cell(1, 1), "DashStyle", (gfx, r) =>
        {
            double y = r.Y + 4;
            foreach (XDashStyle style in new[]
            {
                XDashStyle.Solid, XDashStyle.Dash, XDashStyle.Dot,
                XDashStyle.DashDot, XDashStyle.DashDotDot,
            })
            {
                gfx.DrawLine(new XPen(XColors.MidnightBlue, 1.5) { DashStyle = style },
                    r.X, y, r.Right, y);
                y += r.Height / 5;
            }
        });

        Panel(gfx3, Cell(2, 1), "DashPattern and DashOffset", (gfx, r) =>
        {
            // The pattern is in multiples of the pen's width, on then off, and setting it puts the
            // style on Custom. The offset slides the whole pattern along the line.
            double y = r.Y + 6;
            #pragma warning disable CA1861 // The demo prints only its example region, so the values stay beside what they draw.
            foreach (double offset in new[] { 0.0, 2.0, 4.0 })
            {
                gfx.DrawLine(new XPen(XColors.MidnightBlue, 2)
                {
                    DashPattern = new[] { 4.0, 2.0, 1.0, 2.0 },
                    DashOffset = offset,
                }, r.X, y, r.Right, y);
                y += r.Height / 3;
            }
            #pragma warning restore CA1861
        });

        Panel(gfx3, Cell(0, 2), "XSolidBrush with alpha", (gfx, r) =>
        {
            gfx.DrawRectangle(new XSolidBrush(XColors.Gold), r);
            for (int index = 0; index < 4; index++)
            {
                gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(60, 25, 25, 112)),
                    r.X + index * 12, r.Y + 8, r.Width * 0.55, r.Height * 0.6);
            }
        });

        Panel(gfx3, Cell(1, 2), "XLinearGradientBrush", (gfx, r) =>
            gfx.DrawRectangle(new XLinearGradientBrush(r,
                XColors.MidnightBlue, XColors.Gold, XLinearGradientMode.ForwardDiagonal), r));

        Panel(gfx3, Cell(2, 2), "XRadialGradientBrush", (gfx, r) =>
        {
            XPoint centre = new XPoint(r.X + r.Width / 2, r.Y + r.Height / 2);
            gfx.DrawRectangle(new XRadialGradientBrush(centre, 0,
                Math.Min(r.Width, r.Height) / 2, XColors.Gold, XColors.MidnightBlue), r);
        });

        Panel(gfx3, Cell(0, 3), "A gradient into transparency", (gfx, r) =>
        {
            // A gradient one of whose ends is transparent needs a soft mask as well as a colour
            // ramp. It is drawn over a chequer here so that the transparency is visible as
            // transparency rather than as a colour.
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    gfx.DrawRectangle((x + y) % 2 == 0 ? XBrushes.WhiteSmoke : XBrushes.Gainsboro,
                        r.X + x * r.Width / 8, r.Y + y * r.Height / 5, r.Width / 8, r.Height / 5);
                }
            }

            gfx.DrawRectangle(new XLinearGradientBrush(r,
                XColor.FromArgb(255, 25, 25, 112), XColor.FromArgb(0, 25, 25, 112),
                XLinearGradientMode.Horizontal), r);
        });

        Panel(gfx3, Cell(1, 3), "A pen made from a brush", (gfx, r) =>
        {
            // XPen takes a brush as well as a colour, so a stroke can carry a gradient.
            XPen gradient = new XPen(new XLinearGradientBrush(r,
                XColors.Firebrick, XColors.MidnightBlue, XLinearGradientMode.Horizontal), 6);
            gfx.DrawLines(gradient, Wave(r, 9));
        });

        Panel(gfx3, Cell(2, 3), "Overprint", (gfx, r) =>
        {
            // Written into the graphics state for a press to read. It changes nothing on screen,
            // which is exactly why it is worth a panel that says so.
            gfx.DrawLine(new XPen(XColors.MidnightBlue, 6) { Overprint = true },
                r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
            gfx.DrawString("no effect on screen", note, XBrushes.Gray,
                new XRect(r.X, r.Y + r.Height * 0.6, r.Width, 10), XStringFormats.TopCenter);
        });

        // ----- page 4: transforms, state and clipping -----

        PdfPage page4 = document.AddPage();
        XGraphics gfx4 = XGraphics.FromPdfPage(page4);
        gfx4.DrawString("Transforms, state and clipping", heading, XBrushes.Black, new XPoint(50, 60));

        // One shape, drawn under every transform below, so that what differs between the panels is
        // the matrix rather than the drawing.
        void Arrow(XGraphics gfx)
        {
            XGraphicsPath path = new XGraphicsPath();
            path.AddPolygon(new[]
            {
                new XPoint(0, 8), new XPoint(30, 8), new XPoint(30, 0),
                new XPoint(45, 15), new XPoint(30, 30), new XPoint(30, 22), new XPoint(0, 22),
            });
            gfx.DrawPath(new XPen(XColors.MidnightBlue, 1), new XSolidBrush(
                XColor.FromArgb(120, 100, 149, 237)), path);
        }

        // Two across and two down for the transforms, then one wide panel for the clip - a
        // different grid from the pages above, because a transform needs room to be seen moving.
        XRect Wide(int column, int row) =>
            new XRect(50 + column * 250, 90 + row * 200, 240, 190);

        Panel(gfx4, Wide(0, 0), "TranslateTransform", (gfx, r) =>
        {
            XGraphicsState state = gfx.Save();
            gfx.TranslateTransform(r.X, r.Y + 10);
            Arrow(gfx);

            // Transforms compose rather than replace: the second translate is applied on top of
            // the first, so this arrow is 60 across and 50 down from the one above rather than
            // from the panel's corner.
            gfx.TranslateTransform(60, 50);
            Arrow(gfx);
            gfx.Restore(state);
        });

        Panel(gfx4, Wide(1, 0), "ScaleTransform and RotateTransform", (gfx, r) =>
        {
            XGraphicsState state = gfx.Save();

            // Rotation is about the origin of the current transform, not about the shape - which
            // is why translating to the point everything should turn about comes first.
            gfx.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);

            for (int index = 0; index < 5; index++)
            {
                XGraphicsState turn = gfx.Save();
                gfx.RotateTransform(-90 + index * 45);

                // Translating *after* rotating moves along the axis the rotation left pointing,
                // which is what spaces the arrows around the centre instead of piling them all on
                // top of it. The order transforms are applied in is the whole of the difference.
                gfx.TranslateTransform(28, -10);
                gfx.ScaleTransform(0.7, 0.7);
                Arrow(gfx);
                gfx.Restore(turn);
            }

            gfx.Restore(state);
        });

        Panel(gfx4, Wide(0, 1), "MultiplyTransform - a shear", (gfx, r) =>
        {
            XGraphicsState state = gfx.Save();
            gfx.TranslateTransform(r.X + 20, r.Y + 20);

            // The transform nothing else offers: the four scale-and-skew components written out.
            // Everything but a shear can be had from Translate, Scale and Rotate; this cannot.
            foreach (double lean in new[] { 0.0, -0.5, -1.0 })
            {
                XGraphicsState sheared = gfx.Save();
                gfx.MultiplyTransform(new XMatrix(1, 0, lean, 1, 0, 0));
                Arrow(gfx);
                gfx.Restore(sheared);
                gfx.TranslateTransform(0, 45);
            }

            gfx.Restore(state);
        });

        Panel(gfx4, Wide(1, 1), "Save/Restore against BeginContainer", (gfx, r) =>
        {
            // Save and Restore are a stack of graphics states: everything set between them is
            // undone, and Restore takes a token so the pairs cannot be crossed by accident.
            XGraphicsState state = gfx.Save();
            gfx.TranslateTransform(r.X + 20, r.Y + 10);
            gfx.ScaleTransform(1.4, 1.4);
            Arrow(gfx);
            gfx.Restore(state);

            // A container is the same idea with a nesting of its own, so a routine handed an
            // XGraphics can put the surface back exactly as it found it without knowing, or
            // disturbing, how many states its caller had already pushed.
            XGraphicsContainer container = gfx.BeginContainer();
            gfx.TranslateTransform(r.X + 20, r.Y + 70);
            gfx.RotateTransform(-12);
            Arrow(gfx);
            gfx.EndContainer(container);

            // Both undone, so this one is drawn against the page rather than against either.
            XGraphicsState plain = gfx.Save();
            gfx.TranslateTransform(r.X + 20, r.Y + 125);
            Arrow(gfx);
            gfx.Restore(plain);
        });

        Panel(gfx4, new XRect(50, 490, 495, 190), "IntersectClip", (gfx, r) =>
        {
            XGraphicsState state = gfx.Save();

            // The clip belongs to the graphics state it was set in, and Restore is what takes it
            // off again. Resetting one at a deeper level than it was set is refused outright, which
            // is why a clip and its Save/Restore pair belong together in the same block.
            XGraphicsPath clip = new XGraphicsPath();
            clip.AddEllipse(r);
            gfx.IntersectClip(clip);

            for (double y = r.Y - 60; y < r.Bottom + 60; y += 9)
            {
                gfx.DrawLine(new XPen(XColors.MidnightBlue, 3),
                    r.X - 20, y, r.Right + 20, y - 60);
            }

            gfx.Restore(state);

            // Drawn after the Restore, so it is not clipped: the outline shows where the clip was.
            gfx.DrawEllipse(new XPen(XColors.Firebrick, 0.8), r);
        });
        #endregion

        return document;
    }
}
