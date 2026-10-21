using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Pdf;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

public class PdfDateTests
{
    // format for PDF date is generally D:YYYYMMDDHHmmSSOHH'mm'

    [Test]
    public void ParseDateString_WithTimezoneOffset()
    {
        var pdfDate = new PdfDate("D:19981223195200-02'00'");
        var expectedDateWithOffset =
            new DateTimeOffset(new DateTime(1998, 12, 23, 19, 52, 0), new TimeSpan(-2, 0, 0));
        pdfDate.Value.ToUniversalTime().Should().Be(expectedDateWithOffset.UtcDateTime);
    }

    [Test]
    public void ParseDateString_WithNoOffset()
    {
        var pdfDate = new PdfDate("D:19981223195200Z");
        var expectedDateWithOffset =
            new DateTimeOffset(new DateTime(1998, 12, 23, 19, 52, 0), new TimeSpan(0, 0, 0));
        pdfDate.Value.ToUniversalTime().Should().Be(expectedDateWithOffset.UtcDateTime);
    }

    // What each shape of date string reads as, pinned before the parser stopped using exceptions
    // to say a date was malformed. Every one of these is what the throwing version answered.

    [Test]
    [Arguments("D:19981223195200-08'00'", "1998-12-24T03:52:00")]  // west of UT: added
    [Arguments("D:19981223195200+05'30'", "1998-12-23T14:22:00")]  // east of UT: subtracted
    [Arguments("D:19981223195200Z00'00'", "1998-12-23T19:52:00")]
    [Arguments("D:19981223195200Z", "1998-12-23T19:52:00")]        // too short for an offset
    [Arguments("D:19981223195200+05", "1998-12-23T14:52:00")]      // an offset short of its minutes has none
    [Arguments("D:20240101120000-08", "2024-01-01T20:00:00")]
    [Arguments("D:20240101120000+05'00", "2024-01-01T07:00:00")]   // ISO 32000-2: no closing apostrophe
    [Arguments("D:20240101120000+05'00'", "2024-01-01T07:00:00")]  // ISO 32000-1: one
    [Arguments("D:20240101120000+05'", "2024-01-01T07:00:00")]     // hours and their apostrophe alone
    [Arguments("20240101120000Z", "2024-01-01T12:00:00")]          // no D: prefix
    [Arguments("20240101120000+05'00'", "2024-01-01T07:00:00")]
    [Arguments("D:2024", "2024-01-01T00:00:00")]                   // every field after the year is optional
    [Arguments("D:202403", "2024-03-01T00:00:00")]
    [Arguments("D:2024030515", "2024-03-05T15:00:00")]
    [Arguments("D:202401011200Z", "2024-01-01T12:00:00")]          // an offset after a time short of its seconds
    [Arguments("D:19981223", "1998-12-23T00:00:00")]               // the date alone
    [Arguments("D:199812231952", "1998-12-23T19:52:00")]           // a time short of its seconds has none
    [Arguments("D:19980023", "1998-01-23T00:00:00")]               // month 0 is taken as January
    [Arguments("D:19981323", "1998-12-23T00:00:00")]               // and month 13 as December
    [Arguments("D:1998 1 3", "1998-01-03T00:00:00")]               // a digit padded with a space
    public void APdfDateIsReadAsUniversalTime(string text, string expected)
    {
        var value = new PdfDate(text).Value;

        value.Should().Be(DateTime.Parse(expected, CultureInfo.InvariantCulture));
        value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void ADateInPlainEnglishIsReadTheWayTheInvariantCultureReadsIt()
    {
        // Some libraries write this rather than the PDF format. Nothing says which zone it is in.
        var value = new PdfDate("12/23/1998 19:52:00").Value;

        value.Should().Be(new DateTime(1998, 12, 23, 19, 52, 0));
        value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Test]
    [Arguments("D:199")]                      // too short to hold a year
    [Arguments("D:19981")]                    // a field cut off halfway
    [Arguments("D:1998122")]
    [Arguments("D:19981223195200X")]          // a designator that is none of +, - and Z
    [Arguments("D:19981223195200+")]          // a designator with no hours after it
    [Arguments("D:19981223195200+5")]         // hours of one digit
    [Arguments("D:19981223195200+05'0")]      // minutes of one digit
    [Arguments("D:19981223195200+05:00")]     // a separator that is not an apostrophe, unclosed
    [Arguments("D:19981223195200+05'00x")]    // something after the minutes that is not an apostrophe
    [Arguments("D:19981223195200-08'60")]     // an hour's worth of minutes, unclosed
    [Arguments("D:19981223195200+24")]        // a whole day of hours, without minutes
    [Arguments("D:1998AB23")]                 // a month that is not a number
    [Arguments("D:19980230")]                 // a day the month does not have
    [Arguments("D:00001223")]                 // a year DateTime cannot hold
    [Arguments("D:19981223250000")]           // an hour past the end of the day
    [Arguments("D:19981223196000")]           // a minute past the end of the hour
    [Arguments("D:19981223195260")]           // a second past the end of the minute
    [Arguments("D:19981223195200+AB'00'")]    // an offset that is not a number
    [Arguments("D:99991231235900-08'00'")]    // an offset that carries it past the last date
    [Arguments("D:00010101000000+08'00'")]    // or before the first one
    [Arguments("D:19981223195200X08'00'")]    // a designator that is none of +, - and Z
    [Arguments("D:19981223195200+24'00'")]    // an offset of a whole day
    [Arguments("D:19981223195200+08'60'")]    // an offset of an hour's worth of minutes
    [Arguments("D:19981223195200+-8'00'")]    // an offset with a sign of its own
    [Arguments("D:19981223195200+08:00'")]    // a separator that is not an apostrophe
    [Arguments("D:19981223195200+08'00:")]    // nor the one after the minutes
    [Arguments("not a date at all")]
    [Arguments("")]
    public void ADateThatCannotBeReadIsTheEarliestDate(string text)
    {
        new PdfDate(text).Value.Should().Be(DateTime.MinValue);
    }
}
