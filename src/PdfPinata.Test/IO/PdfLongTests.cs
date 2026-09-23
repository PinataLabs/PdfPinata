using System;
using System.Globalization;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfLong"/> is what the lexer makes of an integer too wide for 32 bits - a byte
///   offset past 2 GB, or the <c>/C 264584027963392</c> an AutoCAD file was once found to carry.
///   It has to survive being read and written, and converting it has to give back the number.
/// </summary>
public class PdfLongTests
{
    private const long WiderThanAnInt = 5_000_000_000;

    [Fact]
    public void AnIntegerWiderThan32BitsIsReadBackAsAPdfLong()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Info.Elements["/Wide"] = new PdfLong(WiderThanAnInt);
        using var stream = new MemoryStream();
        document.Save(stream, false);

        var reopened = Pdf.IO.PdfReader.Open(new MemoryStream(stream.ToArray(), false), PdfDocumentOpenMode.Import);

        reopened.Info.Elements["/Wide"].Should().BeOfType<PdfLong>()
            .Which.Value.Should().Be(WiderThanAnInt);
    }

    [Fact]
    public void ItReadsAsItsDigits()
    {
        new PdfLong(WiderThanAnInt).ToString().Should().Be("5000000000");
    }

    [Fact]
    public void ANewPdfLongIsZero()
    {
        new PdfLong().Value.Should().Be(0);
    }

    [Fact]
    public void ConvertingItGivesBackTheNumber()
    {
        var wide = new PdfLong(WiderThanAnInt);
        var small = new PdfLong(65);

        Convert.ToInt64(wide).Should().Be(WiderThanAnInt);
        Convert.ToUInt64(wide).Should().Be(5_000_000_000UL);
        Convert.ToUInt32(small).Should().Be(65U);
        Convert.ToInt32(small).Should().Be(65);
        Convert.ToInt16(small).Should().Be(65);
        Convert.ToUInt16(small).Should().Be(65);
        Convert.ToByte(small).Should().Be(65);
        Convert.ToSByte(small).Should().Be(65);
        Convert.ToSByte(new PdfLong(-128)).Should().Be(-128);
        Convert.ToChar(small).Should().Be('A');
        Convert.ToBoolean(small).Should().BeTrue();
        Convert.ToBoolean(new PdfLong(0)).Should().BeFalse();
        Convert.ToDouble(wide).Should().Be(5e9);
        Convert.ToSingle(wide).Should().Be(5e9f);
        Convert.ToDecimal(wide).Should().Be(5_000_000_000m);
        Convert.ToString(wide, CultureInfo.InvariantCulture).Should().Be("5000000000");
    }

    [Fact]
    public void ConvertingToADateTimeIsRefusedAsItIsForTheLongItWraps()
    {
        // It used to read the number as ticks, so a number too wide for an int converted and one
        // that fits, read as a PdfInteger, threw.
        var value = new PdfLong(WiderThanAnInt);

        var convert = () => value.ToDateTime(null);
        var throughConvert = () => Convert.ToDateTime(value);

        convert.Should().Throw<InvalidCastException>().WithMessage("*PdfLong*DateTime*");
        throughConvert.Should().Throw<InvalidCastException>();
        ((Func<DateTime>)(() => Convert.ToDateTime(WiderThanAnInt))).Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ANumberTooWideForTheTargetOverflowsRatherThanWrapping()
    {
        var wide = new PdfLong(WiderThanAnInt);

        ((Action)(() => _ = Convert.ToInt32(wide))).Should().Throw<OverflowException>();
        ((Action)(() => _ = Convert.ToUInt32(wide))).Should().Throw<OverflowException>();
        ((Action)(() => _ = Convert.ToSByte(new PdfLong(128)))).Should().Throw<OverflowException>();
    }
}
