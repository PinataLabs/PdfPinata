using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The smallest document worth saving, and every field of its metadata.
/// </summary>
internal sealed class HelloWorldDemo : PdfDemo
{
    public HelloWorldDemo() : base() { }

    public override string Name => "HelloWorld";

    public override string Summary => "One page, one string, and the document's metadata.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "The four calls a document needs: new, AddPage, FromPdfPage, Save",
        "Every writable field of PdfDocument.Info",
        "A string centred in a rectangle with XStringFormats.Center",
    };

    public override int PageCount => 1;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();

        // What a reader shows under "document properties". Producer is not set here: the
        // library writes its own and the property is read only.
        document.Info.Title = "Hello World";
        document.Info.Author = "PdfPinata";
        document.Info.Subject = "The smallest document worth saving";
        document.Info.Keywords = "pdfsharpcore; demo; metadata";
        document.Info.Creator = "PdfPinata SampleApp";

        // A fixed date rather than DateTime.Now, so that running the demo twice produces
        // two files that differ only in the document identifier.
        document.Info.CreationDate = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        double width = page.Width.Point;
        double height = page.Height.Point;

        // The rectangle overload plus a format centres the string in the box. The point
        // overload would put the text's baseline at the point instead.
        XFont title = new XFont("Liberation Sans", 30, XFontStyle.Bold);
        gfx.DrawString("Hello, World!", title, XBrushes.Black,
            new XRect(0, 0, width, height * 0.4), XStringFormats.Center);

        // The same metadata again, on the page, so it can be read without a properties
        // dialog - and so a round trip through a reader can be checked against it.
        XFont label = new XFont("Liberation Sans", 10, XFontStyle.Bold);
        XFont value = new XFont("Liberation Sans", 10);

        (string Label, string Value)[] rows =
        {
            ("Title", document.Info.Title),
            ("Author", document.Info.Author),
            ("Subject", document.Info.Subject),
            ("Keywords", document.Info.Keywords),
            ("Creator", document.Info.Creator),
            ("CreationDate", document.Info.CreationDate.ToString("u")),
        };

        double y = height * 0.45;
        foreach ((string Label, string Value) row in rows)
        {
            gfx.DrawString(row.Label, label, XBrushes.Black, new XPoint(80, y));
            gfx.DrawString(row.Value, value, XBrushes.DimGray, new XPoint(190, y));
            y += 18;
        }

        gfx.DrawLine(XPens.LightGray, 80, height * 0.45 - 16, width - 80, height * 0.45 - 16);
        #endregion

        return document;
    }
}
