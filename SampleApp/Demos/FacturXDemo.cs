using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.EInvoice;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A hybrid e-invoice: one file that a person reads and a machine books, which is what the
///   European invoicing mandates ask for and what PDF/A-3 exists to carry.
/// </summary>
/// <remarks>
///   The Archive demo is the PDF/A claim itself. This one is what the claim is for: the same
///   invoice twice over, drawn on the page and attached as XML, tied together by an
///   <c>/AFRelationship</c> and described in the metadata by an extension schema. Every one of
///   those has a silent failure mode - the file opens perfectly and the system it was sent to
///   rejects it - which is the argument for <c>PdfPinata.EInvoice</c> being a package rather
///   than a paragraph of documentation.
/// </remarks>
internal sealed class FacturXDemo : PdfDemo
{
    public FacturXDemo() : base() { }

    public override string Name => "FacturX";

    public override string Summary => "A ZUGFeRD / Factur-X invoice: PDF/A-3 with its own XML inside it.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "FacturXInvoice.AttachTo - the file name, the /Data relationship and the media type",
        "That attaching the invoice is what claims PDF/A-3, the only profile that may carry one",
        "The XMP extension schema, which declares the fx: properties the packet then writes",
        "EInvoiceProfile - the exact spelling a receiver reads, spaces and all",
        "FacturXInvoice.FindIn and ReadFrom - the receiving half of the mandate",
        "That the XML carries the line items the page draws, from the same array, and reconciles",
        "That an RGB document claiming PDF/A is given an sRGB output intent it never asked for",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        XFont heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        XFont label = new XFont(BundledFontResolver.SansFamily, 9.5, XFontStyle.Bold);
        XFont body = new XFont(BundledFontResolver.SansFamily, 9);
        XFont mono = new XFont(BundledFontResolver.MonoFamily, 7);

        PdfDocument document = new PdfDocument();

        // A PDF/A document has to have a title, and attaching the invoice below is what makes this
        // a PDF/A document - so this line is load-bearing rather than decorative.
        document.Info.Title = "Invoice 2026-0042";
        document.Info.Author = "PdfPinata sample app";
        document.Info.Subject = "A Factur-X invoice: the page and the XML are the same invoice";

        // Nothing here says anything about colour, and the document still gets the output intent
        // PDF/A requires: colours written as RGB by a library nobody told otherwise are sRGB, so an
        // RGB document that names no profile is given PdfOutputIntents.SrgbProfile and the sRGB
        // condition to name it by. The Archive demo sets it explicitly and gets the same bytes.

        // ----- page one: the invoice a person reads ------------------------------------------------

