using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The roman numerals and letter sequences a numeric field's <c>Format</c> asks for, and a
///   footnote's mark after it. Until this moved out of the renderer the only coverage it had was
///   the "I" and the "A" two rendered pages happened to assert, so neither of its two ceilings -
///   the fall back to plain digits past 32768, and the wrap past Z - had ever been run.
/// </summary>
public class NumberFormatterTests
{
    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(9, "IX")]
    [InlineData(27, "XXVII")]
    [InlineData(1990, "MCMXC")]
    [InlineData(3888, "MMMDCCCLXXXVIII")]
    public void ANumberIsWrittenAsARomanNumeral(int number, string expected)
    {
        NumberFormatter.Format(number, "ROMAN").Should().Be(expected);
    }

    [Fact]
    public void ALowercaseRomanNumeralIsTheSameNumeralInLowercase()
    {
        NumberFormatter.Format(1990, "roman").Should().Be("mcmxc");
    }

    /// <summary>
    ///   Roman numerals have no zero and no sign, so both are written the way arabic writes them
    ///   and the numeral carries the magnitude.
    /// </summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(-4, "-IV")]
    public void ZeroAndANegativeStillReadAsSomething(int number, string expected)
    {
        NumberFormatter.Format(number, "ROMAN").Should().Be(expected);
    }

    /// <summary>
    ///   Past 32768 a roman numeral is thirty-odd M's and says nothing a reader can use, so the
    ///   number is written plainly instead. The same ceiling applies to letters, where the run of
    ///   repeated characters would be longer still.
    /// </summary>
    [Theory]
    [InlineData("ROMAN", 32769, "32769")]
    [InlineData("roman", -32769, "-32769")]
    [InlineData("ALPHABETIC", 32769, "32769")]
    [InlineData("alphabetic", -32769, "-32769")]
    public void ANumberTooLargeToWriteThatWayIsWrittenInDigits(string format, int number, string expected)
    {
        NumberFormatter.Format(number, format).Should().Be(expected);
    }

    /// <summary>
    ///   The number furthest past the ceiling was the one the fallback could not catch: taking the
    ///   magnitude of <c>int.MinValue</c> overflows, because it has no positive counterpart, so the
    ///   guard threw where it was supposed to hand the number on to be written in digits.
    /// </summary>
    [Theory]
    [InlineData("ROMAN")]
    [InlineData("roman")]
    [InlineData("ALPHABETIC")]
    [InlineData("alphabetic")]
    public void TheMostNegativeNumberFallsBackToDigitsLikeAnyOtherPastTheCeiling(string format)
    {
        NumberFormatter.Format(int.MinValue, format).Should().Be("-2147483648");
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "ZZ")]
    [InlineData(53, "AAA")]
    public void ANumberPastZIsWrittenAsTheLetterRepeated(int number, string expected)
    {
        NumberFormatter.Format(number, "ALPHABETIC").Should().Be(expected);
    }

    [Fact]
    public void ALowercaseLetterSequenceIsTheSameSequenceInLowercase()
    {
        NumberFormatter.Format(27, "alphabetic").Should().Be("aa");
    }

    /// <summary>
    ///   The empty string is what a numeric field's <c>Format</c> reads as when nothing set it, and
    ///   an unrecognised one is treated no differently: both mean ordinary digits.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Roman")]
    [InlineData("not a format")]
    public void AFormatThatNamesNothingLeavesTheNumberInDigits(string format)
    {
        NumberFormatter.Format(42, format).Should().Be("42");
    }
}
