using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The only demo whose subject is the file rather than the page: reading back the operators a
///   page was drawn with.
/// </summary>
internal sealed class InspectDemo : PdfDemo
{
    public InspectDemo() : base() { }

    public override string Name => "Inspect";

    public override string Summary => "Reading a page's own content stream back with ContentReader.";

    public override IReadOnlyList<string> Shows => [
        "ContentReader.ReadContent, over a page this demo drew a moment earlier",
        "The CObject model - COperator, CInteger, CReal, CString, CName, CArray",
        "The operators a few ordinary drawing calls actually produce, listed in order",
        "A count by operator, which is the fastest way to see what a page is made of",
        "Why the text reads as numbers: a font embedded as Identity-H shows glyph ids, not letters"
    ];

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var document = new PdfDocument();
        document.Info.Title = "Inspect";

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        var body = new XFont("Liberation Sans", 9);
        var mono = new XFont("Source Code Pro", 7.5);

        // ----- page 1: something worth reading back -----

        var subject = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(subject))
        {
            gfx.DrawString("The page being read", heading, XBrushes.Black, new XPoint(50, 60));

            new XTextFormatter(gfx).DrawString(
                "A deliberately short page, so that the operators it produces fit on the next one "
                + "and can be read against the calls that made them. Six calls: a string, a "
                + "rectangle with a pen and a brush, a line, an ellipse, a path and a second "
                + "string in another colour.",
                body, XBrushes.Black, new XRect(50, 80, 495, 50));

            gfx.DrawRectangle(new XPen(XColors.MidnightBlue, 2),
                new XSolidBrush(XColor.FromArgb(60, 70, 130, 180)), 50, 150, 200, 100);
            gfx.DrawLine(new XPen(XColors.Firebrick, 3), 300, 150, 500, 250);
            gfx.DrawEllipse(new XPen(XColors.SeaGreen, 1.5), null, 50, 280, 200, 100);

            var path = new XGraphicsPath();
            path.AddPolygon([
                new XPoint(320, 290), new XPoint(420, 290), new XPoint(370, 370)
            ]);
            gfx.DrawPath(new XPen(XColors.DarkOrange, 1.5), path);

            gfx.DrawString("Six calls, and the operators overleaf", body, XBrushes.Firebrick,
                new XPoint(50, 410));
        }

        // docs:begin read-content
        // The page has to be saved and reopened before its content can be read: what is being
        // read is the content stream as it was written, and until the document is saved there is
        // no stream to read. This is the same round trip the test suite's helpers make.
        byte[] saved;
        using (var buffer = new MemoryStream())
        {
            document.Save(buffer, false);
            saved = buffer.ToArray();
        }

        var operators = new List<COperator>();
        using (var buffer = new MemoryStream(saved))
        {
            using var reopened = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);

            // One call. What comes back is a CSequence - a list of CObject, most of which are the
            // operators, each carrying the operands it was given.
            var content = ContentReader.ReadContent(reopened.Pages[0]);
            operators.AddRange(content.OfType<COperator>());
        }
        // docs:end read-content

        // ----- page 2: the operators themselves -----

        var listing = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(listing))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What the page is made of", heading, XBrushes.Black, new XPoint(50, 60));

            prose.DrawString(
                $"The previous page came back as {operators.Count} operators. The first sixty are "
                + "below, each with its operands, in the order they were written. An operator's "
                + "name is the PDF one rather than the drawing call's - re for a rectangle, S to "
                + "stroke, f to fill, B to do both, Tj to show text.",
                body, XBrushes.Black, new XRect(50, 80, 495, 50));

            // docs:begin operands
            // The operand types are the whole of the CObject model: numbers, strings, names and
            // arrays. Rendering them by type is what makes the model visible rather than the text.
            string Describe(CObject operand) => operand switch
            {
                CInteger integer => integer.Value.ToString(),
                CReal real => real.Value.ToString("0.###"),
                CName name => name.Name,
                CString text => $"({text.Value.Length} bytes)",
                CArray array => $"[{array.Count} items]",
                _ => operand.ToString() ?? ""
            };
            // docs:end operands

            double y = 140;
            double x = 50;
            foreach (var op in operators.Take(60))
            {
                var operands = string.Join(" ", op.Operands.Select(Describe));
                gfx.DrawString(op.OpCode.Name, mono, XBrushes.Firebrick, new XPoint(x, y));
                gfx.DrawString(operands.Length > 44 ? string.Concat(operands.AsSpan(0, 41), "...") : operands,
                    mono, XBrushes.DimGray, new XPoint(x + 26, y));

                y += 11;
                if (y > 740)
                {
                    y = 140;
                    x += 250;
                }
            }
        }

        // ----- page 3: the tally, and what the text does not say -----

        var tally = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(tally))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("By operator", heading, XBrushes.Black, new XPoint(50, 60));

            prose.DrawString(
                "The same content counted rather than listed, which is usually the more useful "
                + "view: it says at a glance whether a page is mostly text, mostly paths or mostly "
                + "graphics state, and a page that is unexpectedly large usually says so here.",
                body, XBrushes.Black, new XRect(50, 80, 495, 45));

            // docs:begin tally
            var counted = operators
                .GroupBy(op => op.OpCode.Name)
                .Select(group => new { Name = group.Key, Count = group.Count() })
                .OrderByDescending(entry => entry.Count)
                .ThenBy(entry => entry.Name)
                .ToList();
            // docs:end tally

            (string Code, string Means)[] glossary =
            [
                ("q", "save the graphics state"), ("Q", "restore it"),
                ("cm", "concatenate a matrix"), ("re", "add a rectangle to the path"),
                ("m", "move to"), ("l", "line to"), ("c", "curve to"), ("h", "close the figure"),
                ("S", "stroke"), ("f", "fill"), ("f*", "fill, even-odd"), ("B", "fill and stroke"),
                ("n", "end the path, painting nothing"), ("W", "use the path as a clip"),
                ("W*", "clip, even-odd"), ("BT", "begin text"), ("ET", "end text"),
                ("Tf", "set font and size"), ("Td", "move the text position"),
                ("Tj", "show text"), ("TJ", "show text, with the parts moved apart"),
                ("rg", "set a non-stroking colour"), ("RG", "set a stroking colour"),
                ("w", "set the line width"), ("gs", "apply an extended graphics state"),
                ("J", "set the line cap"), ("j", "set the line join"), ("d", "set the dash"),
                ("Do", "paint an XObject"), ("M", "set the miter limit")
            ];

            double y = 140;
            foreach (var entry in counted)
            {
                var means = glossary.FirstOrDefault(item => item.Code == entry.Name).Means;

                gfx.DrawString($"{entry.Count,4}", mono, XBrushes.Black, new XPoint(50, y));
                gfx.DrawString(entry.Name, mono, XBrushes.Firebrick, new XPoint(90, y));
                gfx.DrawString(means ?? "", body, XBrushes.DimGray, new XPoint(130, y));
                y += 13;
            }

            gfx.DrawString("Why the text is not readable", label, XBrushes.Black,
                new XPoint(50, y + 20));

            prose.DrawString(
                "The operands of a Tj are shown above as a byte count rather than as words, and "
                + "that is not the reader being coy. Fonts here are embedded as Identity-H by "
                + "default, so a show-text operator carries two-byte glyph identifiers rather than "
                + "characters - the face's own numbering, which means nothing without the font's "
                + "tables. Setting XPdfFontOptions.WinAnsiDefault writes readable string literals "
                + "instead, at the cost of the characters WinAnsi cannot represent. The Unicode "
                + "demo is where that trade is laid out.",
                body, XBrushes.Black, new XRect(50, y + 33, 495, 80));

            prose.DrawString(
                "Reading content back is how the test suite checks what the renderer wrote without "
                + "rasterizing anything - four of its helpers do exactly this. It is also the "
                + "quickest answer to \"why is my page blank\": a page whose operators are there "
                + "but whose coordinates are wrong looks identical to one that drew nothing, and "
                + "only one of the two says so here.",
                body, XBrushes.Black, new XRect(50, y + 118, 495, 60));
        }
        #endregion

        return document;
    }
}
