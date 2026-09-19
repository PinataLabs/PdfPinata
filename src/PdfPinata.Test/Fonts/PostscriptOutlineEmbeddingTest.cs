using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
/// A font with PostScript (CFF) outlines has no 'glyf' and no 'loca' table, so it cannot be
/// subsetted. The simple-font path used to walk into that and throw NullReferenceException; the
/// CID path survived by embedding the whole OTTO file as '/FontFile2' under a '/CIDFontType2'
/// descendant, which describes a TrueType program and is therefore a lie about what is in the
/// stream. Such a font is now embedded whole, as '/FontFile3' with a subtype of '/OpenType'.
/// </summary>
[Collection(RasterizingCollection.Name)]
public class PostscriptOutlineEmbeddingTest
{
    [Fact]
    public void AFontWithPostscriptOutlinesIsEmbeddedAsAnOpenTypeProgram()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode);

        var descriptor = FontDescriptorOf(saved);

        descriptor.Elements.ContainsKey("/FontFile3").Should()
            .BeTrue("PostScript outlines are an OpenType program, not a TrueType one");
        descriptor.Elements.ContainsKey("/FontFile2").Should()
            .BeFalse("'/FontFile2' is defined as a TrueType font program");

        var program = Resolve(descriptor.Elements["/FontFile3"]);
        program.Elements.GetName("/Subtype").Should().Be("/OpenType");

        // '/Length1' belongs to a TrueType program; '/Subtype' takes its place here.
        program.Elements.ContainsKey("/Length1").Should().BeFalse();
    }

    /// <summary>
    /// PostScript outlines are not subsetted - that would mean rebuilding the charstrings and
    /// the subroutine sets - so the stream has to be the font, whole and unaltered. Checking
    /// the bytes is what says so; the dictionary keys only say what was intended.
    /// </summary>
    [Fact]
    public void TheEmbeddedProgramIsTheFontFileItself()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode);

        var program = Resolve(FontDescriptorOf(saved).Elements["/FontFile3"]);
        var expected = File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "SourceCodePro-Regular.otf"));

        program.Stream.UnfilteredValue.Should().Equal(expected);
    }

    [Fact]
    public void TheDescendantOfAPostscriptFontIsACidFontType0()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode);

        DescendantFontsOf(saved).Single()
            .Elements.GetName("/Subtype").Should().Be("/CIDFontType0",
                "CIDFontType2 means glyf outlines, which this font does not have");
    }

    [Fact]
    public void EmbeddingAnOpenTypeProgramRaisesTheDocumentVersion()
    {
        // '/Subtype /OpenType' in a font program stream arrives in PDF 1.6.
        DrawAndReopen(PdfFontEncoding.Unicode).Version.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void ASimpleFontWithPostscriptOutlinesEmbedsRatherThanThrowing()
    {
        // The path that used to raise NullReferenceException on a missing 'loca' table.
        var saved = DrawAndReopen(PdfFontEncoding.WinAnsi);

        FontDescriptorOf(saved).Elements.ContainsKey("/FontFile3").Should().BeTrue();
    }

    /// <summary>
    /// A font embedded whole does not wear the tag that says it was cut down.
    /// </summary>
    /// <remarks>
    /// ISO 32000-1 9.6.4 makes the six-letter 'ABCDEF+' prefix a statement of fact: it marks a
    /// program holding only some of the face's glyphs, and exists so that two subsets of one face -
    /// same name, different glyphs - cannot be mistaken for each other. This font is embedded
    /// entire, because CFF outlines cannot be subsetted, so it has nothing to be distinguished from
    /// and the tag claims something untrue of it. No validator objects, which is why this went
    /// unnoticed; a tool merging two documents is nonetheless entitled to believe two
    /// differently-tagged programs differ, and to keep both.
    /// </remarks>
    [Fact]
    public void AFontEmbeddedWholeIsNotNamedAsASubset()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode);

        var name = FontDescriptorOf(saved).Elements.GetName("/FontName");

        name.Should().NotBeEmpty();
        name.Should().NotContain("+", "nothing was left out, so there is no subset to tag");

        // The descendant and the descriptor have to agree; they are the same name written twice.
        DescendantFontsOf(saved).Single().Elements.GetName("/BaseFont").Should().Be(name);
    }

    /// <summary>
    /// TrueType fonts must be unaffected: still subsetted, still tagged, still '/FontFile2'.
    /// </summary>
    [Fact]
    public void ASubsettedFontIsStillNamedAsOne()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode, "Arial");

        // Six upper-case letters and a plus, which is the form the standard fixes.
        FontDescriptorOf(saved).Elements.GetName("/FontName")
            .Should().MatchRegex(@"^/[A-Z]{6}\+");
    }

    [Fact]
    public void ATrueTypeFontIsStillSubsettedIntoAFontFile2()
    {
        var saved = DrawAndReopen(PdfFontEncoding.Unicode, "Arial");

        var descriptor = FontDescriptorOf(saved);

        descriptor.Elements.ContainsKey("/FontFile2").Should().BeTrue();
        descriptor.Elements.ContainsKey("/FontFile3").Should().BeFalse();

        DescendantFontsOf(saved).Single()
            .Elements.GetName("/Subtype").Should().Be("/CIDFontType2");

        // The point of subsetting: what goes in is smaller than the font it came from.
        var program = Resolve(descriptor.Elements["/FontFile2"]);
        long embedded = program.Elements.GetInteger("/Length1");
        var source = new FileInfo(
            PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf")).Length;

        embedded.Should().BeLessThan(source);
    }

    /// <summary>
    /// The bytes being right does not make the file readable: the old output embedded this same
    /// font correctly enough that lenient viewers coped, and described it as something it was
    /// not, which strict readers reject. Ghostscript is the strict reader on hand.
    /// </summary>
    /// <remarks>
    /// This asserts that the page is drawn, not which glyphs are on it: Ghostscript substitutes
    /// silently for a font it cannot use, so ink alone does not prove the embedded program was
    /// the one drawn from. What it does prove is that a reader that refuses malformed font
    /// dictionaries accepts this one and renders the page - which is what the old output failed
    /// to do. The identity of the program is settled above, against its bytes.
    /// </remarks>
    [GoldenImageFact]
    public void GhostscriptRendersAPageCarryingAnEmbeddedOpenTypeProgram()
    {
        using var rendered = PdfHelper.Rasterize(Draw(PdfFontEncoding.Unicode)).ImageCollection;

        using var page = rendered.Single().Clone();
        page.ColorFuzz = new Percentage(20);
        page.Trim();

        page.Width.Should().BeGreaterThan(0, "the page must not come out blank");
        page.Height.Should().BeGreaterThan(0, "the page must not come out blank");
    }

    /// <summary>
    /// A simple font with CFF outlines ends up as a '/Subtype /TrueType' dictionary whose
    /// descriptor carries a '/FontFile3' of subtype '/OpenType'. That pairing looks contradictory
    /// - the traditional one for CFF outlines in a simple font is '/Type1' with '/Type1C' - so it
    /// is pinned here and put in front of the strict reader, rather than argued about from the
    /// specification alone. The CID path is covered above; this is the other half.
    /// </summary>
    [GoldenImageFact]
    public void GhostscriptRendersASimpleFontCarryingPostscriptOutlines()
    {
        var saved = DrawAndReopen(PdfFontEncoding.WinAnsi);

        // The exact combination under test.
        SimpleFontOf(saved).Elements.GetName("/Subtype").Should().Be("/TrueType");
        Resolve(FontDescriptorOf(saved).Elements["/FontFile3"])
            .Elements.GetName("/Subtype").Should().Be("/OpenType");

        using var rendered = PdfHelper.Rasterize(Draw(PdfFontEncoding.WinAnsi)).ImageCollection;

        using var page = rendered.Single().Clone();
        page.ColorFuzz = new Percentage(20);
        page.Trim();

        page.Width.Should().BeGreaterThan(0, "the page must not come out blank");
        page.Height.Should().BeGreaterThan(0, "the page must not come out blank");
    }

    /// <summary>
    /// Discovery globbed '*.ttf' alone, so an .otf was invisible to the shipped resolver no
    /// matter what the embedding code could do with one.
    /// </summary>
    [Fact]
    public void AnOtfIsDiscoveredAndResolvedByTheShippedResolver()
    {
        var resolver = new Probe();
        resolver.SetupFontsFiles(new[]
        {
            PathHelper.GetInstance().GetAssetPath("Fonts", "SourceCodePro-Regular.otf")
        });

        var info = resolver.ResolveTypeface("Source Code Pro", false, false);

        info.Should().NotBeNull();
        info.FaceName.Should().Be("SourceCodePro-Regular.otf");

        // 'OTTO' - the signature that says the outlines are PostScript rather than TrueType.
        resolver.GetFont(info.FaceName).Take(4).Should().Equal((byte)'O', (byte)'T', (byte)'T', (byte)'O');
    }

    private sealed class Probe : SkiaFontResolver
    {
        public Probe()
        {
            NullIfFontNotFound = true;
        }
    }

    private static PdfDocument Draw(PdfFontEncoding encoding,
        string familyName = PinnedFontResolver.CffFamilyName)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont(familyName, 12, XFontStyle.Regular, new XPdfFontOptions(encoding));
            gfx.DrawString("Embedding check", font, XBrushes.Black, new XPoint(20, 40));
        }

        return document;
    }

    private static PdfDocument DrawAndReopen(PdfFontEncoding encoding,
        string familyName = PinnedFontResolver.CffFamilyName)
    {
        byte[] saved;
        using (var stream = new MemoryStream())
        {
            Draw(encoding, familyName).Save(stream, false);
            saved = stream.ToArray();
        }

        // The returned document reads lazily through its lexer, so it is opened over a stream
        // that outlives this method rather than one disposed on the way out.
        return Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
    }

    private static PdfDictionary FontDescriptorOf(PdfDocument document)
    {
        return document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(d => d.Elements.GetName("/Type") == "/FontDescriptor");
    }

    /// <summary>
    /// The font dictionary of a simple font: a '/Font' that is not a CIDFont descendant and not
    /// the '/Type0' parent of one.
    /// </summary>
    private static PdfDictionary SimpleFontOf(PdfDocument document)
    {
        return document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/Font")
            .Single(d => !d.Elements.ContainsKey("/CIDSystemInfo")
                         && !d.Elements.ContainsKey("/DescendantFonts"));
    }

    private static PdfDictionary[] DescendantFontsOf(PdfDocument document)
    {
        return document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/Font")
            .Where(d => d.Elements.ContainsKey("/CIDSystemInfo"))
            .ToArray();
    }

    private static PdfDictionary Resolve(PdfItem item)
    {
        return (PdfDictionary)(item is PdfReference reference ? reference.Value : item);
    }
}
