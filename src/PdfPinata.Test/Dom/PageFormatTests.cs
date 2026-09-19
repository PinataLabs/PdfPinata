using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   PageFormat named twelve of the sizes a section can be set to and left the rest to PageWidth
///   and PageHeight. These measure what it names now: the ISO and DIN sheets against the whole
///   millimetres that define them, and the North American and traditional sheets against the whole
///   inches that define theirs.
/// </summary>
public class PageFormatTests
{
    [Theory]
    // ISO 216 A series.
    [InlineData(PageFormat.A0, 841, 1189)]
    [InlineData(PageFormat.A1, 594, 841)]
    [InlineData(PageFormat.A2, 420, 594)]
    [InlineData(PageFormat.A3, 297, 420)]
    [InlineData(PageFormat.A4, 210, 297)]
    [InlineData(PageFormat.A5, 148, 210)]
    [InlineData(PageFormat.A6, 105, 148)]
    [InlineData(PageFormat.A7, 74, 105)]
    [InlineData(PageFormat.A8, 52, 74)]
    [InlineData(PageFormat.A9, 37, 52)]
    [InlineData(PageFormat.A10, 26, 37)]
    // DIN 476 oversizes.
    [InlineData(PageFormat.TwoA0, 1189, 1682)]
    [InlineData(PageFormat.FourA0, 1682, 2378)]
    // ISO 216 B series.
    [InlineData(PageFormat.B0, 1000, 1414)]
    [InlineData(PageFormat.B1, 707, 1000)]
    [InlineData(PageFormat.B2, 500, 707)]
    [InlineData(PageFormat.B3, 353, 500)]
    [InlineData(PageFormat.B4, 250, 353)]
    [InlineData(PageFormat.B5, 176, 250)]
    [InlineData(PageFormat.B6, 125, 176)]
    [InlineData(PageFormat.B7, 88, 125)]
    [InlineData(PageFormat.B8, 62, 88)]
    [InlineData(PageFormat.B9, 44, 62)]
    [InlineData(PageFormat.B10, 31, 44)]
    [InlineData(PageFormat.JISB5, 182, 257)]
    // ISO 269 C series, the envelopes.
    [InlineData(PageFormat.C0, 917, 1297)]
    [InlineData(PageFormat.C1, 648, 917)]
    [InlineData(PageFormat.C2, 458, 648)]
    [InlineData(PageFormat.C3, 324, 458)]
    [InlineData(PageFormat.C4, 229, 324)]
    [InlineData(PageFormat.C5, 162, 229)]
    [InlineData(PageFormat.C6, 114, 162)]
    [InlineData(PageFormat.C7, 81, 114)]
    [InlineData(PageFormat.C8, 57, 81)]
    [InlineData(PageFormat.C9, 40, 57)]
    [InlineData(PageFormat.C10, 28, 40)]
    // ISO 217 untrimmed stock.
    [InlineData(PageFormat.RA0, 860, 1220)]
    [InlineData(PageFormat.RA1, 610, 860)]
    [InlineData(PageFormat.RA2, 430, 610)]
    [InlineData(PageFormat.RA3, 305, 430)]
    [InlineData(PageFormat.RA4, 215, 305)]
    [InlineData(PageFormat.RA5, 153, 215)]
    [InlineData(PageFormat.SRA0, 900, 1280)]
    [InlineData(PageFormat.SRA1, 640, 900)]
    [InlineData(PageFormat.SRA2, 450, 640)]
    [InlineData(PageFormat.SRA3, 320, 450)]
    [InlineData(PageFormat.SRA4, 225, 320)]
    public void AFormatDefinedInMillimetresMeasuresThoseMillimetres(
        PageFormat format, double width, double height)
    {
        PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);

