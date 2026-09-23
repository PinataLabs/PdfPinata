using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.BarCodes;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="CodeOmr.StandardMarkDistance"/> and <see cref="CodeOmr.ToUnit(MarkDistance)"/>: the
///   standard mark pitches as a type, over the <see cref="CodeOmr.MakerDistance"/> in points that
///   is what the code is drawn from.
/// </summary>
/// <remarks>
///   Both, and the <see cref="MarkDistance"/> enum they take, were inherited as comments - the
///   enum's file compiled to nothing - so the only way to ask for a 2/8 inch pitch was to know it
///   is 18 points. They are one value seen two ways rather than two settings, which is what the
///   round trips here pin: whichever is assigned, the other reads it back.
/// </remarks>
public class CodeOmrTests
{
    private static readonly XSize Size = new(200, 20);

    private static CodeOmr Omr(string text = "1") => new(text, Size, CodeDirection.LeftToRight);

    [Theory]
    [InlineData(MarkDistance.Inch1_6, 1.0 / 6.0)]
    [InlineData(MarkDistance.Inch2_6, 2.0 / 6.0)]
    [InlineData(MarkDistance.Inch2_8, 2.0 / 8.0)]
    public void EachStandardDistanceIsTheFractionOfAnInchItIsNamedFor(MarkDistance distance, double inches)
    {
        CodeOmr.ToUnit(distance).Inch.Should().BeApproximately(inches, 1e-12);
    }

    [Fact]
    public void AValueThatIsNotInTheEnumIsRefusedAsOne()
    {
        var converting = () => CodeOmr.ToUnit((MarkDistance)99);

        converting.Should().Throw<InvalidEnumArgumentException>();
    }

    [Fact]
    public void ANewCodeReadsAsOneSixthOfAnInch()
    {
        // The default MakerDistance has always been 12 points, so the typed view of it must
        // agree rather than introduce a default of its own.
        var code = Omr();

        code.MakerDistance.Should().Be(12);
        code.StandardMarkDistance.Should().Be(MarkDistance.Inch1_6);
    }

    [Theory]
    [InlineData(MarkDistance.Inch1_6, 12)]
    [InlineData(MarkDistance.Inch2_6, 24)]
    [InlineData(MarkDistance.Inch2_8, 18)]
    public void AssigningAStandardDistanceSetsTheDistanceInPoints(MarkDistance distance, double points)
    {
        var code = Omr();

        code.StandardMarkDistance = distance;

        code.MakerDistance.Should().Be(points);
        code.StandardMarkDistance.Should().Be(distance);
    }

    [Fact]
    public void ADistanceInPointsThatIsStandardReadsAsTheOneItIs()
    {
        var code = Omr();

        code.MakerDistance = 18;

        code.StandardMarkDistance.Should().Be(MarkDistance.Inch2_8);
    }

    [Theory]
    [InlineData(6, MarkDistance.Inch1_6)]
    [InlineData(3, MarkDistance.Inch2_6)]
    [InlineData(4, MarkDistance.Inch2_8)]
    public void AStandardDistanceGivenInMillimetresReadsAsTheOneItIs(int perInch, MarkDistance expected)
    {
        // 25.4 / 6 mm converts to 12.000000000000002 points and 25.4 / 3 mm to 24.000000000000004:
        // the pitch named, short of the last bit. Compared exactly, those read as no standard
        // distance at all, although MakerDistance is a double any unit converts into.
        var code = Omr();

        code.MakerDistance = XUnit.FromMillimeter(25.4 / perInch);

        code.StandardMarkDistance.Should().Be(expected);
    }

    [Fact]
    public void ADistanceInPointsThatIsNoStandardOneReadsAsNull()
    {
        var code = Omr();

        code.MakerDistance = 13;

        code.StandardMarkDistance.Should().BeNull();
    }

    [Fact]
    public void NullIsRefusedAndLeavesTheDistanceAlone()
    {
        var code = Omr();
        code.StandardMarkDistance = MarkDistance.Inch2_6;

        var clearing = () => code.StandardMarkDistance = null;

        clearing.Should().Throw<ArgumentNullException>().WithMessage("*MakerDistance*");
        code.MakerDistance.Should().Be(24);
    }

    [Theory]
    [InlineData(MarkDistance.Inch1_6, 12)]
    [InlineData(MarkDistance.Inch2_6, 24)]
    [InlineData(MarkDistance.Inch2_8, 18)]
    public void TheMarksAreDrawnThatFarApart(MarkDistance distance, double points)
    {
        // "3" sets the two lowest bits, so after the synchronising mark there are two marks side
        // by side, one pitch apart.
        var code = Omr("3");
        code.StandardMarkDistance = distance;

        var marks = MarkLeftEdges(gfx => gfx.DrawBarCode(code, new XPoint(50, 50)));

        marks.Should().HaveCount(3, "one synchronising mark and a mark for each set bit");
        (marks[2] - marks[1]).Should().BeApproximately(points, 1e-3);
    }

    /// <summary>
    ///   The left edge of every rectangle the drawing painted, in the order they were drawn.
    /// </summary>
    private static double[] MarkLeftEdges(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = false;
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        var content = Encoding.Latin1.GetString(PageContent.Of(page));
        return Regex.Matches(content, @"(-?[\d.]+) -?[\d.]+ -?[\d.]+ -?[\d.]+ re")
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
    }
}
