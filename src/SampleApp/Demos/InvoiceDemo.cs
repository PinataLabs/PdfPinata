using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A real business document: letterhead, addressee, line items, totals and terms.
/// </summary>
/// <remarks>
///   PinataLayout's kind of document. Everything here flows: nothing is positioned by arithmetic except
///   the address, the header and footer repeat without being asked twice, and how many pages there
///   are is not known until it has all been laid out - which is why the footer can count them.
///   <para>
///     This invoice happens to fit on one page. What happens when a table does not is the Tables
///     demo's business, and padding this one with filler line items to show it again would make a
///     worse invoice and a worse example.
///   </para>
/// </remarks>
internal sealed class InvoiceDemo : PdfDemo
{
    public InvoiceDemo() : base() { }

    public override string Name => "Invoice";

    public override string Summary => "Letterhead, line items, totals and terms.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "A header and footer that repeat, with page fields resolved at render time",
        "An address block positioned as a text frame, in a window envelope's place",
        "Tab stops aligning a reference block without a table",
        "A borderless item table, merged total rows, and a shaded terms box"
    };

    public override int PageCount => 1;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        // The line items. A record rather than an XML file or a database, so that the data
        // is visible in the same source as the layout that renders it.
        (string Code, string Description, int Quantity, decimal UnitPrice)[] items =
        {
            ("PS-1001", "PdfPinata support, annual", 1, 1200.00m),
            ("PS-1002", "Migration consultancy, per day", 6, 780.00m),
            ("PS-2010", "Font licensing review", 1, 450.00m),
            ("PS-2011", "Embedded subsetting audit", 2, 325.00m),
            ("PS-3100", "Layout engine training, per seat", 12, 145.00m),
            ("PS-3101", "Training materials, printed", 12, 18.50m),
            ("PS-4000", "On-site workshop, two days", 1, 2400.00m),
            ("PS-4001", "Travel and accommodation", 1, 615.40m),
            ("PS-5000", "Document template design", 4, 390.00m),
            ("PS-5001", "Accessibility tagging review", 1, 880.00m),
            ("PS-6000", "Performance profiling", 3, 540.00m),
            ("PS-6001", "Rasterization test harness", 1, 720.00m),
            ("PS-7000", "Priority incident cover, quarterly", 4, 950.00m),
            ("PS-7001", "Out of hours callout allowance", 2, 275.00m),
            ("PS-8000", "Archival conversion, per thousand pages", 34, 12.75m),
            ("PS-8001", "Optical character recognition pass", 34, 8.20m),
            ("PS-9000", "Signature and encryption review", 1, 1150.00m),
            ("PS-9001", "Long term validation setup", 1, 640.00m)
        };

        // docs:begin document-and-styles
        var document = new Document
        {
            Info =
            {
                Title = "Invoice 2026-0417",
                Author = "Thornbury & Vale Ltd"
            }
        };

        document.Styles["Normal"].Font.Name = "Liberation Sans";
        document.Styles["Normal"].Font.Size = 9;

        var reference = document.Styles.AddStyle("Reference", "Normal");
        reference.ParagraphFormat.SpaceBefore = 0;
        reference.ParagraphFormat.SpaceAfter = 0;
        // docs:end document-and-styles

        // docs:begin page-setup
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(4.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        // docs:end page-setup

        // ---- Letterhead ---------------------------------------------------------------
        // The image goes through the ImageSource seam rather than XImage, which is how
        // PinataLayout reaches a backend. The stream factory reads the embedded photograph.
        // docs:begin header-and-footer
        var mark = section.Headers.Primary.AddParagraph();
        mark.Format.Alignment = ParagraphAlignment.Right;
        var logo = mark.AddImage(ImageSource.FromStream(
            "logo.jpg", () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg")));
        logo.Height = Unit.FromCentimeter(1.8);
        logo.LockAspectRatio = true;

        var letterhead = section.Headers.Primary.AddParagraph();
        letterhead.Format.Alignment = ParagraphAlignment.Right;
        letterhead.Format.Font.Size = 8;
        letterhead.Format.Font.Color = Colors.Gray;
        letterhead.AddText("Thornbury & Vale Ltd · 14 Cheapside · Bristol BS1 4TR");
        letterhead.AddLineBreak();
        letterhead.AddText("VAT 271 8834 09 · accounts@thornburyvale.example");

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Invoice 2026-0417 · page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
        // docs:end header-and-footer

        // ---- Addressee ----------------------------------------------------------------
        // A text frame is positioned rather than flowed, which is what puts an address
        // where a window envelope expects to find it.
        // docs:begin address-frame
        var address = section.AddTextFrame();
        address.Width = Unit.FromCentimeter(8);
        address.Height = Unit.FromCentimeter(3);
        address.Left = ShapePosition.Left;
        address.RelativeHorizontal = RelativeHorizontal.Margin;
        address.Top = Unit.FromCentimeter(4.6);
        address.RelativeVertical = RelativeVertical.Page;

        address.AddParagraph("Marlowe & Finch LLP").Format.Font.Bold = true;
        address.AddParagraph("Attn: Accounts Payable");
        address.AddParagraph("88 Corn Street");
        address.AddParagraph("Bristol BS1 1HQ");
        // docs:end address-frame

        // ---- Reference block ----------------------------------------------------------
        // Tab stops align a two column block without the weight of a table. The right
        // aligned stop is what keeps the values flush with the margin.
        var spacer = section.AddParagraph();
        spacer.Format.SpaceAfter = Unit.FromCentimeter(2.6);

        var invoiceTitle = section.AddParagraph("INVOICE");
        invoiceTitle.Format.Font.Size = 20;
        invoiceTitle.Format.Font.Bold = true;
        invoiceTitle.Format.SpaceAfter = Unit.FromPoint(10);

        (string Label, string Value)[] references =
        {
            ("Invoice number", "2026-0417"),
            ("Invoice date", "12 August 2026"),
            ("Payment due", "11 September 2026"),
            ("Purchase order", "MF-PO-88213")
        };

        // docs:begin tab-stops
        foreach ((var label, var value) in references)
        {
            var line = section.AddParagraph();
            line.Style = "Reference";
            line.Format.TabStops.ClearAll();
            line.Format.TabStops.AddTabStop(Unit.FromCentimeter(4), TabAlignment.Left);
            line.AddText(label);
            line.AddTab();
            line.AddFormattedText(value, TextFormat.Bold);
        }
        // docs:end tab-stops

        // ---- Items --------------------------------------------------------------------
        var itemsGap = section.AddParagraph();
        itemsGap.Format.SpaceAfter = Unit.FromPoint(16);

        var table = section.AddTable();
        table.Borders.Width = 0;
        table.Rows.LeftIndent = 0;

        table.AddColumn(Unit.FromCentimeter(2.2)).Format.Alignment = ParagraphAlignment.Left;
        table.AddColumn(Unit.FromCentimeter(7.4)).Format.Alignment = ParagraphAlignment.Left;
        table.AddColumn(Unit.FromCentimeter(1.6)).Format.Alignment = ParagraphAlignment.Right;
        table.AddColumn(Unit.FromCentimeter(2.6)).Format.Alignment = ParagraphAlignment.Right;
        table.AddColumn(Unit.FromCentimeter(2.8)).Format.Alignment = ParagraphAlignment.Right;

        var head = table.AddRow();
        head.HeadingFormat = true;
        head.Format.Font.Bold = true;
        head.Borders.Bottom.Width = 0.8;
        head.Borders.Bottom.Color = Colors.Black;
        head.TopPadding = Unit.FromPoint(2);
        head.BottomPadding = Unit.FromPoint(4);

        string[] headings = { "Code", "Description", "Qty", "Unit price", "Amount" };
        for (var column = 0; column < headings.Length; column++)
            head.Cells[column].AddParagraph(headings[column]);

        decimal net = 0;
        for (var index = 0; index < items.Length; index++)
        {
            (var code, var description, var quantity, var unitPrice) = items[index];
            var amount = quantity * unitPrice;
            net += amount;

            var row = table.AddRow();
            row.TopPadding = Unit.FromPoint(3);
            row.BottomPadding = Unit.FromPoint(3);
            row.Borders.Bottom.Width = 0.25;
            row.Borders.Bottom.Color = Colors.Gainsboro;

            row.Cells[0].AddParagraph(code);
            row.Cells[1].AddParagraph(description);
            row.Cells[2].AddParagraph(quantity.ToString());
            row.Cells[3].AddParagraph($"{unitPrice:N2}");
            row.Cells[4].AddParagraph($"{amount:N2}");
        }

        var vat = net * 0.20m;

        // docs:begin total-rows
        void Total(string label, decimal amount, bool emphasis)
        {
            var row = table.AddRow();
            row.TopPadding = Unit.FromPoint(4);
            row.BottomPadding = Unit.FromPoint(4);
            row.Cells[0].MergeRight = 3;
            row.Cells[0].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[0].AddParagraph(label);
            row.Cells[4].AddParagraph($"{amount:N2}");

            if (emphasis)
            {
                row.Format.Font.Bold = true;
                row.Borders.Top.Width = 0.8;
                row.Borders.Top.Color = Colors.Black;
            }
        }

        Total("Net", net, false);
        Total("VAT at 20%", vat, false);
        Total("Total due (GBP)", net + vat, true);
        // docs:end total-rows

        // ---- Terms --------------------------------------------------------------------
        // docs:begin terms-box
        var terms = section.AddParagraph();
        terms.Format.SpaceBefore = Unit.FromPoint(20);
        terms.Format.Borders.Width = 0.5;
        terms.Format.Borders.Color = Colors.Gainsboro;
        terms.Format.Shading.Color = Colors.WhiteSmoke;
        terms.Format.LeftIndent = Unit.FromPoint(8);
        terms.Format.RightIndent = Unit.FromPoint(8);
        terms.Format.SpaceAfter = Unit.FromPoint(8);
        terms.Format.Font.Size = 8;
        terms.AddFormattedText("Terms. ", TextFormat.Bold);
        terms.AddText("Payment within 30 days by transfer to the account above, quoting the "
            + "invoice number. Interest is charged on overdue amounts at 8% above base rate "
            + "under the Late Payment of Commercial Debts (Interest) Act 1998.");
        // docs:end terms-box

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        #endregion

        return renderer.PdfDocument;
    }
}
