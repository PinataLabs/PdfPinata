using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Drawing.BarCodes;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The four codes <c>Drawing.BarCodes</c> can draw, and the options that shape them.
/// </summary>
internal sealed class BarcodesDemo : PdfDemo
{
    public BarcodesDemo() : base() { }

    public override string Name => "Barcodes";

    public override string Summary => "Code 3 of 9, interleaved 2 of 5, OMR marks and an ECC200 data matrix.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Code 3 of 9 and interleaved 2 of 5, drawn through XGraphics.DrawBarCode",
        "The wide-to-narrow ratio, whose default is 2.6 rather than the 2 or 3 the standard names",
        "The five text locations, including the two that sit the text inside the bars",
        "All four CodeDirection values, each turning about the point it was drawn at",
        "The nine AnchorType values, each against the point it was given",
        "OMR marks, whose 'code' is the bits of a number rather than characters",
        "An ECC200 data matrix through DrawMatrixCode, square and rectangular, with quiet zones",
        "What each code will and will not accept, and what it says when it will not",
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();
        document.Info.Title = "Barcodes";

        XFont heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        XFont label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        XFont note = new XFont("Liberation Sans", 7.5);
        XFont codeText = new XFont("Liberation Sans", 8);

        void Caption(XGraphics gfx, double x, double y, string title, string detail)
        {
            gfx.DrawString(title, label, XBrushes.Black, new XPoint(x, y));
            if (detail.Length > 0)
                gfx.DrawString(detail, note, XBrushes.DimGray, new XPoint(x, y + 11));
        }

        // ----- page 1: the linear codes -----

        PdfPage page1 = document.AddPage();
        XGraphics gfx1 = XGraphics.FromPdfPage(page1);
        gfx1.DrawString("Linear codes", heading, XBrushes.Black, new XPoint(50, 60));

        // docs:begin code39
        // A bar code is an object rather than a call: it carries the text, the size it should
        // occupy and the direction it runs in, and DrawBarCode paints it at a point. That point is
        // the code's Anchor, which is its top left corner until it is told otherwise.
        Code3of9Standard code39 = new Code3of9Standard("PDFSHARP-2026", new XSize(230, 50))
        {
            TextLocation = TextLocation.Below,
        };
        gfx1.DrawBarCode(code39, XBrushes.Black, codeText, new XPoint(50, 95));
        // docs:end code39
        Caption(gfx1, 50, 180, "Code 3 of 9 (Code 39)",
            "0-9, A-Z and - . $ / + % space. Anything else throws, by name.");

        // docs:begin code25
        // Interleaved 2 of 5 packs two digits into every five bars, so it is denser than Code 39
        // and takes digits only - and an even number of them, because of the interleaving.
        Code2of5Interleaved code25 = new Code2of5Interleaved("20260816", new XSize(200, 50))
        {
            TextLocation = TextLocation.Below,
        };
        gfx1.DrawBarCode(code25, XBrushes.Black, codeText, new XPoint(320, 95));
        // docs:end code25
        Caption(gfx1, 320, 180, "Interleaved 2 of 5",
            "Digits, evenly many - two are carried per five bars.");

        // The ratio of a wide bar to a narrow one. The standard allows anything from 2 to 3; the
        // wider the ratio the easier a scanner finds it and the more paper it takes. The default
        // here is 2.6, which is neither of the two numbers the standard actually names.
        double left = 50;
        foreach (double ratio in new[] { 2.0, 2.6, 3.0 })
        {
            // docs:begin ratio
            Code3of9Standard scaled = new Code3of9Standard("RATIO", new XSize(140, 42))
            {
                TextLocation = TextLocation.None,
                WideNarrowRatio = ratio,
            };
            gfx1.DrawBarCode(scaled, XBrushes.Black, codeText, new XPoint(left, 240));
            // docs:end ratio
            #pragma warning disable S1244 // Exact on purpose: compared with the literal the value was taken from.
            gfx1.DrawString($"WideNarrowRatio {ratio:0.0}" + (ratio == 2.6 ? " (default)" : ""),
                note, XBrushes.DimGray, new XPoint(left, 296));
            #pragma warning restore S1244
            left += 165;
        }

        Caption(gfx1, 50, 225, "The same five characters in the same box", "");

        // docs:begin text-location
        // Where the human-readable text goes, and whether it takes room from the bars or sits over
        // them. The two "embedded" locations put it inside the code's own box.
        left = 50;
        foreach (TextLocation location in new[]
        {
            TextLocation.None, TextLocation.Above, TextLocation.Below,
            TextLocation.AboveEmbedded, TextLocation.BelowEmbedded,
        })
        {
            Code3of9Standard located = new Code3of9Standard("TEXT", new XSize(85, 55))
            {
                TextLocation = location,
            };
            gfx1.DrawBarCode(located, XBrushes.Black, codeText, new XPoint(left, 350));
            gfx1.DrawString(location.ToString(), note, XBrushes.DimGray, new XPoint(left, 425));
            left += 100;
        }
        // docs:end text-location

