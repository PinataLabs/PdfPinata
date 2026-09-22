using System;
using System.Globalization;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfUInteger"/> is a simple type holding a <see cref="uint"/>. Unlike
///   <see cref="PdfPinata.Pdf.PdfInteger"/> its <see cref="IConvertible"/> members are public, and
///   each answers what the same conversion of the <see cref="uint"/> it wraps would answer.
///
///   <para>
///   The lexer never reads a number as unsigned: what fits an <see cref="int"/> comes back as a
///   <see cref="PdfPinata.Pdf.PdfInteger"/> and what does not as a <see cref="PdfLong"/>, so a
///   round trip keeps the number and not the type. <c>ToSByte</c> is left out for the reason the
///   class comment of <see cref="PdfIntegerTests"/> gives.
///   </para>
/// </summary>
public class PdfUIntegerTests
{
    [Fact]
    public void ANewUnsignedIntegerIsZero()
    {
        var value = new PdfUInteger();

        value.Value.Should().Be(0u);
        value.ToString().Should().Be("0");
    }

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(42u, "42")]
    [InlineData(uint.MaxValue, "4294967295")]
    public void AnUnsignedIntegerIsSpelledAsTheInvariantCultureSpellsIt(uint number, string expected)
    {
        new PdfUInteger(number).ToString().Should().Be(expected);
    }

    [Fact]
    public void ConvertingToADateTimeIsRefusedAsItIsForTheUIntItWraps()
    {
        // It used to answer DateTime.MinValue, a date nobody asked for, where UInt32 throws.
        var value = new PdfUInteger(42);

        var convert = () => value.ToDateTime(null);
        var throughConvert = () => Convert.ToDateTime(value);

        convert.Should().Throw<InvalidCastException>().WithMessage("*PdfUInteger*DateTime*");
        throughConvert.Should().Throw<InvalidCastException>();
        ((Func<DateTime>)(() => Convert.ToDateTime(42u))).Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ItReportsItselfAsAThirtyTwoBitUnsignedInteger()
    {
        new PdfUInteger(1).GetTypeCode().Should().Be(TypeCode.UInt32);
    }

    [Fact]
    public void ItConvertsToEveryTypeWideEnoughForItsWholeRange()
    {
        var value = new PdfUInteger(uint.MaxValue);

        value.ToUInt32(null).Should().Be(uint.MaxValue);
        value.ToUInt64(null).Should().Be(4294967295ul);
        value.ToInt64(null).Should().Be(4294967295L);
        value.ToDouble(null).Should().Be(4294967295d);
        value.ToSingle(null).Should().Be(4294967295f);
        value.ToDecimal(null).Should().Be(4294967295m);
    }

    [Fact]
    public void ItConvertsToANarrowerTypeWhenTheValueFits()
    {
        var value = new PdfUInteger(200);

        value.ToByte(null).Should().Be(200);
        value.ToInt16(null).Should().Be(200);
        value.ToUInt16(null).Should().Be(200);
        value.ToInt32(null).Should().Be(200);
        new PdfUInteger('Z').ToChar(null).Should().Be('Z');
    }

    [Fact]
    public void ANarrowingConversionOfAValueThatDoesNotFitOverflows()
    {
        var large = new PdfUInteger(uint.MaxValue);

        FluentActions.Invoking(() => large.ToInt32(null)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => large.ToInt16(null)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => large.ToUInt16(null)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => large.ToByte(null)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => large.ToChar(null)).Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(uint.MaxValue, true)]
    public void ItConvertsToABooleanTheWayAnIntegerDoes(uint number, bool expected)
    {
        new PdfUInteger(number).ToBoolean(null).Should().Be(expected);
    }

    [Fact]
    public void ItConvertsToAStringThroughTheInterface()
    {
        IConvertible value = new PdfUInteger(3000000000u);

        value.ToString(CultureInfo.InvariantCulture).Should().Be("3000000000");
        Convert.ToString(new PdfUInteger(7u), CultureInfo.InvariantCulture).Should().Be("7");
    }

    [Fact]
    public void ToTypeConvertsAsTheUnsignedIntegerItWrapsWould()
    {
        var value = new PdfUInteger(3000000000u);

        value.ToType(typeof(long), CultureInfo.InvariantCulture).Should().Be(3000000000L);
        value.ToType(typeof(string), CultureInfo.InvariantCulture).Should().Be("3000000000");
        FluentActions.Invoking(() => value.ToType(typeof(int), CultureInfo.InvariantCulture))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void AnUnsignedIntegerWithinTheRangeOfAnIntegerIsReadBackAsOne()
    {
        var item = RoundTrip(new PdfUInteger(7u), out var written);

        written.Should().Contain("/TestValue 7");
        item.Should().BeOfType<PdfPinata.Pdf.PdfInteger>().Which.Value.Should().Be(7);
    }

    [Fact]
    public void AnUnsignedIntegerBeyondTheRangeOfAnIntegerIsReadBackAsALong()
    {
        var item = RoundTrip(new PdfUInteger(uint.MaxValue), out var written);

        written.Should().Contain("/TestValue 4294967295");
        item.Should().BeOfType<PdfLong>().Which.Value.Should().Be(uint.MaxValue);
    }

    private static PdfItem RoundTrip(PdfUInteger value, out string written)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Internals.Catalog.Elements["/TestValue"] = value;

        using var output = new MemoryStream();
        document.Save(output, false);
        var saved = output.ToArray();
        written = Encoding.Latin1.GetString(saved);

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Import);
        return reread.Internals.Catalog.Elements["/TestValue"];
    }
}
