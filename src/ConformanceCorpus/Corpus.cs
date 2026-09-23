using System;
using System.Collections.Generic;
using System.Text;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.EInvoice;
using PdfPinata.Pdf;

namespace ConformanceCorpus;

/// <summary>
/// The documents a validator is asked about, and what each of them is for.
/// </summary>
/// <remarks>
/// <para>
/// Every document here <b>makes a claim</b> — a PDF/A part or PDF/UA-1 — and that is the whole
/// entry requirement. veraPDF is run with automatic flavour detection, so each file is held to the
/// profile it names in its own metadata; a file claiming nothing would be held to the fallback
/// flavour instead and fail for saying nothing rather than for being wrong.
/// </para>
/// <para>
/// They are small on purpose. A corpus is read by whoever is looking at a failure, and a hundred-page
/// document says no more about a rule than a paragraph does while making the report far harder to
/// place. Each one covers a feature that has a way of breaking conformance: an embedded subset, an
/// output intent, transparency, an associated file, a structure tree.
/// </para>
/// </remarks>
static class Corpus
{
    internal static IEnumerable<(string Name, byte[] Bytes)> Documents()
    {
        yield return ("pdfa-1b", Drawn(PdfAConformance.PdfA1B, transparency: false));
        yield return ("pdfa-2b", Drawn(PdfAConformance.PdfA2B, transparency: true));
        yield return ("pdfa-3b", Drawn(PdfAConformance.PdfA3B, transparency: true));
        yield return ("pdfa-2b-cff", Cff());
        yield return ("pdfa-3b-facturx", Invoice());
        yield return ("pdfua-1", Tagged(doc => doc.Options.UAConformance = PdfUAConformance.PdfUA1));
        yield return ("pdfa-1a", Tagged(doc => doc.Options.Conformance = PdfAConformance.PdfA1A));
        yield return ("pdfa-2a", Tagged(doc => doc.Options.Conformance = PdfAConformance.PdfA2A));
        yield return ("pdfa-3a", Tagged(doc => doc.Options.Conformance = PdfAConformance.PdfA3A));

        // No pdfua-2 document. ISO 14289-2 clause 8.8 requires every destination internal to the
        // document — outline items, links, OpenAction — to be a "structure destination" rather than
        // a page-relative one, through the /SD entry ISO 32000-2:2020 introduced. That entry is
        // itself unresolved in the published standard: pdf-association/pdf-issues#162 is an open,
        // unfixed errata report that the specification never defines what /SD contains or how a
        // reader is meant to use it. PdfUAConformance.PdfUA2 exists and every other rule this
        // library can check for it is enforced — see its own remarks — but a document is not put in
        // the gated corpus claiming a profile this library cannot yet make good on for that one
        // clause. Adding it back is a matter of building the corpus again, once /SD has an answer.
    }

    /// <summary>
    /// A page drawn straight onto the graphics surface, claiming a PDF/A part.
    /// </summary>
    /// <remarks>
    /// Text in an embedded subset and a filled rectangle: between them they exercise the font
    /// embedding and the output intent, which is most of what a basic-level claim rests on.
    /// Transparency is what separates PDF/A-1b from the two after it — 1b forbids it outright — so
    /// the same page is drawn with and without, and a validator that disagrees with us about which
    /// part allows what says so on exactly one of them.
    /// </remarks>
    private static byte[] Drawn(PdfAConformance conformance, bool transparency)
    {
        var document = new PdfDocument();
        document.Info.Title = "Conformance corpus: " + conformance;
        document.Info.Author = "PdfPinata conformance corpus";

        // The output intent is not set, and that is the point of not setting it: an RGB document
        // claiming PDF/A is given PdfOutputIntents.SrgbProfile and the sRGB condition to name it,
        // so what veraPDF passes here is what a caller gets for writing nothing at all.
        document.Options.Conformance = conformance;

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14);
            gfx.DrawString("PdfPinata conformance corpus", font, XBrushes.Black, 50, 70);
            gfx.DrawString(conformance.ToString(), new XFont("Arial", 24), XBrushes.Black, 50, 110);

