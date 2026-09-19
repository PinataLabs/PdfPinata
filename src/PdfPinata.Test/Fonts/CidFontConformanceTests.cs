using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What a CIDFont dictionary has to say about itself before a validator will accept the document
///   around it.
/// </summary>
/// <remarks>
///   Both of these were found by veraPDF rather than by anything here, which is the argument for
///   having it — see <c>docs/specs/verapdf-validation.md</c>. Neither changes what any reader draws,
///   and that is exactly why nothing noticed: a viewer applies the same defaults the file was
///   relying on, and only a validator objects to the file relying on them.
/// </remarks>
public class CidFontConformanceTests
{
    [Fact]
    public void ADescendantFontSaysItsGlyphMappingIsTheIdentity()
    {
        // ISO 32000-1 Table 117 makes /CIDToGIDMap optional with a default of /Identity, and PDF/A
        // and PDF/UA both require it written out. This library writes glyph indices as the character
        // codes, so the identity is the truth - it was simply never said.
        var cidFont = DescendantFontOf(Saved(_ => { }));

        cidFont.Elements.GetName("/Subtype").Should().Be("/CIDFontType2",
            "Liberation Sans has glyf outlines, so the descendant is a Type 2 CIDFont");
        cidFont.Elements.GetName("/CIDToGIDMap").Should().Be("/Identity");
    }

    [Fact]
    public void APdfA1DocumentSaysWhichGlyphsItsSubsetHolds()
    {
        // PDF/A-1 clause 6.3.5 wants a /CIDSet on every subset CIDFont, and every CID font this
        // library writes is a subset by name.
        var descriptor = FontDescriptorOf(Saved(Conforming));

        var set = descriptor.Elements.GetDictionary("/CIDSet");

        set.Should().NotBeNull("a PDF/A-1 document has to say which CIDs its subset holds");
        set.Stream.Value.Should().NotBeEmpty();

        // The highest bit of the first byte is CID 0. It is .notdef, it is in every font program
        // whether or not a page drew it, and the set describes the program rather than the page.
        (set.Stream.Value[0] & 0x80).Should().NotBe(0, "every font program holds .notdef");
    }

    [Fact]
    public void ADocumentClaimingNothingCarriesNoCidSet()
    {
        // PDF/A-2 dropped the requirement as redundant, so this is bytes that exactly one profile
        // has a use for. Writing it into every document would be a cost paid by every caller for a
        // rule none of them invoked.
        var descriptor = FontDescriptorOf(Saved(_ => { }));

        descriptor.Elements.GetDictionary("/CIDSet").Should().BeNull();
    }

    [Fact]
    public void APdfA2DocumentCarriesNoCidSetEither()
    {
        var descriptor = FontDescriptorOf(Saved(document =>
        {
            Conforming(document);
            document.Options.Conformance = PdfAConformance.PdfA2B;
        }));

        descriptor.Elements.GetDictionary("/CIDSet").Should().BeNull(
            "PDF/A-2 dropped clause 6.3.5, so the entry has no reader left");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Everything a document needs before it may claim PDF/A-1b.</summary>
    static void Conforming(PdfDocument document)
    {
        document.Options.Conformance = PdfAConformance.PdfA1B;

        // Not a real profile. Nothing in this library parses one, and these tests are about a font
        // dictionary rather than about colour - the conformance corpus builds a real profile,
        // because a validator does parse it.
        document.Options.OutputIntentIccProfile = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        document.Options.OutputIntentIdentifier = "sRGB IEC61966-2.1";
        document.Info.Title = "CID font conformance";
    }

    static PdfDocument Saved(Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        arrange(document);

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            // Unicode encoding is what produces a Type 0 font with a CID descendant; the ANSI path
            // writes a simple TrueType font and has no descendant at all.
            var font = new XFont("Arial", 20, XFontStyle.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));
            gfx.DrawString("Descendant", font, XBrushes.Black, 20, 40);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return Reader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    /// <summary>The one descendant CIDFont in a document that drew one string.</summary>
    static PdfDictionary DescendantFontOf(PdfDocument document)
    {
        var type0 = document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(dictionary => dictionary.Elements.GetName("/Subtype") == "/Type0");

        var descendants = type0.Elements.GetArray("/DescendantFonts");
        descendants.Should().NotBeNull("a Type 0 font is nothing without its descendant");

        return (PdfDictionary)((PdfReference)descendants.Elements[0]).Value;
    }

    static PdfDictionary FontDescriptorOf(PdfDocument document)
        => DescendantFontOf(document).Elements.GetDictionary("/FontDescriptor");
}
