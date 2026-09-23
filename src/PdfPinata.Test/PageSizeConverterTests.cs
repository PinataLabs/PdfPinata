using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test;

/// <summary>
/// The predefined page sizes, measured against the dimensions published at
/// https://pdfkit.org/docs/paper_sizes.html. <see cref="PageSizeConverter"/> rounds every size to
/// whole points, so a point of slack is allowed; anything further apart is a different sheet.
/// </summary>
public class PageSizeConverterTests
{
    [Theory]
    // ISO 216 A series.
    [InlineData(PageSize.A0, 2383.94, 3370.39)]
    [InlineData(PageSize.A1, 1683.78, 2383.94)]
    [InlineData(PageSize.A2, 1190.55, 1683.78)]
    [InlineData(PageSize.A3, 841.89, 1190.55)]
    [InlineData(PageSize.A4, 595.28, 841.89)]
    [InlineData(PageSize.A5, 419.53, 595.28)]
    [InlineData(PageSize.A6, 297.64, 419.53)]
    [InlineData(PageSize.A7, 209.76, 297.64)]
    [InlineData(PageSize.A8, 147.40, 209.76)]
    [InlineData(PageSize.A9, 104.88, 147.40)]
    [InlineData(PageSize.A10, 73.70, 104.88)]
    // DIN 476 oversizes.
    [InlineData(PageSize.TwoA0, 3370.39, 4767.87)]
    [InlineData(PageSize.FourA0, 4767.89, 6740.79)]
    // ISO 216 B series.
    [InlineData(PageSize.B0, 2834.65, 4008.19)]
    [InlineData(PageSize.B1, 2004.09, 2834.65)]
    [InlineData(PageSize.B2, 1417.32, 2004.09)]
    [InlineData(PageSize.B3, 1000.63, 1417.32)]
    [InlineData(PageSize.B4, 708.66, 1000.63)]
    [InlineData(PageSize.B5, 498.90, 708.66)]
    [InlineData(PageSize.B6, 354.33, 498.90)]
    [InlineData(PageSize.B7, 249.45, 354.33)]
    [InlineData(PageSize.B8, 175.75, 249.45)]
    [InlineData(PageSize.B9, 124.72, 175.75)]
    [InlineData(PageSize.B10, 87.87, 124.72)]
    // ISO 269 C series, the envelopes.
    [InlineData(PageSize.C0, 2599.37, 3676.54)]
    [InlineData(PageSize.C1, 1836.85, 2599.37)]
    [InlineData(PageSize.C2, 1298.27, 1836.85)]
    [InlineData(PageSize.C3, 918.43, 1298.27)]
    [InlineData(PageSize.C4, 649.13, 918.43)]
    [InlineData(PageSize.C5, 459.21, 649.13)]
    [InlineData(PageSize.C6, 323.15, 459.21)]
    [InlineData(PageSize.C7, 229.61, 323.15)]
    [InlineData(PageSize.C8, 161.57, 229.61)]
    [InlineData(PageSize.C9, 113.39, 161.57)]
    [InlineData(PageSize.C10, 79.37, 113.39)]
    // ISO 217 untrimmed stock.
    [InlineData(PageSize.RA0, 2437.80, 3458.27)]
    [InlineData(PageSize.RA1, 1729.13, 2437.80)]
    [InlineData(PageSize.RA2, 1218.90, 1729.13)]
    [InlineData(PageSize.RA3, 864.57, 1218.90)]
    [InlineData(PageSize.RA4, 609.45, 864.57)]
    [InlineData(PageSize.SRA0, 2551.18, 3628.35)]
    [InlineData(PageSize.SRA1, 1814.17, 2551.18)]
    [InlineData(PageSize.SRA2, 1275.59, 1814.17)]
    [InlineData(PageSize.SRA3, 907.09, 1275.59)]
    [InlineData(PageSize.SRA4, 637.80, 907.09)]
    // North American sizes.
    [InlineData(PageSize.Executive, 521.86, 756.00)]
    [InlineData(PageSize.Folio, 612.00, 936.00)]
    [InlineData(PageSize.Legal, 612.00, 1008.00)]
    [InlineData(PageSize.Letter, 612.00, 792.00)]
    [InlineData(PageSize.Tabloid, 792.00, 1224.00)]
    public void SizeMatchesThePublishedDimensions(PageSize size, double width, double height)
    {
        var actual = PageSizeConverter.ToSize(size);

        actual.Width.Should().BeApproximately(width, 1, $"{size} is {width} points wide");
        actual.Height.Should().BeApproximately(height, 1, $"{size} is {height} points high");
    }

    /// <summary>
    /// A size named by the enumeration but missing from the converter throws on use rather than
    /// falling back to anything, so the two have to be extended together.
    /// </summary>
    [Fact]
    public void EveryNamedSizeConvertsToPoints()
    {
        var named = Enum.GetValues<PageSize>()
            .Where(size => size != PageSize.Undefined)
            .ToArray();

        foreach (var size in named)
        {
            XSize converted = default;
            Action convert = () => converted = PageSizeConverter.ToSize(size);

            convert.Should().NotThrow($"{size} is a named page size");
            converted.Width.Should().BePositive($"{size} has a width");
            converted.Height.Should().BePositive($"{size} has a height");
        }
    }

