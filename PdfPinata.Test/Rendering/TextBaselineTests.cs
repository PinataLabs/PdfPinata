using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using PdfPinata.Test.IO;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   The helper the layout tests read positions with. PdfPinata writes text one way only,
///   so these documents are written by hand to put the rest of the ways through it.
/// </summary>
public class TextBaselineTests
{
    [Fact]
    public void TdMovesRelativeToTheLineBefore()
    {
        var page = PageDrawing("BT 50 700 Td (a) Tj 0 -12 Td (b) Tj 0 -12 Td (c) Tj ET");

        TextBaselines.Of(page).Should().Equal(700, 688, 676);
    }

    [Fact]
    public void TmMovesToAPlaceRatherThanByADistance()
    {
        var page = PageDrawing("BT 50 700 Td (a) Tj 1 0 0 1 50 400 Tm (b) Tj ET");

        TextBaselines.Of(page).Should().Equal(700, 400);
    }

    [Fact]
    public void TStarMovesDownByTheLeading()
    {
        var page = PageDrawing("BT 14 TL 50 700 Td (a) Tj T* (b) Tj T* (c) Tj ET");

        TextBaselines.Of(page).Should().Equal(700, 686, 672);
    }

    [Fact]
    public void TheQuoteOperatorsMoveDownALineBeforeShowingTheirText()
    {
        // ' is T* then Tj, and " is the same after setting the spacing. Text shown by either
        // therefore sits a line below the one before it, not on it.
        var page = PageDrawing("BT 14 TL 50 700 Td (a) Tj (b) ' 1 2 (c) \" ET");

        TextBaselines.Of(page).Should().Equal(700, 686, 672);
    }

    [Fact]
    public void TDSetsTheLeadingAsWellAsMoving()
    {
        // TD is Td with the leading set to the distance it moved down by, so the T* after it
        // moves by that same distance without a TL of its own.
        var page = PageDrawing("BT 50 700 Td 0 -20 TD (a) Tj T* (b) Tj ET");

        TextBaselines.Of(page).Should().Equal(680, 660);
    }

    [Fact]
    public void ContentSplitAcrossStreamsIsReadAsOne()
    {
        // The streams of a page are one stream broken up, and the break falls between tokens
        // rather than within one. Running them together with nothing in between would join the
        // token either side of it: the "0" ending one stream and the "12" beginning the next
        // would read as the single number 12, and the move they are the operands of is lost.
        var page = PageDrawingInParts("BT 50 700 Td (a) Tj 0", "12 Td (b) Tj ET");

        TextBaselines.Of(page).Should().Equal(700, 712);
    }

    static PdfPage PageDrawing(string content)
    {
        return PageDrawingInParts(content);
    }

    static PdfPage PageDrawingInParts(params string[] parts)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
        };

        var contents = parts.Length == 1
            ? "4 0 R"
            : "[" + string.Join(" ", Enumerable.Range(4, parts.Length).Select(n => n + " 0 R")) + "]";

        objects.Add("<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]" +
                    "/Resources<</Font<</F1 " + (4 + parts.Length) + " 0 R>>>>" +
                    "/Contents " + contents + ">>");

        foreach (var part in parts)
            objects.Add(RawPdf.Stream("", part));

        objects.Add("<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        var document = RawPdf.Build(objects);
        return Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify).Pages[0];
    }
}