        PdfPage first = document.AddPage();
        using (XGraphics gfx = XGraphics.FromPdfPage(first))
        {
            XTextFormatter prose = new XTextFormatter(gfx);

            gfx.DrawString("Invoice 2026-0042", heading, XBrushes.Black, 50, 60);
            gfx.DrawString("Issued 14 August 2026 · payable within 30 days", body, XBrushes.Gray, 50, 78);

            gfx.DrawString("PdfPinata Ltd", label, XBrushes.Black, 50, 108);
            gfx.DrawString("Kölnstraße 1, 50667 Köln", body, XBrushes.Black, 50, 122);
            gfx.DrawString("VAT DE123456789", body, XBrushes.Black, 50, 136);

            gfx.DrawString("Billed to", label, XBrushes.Black, 330, 108);
            gfx.DrawString("Beispiel GmbH", body, XBrushes.Black, 330, 122);
            gfx.DrawString("Musterweg 12, 10115 Berlin", body, XBrushes.Black, 330, 136);

            // A column of money is read by comparing digits that line up, so every numeric column -
            // and the heading over it - is placed by its right edge instead of its left. The point
            // overload of DrawString takes an XStringFormat, and BaseLineRight is the one that moves
            // the alignment without moving the baseline: XStringFormats.TopRight would also shift
            // every line down by the ascent, because a point has no rectangle to sit at the top of.
            XStringFormat figures = XStringFormats.BaseLineRight;

            double y = 180;
            gfx.DrawString("Description", label, XBrushes.Black, 50, y);
            gfx.DrawString("Qty", label, XBrushes.Black, 375, y, figures);
            gfx.DrawString("Unit", label, XBrushes.Black, 460, y, figures);
            gfx.DrawString("Amount", label, XBrushes.Black, 545, y, figures);
            gfx.DrawLine(XPens.Black, 50, y + 5, 545, y + 5);

            y += 22;
            foreach ((string Description, int Quantity, decimal UnitPrice) item in Items)
            {
                decimal amount = item.Quantity * item.UnitPrice;
                gfx.DrawString(item.Description, body, XBrushes.Black, 50, y);
                gfx.DrawString(item.Quantity.ToString(Invariant), body, XBrushes.Black, 375, y, figures);
                gfx.DrawString(Money(item.UnitPrice), body, XBrushes.Black, 460, y, figures);
                gfx.DrawString(Money(amount), body, XBrushes.Black, 545, y, figures);
                y += 16;
            }

            gfx.DrawLine(XPens.Gray, 350, y + 2, 545, y + 2);
            y += 20;

            (string Caption, decimal Amount)[] totals =
            {
                ("Net", Net),
                ("VAT 19%", Tax),
                ("Total due", Gross),
            };

            foreach ((string Caption, decimal Amount) total in totals)
            {
                XFont font = total.Caption == "Total due" ? label : body;
                gfx.DrawString(total.Caption, font, XBrushes.Black, 460, y, figures);
                gfx.DrawString(Money(total.Amount), font, XBrushes.Black, 545, y, figures);
                y += 16;
            }

            gfx.DrawString("This page is half of the invoice", label, XBrushes.Firebrick, 50, y + 34);

            prose.DrawString(
                "The other half is attached to this file as XML, and the two are the same invoice "
                + "rather than a document with a copy of itself inside it. A person reads the page; "
                + "an accounts system reads the attachment and books it without anybody retyping a "
                + "figure. Open the attachments pane of a reader to find it, under the name the "
                + "standard requires - factur-x.xml, and nothing else, because a receiver looks for "
                + "it by that name.",
                body, XBrushes.Black, new XRect(50, y + 48, 495, 62));
        }

        // ----- the attachment: what makes it a Factur-X invoice ------------------------------------

        byte[] xml = Encoding.UTF8.GetBytes(CrossIndustryInvoice());

        // The whole of the PDF side of ZUGFeRD and Factur-X. It names the attachment factur-x.xml,
        // relates it to the document as /Data, calls it text/xml, associates it with the catalog so
        // that it is part of the document rather than merely inside it, claims PDF/A-3 - the only
        // archival profile that may carry a file at all - and writes the two metadata descriptions
        // the format wants. None of that is difficult; all of it is silent when it is wrong.
        FacturXInvoice invoice = new FacturXInvoice(xml)
        {
            // The profile is a claim about the XML, and the spelling of it is a trap worth an enum:
            // the value a receiver reads is "EN 16931", with the space, and "BASIC WL" for the one
            // below it. A document writing EN16931 passes every check that looks at the PDF.
            Profile = EInvoiceProfile.En16931,
            Description = "Invoice 2026-0042 as EN 16931 CII data",
        };

        PdfFileSpecification attached = invoice.AttachTo(document);

        // ----- page two: what that did -------------------------------------------------------------

