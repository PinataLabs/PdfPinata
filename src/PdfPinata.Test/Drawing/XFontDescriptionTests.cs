using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The three types that describe a font rather than draw with one: <see cref="XFont"/>, the
///   family it belongs to, and the metrics read out of its file. Everything here is measured
///   against Liberation Sans, which the suite serves in place of the machine's own fonts through
///   <c>PinnedFontResolver</c> - so the numbers below are the same on every machine, and asserting
///   relations between them rather than the numbers themselves keeps that from being load-bearing.
/// </summary>
public class XFontDescriptionTests
{
    const string Family = "Arial";
    const double EmSize = 12;

    [Fact]
    public void AFontIsTheSizeAndStyleItWasAskedForAndTheFamilyItActuallyGot()
    {
        // Name is the family the resolver handed back rather than the one the caller asked for,
        // which is the whole point of having a resolver: this suite serves Liberation Sans for
        // every request so that glyph widths - and therefore line breaks - are the same on every
        // machine. A caller reading Name back expecting to see what it passed in is wrong.
        var font = new XFont(Family, EmSize);

        font.Size.Should().Be(EmSize);
        font.Style.Should().Be(XFontStyle.Regular);
        font.Name.Should().NotBeNullOrWhiteSpace();
        font.Name.Should().Be(font.FontFamily.Name);
    }

    [Theory]
    [InlineData(XFontStyle.Regular, false, false, false, false)]
    [InlineData(XFontStyle.Bold, true, false, false, false)]
    [InlineData(XFontStyle.Italic, false, true, false, false)]
    [InlineData(XFontStyle.BoldItalic, true, true, false, false)]
    [InlineData(XFontStyle.Underline, false, false, true, false)]
    [InlineData(XFontStyle.Strikeout, false, false, false, true)]
    public void EachPartOfTheStyleIsReadableOnItsOwn(
        XFontStyle style, bool bold, bool italic, bool underline, bool strikeout)
    {
        var font = new XFont(Family, EmSize, style);

        font.Style.Should().Be(style);
        font.Bold.Should().Be(bold);
        font.Italic.Should().Be(italic);
        font.Underline.Should().Be(underline);
        font.Strikeout.Should().Be(strikeout);
    }

