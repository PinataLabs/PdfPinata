using System;
using System.Globalization;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;
using PdfIntegerValue = PdfPinata.Pdf.PdfInteger;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfIntegerValue"/> is a simple type holding an <see cref="int"/>, and implements
///   <see cref="IConvertible"/> explicitly, so every conversion is reached the way a caller reaches
///   it: through <see cref="Convert"/> or a cast to the interface. Each conversion answers what the
///   same conversion of the <see cref="int"/> it wraps would answer, overflow included.
/// </summary>
public class PdfIntegerTests
{
    [Fact]
    public void ANewIntegerIsZero()
    {
        var value = new PdfIntegerValue();

        value.Value.Should().Be(0);
        value.ToString().Should().Be("0");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(-42, "-42")]
    [InlineData(int.MaxValue, "2147483647")]
    [InlineData(int.MinValue, "-2147483648")]
    public void AnIntegerIsSpelledAsTheInvariantCultureSpellsIt(int number, string expected)
    {
        new PdfIntegerValue(number).ToString().Should().Be(expected);
    }

    [Fact]
    public void AnIntegerIsSpelledTheSameWhateverTheCurrentCulture()
    {
        // A culture with its own negative sign would put that sign into a content stream or a
        // dictionary, where only the invariant one is a number.
        var original = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;

            new PdfIntegerValue(-7).ToString().Should().Be("-7");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ItReportsItselfAsAThirtyTwoBitInteger()
    {
        new PdfIntegerValue(1).GetTypeCode().Should().Be(TypeCode.Int32);
        Convert.GetTypeCode(new PdfIntegerValue(1)).Should().Be(TypeCode.Int32);
    }

    [Fact]
    public void ItConvertsToEveryWiderNumericType()
    {
        var value = new PdfIntegerValue(-123456);

        Convert.ToInt32(value).Should().Be(-123456);
        Convert.ToInt64(value).Should().Be(-123456L);
        Convert.ToDouble(value).Should().Be(-123456d);
        Convert.ToSingle(value).Should().Be(-123456f);
        Convert.ToDecimal(value).Should().Be(-123456m);
    }

    [Fact]
    public void ItConvertsToANarrowerTypeWhenTheValueFits()
    {
        var value = new PdfIntegerValue(200);

        Convert.ToByte(value).Should().Be(200);
        Convert.ToInt16(value).Should().Be(200);
        Convert.ToUInt16(value).Should().Be(200);
        Convert.ToUInt32(value).Should().Be(200u);
        Convert.ToUInt64(value).Should().Be(200ul);
        Convert.ToChar(new PdfIntegerValue('A')).Should().Be('A');
    }

    [Fact]
    public void ANarrowingConversionOfAValueThatDoesNotFitOverflows()
    {
        // The same as converting the int itself, rather than a silent truncation.
        FluentActions.Invoking(() => Convert.ToByte(new PdfIntegerValue(256))).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => Convert.ToInt16(new PdfIntegerValue(40000))).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => Convert.ToUInt16(new PdfIntegerValue(-1))).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => Convert.ToUInt32(new PdfIntegerValue(-1))).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => Convert.ToUInt64(new PdfIntegerValue(-1))).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => Convert.ToChar(new PdfIntegerValue(-1))).Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(sbyte.MaxValue)]
    [InlineData(sbyte.MinValue)]
    public void ItConvertsToASignedByteWhenTheValueFits(int number)
    {
        // ToSByte used to throw InvalidCastException for every value, where every other narrowing
        // conversion went through Convert.
        Convert.ToSByte(new PdfIntegerValue(number)).Should().Be((sbyte)number);
    }

    [Theory]
    [InlineData(sbyte.MaxValue + 1)]
    [InlineData(sbyte.MinValue - 1)]
    public void ASignedByteConversionOfAValueThatDoesNotFitOverflows(int number)
    {
        FluentActions.Invoking(() => Convert.ToSByte(new PdfIntegerValue(number))).Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public void ItConvertsToABooleanTheWayAnIntegerDoes(int number, bool expected)
    {
        Convert.ToBoolean(new PdfIntegerValue(number)).Should().Be(expected);
    }

    [Fact]
    public void ItConvertsToAStringWithTheFormatProviderGiven()
    {
        var provider = new NumberFormatInfo { NegativeSign = "~" };

        Convert.ToString(new PdfIntegerValue(-5), provider).Should().Be("~5");
    }

    [Theory]
    [InlineData("{0:D5}", "00042")]
    [InlineData("{0:X}", "2A")]
    [InlineData("{0}", "42")]
    [InlineData("[{0,4}]", "[  42]")]
    public void ItFormatsAsTheIntegerItWrapsWould(string format, string expected)
    {
        // IFormattable used to answer the format string itself, and so nothing at all when there
        // was no format, as there is not in an interpolated string.
        string.Format(CultureInfo.InvariantCulture, format, new PdfIntegerValue(42)).Should().Be(expected);
    }

    [Fact]
    public void AnInterpolatedIntegerIsItsValue()
    {
        var value = new PdfIntegerValue(42);

        $"{value}".Should().Be("42");
        $"{value:N0}".Should().Be(42.ToString("N0"));
    }

    [Fact]
    public void ItFormatsWithTheFormatProviderGiven()
    {
        IFormattable value = new PdfIntegerValue(-5);
        var provider = new NumberFormatInfo { NegativeSign = "~" };

        value.ToString("D3", provider).Should().Be("~005");
        value.ToString(null, provider).Should().Be("~5");
    }

    [Fact]
    public void ToTypeConvertsAsTheIntegerItWrapsWould()
    {
        IConvertible value = new PdfIntegerValue(65);

        value.ToType(typeof(long), CultureInfo.InvariantCulture).Should().Be(65L);
        value.ToType(typeof(double), CultureInfo.InvariantCulture).Should().Be(65d);
        value.ToType(typeof(string), CultureInfo.InvariantCulture).Should().Be("65");
        FluentActions.Invoking(() => value.ToType(typeof(Guid), CultureInfo.InvariantCulture))
            .Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ChangeTypeReachesTheConversionsToo()
    {
        Convert.ChangeType(new PdfIntegerValue(12), typeof(decimal), CultureInfo.InvariantCulture)
            .Should().Be(12m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1234567)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void AnIntegerComesBackFromTheFileAsTheSameInteger(int number)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Internals.Catalog.Elements["/TestValue"] = new PdfIntegerValue(number);

        var saved = Saved.Bytes(document);
        Encoding.Latin1.GetString(saved).Should().Contain(
            "/TestValue " + number.ToString(CultureInfo.InvariantCulture));

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Import);
        var item = reread.Internals.Catalog.Elements["/TestValue"];
        item.Should().BeOfType<PdfIntegerValue>().Which.Value.Should().Be(number);
        reread.Internals.Catalog.Elements.GetInteger("/TestValue").Should().Be(number);
    }

    [Fact]
    public void ConvertingToADateTimeIsRefusedAsItIsForTheIntItWraps()
    {
        // It used to answer DateTime.MinValue, a date nobody asked for, where Int32 throws.
        IConvertible value = new PdfIntegerValue(42);

        var convert = () => value.ToDateTime(null);
        var throughConvert = () => Convert.ToDateTime(value);

        convert.Should().Throw<InvalidCastException>().WithMessage("*PdfInteger*DateTime*");
        throughConvert.Should().Throw<InvalidCastException>();
        ((Func<DateTime>)(() => Convert.ToDateTime(42))).Should().Throw<InvalidCastException>();
    }
}
