using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XSize"/> is an extent rather than a pair of numbers: it refuses to go negative,
///   and it has a distinguished empty value that is not the same as zero by zero. Empty is the
///   part worth pinning, because it is stored as a negative width rather than as a flag, and
///   every setter on the struct has to refuse to touch it.
/// </summary>
public class XSizeTests
{
    [Fact]
    public void ASizeIsTheWidthAndHeightItWasGiven()
    {
        var size = new XSize(3, 4);

        size.Width.Should().Be(3);
        size.Height.Should().Be(4);
        size.IsEmpty.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void ASizeCannotBeBuiltNegative(double width, double height)
    {
        var act = () => new XSize(width, height);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ZeroBySizeIsAPerfectlyGoodSizeAndIsNotEmpty()
    {
        // The distinction matters: a zero-area rectangle still has a position, and the drawing
        // code tells the two apart by IsEmpty rather than by area.
        var size = new XSize(0, 0);

        size.IsEmpty.Should().BeFalse();
        size.Should().NotBe(XSize.Empty);
    }

    [Fact]
    public void TheEmptySizeIsEmptyAndReadsAsNegativelyInfiniteRatherThanZero()
    {
        XSize.Empty.IsEmpty.Should().BeTrue();
        XSize.Empty.Width.Should().Be(double.NegativeInfinity);
        XSize.Empty.Height.Should().Be(double.NegativeInfinity);
    }

    [Fact]
    public void TheEmptySizeRefusesToBeGivenAWidthOrAHeight()
    {
        var width = () => { var size = XSize.Empty; size.Width = 1; };
        var height = () => { var size = XSize.Empty; size.Height = 1; };

        width.Should().Throw<InvalidOperationException>();
        height.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASizeRefusesANegativeWidthOrHeightAfterTheFactToo(bool testWidth)
    {
        var act = () =>
        {
            var size = new XSize(1, 1);
            if (testWidth)
                size.Width = -1;
            else
                size.Height = -1;
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WidthAndHeightCanBeSetOnASizeThatIsNotEmpty()
    {
        var size = new XSize(1, 1);

        size.Width = 10;
        size.Height = 20;

        size.Should().Be(new XSize(10, 20));
    }

    [Fact]
    public void EveryEmptySizeIsEqualToEveryOtherOne()
    {
        XSize.Equals(XSize.Empty, XSize.Empty).Should().BeTrue();
        XSize.Equals(XSize.Empty, new XSize(1, 1)).Should().BeFalse();
        XSize.Empty.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void TwoSizesAreEqualWhenBothExtentsAre()
    {
        var size = new XSize(3, 4);

        (size == new XSize(3, 4)).Should().BeTrue();
        (size != new XSize(3, 4)).Should().BeFalse();
        (size != new XSize(3, 5)).Should().BeTrue();
        size.Equals(new XSize(3, 4)).Should().BeTrue();
        size.Equals((object)new XSize(3, 4)).Should().BeTrue();
        size.Equals("not a size").Should().BeFalse();
        size.GetHashCode().Should().Be(new XSize(3, 4).GetHashCode());
    }

    [Fact]
    public void ASizeConvertsToAPointAndAVectorOfTheSameTwoNumbers()
    {
        var size = new XSize(3, 4);

        size.ToXPoint().Should().Be(new XPoint(3, 4));
        size.ToXVector().Should().Be(new XVector(3, 4));
        ((XPoint)size).Should().Be(new XPoint(3, 4));
        ((XVector)size).Should().Be(new XVector(3, 4));
    }

    [Fact]
    public void ASizeIsWrittenAsTwoNumbersAndReadBackTheSameWay()
    {
        var size = new XSize(1.5, 2.5);

        var text = size.ToString(CultureInfo.InvariantCulture);

        text.Should().Be("1.5,2.5");
        XSize.Parse(text).Should().Be(size);
    }

    [Fact]
    public void TheEmptySizeIsWrittenByNameAndReadBackByName()
    {
        XSize.Empty.ToString(CultureInfo.InvariantCulture).Should().Be("Empty");
        XSize.Parse("Empty").Should().Be(XSize.Empty);
    }

    [Fact]
    public void AFormatStringIsAppliedToBothExtents()
    {
        IFormattable size = new XSize(1.23456, 2.34567);

        size.ToString("0.0", CultureInfo.InvariantCulture).Should().Be("1.2,2.3");
    }

    /// <summary>
    ///   Deserialization writes the fields directly and never runs the constructor that refuses a
    ///   negative extent, so the struct checks again once it has been read. The fields are set here
    ///   by reflection, which is what a formatter does.
    /// </summary>
    [Fact]
    public void ADeserializedSizeWithANegativeExtentIsRefused()
    {
        object size = new XSize(3, 4);
        typeof(XSize).GetField("_height", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(size, -1.0);

        Action deserialized = () => ((IDeserializationCallback)size).OnDeserialization(null);

        deserialized.Should().Throw<SerializationException>();
    }

    [Fact]
    public void ADeserializedSizeThatAConstructorCouldHaveMadeIsAccepted()
    {
        foreach (XSize size in new[] { new XSize(3, 4), new XSize(0, 0), XSize.Empty })
        {
            Action deserialized = () => ((IDeserializationCallback)size).OnDeserialization(null);

            deserialized.Should().NotThrow();
        }
    }
}
