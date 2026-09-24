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
            foreach (var format in Enum.GetValues<PageFormat>())
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
        var byName = Enum.GetValues<PageFormat>().ToDictionary(
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

    /// <summary>
    ///   Every size stated outright, with the unit it is built in. The relationships above would
    ///   pass on a table that moved every sheet by the same amount, and a sheet built in points
    ///   rather than millimetres serializes differently even where it measures the same, so this
    ///   pins the lookup exactly: the value and the unit of both sides of every format.
    /// </summary>
    [Theory]
    [InlineData(PageFormat.A0, "mm", 841, 1189)]
    [InlineData(PageFormat.A1, "mm", 594, 841)]
    [InlineData(PageFormat.A2, "mm", 420, 594)]
    [InlineData(PageFormat.A3, "mm", 297, 420)]
    [InlineData(PageFormat.A4, "mm", 210, 297)]
    [InlineData(PageFormat.A5, "mm", 148, 210)]
    [InlineData(PageFormat.A6, "mm", 105, 148)]
    [InlineData(PageFormat.A7, "mm", 74, 105)]
    [InlineData(PageFormat.A8, "mm", 52, 74)]
    [InlineData(PageFormat.A9, "mm", 37, 52)]
    [InlineData(PageFormat.A10, "mm", 26, 37)]
    [InlineData(PageFormat.TwoA0, "mm", 1189, 1682)]
    [InlineData(PageFormat.FourA0, "mm", 1682, 2378)]
    [InlineData(PageFormat.B0, "mm", 1000, 1414)]
    [InlineData(PageFormat.B1, "mm", 707, 1000)]
    [InlineData(PageFormat.B2, "mm", 500, 707)]
    [InlineData(PageFormat.B3, "mm", 353, 500)]
    [InlineData(PageFormat.B4, "mm", 250, 353)]
    [InlineData(PageFormat.B5, "mm", 176, 250)]
    [InlineData(PageFormat.B6, "mm", 125, 176)]
    [InlineData(PageFormat.B7, "mm", 88, 125)]
    [InlineData(PageFormat.B8, "mm", 62, 88)]
    [InlineData(PageFormat.B9, "mm", 44, 62)]
    [InlineData(PageFormat.B10, "mm", 31, 44)]
    [InlineData(PageFormat.JISB5, "mm", 182, 257)]
    [InlineData(PageFormat.C0, "mm", 917, 1297)]
    [InlineData(PageFormat.C1, "mm", 648, 917)]
    [InlineData(PageFormat.C2, "mm", 458, 648)]
    [InlineData(PageFormat.C3, "mm", 324, 458)]
    [InlineData(PageFormat.C4, "mm", 229, 324)]
    [InlineData(PageFormat.C5, "mm", 162, 229)]
    [InlineData(PageFormat.C6, "mm", 114, 162)]
    [InlineData(PageFormat.C7, "mm", 81, 114)]
    [InlineData(PageFormat.C8, "mm", 57, 81)]
    [InlineData(PageFormat.C9, "mm", 40, 57)]
    [InlineData(PageFormat.C10, "mm", 28, 40)]
    [InlineData(PageFormat.RA0, "mm", 860, 1220)]
    [InlineData(PageFormat.RA1, "mm", 610, 860)]
    [InlineData(PageFormat.RA2, "mm", 430, 610)]
    [InlineData(PageFormat.RA3, "mm", 305, 430)]
    [InlineData(PageFormat.RA4, "mm", 215, 305)]
    [InlineData(PageFormat.RA5, "mm", 153, 215)]
    [InlineData(PageFormat.SRA0, "mm", 900, 1280)]
    [InlineData(PageFormat.SRA1, "mm", 640, 900)]
    [InlineData(PageFormat.SRA2, "mm", 450, 640)]
    [InlineData(PageFormat.SRA3, "mm", 320, 450)]
    [InlineData(PageFormat.SRA4, "mm", 225, 320)]
    [InlineData(PageFormat.Letter, "in", 8.5, 11)]
    [InlineData(PageFormat.Legal, "in", 8.5, 14)]
    [InlineData(PageFormat.Ledger, "in", 17, 11)]
    [InlineData(PageFormat.Tabloid, "in", 11, 17)]
    [InlineData(PageFormat.P11x17, "in", 11, 17)]
    [InlineData(PageFormat.Executive, "in", 7.25, 10.5)]
    [InlineData(PageFormat.GovernmentLetter, "in", 8, 10.5)]
    [InlineData(PageFormat.Statement, "in", 5.5, 8.5)]
    [InlineData(PageFormat.STMT, "in", 5.5, 8.5)]
    [InlineData(PageFormat.Folio, "in", 8.5, 13)]
    [InlineData(PageFormat.Size10x14, "in", 10, 14)]
    [InlineData(PageFormat.Quarto, "in", 8, 10)]
    [InlineData(PageFormat.Foolscap, "in", 8, 13)]
    [InlineData(PageFormat.Post, "in", 15.5, 19.25)]
    [InlineData(PageFormat.Crown, "in", 20, 15)]
    [InlineData(PageFormat.LargePost, "in", 16.5, 21)]
    [InlineData(PageFormat.Demy, "in", 17.5, 22)]
    [InlineData(PageFormat.Medium, "in", 18, 23)]
    [InlineData(PageFormat.Royal, "in", 20, 25)]
    [InlineData(PageFormat.Elephant, "in", 23, 28)]
    [InlineData(PageFormat.DoubleDemy, "in", 23.5, 35)]
    [InlineData(PageFormat.QuadDemy, "in", 35, 45)]
    public void EveryNamedFormatIsExactlyTheSizeItHasAlwaysBeen(PageFormat format, string unit, double width, double height)
    {
        PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);

        if (unit == "mm")
        {
            pageWidth.Type.Should().Be(UnitType.Millimeter);
            pageHeight.Type.Should().Be(UnitType.Millimeter);
            pageWidth.Value.Should().Be(width);
            pageHeight.Value.Should().Be(height);
        }
        else
        {
            // An inch size goes in as points, 72 to the inch, so that Letter serializes as 612.
            pageWidth.Type.Should().Be(UnitType.Point);
            pageHeight.Type.Should().Be(UnitType.Point);
            pageWidth.Value.Should().Be(width * 72);
            pageHeight.Value.Should().Be(height * 72);
        }
    }

    [Fact]
    public void TheExactTableNamesEveryFormat()
    {
        var method = typeof(PageSetupTests).GetMethod(nameof(EveryNamedFormatIsExactlyTheSizeItHasAlwaysBeen))!;
        var pinned = method.GetCustomAttributes(typeof(InlineDataAttribute), false)
            .Cast<InlineDataAttribute>()
            .Select(data => (PageFormat)data.GetData(method).Single()[0]);

        pinned.Should().BeEquivalentTo(Enum.GetValues<PageFormat>());
    }
}