        PdfPage second = document.AddPage();
        using (XGraphics gfx = XGraphics.FromPdfPage(second))
        {
            XTextFormatter prose = new XTextFormatter(gfx);

            gfx.DrawString("What the attachment has to satisfy", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "Read back from this document rather than described. The relationship says what the "
                + "file is to the document - /Data means the XML and the page are one invoice - and "
                + "the catalog's /AF array is what associates it. A file in the embedded-files name "
                + "tree alone is invisible to a validator; a file in /AF alone is invisible to a "
                + "reader's attachments pane. Both, or it is not an e-invoice.",
                body, XBrushes.Black, new XRect(50, 80, 495, 62));

            (string Field, string Value)[] facts =
            {
                ("File name", attached.FileName),
                ("/AFRelationship", "/" + attached.Relationship),
                ("Media type", attached.EmbeddedFile.MimeType),
                ("Description", attached.Description),
                ("Attached bytes", xml.Length.ToString("N0", Invariant) + " bytes of CII XML"),
                ("Conformance now claimed", document.Options.Conformance.ToString()),
                ("Output intent", PdfOutputIntents.SrgbIdentifier + ", "
                    + PdfOutputIntents.SrgbProfile.Length.ToString("N0", Invariant)
                    + " bytes, supplied by the writer"),
            };

            double y = 155;
            foreach ((string Field, string Value) fact in facts)
            {
                gfx.DrawString(fact.Field, label, XBrushes.Black, 50, y);
                gfx.DrawString(fact.Value, body, XBrushes.Black, 230, y);
                y += 16;
            }

            gfx.DrawString("Nobody set the conformance", label, XBrushes.Firebrick, 50, y + 14);

            prose.DrawString(
                "Attaching the invoice claimed PDF/A-3, because a Factur-X document is a PDF/A-3 "
                + "document by definition and no other archival profile may carry a file. A claim of "
                + "PDF/A-1 or PDF/A-2 already on the document is refused rather than promoted: those "
                + "profiles carry nothing, and quietly rewriting a caller's claim would be deciding "
                + "for them which standard their document meets.",
                body, XBrushes.Black, new XRect(50, y + 28, 495, 62));

            gfx.DrawString("The metadata, from a document built the same way", label, XBrushes.Black, 50, y + 100);

            prose.DrawString(
                "PDF/A holds every property in the metadata packet to a schema the file either "
                + "predefines or describes, and the invoice namespace is nobody's predefined schema. "
                + "So the packet declares the four fx: properties in an extension schema before "
                + "writing them - without that, the file fails validation for its metadata rather "
                + "than for its invoice, which is a confusing way to be wrong. Note that "
                + "fx:DocumentFileName is the name the file was actually attached under: a receiver "
                + "takes the attachment by the name the metadata gives it.",
                body, XBrushes.Black, new XRect(50, y + 114, 495, 76));

            y += 202;
            foreach (string line in InvoiceMetadataOfAProbe())
            {
                if (y > 792)
                    break;

                gfx.DrawString(line, mono, XBrushes.Black, 50, y);
                y += 8.4;
            }
        }
        #endregion

