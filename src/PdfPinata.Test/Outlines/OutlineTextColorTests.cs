using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   How <see cref="PdfOutline.TextColor"/> is written. It is the outline item's <c>/C</c> entry,
///   which ISO 32000-1 Table 153 adds in PDF 1.4 - and which used to be gated on the catalog's
///   version string, "1.3" for every document read from a file whatever its header said, so a colour
///   set on an entry of an opened document was silently never written.
/// </summary>
public class OutlineTextColorTests
{
    static PdfDocument OnePage(int version = 14)
    {
        var document = new PdfDocument { Version = version };
        _ = document.AddPage();
        return document;
    }

    static PdfDocument SaveAndOpen(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        // Fully qualified: PdfPinata.Test carries a PdfReader of its own, which wins here.
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);
    }

    [Fact]
    public void AColouredEntryIsWrittenWithItsColour()
    {
        var document = OnePage();
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var reopened = SaveAndOpen(document);

        reopened.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        reopened.Outlines[0].TextColor.R.Should().Be(255);
    }

    [Fact]
    public void AColourSetOnAnEntryOfADocumentReadFromAFileIsWritten()
    {
        var document = OnePage(version: 17);
        document.Outlines.Add("Plain", document.Pages[0], true);
        using var once = SaveAndOpen(document);

        once.Outlines[0].TextColor = XColors.Blue;
        using var twice = SaveAndOpen(once);

        twice.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        twice.Outlines[0].TextColor.B.Should().Be(255);
    }

    [Fact]
    public void AColourSurvivesAReadAndAnotherSave()
    {
        var document = OnePage();
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var once = SaveAndOpen(document);
        using var twice = SaveAndOpen(once);

        twice.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        twice.Outlines[0].TextColor.R.Should().Be(255);
    }

    [Fact]
    public void AColouredEntryInADocumentOlderThanPdf14IsWrittenWithoutAColour()
    {
        var document = OnePage(version: 13);
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var reopened = SaveAndOpen(document);

        reopened.Outlines[0].Elements.ContainsKey("/C").Should().BeFalse();
    }
}
