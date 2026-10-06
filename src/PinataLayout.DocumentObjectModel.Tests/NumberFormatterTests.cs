using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The roman numerals and letter sequences a numeric field's <c>Format</c> asks for, and a
///   footnote's mark after it. Until this moved out of the renderer the only coverage it had was
///   the "I" and the "A" two rendered pages happened to assert, so neither of its two ceilings -
///   the fall back to plain digits past 32768, and the wrap past Z - had ever been run.
/// </summary>
public class NumberFormatterTests
{
    [Test]
    [Arguments(1, "I")]
    [Arguments(4, "IV")]
    [Arguments(9, "IX")]
    [Arguments(27, "XXVII")]
    [Arguments(1990, "MCMXC")]
    [Arguments(3888, "MMMDCCCLXXXVIII")]
    public void ANumberIsWrittenAsARomanNumeral(int number, string expected)
    {
        NumberFormatter.Format(number, "ROMAN").Should().Be(expected);
    }

    [Test]
    public void ALowercaseRomanNumeralIsTheSameNumeralInLowercase()
    {
        NumberFormatter.Format(1990, "roman").Should().Be("mcmxc");
    }

    /// <summary>
    ///   Roman numerals have no zero and no sign, so both are written the way arabic writes them
    ///   and the numeral carries the magnitude.
    /// </summary>
    [Test]
    [Arguments(0, "0")]
    [Arguments(-4, "-IV")]
    public void ZeroAndANegativeStillReadAsSomething(int number, string expected)
    {
        NumberFormatter.Format(number, "ROMAN").Should().Be(expected);
    }

    /// <summary>
    ///   Past 32768 a roman numeral is thirty-odd M's and says nothing a reader can use, so the
    ///   number is written plainly instead. The same ceiling applies to letters, where the run of
    ///   repeated characters would be longer still.
    /// </summary>
    [Test]
    [Arguments("ROMAN", 32769, "32769")]
    [Arguments("roman", -32769, "-32769")]
    [Arguments("ALPHABETIC", 32769, "32769")]
    [Arguments("alphabetic", -32769, "-32769")]
    public void ANumberTooLargeToWriteThatWayIsWrittenInDigits(string format, int number, string expected)
    {
        NumberFormatter.Format(number, format).Should().Be(expected);
    }

    /// <summary>
    ///   The number furthest past the ceiling was the one the fallback could not catch: taking the
    ///   magnitude of <c>int.MinValue</c> overflows, because it has no positive counterpart, so the
    ///   guard threw where it was supposed to hand the number on to be written in digits.
    /// </summary>
    [Test]
    [Arguments("ROMAN")]
    [Arguments("roman")]
    [Arguments("ALPHABETIC")]
    [Arguments("alphabetic")]
    public void TheMostNegativeNumberFallsBackToDigitsLikeAnyOtherPastTheCeiling(string format)
    {
        NumberFormatter.Format(int.MinValue, format).Should().Be("-2147483648");
    }

    [Test]
    [Arguments(1, "A")]
    [Arguments(26, "Z")]
    [Arguments(27, "AA")]
    [Arguments(52, "ZZ")]
    [Arguments(53, "AAA")]
    public void ANumberPastZIsWrittenAsTheLetterRepeated(int number, string expected)
    {
        NumberFormatter.Format(number, "ALPHABETIC").Should().Be(expected);
    }

    [Test]
    public void ALowercaseLetterSequenceIsTheSameSequenceInLowercase()
    {
        NumberFormatter.Format(27, "alphabetic").Should().Be("aa");
    }

    /// <summary>
    ///   The empty string is what a numeric field's <c>Format</c> reads as when nothing set it, and
    ///   an unrecognised one is treated no differently: both mean ordinary digits.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("Roman")]
    [Arguments("not a format")]
    public void AFormatThatNamesNothingLeavesTheNumberInDigits(string format)
    {
        NumberFormatter.Format(42, format).Should().Be("42");
    }
}
