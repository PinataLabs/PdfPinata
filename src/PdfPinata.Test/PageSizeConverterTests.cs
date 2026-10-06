using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel;
using TUnit.Core;

namespace PdfPinata.Test;

/// <summary>
/// The predefined page sizes, measured against the dimensions published at
/// https://pdfkit.org/docs/paper_sizes.html. <see cref="PageSizeConverter"/> rounds every size to
/// whole points, so a point of slack is allowed; anything further apart is a different sheet.
/// </summary>
public class PageSizeConverterTests
{
    [Test]
    // ISO 216 A series.
    [Arguments(PageSize.A0, 2383.94, 3370.39)]
    [Arguments(PageSize.A1, 1683.78, 2383.94)]
    [Arguments(PageSize.A2, 1190.55, 1683.78)]
    [Arguments(PageSize.A3, 841.89, 1190.55)]
    [Arguments(PageSize.A4, 595.28, 841.89)]
    [Arguments(PageSize.A5, 419.53, 595.28)]
    [Arguments(PageSize.A6, 297.64, 419.53)]
    [Arguments(PageSize.A7, 209.76, 297.64)]
    [Arguments(PageSize.A8, 147.40, 209.76)]
    [Arguments(PageSize.A9, 104.88, 147.40)]
    [Arguments(PageSize.A10, 73.70, 104.88)]
    // DIN 476 oversizes.
    [Arguments(PageSize.TwoA0, 3370.39, 4767.87)]
    [Arguments(PageSize.FourA0, 4767.89, 6740.79)]
    // ISO 216 B series.
    [Arguments(PageSize.B0, 2834.65, 4008.19)]
    [Arguments(PageSize.B1, 2004.09, 2834.65)]
    [Arguments(PageSize.B2, 1417.32, 2004.09)]
    [Arguments(PageSize.B3, 1000.63, 1417.32)]
    [Arguments(PageSize.B4, 708.66, 1000.63)]
    [Arguments(PageSize.B5, 498.90, 708.66)]
    [Arguments(PageSize.B6, 354.33, 498.90)]
    [Arguments(PageSize.B7, 249.45, 354.33)]
    [Arguments(PageSize.B8, 175.75, 249.45)]
    [Arguments(PageSize.B9, 124.72, 175.75)]
    [Arguments(PageSize.B10, 87.87, 124.72)]
    // ISO 269 C series, the envelopes.
    [Arguments(PageSize.C0, 2599.37, 3676.54)]
    [Arguments(PageSize.C1, 1836.85, 2599.37)]
    [Arguments(PageSize.C2, 1298.27, 1836.85)]
    [Arguments(PageSize.C3, 918.43, 1298.27)]
    [Arguments(PageSize.C4, 649.13, 918.43)]
    [Arguments(PageSize.C5, 459.21, 649.13)]
    [Arguments(PageSize.C6, 323.15, 459.21)]
    [Arguments(PageSize.C7, 229.61, 323.15)]
    [Arguments(PageSize.C8, 161.57, 229.61)]
    [Arguments(PageSize.C9, 113.39, 161.57)]
    [Arguments(PageSize.C10, 79.37, 113.39)]
    // ISO 217 untrimmed stock.
    [Arguments(PageSize.RA0, 2437.80, 3458.27)]
    [Arguments(PageSize.RA1, 1729.13, 2437.80)]
    [Arguments(PageSize.RA2, 1218.90, 1729.13)]
    [Arguments(PageSize.RA3, 864.57, 1218.90)]
    [Arguments(PageSize.RA4, 609.45, 864.57)]
    [Arguments(PageSize.SRA0, 2551.18, 3628.35)]
    [Arguments(PageSize.SRA1, 1814.17, 2551.18)]
    [Arguments(PageSize.SRA2, 1275.59, 1814.17)]
    [Arguments(PageSize.SRA3, 907.09, 1275.59)]
    [Arguments(PageSize.SRA4, 637.80, 907.09)]
    // North American sizes.
    [Arguments(PageSize.Executive, 521.86, 756.00)]
    [Arguments(PageSize.Folio, 612.00, 936.00)]
    [Arguments(PageSize.Legal, 612.00, 1008.00)]
    [Arguments(PageSize.Letter, 612.00, 792.00)]
    [Arguments(PageSize.Tabloid, 792.00, 1224.00)]
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
    [Test]
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

