using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
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
    private static PdfDocument OnePage(int version = 14)
    {
        var document = new PdfDocument { Version = version };
        _ = document.AddPage();
        return document;
    }

    [Fact]
    public void AColouredEntryIsWrittenWithItsColour()
    {
        var document = OnePage();
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var reopened = document.Reopened();

        reopened.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        reopened.Outlines[0].TextColor.R.Should().Be(255);
    }

    [Fact]
    public void AColourSetOnAnEntryOfADocumentReadFromAFileIsWritten()
    {
        var document = OnePage(version: 17);
        document.Outlines.Add("Plain", document.Pages[0], true);
        using var once = document.Reopened();

        once.Outlines[0].TextColor = XColors.Blue;
        using var twice = once.Reopened();

        twice.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        twice.Outlines[0].TextColor.B.Should().Be(255);
    }

    [Fact]
    public void AColourSurvivesAReadAndAnotherSave()
    {
        var document = OnePage();
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var once = document.Reopened();
        using var twice = once.Reopened();

        twice.Outlines[0].Elements.ContainsKey("/C").Should().BeTrue();
        twice.Outlines[0].TextColor.R.Should().Be(255);
    }

    [Theory]
    [InlineData(127, 64, 1)]
    [InlineData(254, 128, 3)]
    public void EveryComponentComesBackAsTheLevelItWasWrittenAs(int red, int green, int blue)
    {
        // A component goes out as a fraction of 255 at limited precision - 127 as 0.4980392 - and
        // comes back a hair under the level it was. Truncating that answered 126.
        var document = OnePage();
        var colour = XColor.FromArgb(red, green, blue);
        document.Outlines.Add(new PdfOutline("Colour", document.Pages[0], true, PdfOutlineStyle.Regular, colour));

        using var once = document.Reopened();
        using var twice = once.Reopened();

        foreach (var read in new[] { once.Outlines[0].TextColor, twice.Outlines[0].TextColor })
            ((int)read.R, (int)read.G, (int)read.B).Should().Be((red, green, blue));
    }

    [Fact]
    public void AComponentOutsideTheUnitRangeIsClampedRatherThanRefused()
    {
        // Another writer's file, which this library would never write: the components are edited in
        // the saved bytes, at the same length so the cross-reference offsets still hold.
        var document = OnePage();
        document.Outlines.Add(new PdfOutline("Loud", document.Pages[0], true, PdfOutlineStyle.Regular,
            XColor.FromArgb(127, 127, 127)));
        using var stream = new MemoryStream();
        document.Save(stream, false);
        var text = Encoding.Latin1.GetString(stream.ToArray());
        text.Should().Contain("0.498 0.498 0.498");
        var edited = Encoding.Latin1.GetBytes(text.Replace("0.498 0.498 0.498", "1.498 -.498 0.498"));

        using var reopened = Pdf.IO.PdfReader.Open(new MemoryStream(edited), Pdf.IO.PdfDocumentOpenMode.Modify);

        var read = reopened.Outlines[0].TextColor;
        ((int)read.R, (int)read.G, (int)read.B).Should().Be((255, 0, 127));
    }

    [Fact]
    public void AColouredEntryInADocumentOlderThanPdf14IsWrittenWithoutAColour()
    {
        var document = OnePage(version: 13);
        document.Outlines.Add(new PdfOutline("Red", document.Pages[0], true, PdfOutlineStyle.Regular, XColors.Red));

        using var reopened = document.Reopened();

        reopened.Outlines[0].Elements.ContainsKey("/C").Should().BeFalse();
    }
}