        Caption(gfx1, 50, 335, "TextLocation", "");

        // docs:begin omr
        // OMR is not a bar code in the reading sense. Its "text" is parsed as a number and the
        // marks drawn are that number's bits, which a sorting machine counts rather than decodes.
        // The low bit is forced on by the renderer, so 1382 and 1383 draw the same marks.
        left = 50;
        foreach (int value in new[] { 1, 5, 1382 })
        {
            CodeOmr omr = new CodeOmr(value.ToString(), new XSize(150, 40), CodeDirection.LeftToRight)
            {
                SynchronizeCode = true,
            };
            gfx1.DrawBarCode(omr, XBrushes.Black, new XPoint(left, 480));
            gfx1.DrawString($"OMR for {value}", note, XBrushes.DimGray, new XPoint(left, 535));
            left += 165;
        }
        // docs:end omr

        Caption(gfx1, 50, 465, "OMR marks",
            "The bits of a number, low bit first, behind one synchronisation mark.");

        gfx1.DrawString("What each code accepts", label, XBrushes.Black, new XPoint(50, 590));

        (string Code, string Accepts)[] rules =
        {
            ("Code 3 of 9", "0-9, A-Z and - . $ / + % * space. Anything else throws ArgumentException."),
            ("Interleaved 2 of 5", "Digits, evenly many. Anything else throws ArgumentException."),
            ("OMR", "A number. Text that will not parse becomes zero, and the low bit is forced on."),
            ("Data matrix", "Any text, in ASCII encodation, within the symbol size asked for."),
        };

        double y = 610;
        foreach ((string Code, string Accepts) rule in rules)
        {
            gfx1.DrawString(rule.Code, note, XBrushes.Black, new XPoint(50, y));
            gfx1.DrawString(rule.Accepts, note, XBrushes.DimGray, new XPoint(160, y));
            y += 14;
        }

        // ----- page 2: where a code lands and which way it runs -----

        PdfPage page2 = document.AddPage();
        XGraphics gfx2 = XGraphics.FromPdfPage(page2);
        gfx2.DrawString("Placing a code", heading, XBrushes.Black, new XPoint(50, 60));

        Caption(gfx2, 50, 90, "CodeDirection",
            "The code turns about the point it is drawn at - the red dot - so it can run up a page "
            + "without the caller touching the transform.");

        // docs:begin direction
        // Each of the four is given the same box and the same point. What differs is which way the
        // bars run away from that point, which is why the point is marked on every one.
        // The label goes on the side of the point the code does not occupy, which differs per
        // direction - that being the whole of what this panel is about.
        (CodeDirection Direction, double X, double Y, double LabelY)[] directions =
        {
            (CodeDirection.LeftToRight, 90, 150, -8),
            (CodeDirection.RightToLeft, 400, 150, 14),
            (CodeDirection.TopToBottom, 150, 250, -8),
            (CodeDirection.BottomToTop, 420, 400, 14),
        };

        foreach ((CodeDirection Direction, double X, double Y, double LabelY) each in directions)
        {
            Code3of9Standard turned = new Code3of9Standard("TURN", new XSize(110, 34), each.Direction)
            {
                TextLocation = TextLocation.None,
            };
            gfx2.DrawBarCode(turned, XBrushes.Black, codeText, new XPoint(each.X, each.Y));
            gfx2.DrawEllipse(XBrushes.Firebrick, each.X - 2.5, each.Y - 2.5, 5, 5);
            gfx2.DrawString(each.Direction.ToString(), note, XBrushes.Firebrick,
                new XPoint(each.X + 6, each.Y + each.LabelY));
        }
        // docs:end direction

        Caption(gfx2, 50, 450, "AnchorType",
            "Which part of the code lands on the point given. The default is TopLeft.");

        AnchorType[] anchors =
        {
            AnchorType.TopLeft, AnchorType.TopCenter, AnchorType.TopRight,
            AnchorType.MiddleLeft, AnchorType.MiddleCenter, AnchorType.MiddleRight,
            AnchorType.BottomLeft, AnchorType.BottomCenter, AnchorType.BottomRight,
        };