        return document;
    }

    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    ///   The invoice, stated once. The page below and the XML attached to it are both written from
    ///   this array, which is the whole claim a hybrid invoice makes: not a document with a copy of
    ///   itself inside it, but one invoice rendered twice - once for a person and once for a
    ///   machine. Two literals that happened to agree would demonstrate the opposite.
    /// </summary>
    static readonly (string Description, int Quantity, decimal UnitPrice)[] Items =
    {
        ("PdfPinata support, annual", 1, 1200.00m),
        ("Migration consultancy, per day", 2, 780.00m),
        ("Font licensing review", 1, 450.00m),
    };

    const decimal VatRate = 19.00m;

    // Worked out once, in declaration order, from the array above - so the page and the XML cannot
    // state different totals for the same invoice however either of them is edited later.
    static readonly decimal Net = SumOfLines();

    static readonly decimal Tax = decimal.Round(Net * VatRate / 100m, 2);

    static readonly decimal Gross = Net + Tax;

    static decimal SumOfLines()
    {
        decimal net = 0;
        foreach ((string Description, int Quantity, decimal UnitPrice) item in Items)
            net += item.Quantity * item.UnitPrice;
        return net;
    }

    static string Money(decimal amount) => amount.ToString("N2", Invariant) + " EUR";

    /// <summary>The plain decimal a CII amount is written as: no grouping, no currency, two places.</summary>
    static string Amount(decimal amount) => amount.ToString("F2", Invariant);

    /// <summary>
    ///   The invoice as UN/CEFACT Cross Industry Invoice XML, which is what ZUGFeRD and Factur-X
    ///   carry - the same line items the page above draws, priced and totalled the same way.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The line items are here rather than the grand total alone, because the grand total alone
    ///   would misrepresent what the format is for. A receiver books an invoice from its lines: EN
    ///   16931 requires at least one, each with its identifier, its price, its quantity, its tax
    ///   category and its own line total, and requires the header summation to reconcile against
    ///   them. An XML stating only what was payable would be a receipt, and the page beside it
    ///   would be the only place the detail existed - which is the thing a hybrid invoice exists
    ///   not to be.
    ///   </para>
    ///   <para>
    ///   <b>Still a plausible skeleton, not a validated EN 16931 document.</b> A real one also
    ///   states the seller and buyer addresses and tax registrations, the payment means and terms,
    ///   the delivery event, and any allowances and charges, under business rules that differ by
    ///   country and are revised on a public timetable. Generating and validating that is somebody
    ///   else's library and a permanent maintenance liability - deliberately outside
    ///   <c>PdfPinata.EInvoice</c>, which takes the bytes and puts them in the document
    ///   correctly. This is enough to show what shape they are and how they answer to the page.
    ///   </para>
    /// </remarks>
    static string CrossIndustryInvoice()
    {
        StringBuilder xml = new StringBuilder();

        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        xml.Append("<rsm:CrossIndustryInvoice\n");
        xml.Append("    xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"\n");
        xml.Append("    xmlns:ram=\"urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100\"\n");
        xml.Append("    xmlns:udt=\"urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100\">\n");
        xml.Append("  <rsm:ExchangedDocumentContext>\n");
        xml.Append("    <ram:GuidelineSpecifiedDocumentContextParameter>\n");
        xml.Append("      <ram:ID>urn:cen.eu:en16931:2017</ram:ID>\n");
        xml.Append("    </ram:GuidelineSpecifiedDocumentContextParameter>\n");
        xml.Append("  </rsm:ExchangedDocumentContext>\n");
        xml.Append("  <rsm:ExchangedDocument>\n");
        xml.Append("    <ram:ID>2026-0042</ram:ID>\n");
        xml.Append("    <ram:TypeCode>380</ram:TypeCode>\n");
        xml.Append("    <ram:IssueDateTime><udt:DateTimeString format=\"102\">20260814</udt:DateTimeString></ram:IssueDateTime>\n");
        xml.Append("  </rsm:ExchangedDocument>\n");
        xml.Append("  <rsm:SupplyChainTradeTransaction>\n");

        // The lines come first: CII puts them before the three header groups rather than after
        // them, so a document writing them last is refused by the schema rather than by a business
        // rule. One element per row of the table on page one, in the same order.
        int line = 0;
        foreach ((string Description, int Quantity, decimal UnitPrice) item in Items)
        {
            line++;
            decimal amount = item.Quantity * item.UnitPrice;

            xml.Append("    <ram:IncludedSupplyChainTradeLineItem>\n");
            xml.Append("      <ram:AssociatedDocumentLineDocument>\n");
            xml.Append("        <ram:LineID>" + line.ToString(Invariant) + "</ram:LineID>\n");
            xml.Append("      </ram:AssociatedDocumentLineDocument>\n");
            xml.Append("      <ram:SpecifiedTradeProduct>\n");
            xml.Append("        <ram:Name>" + Escaped(item.Description) + "</ram:Name>\n");
            xml.Append("      </ram:SpecifiedTradeProduct>\n");
            xml.Append("      <ram:SpecifiedLineTradeAgreement>\n");
            xml.Append("        <ram:NetPriceProductTradePrice>\n");
            xml.Append("          <ram:ChargeAmount>" + Amount(item.UnitPrice) + "</ram:ChargeAmount>\n");
            xml.Append("        </ram:NetPriceProductTradePrice>\n");
            xml.Append("      </ram:SpecifiedLineTradeAgreement>\n");
            xml.Append("      <ram:SpecifiedLineTradeDelivery>\n");
            // C62 is UN/ECE Recommendation 20 for "one of something", which is what a year of
            // support or a day of consultancy is billed in. The unit code is not optional.
            xml.Append("        <ram:BilledQuantity unitCode=\"C62\">"
                + item.Quantity.ToString(Invariant) + "</ram:BilledQuantity>\n");
            xml.Append("      </ram:SpecifiedLineTradeDelivery>\n");
            xml.Append("      <ram:SpecifiedLineTradeSettlement>\n");
            xml.Append("        <ram:ApplicableTradeTax>\n");
            xml.Append("          <ram:TypeCode>VAT</ram:TypeCode>\n");
            xml.Append("          <ram:CategoryCode>S</ram:CategoryCode>\n");
            xml.Append("          <ram:RateApplicablePercent>" + Amount(VatRate) + "</ram:RateApplicablePercent>\n");
            xml.Append("        </ram:ApplicableTradeTax>\n");
            xml.Append("        <ram:SpecifiedTradeSettlementLineMonetarySummation>\n");
            xml.Append("          <ram:LineTotalAmount>" + Amount(amount) + "</ram:LineTotalAmount>\n");
            xml.Append("        </ram:SpecifiedTradeSettlementLineMonetarySummation>\n");
            xml.Append("      </ram:SpecifiedLineTradeSettlement>\n");
            xml.Append("    </ram:IncludedSupplyChainTradeLineItem>\n");
        }

        xml.Append("    <ram:ApplicableHeaderTradeAgreement>\n");
        xml.Append("      <ram:SellerTradeParty><ram:Name>PdfPinata Ltd</ram:Name></ram:SellerTradeParty>\n");
        xml.Append("      <ram:BuyerTradeParty><ram:Name>Beispiel GmbH</ram:Name></ram:BuyerTradeParty>\n");
        xml.Append("    </ram:ApplicableHeaderTradeAgreement>\n");
        // Empty and present: nothing was delivered to a place, and the group still has to be here
        // for the settlement group after it to be where the schema expects to find it.
        xml.Append("    <ram:ApplicableHeaderTradeDelivery/>\n");
        xml.Append("    <ram:ApplicableHeaderTradeSettlement>\n");
        xml.Append("      <ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode>\n");
        // One breakdown per rate, and everything here is at the one rate. The basis is what the
        // rate was applied to, so a receiver can recompute the tax rather than take it on trust.
        xml.Append("      <ram:ApplicableTradeTax>\n");
        xml.Append("        <ram:CalculatedAmount>" + Amount(Tax) + "</ram:CalculatedAmount>\n");
        xml.Append("        <ram:TypeCode>VAT</ram:TypeCode>\n");
        xml.Append("        <ram:BasisAmount>" + Amount(Net) + "</ram:BasisAmount>\n");
        xml.Append("        <ram:CategoryCode>S</ram:CategoryCode>\n");
        xml.Append("        <ram:RateApplicablePercent>" + Amount(VatRate) + "</ram:RateApplicablePercent>\n");
        xml.Append("      </ram:ApplicableTradeTax>\n");
        // The five amounts the header states, and they have to reconcile: the line total is the
        // sum of the lines, the tax basis is that after allowances and charges, the grand total is
        // basis plus tax, and due payable is the grand total less anything already prepaid. A
        // receiver checks every one of those sums and rejects the invoice, not the arithmetic.
        xml.Append("      <ram:SpecifiedTradeSettlementHeaderMonetarySummation>\n");
        xml.Append("        <ram:LineTotalAmount>" + Amount(Net) + "</ram:LineTotalAmount>\n");
        xml.Append("        <ram:TaxBasisTotalAmount>" + Amount(Net) + "</ram:TaxBasisTotalAmount>\n");
        xml.Append("        <ram:TaxTotalAmount currencyID=\"EUR\">" + Amount(Tax) + "</ram:TaxTotalAmount>\n");
        xml.Append("        <ram:GrandTotalAmount>" + Amount(Gross) + "</ram:GrandTotalAmount>\n");
        xml.Append("        <ram:DuePayableAmount>" + Amount(Gross) + "</ram:DuePayableAmount>\n");
        xml.Append("      </ram:SpecifiedTradeSettlementHeaderMonetarySummation>\n");
        xml.Append("    </ram:ApplicableHeaderTradeSettlement>\n");
        xml.Append("  </rsm:SupplyChainTradeTransaction>\n");
        xml.Append("</rsm:CrossIndustryInvoice>\n");

        return xml.ToString();
    }

    /// <summary>
    ///   Escapes the three characters that cannot stand in element content. A description is the
    ///   one part of this document that comes from outside it, and so the one part that could
    ///   carry an ampersand and turn well-formed XML into an attachment nothing can parse.
    /// </summary>
    static string Escaped(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    /// <summary>
    ///   Builds a probe invoice the same way, saves it, reopens it, and hands back the part of its
    ///   XMP packet that the e-invoice helper wrote - plus what a receiver finds in it.
    /// </summary>
    /// <remarks>
    ///   Reopened rather than described, because the receiving half of the mandate is the half this
    ///   page can actually demonstrate: <see cref="FacturXInvoice.FindIn"/> is what an accounts
    ///   system runs over an arriving PDF, and it answers from a document nothing has told where the
    ///   invoice is. Germany has required the ability to receive one since January 2025.
    /// </remarks>
    static IEnumerable<string> InvoiceMetadataOfAProbe()
    {
        using PdfDocument probe = new PdfDocument();
        probe.AddPage();
        probe.Info.Title = "A probe";

        byte[] xml = Encoding.UTF8.GetBytes(CrossIndustryInvoice());
        new FacturXInvoice(xml).AttachTo(probe);

        using MemoryStream buffer = new MemoryStream();
        probe.Save(buffer, false);
        buffer.Position = 0;

        using PdfDocument reopened = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);

        PdfDictionary metadata = reopened.Internals.Catalog.Elements.GetDictionary("/Metadata");
        string packet = Encoding.UTF8.GetString(metadata.Stream.UnfilteredValue);

        // The extension schema and the properties it declares, which is the part of the packet this
        // package exists to write. The rest of it - the title, the producer, the pdfaid identifier -
        // is the Archive demo's page two.
        int begins = packet.IndexOf("<rdf:Description rdf:about=\"\" xmlns:pdfaExtension", StringComparison.Ordinal);
        int ends = packet.IndexOf("</rdf:RDF>", StringComparison.Ordinal);

        if (begins >= 0 && ends > begins)
        {
            foreach (string line in packet.Substring(begins, ends - begins).TrimEnd().Split('\n'))
                yield return line.TrimEnd();
        }

        yield return "";

        // And the other direction: what a system receiving this file finds in it, knowing only that
        // it is a PDF. Found by name, because the standards name the file precisely so that a
        // receiver need not guess - and not by relationship and media type, which every /Data
        // attachment that is text/xml would match.
        PdfFileSpecification found = FacturXInvoice.FindIn(reopened);
        byte[] read = FacturXInvoice.ReadFrom(reopened);

        yield return "FacturXInvoice.FindIn(reopened).FileName  ->  " + found.FileName;
        yield return "FacturXInvoice.ReadFrom(reopened).Length  ->  " + read.Length
            + "  (attached " + xml.Length + ")";
        yield return "First line of it                          ->  "
            + read.Length.ToString(Invariant) + " bytes beginning "
            + Encoding.UTF8.GetString(read, 0, 38).Replace("\n", "");
    }
}
