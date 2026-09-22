using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs;

public class PdfDateTests
{
    // format for PDF date is generally D:YYYYMMDDHHmmSSOHH'mm'

    [Fact]
    public void ParseDateString_WithTimezoneOffset()
    {
        var pdfDate = new PdfDate("D:19981223195200-02'00'");
        var expectedDateWithOffset =
            new DateTimeOffset(new DateTime(1998, 12, 23, 19, 52, 0), new TimeSpan(-2, 0, 0));
        pdfDate.Value.ToUniversalTime().Should().Be(expectedDateWithOffset.UtcDateTime);
    }

    [Fact]
    public void ParseDateString_WithNoOffset()
    {
        var pdfDate = new PdfDate("D:19981223195200Z");
        var expectedDateWithOffset =
            new DateTimeOffset(new DateTime(1998, 12, 23, 19, 52, 0), new TimeSpan(0, 0, 0));
        pdfDate.Value.ToUniversalTime().Should().Be(expectedDateWithOffset.UtcDateTime);
    }

    // What each shape of date string reads as, pinned before the parser stopped using exceptions
    // to say a date was malformed. Every one of these is what the throwing version answered.

    [Theory]
    [InlineData("D:19981223195200-08'00'", "1998-12-24T03:52:00")]  // west of UT: added
    [InlineData("D:19981223195200+05'30'", "1998-12-23T14:22:00")]  // east of UT: subtracted
    [InlineData("D:19981223195200Z00'00'", "1998-12-23T19:52:00")]
    [InlineData("D:19981223195200Z", "1998-12-23T19:52:00")]        // too short for an offset
    [InlineData("D:19981223195200+05", "1998-12-23T19:52:00")]      // an offset short of its minutes is ignored
    [InlineData("D:19981223", "1998-12-23T00:00:00")]               // the date alone
    [InlineData("D:199812231952", "1998-12-23T00:00:00")]           // a time short of its seconds is ignored
    [InlineData("D:19980023", "1998-01-23T00:00:00")]               // month 0 is taken as January
    [InlineData("D:19981323", "1998-12-23T00:00:00")]               // and month 13 as December
    [InlineData("D:1998 1 3", "1998-01-03T00:00:00")]               // a digit padded with a space
    public void APdfDateIsReadAsUniversalTime(string text, string expected)
    {
        var value = new PdfDate(text).Value;

        value.Should().Be(DateTime.Parse(expected, CultureInfo.InvariantCulture));
        value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ADateInPlainEnglishIsReadTheWayTheInvariantCultureReadsIt()
    {
        // Some libraries write this rather than the PDF format. Nothing says which zone it is in.
        var value = new PdfDate("12/23/1998 19:52:00").Value;

        value.Should().Be(new DateTime(1998, 12, 23, 19, 52, 0));
        value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Theory]
    [InlineData("D:1998")]                     // too short to hold a whole date
    [InlineData("D:1998AB23")]                 // a month that is not a number
    [InlineData("D:19980230")]                 // a day the month does not have
    [InlineData("D:00001223")]                 // a year DateTime cannot hold
    [InlineData("D:19981223250000")]           // an hour past the end of the day
    [InlineData("D:19981223196000")]           // a minute past the end of the hour
    [InlineData("D:19981223195260")]           // a second past the end of the minute
    [InlineData("D:19981223195200+AB'00'")]    // an offset that is not a number
    [InlineData("D:99991231235900-08'00'")]    // an offset that carries it past the last date
    [InlineData("D:00010101000000+08'00'")]    // or before the first one
    [InlineData("not a date at all")]
    [InlineData("")]
    public void ADateThatCannotBeReadIsTheEarliestDate(string text)
    {
        new PdfDate(text).Value.Should().Be(DateTime.MinValue);
    }
}
