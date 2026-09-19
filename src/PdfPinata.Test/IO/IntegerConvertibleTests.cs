using System;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The three integer types each wrap one primitive and are <see cref="IConvertible"/> through
///   it. <c>PdfUInteger</c> and <c>PdfLong</c> were copied from <c>PdfInteger</c> and kept its
///   <see cref="TypeCode.Int32"/>, so a caller that switched on the type code asked a 64-bit value
///   for 32 bits and overflowed; and all three answered <c>ToType</c> with null, which
///   <see cref="IConvertible"/> does not allow - a conversion either succeeds or throws.
///   See https://github.com/PinataLabs/PdfPinata/issues/18.
/// </summary>
public class IntegerConvertibleTests
{
    // Each row names the wrapper rather than holding one, so that the rows are serializable and
    // Test Explorer lists them one by one.
    public static TheoryData<string, TypeCode> TypeCodes => new()
    {
        { nameof(Pdf.PdfInteger), TypeCode.Int32 },
        { nameof(PdfUInteger), TypeCode.UInt32 },
        { nameof(PdfLong), TypeCode.Int64 },
    };

    static IConvertible Number(string wrapper) => wrapper switch
    {
        nameof(Pdf.PdfInteger) => new Pdf.PdfInteger(-7),
        nameof(PdfUInteger) => new PdfUInteger(3_000_000_000),
        nameof(PdfLong) => new PdfLong(5_000_000_000),
        _ => throw new ArgumentOutOfRangeException(nameof(wrapper), wrapper, null),
    };

    [Theory]
    [MemberData(nameof(TypeCodes))]
    public void TheTypeCodeNamesThePrimitiveTheNumberIsHeldIn(string wrapper, TypeCode expected)
    {
        var number = Number(wrapper);

        number.GetTypeCode().Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(TypeCodes))]
    public void ConvertingByTheTypeCodeGivesBackTheNumberWithoutOverflowing(string wrapper, TypeCode _)
    {
        var number = Number(wrapper);

        // What a caller switching on the type code does: ask for the primitive it names. A value
        // wider than 32 bits used to be asked for 32 bits, and threw.
        object value = number.GetTypeCode() switch
        {
            TypeCode.Int32 => number.ToInt32(null),
            TypeCode.UInt32 => number.ToUInt32(null),
            TypeCode.Int64 => number.ToInt64(null),
            var other => throw new InvalidOperationException($"No arm for {other}."),
        };

        Convert.ToDecimal(value).Should().Be(number.ToDecimal(null));
    }

    [Theory]
    [MemberData(nameof(TypeCodes))]
    public void ToTypeConvertsAsTheWrappedPrimitiveWould(string wrapper, TypeCode _)
    {
        var number = Number(wrapper);

        number.ToType(typeof(decimal), null).Should().Be(number.ToDecimal(null));
        number.ToType(typeof(double), null).Should().Be(number.ToDouble(null));
        number.ToType(typeof(string), null).Should().Be(number.ToString(null));
    }

    [Theory]
    [MemberData(nameof(TypeCodes))]
    public void ToTypeRefusesATypeTheNumberCannotBecomeRatherThanAnsweringNull(string wrapper, TypeCode _)
    {
        var number = Number(wrapper);
        var convert = () => number.ToType(typeof(Uri), null);

        convert.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ToTypeOverflowsRatherThanWrapping()
    {
        IConvertible wide = new PdfLong(5_000_000_000);

        var convert = () => wide.ToType(typeof(int), null);

        convert.Should().Throw<OverflowException>();
    }
}