    [Test]
    public void UndefinedHasNoSize()
    {
        Action convert = () => PageSizeConverter.ToSize(PageSize.Undefined);

        convert.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// The whole points each size has always converted to, exactly: the published dimensions above
    /// allow a point of slack, and a size moved by less than that is still a different page.
    /// </summary>
    [Test]
    [Arguments(PageSize.A0, 2384, 3370)]
    [Arguments(PageSize.A1, 1684, 2384)]
    [Arguments(PageSize.A2, 1191, 1684)]
    [Arguments(PageSize.A3, 842, 1191)]
    [Arguments(PageSize.A4, 595, 842)]
    [Arguments(PageSize.A5, 420, 595)]
    [Arguments(PageSize.A6, 298, 420)]
    [Arguments(PageSize.A7, 210, 298)]
    [Arguments(PageSize.A8, 147, 210)]
    [Arguments(PageSize.A9, 105, 147)]
    [Arguments(PageSize.A10, 74, 105)]
    [Arguments(PageSize.TwoA0, 3370, 4768)]
    [Arguments(PageSize.FourA0, 4768, 6741)]
    [Arguments(PageSize.RA0, 2438, 3458)]
    [Arguments(PageSize.RA1, 1729, 2438)]
    [Arguments(PageSize.RA2, 1219, 1729)]
    [Arguments(PageSize.RA3, 865, 1219)]
    [Arguments(PageSize.RA4, 609, 865)]
    [Arguments(PageSize.RA5, 434, 609)]
    [Arguments(PageSize.SRA0, 2551, 3628)]
    [Arguments(PageSize.SRA1, 1814, 2551)]
    [Arguments(PageSize.SRA2, 1276, 1814)]
    [Arguments(PageSize.SRA3, 907, 1276)]
    [Arguments(PageSize.SRA4, 638, 907)]
    [Arguments(PageSize.B0, 2835, 4008)]
    [Arguments(PageSize.B1, 2004, 2835)]
    [Arguments(PageSize.B2, 1417, 2004)]
    [Arguments(PageSize.B3, 1001, 1417)]
    [Arguments(PageSize.B4, 709, 1001)]
    [Arguments(PageSize.B5, 499, 709)]
    [Arguments(PageSize.B6, 354, 499)]
    [Arguments(PageSize.B7, 249, 354)]
    [Arguments(PageSize.B8, 176, 249)]
    [Arguments(PageSize.B9, 125, 176)]
    [Arguments(PageSize.B10, 88, 125)]
    [Arguments(PageSize.C0, 2599, 3677)]
    [Arguments(PageSize.C1, 1837, 2599)]
    [Arguments(PageSize.C2, 1298, 1837)]
    [Arguments(PageSize.C3, 918, 1298)]
    [Arguments(PageSize.C4, 649, 918)]
    [Arguments(PageSize.C5, 459, 649)]
    [Arguments(PageSize.C6, 323, 459)]
    [Arguments(PageSize.C7, 230, 323)]
    [Arguments(PageSize.C8, 162, 230)]
    [Arguments(PageSize.C9, 113, 162)]
    [Arguments(PageSize.C10, 79, 113)]
    [Arguments(PageSize.Quarto, 576, 720)]
    [Arguments(PageSize.Foolscap, 576, 936)]
    [Arguments(PageSize.Executive, 522, 756)]
    [Arguments(PageSize.GovernmentLetter, 576, 756)]
    [Arguments(PageSize.Letter, 612, 792)]
    [Arguments(PageSize.Legal, 612, 1008)]
    [Arguments(PageSize.Ledger, 1224, 792)]
    [Arguments(PageSize.Tabloid, 792, 1224)]
    [Arguments(PageSize.Post, 1116, 1386)]
    [Arguments(PageSize.Crown, 1440, 1080)]
    [Arguments(PageSize.LargePost, 1188, 1512)]
    [Arguments(PageSize.Demy, 1260, 1584)]
    [Arguments(PageSize.Medium, 1296, 1656)]
    [Arguments(PageSize.Royal, 1440, 1800)]
    [Arguments(PageSize.Elephant, 1656, 2016)]
    [Arguments(PageSize.DoubleDemy, 1692, 2520)]
    [Arguments(PageSize.QuadDemy, 2520, 3240)]
    [Arguments(PageSize.STMT, 396, 612)]
    [Arguments(PageSize.Folio, 612, 936)]
    [Arguments(PageSize.Statement, 396, 612)]
    [Arguments(PageSize.Size10x14, 720, 1008)]
    public void EveryNamedSizeIsExactlyTheWholePointsItHasAlwaysBeen(PageSize size, double width, double height)
    {
        var actual = PageSizeConverter.ToSize(size);

        actual.Width.Should().Be(width);
        actual.Height.Should().Be(height);
    }

    [Test]
    public void TheExactTableNamesEveryDefinedSize()
    {
        var method = typeof(PageSizeConverterTests).GetMethod(nameof(EveryNamedSizeIsExactlyTheWholePointsItHasAlwaysBeen))!;
        var pinned = method.GetCustomAttributes(typeof(ArgumentsAttribute), false)
            .Cast<ArgumentsAttribute>()
            .Select(data => (PageSize)data.Values[0]);

        pinned.Should().BeEquivalentTo(Enum.GetValues<PageSize>().Where(size => size != PageSize.Undefined));
    }

    /// <summary>
    /// The core's whole-point table and the DOM's, which builds each sheet from the millimetres or
    /// inches that define it, name most of the same sheets - and must agree on them, or a page
    /// sized by <see cref="PageSize"/> and one sized by <see cref="PageFormat"/> are different
    /// paper. Three did not: Post was 1126 wide for 15.5 inches, which is 1116, Elephant 1565 for 23
    /// inches, which is 1656, and RA5 433 for 153 mm, which is 433.7 and so rounds to 434.
    /// </summary>
    [Test]
    public void EverySizeTheDocumentObjectModelAlsoNamesIsItsSizeRoundedToWholePoints()
    {
        var shared = Enum.GetNames<PageSize>().Intersect(Enum.GetNames<PageFormat>()).ToList();
        shared.Should().Contain([nameof(PageSize.A4), nameof(PageSize.Post), nameof(PageSize.Elephant)]);

        foreach (var name in shared)
        {
            var core = PageSizeConverter.ToSize(Enum.Parse<PageSize>(name));
            PageSetup.GetPageSize(Enum.Parse<PageFormat>(name), out var width, out var height);

            (core.Width, core.Height).Should().Be((Math.Round(width.Point), Math.Round(height.Point)),
                $"{name} is {width.Point} by {height.Point} points in the DOM");
        }
    }

    /// <summary>
    /// A value the enumeration does not name is refused the same way as Undefined, naming the
    /// argument it came in through.
    /// </summary>
    [Test]
    public void AValueThatNamesNoSizeIsRefusedByName()
    {
        Action convert = () => PageSizeConverter.ToSize((PageSize)9999);

        convert.Should().Throw<ArgumentException>()
            .Which.Should().Match<ArgumentException>(e => e.ParamName == "value" && e.Message.StartsWith("Invalid PageSize."));
    }
}