            gfx.DrawRectangle(XBrushes.LightSteelBlue, 50, 140, 200, 60);

            // A spot colour, filled and stroked: a /Separation space with a Type 2 tint transform.
            // Its alternate is RGB because the output intent is sRGB - a CMYK alternate is device
            // CMYK to a reader without the ink, and is refused here exactly as "k" would be.
            var spot = new XSpotColor("PdfPinata Corpus Orange", XColor.FromArgb(240, 120, 0));
            gfx.DrawRectangle(new XPen(XColor.FromSpot(spot), 3), new XSolidBrush(XColor.FromSpot(spot, 0.4)), 300, 140, 120, 60);

            if (transparency)
            {
                // Forbidden by PDF/A-1b and allowed by the two after it, which is the one thing
                // these three pages do not have in common.
                var translucent = new XSolidBrush(XColor.FromArgb(128, 0, 0, 160));
                gfx.DrawRectangle(translucent, 150, 170, 200, 60);
            }
        }

        return Bytes(document);
    }

    /// <summary>
    /// A page set in a face with PostScript outlines, claiming PDF/A-2b.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CFF outlines take a different path through the writer from every other document here: the
    /// font is embedded whole because it cannot be subsetted, the descendant is a Type 0 CIDFont
    /// rather than a Type 2, and so it must carry <em>no</em> <c>/CIDToGIDMap</c> where the others
    /// must carry one. That branch had nothing exercising it.
    /// </para>
    /// <para>
    /// PDF/A-2b rather than 1b, and not by choice: embedding CFF writes a <c>/FontFile3</c> with
    /// <c>/Subtype /OpenType</c>, which arrives in PDF 1.6, and PDF/A-1 is defined against PDF 1.4 —
    /// so the writer refuses that combination outright. Which settles the question this document was
    /// added to ask. Clause 6.3.5, the one wanting a <c>/CIDSet</c> on a subset CIDFont, is PDF/A-1's
    /// alone, and a CFF font cannot appear in a PDF/A-1 document from this library at all.
    /// </para>
    /// </remarks>
    private static byte[] Cff()
    {
        var document = new PdfDocument();
        document.Info.Title = "Conformance corpus: PostScript outlines";
        document.Info.Author = "PdfPinata conformance corpus";

        document.Options.Conformance = PdfAConformance.PdfA2B;

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont(CffFontResolver.Family, 14, XFontStyle.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));

            gfx.DrawString("PostScript outlines, embedded whole", font, XBrushes.Black, 50, 70);
            gfx.DrawString("static void Main(string[] args)", font, XBrushes.Black, 50, 100);
        }

        return Bytes(document);
    }

    /// <summary>
    /// The hybrid e-invoice shape: a readable page for a person and the same invoice as XML for a
    /// machine, associated with the document rather than merely carried inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what PDF/A-3 exists for, and the part a validator is worth most on — an associated
    /// file has to carry an <c>/AFRelationship</c>, be reachable from the catalog's <c>/AF</c>, and
    /// appear in the embedded-files name tree, and getting two of the three right produces a file
    /// that opens perfectly and conforms to nothing.
    /// </para>
    /// <para>
    /// Built through <see cref="FacturXInvoice"/>, which adds the second thing only a validator can
    /// settle: the XMP extension schema. PDF/A holds every property in the metadata packet to a
    /// schema the file predefines or describes, and the invoice namespace is nobody's predefined
    /// schema — so a packet naming <c>fx:DocumentType</c> without declaring it fails for its
    /// metadata rather than for its invoice. Nothing in the unit tests can settle whether the
    /// declaration is the shape PDF/A asks for; this document is how that gets checked.
    /// </para>
    /// <para>
    /// The XML is a plausible skeleton rather than real Factur-X. Nothing here validates against
    /// EN 16931 and nothing here could, so the profile the metadata names is a claim about the
    /// bytes attached and not a finding about them — the PDF side is what this document is for,
    /// and it is the only side veraPDF reads.
    /// </para>
    /// </remarks>
    private static byte[] Invoice()
    {
        var document = new PdfDocument();
        document.Info.Title = "Conformance corpus: invoice with an associated file";
        document.Info.Author = "PdfPinata conformance corpus";

        // Nothing about conformance is set here at all: attaching the invoice claims PDF/A-3, the
        // only profile that may carry one, and the output intent an RGB document needs comes with
        // the claim.

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawString("Invoice 2026-0042", new XFont("Arial", 20), XBrushes.Black, 50, 70);
            gfx.DrawString("Total due: 240.00 EUR", new XFont("Arial", 14), XBrushes.Black, 50, 110);
        }

        const string invoice =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<Invoice><ID>2026-0042</ID><Total currency=\"EUR\">240.00</Total></Invoice>\n";

        new FacturXInvoice(Encoding.UTF8.GetBytes(invoice)).AttachTo(document);

        return Bytes(document);
    }

    /// <summary>
    /// A tagged document rendered through PinataLayout, making whichever accessibility or archival
    /// claim <paramref name="claim"/> asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape that most needs an outside opinion. <c>PdfUaValidator</c> holds a document to the
    /// rules that can be settled by looking at it, and says in its own remarks what it cannot reach —
    /// the largest being that no content sits outside the structure tree, which needs a content-stream
    /// pass it does not make. That is exactly what veraPDF does make.
    /// </para>
    /// <para>
    /// It carries a heading, body text, a list, a table with a heading row, a hyperlink and a
    /// footnote, because each of those is a different corner of the tagger. The footnote is the
    /// reason this document exists: its <c>/Note</c> is built where the mark is drawn and filled in
    /// from the foot of the page, and the nesting that produces — a <c>/Lbl</c> and body paragraphs
    /// inside an inline-level <c>/Note</c> — is a reading of the standard that no test here can
    /// settle.
    /// </para>
    /// <para>
    /// An A-level archival claim is the archival rules of its part plus these same tagging rules, so
    /// one document shape now serves both PDF/UA and the three <c>A</c> levels — passed
    /// <paramref name="claim"/> rather than copied out per claim.
    /// </para>
    /// </remarks>
    private static byte[] Tagged(Action<PdfDocument> claim)
    {
        var document = new Document();
        var section = document.AddSection();

        section.AddParagraph("Statement of account", "Heading1");
        section.AddParagraph(
            "Amounts are shown in pounds sterling and include value added tax where it applies.");

        var cited = section.AddParagraph("The rate applied to this account changed in April.");
        cited.AddFootnote("Set by the schedule in force on the date of issue.");

        var bulleted = section.AddParagraph("Payable by bank transfer", "List");
        bulleted.Format.ListInfo = new ListInfo { ListType = ListType.BulletList1 };
        var alsoBulleted = section.AddParagraph("Payable by direct debit", "List");
        alsoBulleted.Format.ListInfo = new ListInfo { ListType = ListType.BulletList1 };

        var table = section.AddTable();
        table.AddColumn(Unit.FromCentimeter(6));
        table.AddColumn(Unit.FromCentimeter(4));

        var heading = table.AddRow();
        heading.HeadingFormat = true;
        heading.Cells[0].AddParagraph("Description");
        heading.Cells[1].AddParagraph("Amount");

        var row = table.AddRow();
        row.Cells[0].AddParagraph("Standing charge");
        row.Cells[1].AddParagraph("40.00");

        var linking = section.AddParagraph("Terms are published at ");
        var link = linking.AddHyperlink("https://example.org/terms", HyperlinkType.Web);
        link.AddText("example.org/terms");

        var renderer = new PdfDocumentRenderer(true)
        {
            Document = document,
            TagContent = true,
            Language = "en-GB"
        };

        renderer.RenderDocument();

        renderer.PdfDocument.Info.Title = "Statement of account";
        renderer.PdfDocument.Info.Author = "PdfPinata conformance corpus";
        claim(renderer.PdfDocument);

        return Bytes(renderer.PdfDocument);
    }

    private static byte[] Bytes(PdfDocument document)
    {
        using var stream = new System.IO.MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}
