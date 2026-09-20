using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   What the five geometry types do with a string that is not one of them.
///   <para>
///   <see cref="XPoint.Parse"/> and its four siblings are the other half of <c>ToString</c>, and
///   each is tested elsewhere against the text its own <c>ToString</c> produces. That is the happy
///   path, and it is the only path those tests take - so the tokenizer underneath them, which is
///   one class shared by all five, had never been asked for a token that was not there. Every one
///   of these parsers reads a fixed number of numbers and then insists the string is finished, and
///   both halves of that have to say so rather than answering a rectangle made of whatever was
///   read before the text ran out.
///   </para>
///   <para>
///   The separator is the invariant culture's, which is a comma; whitespace around it is skipped
///   and whitespace alone separates two numbers just as well. <c>Empty</c> and <c>Identity</c> are
///   words rather than numbers, and each is accepted by exactly the type that writes it.
///   </para>
/// </summary>
public class GeometryParsingTests
{
    // ----- the whole string has to be read ----------------------------------------------------

    [Theory]
    [InlineData("1")]
    [InlineData("1,")]
    [InlineData("")]
    [InlineData("   ")]
    public void APointThatRunsOutOfNumbersIsRefusedRatherThanFilledIn(string source)
    {
        var parsing = () => XPoint.Parse(source);

        parsing.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1,2 3")]
    public void APointWithMoreNumbersThanItHasCoordinatesIsRefused(string source)
    {
        var parsing = () => XPoint.Parse(source);

        parsing.Should().Throw<InvalidOperationException>().WithMessage("*Extra data*");
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1")]
    public void ASizeIsRefusedUnlessItIsExactlyTwoNumbers(string source)
    {
        var parsing = () => XSize.Parse(source);

        parsing.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1")]
    public void AVectorIsRefusedUnlessItIsExactlyTwoNumbers(string source)
    {
        var parsing = () => XVector.Parse(source);

        parsing.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    public void ARectangleIsRefusedUnlessItIsExactlyFourNumbers(string source)
    {
        var parsing = () => XRect.Parse(source);

        parsing.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,2,3,4,5,6,7")]
    public void AMatrixIsRefusedUnlessItIsExactlySixNumbers(string source)
    {
        var parsing = () => XMatrix.Parse(source);

        parsing.Should().Throw<InvalidOperationException>();
    }

    // ----- what is accepted -------------------------------------------------------------------

    /// <summary>
    ///   A comma is the invariant culture's separator and whitespace is skipped around it, so the
    ///   same pair of numbers can be written several ways and all of them mean the same point.
    /// </summary>
    [Theory]
    [InlineData("1,2")]
    [InlineData("1 2")]
    [InlineData("  1 , 2  ")]
    public void TheSeparatorBetweenTwoNumbersMayBeACommaOrWhitespaceOrBoth(string source)
    {
        XPoint.Parse(source).Should().Be(new XPoint(1, 2));
    }

    /// <summary>
    ///   <c>Empty</c> is what an empty rectangle and an empty size write themselves as, so it has
    ///   to read back as one - and a word is not a number, which is why each parser looks at its
    ///   first token before deciding how many more to read.
    /// </summary>
    [Fact]
    public void TheWordEachTypeWritesForNothingIsReadBackAsThatNothing()
    {
        XRect.Parse("Empty").Should().Be(XRect.Empty);
        XSize.Parse("Empty").Should().Be(XSize.Empty);
        XMatrix.Parse("Identity").Should().Be(XMatrix.Identity);
    }

    [Fact]
    public void AWordOneTypeWritesIsNotAWordAnotherReads()
    {
        var emptyMatrix = () => XMatrix.Parse("Empty");
        var identityRect = () => XRect.Parse("Identity");

        emptyMatrix.Should().Throw<Exception>();
        identityRect.Should().Throw<Exception>();
    }

    // ----- several points at once -------------------------------------------------------------

    [Fact]
    public void ARunOfPointsIsReadAsOnePointPerSpaceSeparatedPair()
    {
        var points = XPoint.ParsePoints("1,2 3,4 5,6");

        points.Should().Equal(new XPoint(1, 2), new XPoint(3, 4), new XPoint(5, 6));
    }

    [Fact]
    public void ARunOfPointsThatIsNotThereSaysSoRatherThanAnsweringNone()
    {
        var parsing = () => XPoint.ParsePoints(null);

        parsing.Should().Throw<ArgumentNullException>();
    }
}
