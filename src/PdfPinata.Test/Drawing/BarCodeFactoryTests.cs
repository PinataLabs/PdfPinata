using System;
using System.ComponentModel;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.BarCodes;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="BarCode.FromType(CodeType, string, XSize, CodeDirection)"/> against the enum it
///   claims to switch on.
/// </summary>
/// <remarks>
///   It handled two of the four values and answered the rest with
///   <see cref="InvalidEnumArgumentException"/> - the exception that means "that is not a member of
///   this enum", said about two members of the enum whose implementations are both in the same
///   folder. <c>Omr</c> is a bar code and simply had no case. <c>DataMatrix</c> is a
///   <see cref="MatrixCode"/>, which is not a <see cref="BarCode"/> at all, so it cannot come back
///   from a method typed this way and needs to say what to construct instead.
/// </remarks>
public class BarCodeFactoryTests
{
    private static readonly XSize Size = new XSize(120, 40);

    [Theory]
    [InlineData(CodeType.Code2of5Interleaved, typeof(Code2of5Interleaved))]
    [InlineData(CodeType.Code3of9Standard, typeof(Code3of9Standard))]
    [InlineData(CodeType.Omr, typeof(CodeOmr))]
    public void EveryCodeTypeThatIsABarCodeComesBackAsOne(CodeType type, Type expected)
    {
        var code = BarCode.FromType(type, "1234", Size, CodeDirection.LeftToRight);

        code.Should().BeOfType(expected);
    }

    [Fact]
    public void TheOmrCodeKeepsWhatItWasAskedFor()
    {
        // The two-line case is the one worth checking carries its arguments through rather than
        // being constructed with defaults, because nothing else here would notice.
        var code = BarCode.FromType(CodeType.Omr, "1010", Size, CodeDirection.RightToLeft);

        code.Text.Should().Be("1010");
        code.Size.Should().Be(Size);
        code.Direction.Should().Be(CodeDirection.RightToLeft);
    }

    [Fact]
    public void ADataMatrixIsRefusedByNameRatherThanAsAnUnknownEnumValue()
    {
        Action fromType = () => BarCode.FromType(CodeType.DataMatrix, "1234", Size);

        // Not InvalidEnumArgumentException: the caller passed a value the enum really has, and
        // being told otherwise sends them looking for a typo that is not there.
        fromType.Should().Throw<ArgumentException>()
            .Which.Should().NotBeOfType<InvalidEnumArgumentException>();
    }

    [Fact]
    public void TheDataMatrixRefusalNamesWhatToBuildAndWhatDrawsIt()
    {
        Action fromType = () => BarCode.FromType(CodeType.DataMatrix, "1234", Size);

        fromType.Should().Throw<ArgumentException>()
            .WithMessage("*CodeDataMatrix*")
            .And.Message.Should().Contain("DrawMatrixCode");
    }

    [Fact]
    public void AValueThatIsNotInTheEnumIsStillReportedAsOne()
    {
        Action fromType = () => BarCode.FromType((CodeType)999, "1234", Size);

        fromType.Should().Throw<InvalidEnumArgumentException>();
    }

    // ----- what interleaved 2 of 5 will carry -----

    // Its CheckCode was an empty method, and BcgSR.Invalid2Of5Code - which describes exactly the
    // rule it should have been enforcing - was written and called from nowhere. So a code the
    // symbology cannot carry was accepted where it was set and failed later inside the renderer:
    // IndexOutOfRangeException for an odd number of digits, FormatException for anything that is
    // not one. Neither names the code or the rule, and both arrive at drawing time.

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("20260816")]
    public void InterleavedTwoOfFiveTakesAnEvenNumberOfDigits(string code)
    {
        var act = () => new Code2of5Interleaved(code, Size);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1")]
    public void InterleavedTwoOfFiveRefusesAnOddNumberOfDigits(string code)
    {
        var act = () => new Code2of5Interleaved(code, Size);

        act.Should().Throw<ArgumentException>().WithMessage("*even number of digits*");
    }

    [Theory]
    [InlineData("12A4")]
    [InlineData("12 4")]
    [InlineData("-123")]
    public void InterleavedTwoOfFiveRefusesAnythingThatIsNotADigit(string code)
    {
        var act = () => new Code2of5Interleaved(code, Size);

        act.Should().Throw<ArgumentException>().WithMessage($"*{code}*");
    }

    [Fact]
    public void TheRefusalNamesTheCodeItRefused()
    {
        // The point of raising it where the code is set rather than where it is drawn: the caller
        // is told which value was wrong while they still have it in their hand.
        var act = () => new Code2of5Interleaved("ODD", Size);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*'ODD'*")
            .And.Message.Should().Contain("2 of 5");
    }
}