        pageWidth.Millimeter.Should().BeApproximately(width, 0.001, $"{format} is {width} mm wide");
        pageHeight.Millimeter.Should().BeApproximately(height, 0.001, $"{format} is {height} mm high");
    }

    [Theory]
    // North American sizes.
    [InlineData(PageFormat.Letter, 8.5, 11)]
    [InlineData(PageFormat.Legal, 8.5, 14)]
    [InlineData(PageFormat.Ledger, 17, 11)]
    [InlineData(PageFormat.Tabloid, 11, 17)]
    [InlineData(PageFormat.P11x17, 11, 17)]
    [InlineData(PageFormat.Executive, 7.25, 10.5)]
    [InlineData(PageFormat.GovernmentLetter, 8, 10.5)]
    [InlineData(PageFormat.Statement, 5.5, 8.5)]
    [InlineData(PageFormat.STMT, 5.5, 8.5)]
    [InlineData(PageFormat.Folio, 8.5, 13)]
    [InlineData(PageFormat.Size10x14, 10, 14)]
    // Traditional British sizes.
    [InlineData(PageFormat.Quarto, 8, 10)]
    [InlineData(PageFormat.Foolscap, 8, 13)]
    [InlineData(PageFormat.Post, 15.5, 19.25)]
    [InlineData(PageFormat.Crown, 20, 15)]
    [InlineData(PageFormat.LargePost, 16.5, 21)]
    [InlineData(PageFormat.Demy, 17.5, 22)]
    [InlineData(PageFormat.Medium, 18, 23)]
    [InlineData(PageFormat.Royal, 20, 25)]
    [InlineData(PageFormat.Elephant, 23, 28)]
    [InlineData(PageFormat.DoubleDemy, 23.5, 35)]
    [InlineData(PageFormat.QuadDemy, 35, 45)]
    public void AFormatDefinedInInchesMeasuresThoseInches(
        PageFormat format, double width, double height)
    {
        PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);

        pageWidth.Inch.Should().BeApproximately(width, 0.001, $"{format} is {width} inch wide");
        pageHeight.Inch.Should().BeApproximately(height, 0.001, $"{format} is {height} inch high");
    }

    /// <summary>
    ///   A format the switch does not answer for falls through to zero by zero rather than
    ///   throwing, so a member added to the enumeration and forgotten here would make pages of no
    ///   size instead of failing. This is what notices.
    /// </summary>
    [Fact]
    public void EveryNamedFormatHasASize()
    {
        var named = Enum.GetValues<PageFormat>();

        foreach (var format in named)
        {
            PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);

            pageWidth.Point.Should().BePositive($"{format} has a width");
            pageHeight.Point.Should().BePositive($"{format} has a height");
        }
    }

    /// <summary>
    ///   B5 measured the JIS sheet while it was the only B format here. The series it now sits in
    ///   is the ISO one, and the sheet it used to measure is named JISB5.
    /// </summary>
    [Fact]
    public void B5IsTheIsoSheetAndTheJisOneIsNamedSeparately()
    {
        PageSetup.GetPageSize(PageFormat.B5, out var isoWidth, out var isoHeight);
        PageSetup.GetPageSize(PageFormat.JISB5, out var jisWidth, out var jisHeight);

        isoWidth.Millimeter.Should().BeApproximately(176, 0.001);
        isoHeight.Millimeter.Should().BeApproximately(250, 0.001);
        jisWidth.Millimeter.Should().BeApproximately(182, 0.001);
        jisHeight.Millimeter.Should().BeApproximately(257, 0.001);

        // Halving B4 across its longer side gives B5, which is what makes the series a series.
        PageSetup.GetPageSize(PageFormat.B4, out var b4Width, out var b4Height);
        isoWidth.Millimeter.Should().BeApproximately(b4Height.Millimeter / 2, 1);
        isoHeight.Millimeter.Should().BeApproximately(b4Width.Millimeter, 0.001);
    }

    /// <summary>
    ///   Sizes defined in inches are carried as points, as they were before the rest of them were
    ///   named. A Unit remembers what it was built from, so building Letter from inches would write
    ///   it into DDL as 8.5in where every existing file has 612.
    /// </summary>
    [Fact]
    public void ASizeDefinedInInchesIsStillCarriedAsPoints()
    {
        PageSetup.GetPageSize(PageFormat.Letter, out var pageWidth, out var pageHeight);

        pageWidth.ToString().Should().Be("612");
        pageHeight.ToString().Should().Be("792");
    }

    [Fact]
    public void AFormatSurvivesAWriteAndARead()
    {
        var document = new Document();
        document.AddSection().PageSetup.PageFormat = PageFormat.C5;

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        reread.Sections[0].As<Section>().PageSetup.PageFormat.Should().Be(PageFormat.C5);
    }

    [Fact]
    public void AValueNamingNoFormatIsRefused()
    {
        var pageSetup = new Document().AddSection().PageSetup;

        Action set = () => pageSetup.PageFormat = (PageFormat)999;

        set.Should().Throw<ArgumentException>();
    }
}
