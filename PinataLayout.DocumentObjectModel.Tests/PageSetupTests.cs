using System;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="PageSetup.GetPageSize(PageFormat, out Unit, out Unit)"/> is a switch over every
///   named page size the DOM offers, and a switch is exactly the shape of code where one arm is
///   wrong and nothing says so - a page a few millimetres out looks like a page.
///   <para>
///   So the sweep below is over every member of the enumeration rather than a handful, and it
///   checks the relationships that hold between the sizes rather than restating each one from the
///   same table the code was written from. An A series where each size is the one above it halved
///   is right; an A series where one entry was typed from the wrong row of a reference is not, and
///   only the relationship catches that.
///   </para>
/// </summary>
public class PageSetupTests
{
    public static TheoryData<PageFormat> EveryPageFormat
    {
        get
        {
            var data = new TheoryData<PageFormat>();
            foreach (PageFormat format in Enum.GetValues(typeof(PageFormat)))
                data.Add(format);
            return data;
        }
    }

    /// <summary>
    ///   Two of the sixty are wider than they are tall, and both on purpose: Ledger is Tabloid
    ///   turned over - 17 by 11 rather than 11 by 17, which is the whole difference between the two
    ///   names - and the traditional Crown is 20 by 15. Naming them here rather than loosening the
    ///   rule keeps the rule useful: a third landscape size would be a mistake, and would fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPageFormat))]
    public void EveryNamedFormatHasASizeAndAlmostAllOfThemArePortrait(PageFormat format)
    {
        PageSetup.GetPageSize(format, out var width, out var height);

        width.Point.Should().BePositive();
        height.Point.Should().BePositive();

        if (format is PageFormat.Ledger or PageFormat.Crown)
            width.Point.Should().BeGreaterThan(height.Point, "these two are defined lying down");
        else
            height.Point.Should().BeGreaterThan(width.Point);
    }

    [Theory]
    [MemberData(nameof(EveryPageFormat))]
    public void EveryNamedFormatIsAPlausibleSizeOfPaper(PageFormat format)
    {
        // Between a postage stamp and a poster. Loose on purpose: the point is to catch an arm of
        // the switch that returns points where it meant millimetres, which is out by a factor of
        // about three.
        PageSetup.GetPageSize(format, out var width, out var height);

        width.Millimeter.Should().BeInRange(20, 2500);
        height.Millimeter.Should().BeInRange(20, 2500);
    }

    /// <summary>
    ///   The defining property of the ISO 216 A series: each size is the one above it cut in half
    ///   across its longer side, so the height of one is the width of the next and the width of one
    ///   is half the height of the next. Millimetre sizes are rounded to whole millimetres, which is
    ///   why this allows a millimetre either way rather than asking for exactness.
    /// </summary>
    [Theory]
    [InlineData(PageFormat.A0, PageFormat.A1)]
    [InlineData(PageFormat.A1, PageFormat.A2)]
    [InlineData(PageFormat.A2, PageFormat.A3)]
    [InlineData(PageFormat.A3, PageFormat.A4)]
    [InlineData(PageFormat.A4, PageFormat.A5)]
    [InlineData(PageFormat.A5, PageFormat.A6)]
    [InlineData(PageFormat.A6, PageFormat.A7)]
    [InlineData(PageFormat.A7, PageFormat.A8)]
    [InlineData(PageFormat.A8, PageFormat.A9)]
    [InlineData(PageFormat.A9, PageFormat.A10)]
    public void EachASizeIsTheOneAboveItHalved(PageFormat larger, PageFormat smaller)
    {
        PageSetup.GetPageSize(larger, out var largeWidth, out var largeHeight);
        PageSetup.GetPageSize(smaller, out var smallWidth, out var smallHeight);

        smallWidth.Millimeter.Should().BeApproximately(largeHeight.Millimeter / 2, 1);
        smallHeight.Millimeter.Should().BeApproximately(largeWidth.Millimeter, 1);
    }

    [Fact]
    public void A4IsTheSizeEverybodyKnows()
    {
        // One size stated outright, so that a sweep which only checks relationships cannot pass on
        // a series that is internally consistent and uniformly wrong.
        PageSetup.GetPageSize(PageFormat.A4, out var width, out var height);

        width.Millimeter.Should().BeApproximately(210, 0.5);
        height.Millimeter.Should().BeApproximately(297, 0.5);
    }

    [Fact]
    public void LetterIsTheSizeTheOtherHalfOfTheWorldKnows()
    {
        PageSetup.GetPageSize(PageFormat.Letter, out var width, out var height);

        width.Inch.Should().BeApproximately(8.5, 0.02);
        height.Inch.Should().BeApproximately(11, 0.02);
    }

