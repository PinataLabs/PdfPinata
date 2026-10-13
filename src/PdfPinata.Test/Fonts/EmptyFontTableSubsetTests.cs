using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   A face whose <c>cvt </c>, <c>fpgm</c> or <c>prep</c> table has no bytes. The subset copies
///   those three tables from the face as they are, so it used to copy an empty one too, and write
///   a table of length zero - the same fault as an all-blank <c>glyf</c>, which
///   <see cref="BlankGlyphSubsetTests"/> covers, reached from the face rather than from the text.
///   All three are optional, and an empty one says nothing, so the subset leaves it out.
/// </summary>
public class EmptyFontTableSubsetTests
{
    [Test]
    [Arguments("cvt ", 'Q')]
    [Arguments("fpgm", 'R')]
    [Arguments("prep", 'S')]
    public void AnEmptyOptionalTableOfTheFaceIsLeftOutOfTheSubset(string tag, char letter)
    {
        // A family, and a name inside the font, of each case's own: the library caches a face
        // under the name in its name table, and the resolver keeps the first bytes it is given.
        var family = "Empty " + tag.Trim() + " Probe";
        var original = new TrueTypeGlyphs(File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf")));
        PinnedFontResolver.Register(family,
            new TrueTypeGlyphs(original.WithTableEmptied(tag)).WithADistinctFontName(letter));

        var program = new TrueTypeGlyphs(EmbeddedSubsetOfPageDrawing(family));

        program.TableRecords.Should().NotContain(record => record.Tag == tag,
            "the face's '{0}' is empty, and an empty optional table is no table", tag);
        // One table fewer changes the count the search fields follow from, and every offset after
        // the table that went; and none of what is left may have no bytes.
        program.DirectoryProblems().Should().BeEmpty();
    }

    private static byte[] EmbeddedSubsetOfPageDrawing(string family)
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            var font = new XFont(family, 12, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
            gfx.DrawString("Ink", font, XBrushes.Black, new XPoint(20, 40));
        }

        var descriptor = document.Reopened().Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(d => d.Elements.GetName("/Type") == "/FontDescriptor");

        var program = descriptor.Elements["/FontFile2"];
        return ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
            .Stream.UnfilteredValue;
    }
}