        for (int index = 0; index < anchors.Length; index++)
        {
            XPoint at = new XPoint(140 + index % 3 * 170, 520 + index / 3 * 100);

            // docs:begin anchor
            Code3of9Standard anchored = new Code3of9Standard("ABC", new XSize(80, 30))
            {
                TextLocation = TextLocation.None,
                Anchor = anchors[index],
            };
            gfx2.DrawBarCode(anchored, XBrushes.Black, codeText, at);
            // docs:end anchor

            // Drawn after the code so the point is not buried under the bars. The label clears the
            // full height of the code below the point, whichever way the anchor put it.
            gfx2.DrawEllipse(XBrushes.Firebrick, at.X - 2.5, at.Y - 2.5, 5, 5);
            gfx2.DrawString(anchors[index].ToString(), note, XBrushes.DimGray,
                new XRect(at.X - 85, at.Y + 40, 170, 10), XStringFormats.TopCenter);
        }

        // ----- page 3: the data matrix -----

        PdfPage page3 = document.AddPage();
        XGraphics gfx3 = XGraphics.FromPdfPage(page3);
        gfx3.DrawString("ECC200 data matrix", heading, XBrushes.Black, new XPoint(50, 60));

        // DrawString does not wrap - it draws one line and runs off the page if the line is too
        // long for it. Anything that has to fit a measure goes through XTextFormatter instead.
        XTextFormatter prose = new XTextFormatter(gfx3);
        prose.DrawString(
            "A data matrix is a MatrixCode rather than a BarCode - a different base class, and "
            + "DrawMatrixCode rather than DrawBarCode. BarCode.FromType says so if asked for one.",
            note, XBrushes.DimGray, new XRect(50, 74, 495, 30));

        // docs:begin data-matrix
        // The symbol size is given in modules, and the encoder needs one large enough for the data
        // plus its error correction. ECC200 fixes the legal sizes; one that is not on the list, or
        // one too small for the text, is refused rather than silently truncated.
        (string Code, int Size, string Note)[] matrices =
        {
            ("PDFSHARPCORE", 16, "16 x 16 modules"),
            ("PDFSHARPCORE-2026-08-16", 22, "22 x 22, the same plus a date"),
            ("https://github.com/PinataLabs/PdfPinata", 32, "32 x 32, a whole URL"),
        };

        left = 50;
        foreach ((string Code, int Size, string Note) matrix in matrices)
        {
            CodeDataMatrix square = new CodeDataMatrix(matrix.Code, matrix.Size, matrix.Size,
                new XSize(120, 120));
            gfx3.DrawMatrixCode(square, XBrushes.Black, new XPoint(left, 110));
            Caption(gfx3, left, 250, matrix.Note, "");
            left += 165;
        }
        // docs:end data-matrix

        // docs:begin rectangular
        // A symbol does not have to be square. ECC200 defines rectangular sizes too, which suit a
        // label with width to spare and no height - a cable marker, a shelf edge.
        (int Rows, int Columns, string Note)[] shapes =
        {
            (18, 18, "18 x 18, square"),
            (8, 32, "8 x 32, rectangular"),
            (12, 36, "12 x 36, rectangular"),
        };

        left = 50;
        foreach ((int Rows, int Columns, string Note) shape in shapes)
        {
            // The drawn size is exactly what it is asked for, so a rectangular symbol given a
            // square box comes out with rectangular modules. Matching the box to the symbol's own
            // proportions is the caller's job.
            double height = 120.0 * shape.Rows / shape.Columns;
            CodeDataMatrix oblong = new CodeDataMatrix("PDFSHARP", shape.Rows, shape.Columns,
                new XSize(120, height));
            gfx3.DrawMatrixCode(oblong, XBrushes.Black, new XPoint(left, 300));
            Caption(gfx3, left, 440, shape.Note, "");
            left += 165;
        }
        // docs:end rectangular

        // docs:begin quiet-zone
        // The quiet zone is the blank margin a reader needs to find the symbol's edges. It is
        // counted in modules and drawn inside the size given, so a wider one shrinks the symbol
        // rather than growing the code. The grey box is the size asked for.
        left = 50;
        foreach (int quiet in new[] { 0, 2, 5 })
        {
            CodeDataMatrix bordered = new CodeDataMatrix("QUIET", "", 16, 16, quiet,
                new XSize(120, 120));
            gfx3.DrawRectangle(new XPen(XColors.Firebrick, 0.5), left, 480, 120, 120);
            gfx3.DrawMatrixCode(bordered, XBrushes.Black, new XPoint(left, 480));
            Caption(gfx3, left, 620, $"QuietZone = {quiet}", "");
            left += 165;
        }
        // docs:end quiet-zone

        gfx3.DrawString("Encodation: ASCII only", label, XBrushes.Black, new XPoint(50, 660));
        prose.DrawString(
            "DataMatrixEncoding names C40, Text, X12, Edifact and Base256 beside it, and every one "
            + "of them throws NotImplementedException rather than encoding wrongly. ASCII carries "
            + "anything a data matrix can hold; it is only less dense over a long run of one case.",
            note, XBrushes.DimGray, new XRect(50, 668, 495, 40));
        #endregion

        return document;
    }
}
