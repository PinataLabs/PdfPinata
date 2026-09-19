using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   A composite glyph is drawn from other glyphs, and those may be composite in their turn.
///   Subsetting has to carry the whole chain: a component left out is a shape the subset says
///   it draws and has nothing to draw it from. The closure used to expand only the glyphs it
///   was handed, so a component one step down went in without the glyphs it is made of.
/// </summary>
/// <remarks>
///   None of the fonts shipped with the tests has a composite glyph made of a composite glyph -
///   Liberation Sans has over a thousand composites and not one of them nests - so the font
///   under test is built here by pointing one component of an accented letter at a composite
///   glyph. Only the glyph a component names is changed, so the data keeps its length and every
///   other table still describes the file.
/// </remarks>
public class CompositeGlyphClosureTest
{
    private const string FamilyName = "Nested Composite Probe";

    [Fact]
    public void ASubsetCarriesTheGlyphsReachedThroughAComponentThatIsItselfComposite()
    {
        byte[] original = File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf"));
        var source = new TrueTypeGlyphs(original);

        int drawn = source.GlyphIndexOf('é');
        source.IsComposite(drawn).Should().BeTrue("the letter drawn has to be a composite glyph");

        int nested = ACompositeGlyphReachedOnlyThrough(source, drawn);
        int[] expected = source.ComponentsOf(nested);
        expected.Should().NotBeEmpty();

        byte[] patched = source.WithFirstComponentRepointed(drawn, nested);
        PinnedFontResolver.Register(FamilyName, new TrueTypeGlyphs(patched).WithADistinctFontName());

        var subset = new TrueTypeGlyphs(EmbeddedSubsetOfPageDrawing("é"));

        // The letter itself, and the composite it now names: both are reached in one step and
        // went in even before this was fixed.
        subset.LengthOf(drawn).Should().BeGreaterThan(0);
        subset.LengthOf(nested).Should().BeGreaterThan(0);

        // What the letter reaches only through that composite. Chosen below so that this is
        // the only way in: nothing else the letter names leads to them.
        foreach (int component in expected)
        {
            subset.LengthOf(component).Should().BeGreaterThan(0,
                "glyph {0} is what glyph {1} is drawn from, and glyph {1} is in the subset",
                component, nested);
        }
    }

    /// <summary>
    ///   A composite glyph whose own components are not reachable from the drawn glyph by any
    ///   other route, so that finding them in the subset says the closure followed the chain
    ///   rather than that something else brought them along.
    /// </summary>
    private static int ACompositeGlyphReachedOnlyThrough(TrueTypeGlyphs font, int drawn)
    {
        // Glyph 0 is added to every subset regardless.
        var reachedAnyway = new HashSet<int>(font.ComponentsOf(drawn)) { 0, drawn };

        return Enumerable.Range(1, font.NumGlyphs - 1)
            .Where(glyph => !reachedAnyway.Contains(glyph))
            .Where(font.IsComposite)
            .First(glyph => font.ComponentsOf(glyph)
                .All(component => !reachedAnyway.Contains(component)
                                  && component != glyph
                                  && font.LengthOf(component) > 0));
    }

    private static byte[] EmbeddedSubsetOfPageDrawing(string text)
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            var font = new XFont(FamilyName, 12, XFontStyle.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));
            gfx.DrawString(text, font, XBrushes.Black, new XPoint(20, 40));
        }

        byte[] saved;
        using (var stream = new MemoryStream())
        {
            document.Save(stream, false);
            saved = stream.ToArray();
        }

        PdfDocument reopened = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);

        PdfDictionary descriptor = reopened.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(d => d.Elements.GetName("/Type") == "/FontDescriptor");

        PdfItem program = descriptor.Elements["/FontFile2"];
        return ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
            .Stream.UnfilteredValue;
    }
}