    [Fact]
    public void UndefinedHasNoSize()
    {
        Action convert = () => PageSizeConverter.ToSize(PageSize.Undefined);

        convert.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// The whole points each size has always converted to, exactly: the published dimensions above
    /// allow a point of slack, and a size moved by less than that is still a different page.
    /// </summary>
    [Theory]
    [InlineData(PageSize.A0, 2384, 3370)]
    [InlineData(PageSize.A1, 1684, 2384)]
    [InlineData(PageSize.A2, 1191, 1684)]
    [InlineData(PageSize.A3, 842, 1191)]
    [InlineData(PageSize.A4, 595, 842)]
    [InlineData(PageSize.A5, 420, 595)]
    [InlineData(PageSize.A6, 298, 420)]
    [InlineData(PageSize.A7, 210, 298)]
    [InlineData(PageSize.A8, 147, 210)]
    [InlineData(PageSize.A9, 105, 147)]
    [InlineData(PageSize.A10, 74, 105)]
    [InlineData(PageSize.TwoA0, 3370, 4768)]
    [InlineData(PageSize.FourA0, 4768, 6741)]
    [InlineData(PageSize.RA0, 2438, 3458)]
    [InlineData(PageSize.RA1, 1729, 2438)]
    [InlineData(PageSize.RA2, 1219, 1729)]
    [InlineData(PageSize.RA3, 865, 1219)]
    [InlineData(PageSize.RA4, 609, 865)]
    [InlineData(PageSize.RA5, 433, 609)]
    [InlineData(PageSize.SRA0, 2551, 3628)]
    [InlineData(PageSize.SRA1, 1814, 2551)]
    [InlineData(PageSize.SRA2, 1276, 1814)]
    [InlineData(PageSize.SRA3, 907, 1276)]
    [InlineData(PageSize.SRA4, 638, 907)]
    [InlineData(PageSize.B0, 2835, 4008)]
    [InlineData(PageSize.B1, 2004, 2835)]
    [InlineData(PageSize.B2, 1417, 2004)]
    [InlineData(PageSize.B3, 1001, 1417)]
    [InlineData(PageSize.B4, 709, 1001)]
    [InlineData(PageSize.B5, 499, 709)]
    [InlineData(PageSize.B6, 354, 499)]
    [InlineData(PageSize.B7, 249, 354)]
    [InlineData(PageSize.B8, 176, 249)]
    [InlineData(PageSize.B9, 125, 176)]
    [InlineData(PageSize.B10, 88, 125)]
    [InlineData(PageSize.C0, 2599, 3677)]
    [InlineData(PageSize.C1, 1837, 2599)]
    [InlineData(PageSize.C2, 1298, 1837)]
    [InlineData(PageSize.C3, 918, 1298)]
    [InlineData(PageSize.C4, 649, 918)]
    [InlineData(PageSize.C5, 459, 649)]
    [InlineData(PageSize.C6, 323, 459)]
    [InlineData(PageSize.C7, 230, 323)]
    [InlineData(PageSize.C8, 162, 230)]
    [InlineData(PageSize.C9, 113, 162)]
    [InlineData(PageSize.C10, 79, 113)]
    [InlineData(PageSize.Quarto, 576, 720)]
    [InlineData(PageSize.Foolscap, 576, 936)]
    [InlineData(PageSize.Executive, 522, 756)]
    [InlineData(PageSize.GovernmentLetter, 576, 756)]
    [InlineData(PageSize.Letter, 612, 792)]
    [InlineData(PageSize.Legal, 612, 1008)]
    [InlineData(PageSize.Ledger, 1224, 792)]
    [InlineData(PageSize.Tabloid, 792, 1224)]
    [InlineData(PageSize.Post, 1126, 1386)]
    [InlineData(PageSize.Crown, 1440, 1080)]
    [InlineData(PageSize.LargePost, 1188, 1512)]
    [InlineData(PageSize.Demy, 1260, 1584)]
    [InlineData(PageSize.Medium, 1296, 1656)]
    [InlineData(PageSize.Royal, 1440, 1800)]
    [InlineData(PageSize.Elephant, 1565, 2016)]
    [InlineData(PageSize.DoubleDemy, 1692, 2520)]
    [InlineData(PageSize.QuadDemy, 2520, 3240)]
    [InlineData(PageSize.STMT, 396, 612)]
    [InlineData(PageSize.Folio, 612, 936)]
    [InlineData(PageSize.Statement, 396, 612)]
    [InlineData(PageSize.Size10x14, 720, 1008)]
    public void EveryNamedSizeIsExactlyTheWholePointsItHasAlwaysBeen(PageSize size, double width, double height)
    {
        var actual = PageSizeConverter.ToSize(size);

        actual.Width.Should().Be(width);
        actual.Height.Should().Be(height);
    }

    [Fact]
    public void TheExactTableNamesEveryDefinedSize()
    {
        var method = typeof(PageSizeConverterTests).GetMethod(nameof(EveryNamedSizeIsExactlyTheWholePointsItHasAlwaysBeen))!;
        var pinned = method.GetCustomAttributes(typeof(InlineDataAttribute), false)
            .Cast<InlineDataAttribute>()
            .Select(data => (PageSize)data.GetData(method).Single()[0]);

        pinned.Should().BeEquivalentTo(Enum.GetValues<PageSize>().Where(size => size != PageSize.Undefined));
    }

    /// <summary>
    /// A value the enumeration does not name is refused the same way as Undefined, naming the
    /// argument it came in through.
    /// </summary>
    [Fact]
    public void AValueThatNamesNoSizeIsRefusedByName()
    {
        Action convert = () => PageSizeConverter.ToSize((PageSize)9999);

        convert.Should().Throw<ArgumentException>()
            .Which.Should().Match<ArgumentException>(e => e.ParamName == "value" && e.Message.StartsWith("Invalid PageSize."));
    }
}
