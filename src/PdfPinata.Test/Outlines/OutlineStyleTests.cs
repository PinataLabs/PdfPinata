using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   How <see cref="PdfOutline.Style"/> is written. It is the outline item's <c>/F</c> entry,
///   which ISO 32000-1 Table 153 adds in PDF 1.4 and gives a default of 0, so a regular entry and
///   an entry in an older document carry none - and the setter used to write it unconditionally.
/// </summary>
public class OutlineStyleTests
{
    private static PdfDocument OnePage(int version = 14)
    {
        var document = new PdfDocument { Version = version };
        _ = document.AddPage();
        return document;
    }

    [Fact]
    public void ARegularEntryIsWrittenWithoutAStyle()
    {
        var document = OnePage();
        document.Outlines.Add("Regular", document.Pages[0], true, PdfOutlineStyle.Regular);

        using var reopened = document.Reopened();

        reopened.Outlines[0].Elements.ContainsKey("/F").Should().BeFalse();
        reopened.Outlines[0].Style.Should().Be(PdfOutlineStyle.Regular);
    }

    [Theory]
    [InlineData(PdfOutlineStyle.Italic, 1)]
    [InlineData(PdfOutlineStyle.Bold, 2)]
    [InlineData(PdfOutlineStyle.BoldItalic, 3)]
    public void AStyledEntryIsWrittenWithItsFlags(PdfOutlineStyle style, int flags)
    {
        var document = OnePage();
        document.Outlines.Add("Styled", document.Pages[0], true, style);

        using var reopened = document.Reopened();

        reopened.Outlines[0].Elements.GetInteger("/F").Should().Be(flags);
        reopened.Outlines[0].Style.Should().Be(style);
    }

    [Fact]
    public void AStyledEntryInADocumentOlderThanPdf14IsWrittenWithoutAStyle()
    {
        var document = OnePage(version: 13);
        var bold = document.Outlines.Add("Bold", document.Pages[0], true, PdfOutlineStyle.Bold);

        using var reopened = document.Reopened();

        reopened.Outlines[0].Elements.ContainsKey("/F").Should().BeFalse();
        // The document was only saved; the entry it was saved from still says what it was given.
        bold.Style.Should().Be(PdfOutlineStyle.Bold);
    }

    [Fact]
    public void AnEntryMadeRegularAgainLosesTheStyleItWasReadWith()
    {
        var document = OnePage();
        document.Outlines.Add("Bold", document.Pages[0], true, PdfOutlineStyle.Bold);
        using var once = document.Reopened();

        once.Outlines[0].Style.Should().Be(PdfOutlineStyle.Bold);
        once.Outlines[0].Style = PdfOutlineStyle.Regular;
        using var twice = once.Reopened();

        twice.Outlines[0].Elements.ContainsKey("/F").Should().BeFalse();
    }

    [Fact]
    public void AStyleSurvivesAReadAndAnotherSave()
    {
        var document = OnePage(version: 17);
        document.Outlines.Add("Italic", document.Pages[0], true, PdfOutlineStyle.Italic);

        using var once = document.Reopened();
        using var twice = once.Reopened();

        twice.Outlines[0].Style.Should().Be(PdfOutlineStyle.Italic);
        twice.Outlines[0].Elements.GetInteger("/F").Should().Be(1);
    }
}