    /// <summary>
    ///   Two sizes share an arm of the switch apiece, and both pairs are the same paper under two
    ///   names: Tabloid is also sold as 11x17, and Statement is abbreviated STMT. Every other size
    ///   is its own, which is the property worth holding - a table of sixty entries written by hand
    ///   invites the copied-and-not-edited arm, and that is what an unexpected duplicate would be.
    /// </summary>
    [Fact]
    public void TheOnlyFormatsThatShareASizeAreTheOnesThatShareAName()
    {
        var byName = Enum.GetValues(typeof(PageFormat)).Cast<PageFormat>().ToDictionary(
            format => format,
            format =>
            {
                PageSetup.GetPageSize(format, out var width, out var height);
                return $"{Math.Round(width.Millimeter, 1)}x{Math.Round(height.Millimeter, 1)}";
            });

        var shared = byName.GroupBy(entry => entry.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(entry => entry.Key.ToString())
                .OrderBy(name => name, StringComparer.Ordinal))
            .Select(names => string.Join("/", names))
            .OrderBy(names => names);

        shared.Should().Equal("P11x17/Tabloid", "STMT/Statement");
    }

    /// <summary>
    ///   A value that names no format has no size, and this answers zero by zero rather than
    ///   throwing. It is not a hole: <c>PageSetup.PageFormat</c> refuses a value the enumeration
    ///   does not define, so the only way here is to cast an integer and call this directly.
    /// </summary>
    [Fact]
    public void AFormatThatIsNotOneHasNoSize()
    {
        PageSetup.GetPageSize((PageFormat)9999, out var width, out var height);

        width.Point.Should().Be(0);
        height.Point.Should().Be(0);
    }

    [Fact]
    public void AFormatThatIsNotOneIsRefusedWhereItWouldReachADocument()
    {
        var setup = new Document().AddSection().PageSetup;

        var act = () => setup.PageFormat = (PageFormat)9999;

        act.Should().Throw<ArgumentException>();
    }

    // ----- what a section's setup does with a format ------------------------------------------------

    /// <summary>
    ///   Naming a format records the name and nothing else. <c>PageWidth</c> and
    ///   <c>PageHeight</c> stay unset, and the size is looked up from the name later - which is why
    ///   <see cref="PageSetup.GetPageSize(PageFormat, out Unit, out Unit)"/> is public and static.
    ///   Worth pinning because the opposite is the natural guess, and a caller who reads PageWidth
    ///   back expecting 148mm gets nothing and no complaint.
    /// </summary>
    [Fact]
    public void NamingAFormatRecordsTheNameRatherThanTheSize()
    {
        var setup = new Document().AddSection().PageSetup;

        setup.PageFormat = PageFormat.A5;

        setup.PageFormat.Should().Be(PageFormat.A5);
        setup.PageWidth.IsEmpty.Should().BeTrue("the size is not written down here");
        setup.PageHeight.IsEmpty.Should().BeTrue();

        PageSetup.GetPageSize(setup.PageFormat, out var width, out _);
        width.Millimeter.Should().BeApproximately(148, 0.5, "it is looked up from the name");
    }

    [Fact]
    public void AWidthGivenOutrightIsKeptAsGiven()
    {
        // The other way round: a setup told a size explicitly holds that size, and the format it
        // also carries does not overwrite it.
        var setup = new Document().AddSection().PageSetup;

        setup.PageFormat = PageFormat.A4;
        setup.PageWidth = "10cm";
        setup.PageHeight = "20cm";

        setup.PageWidth.Centimeter.Should().BeApproximately(10, 1e-6);
        setup.PageHeight.Centimeter.Should().BeApproximately(20, 1e-6);
        setup.PageFormat.Should().Be(PageFormat.A4, "both are recorded, and the renderer decides");
    }

    [Fact]
    public void TheOrientationIsRecordedBesideTheFormatRatherThanAppliedToIt()
    {
        var setup = new Document().AddSection().PageSetup;

        setup.PageFormat = PageFormat.A4;
        setup.Orientation = Orientation.Landscape;

        setup.Orientation.Should().Be(Orientation.Landscape);
        // The lookup answers portrait whatever the orientation says, because the orientation is not
        // one of its arguments. Turning the page over is the renderer's job.
        PageSetup.GetPageSize(setup.PageFormat, out var width, out var height);
        height.Point.Should().BeGreaterThan(width.Point);
    }

    [Fact]
    public void ASectionWithNoSetupOfItsOwnFallsBackToTheDocumentDefault()
    {
        var document = new Document();
        var section = document.AddSection();

        section.PageSetup.Should().NotBeNull();
        document.DefaultPageSetup.Should().NotBeNull();
        document.DefaultPageSetup.PageFormat.Should().Be(PageFormat.A4, "which is the DOM's default");
    }
}