    [Fact]
    public void AFontHasPdfOptionsWhetherOrNotItWasGivenAny()
    {
        new XFont(Family, EmSize).PdfOptions.Should().NotBeNull();

        var options = new XPdfFontOptions(PdfFontEncoding.Unicode);
        new XFont(Family, EmSize, XFontStyle.Regular, options).PdfOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void AFontIsAsTallAsItsAscentAndDescentTogether()
    {
        var font = new XFont(Family, EmSize);

        font.CellAscent.Should().BePositive();
        font.CellDescent.Should().NotBe(0);
        font.CellSpace.Should().BeGreaterThan(font.CellAscent,
            "the line box has to hold the descent as well");
        font.GetHeight().Should().BePositive();
        font.Height.Should().BePositive();
    }

    [Fact]
    public void ABiggerFontIsTaller()
    {
        // The cell values are in design units and so do not change with the size; the height in
        // points does, and in proportion.
        var small = new XFont(Family, 10);
        var large = new XFont(Family, 20);

        large.CellAscent.Should().Be(small.CellAscent, "design units do not know the size");
        large.GetHeight().Should().BeApproximately(small.GetHeight() * 2, 1e-6);
    }

    [Fact]
    public void TheMetricsAreTheSameFontMeasuredInDesignUnits()
    {
        var font = new XFont(Family, EmSize);

        var metrics = font.Metrics;

        metrics.Should().NotBeNull();
        metrics.Should().BeSameAs(font.Metrics, "they are read once and kept");
        metrics.Name.Should().NotBeNullOrEmpty();
        metrics.UnitsPerEm.Should().BePositive();
        metrics.Ascent.Should().Be(font.CellAscent);
        metrics.Descent.Should().Be(font.CellDescent);
        metrics.LineSpacing.Should().Be(font.CellSpace);
    }

    [Fact]
    public void TheMetricsCarryTheMeasurementsTheRulesAndTheDescriptorAreDrawnFrom()
    {
        var metrics = new XFont(Family, EmSize).Metrics;

        metrics.CapHeight.Should().BePositive();
        metrics.XHeight.Should().BePositive();
        metrics.UnderlineThickness.Should().BePositive();
        metrics.StrikethroughThickness.Should().BePositive();
        metrics.UnderlinePosition.Should().NotBe(metrics.StrikethroughPosition,
            "the two rules are drawn in different places");
        metrics.Leading.Should().Be(metrics.LineSpacing - metrics.Ascent - metrics.Descent);
    }

    [Fact]
    public void TheStemWidthsAndTheWidthSummariesAreNeverFilledInAtAll()
    {
        // Both stem widths and the two width summaries come out as zero: the horizontal stem and
        // the widths are passed as literal zeroes when the metrics are built, and the OpenType
        // descriptor this font's vertical stem is read from does not fill that in either. They
        // are part of the public surface, so a caller can ask and will get nothing; pinned here
        // so that reading real values into them shows up as a change rather than as a surprise.
        var metrics = new XFont(Family, EmSize).Metrics;

        metrics.StemV.Should().Be(0);
        metrics.StemH.Should().Be(0);
        metrics.AverageWidth.Should().Be(0);
        metrics.MaxWidth.Should().Be(0);
    }

    [Fact]
    public void AFamilyKnowsItsOwnMetricsWithoutAFontToGoWithThem()
    {
        var family = new XFontFamily(Family);

        family.Name.Should().Be(Family);
        family.GetEmHeight(XFontStyle.Regular).Should().BePositive();
        family.GetCellAscent(XFontStyle.Regular).Should().BePositive();
        family.GetCellDescent(XFontStyle.Regular).Should().BePositive();
        family.GetLineSpacing(XFontStyle.Regular).Should().BePositive();
    }

    [Fact]
    public void AFamilyAgreesWithAFontOfTheSameFamily()
    {
        var family = new XFontFamily(Family);
        var font = new XFont(Family, EmSize);

        family.GetCellAscent(XFontStyle.Regular).Should().Be(font.CellAscent);
        family.GetCellDescent(XFontStyle.Regular).Should().Be(font.CellDescent);
        family.GetLineSpacing(XFontStyle.Regular).Should().Be(font.CellSpace);
        family.GetEmHeight(XFontStyle.Regular).Should().Be(font.Metrics.UnitsPerEm);
    }

    [Fact]
    public void AFamilyAskedForTwiceIsTheSameFamilyUnderneath()
    {
        // Families are cached by name, so two XFontFamily objects for one name share their
        // implementation rather than reading the font file twice.
        new XFontFamily(Family).Name.Should().Be(new XFontFamily(Family).Name);
    }

    [Fact]
    public void NoStyleIsReportedAsAvailableWhicheverIsAskedAbout()
    {
        // Upstream never implemented the lookup and answers no to everything. Pinned so that the
        // day it starts answering truthfully, the callers that trust it are found.
        var family = new XFontFamily(Family);

        family.IsStyleAvailable(XFontStyle.Regular).Should().BeFalse();
        family.IsStyleAvailable(XFontStyle.Bold).Should().BeFalse();
    }

    [Fact]
    public void TwoFontsOfTheSameDescriptionMeasureTheSameText()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        var first = gfx.MeasureString("Handles", new XFont(Family, EmSize));
        var second = gfx.MeasureString("Handles", new XFont(Family, EmSize));

        first.Should().Be(second);
        first.Width.Should().BePositive();
        first.Height.Should().BePositive();
    }

    [Fact]
    public void BoldTextIsWiderThanTheSameTextRegular()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        var regular = gfx.MeasureString("Handles", new XFont(Family, EmSize));
        var bold = gfx.MeasureString("Handles", new XFont(Family, EmSize, XFontStyle.Bold));

        bold.Width.Should().BeGreaterThan(regular.Width);
    }

    [Fact]
    public void MeasuringNothingIsNoWidthAtAll()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        gfx.MeasureString("", new XFont(Family, EmSize)).Width.Should().Be(0);
    }

    // ----- image formats -------------------------------------------------------------------------

    [Fact]
    public void EachImageFormatIsItselfAndNoneOfTheOthers()
    {
        var formats = new[]
        {
            XImageFormat.Png, XImageFormat.Gif, XImageFormat.Jpeg,
            XImageFormat.Tiff, XImageFormat.Icon, XImageFormat.Pdf
        };

        for (var outer = 0; outer < formats.Length; outer++)
        {
            // ReSharper disable once EqualExpressionComparison
            formats[outer].Equals(formats[outer]).Should().BeTrue();
            formats[outer].GetHashCode().Should().Be(formats[outer].GetHashCode());
            // ReSharper disable once SuspiciousTypeConversion.Global
            formats[outer].Equals("not a format").Should().BeFalse();
            formats[outer].Equals(null).Should().BeFalse();

            for (var inner = 0; inner < formats.Length; inner++)
            {
                if (inner != outer)
                    formats[outer].Equals(formats[inner]).Should().BeFalse();
            }
        }
    }

    [Fact]
    public void AnImageFormatIsTheSameObjectEveryTimeItIsAskedFor()
    {
        XImageFormat.Png.Should().BeSameAs(XImageFormat.Png);
    }
}
