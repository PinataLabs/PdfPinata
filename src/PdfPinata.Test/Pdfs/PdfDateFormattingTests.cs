using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Metadata;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   How a date is written, into <c>/Info</c> and every other date entry through
///   <see cref="PdfDate"/>, and into the XMP packet through <see cref="XmpMetadata"/>.
/// </summary>
/// <remarks>
///   <see cref="PdfDate"/> formatted in the current culture, so a thread running under th-TH wrote
///   the year from the Buddhist calendar - 2569 for 2026. And the two writers worked out the offset
///   each in their own way: <c>zzz</c> writes the local offset for a date of unspecified kind and
///   <c>K</c> writes none, so the same instant was described two ways, which PDF/A does not allow.
/// </remarks>
public class PdfDateFormattingTests
{
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";

    private static T UnderCulture<T>(string name, Func<T> action)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static string CreateDateIn(XmpMetadata metadata)
    {
        using var stream = new MemoryStream(metadata.Build());
        return XDocument.Load(stream).Descendants(Xmp + "CreateDate").Single().Value;
    }

    [Fact]
    public void APdfDateIsWrittenInTheGregorianCalendarWhateverTheCurrentCulture()
    {
        var instant = new DateTime(2026, 9, 26, 10, 30, 15, DateTimeKind.Utc);

        var text = UnderCulture("th-TH", () => new PdfDate(instant).ToString());

        text.Should().Be("D:20260926103015+00'00'");
    }

    [Fact]
    public void AnXmpDateIsWrittenInTheGregorianCalendarWhateverTheCurrentCulture()
    {
        var instant = new DateTime(2026, 9, 26, 10, 30, 15, DateTimeKind.Utc);

        var text = UnderCulture("th-TH", () => CreateDateIn(new XmpMetadata { CreationDate = instant }));

        text.Should().Be("2026-09-26T10:30:15Z");
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void InfoAndXmpDescribeTheSameInstant(DateTimeKind kind)
    {
        var document = new PdfDocument();
        document.Info.CreationDate = new DateTime(2026, 1, 15, 10, 0, 0, kind);

        var info = new PdfDate(document.Info.CreationDate).ToString();
        var xmp = CreateDateIn(XmpMetadata.FromDocument(document));

        // Neither may leave its offset to the reader, and both must name the same one.
        var infoOffset = info.Substring("D:yyyyMMddHHmmss".Length).TrimEnd('\'').Replace('\'', ':');
        var xmpOffset = xmp.Substring("yyyy-MM-ddTHH:mm:ss".Length);
        xmpOffset.Should().NotBeEmpty();
        (xmpOffset == "Z" ? "+00:00" : xmpOffset).Should().Be(infoOffset);

        new PdfDate(info).Value.Should().Be(
            DateTimeOffset.Parse(xmp, CultureInfo.InvariantCulture).UtcDateTime);
    }
}
