using System;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Everything the library stamps a document with - a creation date, a modification date, an
///   annotation's date, a signing time - used to read <c>DateTime.Now</c> where it stood, so a
///   caller who needed a document to come out the same twice had no way to say what the time was.
///   <see cref="GlobalTimeSettings"/> is that seam.
///   <para>
///   The clock is one static for the whole application domain, so these tests restore it in a
///   <c>finally</c> and live in a collection that does not run beside anything else.
///   </para>
/// </summary>
[Collection(ClockCollection.Name)]
public class GlobalTimeSettingsTests
{
    private static readonly DateTime AFixedTime = new DateTime(2019, 7, 16, 13, 45, 22);

    [Fact]
    public void TheClockReadsTheSystemClockUntilOneIsSet()
    {
        var before = DateTime.Now;
        var reading = GlobalTimeSettings.Now;

        reading.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.Now);
    }

    [Fact]
    public void ADocumentIsStampedWithTheTimeTheClockReports()
    {
        try
        {
            GlobalTimeSettings.Clock = () => AFixedTime;

            var document = new PdfDocument();

            document.Info.CreationDate.Should().Be(AFixedTime);
        }
        finally
        {
            GlobalTimeSettings.Clock = null;
        }
    }

    /// <summary>
    ///   Two documents created a moment apart carry the same date when the clock is fixed, which is
    ///   the point of fixing it: it is what lets a caller compare one run's output against another's.
    /// </summary>
    [Fact]
    public void TwoDocumentsCreatedUnderAFixedClockCarryTheSameDate()
    {
        try
        {
            GlobalTimeSettings.Clock = () => AFixedTime;

            var first = new PdfDocument();
            var second = new PdfDocument();

            second.Info.CreationDate.Should().Be(first.Info.CreationDate);
        }
        finally
        {
            GlobalTimeSettings.Clock = null;
        }
    }

    [Fact]
    public void SettingTheClockToNullPutsTheSystemClockBack()
    {
        GlobalTimeSettings.Clock = () => AFixedTime;

        GlobalTimeSettings.Clock = null;

        var before = DateTime.Now;
        GlobalTimeSettings.Now.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.Now);
    }

    [Fact]
    public void TheClockIsReadEachTimeRatherThanOnce()
    {
        try
        {
            var readings = 0;
            GlobalTimeSettings.Clock = () => AFixedTime.AddSeconds(readings++);

            var first = GlobalTimeSettings.Now;
            var second = GlobalTimeSettings.Now;

            first.Should().Be(AFixedTime);
            second.Should().Be(AFixedTime.AddSeconds(1));
        }
        finally
        {
            GlobalTimeSettings.Clock = null;
        }
    }
}
